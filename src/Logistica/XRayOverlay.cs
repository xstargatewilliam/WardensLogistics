using System.Collections.Generic;
using UnityEngine;
using WardensLogistics.Core;

namespace WardensLogistics.Logistica
{
    /// <summary>
    /// Painel do Raio-x (canto superior direito) e marcadores coloridos sobre os baús
    /// envolvidos em cada problema. Liga e desliga com a tecla do Raio-x; enquanto está
    /// ligado, a análise é refeita a cada poucos segundos. Não bloqueia o movimento e
    /// fica escondido quando há janela aberta.
    /// </summary>
    internal sealed class XRayOverlay : MonoBehaviour
    {
        private const float RefreshSeconds = 2f;
        private const int MaxIssueLines = 8;
        private const int MaxMarkers = 25;

        private static bool _visible;
        private static XRay.Report _report;
        private static float _nextRefresh;

        private Texture2D _white;
        private GUIStyle _title, _line, _small;

        private static readonly Color Critical = new Color(1f, 0.35f, 0.3f);
        private static readonly Color Warning = new Color(1f, 0.75f, 0.25f);
        private static readonly Color Ok = new Color(0.45f, 0.9f, 0.55f);

        public static void Toggle()
        {
            _visible = !_visible;
            _nextRefresh = 0f;
            if (!_visible) _report = null;
        }

        public static void Create()
        {
            var go = new GameObject("WardensLogistics.XRay");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<XRayOverlay>();
        }

        private void Update()
        {
            if (!_visible || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            try { _report = XRay.Analyze(); }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[XRay] Analysis failed: {ex}");
                _report = null;
            }
        }

        private void OnGUI()
        {
            if (!_visible || _report == null || Game.IsUiOpen) return;
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            EnsureStyles(scale);
            DrawMarkers(scale);
            DrawPanel(scale);
        }

        private void DrawPanel(float scale)
        {
            var r = _report;
            float width = 470f * scale, pad = 10f * scale, lineH = 24f * scale;
            var lines = new List<(Color color, Sprite icon, string text)>();

            if (!r.Available)
            {
                lines.Add((Color.white, null, Text.Get("xray.host_only")));
            }
            else
            {
                lines.Add((Color.white, null, Text.Get("xray.drones", r.DronesInStations + r.DronesFlying, r.DronesFlying, r.DronesInStations, r.Stations)));
                lines.Add((Color.white, null, Text.Get("xray.tasks", r.TasksWaiting, r.TasksToSupply, r.TasksToDemand)));
                lines.Add((Color.white, null, Text.Get("xray.containers", r.DemandContainers, r.SupplyContainers)));
                lines.Add((r.Energy ? Ok : Critical, null, Text.Get(r.Energy ? "xray.energy_ok" : "xray.energy_low")));

                if (r.Issues.Count == 0)
                    lines.Add((Ok, null, Text.Get("xray.all_good")));
                for (int i = 0; i < r.Issues.Count && i < MaxIssueLines; i++)
                {
                    var issue = r.Issues[i];
                    lines.Add((issue.Level == XRay.Severity.Critical ? Critical : Warning, issue.Icon, issue.Text));
                }
                if (r.Issues.Count > MaxIssueLines)
                    lines.Add((Color.gray, null, Text.Get("xray.more", r.Issues.Count - MaxIssueLines)));
            }

            float height = pad + 28f * scale + lines.Count * lineH + 20f * scale + pad;
            var box = new Rect(Screen.width - width - 16f * scale, 90f * scale, width, height);
            GUI.color = new Color(0.03f, 0.06f, 0.12f, 0.82f);
            GUI.DrawTexture(box, _white);
            GUI.color = new Color(0.25f, 0.55f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(box.x, box.y, 4f * scale, box.height), _white); // faixa azul "Warden"
            GUI.color = Color.white;

            string title = Text.Get("xray.title") + (r.Available && r.Issues.Count > 0 ? $"  ({r.Issues.Count})" : "");
            GUI.Label(new Rect(box.x + pad, box.y + pad, width - 2 * pad, 26f * scale), title, _title);

            float y = box.y + pad + 28f * scale;
            float icon = lineH - 4f * scale;
            foreach (var (color, sprite, text) in lines)
            {
                float x = box.x + pad;
                if (sprite != null)
                {
                    DrawSprite(new Rect(x, y + 2f * scale, icon, icon), sprite);
                    x += icon + 6f * scale;
                }
                _line.normal.textColor = color;
                GUI.Label(new Rect(x, y, box.xMax - pad - x, lineH), text, _line);
                y += lineH;
            }
            GUI.Label(new Rect(box.x + pad, y + 2f * scale, width - 2 * pad, 18f * scale),
                Text.Get("xray.footer", Plugin.XRayKey.Value), _small);
        }

        private void DrawMarkers(float scale)
        {
            if (_report == null || !_report.Available) return;
            var camera = Draw.PlayerCamera();
            var player = Game.PlayerPosition;
            if (camera == null || player == null) return;

            int drawn = 0;
            float size = 30f * scale;
            foreach (var issue in _report.Issues)
            {
                var color = issue.Level == XRay.Severity.Critical ? Critical : Warning;
                foreach (var place in issue.Places)
                {
                    if (drawn++ >= MaxMarkers) return;
                    Vector3 sp = camera.WorldToScreenPoint(place + Vector3.up * 1.8f);
                    if (sp.z <= 0f) continue;
                    var rect = new Rect(sp.x - size / 2f, Screen.height - sp.y - size, size, size);

                    GUI.color = new Color(color.r, color.g, color.b, 0.85f);
                    GUI.DrawTexture(rect, _white);
                    GUI.color = Color.white;
                    if (issue.Icon != null)
                        DrawSprite(new Rect(rect.x + 3f * scale, rect.y + 3f * scale, size - 6f * scale, size - 6f * scale), issue.Icon);
                    else
                        GUI.Label(rect, "!", _title);

                    float distance = Vector3.Distance(player.Value, place);
                    _small.normal.textColor = color;
                    GUI.Label(new Rect(rect.x - 30f * scale, rect.yMax, size + 60f * scale, 18f * scale), $"{distance:0} m", _small);
                    _small.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
                }
            }
        }

        private void EnsureStyles(float scale)
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(18 * scale), alignment = TextAnchor.MiddleLeft };
            _title.normal.textColor = new Color(0.55f, 0.8f, 1f);
            _line = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * scale), alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(12 * scale), alignment = TextAnchor.MiddleCenter };
            _small.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
        }

        private static void DrawSprite(Rect rect, Sprite sprite) => Draw.Sprite(rect, sprite);
    }
}
