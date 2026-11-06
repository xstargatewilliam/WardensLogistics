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
            private static bool Prefix(Inventory __instance, ref bool __result, List<WorldObject> ____worldObjectsInInventory, int ____inventorySize, int ____inventoryId)
            {
                string incoming = _hasIncoming ? _incomingKey : LogisticsPatches.DroneItemKey;
                _hasIncoming = false;
                if (!Stacks.CanStack(____inventoryId)) return true; // regra original do jogo

                int count = ____worldObjectsInInventory.Count;
                if (count < ____inventorySize) { __result = false; return false; }               // cabe até sem pilhas
                if (count >= (long)____inventorySize * Stacks.StackSize) { __result = true; return false; } // todas as pilhas cheias

                // Com um item específico: cheio se ele abriria uma pilha além dos espaços.
                // Sem item: cheio se não há espaço para uma pilha nova.
                if (incoming != null)
                {
                    __result = Stacks.SlotsUsedCached(____inventoryId, ____worldObjectsInInventory, incoming) > ____inventorySize;
                    return false;
                }
                bool noFreeSlot = Stacks.SlotsUsedCached(____inventoryId, ____worldObjectsInInventory) >= ____inventorySize;
                // Na logística, um baú de pedido sem espaço livre ainda não está cheio se a
                // pilha de algum item pedido tem espaço.
                if (noFreeSlot && LogisticsPatches.InScope)
                {
                    var entity = __instance.GetLogisticEntity();
                    if (entity != null && Stacks.MaxPartialRoom(____inventoryId, entity.GetDemandGroups()) > 0) noFreeSlot = false;
                }
                __result = noFreeSlot;
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

        /// <summary>Registra baús e máquinas assim que o jogo liga o inventário ao objeto (em todos os jogadores).</summary>
        [HarmonyPatch(typeof(InventoryAssociated), nameof(InventoryAssociated.SetInventory))]
        private static class RegisterContainerPatch
        {
            private static void Postfix(InventoryAssociated __instance, Inventory inventory)
            {
                if (inventory == null || __instance == null) return;
                if (Core.ContainerIndex.IsStorage(__instance)) { Stacks.Register(inventory); return; }
                var woa = __instance.GetComponentInParent<WorldObjectAssociated>();
                Stacks.TryRegisterMachine(woa != null ? woa.gameObject : __instance.gameObject, inventory);
            }
        }

        /// <summary>
        /// O desintegrador tem um segundo inventário (a saída) e os foguetes de troca ligam o
        /// inventário por outro componente.
        /// Registramos a saída quando o jogo a entrega, inclusive para os outros jogadores
        /// (para a tela mostrar as pilhas).
        /// </summary>
        [HarmonyPatch(typeof(InventoryAssociatedProxy), nameof(InventoryAssociatedProxy.GetInventory))]
        private static class ProxyInventoryPatch
        {
            private static void Prefix(InventoryAssociatedProxy __instance, ref Action<Inventory, WorldObject> callback)
            {
                if (__instance == null) return;
                Action<Inventory> register = null;
                var machine = __instance.GetComponentInParent<MachineDisintegrator>();
                if (machine != null && machine.secondInventoryAssociatedProxy == __instance) register = Stacks.RegisterMachine;
                else if (__instance.GetComponent<MachineRocketBackAndForth>() != null) register = Stacks.RegisterRocket;
                if (register == null) return;
                var original = callback;
                callback = (inv, wo) =>
                {
                    register(inv);
                    original?.Invoke(inv, wo);
                };
            }
        }

        /// <summary>
        /// O desintegrador confere o espaço livre da saída contando itens (tamanho − itens).
        /// Com pilhas, o espaço livre é tamanho − pilhas ocupadas. Mesma lógica do jogo, com
        /// essa conta trocada; conservadora (supõe que cada ingrediente abre uma pilha nova).
        /// </summary>
        [HarmonyPatch(typeof(MachineDisintegrator), "TryToDesintegrateAnObjectInInventory")]
        private static class DisintegratorCapacityPatch
        {
            private static bool Prefix(MachineDisintegrator __instance, Inventory ____firstIventory, Inventory ____secondInventory)
            {
                if (____firstIventory == null || ____secondInventory == null) return true;
                int giveXIngredientsBack = __instance.giveXIngredientsBack;
                Stacks.RegisterMachine(____secondInventory);
                if (!Stacks.CanStack(____secondInventory)) return true;
                if (____secondInventory.IsFull()) return false;

                var output = ____secondInventory.GetInsideWorldObjects();
                int free = ____secondInventory.GetSize() - Stacks.SlotsUsed(output);
                var input = ____firstIventory.GetInsideWorldObjects();
                for (int i = 0; i < input.Count; i++)
                {
                    var wo = input[i];
                    var recipe = wo?.GetGroup()?.GetRecipe();
                    if (recipe == null) continue;
                    var ingredients = recipe.GetIngredientsGroupInRecipe();
                    if (ingredients.Count <= 0 || free < Mathf.Min(giveXIngredientsBack, ingredients.Count)) continue;

                    var pool = new List<Group>(ingredients);
                    InventoriesHandler.Instance.RemoveItemFromInventory(wo, ____firstIventory, true);
                    for (int j = 0; j < giveXIngredientsBack && pool.Count > 0; j++)
                    {
                        int index = UnityEngine.Random.Range(0, pool.Count);
                        InventoriesHandler.Instance.AddItemToInventory(pool[index], ____secondInventory);
                        pool.RemoveAt(index);
                    }
                    break;
                }
                return false;
            }
        }
    }
}
