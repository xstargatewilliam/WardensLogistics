using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;

namespace WardensLogistics.Pilhas
{
    /// <summary>
    /// Pilhas fase 4: a tela de compra dos foguetes de troca (e da troca interplanetária)
    /// limita o carrinho ao número de espaços do foguete. Com pilhas, o limite passa a ser
    /// "o carrinho cabe nas pilhas": soma de ceil(quantidade de cada item / tamanho da pilha)
    /// até o número de espaços.
    /// </summary>
    internal static class TradePatches
    {
        private static bool Allow(Dictionary<Group, int> cart, Group group, int change, int slots, out bool handled)
        {
            handled = false;
            if (change < 1 || cart == null || group == null || slots <= 0) return true;
            int size = Stacks.StackSize;
            int used = 0;
            bool seen = false;
            foreach (var kv in cart)
            {
                int n = kv.Value;
                if (kv.Key == group) { n += change; seen = true; }
                used += (n + size - 1) / size;
            }
            if (!seen) used += (change + size - 1) / size;
            handled = true;
            return used <= slots;
        }

        private static bool Before(ref int inventorySize, Dictionary<Group, int> cart, Group group, int change, ref int __state)
        {
            __state = -1;
            if (!Stacks.Enabled || !Plugin.StackRockets.Value || inventorySize <= 0) return true;
            if (!Allow(cart, group, change, inventorySize, out bool handled)) return false;
            if (!handled) return true;
            __state = inventorySize;
            inventorySize = int.MaxValue; // a checagem original (itens >= espaços) não vale com pilhas
            return true;
        }

        private static void After(ref int inventorySize, int __state)
        {
            if (__state >= 0) inventorySize = __state;
        }

        [HarmonyPatch(typeof(UiWindowTrade), "OnClickButtons")]
        private static class TradeCart
        {
            private static bool Prefix(ref int ____inventorySize, Dictionary<Group, int> ____groupsWithNumber, Group group, int changeOfValue, ref int __state)
                => Before(ref ____inventorySize, ____groupsWithNumber, group, changeOfValue, ref __state);
            private static void Finalizer(ref int ____inventorySize, int __state) => After(ref ____inventorySize, __state);
        }

        [HarmonyPatch(typeof(UiWindowInterplanetaryExchange), "OnClickButtons")]
        private static class ExchangeCart
        {
            private static bool Prefix(ref int ____inventorySize, Dictionary<Group, int> ____groupsWithNumber, Group group, int changeOfValue, ref int __state)
                => Before(ref ____inventorySize, ____groupsWithNumber, group, changeOfValue, ref __state);
            private static void Finalizer(ref int ____inventorySize, int __state) => After(ref ____inventorySize, __state);
        }

        /// <summary>O contador "itens / espaços" passa a mostrar também os espaços ocupados pelas pilhas.</summary>
        [HarmonyPatch(typeof(UiWindowInterplanetaryExchange), "UpdateUiForSelectedItems")]
        private static class ExchangeCounter
        {
            private static void Postfix(UiWindowInterplanetaryExchange __instance, int ____inventorySize, Dictionary<Group, int> ____groupsWithNumber)
            {
                if (!Stacks.Enabled || !Plugin.StackRockets.Value || ____groupsWithNumber == null) return;
                var text = __instance.textInventorySpace;
                if (text == null) return;
                int size = Stacks.StackSize, items = 0, slots = 0;
                foreach (var kv in ____groupsWithNumber) { items += kv.Value; slots += (kv.Value + size - 1) / size; }
                text.text = $"{items}  ({slots} / {____inventorySize})";
            }
        }
    }
}
