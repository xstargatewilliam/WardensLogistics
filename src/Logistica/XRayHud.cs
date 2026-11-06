using System.Collections.Generic;
using System.IO;
using SpaceCraft;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WardensLogistics.Core;
using Text = WardensLogistics.Core.Text;

namespace WardensLogistics.Logistica
{
    /// <summary>
    /// Painel compacto do Raio-x, no estilo do HUD do jogo: caixa escura translúcida com
    /// borda branca fina, fonte do próprio jogo e ícones no lugar de texto corrido.
    ///
    /// Linha 1: indicadores (drones, estações, entregas pendentes e, se há estação, energia).
    /// Linhas seguintes: até 5 problemas, cada um com uma faixa de gravidade, ícone e texto curto.
    ///
    /// Ícones: o painel procura PNGs em BepInEx/plugins/WardensLogistics/icons/
    /// (drone.png, station.png, delivery.png, energy.png, critical.png, warning.png).
    /// Sem arquivo, usa o ícone de um item do próprio jogo.
    /// </summary>
    internal static class XRayHud
    {
        private const int MaxRows = 5;
        private static readonly Color Panel = new Color(0.02f, 0.02f, 0.03f, 0.62f);
        private static readonly Color Border = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color Critical = new Color(1f, 0.32f, 0.28f);
        private static readonly Color Warning = new Color(1f, 0.72f, 0.2f);
        private static readonly Color Ok = new Color(0.55f, 0.95f, 0.6f);

        private static GameObject _canvas;
        private static RectTransform _panel;
        private static TMP_FontAsset _font;
        private static string _iconFolder;
        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

        public static void SetIconFolder(string pluginFolder) => _iconFolder = Path.Combine(pluginFolder, "icons");

        public static void Hide()
        {
            if (_canvas != null) _canvas.SetActive(false);
        }

        public static void Show(XRay.Report report)
        {
            if (report == null) { Hide(); return; }
            EnsureCanvas();
            _canvas.SetActive(true);
            Rebuild(report);
        }

        private static void EnsureCanvas()
        {
            if (_canvas != null) return;
            _canvas = new GameObject("WardensLogistics.XRayHud");
            Object.DontDestroyOnLoad(_canvas);
            var canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = _canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            // Caixa com borda: um retângulo branco e, dentro dele, o fundo escuro com 1,5 px de margem.
            var frame = NewChild("Frame", _canvas.transform);
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(1f, 1f);
            frame.anchoredPosition = new Vector2(-38f, -128f); // abaixo do Índice de Terraformação
            frame.gameObject.AddComponent<Image>().color = Border;
            var frameFit = frame.gameObject.AddComponent<VerticalLayoutGroup>();
            frameFit.padding = new RectOffset(2, 2, 2, 2);
            frameFit.childControlWidth = frameFit.childControlHeight = true;
            frame.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            frame.gameObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _panel = NewChild("Panel", frame);
            _panel.gameObject.AddComponent<Image>().color = Panel;
            var layout = _panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 16, 10, 12);
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
        }

        private static void Rebuild(XRay.Report r)
        {
            for (int i = _panel.childCount - 1; i >= 0; i--) Object.Destroy(_panel.GetChild(i).gameObject);
            _panel.DetachChildren();

            Label(_panel, Text.Get("xray.title").ToUpperInvariant(), 15f, new Color(1f, 1f, 1f, 0.9f), 2f);

            if (!r.Available)
            {
                Label(_panel, Text.Get("xray.host_only"), 16f, Color.white);
                return;
            }

            // Indicadores
            var stats = Row(_panel, 18f);
            Chip(stats, IconOrGame("drone", GameIcons.Drone), (r.DronesInStations + r.DronesFlying).ToString(), Color.white);
            Chip(stats, IconOrGame("station", GameIcons.Station), r.Stations.ToString(), Color.white);
            Chip(stats, IconOrGame("delivery", GameIcons.Delivery), r.TasksWaiting.ToString(),
                r.TasksWaiting > 0 && r.DronesInStations == 0 ? Warning : Color.white);
            if (r.Stations > 0)
                Chip(stats, IconOrGame("energy", GameIcons.Energy), r.Energy ? "OK" : "!", r.Energy ? Ok : Critical);

            // Problemas
            if (r.Issues.Count == 0)
            {
                Label(_panel, Text.Get("xray.ok"), 16f, Ok);
                return;
            }
            for (int i = 0; i < r.Issues.Count && i < MaxRows; i++)
            {
                var issue = r.Issues[i];
                var color = issue.Level == XRay.Severity.Critical ? Critical : Warning;
                var row = Row(_panel, 10f);
                var bar = NewChild("Severity", row);
                bar.gameObject.AddComponent<Image>().color = color;
                var barLayout = bar.gameObject.AddComponent<LayoutElement>();
                barLayout.preferredWidth = 3f;
                barLayout.preferredHeight = 26f;

                var icon = issue.Icon ?? IconOrGame(issue.Level == XRay.Severity.Critical ? "critical" : "warning", null);
                if (icon != null) Icon(row, icon, 26f);
                Label(row, issue.Text, 16f, Color.white);
            }
            if (r.Issues.Count > MaxRows)
                Label(_panel, Text.Get("xray.more", r.Issues.Count - MaxRows), 14f, new Color(1f, 1f, 1f, 0.6f));
        }

        // ---------- peças da interface ----------

        private static RectTransform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Row(Transform parent, float spacing)
        {
            var row = NewChild("Row", parent);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childControlWidth = h.childControlHeight = true;
            h.childForceExpandWidth = h.childForceExpandHeight = false;
            return row;
        }

        private static void Chip(Transform parent, Sprite icon, string value, Color color)
        {
            var chip = Row(parent, 6f);
            if (icon != null) Icon(chip, icon, 28f);
            Label(chip, value, 20f, color);
        }

        private static void Icon(Transform parent, Sprite sprite, float size)
        {
            var rt = NewChild("Icon", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = size;
        }

        private static TextMeshProUGUI Label(Transform parent, string text, float size, Color color, float spacing = 0f)
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

        /// <summary>Fonte do HUD do jogo (a do título "Índice de Terraformação" e das listas).</summary>
        private static TMP_FontAsset GameFont()
        {
            if (_font != null) return _font;
            foreach (var t in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (t == null || t.font == null) continue;
                if (t.GetComponentInParent<Canvas>() == null || t.transform.IsChildOf(_canvas.transform)) continue;
                _font = t.font;
                break;
            }
            return _font ?? TMP_Settings.defaultFontAsset;
        }

        /// <summary>PNG da pasta icons/ se existir; senão o ícone de reserva do jogo.</summary>
        private static Sprite IconOrGame(string name, System.Func<Sprite> fallback)
        {
            if (!Icons.TryGetValue(name, out var sprite))
            {
                sprite = LoadPng(name);
                Icons[name] = sprite; // guarda até o null, para não procurar o arquivo de novo
            }
            return sprite != null ? sprite : fallback?.Invoke();
        }

        private static Sprite LoadPng(string name)
        {
            try
            {
                if (_iconFolder == null) return null;
                string path = Path.Combine(_iconFolder, name + ".png");
                if (!File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path))) return null;
                tex.filterMode = FilterMode.Bilinear;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[XRay] Could not load icon {name}.png: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>Ícones de reserva tirados dos próprios itens do jogo.</summary>
    internal static class GameIcons
    {
        private static Sprite _drone, _station, _delivery, _energy;

        public static Sprite Drone() => _drone ?? (_drone = FirstById("Drone1", "Drone"));
        public static Sprite Delivery() => _delivery ?? (_delivery = FirstById("Container1", "Container"));
        public static Sprite Energy() => _energy ?? (_energy = FirstById("EnergyGenerator1", "Energy"));

        public static Sprite Station()
        {
            if (_station != null) return _station;
            var lm = Managers.GetManager<LogisticManager>();
            if (lm != null && lm.droneStationGroup != null)
            {
                var g = GroupsHandler.GetGroupViaId(lm.droneStationGroup.id);
                if (g != null) _station = g.GetImage();
            }
            return _station;
        }

        /// <summary>Ícone do item com o id exato ou, se não existir, do primeiro cujo id começa com o prefixo.</summary>
        private static Sprite FirstById(string exactId, string prefix)
        {
            var exact = GroupsHandler.GetGroupViaId(exactId);
            if (exact != null) return exact.GetImage();
            foreach (var g in GroupsHandler.GetAllGroups())
                if (g != null && g.GetId().StartsWith(prefix) && g.GetImage() != null) return g.GetImage();
            return null;
        }
    }
}
