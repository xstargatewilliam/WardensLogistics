using System.Collections.Generic;
using SpaceCraft;
using TMPro;
using UnityEngine;

namespace WardensLogistics.Core
{
    /// <summary>
    /// Atalhos do mod sempre visíveis e discretos: uma linha de texto logo acima da versão
    /// do jogo ("v2.104 ... - Modded"), no canto inferior direito, com a mesma fonte e o
    /// mesmo alinhamento. O conteúdo acompanha o contexto:
    /// - sem janela: as teclas principais (guardar, buscar, raio-x);
    /// - baú aberto: depositar, mover pilha, mover tudo, onde está;
    /// - inventário: onde está o item; construção e bancadas: ingredientes;
    /// - outras janelas: nada.
    /// A linha fica no mesmo retângulo do texto da versão, com uma margem inferior igual à
    /// altura desse texto, então acompanha qualquer resolução.
    /// </summary>
    internal sealed class KeyHints : MonoBehaviour
    {
        private TextMeshProUGUI _legend;
        private TextMeshProUGUI _source;
        private string _lastText;

        public static void Create()
        {
            var go = new GameObject("WardensLogistics.KeyHintsDriver");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<KeyHints>();
        }

        private void LateUpdate()
        {
            var hud = Managers.GetManager<BaseHudHandler>();
            var version = hud != null ? hud.textBottomRight : null;
            if (version == null || !Plugin.ShowKeyHints.Value)
            {
                if (_legend != null) _legend.gameObject.SetActive(false);
                return;
            }
            if (_legend == null || _source != version) Attach(version);

            var windows = Managers.GetManager<WindowsHandler>();
            string text = Compose(windows == null ? DataConfig.UiType.Null : windows.GetOpenedUi());
            bool show = !string.IsNullOrEmpty(text) && version.gameObject.activeInHierarchy;
            _legend.gameObject.SetActive(show);
            if (!show) return;

            if (text != _lastText) { _legend.text = text; _lastText = text; }

            // Logo acima do texto da versão: margem inferior = altura que o texto ocupa no retângulo.
            float above = version.textBounds.size.y > 0f
                ? version.textBounds.max.y - version.rectTransform.rect.yMin + 4f
                : version.fontSize * 1.3f;
            var m = _legend.margin;
            if (Mathf.Abs(m.w - above) > 0.5f) _legend.margin = new Vector4(m.x, m.y, m.z, above);
        }

        private void Attach(TextMeshProUGUI version)
        {
            _source = version;
            if (_legend != null) Destroy(_legend.gameObject);

            var go = new GameObject("WardensLogistics.KeyLegend", typeof(RectTransform));
            go.transform.SetParent(version.transform.parent, false);
            var rt = (RectTransform)go.transform;
            var src = version.rectTransform;
            rt.anchorMin = src.anchorMin;
            rt.anchorMax = src.anchorMax;
            rt.pivot = src.pivot;
            rt.sizeDelta = src.sizeDelta;
            rt.anchoredPosition = src.anchoredPosition;

            _legend = go.AddComponent<TextMeshProUGUI>();
            _legend.font = version.font;
            _legend.fontSharedMaterial = version.fontSharedMaterial;
            _legend.fontSize = version.fontSize * 0.85f;
            _legend.fontStyle = version.fontStyle;
            _legend.alignment = TextAlignmentOptions.BottomRight;
            _legend.color = new Color(1f, 1f, 1f, 0.72f);
            _legend.enableWordWrapping = false;
            _legend.overflowMode = TextOverflowModes.Overflow;
            _legend.raycastTarget = false;
            _legend.richText = true;
            _lastText = null;
        }

        private static string Compose(DataConfig.UiType ui)
        {
            var parts = new List<string>();
            string store = Plugin.QuickStoreEnabled.Value ? Plugin.QuickStoreKey.Value.ToString() : null;
            string find = Plugin.FinderEnabled.Value ? Plugin.FinderKey.Value.ToString() : null;
            string xray = Plugin.XRayEnabled.Value ? Plugin.XRayKey.Value.ToString() : null;

            switch (ui)
            {
                case DataConfig.UiType.Null:
                    if (store != null) parts.Add(Key(store, "legend.store"));
                    if (find != null) parts.Add(Key(find, "legend.find"));
                    if (xray != null) parts.Add(Key(xray, "legend.xray"));
                    if (xray != null) parts.Add(Key("Ctrl+" + xray, "legend.xray_detail"));
                    break;
                case DataConfig.UiType.Container:
                    if (store != null && Plugin.DepositOpenEnabled.Value) parts.Add(Key(store, "legend.deposit"));
                    if (Pilhas.Stacks.Enabled)
                    {
                        parts.Add(Key(Text.Get("legend.click"), "legend.move_stack"));
                        parts.Add(Key("Shift+" + Text.Get("legend.click"), "legend.move_all"));
                    }
                    if (find != null) parts.Add(Key(find, "legend.where"));
                    break;
                case DataConfig.UiType.Equipment:
                    if (find != null) parts.Add(Key(find, "legend.where"));
                    break;
                case DataConfig.UiType.Construction:
                case DataConfig.UiType.Craft:
                    if (find != null) parts.Add(Key(find, "legend.ingredients"));
                    break;
            }
            return string.Join("   ", parts);
        }

        private static string Key(string key, string labelKey) => $"<b>{key}</b> {Text.Get(labelKey)}";
    }
}
