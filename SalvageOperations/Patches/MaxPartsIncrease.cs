using BattleTech;
using BattleTech.UI;
using Harmony;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisUnitElement), "SetData")]
    public static class MechBayChassisUnitElement_SetData_Patch
    {
        public static void Prefix(ChassisDef chassisDef, ref int partsCount, ref int partsMax, int chassisQuantity)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Main.Settings.DependsOnArgoUpgrade && !sim.PurchasedArgoUpgrades.Contains(Main.Settings.ArgoUpgrade))
                return;

            if (chassisDef != null)
            {
                partsMax = 99;
                if (partsCount == 0)
                {
                    chassisDef.MechPartCount = 99;
                    partsCount = 99;
                }
            }
        }
    }

    //TODO: The use of __state here is an ineligant kludge and a better way should be found to address this problem. 
    [HarmonyPatch(typeof(MechBayChassisInfoWidget), "SetDescriptions")]
    public static class MechBayChassisInfoWidget_SetDescriptions_Patch
    {
        public static void Prefix(MechBayChassisInfoWidget __instance, ChassisDef ___selectedChassis, ref int __state)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Main.Settings.DependsOnArgoUpgrade && !sim.PurchasedArgoUpgrades.Contains(Main.Settings.ArgoUpgrade))
                return;

            if (___selectedChassis != null)
            {
                __state = ___selectedChassis.MechPartMax;
                ___selectedChassis.MechPartMax = 99;
            }
        }

        public static void Postfix(MechBayChassisInfoWidget __instance, ChassisDef ___selectedChassis, ref int __state)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Main.Settings.DependsOnArgoUpgrade && !sim.PurchasedArgoUpgrades.Contains(Main.Settings.ArgoUpgrade))
                return;

            if (___selectedChassis != null)
            {
                ___selectedChassis.MechPartMax = __state;
            }
        }
    }
    
    //Correction for scrapping mechs for money.
    [HarmonyPatch(typeof(SimGameState), "ScrapMechPart")]
    public static class SimGameState_ScrapMechPart_Patch
    {
        public static void Prefix(SimGameState __instance, ref float partMax)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Main.Settings.DependsOnArgoUpgrade && !sim.PurchasedArgoUpgrades.Contains(Main.Settings.ArgoUpgrade))
                return;

            partMax = __instance.Constants.Story.DefaultMechPartMax;
        }
    }
}