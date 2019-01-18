using BattleTech;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SalvageOperations
{
    [HarmonyPatch(typeof(Contract), "GenerateSalvage")]
    public static class Contract_GenerateSalvage_Patch
    {
        private static readonly ChassisLocations[] BODY_LOCATIONS = { ChassisLocations.LeftArm, ChassisLocations.RightArm, ChassisLocations.LeftLeg, ChassisLocations.RightLeg, ChassisLocations.LeftTorso, ChassisLocations.RightTorso };

        public static void Postfix(Contract __instance, List<UnitResult> enemyMechs, List<VehicleDef> enemyVehicles, List<UnitResult> lostUnits, bool logResults = false)
        {
            var simGame = __instance.BattleTechGame.Simulation;

            if (simGame == null)
                return;

            var instTrav = Traverse.Create(__instance);

            var finalPotentialSalvage = instTrav.Field("finalPotentialSalvage").GetValue<List<SalvageDef>>();
            var maxMechParts = simGame.Constants.Story.DefaultMechPartMax;

            // remove all mech parts
            var numRemovedSalvage = finalPotentialSalvage.RemoveAll(x => x.Type == SalvageDef.SalvageType.MECH_PART);
            Main.HBSLog.Log($"Removed {numRemovedSalvage} mech pieces.");

            // go through enemyMechs and re-add mech parts based on damage
            foreach (var unitResult in enemyMechs)
            {
                var mechDef = unitResult.mech;
                var pilotDef = unitResult.pilot;

                // if the mech wasn't destroyed or the pilot wasn't killed then we don't get to salvage
                // thanks to morph's AdjustedMechSalvage for pointing out the critical component stuff
                if (!pilotDef.IsIncapacitated && !mechDef.IsDestroyed && !mechDef.Inventory.Any(x => x.Def != null && x.Def.CriticalComponent && x.DamageLevel == ComponentDamageLevel.Destroyed))
                    continue;

                double bits = 0;
                Main.HBSLog.Log($"Evaluating {mechDef.Description.Id}");

                // CT is worth 1/2 of the salvage
                if (!mechDef.IsLocationDestroyed(ChassisLocations.CenterTorso))
                {
                    bits += maxMechParts / 2.0;
                    Main.HBSLog.Log($"+ {maxMechParts / 2.0} CT Intact");
                }

                // rest of the 6 pieces combined are worth the other 1/2 of the salvage, so 1/12 each
                foreach (var limbLocation in BODY_LOCATIONS)
                {
                    if (!mechDef.IsLocationDestroyed(limbLocation))
                    {
                        bits += maxMechParts / 12.0;
                        Main.HBSLog.Log($"+ {maxMechParts / 12.0} {limbLocation} Intact");
                    }
                }

                var mechParts = (int)Math.Round(bits - 0.25);
                Main.HBSLog.Log($"= floor({bits}) = {mechParts}");

                if (mechParts > 0)
                    instTrav.Method("CreateAndAddMechPart", simGame.Constants, mechDef, mechParts, finalPotentialSalvage).GetValue();
            }
        }
    }
}