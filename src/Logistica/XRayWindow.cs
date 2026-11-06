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
    /// Abas, na ordem em que o jogo libera cada assunto:
    /// - Terraformação: medidas do planeta, próximo desbloqueio, tempo estimado e sugestão.
    /// - Produção: itens criados pelas máquinas por minuto.
    /// - Drones (Raio-x): visão geral, problemas com "por quê" e "como resolver", itens na logística.
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
        private static int _tab;
        private static readonly List<TextMeshProUGUI> TabLabels = new List<TextMeshProUGUI>();
        private static readonly List<Image> TabFrames = new List<Image>();
        private static readonly string[] TabKeys = { "w.tab.terraform", "w.tab.production", "w.tab.drones" };

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
            var title = Ui.Label(header, Text.Get("w.title").ToUpperInvariant(), 28f, Color.white, 2f);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Ui.Label(header, Text.Get("xray.w.close", Plugin.XRayKey.Value), 15f, Muted);

            // Abas
            var tabs = Ui.Row(inner, 10f);
            TabLabels.Clear();
            TabFrames.Clear();
            for (int i = 0; i < TabKeys.Length; i++)
            {
                int index = i;
                var box = Ui.Box(tabs, "Tab", new RectOffset(18, 18, 6, 6), 1.5f, 0f);
                var img = box.GetComponent<Image>();
                img.raycastTarget = true;
                var label = Ui.Label(box, Text.Get(TabKeys[i]).ToUpperInvariant(), 16f, Color.white, 1.5f);
                var button = box.gameObject.AddComponent<Button>();
                button.targetGraphic = img;
                button.onClick.AddListener(() => SelectTab(index));
                TabLabels.Add(label);
                TabFrames.Add(img);
            }

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

        private static void SelectTab(int index)
        {
            if (index == _tab) return;
            _tab = index;
            Rebuild(keepScroll: false);
        }

        private static void PaintTabs()
        {
            for (int i = 0; i < TabLabels.Count; i++)
            {
                bool on = i == _tab;
                TabLabels[i].color = on ? Color.black : new Color(1f, 1f, 1f, 0.75f);
                TabFrames[i].color = on ? new Color(1f, 1f, 1f, 0.92f) : new Color(1f, 1f, 1f, 0.04f);
            }
        }

        private static void Rebuild(bool keepScroll)
        {
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            float scrollPos = keepScroll && _scroll != null ? _scroll.verticalNormalizedPosition : 1f;
            PaintTabs();

            if (_tab != 2)
            {
                Ui.ClearChildren(_content);
                try
                {
                    if (_tab == 0) TerraformTab();
                    else ProductionTab();
                }
                catch (System.Exception ex) { Plugin.Log.LogWarning($"[Window] Tab {_tab} failed: {ex}"); }
                Canvas.ForceUpdateCanvases();
                _scroll.verticalNormalizedPosition = scrollPos;
                return;
            }

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

        // ---------- aba Terraformação ----------

        private static void TerraformTab()
        {
            var r = Terraformacao.Terraform.Analyze();
            if (!r.Available) { Paragraph(_content, Text.Get("tf.loading"), 16f, Muted); return; }

            // Sugestão
            Section(Text.Get("tf.suggestion"));
            if (r.Stalled != null)
                Paragraph(_content, Text.Get("tf.stalled", r.Stalled.Name, Builder(r.Stalled)), 17f, Warning);
            if (r.Soonest != null)
            {
                string what = What(r.Soonest);
                Paragraph(_content, Text.Get("tf.soonest", what, Terraformacao.Terraform.Duration(r.Soonest.Eta), r.Soonest.Name), 17f, Color.white);
                if (r.Soonest.BestBuilder != null)
                    Paragraph(_content, Text.Get("tf.speed_up", Readable.GetGroupName(r.Soonest.BestBuilder), r.Soonest.BestBuilderRateText), 16f, Accent);
            }
            else if (r.Stalled == null)
                Paragraph(_content, Text.Get("tf.nothing_next"), 16f, Ok);

            // Medidas
            Section(Text.Get("tf.units"));
            TfRow(null, Text.Get("tf.col_unit"), Text.Get("tf.col_value"), Text.Get("tf.col_rate"), Text.Get("tf.col_next"), Text.Get("tf.col_eta"), Text.Get("tf.col_best"), true);
            foreach (var row in r.Rows)
            {
                string next = row.NextValue < 0 ? "—" : $"{row.NextValueText}  ·  {What(row)}";
                string eta = row.NextValue < 0 ? "—" : row.PerSecond <= 0 ? Text.Get("tf.stopped") : Terraformacao.Terraform.Duration(row.Eta);
                string best = row.BestBuilder == null ? "—" : $"{Readable.GetGroupName(row.BestBuilder)} ({row.BestBuilderRateText})";
                Sprite icon = row.NextGroups.Count > 0 ? row.NextGroups[0].GetImage() : null;
                TfRow(icon, row.Name, row.Value, row.Rate, next, eta, best, false);
            }
            Paragraph(_content, Text.Get("tf.note"), 14f, Muted);
        }

        private static string What(Terraformacao.Terraform.UnitRow row)
        {
            if (row.NextStage != null) return row.NextStage;
            if (row.NextGroups.Count == 0) return "—";
            string first = Readable.GetGroupName(row.NextGroups[0]);
            return row.NextGroups.Count == 1 ? first : Text.Get("tf.and_more", first, row.NextGroups.Count - 1);
        }

        private static string Builder(Terraformacao.Terraform.UnitRow row) =>
            row.BestBuilder != null ? Readable.GetGroupName(row.BestBuilder) : Text.Get("tf.any_builder");

        private static void TfRow(Sprite icon, string unit, string value, string rate, string next, string eta, string best, bool header)
        {
            var row = Ui.Row(_content, 0f);
            float size = header ? 14f : 16f;
            var c = header ? Muted : Color.white;
            Cell(row, unit, 170f, size, header ? Muted : Accent, TextAlignmentOptions.Left);
            Cell(row, value, 140f, size, c, TextAlignmentOptions.Left);
            Cell(row, rate, 140f, size, c, TextAlignmentOptions.Left);
            var iconCell = Ui.NewChild("IconCell", row);
            iconCell.gameObject.AddComponent<LayoutElement>().preferredWidth = 34f;
            if (icon != null)
            {
                var img = Ui.NewChild("Icon", iconCell);
                img.sizeDelta = new Vector2(26f, 26f);
                img.gameObject.AddComponent<Image>().sprite = icon;
                img.GetComponent<Image>().preserveAspect = true;
            }
            Cell(row, next, 330f, size, c, TextAlignmentOptions.Left);
            Cell(row, eta, 110f, size, c, TextAlignmentOptions.Left);
            Cell(row, best, 220f, size, header ? Muted : new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Left);
        }

        // ---------- aba Produção ----------

        private static void ProductionTab()
        {
            Section(Text.Get("prod.title"));
            var net = Unity.Netcode.NetworkManager.Singleton;
            if (net != null && net.IsListening && !net.IsServer)
            {
                Paragraph(_content, Text.Get("prod.host_only"), 16f, Muted);
                return;
            }
            float measured = Production.MeasuredSeconds;
            Paragraph(_content, Text.Get("prod.explain", Terraformacao.Terraform.Duration(measured), (int)(Production.WindowSeconds / 60)), 15f, Muted);
            var rates = Production.Rates();
            if (rates.Count == 0) { Paragraph(_content, Text.Get("prod.none"), 16f, Muted); return; }

            var stock = new Dictionary<Group, int>();
            foreach (var e in ContainerIndex.All())
                foreach (var wo in e.Inventory.GetInsideWorldObjects())
                {
                    var g = wo?.GetGroup();
                    if (g == null) continue;
                    stock.TryGetValue(g, out int n);
                    stock[g] = n + 1;
                }

            ProdRow(null, Text.Get("xray.w.col_item"), Text.Get("prod.col_rate"), Text.Get("prod.col_hour"), Text.Get("xray.w.col_stock"), Muted, true);
            int shown = 0;
            foreach (var rate in rates)
            {
                if (shown++ >= 60) break;
                stock.TryGetValue(rate.Group, out int n);
                ProdRow(rate.Group.GetImage(), Readable.GetGroupName(rate.Group), rate.PerMinute.ToString(rate.PerMinute < 10 ? "0.0" : "0"),
                    (rate.PerMinute * 60f).ToString("0"), n.ToString(), Color.white, false);
            }
        }

        private static void ProdRow(Sprite icon, string name, string perMinute, string perHour, string stock, Color color, bool header)
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
            Cell(row, name, 400f, size, header ? Muted : color, TextAlignmentOptions.Left);
            Cell(row, perMinute, 160f, size, header ? Muted : Ok, TextAlignmentOptions.Center);
            Cell(row, perHour, 160f, size, header ? Muted : Color.white, TextAlignmentOptions.Center);
            Cell(row, stock, 160f, size, header ? Muted : Color.white, TextAlignmentOptions.Center);
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
