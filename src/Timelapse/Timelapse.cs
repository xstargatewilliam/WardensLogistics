using System;
using System.Globalization;
using System.IO;
using BepInEx;
using SpaceCraft;
using UnityEngine;
using WardensLogistics.Core;
using Text = WardensLogistics.Core.Text;

namespace WardensLogistics.Timelapse
{
    /// <summary>
    /// Timelapse da terraformação. Ctrl+T grava o ponto de vista atual (posição, direção e
    /// campo de visão da câmera). A cada intervalo de jogo, uma câmera invisível fotografa
    /// esse ponto e salva um PNG em "Timelapse/&lt;save&gt;/&lt;planeta&gt;/" na pasta do jogo,
    /// numerado e com o índice de terraformação no nome. Juntando as fotos (qualquer editor
    /// de vídeo ou o ffmpeg), o planeta ganha vida em segundos.
    /// A foto não mostra a interface. Partes do mapa muito longe do jogador podem estar
    /// descarregadas pelo jogo, então escolha um ponto perto da base.
    /// </summary>
    internal sealed class Timelapse : MonoBehaviour
    {
        private struct Spot { public Vector3 Position; public Quaternion Rotation; public float Fov; }

        private static Timelapse _instance;
        private Spot? _spot;
        private string _folder;
        private string _loadedFor;
        private float _nextShot = float.PositiveInfinity;
        private int _index;
        private Camera _camera;
        private RenderTexture _rt;

        public static void Create()
        {
            var go = new GameObject("WardensLogistics.Timelapse");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _instance = go.AddComponent<Timelapse>();
        }

        public static bool HasSpot => _instance != null && _instance._spot.HasValue;

        /// <summary>Ctrl+T: grava o ponto de vista atual e tira a primeira foto.</summary>
        public static void SetSpotHere()
        {
            if (_instance == null || !Plugin.TimelapseEnabled.Value) return;
            var cam = Draw.PlayerCamera();
            if (cam == null || !_instance.EnsureFolder()) return;
            _instance._spot = new Spot { Position = cam.transform.position, Rotation = cam.transform.rotation, Fov = cam.fieldOfView };
            _instance.SaveSpot();
            _instance._nextShot = Time.time + 1f; // primeira foto logo em seguida
            Game.Notify(Text.Get("tl.set", Plugin.TimelapseMinutes.Value));
        }

        private void Update()
        {
            if (!Plugin.TimelapseEnabled.Value) return;
            var loader = Managers.GetManager<PlanetLoader>();
            if (loader == null || !loader.GetIsLoaded()) { _loadedFor = null; return; }

            string key = SaveKey();
            if (key != _loadedFor)
            {
                _loadedFor = key;
                _spot = null;
                _folder = null;
                _index = 0;
                if (EnsureFolder()) LoadSpot();
                _nextShot = Time.time + Interval;
            }

            if (!_spot.HasValue || Time.time < _nextShot) return;
            _nextShot = Time.time + Interval;
            try { Shoot(); }
            catch (Exception e) { Plugin.Log.LogWarning("[Timelapse] " + e.Message); }
        }

        private static float Interval => Mathf.Max(0.5f, Plugin.TimelapseMinutes.Value) * 60f;

        private void Shoot()
        {
            var source = Draw.PlayerCamera();
            if (source == null) return;
            int w = Mathf.Clamp(Plugin.TimelapseWidth.Value, 320, 7680);
            int h = Mathf.Clamp(Plugin.TimelapseHeight.Value, 180, 4320);

            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) _rt.Release();
                _rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            }
            if (_camera == null)
            {
                var go = new GameObject("WardensLogistics.TimelapseCamera");
                DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                _camera = go.AddComponent<Camera>();
            }
            _camera.CopyFrom(source);
            _camera.enabled = false;
            var spot = _spot.Value;
            _camera.transform.SetPositionAndRotation(spot.Position, spot.Rotation);
            _camera.fieldOfView = spot.Fov;
            _camera.aspect = (float)w / h;
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) _camera.cullingMask &= ~(1 << ui);
            _camera.targetTexture = _rt;
            _camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            _camera.targetTexture = null;

            byte[] png = tex.EncodeToPNG();
            Destroy(tex);
            string ti = TerraIndex();
            _index++;
            string file = Path.Combine(_folder, $"{_index:00000}_{ti}.png");
            File.WriteAllBytes(file, png);
            if (_index == 1) Game.Notify(Text.Get("tl.first", _folder));
            if (Plugin.DebugLog.Value) Plugin.Log.LogInfo("[Timelapse] " + file);
        }

        private static string TerraIndex()
        {
            var unit = Managers.GetManager<WorldUnitsHandler>()?.GetUnit(DataConfig.WorldUnitType.Terraformation);
            if (unit == null) return "Ti";
            string s = unit.GetValueString();
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }

        private static string SaveKey()
        {
            string save = Managers.GetManager<SavedDataHandler>()?.GetCurrentSaveFileName() ?? "save";
            string planet = Managers.GetManager<PlanetLoader>()?.GetCurrentPlanetData()?.id ?? "planet";
            return Safe(save) + "|" + Safe(planet);
        }

        private static string Safe(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private bool EnsureFolder()
        {
            if (_folder != null) return true;
            try
            {
                var parts = SaveKey().Split('|');
                _folder = Path.Combine(Paths.GameRootPath, "Timelapse", parts[0], parts[1]);
                Directory.CreateDirectory(_folder);
                _index = Directory.GetFiles(_folder, "*.png").Length;
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[Timelapse] " + e.Message);
                _folder = null;
                return false;
            }
        }

        private string SpotFile => Path.Combine(_folder, "camera.txt");

        private void SaveSpot()
        {
            var s = _spot.Value;
            var ci = CultureInfo.InvariantCulture;
            File.WriteAllText(SpotFile, string.Join(";",
                s.Position.x.ToString(ci), s.Position.y.ToString(ci), s.Position.z.ToString(ci),
                s.Rotation.x.ToString(ci), s.Rotation.y.ToString(ci), s.Rotation.z.ToString(ci), s.Rotation.w.ToString(ci),
                s.Fov.ToString(ci)));
        }

        private void LoadSpot()
        {
            try
            {
                if (!File.Exists(SpotFile)) return;
                var p = File.ReadAllText(SpotFile).Trim().Split(';');
                if (p.Length < 8) return;
                var ci = CultureInfo.InvariantCulture;
                float F(int i) => float.Parse(p[i], ci);
                _spot = new Spot
                {
                    Position = new Vector3(F(0), F(1), F(2)),
                    Rotation = new Quaternion(F(3), F(4), F(5), F(6)),
                    Fov = F(7),
                };
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Timelapse] camera.txt: " + e.Message); }
        }
    }
}
