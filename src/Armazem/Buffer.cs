using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;
using WardensLogistics.Pilhas;

namespace WardensLogistics.Armazem
{
    /// <summary>
    /// Baú buffer: um baú com "[Buffer]" no nome pede os itens configurados como pedido e,
    /// ao mesmo tempo, oferece esses mesmos itens a quem pede. O jogo não deixa um item
    /// ser pedido e oferecido pelo mesmo baú, então a oferta extra só existe enquanto o
    /// gerente de logística faz a conta. Um buffer nunca enche outro buffer (sem pingue-pongue).
    /// Use prioridade baixa no buffer para que os baús de pedido "de verdade" sejam
    /// atendidos primeiro. Roda no servidor (quem hospeda), como a logística do jogo.
    /// </summary>
    internal static class Buffer
    {
        private static readonly Dictionary<LogisticEntity, HashSet<Group>> Union = new Dictionary<LogisticEntity, HashSet<Group>>();
        private static int _unionFrame = -1;
        private static float _lastSync;

        public static bool Enabled => Plugin.BufferEnabled.Value && Core.Progress.Unlocked(Core.Progress.Feature.Logistics);

        public static bool HasTag(string name) =>
            !string.IsNullOrEmpty(name) && name.IndexOf(ContainerTools.BufferTag, System.StringComparison.OrdinalIgnoreCase) >= 0;

        public static string StripTag(string name)
        {
            int i = name.IndexOf(ContainerTools.BufferTag, System.StringComparison.OrdinalIgnoreCase);
            return i < 0 ? name : (name.Remove(i, ContainerTools.BufferTag.Length)).Trim();
        }

        public static bool IsBuffer(LogisticEntity entity)
        {
            if (!Enabled || entity == null || !entity.HasDemandGroups()) return false;
            var wo = entity.GetWorldObject();
            return wo != null && HasTag(wo.GetText());
        }

        public static bool IsBuffer(Inventory inventory) => inventory != null && IsBuffer(inventory.GetLogisticEntity());

        /// <summary>Durante a conta da logística, o buffer também oferece o que pede.</summary>
        [HarmonyPatch(typeof(LogisticEntity), nameof(LogisticEntity.GetSupplyGroups))]
        private static class SupplyPatch
        {
            private static void Postfix(LogisticEntity __instance, ref HashSet<Group> __result)
            {
                if (!LogisticsPatches.InScope || !IsBuffer(__instance)) return;
                if (IsBuffer(LogisticsPatches.CurrentDemand)) return; // buffer não abastece buffer
                if (_unionFrame != Time.frameCount) { Union.Clear(); _unionFrame = Time.frameCount; }
                if (!Union.TryGetValue(__instance, out var set))
                {
                    set = new HashSet<Group>();
                    if (__result != null) set.UnionWith(__result);
                    set.UnionWith(__instance.GetDemandGroups());
                    Union[__instance] = set;
                }
                __result = set;
            }
        }

        /// <summary>
        /// O jogo só põe na lista de fornecedores quem tem oferta configurada. A cada poucos
        /// segundos, os buffers entram nessa lista (e saem quando deixam de ser buffer).
        /// </summary>
        public static void Sync(List<Inventory> demand, List<Inventory> supply)
        {
            if (Time.time - _lastSync < 2f || demand == null || supply == null) return;
            _lastSync = Time.time;
            foreach (var inv in demand)
                if (IsBuffer(inv) && !supply.Contains(inv)) supply.Add(inv);
            for (int i = supply.Count - 1; i >= 0; i--)
            {
                var inv = supply[i];
                var e = inv?.GetLogisticEntity();
                if (e != null && !e.HasSupplyGroups() && !IsBuffer(e)) supply.RemoveAt(i);
            }
        }
    }
}
