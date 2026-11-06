using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using HarmonyLib;
using SpaceCraft;
using WardensLogistics.Core;

namespace WardensLogistics.Pilhas
{
    /// <summary>
    /// Dois mods de empilhamento ao mesmo tempo disputam a mesma regra de capacidade e
    /// costumam corromper inventários. O empilhamento do Wardens Logistics continua ativo
    /// e pede para o jogador desinstalar o outro: aviso no log ao abrir o jogo e na tela
    /// ao carregar o mundo.
    /// </summary>
    internal static class StackConflict
    {
        // Identificadores conhecidos de outros mods de empilhamento.
        private static readonly string[] KnownIds =
        {
            "akarnokd.theplanetcraftermods.cheatinventorystacking",
        };

        private static readonly List<string> Found = new List<string>();
        private static bool _notifiedInGame;

        public static bool HasConflict => Found.Count > 0;

        public static void Check()
        {
            Found.Clear();
            foreach (var pair in Chainloader.PluginInfos)
            {
                var meta = pair.Value.Metadata;
                if (meta.GUID == Plugin.Guid) continue;
                bool known = Array.IndexOf(KnownIds, meta.GUID) >= 0;
                bool looksLikeStacking = meta.Name.IndexOf("stack", StringComparison.OrdinalIgnoreCase) >= 0
                                         || meta.GUID.IndexOf("stack", StringComparison.OrdinalIgnoreCase) >= 0;
                if (known || looksLikeStacking) Found.Add($"{meta.Name} ({meta.GUID})");
            }
            if (HasConflict && Stacks.Enabled)
                Plugin.Log.LogWarning("Another inventory stacking mod is installed: " + string.Join(", ", Found) +
                                      ". Uninstall it: two stacking mods at once can corrupt inventories. " +
                                      "Wardens Logistics stacking stays active.");
        }

        /// <summary>Avisa na tela uma vez, quando o mundo termina de carregar.</summary>
        [HarmonyPatch(typeof(PlanetLoader), "HandleDataAfterLoad")]
        private static class NotifyAfterLoad
        {
            private static void Postfix()
            {
                if (_notifiedInGame) return;
                Check(); // aqui todos os mods já carregaram
                if (!HasConflict || !Stacks.Enabled) return;
                _notifiedInGame = true;
                Game.Notify(Text.Get("stack.conflict", string.Join(", ", Found)), null, 12f);
            }
        }
    }
}
