using System;
using System.Collections.Generic;
using HarmonyLib;
using WardensLogistics.Core;
using SpaceCraft;
using UnityEngine;

namespace WardensLogistics.Armazem
{
    /// <summary>
    /// Construir e craftar usando ingredientes dos baús próximos (sucessor do Craft From Containers).
    ///
    /// Disponibilidade: quando o jogo pergunta se a mochila tem os ingredientes,
    /// completamos a resposta com o que existe nos baús do índice. A contagem fica em
    /// cache por frame, então o menu de construção não refaz a varredura a cada checagem.
    ///
    /// Consumo: quando o jogo vai remover ingredientes da mochila, removemos dos baús
    /// o que faltar, pela função oficial de remoção (que sincroniza em multiplayer),
    /// e passamos ao jogo só o que a mochila realmente tem. O servidor aceita
    /// remoções parciais, então o fluxo de construção continua normal.
    /// </summary>
    internal static class PullFromContainers
    {
        private static string Ids(List<Group> groups)
        {
            var ids = new List<string>(groups.Count);
            foreach (var g in groups) ids.Add(g == null ? "null" : g.GetId());
            return string.Join(",", ids);
        }

        [ThreadStatic] private static int _guard;
        private static int _countsFrame = -1;
        private static readonly Dictionary<Group, int> Counts = new Dictionary<Group, int>();

        private static bool Active => Plugin.PullEnabled.Value && _guard == 0 && !Game.FreeCraft && Progress.Unlocked(Progress.Feature.Pull);

        public static void Toggle()
        {
            Plugin.PullEnabled.Value = !Plugin.PullEnabled.Value;
            Game.Notify(Text.Get(Plugin.PullEnabled.Value ? "pull.on" : "pull.off"));
        }

        private static Dictionary<Group, int> NearbyCounts()
        {
            if (_countsFrame == Time.frameCount) return Counts;
            _countsFrame = Time.frameCount;
            Counts.Clear();
            foreach (var c in ContainerIndex.Nearby(Plugin.PullRadius))
            {
                foreach (var wo in c.Inventory.GetInsideWorldObjects())
                {
                    var g = wo?.GetGroup();
                    if (g == null) continue;
                    Counts.TryGetValue(g, out int n);
                    Counts[g] = n + 1;
                }
            }
            return Counts;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.ItemsContainsStatus))]
        private static class Availability
        {
            private static void Postfix(Inventory __instance, List<bool> __result, List<Group> groups)
            {
                if (!Active || __result == null || groups == null || !__result.Contains(false)) return;
                if (__instance != Game.Backpack) return;

                var available = NearbyCounts();
                if (available.Count == 0) return;

                var used = new Dictionary<Group, int>();
                for (int i = 0; i < __result.Count && i < groups.Count; i++)
                {
                    if (__result[i]) continue;
                    var g = groups[i];
                    if (g == null || !available.TryGetValue(g, out int have)) continue;
                    used.TryGetValue(g, out int u);
                    if (u >= have) continue;
                    __result[i] = true;
                    used[g] = u + 1;
                }
            }
        }

        [HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.RemoveItemsFromInventory),
            new[] { typeof(List<Group>), typeof(Inventory), typeof(bool), typeof(bool), typeof(Action<bool>) })]
        private static class Consumption
        {
            private static void Prefix(ref List<Group> groups, Inventory inventory, bool destroy)
            {
                if (!Active || groups == null || groups.Count == 0 || inventory == null) return;
                if (inventory != Game.Backpack) return;

                List<bool> inBackpack;
                _guard++;
                try { inBackpack = inventory.ItemsContainsStatus(groups); }
                finally { _guard--; }
                if (Trace.On)
                    Plugin.Log.LogInfo($"[Pull] Remove request: [{Ids(groups)}] inBackpack=[{string.Join(",", inBackpack)}] | {Trace.State()}");
                if (!inBackpack.Contains(false)) return;

                var keep = new List<Group>(groups.Count);
                var plan = new Dictionary<Inventory, List<WorldObject>>();
                var taken = new HashSet<WorldObject>();
                var containers = ContainerIndex.Nearby(Plugin.PullRadius);

                for (int i = 0; i < groups.Count; i++)
                {
                    var g = groups[i];
                    if (inBackpack[i]) { keep.Add(g); continue; }

                    WorldObject found = null;
                    Inventory source = null;
                    foreach (var c in containers)
                    {
                        foreach (var wo in c.Inventory.GetInsideWorldObjects())
                        {
                            if (wo != null && wo.GetGroup() == g && !taken.Contains(wo)) { found = wo; break; }
                        }
                        if (found != null) { source = c.Inventory; break; }
                    }

                    if (found == null) { keep.Add(g); continue; } // nada nos baús: o jogo segue como faria
                    taken.Add(found);
                    if (!plan.TryGetValue(source, out var list)) plan[source] = list = new List<WorldObject>();
                    list.Add(found);
                }

                if (plan.Count == 0) return;

                _guard++;
                try
                {
                    foreach (var pair in plan)
                    {
                        int invId = pair.Key.GetId();
                        int n = pair.Value.Count;
                        InventoriesHandler.Instance.RemoveItemsFromInventory(pair.Value.AsReadOnly(), pair.Key, destroy, displayInformation: true,
                            ok => { if (Trace.On) Plugin.Log.LogInfo($"[Pull] Server reply for container {invId} ({n} item(s)): success={ok} | {Trace.State()}"); });
                    }
                }
                finally { _guard--; }

                // Nova lista (nunca alterar a original: pode ser a lista da própria receita).
                groups = keep;
                Plugin.Debug($"[Pull] Took {taken.Count} ingredient(s) from {plan.Count} container(s); backpack keeps [{Ids(keep)}] | {Trace.State()}");
            }
        }
    }
}
