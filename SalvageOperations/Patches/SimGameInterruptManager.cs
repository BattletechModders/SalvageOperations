using BattleTech.UI;
using Harmony;

namespace SalvageOperations.Patches
{
    // prevent the popup until we've set it back to True with mech shift-click on Storage
    //[HarmonyPatch(typeof(SimGameInterruptManager), "QueueEventPopup")]
    //public class SimGameInterruptManager_QueueEventPopup_Patch
    //{
    //    public static bool Prefix(SimGameEventDef evt)
    //    {
    //        if (evt.Description.Name == "Salvage Operations")
    //        {
    //            return Main.ShowBuildPopup;
    //        }
    //
    //        return true;
    //    }
    //}

    // refresh the chassis widgets after popups close
    [HarmonyPatch(typeof(SimGameInterruptManager), "PopupClosed")]
    public class SimGameInterruptManager_PopupClosed_Patch
    {
        private static MechBayPanel MechBayPanel = (MechBayPanel) UIManager.Instance.Find(x => x.IsType(typeof(MechBayPanel)));

        public static void Prefix()
        {
            if (MechBayPanel.Visible)
            {
                var storageWidget = Traverse.Create(MechBayPanel).Field("storageWidget").GetValue<MechBayMechStorageWidget>();
                storageWidget.InitInventory(MechBayPanel.Sim.GetAllInventoryMechDefs(), false);
            }
        }
    }
}