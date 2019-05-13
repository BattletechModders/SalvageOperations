using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BattleTech;
using BattleTech.UI;
using Harmony;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SalvageOperations.Patches
{
    // THIS PATCH CRASHES THE GAME AFTER SALVAGE IS CHOSEN FOR SOME REASON
    [HarmonyPatch(typeof(ListElementController_SalvageMechPart_NotListView), "RefreshInfoOnWidget")]
    public static class ListElementController_SalvageMechPart_NotListView_RefreshInfoOnWidget_Patch
    {
        public static void Postfix(ListElementController_SalvageMechPart_NotListView __instance, InventoryItemElement_NotListView theWidget, SimGameState ___simState)
        {
            var defaultMechPartMax = ___simState.Constants.Story.DefaultMechPartMax;
            var thisMechPieces = Main.GetMechPieces(___simState, __instance.mechDef);
            var allMechPieces = Main.GetAllVariantMechPieces(___simState, __instance.mechDef);

            if (allMechPieces > thisMechPieces)
                theWidget.mechPartsNumbersText.SetText($"{thisMechPieces} ({allMechPieces}) / {defaultMechPartMax}");
        }

    }
}