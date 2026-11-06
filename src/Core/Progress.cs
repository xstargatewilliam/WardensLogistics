using System.Collections.Generic;
using System.IO;
using BepInEx;
using SpaceCraft;
using UnityEngine;

namespace WardensLogistics.Core
{
    /// <summary>
    /// Progressão: a tecnologia Warden é "redescoberta" ao longo do jogo.
    /// - Desde o início: guardar (K), buscar (J), assistente de terraformação, timelapse.
    /// - Primeiro baú construído: construir puxando dos baús.
    /// - Pilhas: crescem com as etapas de terraformação (10% → 40% → 100% do StackSize,
    ///   ou seja 25 → 100 → 250 no padrão).
    /// - Estação de drones liberada: Raio-x, rotas, produção, baú buffer e copiar logística.
    /// Tudo é calculado a partir de dados que o jogo sincroniza (índice de terraformação,
    /// desbloqueios), então anfitrião e convidados chegam ao mesmo resultado.
    /// A pilha nunca diminui: o maior tamanho já alcançado no save fica gravado, e um save
    /// que já tem pilhas maiores (de antes da progressão) mantém o tamanho cheio.
    /// </summary>
    internal sealed class Progress : MonoBehaviour
    {
        public enum Feature { Pull, Logistics }

        private static bool _pull, _logistics;
        private static int _stack = -1;
        private static int _floor;          // maior pilha já alcançada neste save
        private static string _save;
        private static bool _announce;      // só anuncia o que libera durante o jogo, não ao carregar

        public static bool On => Plugin.ProgressionEnabled.Value;

        public static void Create()
        {
            var go = new GameObject("WardensLogistics.Progress");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<Progress>();
        }

        public static bool Unlocked(Feature f)
        {
            if (!On) return true;
            return f == Feature.Pull ? _pull : _logistics;
        }

        /// <summary>Tamanho de pilha valendo agora.</summary>
        public static int StackSize(int configured)
        {
            if (!On || _stack <= 0) return configured;
            return Mathf.Clamp(_stack, 1, configured);
        }

        /// <summary>Texto de quando a função libera, para avisar quem aperta a tecla antes da hora.</summary>
        public static string WhenText(Feature f) =>
            Text.Get(f == Feature.Pull ? "prog.when_pull" : "prog.when_logistics");

        public static void NotifyLocked(Feature f) => Game.Notify(Text.Get("prog.locked", WhenText(f)));

        private float _next;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 2f;
            var loader = Managers.GetManager<PlanetLoader>();
            if (loader == null || !loader.GetIsLoaded()) { _save = null; _announce = false; return; }

            string save = Managers.GetManager<SavedDataHandler>()?.GetCurrentSaveFileName() ?? "save";
            if (save != _save)
            {
                _save = save;
                _announce = false;
                _floor = LoadFloor();
            }
            try { Evaluate(); }
            catch (System.Exception e) { Plugin.Log.LogWarning("[Progress] " + e.Message); }
            _announce = true;
        }

        private void Evaluate()
        {
            bool pull = ContainerIndex.All().Count > 0;
            bool logistics = DroneStationUnlocked();

            int configured = Mathf.Clamp(Plugin.StackSize.Value, 1, 100000);
            float fraction = StageFraction();
            int tier = fraction < 1f / 3f ? Mathf.CeilToInt(configured * 0.1f)
                     : fraction < 2f / 3f ? Mathf.CeilToInt(configured * 0.4f)
                     : configured;
            if (_floor <= 0 && SaveAlreadyStacksBeyond(tier)) _floor = configured; // save de antes da progressão
            tier = Mathf.Max(tier, _floor);
            if (tier > _floor) { _floor = tier; SaveFloor(); }

            if (_announce && On)
            {
                if (pull && !_pull) Game.Notify(Text.Get("prog.unlocked", Text.Get("prog.f_pull")), null, 5f);
                if (logistics && !_logistics) Game.Notify(Text.Get("prog.unlocked", Text.Get("prog.f_logistics")), null, 5f);
                if (_stack > 0 && tier > _stack) Game.Notify(Text.Get("prog.stack_up", tier), null, 5f);
            }
            _pull = pull;
            _logistics = logistics;
            _stack = tier;
        }

        /// <summary>Fração das etapas de terraformação (índice) já alcançadas.</summary>
        private static float StageFraction()
        {
            var unit = Managers.GetManager<WorldUnitsHandler>()?.GetUnit(DataConfig.WorldUnitType.Terraformation);
            var planet = Managers.GetManager<PlanetLoader>()?.GetCurrentPlanetData();
            if (unit == null || planet == null) return 0f;
            double value = unit.GetValue();
            int total = 0, reached = 0;
            foreach (var s in planet.GetPlanetTerraformationStages())
            {
                if (s == null || s.GetWorldUnitType() != DataConfig.WorldUnitType.Terraformation) continue;
                total++;
                if (value >= s.GetStageStartValue()) reached++;
            }
            return total == 0 ? 1f : (float)reached / total;
        }

        private static bool DroneStationUnlocked()
        {
            bool found = false;
            foreach (var g in GroupsHandler.GetAllGroups())
            {
                if (g == null || !g.GetId().StartsWith("DroneStation")) continue;
                found = true;
                if (Terraformacao.Terraform.IsUnlocked(g)) return true;
            }
            return !found; // se o jogo mudar o nome, não trava a função para sempre
        }

        private static bool SaveAlreadyStacksBeyond(int size)
        {
            foreach (var e in ContainerIndex.All())
            {
                var inv = e.Inventory;
                if (inv == null) continue;
                var counts = new Dictionary<string, int>();
                foreach (var wo in inv.GetInsideWorldObjects())
                {
                    if (wo == null) continue;
                    string k = Pilhas.Stacks.Key(wo);
                    counts.TryGetValue(k, out int n);
                    if (n + 1 > size) return true;
                    counts[k] = n + 1;
                }
            }
            return false;
        }

        private static string FloorFile()
        {
            string safe = _save ?? "save";
            foreach (char c in Path.GetInvalidFileNameChars()) safe = safe.Replace(c, '_');
            return Path.Combine(Paths.ConfigPath, "WardensLogistics", "progress", safe + ".txt");
        }

        private static int LoadFloor()
        {
            try
            {
                var f = FloorFile();
                return File.Exists(f) && int.TryParse(File.ReadAllText(f).Trim(), out int v) ? v : 0;
            }
            catch { return 0; }
        }

        private static void SaveFloor()
        {
            try
            {
                var f = FloorFile();
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                File.WriteAllText(f, _floor.ToString());
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("[Progress] " + e.Message); }
        }
    }
}
