using UnityEngine;
using WardensLogistics.Core;

namespace WardensLogistics.Logistica
{
    /// <summary>
    /// Controla o Raio-x: liga e desliga com a tecla, refaz a análise a cada poucos
    /// segundos, mostra o painel compacto (XRayHud) e desenha marcadores coloridos sobre
    /// os baús envolvidos em cada problema. Não bloqueia o movimento e some quando há
    /// janela aberta.
    /// </summary>
    internal sealed class XRayOverlay : MonoBehaviour
    {
        private const float RefreshSeconds = 2f;
        private const int MaxMarkers = 25;

        private static bool _visible;
        private static XRay.Report _report;
        private static float _nextRefresh;
        private static bool _hudShown;

        private Texture2D _white;
        private GUIStyle _small, _mark;

        private static readonly Color Critical = new Color(1f, 0.32f, 0.28f);
        private static readonly Color Warning = new Color(1f, 0.72f, 0.2f);

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
            bool shouldShow = _visible && !Game.IsUiOpen;
            if (!shouldShow)
            {
                if (_hudShown) { XRayHud.Hide(); _hudShown = false; }
                return;
            }

            bool refresh = Time.unscaledTime >= _nextRefresh;
            if (refresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                try { _report = XRay.Analyze(); }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogWarning($"[XRay] Analysis failed: {ex}");
                    _report = null;
                }
            }
            if (refresh || !_hudShown)
            {
                XRayHud.Show(_report);
                _hudShown = _report != null;
            }
        }

        private void OnGUI()
        {
            if (!_visible || _report == null || !_report.Available || Game.IsUiOpen) return;
            var camera = Draw.PlayerCamera();
            var player = Game.PlayerPosition;
            if (camera == null || player == null) return;

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            EnsureStyles(scale);
            float size = 30f * scale;
            int drawn = 0;
            foreach (var issue in _report.Issues)
            {
                var color = issue.Level == XRay.Severity.Critical ? Critical : Warning;
                foreach (var place in issue.Places)
                {
                    if (drawn++ >= MaxMarkers) return;
                    Vector3 sp = camera.WorldToScreenPoint(place + Vector3.up * 1.8f);
                    if (sp.z <= 0f) continue;
                    var rect = new Rect(sp.x - size / 2f, Screen.height - sp.y - size, size, size);

                    // Moldura branca fina com fundo escuro, como as caixas do HUD; borda na cor da gravidade.
                    GUI.color = color;
                    GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), _white);
                    GUI.color = new Color(0.02f, 0.02f, 0.03f, 0.8f);
                    GUI.DrawTexture(rect, _white);
                    GUI.color = Color.white;
                    if (issue.Icon != null)
                        Draw.Sprite(new Rect(rect.x + 3f * scale, rect.y + 3f * scale, size - 6f * scale, size - 6f * scale), issue.Icon);
                    else
                    {
                        _mark.normal.textColor = color;
                        GUI.Label(rect, "!", _mark);
                    }

                    float distance = Vector3.Distance(player.Value, place);
                    GUI.Label(new Rect(rect.x - 30f * scale, rect.yMax + 2f, size + 60f * scale, 18f * scale), $"{distance:0} m", _small);
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
            if (_small != null) return;
            _small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(13 * scale), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _small.normal.textColor = Color.white;
            _mark = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20 * scale), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
    }
}
