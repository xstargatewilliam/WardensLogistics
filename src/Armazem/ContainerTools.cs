using System.Collections.Generic;
using HarmonyLib;
using SpaceCraft;
using WardensLogistics.Core;
using Text = WardensLogistics.Core.Text;

namespace WardensLogistics.Armazem
{
    /// <summary>
    /// Ferramentas da janela de baú:
    /// - Etiquetar automaticamente: ao fechar um baú sem nome que só tem um tipo de item,
    ///   ele recebe o nome desse item (pelo componente de texto do jogo, que sincroniza).
    /// - Copiar e colar a configuração de logística (pedido, oferta, prioridade).
    /// - Ligar e desligar o modo buffer (marca "[Buffer]" no nome; ver Buffer.cs).
    /// </summary>
    internal static class ContainerTools
    {
        public const string BufferTag = "[Buffer]";

        private static ActionOpenable _openedBy;
        private static Inventory _openedInventory;

        private static int[] _copyDemand;
        private static int[] _copySupply;
        private static int _copyPriority;
        private static string _copySource;

        public static bool HasCopy => _copyDemand != null;

        [HarmonyPatch(typeof(ActionOpenable), "OpenInventories")]
        private static class OpenPatch
        {
            private static void Postfix(ActionOpenable __instance, Inventory objectInventory)
            {
                _openedBy = __instance;
                _openedInventory = objectInventory;
            }
        }

        [HarmonyPatch(typeof(UiWindowContainer), nameof(UiWindowContainer.OnClose))]
        private static class ClosePatch
        {
            private static void Prefix()
            {
                try { AutoName(); }
                catch (System.Exception e) { Plugin.Log.LogWarning("AutoName: " + e.Message); }
                _openedBy = null;
                _openedInventory = null;
            }
        }

        private static WorldObjectText OpenText()
        {
            if (_openedBy == null) return null;
            var text = _openedBy.GetComponent<WorldObjectText>();
            if (text == null) text = _openedBy.GetComponentInParent<WorldObjectText>();
            return text;
        }

        private static void AutoName()
        {
            if (!Plugin.AutoNameEnabled.Value || _openedInventory == null) return;
            if (ContainerIndex.Find(_openedInventory) == null) return; // só baús (não máquinas)
            var text = OpenText();
            if (text == null || text.GetTextIsSet()) return;

            Group only = null;
            foreach (var wo in _openedInventory.GetInsideWorldObjects())
            {
                var g = wo?.GetGroup();
                if (g == null) continue;
                if (only == null) only = g;
                else if (only != g) return; // mais de um tipo: deixa sem nome
            }
            if (only == null) return;
            text.SetText(Readable.GetGroupName(only));
            ContainerIndex.Invalidate();
        }

        /// <summary>Inventário do baú aberto (o lado que não é a mochila).</summary>
        private static Inventory OpenContainer(out UiWindowContainer window)
        {
            window = Managers.GetManager<WindowsHandler>()?.GetWindowViaUiId(DataConfig.UiType.Container) as UiWindowContainer;
            var backpack = Game.Backpack;
            if (window == null || backpack == null) return null;
            return window.GetOtherInventory(backpack);
        }

        public static void Copy()
        {
            var inv = OpenContainer(out _);
            var entity = inv?.GetLogisticEntity();
            if (entity == null) return;
            (_copyDemand, _copySupply, _copyPriority) = entity.Serialize();
            _copySource = OpenText()?.GetText();
            Game.Notify(Text.Get("tools.copied", _copyDemand.Length, _copySupply.Length));
        }

        public static void Paste()
        {
            if (!HasCopy) { Game.Notify(Text.Get("tools.nothing_copied")); return; }
            var inv = OpenContainer(out var window);
            var entity = inv?.GetLogisticEntity();
            if (entity == null) return;
            entity.Deserialize(_copyDemand, _copySupply, _copyPriority);
            InventoriesHandler.Instance.UpdateLogisticEntity(inv);
            // Atualiza a tela de logística aberta, se houver.
            foreach (var selector in window.GetComponentsInChildren<LogisticSelector>(true))
                selector.InitLogisticSelector(inv);
            Game.Notify(Text.Get("tools.pasted", _copyDemand.Length, _copySupply.Length));
        }

        public static void ToggleBuffer()
        {
            var inv = OpenContainer(out var window);
            if (inv == null || ContainerIndex.Find(inv) == null) return;
            var text = OpenText();
            if (text == null) return;
            string name = text.GetText() ?? "";
            bool on = Buffer.HasTag(name);
            name = on ? Buffer.StripTag(name) : (BufferTag + " " + name).Trim();
            text.SetText(name);
            window.SetContainerName(name);
            ContainerIndex.Invalidate();
            Game.Notify(Text.Get(on ? "tools.buffer_off" : "tools.buffer_on"));
        }
    }
}
