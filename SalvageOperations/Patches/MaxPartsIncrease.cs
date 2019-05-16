using BattleTech;
using Harmony;
using BattleTech.UI;
using BattleTech.Data;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisUnitElement), "SetData")]
    public static class MechBayChassisUnitElement_SetData_Patch
    {
        public static void Prefix(ChassisDef chassisDef, ref int partsCount, ref int partsMax, int chassisQuantity)
        {
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

    [HarmonyPatch(typeof(MechBayChassisInfoWidget), "SetDescriptions")]
    public static class MechBayChassisInfoWidget_SetDescriptions_Patch
    {
        public static void Prefix(MechBayChassisInfoWidget __instance, ChassisDef ___selectedChassis, int __state)
        {
            if (___selectedChassis != null)
            {
                __state = ___selectedChassis.MechPartMax;
                ___selectedChassis.MechPartMax = 99;
            }
        }

        public static void Postfix(MechBayChassisInfoWidget __instance, ChassisDef ___selectedChassis, int __state)
        {
            if (___selectedChassis != null)
            {
                ___selectedChassis.MechPartMax = __state;
            }
        }
    }

    [HarmonyPatch(typeof(SimGameState), "UnreadyMech")]
    public static class SimGameState_UnreadyMech_Patch
    {
        public static void Prefix(SimGameState __instance, MechDef def)
        {
            def.Chassis.ChassisTags.Add("SO_Built");
        }
    }
}