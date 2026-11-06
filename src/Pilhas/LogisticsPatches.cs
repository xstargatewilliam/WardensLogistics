using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SpaceCraft;

namespace WardensLogistics.Pilhas
{
    /// <summary>
    /// Pilhas fase 3: a logística de drones. O jogo calcula quantas entregas um baú de
    /// pedido ainda aceita como "tamanho − itens − entregas a caminho". Com pilhas, um baú
    /// de 30 espaços parava de receber ao chegar em 30 itens. Enquanto o gerente de
    /// logística faz essa conta, o tamanho do inventário passa a valer
    /// "itens + quantos ainda cabem", e "está cheio?" considera os itens que o baú pede.
    /// Tudo isso roda só no servidor (quem hospeda), como a logística do jogo.
    /// </summary>
    internal static class LogisticsPatches
    {
        [ThreadStatic] private static int _scope;
        [ThreadStatic] private static string _droneItemKey;

        /// <summary>Dentro da conta da logística.</summary>
        public static bool InScope => _scope > 0;

        /// <summary>Item que um drone carrega procurando onde entregar (ou null).</summary>
        public static string DroneItemKey => _droneItemKey;

        /// <summary>Tamanho "virtual" usado só dentro da conta da logística.</summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetSize))]
        private static class SizePatch
        {
            private static void Postfix(Inventory __instance, int ____inventorySize, ref int __result)
            {
                if (_scope <= 0 || !Stacks.CanStack(__instance)) return;
                var entity = __instance.GetLogisticEntity();
                var groups = entity != null ? entity.GetDemandGroups() : null;
                int capacity = __instance.GetInsideWorldObjects().Count + Stacks.FreeItems(__instance, ____inventorySize, groups);
                if (capacity > __result) __result = capacity;
            }
        }

        [HarmonyPatch]
        private static class SetTasksPatch
        {
            private static MethodBase TargetMethod() =>
                AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(LogisticManager), "SetLogisticTasks"));

            private static void Prefix() => _scope++;
            private static void Finalizer() { if (_scope > 0) _scope--; }
        }

        [HarmonyPatch(typeof(LogisticManager), "AddNewDemandInventory")]
        private static class AddDemandPatch
        {
            private static void Prefix() => _scope++;
            private static void Finalizer() { if (_scope > 0) _scope--; }
        }

        /// <summary>
        /// Um drone com item sem destino procura outro baú de pedido; se não acha, o jogo
        /// destrói o item. Dizemos ao "está cheio?" qual item é, para que um baú com pilha
        /// aberta desse item conte como destino.
        /// </summary>
        [HarmonyPatch(typeof(LogisticManager), "FindDemandForDroneOrDestroyContent")]
        private static class DroneRedirectPatch
        {
            private static void Prefix(Drone drone)
            {
                var items = drone != null && drone.GetDroneInventory() != null ? drone.GetDroneInventory().GetInsideWorldObjects() : null;
                _droneItemKey = items != null && items.Count > 0 && items[0] != null ? Stacks.Key(items[0]) : null;
            }

            private static void Finalizer() => _droneItemKey = null;
        }
    }
}
