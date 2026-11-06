using UnityEngine;

namespace WardensLogistics.Core
{
    /// <summary>Utilitários de desenho na tela (IMGUI) usados pelos painéis e marcadores.</summary>
    internal static class Draw
    {
        public static void Sprite(Rect rect, UnityEngine.Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            var tex = sprite.texture;
            var tr = sprite.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, uv);
        }

        /// <summary>Câmera do jogador; se não achar, a câmera principal.</summary>
        public static Camera PlayerCamera()
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
