using BattleTech;
using Harmony;
using HBS.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace StarkSalvage
{
    [HarmonyPatch(typeof(Contract), "GenerateSalvage")]
    public static class Contract_GenerateSalvage_Patch
    {
        private static readonly ChassisLocations[] LIMB_LOCATIONS = { ChassisLocations.LeftArm, ChassisLocations.RightArm, ChassisLocations.LeftLeg, ChassisLocations.RightLeg };

        public static void Postfix(Contract __instance, List<UnitResult> enemyMechs, List<VehicleDef> enemyVehicles, List<UnitResult> lostUnits, bool logResults = false)
        {
            var simGame = __instance.BattleTechGame.Simulation;

            if (simGame == null)
                return;
            
            var instTrav = Traverse.Create(__instance);
            var finalPotentialSalvage = instTrav.Field("finalPotentialSalvage").GetValue<List<SalvageDef>>();
            var maxMechParts = simGame.Constants.Story.DefaultMechPartMax;

            // remove all mech parts
            var removedSalvage = finalPotentialSalvage.RemoveAll(x => x.Type == SalvageDef.SalvageType.MECH_PART);
            Main.HBSLog.Log($"Removed {removedSalvage} mech pieces.");

            // go through enemyMechs and re-add mech parts based on damage
            foreach (var unitResult in enemyMechs)
            {
                var mechDef = unitResult.mech;
                var pilotDef = unitResult.pilot;

                // if the mech wasn't destroyed or the pilot wasn't killed then we don't get to salvage
                // thanks to morph's AdjustedMechSalvage for pointing out the critical component stuff
                if (!pilotDef.IsIncapacitated && !mechDef.IsDestroyed && !mechDef.Inventory.Any(x => x.Def != null && x.Def.CriticalComponent && x.DamageLevel == ComponentDamageLevel.Destroyed))
                    continue;
                
                float bits = 0;

                // CT is worth 1/2 of the salvage
                if (!mechDef.IsLocationDestroyed(ChassisLocations.CenterTorso))
                    bits += maxMechParts / 2;
                
                // limbs combined are worth the other 1/2 of the salvage, so 1/8 each
                foreach (var limbLocation in LIMB_LOCATIONS)
                    if (!mechDef.IsLocationDestroyed(limbLocation))
                        bits += maxMechParts / 8;

                // just legs 2/8 + 4/8 = 6/8 salvage * 5 mechPieces = 3.75 ~ 4
                // both shoulders + leg = 1/8 + 4/8 = 5/8 * 5 = 3.125 ~ 3
                // cored with all limbs = 4/8 = 4/8 * 5 = 2.5  ~ 2
                // cored with 3 limbs 3/8 * 5

                // round .5 down by subtracting a little
                int mechParts = (int)Math.Round(bits - 0.05);

                Main.HBSLog.Log($"{mechDef.Description.Id} {mechParts}");

                if (mechParts > 0)
                    instTrav.Method("CreateAndAddMechPart", simGame.Constants, mechDef, mechParts, finalPotentialSalvage).GetValue();
            }
        }
    }
    
    public static class Main
    {
        public static ILog HBSLog;
        public static void Init()
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.StarkSalvage");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            HBSLog = Logger.GetLogger("StarkSalvage");
        }
    }
}
