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
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Main.Settings.DependsOnArgoUpgrade && !sim.PurchasedArgoUpgrades.Contains(Main.Settings.ArgoUpgrade))
                return;

            var mechBayPanel = (MechBayPanel) UIManager.Instance.Find(x => x.IsType(typeof(MechBayPanel)));
            if (mechBayPanel == null) return;
            var storageWidget = Traverse.Create(mechBayPanel).Field("storageWidget").GetValue<MechBayMechStorageWidget>();
            if (!storageWidget.enabled) return;
            try
            {
                storageWidget.InitInventory(sim.GetAllInventoryMechDefs(), false);
            }
            catch
            {
            }
        }
    }
}