using System.Collections.Generic;
using UnityEngine;
using WardensLogistics.Core;

namespace WardensLogistics.Busca
{
    /// <summary>
    /// Desenha os marcadores da busca por cima do jogo (IMGUI): sobre cada baú
    /// encontrado, os ícones dos itens procurados com as quantidades, a distância e o
    /// nome do baú. Como é desenhado na tela, aparece mesmo com paredes no caminho.
    /// Fica escondido enquanto há janela aberta e some sozinho depois de alguns segundos.
    /// </summary>
    internal sealed class FinderOverlay : MonoBehaviour
    {
        internal struct Entry
        {
            public Sprite Icon;
            public int Count;
        }

        internal struct Marker
        {
            public Vector3 Position;
            public string Name;
            public List<Entry> Items;
        }

        private static readonly List<Marker> Markers = new List<Marker>();
        private static float _until;

        private Texture2D _background;
        private GUIStyle _count;
        private GUIStyle _label;
        private GUIStyle _name;

        public static bool IsShowing => Markers.Count > 0 && Time.unscaledTime < _until;

        public static void Show(List<Marker> markers, float seconds)
        {
            Markers.Clear();
            Markers.AddRange(markers);
            _until = Time.unscaledTime + seconds;
        }

        public static void Clear()
        {
            Markers.Clear();
            _until = 0f;
        }

        public static void Create()
        {
            var go = new GameObject("WardensLogistics.Overlay");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<FinderOverlay>();
        }

        private void OnGUI()
        {
            SearchBox.Tick();
            if (SearchBox.IsActive)
            {
                DrawSuggestions();
                return;
            }
            if (!IsShowing || Game.IsUiOpen) return;
            var camera = PlayerCamera();
            var player = Game.PlayerPosition;
            if (camera == null || player == null) return;

            float scale = Mathf.Max(1f, Screen.height / 1080f);
            EnsureStyles(scale);
            float icon = 36f * scale;
            float pad = 6f * scale;

            for (int i = Markers.Count - 1; i >= 0; i--) // o mais perto desenhado por último, por cima
            {
                var m = Markers[i];
                Vector3 sp = camera.WorldToScreenPoint(m.Position);
                if (sp.z <= 0f) continue; // atrás da câmera

                float distance = Vector3.Distance(player.Value, m.Position);
                bool hasName = !string.IsNullOrEmpty(m.Name);
                float width = Mathf.Max(110f * scale, m.Items.Count * (icon + pad) + pad);
                float height = pad + icon + 22f * scale + (hasName ? 18f * scale : 0f) + pad;
                var box = new Rect(sp.x - width / 2f, Screen.height - sp.y - height, width, height);

                GUI.color = i == 0 ? new Color(0.2f, 0.5f, 1f, 0.85f) : new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(box, _background);
                GUI.color = Color.white;

                float x = box.x + (width - (m.Items.Count * (icon + pad) - pad)) / 2f;
                foreach (var item in m.Items)
                {
                    var r = new Rect(x, box.y + pad, icon, icon);
                    DrawSprite(r, item.Icon);
                    GUI.Label(new Rect(r.x, r.yMax - 16f * scale, icon + 2f * scale, 16f * scale), "x" + item.Count, _count);
                    x += icon + pad;
                }

                float y = box.y + pad + icon;
                GUI.Label(new Rect(box.x, y, width, 22f * scale), $"{distance:0} m", _label);
                if (hasName)
                    GUI.Label(new Rect(box.x, y + 18f * scale, width, 18f * scale), m.Name, _name);
            }
        }

        private GUIStyle _row;
        private GUIStyle _rowCount;

        /// <summary>Lista de sugestões embaixo da janela de texto, enquanto a busca está aberta.</summary>
        private void DrawSuggestions()
        {
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            EnsureStyles(scale);
            if (_row == null)
            {
                _row = new GUIStyle(_label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal, fontSize = Mathf.RoundToInt(17 * scale) };
                _row.normal.textColor = Color.white;
                _row.hover.textColor = new Color(0.55f, 0.8f, 1f);
                _rowCount = new GUIStyle(_row) { alignment = TextAnchor.MiddleRight, fontSize = Mathf.RoundToInt(15 * scale) };
                _rowCount.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            }

            var list = SearchBox.Suggestions;
            float width = 560f * scale;
            float rowH = 34f * scale;
            float pad = 8f * scale;
            float titleH = 26f * scale;
            int rows = Mathf.Max(1, list.Count);
            float height = pad + titleH + rows * rowH + pad;
            // Logo abaixo da caixa de texto do jogo; se não der para ler a posição, abaixo do centro.
            var input = SearchBox.InputScreenRect();
            float top = input.HasValue ? input.Value.yMax + 12f * scale : Screen.height * 0.6f;
            float centerX = input.HasValue ? input.Value.center.x : Screen.width / 2f;
            if (top + height > Screen.height - 8f * scale) top = Screen.height - 8f * scale - height;
            float left = Mathf.Clamp(centerX - width / 2f, 8f * scale, Screen.width - width - 8f * scale);
            var box = new Rect(left, top, width, height);

            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(box, _background);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x + pad, box.y + pad, width - 2 * pad, titleH), Text.Get("search.title"), _name);

            float y = box.y + pad + titleH;
            if (list.Count == 0)
            {
                GUI.Label(new Rect(box.x + pad, y, width - 2 * pad, rowH), Text.Get("search.empty"), _row);
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                var r = new Rect(box.x + pad, y, width - 2 * pad, rowH);
                if (i == 0)
                {
                    GUI.color = new Color(0.2f, 0.5f, 1f, 0.45f);
                    GUI.DrawTexture(r, _background);
                    GUI.color = Color.white;
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none)) SearchBox.Choose(s.Group);
                float icon = rowH - 6f * scale;
                DrawSprite(new Rect(r.x + 4f * scale, r.y + 3f * scale, icon, icon), s.Group.GetImage());
                GUI.Label(new Rect(r.x + icon + 14f * scale, r.y, r.width * 0.6f, rowH), s.Name, _row);
                string count = s.InContainers > 0 ? Text.Get("search.in_containers", s.InContainers) : Text.Get("search.none_stored");
                GUI.Label(new Rect(r.x, r.y, r.width - 8f * scale, rowH), count, _rowCount);
                y += rowH;
            }
        }

        private void EnsureStyles(float scale)
        {
            if (_background == null)
            {
                _background = new Texture2D(1, 1);
                _background.SetPixel(0, 0, Color.white);
                _background.Apply();
            }
            if (_label != null) return;

            _label = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.RoundToInt(15 * scale),
            };
            _label.normal.textColor = Color.white;
            _name = new GUIStyle(_label) { fontStyle = FontStyle.Normal, fontSize = Mathf.RoundToInt(13 * scale) };
            _name.normal.textColor = new Color(0.85f, 0.9f, 1f);
            _count = new GUIStyle(_label) { alignment = TextAnchor.LowerRight, fontSize = Mathf.RoundToInt(13 * scale) };
        }

        private static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            var tex = sprite.texture;
            var tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, uv);
        }

        private static Camera PlayerCamera()
        {
            var player = Game.Player;
            if (player != null)
            {
                var cam = player.GetComponentInChildren<Camera>();
                if (cam != null && cam.isActiveAndEnabled) return cam;
            }
            return Camera.main;
        }
    }
}
