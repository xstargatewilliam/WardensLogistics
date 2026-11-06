using System;
using System.Collections.Generic;
using WardensLogistics.Core;
using SpaceCraft;

namespace WardensLogistics.Armazem
{
    /// <summary>
    /// Guardar rápido: une o Quick Store (aedenthorn) e o Container Auto Deposit (jamarir)
    /// numa tecla só.
    /// - Nenhuma janela aberta: cada item da mochila vai para o baú próximo mais perto
    ///   que já tenha o mesmo item ou cujo nome combine com ele.
    /// - Janela de um baú aberta: deposita nesse baú só os itens que combinam com ele.
    ///
    /// Diferenças em relação aos mods originais:
    /// - usa a transferência oficial do jogo (sincroniza em multiplayer);
    /// - reconhece qualquer baú do índice, inclusive de outros mods;
    /// - respeita restrições de item do baú e conta o espaço livre durante a operação;
    /// - o depósito no baú aberto não bloqueia a entrada de itens em outros inventários
    ///   (o Container Auto Deposit fazia isso com uma trava global).
    /// </summary>
    internal static class QuickStore
    {
        private sealed class Target
        {
            public Inventory Inventory;
            public string NameLower;
            public int Free;
            public readonly HashSet<Group> Planned = new HashSet<Group>();
        }

        /// <summary>Guarda nos baús próximos (nenhuma janela aberta).</summary>
        public static void RunNearby()
        {
            var containers = ContainerIndex.Nearby();
            if (containers.Count == 0)
            {
                // Diz a que distância está o baú mais perto, para separar "fora do alcance" de "nenhum baú".
                var all = ContainerIndex.All();
                if (all.Count > 0)
                    Game.Notify(Text.Get("store.out_of_range", Plugin.Range.Value.ToString("0"),
                        UnityEngine.Mathf.Sqrt(all[0].DistanceSqr).ToString("0")));
                else
                {
                    Game.Notify(Text.Get("store.no_containers"));
                    ContainerIndex.LogDiagnostics();
                }
                return;
            }

            var targets = new List<Target>(containers.Count);
            foreach (var c in containers)
                targets.Add(new Target { Inventory = c.Inventory, NameLower = c.NameLower, Free = c.FreeSlots });

            var stored = StoreInto(targets);
            Report(stored, "store.nothing");
            Plugin.Debug($"[QuickStore] Nearby: stored {stored.Count} item type(s) into {targets.Count} container(s).");
        }

        /// <summary>Deposita no baú que está aberto na tela só o que combina com ele.</summary>
        public static void RunIntoOpenContainer(Inventory container)
        {
            if (container == null || container == Game.Backpack) return;

            var entry = ContainerIndex.Find(container);
            var target = new Target
            {
                Inventory = container,
                NameLower = entry != null ? entry.NameLower : "",
                Free = container.GetSize() - container.GetInsideWorldObjects().Count,
            };

            var stored = StoreInto(new List<Target> { target });
            Report(stored, "deposit.nothing");
            Plugin.Debug($"[QuickStore] Open container {container.GetId()}: stored {stored.Count} item type(s).");
        }

        private static Dictionary<Group, int> StoreInto(List<Target> targets)
        {
            var stored = new Dictionary<Group, int>();
            var backpack = Game.Backpack;
            var handler = InventoriesHandler.Instance;
            if (backpack == null || handler == null) return stored;

            var excluded = ParseExcluded(Plugin.QuickStoreExcluded.Value);

            // Copia da mochila: a transferência é assíncrona e o inventário só muda
            // quando o servidor responde. Por isso o espaço livre e o conteúdo novo
            // de cada baú são contados à parte (Free / Planned).
            foreach (var item in new List<WorldObject>(backpack.GetInsideWorldObjects()))
            {
                var group = item?.GetGroup();
                if (group == null || excluded.Contains(group.GetId())) continue;
                string itemName = Readable.GetGroupName(group).ToLowerInvariant();

                foreach (var t in targets)
                {
                    if (t.Free <= 0 || !Matches(t, group, itemName)) continue;
                    if (!t.Inventory.GetIsAuthorized(item)) continue;

                    handler.TransferItem(backpack, t.Inventory, item);
                    t.Free--;
                    t.Planned.Add(group);
                    stored.TryGetValue(group, out int n);
                    stored[group] = n + 1;
                    break;
                }
            }
            return stored;
        }

        private static void Report(Dictionary<Group, int> stored, string emptyKey)
        {
            if (stored.Count == 0)
            {
                Game.Notify(Text.Get(emptyKey));
                return;
            }
            foreach (var pair in stored)
                Game.Notify(Text.Get("store.item", Readable.GetGroupName(pair.Key), pair.Value), pair.Key.GetImage());
        }

        private static bool Matches(Target t, Group group, string itemName)
        {
            if (Plugin.QuickStoreMatchExisting.Value && (t.Planned.Contains(group) || t.Inventory.ContainGroup(group)))
                return true;
            if (Plugin.QuickStoreMatchName.Value && t.NameLower.Length > 0 && t.NameLower.Contains(itemName))
                return true;
            return false;
        }

        private static HashSet<string> ParseExcluded(string value)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(value)) return set;
            foreach (var part in value.Split(','))
            {
                var id = part.Trim();
                if (id.Length > 0) set.Add(id);
            }
            return set;
        }
    }
}
