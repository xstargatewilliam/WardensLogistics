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
    /// Fase 1: empilham a mochila do jogador e os baús (inventários abertos como baú que
    /// não são máquinas). Máquinas, drones e inventários especiais entram nas próximas fases.
    /// </summary>
    internal static class Stacks
    {
        private static readonly HashSet<int> StackableInventories = new HashSet<int>();
        private static int _backpackId = -1;
        private static int _backpackFrame = -1;

        public static bool Enabled => Plugin.StackEnabled.Value && StackSize > 1;
        public static int StackSize => Mathf.Clamp(Plugin.StackSize.Value, 1, 100000);

        /// <summary>Marca um inventário como empilhável (baús são registrados ao surgir no mundo).</summary>
        public static void Register(Inventory inventory)
        {
            if (inventory != null) StackableInventories.Add(inventory.GetId());
        }

        public static bool CanStack(int inventoryId)
        {
            if (!Enabled) return false;
            if (StackableInventories.Contains(inventoryId)) return true;
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
