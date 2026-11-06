using System;
using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;

namespace WardensLogistics.Pilhas
{
    /// <summary>
    /// Pontos onde o jogo decide capacidade de inventário. Implementação própria; a lista
    /// de pontos que precisam de atenção foi levantada estudando o CheatInventoryStacking
    /// do akarnokd (Apache 2.0) e o código do jogo.
    /// </summary>
    internal static class StackPatches
    {
        // Tipo do item que está entrando, para o "está cheio?" saber se a pilha dele tem espaço.
        [ThreadStatic] private static string _incomingKey;
        [ThreadStatic] private static bool _hasIncoming;

        /// <summary>
        /// Inventory.AddItemInInventory chama IsFull() sem dizer qual item está entrando.
        /// Guardamos o tipo aqui para o IsFull logo em seguida.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), "AddItemInInventory")]
        private static class AddItemInInventoryPatch
        {
            private static void Prefix(WorldObject worldObject)
            {
                if (worldObject == null || worldObject.GetGroup() == null) return;
                _incomingKey = Stacks.Key(worldObject);
                _hasIncoming = true;
            }

            private static void Finalizer() => _hasIncoming = false;
        }

        [HarmonyPatch(typeof(Inventory), nameof(Inventory.IsFull))]
        private static class IsFullPatch
        {
            private static bool Prefix(ref bool __result, List<WorldObject> ____worldObjectsInInventory, int ____inventorySize, int ____inventoryId)
            {
                string incoming = _hasIncoming ? _incomingKey : null;
                _hasIncoming = false;
                if (!Stacks.CanStack(____inventoryId)) return true; // regra original do jogo

                int count = ____worldObjectsInInventory.Count;
                if (count < ____inventorySize) { __result = false; return false; }               // cabe até sem pilhas
                if (count >= (long)____inventorySize * Stacks.StackSize) { __result = true; return false; } // todas as pilhas cheias

                // Com um item específico: cheio se ele abriria uma pilha além dos espaços.
                // Sem item: cheio se não há espaço para uma pilha nova.
                __result = incoming != null
                    ? Stacks.SlotsUsedCached(____inventoryId, ____worldObjectsInInventory, incoming) > ____inventorySize
                    : Stacks.SlotsUsedCached(____inventoryId, ____worldObjectsInInventory) >= ____inventorySize;
                return false;
            }
        }

        /// <summary>
        /// Quando a mochila diminui (troca de equipamento), o jogo solta no chão o que passou
        /// do novo tamanho. Com pilhas, só sai o que não cabe nas pilhas.
        /// </summary>
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.DropObjectsIfNotEnoughSpace))]
        private static class DropExcessPatch
        {
            private static bool Prefix(ref List<WorldObject> __result, int ____inventoryId, List<WorldObject> ____worldObjectsInInventory,
                int ____inventorySize, Vector3 dropPosition, bool removeOnly)
            {
                if (!Stacks.CanStack(____inventoryId)) return true;
                var dropped = new List<WorldObject>();
                while (____worldObjectsInInventory.Count > 0 && Stacks.SlotsUsed(____worldObjectsInInventory) > ____inventorySize)
                {
                    int last = ____worldObjectsInInventory.Count - 1;
                    var wo = ____worldObjectsInInventory[last];
                    ____worldObjectsInInventory.RemoveAt(last);
                    dropped.Add(wo);
                    if (!removeOnly) WorldObjectsHandler.Instance.DropOnFloor(wo, dropPosition);
                }
                __result = dropped;
                return false;
            }
        }

        /// <summary>
        /// Ao carregar o save, o jogo separa a lista de itens de cada inventário com limite de
        /// 8000 partes; o que passasse disso seria perdido. Com pilhas de 250, um baú grande
        /// passa fácil desse número. Refazemos a leitura sem limite (vale com ou sem pilhas).
        /// </summary>
        [HarmonyPatch(typeof(JsonablesHelper), nameof(JsonablesHelper.JsonableToInventory))]
        private static class LoadInventoryPatch
        {
            private static bool Prefix(ref Inventory __result, JsonableInventory jsonableInventory, Dictionary<int, WorldObject> objectMap)
            {
                var items = new List<WorldObject>();
                if (!string.IsNullOrEmpty(jsonableInventory.woIds))
                {
                    foreach (var part in jsonableInventory.woIds.Split(','))
                    {
                        if (part.Length == 0) continue;
                        if (int.TryParse(part, out int id) && objectMap.TryGetValue(id, out var wo))
                            items.Add(wo);
                    }
                }
                __result = new Inventory(jsonableInventory.id, jsonableInventory.size, items,
                    GroupsHandler.GetGroupsViaString(jsonableInventory.supplyGrps, new HashSet<Group>()) as HashSet<Group>,
                    GroupsHandler.GetGroupsViaString(jsonableInventory.demandGrps, new HashSet<Group>()) as HashSet<Group>,
                    jsonableInventory.priority);
                return false;
            }
        }

        /// <summary>Registra baús como empilháveis assim que o jogo liga o inventário ao objeto.</summary>
        [HarmonyPatch(typeof(InventoryAssociated), nameof(InventoryAssociated.SetInventory))]
        private static class RegisterContainerPatch
        {
            private static void Postfix(InventoryAssociated __instance, Inventory inventory)
            {
                if (inventory == null || __instance == null) return;
                if (Core.ContainerIndex.IsStorage(__instance)) Stacks.Register(inventory);
            }
        }
    }
}
