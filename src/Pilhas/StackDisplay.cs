using System.Collections.Generic;
using System.Collections.ObjectModel;
using HarmonyLib;
using SpaceCraft;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WardensLogistics.Core;

namespace WardensLogistics.Pilhas
{
    /// <summary>
    /// Desenho e cliques dos inventários empilhados.
    /// - O jogo desenha o inventário a partir de uma lista de itens recebida como parâmetro.
    ///   Entregamos um item representante por pilha e depois escrevemos a quantidade no canto
    ///   de cada espaço. O resto do desenho (ícones, cadeado, logística) é o do próprio jogo.
    /// - Clicar numa pilha move a pilha inteira para o outro lado da janela.
    ///   Com a tecla de acessibilidade (Shift), continua movendo todos daquele tipo, como no jogo.
    /// </summary>
    internal static class StackDisplay
    {
        private const string LabelName = "WardensLogistics.StackCount";

        private static readonly List<WorldObject> Representatives = new List<WorldObject>();
        private static readonly List<int> Amounts = new List<int>();
        [System.ThreadStatic] private static bool _stackedDraw;

        [HarmonyPatch(typeof(InventoryDisplayer), "SetInventoryBlocks")]
        private static class SetInventoryBlocksPatch
        {
            private static void Prefix(Inventory ____inventory, ref ReadOnlyCollection<WorldObject> inventoryWorldObjects)
            {
                _stackedDraw = false;
                if (!Stacks.CanStack(____inventory) || inventoryWorldObjects == null) return;
                Stacks.BuildDisplay(inventoryWorldObjects, Representatives, Amounts);
                inventoryWorldObjects = new List<WorldObject>(Representatives).AsReadOnly();
                _stackedDraw = true;
            }

            private static void Postfix(InventoryDisplayer __instance, GridLayoutGroup ____grid)
            {
                if (!_stackedDraw || ____grid == null) return;
                _stackedDraw = false;
                var font = FindFont(__instance);
                var grid = ____grid.transform;
                for (int i = 0; i < grid.childCount && i < Amounts.Count; i++)
                {
                    if (Amounts[i] <= 1) continue;
                    AddLabel(grid.GetChild(i), Amounts[i], font);
                }
            }
        }

        private static void AddLabel(Transform block, int amount, TMP_FontAsset font)
        {
            var go = new GameObject(LabelName, typeof(RectTransform));
            go.transform.SetParent(block, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 2f);
            rt.offsetMax = new Vector2(-5f, -2f);

            var text = go.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = amount.ToString();
            text.fontSize = Plugin.StackFontSize.Value;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.BottomRight;
            text.color = Color.white;
            text.raycastTarget = false; // não atrapalha o clique no bloco
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(0, 0, 0, 255);
            go.transform.SetAsLastSibling();
        }

        private static TMP_FontAsset _font;

        private static TMP_FontAsset FindFont(Component near)
        {
            if (_font != null) return _font;
            var canvas = near.GetComponentInParent<Canvas>();
            var sample = canvas != null ? canvas.rootCanvas.GetComponentInChildren<TMP_Text>(true) : null;
            _font = sample != null ? sample.font : TMP_Settings.defaultFontAsset;
            return _font;
        }

        /// <summary>Clique esquerdo numa pilha dentro da janela de baú: move a pilha inteira.</summary>
        [HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.AnInventoryHasBeenClicked))]
        private static class ClickPatch
        {
            private static bool Prefix(InventoriesHandler __instance, Inventory inventoryClicked, WorldObject itemClicked)
            {
                if (itemClicked == null || !Stacks.CanStack(inventoryClicked)) return true;

                var windows = Managers.GetManager<WindowsHandler>();
                if (windows == null || windows.GetOpenedUi() != DataConfig.UiType.Container) return true;
                var player = Game.Player;
                if (player == null || player.GetPlayerInputDispatcher().IsPressingAccessibilityKey()) return true; // Shift: regra do jogo

                var window = windows.GetWindowViaUiId(DataConfig.UiType.Container) as UiWindowContainer;
                var other = window != null ? window.GetOtherInventory(inventoryClicked) : null;
                if (other == null) return true;

                var stack = Stacks.StackOf(inventoryClicked, itemClicked);
                if (stack.Count <= 1) return true; // item avulso: clique normal do jogo
                if (Trace.PendingServerReplies > 0) return false; // mesma trava do jogo: espera a operação anterior

                var room = new CapacityPlanner(other);
                foreach (var wo in stack)
                {
                    if (!other.GetIsAuthorized(wo) || !room.TryReserve(wo)) break;
                    __instance.TransferItem(inventoryClicked, other, wo);
                }
                return false;
            }
        }
    }
}
