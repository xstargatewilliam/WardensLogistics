using System.Collections.Generic;
using System.Text;
using SpaceCraft;
using UnityEngine;
using WardensLogistics.Core;

namespace WardensLogistics.Pilhas
{
    /// <summary>
    /// Regras do empilhamento. Cada item continua sendo um objeto individual dentro do
    /// inventário, como no jogo original: o empilhamento é só uma regra de contagem de
    /// espaços. Por isso o save não muda de formato e desinstalar o mod não apaga itens
    /// (o que passar da capacidade original fica no inventário até ser retirado).
    ///
    /// Espaços ocupados = soma, para cada tipo de item, de ceil(quantidade / tamanho da pilha).
    /// Um item cabe se a pilha do tipo dele ainda tem espaço ou se sobra um espaço livre.
    ///
    /// Empilham: a mochila do jogador, os baús (fase 1) e as máquinas que só guardam o
    /// que produzem ou consomem (fase 2). Não empilham: equipamento, sequenciador de DNA,
    /// cultivadores (o espaço é da semente), colheitas que aparecem no mundo (cada item é
    /// uma planta física), estações de drones (mais drones por estação seria trapaça).
    /// Foguetes de troca e de troca interplanetária empilham (fase 4).
    /// </summary>
    internal static class Stacks
    {
        private static readonly HashSet<int> StackableInventories = new HashSet<int>();
        private static readonly HashSet<int> MachineInventories = new HashSet<int>();
        private static readonly HashSet<int> OptimizerInventories = new HashSet<int>();

        /// <summary>Máquinas cujo inventário pode empilhar.</summary>
        private static readonly System.Type[] StackableMachines =
        {
            typeof(MachineGenerator), typeof(MachineAutoCrafter), typeof(MachineConvertRecipe),
            typeof(MachineDisintegrator), typeof(MachineDestructInventoryIfFull),
        };

        /// <summary>Componentes que tornam o inventário especial: nunca empilha.</summary>
        private static readonly System.Type[] NeverStack =
        {
            typeof(MachineGrowerBase), typeof(MachineDroneStation),
        };
        private static readonly HashSet<int> RocketInventories = new HashSet<int>();

        public static void RegisterRocket(Inventory inventory)
        {
            if (inventory != null) RocketInventories.Add(inventory.GetId());
        }
        private static int _backpackId = -1;
        private static int _backpackFrame = -1;

        public static bool Enabled => Plugin.StackEnabled.Value && StackSize > 1;
        public static int StackSize => Mathf.Clamp(Plugin.StackSize.Value, 1, 100000);

        /// <summary>Marca um inventário como empilhável (baús são registrados ao surgir no mundo).</summary>
        public static void Register(Inventory inventory)
        {
            if (inventory != null) StackableInventories.Add(inventory.GetId());
        }

        public static void RegisterMachine(Inventory inventory)
        {
            if (inventory != null) MachineInventories.Add(inventory.GetId());
        }

        /// <summary>
        /// Classifica o inventário de uma máquina pelo objeto dono. Devolve true se ele foi
        /// registrado (máquina ou otimizador).
        /// </summary>
        public static bool TryRegisterMachine(GameObject root, Inventory inventory)
        {
            if (root == null || inventory == null) return false;
            foreach (var t in NeverStack)
                if (root.GetComponentInChildren(t, true) != null) return false;
            if (root.GetComponentInChildren<MachineRocketBackAndForth>(true) != null)
            {
                RocketInventories.Add(inventory.GetId());
                return true;
            }
            if (root.GetComponentInChildren<MachineOptimizer>(true) != null)
            {
                OptimizerInventories.Add(inventory.GetId());
                return true;
            }
            foreach (var t in StackableMachines)
                if (root.GetComponentInChildren(t, true) != null)
                {
                    MachineInventories.Add(inventory.GetId());
                    return true;
                }
            return false;
        }

        public static bool CanStack(int inventoryId)
        {
            if (!Enabled) return false;
            if (StackableInventories.Contains(inventoryId)) return true;
            if (MachineInventories.Contains(inventoryId)) return Plugin.StackMachines.Value;
            if (OptimizerInventories.Contains(inventoryId)) return Plugin.StackOptimizer.Value;
            if (RocketInventories.Contains(inventoryId)) return Plugin.StackRockets.Value;
            return inventoryId == BackpackId();
        }

        public static bool CanStack(Inventory inventory) => inventory != null && CanStack(inventory.GetId());

        private static int BackpackId()
        {
            if (_backpackFrame != Time.frameCount)
            {
                _backpackFrame = Time.frameCount;
                var backpack = Game.Backpack;
                _backpackId = backpack != null ? backpack.GetId() : -1;
            }
            return _backpackId;
        }

        /// <summary>
        /// Identidade da pilha. Itens empilham pelo tipo; características genéticas,
        /// sequências de DNA e plantas de projeto só empilham com cópias idênticas,
        /// para não misturar genética nem projetos diferentes.
        /// </summary>
        public static string Key(WorldObject wo)
        {
            var group = wo.GetGroup();
            if (group == null) return "";
            string id = group.GetId();
            switch (id)
            {
                case "GeneticTrait":
                    return id + "|" + Trait(wo);
                case "DNASequence":
                {
                    var sb = new StringBuilder(id);
                    var inv = InventoriesHandler.Instance != null ? InventoriesHandler.Instance.GetInventoryById(wo.GetLinkedInventoryId()) : null;
                    if (inv != null)
                    {
                        var traits = new List<string>();
                        foreach (var t in inv.GetInsideWorldObjects()) if (t != null) traits.Add(Trait(t));
                        traits.Sort(System.StringComparer.Ordinal);
                        foreach (var t in traits) sb.Append('|').Append(t);
                    }
                    return sb.ToString();
                }
                default:
                {
                    var linked = wo.GetLinkedGroups();
                    if (linked != null && linked.Count > 0 && id.StartsWith("Blueprint"))
                        return id + "|" + linked[0].GetId();
                    return id;
                }
            }
        }

        private static string Trait(WorldObject wo)
        {
            var c = wo.GetColor();
            return $"{(int)wo.GetGeneticTraitType()}:{wo.GetGeneticTraitValue()}:{ColorUtility.ToHtmlStringRGBA(c)}";
        }

        /// <summary>Espaços ocupados por uma lista de itens, considerando um item extra opcional.</summary>
        public static int SlotsUsed(IList<WorldObject> items, string extraKey = null)
        {
            int size = StackSize;
            var counts = new Dictionary<string, int>();
            int slots = 0;
            for (int i = 0; i < items.Count; i++)
            {
                var wo = items[i];
                if (wo == null) continue;
                Count(counts, Key(wo), size, ref slots);
            }
            if (extraKey != null) Count(counts, extraKey, size, ref slots);
            return slots;
        }

        private sealed class Snapshot
        {
            public int Frame = -1;
            public int ItemCount = -1;
            public int Slots;
            public readonly Dictionary<string, int> Counts = new Dictionary<string, int>();
        }

        private static readonly Dictionary<int, Snapshot> Snapshots = new Dictionary<int, Snapshot>();

        /// <summary>
        /// Igual a SlotsUsed, mas reaproveita a contagem do inventário dentro do mesmo quadro
        /// enquanto a quantidade de itens não muda. Os drones perguntam "está cheio?" muitas
        /// vezes por segundo; sem isso, cada pergunta recontaria baús de milhares de itens.
        /// </summary>
        public static int SlotsUsedCached(int inventoryId, IList<WorldObject> items, string extraKey = null)
        {
            if (!Snapshots.TryGetValue(inventoryId, out var snap)) Snapshots[inventoryId] = snap = new Snapshot();
            if (snap.Frame != Time.frameCount || snap.ItemCount != items.Count)
            {
                snap.Frame = Time.frameCount;
                snap.ItemCount = items.Count;
                snap.Counts.Clear();
                snap.Slots = 0;
                int size = StackSize;
                for (int i = 0; i < items.Count; i++)
                    if (items[i] != null) Count(snap.Counts, Key(items[i]), size, ref snap.Slots);
            }
            if (extraKey == null) return snap.Slots;
            snap.Counts.TryGetValue(extraKey, out int n);
            return n % StackSize == 0 ? snap.Slots + 1 : snap.Slots;
        }

        /// <summary>
        /// Quantos itens destes tipos ainda cabem: espaços livres inteiros mais o que falta
        /// para completar a pilha já aberta (o menor entre os tipos, para não prometer demais
        /// quando o baú pede vários itens).
        /// </summary>
        public static int FreeItems(Inventory inventory, int size, IEnumerable<Group> groups)
        {
            var items = inventory.GetInsideWorldObjects();
            int id = inventory.GetId();
            int slots = SlotsUsedCached(id, items);
            int free = System.Math.Max(0, size - slots) * StackSize;
            return free + PartialRoom(id, groups);
        }

        /// <summary>Menor espaço restante nas pilhas abertas dos tipos dados (0 se algum não tem pilha aberta).</summary>
        public static int PartialRoom(int inventoryId, IEnumerable<Group> groups)
        {
            if (groups == null || !Snapshots.TryGetValue(inventoryId, out var snap)) return 0;
            int size = StackSize;
            int min = int.MaxValue;
            foreach (var g in groups)
            {
                if (g == null) continue;
                snap.Counts.TryGetValue(g.GetId(), out int n);
                int room = n % size == 0 ? 0 : size - n % size;
                if (room < min) min = room;
            }
            return min == int.MaxValue ? 0 : min;
        }

        /// <summary>Maior espaço restante nas pilhas abertas dos tipos dados.</summary>
        public static int MaxPartialRoom(int inventoryId, IEnumerable<Group> groups)
        {
            if (groups == null || !Snapshots.TryGetValue(inventoryId, out var snap)) return 0;
            int size = StackSize;
            int max = 0;
            foreach (var g in groups)
            {
                if (g == null) continue;
                snap.Counts.TryGetValue(g.GetId(), out int n);
                int room = n % size == 0 ? 0 : size - n % size;
                if (room > max) max = room;
            }
            return max;
        }

        private static void Count(Dictionary<string, int> counts, string key, int size, ref int slots)
        {
            counts.TryGetValue(key, out int n);
            if (n % size == 0) slots++; // este item abre uma pilha nova
            counts[key] = n + 1;
        }

        /// <summary>
        /// Agrupa os itens em pilhas para exibição: um representante por pilha, na ordem
        /// em que cada tipo apareceu, e a quantidade de cada pilha.
        /// </summary>
        public static void BuildDisplay(IList<WorldObject> items, List<WorldObject> representatives, List<int> amounts)
        {
            representatives.Clear();
            amounts.Clear();
            int size = StackSize;
            var open = new Dictionary<string, int>(); // chave -> índice da pilha aberta
            for (int i = 0; i < items.Count; i++)
            {
                var wo = items[i];
                if (wo == null) continue;
                string key = Key(wo);
                if (open.TryGetValue(key, out int idx) && amounts[idx] < size)
                {
                    amounts[idx]++;
                    continue;
                }
                open[key] = representatives.Count;
                representatives.Add(wo);
                amounts.Add(1);
            }
        }

        /// <summary>Os itens da mesma pilha que o representante clicado (até uma pilha).</summary>
        public static List<WorldObject> StackOf(Inventory inventory, WorldObject representative)
        {
            var result = new List<WorldObject>();
            var items = inventory.GetInsideWorldObjects();
            string key = Key(representative);
            int size = StackSize;

            // Descobre em qual pilha (1ª, 2ª...) do tipo o representante está.
            int position = 0, stackIndex = 0;
            foreach (var wo in items)
            {
                if (wo == null || Key(wo) != key) continue;
                if (wo == representative) { stackIndex = position / size; break; }
                position++;
            }
            position = 0;
            foreach (var wo in items)
            {
                if (wo == null || Key(wo) != key) continue;
                if (position / size == stackIndex) result.Add(wo);
                position++;
            }
            return result;
        }
    }

    /// <summary>
    /// Planeja quantos itens cabem num inventário sem esperar o servidor: as transferências
    /// do jogo são assíncronas, então guardar vários itens seguidos precisa contar o que
    /// já foi enviado. Calcula as pilhas uma vez e depois atualiza a cada item.
    /// </summary>
    internal sealed class CapacityPlanner
    {
        private readonly Inventory _inventory;
        private readonly bool _stacks;
        private readonly int _size;
        private readonly int _stackSize;
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        private int _slots;
        private int _items;

        public CapacityPlanner(Inventory inventory)
        {
            _inventory = inventory;
            _size = inventory.GetSize();
            _stacks = Stacks.CanStack(inventory);
            _stackSize = Stacks.StackSize;
            foreach (var wo in inventory.GetInsideWorldObjects())
            {
                if (wo == null) continue;
                _items++;
                if (_stacks) Add(Stacks.Key(wo));
            }
        }

        public bool HasRoom => _stacks ? (_slots < _size || HasPartialStack()) : _items < _size;

        /// <summary>Se o item cabe, reserva o espaço e devolve true.</summary>
        public bool TryReserve(WorldObject wo)
        {
            if (!_stacks)
            {
                if (_items >= _size) return false;
                _items++;
                return true;
            }
            string key = Stacks.Key(wo);
            _counts.TryGetValue(key, out int n);
            if (n % _stackSize == 0 && _slots >= _size) return false;
            Add(key);
            _items++;
            return true;
        }

        private void Add(string key)
        {
            _counts.TryGetValue(key, out int n);
            if (n % _stackSize == 0) _slots++;
            _counts[key] = n + 1;
        }

        private bool HasPartialStack()
        {
            foreach (var n in _counts.Values)
                if (n % _stackSize != 0) return true;
            return false;
        }
    }
}
