using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;
using WardensLogistics.Core;

namespace WardensLogistics.Busca
{
    /// <summary>
    /// Caixa de busca: digitar o nome de um item e marcar os baús que o têm.
    ///
    /// Reaproveita a janela de texto do próprio jogo (a de renomear baú), que já bloqueia
    /// o movimento, mostra o cursor e fecha com Enter ou Esc. Enquanto a busca está
    /// aberta, a janela não renomeia nada: o texto vai só para a busca.
    /// - Embaixo da janela aparece uma lista com até 8 itens que combinam com o texto,
    ///   com a quantidade guardada nos baús (ignora maiúsculas e acentos).
    /// - Enter procura o primeiro da lista; clicar num item procura ele; Esc cancela.
    /// </summary>
    internal static class SearchBox
    {
        internal struct Suggestion
        {
            public Group Group;
            public string Name;
            public int InContainers;
        }

        private const int MaxSuggestions = 8;

        private static bool _active;
        private static UiWindowTextInput _window;
        private static Group _chosen;
        private static string _lastText;
        private static readonly List<Suggestion> Current = new List<Suggestion>();
        private static Dictionary<Group, int> _totals;
        private static List<(Group group, string name, string key)> _catalog;

        public static bool IsActive => _active && _window != null;
        public static IReadOnlyList<Suggestion> Suggestions => Current;

        public static void Open()
        {
            var windows = Managers.GetManager<WindowsHandler>();
            if (windows == null) return;
            var window = windows.OpenAndReturnUi(DataConfig.UiType.TextInput) as UiWindowTextInput;
            if (window == null || window.inputField == null) return;

            _window = window;
            _active = true;
            _chosen = null;
            _lastText = null;
            BuildCatalog();
            window.inputField.text = "";
            window.inputField.ActivateInputField();
            Refresh("");
        }

        /// <summary>Clique numa sugestão: procura esse item e fecha a janela.</summary>
        public static void Choose(Group group)
        {
            _chosen = group;
            var windows = Managers.GetManager<WindowsHandler>();
            if (windows != null) windows.CloseAllWindows();
        }

        /// <summary>Chamado a cada frame pelo overlay: atualiza a lista quando o texto muda.</summary>
        public static void Tick()
        {
            if (!IsActive) return;
            string text = _window.inputField.text ?? "";
            if (text != _lastText) Refresh(text);
        }

        private static void Refresh(string text)
        {
            _lastText = text;
            Current.Clear();
            string query = Normalize(text);
            if (query.Length == 0)
            {
                // Sem texto: mostra o que mais tem nos baús.
                var top = new List<Suggestion>();
                foreach (var pair in _totals)
                    top.Add(new Suggestion { Group = pair.Key, Name = Readable.GetGroupName(pair.Key), InContainers = pair.Value });
                top.Sort((a, b) => b.InContainers.CompareTo(a.InContainers));
                for (int i = 0; i < top.Count && i < MaxSuggestions; i++) Current.Add(top[i]);
                return;
            }

            var scored = new List<(int score, Suggestion s)>();
            foreach (var (group, name, key) in _catalog)
            {
                int score;
                if (key == query) score = 0;
                else if (key.StartsWith(query)) score = 1;
                else if (key.Contains(" " + query)) score = 2;
                else if (key.Contains(query)) score = 3;
                else continue;

                _totals.TryGetValue(group, out int n);
                scored.Add((score, new Suggestion { Group = group, Name = name, InContainers = n }));
            }
            // Melhor correspondência primeiro; empate: o que existe nos baús, depois o nome.
            scored.Sort((a, b) =>
            {
                int c = a.score.CompareTo(b.score);
                if (c != 0) return c;
                c = (b.s.InContainers > 0).CompareTo(a.s.InContainers > 0);
                if (c != 0) return c;
                return string.Compare(a.s.Name, b.s.Name, System.StringComparison.CurrentCultureIgnoreCase);
            });
            for (int i = 0; i < scored.Count && i < MaxSuggestions; i++) Current.Add(scored[i].s);
        }

        private static void BuildCatalog()
        {
            _totals = new Dictionary<Group, int>();
            foreach (var c in ContainerIndex.All())
                foreach (var wo in c.Inventory.GetInsideWorldObjects())
                {
                    var g = wo?.GetGroup();
                    if (g == null) continue;
                    _totals.TryGetValue(g, out int n);
                    _totals[g] = n + 1;
                }

            // Os nomes dependem do idioma do jogo, então o catálogo é refeito a cada abertura.
            _catalog = new List<(Group, string, string)>();
            foreach (var g in GroupsHandler.GetGroupsItem())
            {
                if (g == null) continue;
                string name = Readable.GetGroupName(g);
                if (string.IsNullOrEmpty(name)) continue;
                _catalog.Add((g, name, Normalize(name) + " " + Normalize(g.GetId())));
            }
        }

        /// <summary>Minúsculas e sem acentos: "Silício" e "silicio" combinam.</summary>
        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var decomposed = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(decomposed.Length);
            foreach (char ch in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        // Enter na janela de texto: escolhe o primeiro da lista (o jogo fecha a janela em seguida).
        [HarmonyPatch(typeof(PlayerInputDispatcher), nameof(PlayerInputDispatcher.OnPressEnterDispatcher))]
        private static class EnterPatch
        {
            private static void Prefix()
            {
                if (!IsActive) return;
                Refresh(_window.inputField.text ?? "");
                _chosen = Current.Count > 0 ? Current[0].Group : null;
            }
        }

        // Enquanto a busca está aberta, a janela não tenta renomear nenhum objeto.
        [HarmonyPatch(typeof(UiWindowTextInput), nameof(UiWindowTextInput.OnChangeText))]
        private static class ChangeTextPatch
        {
            private static bool Prefix() => !IsActive;
        }

        // Ao fechar (Enter, clique numa sugestão ou Esc), executa a busca escolhida, se houver.
        [HarmonyPatch(typeof(UiWindowTextInput), nameof(UiWindowTextInput.OnClose))]
        private static class ClosePatch
        {
            private static void Postfix()
            {
                if (!_active) return;
                _active = false;
                _window = null;
                Current.Clear();
                var chosen = _chosen;
                _chosen = null;
                if (chosen != null) ItemFinder.Find(new List<Group> { chosen }, null, closeHint: false);
            }
        }
    }
}
