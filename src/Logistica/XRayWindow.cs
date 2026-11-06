using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WardensLogistics.Core;
using Text = WardensLogistics.Core.Text;

namespace WardensLogistics.Logistica
{
    /// <summary>
    /// Janela detalhada do Raio-x (Ctrl+N). Comporta-se como uma janela do jogo: enquanto
    /// está aberta, o jogo é informado de que há uma janela aberta (o personagem para e o
    /// cursor aparece), e ela fecha junto quando o jogo fecha as janelas (Esc ou outra
    /// janela abrindo).
    ///
    /// Conteúdo, com rolagem pela roda do mouse:
    /// 1. Visão geral: drones, estações, entregas e energia, cada um com uma explicação.
    /// 2. Problemas: o que acontece, por quê, como resolver e quais baús (nome e distância).
    /// 3. Itens na logística: por item, quantos baús pedem e oferecem, estoque, em entrega e situação.
    /// </summary>
    internal static class XRayWindow
    {
        private const float RefreshSeconds = 3f;
        private const float Width = 1180f;
        private const float ScrollHeight = 760f;

        private static readonly Color Critical = new Color(1f, 0.32f, 0.28f);
        private static readonly Color Warning = new Color(1f, 0.72f, 0.2f);
        private static readonly Color Ok = new Color(0.55f, 0.95f, 0.6f);
        private static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Accent = new Color(0.62f, 0.85f, 1f);

        private static GameObject _canvas;
        private static RectTransform _content;
        private static ScrollRect _scroll;
        private static bool _open;
        private static bool _unlockedCursor;
        private static float _nextRefresh;

        public static bool IsOpen => _open;

        public static void Toggle()
        {
            if (_open) Close(); else Open();
        }

        public static void Open()
        {
            var windows = Managers.GetManager<WindowsHandler>();
            if (windows == null || _open) return;
            EnsureCanvas();
            _open = true;
            _canvas.SetActive(true);
            _unlockedCursor = Cursor.lockState == CursorLockMode.Locked;
            if (_unlockedCursor) CursorStateManager.SetLockCursorStatus(isLocked: false);
            Rebuild(keepScroll: false);
        }

        public static void Close()
        {
            if (!_open) return;
            _open = false;
            if (_canvas != null) _canvas.SetActive(false);
            if (_unlockedCursor) CursorStateManager.SetLockCursorStatus(isLocked: true);
            _unlockedCursor = false;
        }

        /// <summary>Chamado a cada quadro: atualiza o conteúdo a cada poucos segundos.</summary>
        public static void Tick()
        {
            if (!_open || Time.unscaledTime < _nextRefresh) return;
            Rebuild(keepScroll: true);
        }

        // ---------- integração com o sistema de janelas do jogo ----------

        [HarmonyPatch(typeof(WindowsHandler), nameof(WindowsHandler.GetHasUiOpen))]
        private static class HasUiOpenPatch
        {
            private static void Postfix(ref bool __result) { if (_open) __result = true; }
        }

        [HarmonyPatch(typeof(WindowsHandler), nameof(WindowsHandler.CloseAllWindows))]
        private static class CloseAllPatch
        {
            private static void Postfix() => Close();
        }

        // ---------- construção da janela ----------

        private static void EnsureCanvas()
        {
            if (_canvas != null) return;
            _canvas = Ui.NewCanvas("WardensLogistics.XRayWindow", 60);
            _canvas.AddComponent<GraphicRaycaster>();

            // Escurece o jogo atrás da janela.
            var dim = Ui.NewChild("Dim", _canvas.transform);
            dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
            dim.offsetMin = dim.offsetMax = Vector2.zero;
            dim.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var inner = Ui.Box(_canvas.transform, "Window", new RectOffset(26, 26, 20, 20), 2f, 12f);
            var frame = (RectTransform)inner.parent;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;
            inner.GetComponent<Image>().color = new Color(0.02f, 0.02f, 0.03f, 0.9f);

            // Cabeçalho
            var header = Ui.Row(inner, 20f);
            var title = Ui.Label(header, Text.Get("xray.title").ToUpperInvariant(), 28f, Color.white, 2f);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Ui.Label(header, Text.Get("xray.w.close", Plugin.XRayKey.Value), 15f, Muted);

            // Área com rolagem
            var scrollGo = Ui.NewChild("Scroll", inner);
            var le = scrollGo.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = Width;
            le.preferredHeight = ScrollHeight;
            scrollGo.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.001f); // recebe a roda do mouse
            scrollGo.gameObject.AddComponent<RectMask2D>();
            _scroll = scrollGo.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;

            _content = Ui.NewChild("Content", scrollGo);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.offsetMin = _content.offsetMax = Vector2.zero;
            var v = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 14f;
            v.padding = new RectOffset(2, 18, 2, 10);
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content = _content;
            _scroll.viewport = scrollGo;
            _canvas.SetActive(false);
        }

        private static void Rebuild(bool keepScroll)
        {
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            float scrollPos = keepScroll && _scroll != null ? _scroll.verticalNormalizedPosition : 1f;

            XRay.Report r;
            try { r = XRay.Analyze(); }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[XRay] Analysis failed: {ex}");
                return;
            }

            Ui.ClearChildren(_content);
            if (!r.Available)
            {
                Paragraph(_content, Text.Get("xray.w.host_only"), 17f, Color.white);
                return;
            }

            Overview(r);
            Problems(r);
            Items(r);

            Canvas.ForceUpdateCanvases();
            _scroll.verticalNormalizedPosition = scrollPos;
        }

        private static void Section(string title)
        {
            var row = Ui.Row(_content, 10f);
            Ui.Label(row, title.ToUpperInvariant(), 17f, Accent, 2f);
        }

        // 1. Visão geral
        private static void Overview(XRay.Report r)
        {
            Section(Text.Get("xray.w.overview"));
            var cards = Ui.Row(_content, 14f);
            int tasks = r.TasksWaiting + r.TasksToSupply + r.TasksToDemand;

            Card(cards, XRayHud.Icon("drone", GameIcons.Drone), (r.DronesInStations + r.DronesFlying).ToString(), Color.white,
                Text.Get("xray.w.drones"), Text.Get("xray.w.drones_d", r.DronesFlying, r.DronesInStations));
            Card(cards, XRayHud.Icon("station", GameIcons.Station), r.Stations.ToString(), r.Stations == 0 && r.DemandContainers > 0 ? Critical : Color.white,
                Text.Get("xray.w.stations"), Text.Get("xray.w.stations_d"));
            Card(cards, XRayHud.Icon("delivery", GameIcons.Delivery), tasks.ToString(), Color.white,
                Text.Get("xray.w.tasks"), Text.Get("xray.w.tasks_d", r.TasksWaiting, r.TasksToSupply, r.TasksToDemand));
            if (r.Stations == 0)
                Card(cards, XRayHud.Icon("energy", GameIcons.Energy), "—", Muted, Text.Get("xray.w.energy"), Text.Get("xray.w.energy_na"));
            else
                Card(cards, XRayHud.Icon("energy", GameIcons.Energy), r.Energy ? "OK" : "!", r.Energy ? Ok : Critical,
                    Text.Get("xray.w.energy"), Text.Get(r.Energy ? "xray.w.energy_ok" : "xray.w.energy_low"));

            Paragraph(_content, Text.Get("xray.w.containers", r.DemandContainers, r.SupplyContainers), 15f, Muted);
        }

        private static void Card(Transform parent, Sprite icon, string value, Color valueColor, string label, string description)
        {
            var box = Ui.Box(parent, "Card", new RectOffset(14, 14, 10, 12), 1.5f, 4f);
            box.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
            var le = box.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 270f;
            le.minHeight = 128f;
            var top = Ui.Row(box, 12f);
            if (icon != null) Ui.Icon(top, icon, 46f);
            var numbers = Ui.NewChild("Numbers", top);
            var v = numbers.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlWidth = v.childControlHeight = true;
            v.childForceExpandHeight = false;
            Ui.Label(numbers, value, 30f, valueColor);
            Ui.Label(numbers, label, 15f, Muted);
            Paragraph(box, description, 14f, new Color(1f, 1f, 1f, 0.85f), 240f);
        }

        // 2. Problemas
        private static void Problems(XRay.Report r)
        {
            Section(Text.Get("xray.w.problems", r.Issues.Count));
            if (r.Issues.Count == 0)
            {
                Paragraph(_content, Text.Get(r.DemandContainers + r.SupplyContainers == 0 ? "xray.w.no_logistics" : "xray.w.all_good"), 16f, Ok);
                return;
            }
            var player = Game.PlayerPosition;
            foreach (var issue in r.Issues)
            {
                var color = issue.Level == XRay.Severity.Critical ? Critical : Warning;
                var card = Ui.Box(_content, "Problem", new RectOffset(16, 16, 10, 12), 1.5f, 5f);
                card.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.07f);

                var head = Ui.Row(card, 10f);
                var icon = issue.Icon ?? XRayHud.Icon(issue.Level == XRay.Severity.Critical ? "critical" : "warning", null);
                if (icon != null) Ui.Icon(head, icon, 34f);
                Ui.Label(head, issue.Title, 20f, color);

                Labeled(card, Text.Get("xray.w.why"), issue.Why, Color.white);
                Labeled(card, Text.Get("xray.w.fix"), issue.Fix, Accent);

                if (issue.Involved.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var p in issue.Involved)
                    {
                        if (names.Count == 5) { names.Add(Text.Get("xray.w.and_more", issue.Involved.Count - 5)); break; }
                        string d = player.HasValue ? $" ({Vector3.Distance(player.Value, p.Position):0} m)" : "";
                        names.Add(p.Name + d);
                    }
                    Labeled(card, Text.Get("xray.w.where"), string.Join(" · ", names), Muted);
                }
            }
            Paragraph(_content, Text.Get("xray.w.markers_hint", Plugin.XRayKey.Value), 14f, Muted);
        }

        private static void Labeled(Transform parent, string label, string text, Color color)
        {
            Paragraph(parent, $"<b>{label}</b>  {text}", 16f, color, Width - 80f);
        }

        // 3. Itens na logística
        private static void Items(XRay.Report r)
        {
            Section(Text.Get("xray.w.items"));
            if (r.Items.Count == 0)
            {
                Paragraph(_content, Text.Get("xray.w.no_items"), 16f, Muted);
                return;
            }
            TableRow(null, Text.Get("xray.w.col_item"), Text.Get("xray.w.col_demand"), Text.Get("xray.w.col_supply"),
                Text.Get("xray.w.col_stock"), Text.Get("xray.w.col_delivery"), Text.Get("xray.w.col_status"), Muted, true);
            foreach (var row in r.Items)
            {
                var (status, color) = StatusText(row.Status);
                TableRow(row.Group.GetImage(), Readable.GetGroupName(row.Group), row.DemandedBy.ToString(), row.SuppliedBy.ToString(),
                    row.Stock.ToString(), row.InDelivery.ToString(), status, color, false);
            }
        }

        private static (string, Color) StatusText(XRay.ItemStatus s)
        {
            switch (s)
            {
                case XRay.ItemStatus.NoSupplier: return (Text.Get("xray.w.st_no_supplier"), Critical);
                case XRay.ItemStatus.SupplierEmpty: return (Text.Get("xray.w.st_empty"), Warning);
                case XRay.ItemStatus.Stuck: return (Text.Get("xray.w.st_stuck"), Warning);
                case XRay.ItemStatus.Idle: return (Text.Get("xray.w.st_idle"), Muted);
                default: return (Text.Get("xray.w.st_ok"), Ok);
            }
        }

        private static void TableRow(Sprite icon, string name, string demand, string supply, string stock, string delivery,
            string status, Color statusColor, bool header)
        {
            var row = Ui.Row(_content, 0f);
            float size = header ? 14f : 16f;
            var iconCell = Ui.NewChild("IconCell", row);
            iconCell.gameObject.AddComponent<LayoutElement>().preferredWidth = 40f;
            if (icon != null)
            {
                var img = Ui.NewChild("Icon", iconCell);
                img.sizeDelta = new Vector2(28f, 28f);
                img.gameObject.AddComponent<Image>().sprite = icon;
                img.GetComponent<Image>().preserveAspect = true;
            }
            Cell(row, name, 330f, size, header ? Muted : Color.white, TextAlignmentOptions.Left);
            Cell(row, demand, 120f, size, header ? Muted : Color.white, TextAlignmentOptions.Center);
            Cell(row, supply, 120f, size, header ? Muted : Color.white, TextAlignmentOptions.Center);
            Cell(row, stock, 120f, size, header ? Muted : Color.white, TextAlignmentOptions.Center);
            Cell(row, delivery, 120f, size, header ? Muted : Color.white, TextAlignmentOptions.Center);
            Cell(row, status, 300f, size, statusColor, TextAlignmentOptions.Left);
        }

        private static void Cell(Transform row, string text, float width, float size, Color color, TextAlignmentOptions align)
        {
            var label = Ui.Label(row, text, size, color);
            label.alignment = align;
            label.overflowMode = TextOverflowModes.Ellipsis;
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.minHeight = 32f;
        }

        private static void Paragraph(Transform parent, string text, float size, Color color, float width = Width - 40f)
        {
            var label = Ui.Label(parent, text, size, color);
            label.enableWordWrapping = true;
            label.richText = true;
            label.gameObject.AddComponent<LayoutElement>().preferredWidth = width;
        }
    }
}
