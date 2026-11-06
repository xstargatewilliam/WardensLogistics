using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using WardensLogistics.Core;
using UnityEngine.InputSystem;

namespace WardensLogistics
{
    /// <summary>
    /// Ponto de entrada. Junta, num só plugin e numa só configuração, os módulos
    /// da suíte: Núcleo (índice de baús) e Armazém (guardar rápido, puxar dos baús).
    /// Nome da suíte: Wardens Logistics.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    [BepInProcess("Planet Crafter.exe")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "wardenslogistics.theplanetcrafter";
        public const string Name = "Wardens Logistics";
        public const string Version = "0.7.0";

        internal static ManualLogSource Log;

        // Geral
        internal static ConfigEntry<string> Language;
        internal static ConfigEntry<bool> DebugLog;
        internal static ConfigEntry<bool> ShowKeyHints;

        // Núcleo
        internal static ConfigEntry<float> PullRange;
        internal static ConfigEntry<float> StoreRangePercent;
        internal static ConfigEntry<float> RefreshSeconds;
        internal static ConfigEntry<bool> IncludeWorldContainers;

        // Armazém: guardar rápido
        internal static ConfigEntry<bool> QuickStoreEnabled;
        internal static ConfigEntry<Key> QuickStoreKey;
        internal static ConfigEntry<bool> QuickStoreMatchExisting;
        internal static ConfigEntry<bool> QuickStoreMatchName;
        internal static ConfigEntry<string> QuickStoreExcluded;
        internal static ConfigEntry<bool> DepositOpenEnabled;

        // Armazém: puxar dos baús ao construir/craftar
        internal static ConfigEntry<bool> PullEnabled;
        internal static ConfigEntry<Key> PullToggleKey;

        // Busca: onde está meu item?
        internal static ConfigEntry<bool> FinderEnabled;
        internal static ConfigEntry<Key> FinderKey;
        internal static ConfigEntry<float> FinderSeconds;
        internal static ConfigEntry<int> FinderMaxMarkers;

        // Pilhas
        internal static ConfigEntry<bool> StackEnabled;
        internal static ConfigEntry<int> StackSize;
        internal static ConfigEntry<int> StackFontSize;

        // Raio-x da logística
        internal static ConfigEntry<bool> XRayEnabled;
        internal static ConfigEntry<Key> XRayKey;

        private void Awake()
        {
            Log = Logger;

            Language = Config.Bind("1. General", "Language", "auto",
                "Idioma dos textos do mod. 'auto' segue o idioma do jogo. Outros: portuguese, english, spanish, french, german... " +
                "Para traduzir, veja a pasta lang ao lado da DLL.\n" +
                "Mod text language. 'auto' follows the game language. Others: portuguese, english, spanish, french, german... " +
                "To translate, see the lang folder next to the DLL.");
            DebugLog = Config.Bind("1. General", "DebugLog", false,
                "Escreve mensagens detalhadas no BepInEx/LogOutput.log.\nWrite detailed messages to BepInEx/LogOutput.log.");

            ShowKeyHints = Config.Bind("1. General", "ShowKeyHints", true,
                "Mostrar os atalhos do mod no canto inferior direito, acima da versão do jogo.\n" +
                "Show the mod's shortcuts in the bottom-right corner, above the game version.");

            PullRange = Config.Bind("2. Core", "PullRange", 200f,
                "Raio em metros dos baús usados para construir e craftar.\n" +
                "Radius in meters of the containers used to build and craft.");
            StoreRangePercent = Config.Bind("2. Core", "StoreRangePercent", 50f,
                "Raio para guardar (tecla K), em % do PullRange. 50 = metade.\n" +
                "Radius for storing (K key), as a % of PullRange. 50 = half.");
            RefreshSeconds = Config.Bind("2. Core", "IndexRefreshSeconds", 3f,
                "De quantos em quantos segundos, no máximo, a lista de baús é atualizada.\n" +
                "How often (seconds) the shared container index rescans the map, at most.");
            IncludeWorldContainers = Config.Bind("2. Core", "IncludeWorldContainers", false,
                "Usar também as caixas que vêm com o mapa. Desligado: só baús que você construiu.\n" +
                "Also use containers that came with the map (loot crates). Off: only containers you built.");

            QuickStoreEnabled = Config.Bind("3. Quick Store", "Enabled", true,
                "Guardar itens da mochila nos baús próximos com uma tecla.\nStore backpack items into nearby containers with one key.");
            QuickStoreKey = Config.Bind("3. Quick Store", "Key", Key.K,
                "Tecla para guardar. (Não é L: três mods populares já usam L.)\nKey that stores items. (Not L: three popular mods already use L.)");
            QuickStoreMatchExisting = Config.Bind("3. Quick Store", "MatchExistingItems", true,
                "Guardar o item num baú que já tem o mesmo item.\nStore an item in a container that already holds the same item.");
            QuickStoreMatchName = Config.Bind("3. Quick Store", "MatchContainerName", true,
                "Guardar o item num baú cujo nome contém o nome do item (ex.: baú chamado 'Ferro').\n" +
                "Store an item in a container whose name contains the item name (e.g. a container named 'Iron').");
            QuickStoreExcluded = Config.Bind("3. Quick Store", "ExcludedItemIds", "",
                "IDs de itens que nunca são guardados, separados por vírgula (ex.: OxygenCapsule1,WaterBottle1).\n" +
                "Comma-separated item IDs that are never stored (e.g. OxygenCapsule1,WaterBottle1).");

            DepositOpenEnabled = Config.Bind("3. Quick Store", "DepositIntoOpenContainer", true,
                "Com a janela de um baú aberta, a mesma tecla deposita nele só os itens que combinam (mesmas regras acima).\n" +
                "With a container window open, the same key deposits only matching items into it (same rules as above).");

            PullEnabled = Config.Bind("4. Pull From Containers", "Enabled", true,
                "Construir e craftar usando ingredientes dos baús próximos. Não age no modo criativo.\n" +
                "Build and craft using ingredients from nearby containers. Inactive in creative (free craft) mode.");
            PullToggleKey = Config.Bind("4. Pull From Containers", "ToggleKey", Key.None,
                "Tecla opcional para ligar/desligar no jogo. None = sem tecla.\nOptional key to turn pulling on/off in game. None = no key.");

            FinderEnabled = Config.Bind("5. Item Finder", "Enabled", true,
                "Mostrar onde está um item: passe o mouse sobre ele (inventário, baú ou menu de construção) e aperte a tecla.\n" +
                "Show where an item is: hover it (inventory, container or build menu) and press the key.");
            FinderKey = Config.Bind("5. Item Finder", "Key", Key.J,
                "Tecla da busca. Com janela aberta: procura o item sob o mouse. Sem janela: abre a caixa de busca (ou apaga os marcadores).\n" +
                "Search key. With a window open: finds the hovered item. With no window: opens the search box (or clears the markers).");
            FinderSeconds = Config.Bind("5. Item Finder", "MarkerSeconds", 20f,
                "Por quantos segundos os marcadores ficam na tela.\nHow many seconds the markers stay on screen.");
            FinderMaxMarkers = Config.Bind("5. Item Finder", "MaxMarkers", 15,
                "Máximo de baús marcados (os mais próximos primeiro).\nMaximum containers marked (nearest first).");

            StackEnabled = Config.Bind("7. Stacking", "Enabled", true,
                "Empilhar itens iguais num mesmo espaço do inventário. Fase 1: mochila e baús.\n" +
                "Stack identical items in one inventory slot. Phase 1: backpack and containers.");
            StackSize = Config.Bind("7. Stacking", "StackSize", 250,
                "Quantos itens iguais cabem em um espaço.\nHow many identical items fit in one slot.");
            StackFontSize = Config.Bind("7. Stacking", "FontSize", 22,
                "Tamanho do número da pilha no inventário.\nSize of the stack number in the inventory.");

            XRayEnabled = Config.Bind("6. Logistics X-Ray", "Enabled", true,
                "Painel com o estado da logística de drones e os problemas encontrados (solo ou anfitrião).\n" +
                "Panel with the drone logistics status and the problems found (single player or host).");
            XRayKey = Config.Bind("6. Logistics X-Ray", "Key", Key.N,
                "Tecla que abre e fecha o painel do Raio-x. Ctrl + esta tecla abre a janela detalhada.\n" +
                "Key that opens and closes the X-Ray panel. Ctrl + this key opens the detailed window.");

            Text.Init(Path.GetDirectoryName(Info.Location));
            Logistica.XRayHud.SetIconFolder(Path.GetDirectoryName(Info.Location));

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
            Busca.FinderOverlay.Create();
            Logistica.XRayOverlay.Create();
            KeyHints.Create();
            Log.LogInfo($"{Name} {Version} loaded.");
        }

        /// <summary>Raio dos baús para construir e craftar.</summary>
        internal static float PullRadius => UnityEngine.Mathf.Max(1f, PullRange.Value);

        /// <summary>Raio dos baús para guardar: uma porcentagem do raio de construção.</summary>
        internal static float StoreRadius => PullRadius * UnityEngine.Mathf.Clamp(StoreRangePercent.Value, 1f, 100f) / 100f;

        internal static void Debug(string message)
        {
            if (DebugLog.Value)
                Log.LogInfo(message);
        }
    }
}
