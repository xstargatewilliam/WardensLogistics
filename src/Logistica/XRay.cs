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
    ///
    /// Cada problema traz um texto curto (painel do N) e, para a janela detalhada
    /// (Ctrl+N), título, explicação, como resolver e os baús envolvidos.
    /// </summary>
    internal static class XRay
    {
        internal enum Severity { Critical, Warning }

        internal sealed class Place
        {
            public string Name;
            public Vector3 Position;
        }

        internal sealed class Issue
        {
            public Severity Level;
            public Sprite Icon;
            public string Text;      // curto, para o painel
            public string Title;     // janela detalhada
            public string Why;
            public string Fix;
            public readonly List<Place> Involved = new List<Place>();

            public IEnumerable<Vector3> Places
            {
                get { foreach (var p in Involved) yield return p.Position; }
            }
        }

        internal enum ItemStatus { Ok, NoSupplier, SupplierEmpty, Stuck, Idle }

        /// <summary>Uma linha da tabela "Itens na logística".</summary>
        internal sealed class ItemRow
        {
            public Group Group;
            public int DemandedBy;   // baús que pedem
            public int SuppliedBy;   // baús que oferecem
            public int Stock;        // unidades nos baús que oferecem
            public int InDelivery;   // unidades com entrega em andamento
            public ItemStatus Status;
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
            public readonly List<ItemRow> Items = new List<ItemRow>();
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

            // Tarefas por estado, e quantas unidades de cada item estão em entrega
            var inDelivery = new Dictionary<Group, int>();
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
                if (moved == null || moved.GetGroup() == null || task.GetTaskState() == LogisticData.TaskState.Done) continue;
                inDelivery.TryGetValue(moved.GetGroup(), out int n);
                inDelivery[moved.GetGroup()] = n + 1;
            }

            // Pedidos e ofertas deste planeta
            var demandsByGroup = new Dictionary<Group, List<Inventory>>();     // só baús que ainda têm espaço
            var demandedAnyByGroup = new Dictionary<Group, int>();             // todos os baús que pedem
            foreach (var inv in DemandsRef(lm))
            {
                if (!OnPlanet(inv, planet)) continue;
                report.DemandContainers++;
                foreach (var g in inv.GetLogisticEntity().GetDemandGroups())
                {
                    if (g == null) continue;
                    demandedAnyByGroup.TryGetValue(g, out int k);
                    demandedAnyByGroup[g] = k + 1;
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
                var offered = inv.GetLogisticEntity().GetSupplyGroups();
                foreach (var g in offered)
                {
                    if (g == null) continue;
                    if (!suppliersByGroup.TryGetValue(g, out var list)) suppliersByGroup[g] = list = new List<Inventory>();
                    list.Add(inv);
                }
                foreach (var wo in inv.GetInsideWorldObjects())
                {
                    var g = wo?.GetGroup();
                    if (g == null || !offered.Contains(g)) continue;
                    stockByGroup.TryGetValue(g, out int n);
                    stockByGroup[g] = n + 1;
                }
            }

            // 1. Sem estação de drones
            if (demandsByGroup.Count > 0 && report.Stations == 0)
                Add(report, Severity.Critical, null, "no_station");

            // 2. Sem energia
            if (!report.Energy && report.Stations > 0 &&
                (report.DronesFlying > 0 || report.TasksWaiting + report.TasksToSupply + report.TasksToDemand > 0))
                Add(report, Severity.Critical, null, "no_energy");

            // 3 e 4. Pedidos sem fornecedor ou com fornecedor vazio
            var stuckGroups = new HashSet<Group>();
            foreach (var pair in demandsByGroup)
            {
                var g = pair.Key;
                if (inDelivery.ContainsKey(g)) continue; // já há entrega a caminho
                string name = Readable.GetGroupName(g);
                if (!suppliersByGroup.TryGetValue(g, out var suppliers))
                {
                    var issue = Add(report, Severity.Critical, g.GetImage(), "no_supplier", name, pair.Value.Count);
                    foreach (var inv in pair.Value) issue.Involved.Add(PlaceOf(inv));
                }
                else if (!stockByGroup.ContainsKey(g))
                {
                    var issue = Add(report, Severity.Warning, g.GetImage(), "supplier_empty", name, suppliers.Count);
                    foreach (var inv in suppliers) issue.Involved.Add(PlaceOf(inv));
                }
            }

            // 5. Entregas esperando e nenhum drone livre
            if (report.TasksWaiting >= WaitingTasksThreshold && report.DronesInStations == 0 && report.Stations > 0)
                Add(report, Severity.Warning, null, "need_drones", report.TasksWaiting);

            // 6. Baú de oferta cheio de algo que ninguém pede
            foreach (var inv in supplies)
            {
                if (!inv.IsFull()) continue;
                Group stuck = null;
                foreach (var g in inv.GetLogisticEntity().GetSupplyGroups())
                    if (g != null && !demandedAnyByGroup.ContainsKey(g) && inv.ContainGroup(g)) { stuck = g; break; }
                if (stuck == null) continue;
                stuckGroups.Add(stuck);
                var place = PlaceOf(inv);
                var issue = Add(report, Severity.Warning, stuck.GetImage(), "supply_full", Readable.GetGroupName(stuck), place.Name);
                issue.Involved.Add(place);
            }

            // Tabela por item
            var allGroups = new HashSet<Group>(demandedAnyByGroup.Keys);
            allGroups.UnionWith(suppliersByGroup.Keys);
            foreach (var g in allGroups)
            {
                var row = new ItemRow { Group = g };
                demandedAnyByGroup.TryGetValue(g, out row.DemandedBy);
                if (suppliersByGroup.TryGetValue(g, out var sup)) row.SuppliedBy = sup.Count;
                stockByGroup.TryGetValue(g, out row.Stock);
                inDelivery.TryGetValue(g, out row.InDelivery);
                bool waiting = demandsByGroup.ContainsKey(g) && row.InDelivery == 0;
                if (stuckGroups.Contains(g)) row.Status = ItemStatus.Stuck;
                else if (waiting && row.SuppliedBy == 0) row.Status = ItemStatus.NoSupplier;
                else if (waiting && row.Stock == 0) row.Status = ItemStatus.SupplierEmpty;
                else if (row.DemandedBy == 0) row.Status = ItemStatus.Idle;
                else row.Status = ItemStatus.Ok;
                report.Items.Add(row);
            }
            // Problemas primeiro, depois por nome
            report.Items.Sort((a, b) =>
            {
                int pa = a.Status == ItemStatus.Ok || a.Status == ItemStatus.Idle ? 1 : 0;
                int pb = b.Status == ItemStatus.Ok || b.Status == ItemStatus.Idle ? 1 : 0;
                if (pa != pb) return pa.CompareTo(pb);
                return string.Compare(Readable.GetGroupName(a.Group), Readable.GetGroupName(b.Group), System.StringComparison.CurrentCultureIgnoreCase);
            });

            report.Issues.Sort((a, b) => a.Level.CompareTo(b.Level));
            return report;
        }

        /// <summary>Cria um problema com todos os textos a partir do código dele (xray.s.*, xray.t.*, xray.why.*, xray.fix.*).</summary>
        private static Issue Add(Report report, Severity level, Sprite icon, string code, params object[] args)
        {
            var issue = new Issue
            {
                Level = level,
                Icon = icon,
                Text = Text.Get("xray.s." + code, args),
                Title = Text.Get("xray.t." + code, args),
                Why = Text.Get("xray.why." + code, args),
                Fix = Text.Get("xray.fix." + code, args),
            };
            report.Issues.Add(issue);
            return issue;
        }

        private static bool OnPlanet(Inventory inv, int planet)
        {
            var entity = inv?.GetLogisticEntity();
            return entity != null && entity.GetWorldObject() != null && entity.GetPlanetHash() == planet;
        }

        private static Place PlaceOf(Inventory inv)
        {
            var wo = inv.GetLogisticEntity().GetWorldObject();
            var place = new Place { Position = wo != null ? wo.GetPosition() : Vector3.zero };
            if (wo == null) place.Name = Text.Get("xray.container");
            else if (!string.IsNullOrEmpty(wo.GetText())) place.Name = $"\"{wo.GetText()}\"";
            else place.Name = wo.GetGroup() != null ? Readable.GetGroupName(wo.GetGroup()) : Text.Get("xray.container");
            return place;
        }
    }
}
