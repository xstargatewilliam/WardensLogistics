using SpaceCraft;
using UnityEngine;

namespace WardensLogistics.Core
{
    /// <summary>Acessos ao jogo usados por vários módulos, com checagem de nulo à moda Unity.</summary>
    internal static class Game
    {
        public static PlayerMainController Player
        {
            get
            {
                var players = Managers.GetManager<PlayersManager>();
                if (players == null) return null;
                var controller = players.GetActivePlayerController();
                return controller == null ? null : controller;
            }
        }

        public static Inventory Backpack
        {
            get
            {
                var player = Player;
                if (player == null) return null;
                var backpack = player.GetPlayerBackpack();
                return backpack == null ? null : backpack.GetInventory();
            }
        }

        public static Vector3? PlayerPosition
        {
            get
            {
                var player = Player;
                return player == null ? (Vector3?)null : player.transform.position;
            }
        }

        public static bool IsUiOpen
        {
            get
            {
                var windows = Managers.GetManager<WindowsHandler>();
                return windows == null || windows.GetHasUiOpen();
            }
        }

        /// <summary>Modo criativo (construção gratuita): o jogo não exige ingredientes.</summary>
        public static bool FreeCraft
        {
            get
            {
                var settings = Managers.GetManager<GameSettingsHandler>();
                if (settings == null) return false;
                var current = settings.GetCurrentGameSettings();
                return current != null && current.GetFreeCraft();
            }
        }

        public static void Notify(string text, Sprite icon = null, float seconds = 2.5f)
        {
            var displayers = Managers.GetManager<DisplayersHandler>();
            if (displayers == null) return;
            var info = displayers.GetInformationsDisplayer();
            if (info == null) return;
            info.AddInformation(seconds, text, DataConfig.UiInformationsType.OutInventory, icon);
        }
    }
}
