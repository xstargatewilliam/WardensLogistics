using System;
using System.Collections.Generic;
using System.Text;
using SpaceCraft;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WardensLogistics.Core
{
    internal sealed class ContainerEntry
    {
        public InventoryAssociated Owner;
        public Inventory Inventory;
        public WorldObject WorldObject;
        public Vector3 Position;
        public string NameLower;
        public string DisplayName; // nome dado pelo jogador, como digitado
        public float DistanceSqr; // preenchido por Nearby()
    }

    /// <summary>
    /// O Núcleo da suíte: uma lista única de baús, compartilhada por todos os módulos.
    /// Os mods atuais varrem o mapa inteiro a cada uso (o Craft From Containers, a cada
    /// checagem de ingredientes). Aqui a varredura acontece no máximo a cada
    /// IndexRefreshSeconds, e o restante só lê a lista em memória.
    ///
    /// Um "baú" é reconhecido pelo comportamento, não pelo nome interno, para que baús
    /// de outros mods (ex.: Containers Plus) entrem automaticamente:
    /// - tem inventário (InventoryAssociated, direto ou via InventoryAssociatedProxy de rede) e abre a janela de baú (ActionOpenable, tipo Container);
    /// - não é uma das máquinas que o próprio jogo liga a um inventário (mesma lista de
    ///   InventoryAssociated.SetInventory: gerador, cultivador, autocrafter, drones...);
    /// - foi construído pelo jogador (baús que vieram com o mapa ficam de fora por padrão).
    /// </summary>
    internal static class ContainerIndex
    {
        // Componentes que o jogo trata como "máquina com inventário" (InventoryAssociated.SetInventory).
        private static readonly Type[] MachineTypes =
        {
            typeof(MachineGrowerBase), typeof(MachineGenerator), typeof(MachineConvertRecipe),
            typeof(MachineAutoCrafter), typeof(MachineDroneStation), typeof(MachineRocketBackAndForth),
            typeof(MachineDestructInventoryIfFull), typeof(MachineOptimizer), typeof(MachineDisintegrator),
            typeof(WorldUnitGenerationViaInventory), typeof(WorldUnitMultiplierViaInventory),
        };

        private enum Verdict { Ok, NoInventoryId, NotOpenableAsContainer, IsMachine, NoWorldObject, FromMap, NoInventory, IsBackpack }

        private static readonly List<ContainerEntry> Entries = new List<ContainerEntry>();
        private static readonly List<ContainerEntry> NearbyBuffer = new List<ContainerEntry>();
        private static float _lastRefresh = float.NegativeInfinity;

        /// <summary>Força nova varredura na próxima consulta.</summary>
        public static void Invalidate() => _lastRefresh = float.NegativeInfinity;

        /// <summary>
        /// Baús dentro do raio informado, do mais perto para o mais longe.
        /// A lista devolvida é reaproveitada: copie se for guardar.
        /// </summary>
        public static List<ContainerEntry> Nearby(float radius)
        {
            NearbyBuffer.Clear();
            var pos = Game.PlayerPosition;
            if (pos == null) return NearbyBuffer;

            EnsureFresh();
            float rangeSqr = radius * radius;
            for (int i = 0; i < Entries.Count; i++)
            {
                var e = Entries[i];
                if (e.Owner == null || e.Inventory == null) continue; // destruído desde a varredura
                e.DistanceSqr = (e.Position - pos.Value).sqrMagnitude;
                if (e.DistanceSqr <= rangeSqr) NearbyBuffer.Add(e);
            }
            NearbyBuffer.Sort((a, b) => a.DistanceSqr.CompareTo(b.DistanceSqr));
            return NearbyBuffer;
        }

        /// <summary>
        /// Todos os baús do índice (sem limite de raio), do mais perto para o mais longe.
        /// Devolve uma lista nova.
        /// </summary>
        public static List<ContainerEntry> All()
        {
            var result = new List<ContainerEntry>();
            var pos = Game.PlayerPosition;
            EnsureFresh();
            foreach (var e in Entries)
            {
                if (e.Owner == null || e.Inventory == null) continue;
                e.DistanceSqr = pos == null ? 0f : (e.Position - pos.Value).sqrMagnitude;
                result.Add(e);
            }
            result.Sort((a, b) => a.DistanceSqr.CompareTo(b.DistanceSqr));
            return result;
        }

        /// <summary>Entrada do índice para um inventário (null se não for um baú do índice).</summary>
        public static ContainerEntry Find(Inventory inventory)
        {
            if (inventory == null) return null;
            EnsureFresh();
            foreach (var e in Entries)
                if (e.Inventory == inventory) return e;
            return null;
        }

        private static void EnsureFresh()
        {
            if (Time.unscaledTime - _lastRefresh < Plugin.RefreshSeconds.Value) return;
            Refresh();
        }

        private static void Refresh()
        {
            _lastRefresh = Time.unscaledTime;
            Entries.Clear();
            if (InventoriesHandler.Instance == null) return;

            var backpack = Game.Backpack;
            var all = Object.FindObjectsByType<InventoryAssociated>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var owner = all[i];
                if (owner == null) continue;
                if (Evaluate(owner, backpack, out var entry) == Verdict.Ok)
                {
                    Entries.Add(entry);
                    Pilhas.Stacks.Register(entry.Inventory);
                }
            }
            Plugin.Debug($"[Core] Index refreshed: {Entries.Count} containers out of {all.Length} inventories.");
        }

        private static Verdict Evaluate(InventoryAssociated owner, Inventory backpack, out ContainerEntry entry)
        {
            entry = null;
            var woa = owner.GetComponentInParent<WorldObjectAssociated>();
            if (woa == null) woa = owner.GetComponentInChildren<WorldObjectAssociated>();
            var root = woa != null ? woa.gameObject : owner.gameObject;

            if (!OpensAsContainer(root)) return Verdict.NotOpenableAsContainer;
            if (MachineOn(root) != null) return Verdict.IsMachine;

            if (woa == null) return Verdict.NoWorldObject;
            var wo = woa.GetWorldObject();
            if (wo == null) return Verdict.NoWorldObject;
            if (!Plugin.IncludeWorldContainers.Value && WorldObjectsIdHandler.IsWorldObjectFromScene(wo.GetId()))
                return Verdict.FromMap;

            // Baús construídos usam InventoryAssociatedProxy (componente de rede). O id pode
            // estar no InventoryAssociated ou, como faz o próprio proxy, no inventário ligado
            // ao WorldObject (GetLinkedInventoryId).
            int id = owner.GetInventoryId();
            if (id < 0) id = wo.GetLinkedInventoryId();
            if (id <= 0) return Verdict.NoInventoryId;
            var inventory = InventoriesHandler.Instance.GetInventoryById(id);
            if (inventory == null) return Verdict.NoInventory;
            if (inventory == backpack) return Verdict.IsBackpack;

            var text = root.GetComponentInChildren<WorldObjectText>(true);
            string name = text != null ? text.GetText() : null;
            entry = new ContainerEntry
            {
                Owner = owner,
                Inventory = inventory,
                WorldObject = wo,
                Position = owner.transform.position,
                NameLower = string.IsNullOrEmpty(name) ? "" : name.ToLowerInvariant(),
                DisplayName = name ?? "",
            };
            return Verdict.Ok;
        }

        /// <summary>Se o objeto dono deste inventário é um baú (abre como baú e não é máquina).</summary>
        public static bool IsStorage(InventoryAssociated owner)
        {
            if (owner == null) return false;
            var woa = owner.GetComponentInParent<WorldObjectAssociated>();
            if (woa == null) woa = owner.GetComponentInChildren<WorldObjectAssociated>();
            var root = woa != null ? woa.gameObject : owner.gameObject;
            return OpensAsContainer(root) && MachineOn(root) == null;
        }

        private static bool OpensAsContainer(GameObject root)
        {
            foreach (var openable in root.GetComponentsInChildren<ActionOpenable>(true))
                if (openable.uiType == DataConfig.UiType.Container) return true;
            return false;
        }

        private static string MachineOn(GameObject root)
        {
            foreach (var t in MachineTypes)
                if (root.GetComponentInChildren(t, true) != null) return t.Name;
            return null;
        }

        /// <summary>
        /// Escreve no LogOutput.log por que cada inventário perto do jogador entrou ou não no índice.
        /// Chamado quando um módulo não encontra baús, para facilitar o diagnóstico em testes.
        /// </summary>
        public static void LogDiagnostics()
        {
            var pos = Game.PlayerPosition;
            if (pos == null || InventoriesHandler.Instance == null) return;
            var backpack = Game.Backpack;
            float radius = Plugin.StoreRadius + 10f;

            var sb = new StringBuilder();
            sb.AppendLine($"[Core] Diagnostics: inventories within {radius:0} m of the player:");
            int count = 0;
            foreach (var owner in Object.FindObjectsByType<InventoryAssociated>(FindObjectsSortMode.None))
            {
                if (owner == null) continue;
                float d = Vector3.Distance(owner.transform.position, pos.Value);
                if (d > radius) continue;
                count++;
                var verdict = Evaluate(owner, backpack, out _);
                var woa = owner.GetComponentInParent<WorldObjectAssociated>();
                if (woa == null) woa = owner.GetComponentInChildren<WorldObjectAssociated>();
                var root = woa != null ? woa.gameObject : owner.gameObject;
                var wo = woa != null ? woa.GetWorldObject() : null;
                var openables = root.GetComponentsInChildren<ActionOpenable>(true);
                var ui = new List<string>();
                foreach (var o in openables) ui.Add(o.uiType.ToString());
                sb.AppendLine($"  - {owner.gameObject.name} | {d:0.0} m | verdict={verdict} | invId={owner.GetInventoryId()} " +
                              $"| group={(wo != null && wo.GetGroup() != null ? wo.GetGroup().GetId() : "?")} | woId={(wo != null ? wo.GetId().ToString() : "?")} " +
                              $"| openables=[{string.Join(",", ui)}] | machine={MachineOn(root) ?? "-"} | root={root.name}");
            }
            if (count == 0) sb.AppendLine("  (none)");
            Plugin.Log.LogWarning(sb.ToString());
        }
    }
}
