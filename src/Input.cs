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

            bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool xrayPressed = Plugin.XRayEnabled.Value && Pressed(keyboard, Plugin.XRayKey.Value);

            // Janela detalhada do Raio-x: Ctrl+N abre e fecha (Esc também fecha, pelo jogo).
            if (Logistica.XRayWindow.IsOpen)
            {
                if (xrayPressed) Logistica.XRayWindow.Close();
                return;
            }
            if (xrayPressed && ctrl && !windows.GetHasUiOpen())
            {
                Logistica.XRayWindow.Open();
                return;
            }

            if (windows.GetHasUiOpen())
            {
                if (findPressed && windows.GetOpenedUi() != DataConfig.UiType.TextInput && windows.GetOpenedUi() != DataConfig.UiType.Chat)
                    ItemFinder.FindHovered(windows.GetOpenedUi());
                if (windows.GetOpenedUi() == DataConfig.UiType.Container)
                {
                    if (storePressed && Plugin.DepositOpenEnabled.Value) DepositIntoOpenContainer(windows);
                    if (ctrl && Pressed(keyboard, Plugin.CopyKey.Value)) ContainerTools.Copy();
                    else if (ctrl && Pressed(keyboard, Plugin.PasteKey.Value)) ContainerTools.Paste();
                    else if (!ctrl && Plugin.BufferEnabled.Value && Pressed(keyboard, Plugin.BufferKey.Value)) ContainerTools.ToggleBuffer();
                }
                return;
            }

            ItemFinder.ForgetHover();

            if (storePressed)
                QuickStore.RunNearby();

            if (findPressed)
                ItemFinder.KeyWithoutWindow();

            if (xrayPressed && !ctrl)
                Logistica.XRayOverlay.Toggle();

            if (ctrl && Plugin.TimelapseEnabled.Value && Pressed(keyboard, Plugin.TimelapseKey.Value))
                Timelapse.Timelapse.SetSpotHere();

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
