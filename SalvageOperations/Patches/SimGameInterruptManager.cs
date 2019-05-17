using BattleTech.UI;
using Harmony;

namespace SalvageOperations.Patches
{
    // refresh the chassis widgets after popups close
    [HarmonyPatch(typeof(SimGameInterruptManager), "PopupClosed")]
    public class SimGameInterruptManager_PopupClosed_Patch
    {
        private static MechBayPanel MechBayPanel = (MechBayPanel) UIManager.Instance.Find(x => x.IsType(typeof(MechBayPanel)));

        public static void Prefix()
        {
            if (MechBayPanel.Visible)// && !Main.ShowBuildPopup)
            {
                var storageWidget = Traverse.Create(MechBayPanel).Field("storageWidget").GetValue<MechBayMechStorageWidget>();
                storageWidget.InitInventory(MechBayPanel.Sim.GetAllInventoryMechDefs(), false);
            }
        }
    }
}