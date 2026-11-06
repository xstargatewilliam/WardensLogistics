using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WardensLogistics.Core
{
    /// <summary>
    /// Peças de interface no estilo do HUD do jogo, usadas pelo painel do Raio-x e pela
    /// barra de atalhos: caixa escura translúcida com borda branca fina e a fonte do jogo.
    /// </summary>
    internal static class Ui
    {
        public static readonly Color PanelColor = new Color(0.02f, 0.02f, 0.03f, 0.62f);
        public static readonly Color BorderColor = new Color(1f, 1f, 1f, 0.85f);

        private static TMP_FontAsset _font;

        /// <summary>Canvas de sobreposição em 1920x1080 de referência, que sobrevive à troca de cena.</summary>
        public static GameObject NewCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            Object.DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            return go;
        }

        public static RectTransform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// Caixa com borda: retângulo branco por fora e fundo escuro por dentro. Devolve o
        /// interior, onde o conteúdo vai; a caixa se ajusta ao tamanho do conteúdo.
        /// </summary>
        public static RectTransform Box(Transform parent, string name, RectOffset padding, float border = 2f, float spacing = 6f)
        {
            var frame = NewChild(name, parent);
            frame.gameObject.AddComponent<Image>().color = BorderColor;
            int b = Mathf.RoundToInt(border);
            var frameLayout = frame.gameObject.AddComponent<VerticalLayoutGroup>();
            frameLayout.padding = new RectOffset(b, b, b, b);
            frameLayout.childControlWidth = frameLayout.childControlHeight = true;
            var fit = frame.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var inner = NewChild("Inner", frame);
            inner.gameObject.AddComponent<Image>().color = PanelColor;
            var layout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            return inner;
        }

        public static RectTransform Row(Transform parent, float spacing)
        {
            var row = NewChild("Row", parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return row;
        }

        public static void Icon(Transform parent, Sprite sprite, float size)
        {
            var rt = NewChild("Icon", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = size;
        }

        public static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, float spacing = 0f)
        {
            var rt = NewChild("Text", parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = GameFont();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.characterSpacing = spacing;
            tmp.enableWordWrapping = false;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Uma "tecla": moldura branca fina com a letra dentro.</summary>
        public static void KeyCap(Transform parent, string key, float size)
        {
            var inner = Box(parent, "Key", new RectOffset(7, 7, 1, 1), 1.5f, 0f);
            var label = Label(inner, key, size, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.minWidth = size * 0.9f;
        }

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
            t.DetachChildren();
        }

        /// <summary>Fonte do HUD do jogo, tirada de um texto que já está na tela.</summary>
        public static TMP_FontAsset GameFont()
        {
            if (_font != null) return _font;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (t == null || t.font == null) continue;
                if (t.gameObject.name.StartsWith("WardensLogistics") || t.GetComponentInParent<Canvas>() == null) continue;
                if (t.GetComponentInParent<Canvas>().rootCanvas.name.StartsWith("WardensLogistics")) continue;
                _font = t.font;
                break;
            }
            return _font != null ? _font : TMP_Settings.defaultFontAsset;
        }
    }
}
