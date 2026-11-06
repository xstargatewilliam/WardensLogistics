using HarmonyLib;
using WardensLogistics.Armazem;
using WardensLogistics.Busca;
using WardensLogistics.Core;
using SpaceCraft;
using UnityEngine.InputSystem;

namespace WardensLogistics
{
    /// <summary>
    /// Atalhos da suíte, todos num lugar só. Lidos no Update do próprio leitor de
    /// teclado do jogo (mesmo ponto usado pelos mods do aedenthorn).
    /// - Sem janela aberta: guardar nos baús próximos; ligar/desligar "puxar dos baús".
    /// - Com a janela de baú aberta: a mesma tecla deposita no baú aberto.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInputDispatcher), "Update")]
    internal static class InputPatch
    {
        private static void Postfix()
        {
            Trace.Watch();
            var keyboard = Keyboard.current;
            var windows = Managers.GetManager<WindowsHandler>();
            if (keyboard == null || windows == null) return;

            bool storePressed = Plugin.QuickStoreEnabled.Value && Pressed(keyboard, Plugin.QuickStoreKey.Value);
            if (storePressed && Trace.On)
                Plugin.Log.LogInfo($"[Trace] Quick store key | {Trace.State()}");

            bool findPressed = Plugin.FinderEnabled.Value && Pressed(keyboard, Plugin.FinderKey.Value);

            if (windows.GetHasUiOpen())
            {
                if (findPressed && windows.GetOpenedUi() != DataConfig.UiType.TextInput && windows.GetOpenedUi() != DataConfig.UiType.Chat)
                    ItemFinder.FindHovered(windows.GetOpenedUi());
                if (storePressed && Plugin.DepositOpenEnabled.Value && windows.GetOpenedUi() == DataConfig.UiType.Container)
                    DepositIntoOpenContainer(windows);
                return;
            }

            ItemFinder.ForgetHover();

            if (storePressed)
                QuickStore.RunNearby();

            if (findPressed)
                ItemFinder.KeyWithoutWindow();

            if (Pressed(keyboard, Plugin.PullToggleKey.Value))
                PullFromContainers.Toggle();
        }

        private static void DepositIntoOpenContainer(WindowsHandler windows)
        {
            var window = windows.GetWindowViaUiId(DataConfig.UiType.Container) as UiWindowContainer;
            var backpack = Game.Backpack;
            if (window == null || backpack == null) return;
            // Na janela de baú, um lado é a mochila e o outro é o baú.
            QuickStore.RunIntoOpenContainer(window.GetOtherInventory(backpack));
        }

        private static bool Pressed(Keyboard keyboard, Key key)
        {
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }
    }
}
