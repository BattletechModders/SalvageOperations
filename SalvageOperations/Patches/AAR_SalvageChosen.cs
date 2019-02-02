using BattleTech;
using BattleTech.UI;
using Harmony;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(AAR_SalvageChosen), "CalculateCurrentSalvageValue")]
    public static class AAR_SalvageChosen_CalculateCurrentSalvageValue_Patch
    {
        public static void Postfix(SimGameState ___simState, ref int __result)
        {
            if (___simState != null)
                __result = (int) (__result * ___simState.Constants.Finances.ShopSellModifier);
        }
    }
}