using System;
using BattleTech;
using BattleTech.UI;
using Harmony;

namespace SalvageOperations.Patches
{
    // refresh the chassis widgets after popups close
    [HarmonyPatch(typeof(SimGameInterruptManager), "PopupClosed")]
    public class SimGameInterruptManager_PopupClosed_Patch
    {
        public static void Prefix()
        {
            var mechBayPanel = (MechBayPanel) UIManager.Instance.Find(x => x.IsType(typeof(MechBayPanel)));
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (!mechBayPanel.storageWidget.enabled) return;
            try
            {
                var storageWidget = Traverse.Create(mechBayPanel).Field("storageWidget").GetValue<MechBayMechStorageWidget>();
                storageWidget.InitInventory(sim.GetAllInventoryMechDefs(), false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }
    }
}