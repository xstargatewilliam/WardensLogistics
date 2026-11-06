using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;
using WardensLogistics.Core;

namespace WardensLogistics.Logistica
{
    /// <summary>
    /// Raio-x da logística: lê o estado da logística de drones do jogo e aponta a causa
    /// de cada problema, não só o sintoma. Funciona no jogo solo e para o anfitrião
    /// (é o servidor quem guarda as tarefas e os drones).
    ///
    /// Problemas detectados, do mais grave para o mais leve:
    /// - há pedidos, mas nenhuma estação de drones no planeta;
    /// - o planeta não tem energia suficiente, então os drones ficam parados;
    /// - um item é pedido e nenhum baú o oferece;
    /// - um item é pedido, mas os baús que o oferecem estão vazios;
    /// - muitas entregas esperando e nenhum drone livre nas estações;
    /// - um baú de oferta está cheio de algo que ninguém pede.
    /// </summary>
    internal static class XRay
    {
        internal enum Severity { Critical, Warning }

        internal sealed class Issue
        {
            public Severity Level;
            public Sprite Icon;
            public string Text;
            public readonly List<Vector3> Places = new List<Vector3>();
        }

        internal sealed class Report
        {
            public bool Available;          // false: cliente em multiplayer
            public bool Energy = true;
            public int Stations;
            public int DronesInStations;
            public int DronesFlying;
            public int TasksWaiting;        // sem drone atribuído
            public int TasksToSupply;       // drone indo buscar
            public int TasksToDemand;       // drone entregando
            public int DemandContainers;
            public int SupplyContainers;
            public readonly List<Issue> Issues = new List<Issue>();
        }

        // Fila de entregas considerada "travada" se nenhum drone estiver livre.
        private const int WaitingTasksThreshold = 5;

        private static readonly AccessTools.FieldRef<LogisticManager, List<Inventory>> DemandsRef =
            AccessTools.FieldRefAccess<LogisticManager, List<Inventory>>("_demandInventories");
        private static readonly AccessTools.FieldRef<LogisticManager, List<Inventory>> SuppliesRef =
            AccessTools.FieldRefAccess<LogisticManager, List<Inventory>>("_supplyInventories");
        private static readonly AccessTools.FieldRef<LogisticManager, HashSet<Drone>> FleetRef =
            AccessTools.FieldRefAccess<LogisticManager, HashSet<Drone>>("_droneFleet");
        private static readonly AccessTools.FieldRef<LogisticManager, HashSet<MachineDroneStation>> StationsRef =
            AccessTools.FieldRefAccess<LogisticManager, HashSet<MachineDroneStation>>("_allDroneStations");

        public static Report Analyze()
        {
            var report = new Report();
            var lm = Managers.GetManager<LogisticManager>();
            var planetLoader = Managers.GetManager<PlanetLoader>();
            if (lm == null || planetLoader == null || planetLoader.GetCurrentPlanetData() == null) return report;
            if (!lm.IsServer) return report; // dados de logística só existem no servidor
            report.Available = true;

            int planet = planetLoader.GetCurrentPlanetData().GetPlanetHash();

            // Estações, drones e energia
            foreach (var station in StationsRef(lm))
            {
                if (station == null || station.GetPlanetHash() != planet) continue;
                report.Stations++;
                report.DronesInStations += station.GetNumberOfDronesInStation();
            }
            foreach (var drone in FleetRef(lm))
                if (drone != null && drone.GetDronePlanetHash() == planet) report.DronesFlying++;
            var energy = Managers.GetManager<RequireEnergyHandler>();
            report.Energy = energy == null || energy.HasEnoughEnergy(planet);

            // Tarefas por estado, e grupos que já estão sendo entregues
            var groupsInDelivery = new HashSet<Group>();
            foreach (var task in lm.GetAllCurrentTasks().Values)
            {
                if (task == null || task.GetPlanetHash() != planet) continue;
                switch (task.GetTaskState())
                {
                    case LogisticData.TaskState.NotAttributed: report.TasksWaiting++; break;
                    case LogisticData.TaskState.ToSupply:
                    case LogisticData.TaskState.Loading: report.TasksToSupply++; break;
                    case LogisticData.TaskState.ToDemand:
                    case LogisticData.TaskState.Unloading: report.TasksToDemand++; break;
                }
                var moved = task.GetWorldObjectToMove();
                if (moved != null && moved.GetGroup() != null) groupsInDelivery.Add(moved.GetGroup());
            }

            // Pedidos e ofertas deste planeta
            var demandsByGroup = new Dictionary<Group, List<Inventory>>();
            var allDemanded = new HashSet<Group>();
            foreach (var inv in DemandsRef(lm))
            {
                if (!OnPlanet(inv, planet)) continue;
                report.DemandContainers++;
                foreach (var g in inv.GetLogisticEntity().GetDemandGroups())
                {
                    if (g == null) continue;
                    allDemanded.Add(g);
                    if (inv.IsFull()) continue; // baú cheio não está esperando nada agora
                    if (!demandsByGroup.TryGetValue(g, out var list)) demandsByGroup[g] = list = new List<Inventory>();
                    list.Add(inv);
                }
            }

            var suppliersByGroup = new Dictionary<Group, List<Inventory>>();
            var stockByGroup = new Dictionary<Group, int>();
            var supplies = new List<Inventory>();
            foreach (var inv in SuppliesRef(lm))
            {
                if (!OnPlanet(inv, planet)) continue;
                supplies.Add(inv);
                report.SupplyContainers++;
                foreach (var g in inv.GetLogisticEntity().GetSupplyGroups())
                {
                    if (g == null) continue;
                    if (!suppliersByGroup.TryGetValue(g, out var list)) suppliersByGroup[g] = list = new List<Inventory>();
                    list.Add(inv);
                }
                foreach (var wo in inv.GetInsideWorldObjects())
                {
                    var g = wo?.GetGroup();
                    if (g == null || !inv.GetLogisticEntity().GetSupplyGroups().Contains(g)) continue;
                    stockByGroup.TryGetValue(g, out int n);
                    stockByGroup[g] = n + 1;
                }
            }

            // 1. Sem estação de drones
            if (demandsByGroup.Count > 0 && report.Stations == 0)
                Add(report, Severity.Critical, null, Text.Get("xray.s.no_station"));

            // 2. Sem energia
            if (!report.Energy && (report.DronesFlying > 0 || report.TasksWaiting + report.TasksToSupply + report.TasksToDemand > 0))
                Add(report, Severity.Critical, null, Text.Get("xray.s.no_energy"));

            // 3 e 4. Pedidos sem fornecedor ou com fornecedor vazio
            foreach (var pair in demandsByGroup)
            {
                var g = pair.Key;
                if (groupsInDelivery.Contains(g)) continue; // já há entrega a caminho
                string name = Readable.GetGroupName(g);
                if (!suppliersByGroup.TryGetValue(g, out var suppliers))
                {
                    var issue = Add(report, Severity.Critical, g.GetImage(), Text.Get("xray.s.no_supplier", name));
                    foreach (var inv in pair.Value) issue.Places.Add(PositionOf(inv));
                }
                else if (!stockByGroup.ContainsKey(g))
                {
                    var issue = Add(report, Severity.Warning, g.GetImage(), Text.Get("xray.s.supplier_empty", name));
                    foreach (var inv in suppliers) issue.Places.Add(PositionOf(inv));
                }
            }

            // 5. Entregas esperando e nenhum drone livre
            if (report.TasksWaiting >= WaitingTasksThreshold && report.DronesInStations == 0 && report.Stations > 0)
                Add(report, Severity.Warning, null, Text.Get("xray.s.need_drones", report.TasksWaiting));

            // 6. Baú de oferta cheio de algo que ninguém pede
            foreach (var inv in supplies)
            {
                if (!inv.IsFull()) continue;
                Group stuck = null;
                foreach (var g in inv.GetLogisticEntity().GetSupplyGroups())
                    if (g != null && !allDemanded.Contains(g) && inv.ContainGroup(g)) { stuck = g; break; }
                if (stuck == null) continue;
                string container = ContainerName(inv);
                var issue = Add(report, Severity.Warning, stuck.GetImage(),
                    Text.Get("xray.s.supply_full", Readable.GetGroupName(stuck), container));
                issue.Places.Add(PositionOf(inv));
            }

            report.Issues.Sort((a, b) => a.Level.CompareTo(b.Level));
            return report;
        }

        private static Issue Add(Report report, Severity level, Sprite icon, string text)
        {
            var issue = new Issue { Level = level, Icon = icon, Text = text };
            report.Issues.Add(issue);
            return issue;
        }

        private static bool OnPlanet(Inventory inv, int planet)
        {
            var entity = inv?.GetLogisticEntity();
            return entity != null && entity.GetWorldObject() != null && entity.GetPlanetHash() == planet;
        }

        private static Vector3 PositionOf(Inventory inv)
        {
            var wo = inv.GetLogisticEntity().GetWorldObject();
            return wo != null ? wo.GetPosition() : Vector3.zero;
        }

        private static string ContainerName(Inventory inv)
        {
            var wo = inv.GetLogisticEntity().GetWorldObject();
            if (wo == null) return Text.Get("xray.container");
            string text = wo.GetText();
            if (!string.IsNullOrEmpty(text)) return $"\"{text}\"";
            return wo.GetGroup() != null ? Readable.GetGroupName(wo.GetGroup()) : Text.Get("xray.container");
        }
    }
}
