using BattleTech;
using BattleTech.Data;
using BattleTech.UI;
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

                var mechParts = (int)Math.Floor(bits);
                Main.HBSLog.Log($"= floor({bits}) = {mechParts}");

                if (mechParts > 0)
                    instTrav.Method("CreateAndAddMechPart", simGame.Constants, mechDef, mechParts, finalPotentialSalvage).GetValue();
            }
        }
    }


    [HarmonyPatch(typeof(SimGameState), "AddMechPart")]
    public static class SimGameState_AddMechPart_Patch
    {
        public static bool Prefix(SimGameState __instance, string id)
        {
            // we're in the middle of resolving a contract
            if (Main.IsResolvingContract)
            {
                if (!Main.SalvageFromContract.ContainsKey(id))
                    Main.SalvageFromContract[id] = 0;

                Main.SalvageFromContract[id]++;
                return false;
            }

            Main.TryBuildMechs(__instance, new Dictionary<string, int> { { id, 1 } });
            return false;
        }
    }


    [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
    public static class SimGameState_ResolveCompleteContract_Patch
    {
        public static void Prefix()
        {
            Main.IsResolvingContract = true;
        }

        public static void Postfix(SimGameState __instance)
        {
            Main.TryBuildMechs(__instance, Main.SalvageFromContract);

            Main.IsResolvingContract = false;
            Main.SalvageFromContract.Clear();
        }
    }


    public static class Main
    {
        public static ILog HBSLog;
        public static bool IsResolvingContract = false;
        public static Dictionary<string, int> SalvageFromContract = new Dictionary<string, int>();


        private static void AddMechPieces(SimGameState simGame, string id, int num)
        {
            for (int i = 0; i < num; i++)
                simGame.AddItemStat(id, "MECHPART", false);

            HBSLog.Log($"Added {num} {id} pieces");
        }

        private static void RemoveMechPieces(SimGameState simGame, string id, int num)
        {
            for (int i = 0; i < num; i++)
                Traverse.Create(simGame).Method("RemoveItemStat", new Type[] { typeof(string), typeof(string), typeof(bool) }).GetValue(id, "MECHPART", false);

            HBSLog.Log($"Removed {num} {id} pieces");
        }

        private static int GetMechPieces(SimGameState simGame, MechDef mechDef)
        {
            return simGame.GetItemCount(mechDef.Description.Id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
        }

        private static List<MechDef> GetAllMatchingVariants(DataManager dataManager, string prefabID)
        {
            var variants = new List<MechDef>();

            dataManager.MechDefs
                .Where(x => x.Value.Chassis.PrefabIdentifier == prefabID)
                .Do(x => variants.Add(x.Value)); // thanks harmony for the do extention method

            return variants;
        }

        private static List<MechDef> GetAllMatchingVariants(DataManager dataManager, MechDef mechDef)
        {
            return GetAllMatchingVariants(dataManager, mechDef.Chassis.PrefabIdentifier);
        }


        private static void GenerateMechPopup(SimGameState simGame, string prefabID)
        {
            var mechPieces = new Dictionary<string, int>();
            var variants = GetAllMatchingVariants(simGame.DataManager, prefabID);

            MechDef highestVariant = null;
            int highest = 0;
            foreach (var variant in variants)
            {
                mechPieces[variant.Description.Id] = GetMechPieces(simGame, variant);

                if (mechPieces[variant.Description.Id] > highest)
                {
                    highestVariant = variant;
                    highest = mechPieces[variant.Description.Id];
                }

                HBSLog.Log($"{variant.Description.Id} has {mechPieces[variant.Description.Id]} pieces, highest {highest}");
            }

            if (highestVariant == null)
                return;

            var popup = new SimGameInterruptManager.GenericPopupEntry(
                $"Could Build {highestVariant.Description.UIName}",
                $"Commander, you've got enough 'Mech parts to build a {highestVariant.Description.UIName} from the pieces of multiple related 'Mechs. Do you want to do this?",
                false)
                .AddButton("No")
                .AddButton("Yes", new Action(() => BuildMech(simGame, highestVariant)));

            simGame.InterruptQueue.AddInterrupt(popup, true);
        }

        private static void BuildMech(SimGameState simGame, MechDef mechDef)
        {
            HBSLog.Log($"BuildMech {mechDef.Description.Name}");

            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;
            var thisParts = GetMechPieces(simGame, mechDef);

            // cap thisParts at defaultMechPartMax
            thisParts = Math.Min(thisParts, defaultMechPartMax);

            // remove the parts from this variant from inventory
            RemoveMechPieces(simGame, mechDef.Description.Id, thisParts);

            // there could still be parts remaining that we need to delete from other variants
            var numPartsRemaining = simGame.Constants.Story.DefaultMechPartMax - thisParts;
            if (numPartsRemaining > 0)
            {
                // delete 1 from each variant until we've gotten all the parts that we need deleted
                var matchingVariants = GetAllMatchingVariants(simGame.DataManager, mechDef);
                while (numPartsRemaining > 0)
                {
                    int partsRemoved = 0;
                    foreach (var variant in matchingVariants)
                    {
                        var parts = GetMechPieces(simGame, variant);
                        if (parts > 0)
                        {
                            RemoveMechPieces(simGame, variant.Description.Id, 1);

                            numPartsRemaining--;
                            partsRemoved++;

                            if (numPartsRemaining <= 0)
                                break;
                        }
                    }

                    if (partsRemoved == 0)
                        break;
                }
            }

            // add the flatpacked mech
            simGame.AddItemStat(mechDef.Chassis.Description.Id, mechDef.GetType(), false);

            // display a message
            simGame.InterruptQueue.QueuePauseNotification(
                $"{mechDef.Description.UIName} Built",
                mechDef.Chassis.YangsThoughts,
                simGame.GetCrewPortrait(SimGameCrew.Crew_Yang),
                "notification_mechreadycomplete");

            simGame.InterruptQueue.DisplayIfAvailable();
            simGame.MessageCenter.PublishMessage(new SimGameMechAddedMessage(mechDef, defaultMechPartMax, true));
        }


        public static void TryBuildMechs(SimGameState simGame, Dictionary<string, int> mechPieces)
        {
            HBSLog.Log($"TryBuildMechs {mechPieces.Count} mechIDs");
            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;
            var chassisPieces = new Dictionary<string, int>();

            // count chassis pieces from mechPieces
            foreach (var mechID in mechPieces.Keys)
            {
                var mechDef = simGame.DataManager.MechDefs.Get(mechID);

                if (!chassisPieces.ContainsKey(mechDef.Chassis.PrefabIdentifier))
                    chassisPieces[mechDef.Chassis.PrefabIdentifier] = 0;

                AddMechPieces(simGame, mechID, mechPieces[mechID]);
                HBSLog.Log($"{mechID} has prefabID {mechDef.Chassis.PrefabIdentifier}");
            }

            // try to build each chassis
            var prefabIDs = chassisPieces.Keys.ToList();
            foreach (var prefabID in prefabIDs)
            {
                // add chassis pieces that we already have
                var matchingMechDefs = GetAllMatchingVariants(simGame.DataManager, prefabID);

                foreach (var mechDef in matchingMechDefs)
                    chassisPieces[prefabID] += GetMechPieces(simGame, mechDef);

                HBSLog.Log($"{prefabID} has {chassisPieces[prefabID]} pieces");

                if (chassisPieces[prefabID] >= defaultMechPartMax)
                {
                    // has enough pieces to build a mech, generate popup
                    HBSLog.Log($"Generting popup for {prefabID}");
                    GenerateMechPopup(simGame, prefabID);
                }
            }
        }


        public static void Init()
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.StarkSalvage");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            HBSLog = Logger.GetLogger("StarkSalvage");
        }
    }
}
