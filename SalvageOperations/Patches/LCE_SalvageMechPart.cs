using BattleTech;
using BattleTech.UI;
using Harmony;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(ListElementController_SalvageMechPart_NotListView), "RefreshInfoOnWidget")]
    public static class ListElementController_SalvageMechPart_NotListView_RefreshInfoOnWidget_Patch
    {
        public static void Postfix(ListElementController_SalvageMechPart_NotListView __instance, InventoryItemElement_NotListView theWidget, SimGameState ___simState)
        {
            var defaultMechPartMax = ___simState.Constants.Story.DefaultMechPartMax;
            var thisMechPieces = Main.GetMechParts(___simState, __instance.mechDef);
            var allMechPieces = Main.GetAllVariantMechParts(___simState, __instance.mechDef);

            if (!Main.Settings.ExcludedMechIds.Contains(__instance.mechDef.Description.Id))
                theWidget.mechPartsNumbersText.SetText($"{thisMechPieces} ({allMechPieces}) / {defaultMechPartMax}");
            else
                theWidget.mechPartsNumbersText.SetText($"{thisMechPieces} (R) / {defaultMechPartMax}");
                
        }
    }
}