using BattleTech;
using Harmony;

// ReSharper disable InconsistentNaming

namespace SalvageOperations.Patches
{
    public class MechBayChassisInfoWidget
    {
        // some of the patches to enable 99 parts
        [HarmonyPatch(typeof(MechBayChassisInfoWidget), "SetDescriptions")]
        public static class MechBayChassisInfoWidget_SetDescriptions_Patch
        {
            public static void Prefix(MechBayChassisInfoWidget __instance, ChassisDef ___selectedChassis, ref int __state)
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
                    ___selectedChassis.MechPartMax = __state;
            }
        }
    }
}
