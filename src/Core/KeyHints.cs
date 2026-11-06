using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;

namespace WardensLogistics.Core
{
    /// <summary>
    /// Atalhos do mod na interface, de forma discreta: uma barra embaixo da tela que mostra
    /// só os atalhos que valem para a janela aberta, com teclas desenhadas como "teclas"
    /// (moldura branca fina, fonte do jogo). Sem janela aberta, nada aparece; ao carregar o
    /// mundo, um lembrete único no canto lista as teclas principais.
    /// </summary>
    internal sealed class KeyHints : MonoBehaviour
    {
        private static readonly Color LabelColor = new Color(1f, 1f, 1f, 0.8f);

        private GameObject _canvas;
        private RectTransform _bar;
        private DataConfig.UiType _shownFor = DataConfig.UiType.Null;
        private static bool _welcomed;

        public static void Create()
        {
            var go = new GameObject("WardensLogistics.KeyHintsDriver");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<KeyHints>();
        }

        private void Update()
        {
            var windows = Managers.GetManager<WindowsHandler>();
            var ui = windows == null || !Plugin.ShowKeyHints.Value ? DataConfig.UiType.Null : windows.GetOpenedUi();
            if (ui == _shownFor) return;
            _shownFor = ui;

            var hints = HintsFor(ui);
            if (hints.Count == 0)
            {
                if (_canvas != null) _canvas.SetActive(false);
                return;
            }
            EnsureCanvas();
            _canvas.SetActive(true);
            Ui.ClearChildren(_bar);
            var row = Ui.Row(_bar, 26f);
            row.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            foreach (var (keys, label) in hints)
            {
                var hint = Ui.Row(row, 8f);
                for (int i = 0; i < keys.Length; i++)
                {
                    if (i > 0) Ui.Label(hint, "+", 15f, LabelColor);
                    Ui.KeyCap(hint, keys[i], 15f);
                }
                Ui.Label(hint, label, 15f, LabelColor);
            }
        }

        private static List<(string[] keys, string label)> HintsFor(DataConfig.UiType ui)
        {
            var list = new List<(string[], string)>();
            string find = Plugin.FinderEnabled.Value ? Plugin.FinderKey.Value.ToString() : null;
            switch (ui)
            {
                case DataConfig.UiType.Container:
                    if (Plugin.QuickStoreEnabled.Value && Plugin.DepositOpenEnabled.Value)
                        list.Add((new[] { Plugin.QuickStoreKey.Value.ToString() }, Text.Get("hint.deposit")));
                    if (Pilhas.Stacks.Enabled)
                    {
                        list.Add((new[] { Text.Get("hint.click") }, Text.Get("hint.move_stack")));
                        list.Add((new[] { "Shift", Text.Get("hint.click") }, Text.Get("hint.move_all")));
                    }
                    if (find != null) list.Add((new[] { find }, Text.Get("hint.find_item")));
                    break;
                case DataConfig.UiType.Equipment:
                    if (find != null) list.Add((new[] { find }, Text.Get("hint.find_item")));
                    break;
                case DataConfig.UiType.Construction:
                case DataConfig.UiType.Craft:
                    if (find != null) list.Add((new[] { find }, Text.Get("hint.find_recipe")));
                    break;
            }
            return list;
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;
            _canvas = Ui.NewCanvas("WardensLogistics.KeyHints", 40);
            _bar = Ui.Box(_canvas.transform, "Bar", new RectOffset(16, 16, 6, 6), 1.5f);
            var frame = (RectTransform)_bar.parent;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0f);
            frame.anchoredPosition = new Vector2(0f, 46f);
        }

        /// <summary>Lembrete único ao carregar o mundo.</summary>
        [HarmonyPatch(typeof(PlanetLoader), "HandleDataAfterLoad")]
        private static class Welcome
        {
            private static void Postfix()
            {
                if (_welcomed || !Plugin.ShowKeyHints.Value) return;
                _welcomed = true;
                Game.Notify(Text.Get("hint.welcome", Plugin.QuickStoreKey.Value, Plugin.FinderKey.Value, Plugin.XRayKey.Value), null, 8f);
            }
        }
    }
}
