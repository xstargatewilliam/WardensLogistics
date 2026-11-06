using System.Collections.Generic;
using SpaceCraft;
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
        private static readonly Color RoutePick = new Color(0.45f, 0.75f, 1f, 0.85f);
        private static readonly Color RouteCarry = new Color(0.5f, 1f, 0.55f, 0.9f);
        private static readonly Color RouteWait = new Color(1f, 1f, 1f, 0.28f);
        private const int MaxRoutes = 80;
        private const float RouteRange = 350f;

        private struct Route { public Vector3 From, To; public Color Color; }
        private static readonly List<Route> Routes = new List<Route>();

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
            XRayWindow.Tick();
            if (_visible && !Progress.Unlocked(Progress.Feature.Logistics)) Toggle();
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
                try { _report = XRay.Analyze(); CollectRoutes(); }
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

        /// <summary>
        /// Rotas dos drones: uma linha de cada entrega, do baú que oferece (ou do item no
        /// chão) até o baú que pede. Azul: drone indo buscar; verde: drone levando;
        /// cinza: entrega esperando drone. Só as entregas perto do jogador.
        /// </summary>
        private static void CollectRoutes()
        {
            Routes.Clear();
            if (!Plugin.XRayRoutes.Value) return;
            var lm = Managers.GetManager<LogisticManager>();
            var player = Game.PlayerPosition;
            if (lm == null || player == null) return;
            var tasks = lm.GetAllCurrentTasks();
            if (tasks == null) return;
            float range2 = RouteRange * RouteRange;
            foreach (var task in tasks.Values)
            {
                if (Routes.Count >= MaxRoutes) break;
                var from = task.GetIsSpawnedObject() ? task.GetWorldObjectToMove() : task.GetSupplyInventoryWorldObject();
                var to = task.GetDemandInventoryWorldObject();
                if (from == null || to == null) continue;
                Vector3 a = from.GetPosition(), b = to.GetPosition();
                if (a == Vector3.zero || b == Vector3.zero) continue;
                if ((a - player.Value).sqrMagnitude > range2 && (b - player.Value).sqrMagnitude > range2) continue;
                Color c;
                switch (task.GetTaskState())
                {
                    case LogisticData.TaskState.ToSupply:
                    case LogisticData.TaskState.Loading: c = RoutePick; break;
                    case LogisticData.TaskState.ToDemand:
                    case LogisticData.TaskState.Unloading: c = RouteCarry; break;
                    case LogisticData.TaskState.NotAttributed: c = RouteWait; break;
                    default: continue;
                }
                Routes.Add(new Route { From = a + Vector3.up * 1.2f, To = b + Vector3.up * 1.2f, Color = c });
            }
        }

        private void DrawRoutes(Camera camera, float scale)
        {
            foreach (var r in Routes)
            {
                if (!ToScreen(camera, r.From, r.To, out var a, out var b)) continue;
                Line(a, b, r.Color, 2f * scale);
                // seta no meio, apontando para o baú que pede
                Vector2 mid = (a + b) * 0.5f, dir = (b - a).normalized;
                if (dir.sqrMagnitude < 0.5f) continue;
                Vector2 side = new Vector2(-dir.y, dir.x);
                float s = 7f * scale;
                Line(mid, mid - dir * s + side * s * 0.7f, r.Color, 2f * scale);
                Line(mid, mid - dir * s - side * s * 0.7f, r.Color, 2f * scale);
            }
        }

        /// <summary>Projeta um segmento na tela, cortando a parte que fica atrás da câmera.</summary>
        private static bool ToScreen(Camera camera, Vector3 p, Vector3 q, out Vector2 a, out Vector2 b)
        {
            a = b = default;
            var fwd = camera.transform.forward;
            var origin = camera.transform.position + fwd * (camera.nearClipPlane + 0.05f);
            float dp = Vector3.Dot(p - origin, fwd), dq = Vector3.Dot(q - origin, fwd);
            if (dp < 0f && dq < 0f) return false;
            if (dp < 0f) p = Vector3.Lerp(p, q, dp / (dp - dq));
            else if (dq < 0f) q = Vector3.Lerp(q, p, dq / (dq - dp));
            Vector3 sp = camera.WorldToScreenPoint(p), sq = camera.WorldToScreenPoint(q);
            a = new Vector2(sp.x, Screen.height - sp.y);
            b = new Vector2(sq.x, Screen.height - sq.y);
            return true;
        }

        private void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 1f || length > 20000f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.color = color;
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, length, width), _white);
            GUI.matrix = matrix;
            GUI.color = Color.white;
        }

        private void OnGUI()
        {
            if (!_visible || _report == null || !_report.Available || Game.IsUiOpen) return;
            var camera = Draw.PlayerCamera();
            var player = Game.PlayerPosition;
            if (camera == null || player == null) return;

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            EnsureStyles(scale);
            if (Event.current.type == EventType.Repaint) DrawRoutes(camera, scale);
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
