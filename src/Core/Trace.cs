using System.Collections;
using HarmonyLib;
using SpaceCraft;
using UnityEngine;

namespace WardensLogistics.Core
{
    /// <summary>
    /// Rastreamento para testes (só escreve com DebugLog = true). Registra o fluxo de
    /// construção do jogo e a fila de respostas do servidor, para entender travamentos
    /// sem precisar reproduzir aqui. Não muda nenhum comportamento.
    /// </summary>
    internal static class Trace
    {
        private static readonly AccessTools.FieldRef<InventoriesHandler, object> QueueRef =
            AccessTools.FieldRefAccess<InventoriesHandler, object>("_callbackAddOrRemoveQueue");

        private static bool _lastBuilding;
        private static DataConfig.UiType _lastUi = DataConfig.UiType.Null;

        public static bool On => Plugin.DebugLog.Value;

        public static int PendingServerReplies
        {
            get
            {
                var h = InventoriesHandler.Instance;
                if (h == null) return -1;
                return QueueRef(h) is ICollection q ? q.Count : -1;
            }
        }

        public static string State()
        {
            var windows = Managers.GetManager<WindowsHandler>();
            string ui = windows == null ? "?" : windows.GetOpenedUi().ToString();
            return $"building={PlayerBuilder.IsDuringConstruction} ui={ui} pendingReplies={PendingServerReplies}";
        }

        /// <summary>Chamado a cada frame pelo leitor de teclado: registra mudanças de estado.</summary>
        public static void Watch()
        {
            if (!On) return;
            var windows = Managers.GetManager<WindowsHandler>();
            var ui = windows == null ? DataConfig.UiType.Null : windows.GetOpenedUi();
            if (_lastBuilding != PlayerBuilder.IsDuringConstruction || _lastUi != ui)
            {
                _lastBuilding = PlayerBuilder.IsDuringConstruction;
                _lastUi = ui;
                Plugin.Log.LogInfo($"[Trace] state changed: {State()}");
            }
        }

        [HarmonyPatch(typeof(PlayerBuilder), nameof(PlayerBuilder.SetNewGhost))]
        private static class SetNewGhostPatch
        {
            private static void Postfix(bool __result, Group groupConstructible)
            {
                if (On) Plugin.Log.LogInfo($"[Trace] SetNewGhost({groupConstructible?.GetId()}) -> {__result} | {State()}");
            }
        }

        [HarmonyPatch(typeof(PlayerBuilder), nameof(PlayerBuilder.InputOnAction))]
        private static class InputOnActionPatch
        {
            private static void Prefix(ConstructibleGhost ____ghost)
            {
                if (!On) return;
                string placement = "no ghost";
                if (____ghost != null)
                {
                    var checker = ____ghost.GetComponent<GhostPlacementChecker>();
                    placement = checker == null ? "no checker" : (checker.GetPositioningStatus() ? "valid spot" : "INVALID spot");
                }
                Plugin.Log.LogInfo($"[Trace] Build click: {placement} | {State()}");
            }
        }

        [HarmonyPatch(typeof(PlayerBuilder), "OnConstructed")]
        private static class OnConstructedPatch
        {
            private static void Prefix() { if (On) Plugin.Log.LogInfo($"[Trace] OnConstructed (server confirmed) | {State()}"); }
            private static void Postfix() { if (On) Plugin.Log.LogInfo($"[Trace] OnConstructed done | {State()}"); }
        }

        [HarmonyPatch(typeof(PlayerBuilder), nameof(PlayerBuilder.InputOnCancelAction))]
        private static class CancelPatch
        {
            private static void Postfix() { if (On) Plugin.Log.LogInfo($"[Trace] Build cancelled | {State()}"); }
        }

        [HarmonyPatch(typeof(ActionOpenable), nameof(ActionOpenable.OnAction))]
        private static class OpenPatch
        {
            private static void Prefix() { if (On) Plugin.Log.LogInfo($"[Trace] Open container pressed | {State()}"); }
        }
    }
}
