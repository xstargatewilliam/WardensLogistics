using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;
using WardensLogistics.Core;

namespace WardensLogistics.Busca
{
    /// <summary>
    /// "Onde está meu item?" — sucessor do ContainerFinder (xkursat), com a diferença
    /// de procurar itens, não só mostrar onde há baús.
    ///
    /// Como escolher o que procurar: com uma janela aberta, passe o mouse sobre um ícone
    /// e aperte a tecla da busca.
    /// - No inventário ou num baú: procura o próprio item.
    /// - No menu de construção ou numa bancada: procura os ingredientes que faltam na
    ///   mochila para aquela receita (ou todos, se não falta nenhum). Não dá para levar
    ///   o mouse até o ingrediente na ficha da receita sem trocar de receita, por isso a
    ///   busca parte da receita.
    /// Ao fechar a janela, cada baú encontrado ganha um marcador com os ícones, as
    /// quantidades e a distância, visível através das paredes.
    ///
    /// A busca usa o índice de baús do Núcleo (o mapa inteiro, não só o raio próximo).
    /// </summary>
    internal static class ItemFinder
    {
        private static Group _hovered;

        [HarmonyPatch(typeof(EventHoverShowGroup), nameof(EventHoverShowGroup.OnImageHovered))]
        private static class HoverIn
        {
            private static void Postfix(Group ____associatedGroup) => _hovered = ____associatedGroup;
        }

        [HarmonyPatch(typeof(EventHoverShowGroup), nameof(EventHoverShowGroup.OnImageHoveredOut))]
        private static class HoverOut
        {
            private static void Postfix() => _hovered = null;
        }

        /// <summary>
        /// Chamado quando não há janela aberta: o jogo nem sempre avisa que o mouse saiu
        /// do ícone ao fechar a janela, então o item em foco é esquecido aqui.
        /// </summary>
        public static void ForgetHover() => _hovered = null;

        /// <summary>Tecla apertada com uma janela aberta.</summary>
        public static void FindHovered(DataConfig.UiType openedUi)
        {
            var group = _hovered;
            if (group == null)
            {
                Game.Notify(Text.Get("find.hover_first", Plugin.FinderKey.Value));
                return;
            }

            bool recipeWindow = openedUi == DataConfig.UiType.Construction || openedUi == DataConfig.UiType.Craft;
            var ingredients = recipeWindow ? IngredientsToFind(group) : null;
            if (ingredients != null && ingredients.Count > 0)
                Find(ingredients, Readable.GetGroupName(group));
            else
                Find(new List<Group> { group }, null);
        }

        /// <summary>
        /// Tecla apertada sem janela aberta: se há marcadores na tela, apaga;
        /// senão, abre a caixa de busca para digitar o nome do item.
        /// </summary>
        public static void KeyWithoutWindow()
        {
            if (FinderOverlay.IsShowing)
            {
                FinderOverlay.Clear();
                return;
            }
            SearchBox.Open();
        }

        /// <summary>
        /// Ingredientes da receita que faltam na mochila (sem repetição). Se a mochila já
        /// tem tudo, devolve todos os ingredientes. Null se o item não tem receita.
        /// </summary>
        private static List<Group> IngredientsToFind(Group group)
        {
            var recipe = group.GetRecipe();
            if (recipe == null) return null;
            var all = recipe.GetIngredientsGroupInRecipe();
            if (all == null || all.Count == 0) return null;

            var distinct = new List<Group>();
            foreach (var g in all)
                if (g != null && !distinct.Contains(g)) distinct.Add(g);

            var backpack = Game.Backpack;
            if (backpack == null) return distinct;

            // Conta o que a mochila tem (sem o complemento dos baús do "puxar dos baús").
            var have = new Dictionary<Group, int>();
            foreach (var wo in backpack.GetInsideWorldObjects())
            {
                var g = wo?.GetGroup();
                if (g == null) continue;
                have.TryGetValue(g, out int n);
                have[g] = n + 1;
            }
            var need = new Dictionary<Group, int>();
            foreach (var g in all)
            {
                if (g == null) continue;
                need.TryGetValue(g, out int n);
                need[g] = n + 1;
            }

            var missing = new List<Group>();
            foreach (var g in distinct)
            {
                have.TryGetValue(g, out int h);
                if (h < need[g]) missing.Add(g);
            }
            return missing.Count > 0 ? missing : distinct;
        }

        /// <param name="recipeName">Nome da receita quando a busca é de ingredientes; null para item único.</param>
        public static void Find(List<Group> groups, string recipeName, bool closeHint = true)
        {
            var markers = new List<FinderOverlay.Marker>();
            var totals = new Dictionary<Group, int>();
            var containersPerGroup = new Dictionary<Group, int>();

            foreach (var c in ContainerIndex.All())
            {
                var counts = new Dictionary<Group, int>();
                foreach (var wo in c.Inventory.GetInsideWorldObjects())
                {
                    var g = wo?.GetGroup();
                    if (g == null || !groups.Contains(g)) continue;
                    counts.TryGetValue(g, out int n);
                    counts[g] = n + 1;
                }
                if (counts.Count == 0) continue;

                var marker = new FinderOverlay.Marker
                {
                    Position = c.Position + Vector3.up * 1.6f,
                    Name = c.DisplayName,
                    Items = new List<FinderOverlay.Entry>(),
                };
                foreach (var g in groups) // mesma ordem da receita em todos os marcadores
                {
                    if (!counts.TryGetValue(g, out int n)) continue;
                    marker.Items.Add(new FinderOverlay.Entry { Icon = g.GetImage(), Count = n });
                    totals.TryGetValue(g, out int t);
                    totals[g] = t + n;
                    containersPerGroup.TryGetValue(g, out int k);
                    containersPerGroup[g] = k + 1;
                }
                if (markers.Count < Plugin.FinderMaxMarkers.Value) markers.Add(marker);
            }

            if (recipeName != null)
                Game.Notify(Text.Get("find.recipe", recipeName), null, 4f);

            foreach (var g in groups)
            {
                string itemName = Readable.GetGroupName(g);
                if (totals.TryGetValue(g, out int total))
                    Game.Notify(Text.Get("find.result", itemName, total, containersPerGroup[g]), g.GetImage(), 4f);
                else
                    Game.Notify(Text.Get("find.none", itemName), g.GetImage(), 4f);
            }

            if (markers.Count == 0)
            {
                FinderOverlay.Clear();
                return;
            }
            FinderOverlay.Show(markers, Plugin.FinderSeconds.Value);
            if (closeHint) Game.Notify(Text.Get("find.close_to_see"), null, 4f);
            Plugin.Debug($"[Finder] {groups.Count} item type(s), {markers.Count} container(s) marked.");
        }
    }
}
