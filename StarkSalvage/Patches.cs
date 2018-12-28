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

                // if the mech wasn't destroyed or the pilot wasn't killed
                // then we don't get to salvage
                // thanks to morph's AdjustedMechSalvage for pointing out the critical component stuff
                if (!pilotDef.IsIncapacitated && !mechDef.IsDestroyed && !mechDef.Inventory.Any(x => x.Def != null && x.Def.CriticalComponent && x.DamageLevel == ComponentDamageLevel.Destroyed))
                    continue;

                float numParts = 1;
                if (!mechDef.IsLocationDestroyed(ChassisLocations.CenterTorso))
                {
                    // add limb value for each intact limb
                    foreach (var limbLocation in LIMB_LOCATIONS)
                        if (!mechDef.IsLocationDestroyed(limbLocation))
                            numParts += (maxMechParts - 1) / LIMB_LOCATIONS.Length;
                }

                Main.HBSLog.Log($"CreateAndAddMechPart {mechDef.Description.Id} {(int)Math.Round(numParts)}");
                instTrav.Method("CreateAndAddMechPart", simGame.Constants, mechDef, (int)Math.Round(numParts), finalPotentialSalvage).GetValue();
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
