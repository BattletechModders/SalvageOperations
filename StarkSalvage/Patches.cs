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
            __instance.AddItemStat(id, "MECHPART", false);

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

    // this is to patch the stat display on the event, since it's broken with flatpacked mechs and mech parts
    [HarmonyPatch(typeof(DataManagerExtensions), "GetStatDescDef")]
    public static class DataManagerExtensions_GetStatDescDef_Patch
    {
        public static bool Prefix(DataManager dataManager, SimGameStat simGameStat, ref SimGameStatDescDef __result)
        {
            string text = "SimGameStatDesc_" + simGameStat.name;
            if (!dataManager.Exists(BattleTechResourceType.SimGameStatDescDef, text))
            {
                if (!text.Contains("SimGameStatDesc_Item"))
                    return true;

                var itemStatDesc = dataManager.SimGameStatDescDefs.Get("SimGameStatDesc_Item");
                var split = text.Split('.');

                if (text.Contains("MECHPART"))
                {
                    var statDescDef = new SimGameStatDescDef();
                    var mechDef = dataManager.MechDefs.Get(split[2]);

                    if (mechDef == null)
                        return true;

                    statDescDef.Description.SetName($"{mechDef.Description.UIName} Parts");
                    __result = statDescDef;
                    return false;
                }
                else if (text.Contains("MechDef"))
                {
                    var statDescDef = new SimGameStatDescDef();
                    var chassisDef = dataManager.ChassisDefs.Get(split[2]);

                    if (chassisDef == null)
                        return true;

                    statDescDef.Description.SetName($"{chassisDef.Description.UIName}");
                    __result = statDescDef;
                    return false;
                }
            }

            return true;
        }
    }

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


    public static class Main
    {
        public static ILog HBSLog;
        public static bool IsResolvingContract = false;
        public static Dictionary<string, int> SalvageFromContract = new Dictionary<string, int>();

        private static SimGameEventTracker eventTracker = new SimGameEventTracker();
        private static bool hasInitEventTracker = false;


        public static int GetAllVariantMechPieces(SimGameState simGame, MechDef mechDef)
        {
            int mechPieces = 0;

            var variants = GetAllMatchingVariants(simGame.DataManager, mechDef);
            foreach (var variant in variants)
                mechPieces += GetMechPieces(simGame, variant);

            return mechPieces;
        }

        public static int GetMechPieces(SimGameState simGame, MechDef mechDef)
        {
            return simGame.GetItemCount(mechDef.Description.Id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
        }

        public static List<MechDef> GetAllMatchingVariants(DataManager dataManager, string prefabID)
        {
            var variants = new List<MechDef>();

            dataManager.MechDefs
                .Where(x => x.Value.Chassis.PrefabIdentifier == prefabID)
                .Do(x => variants.Add(x.Value)); // thanks harmony for the do extention method

            return variants;
        }

        public static List<MechDef> GetAllMatchingVariants(DataManager dataManager, MechDef mechDef)
        {
            return GetAllMatchingVariants(dataManager, mechDef.Chassis.PrefabIdentifier);
        }

        public static string GetItemStatID(string id, string type)
        {
            return string.Format("{0}.{1}.{2}", "Item", type, id);
        }

        public static string GetItemStatID(string id, Type type)
        {
            string text = type.ToString();
            if (text.Contains("."))
            {
                text = text.Split(new char[]
                {
                    '.'
                })[1];
            }
            return string.Format("{0}.{1}.{2}", "Item", text, id);
        }


        private static SimGameEventResult[] GetBuildMechEventResult(SimGameState simGame, MechDef mechDef)
        {
            HBSLog.Log($"Generate Event Result for {mechDef.Description.Id}");

            var stats = new List<SimGameStat>();

            // adds the flatpacked mech
            stats.Add(new SimGameStat(GetItemStatID(mechDef.Chassis.Description.Id, typeof(MechDef)), 1));

            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;
            var thisParts = GetMechPieces(simGame, mechDef);
            thisParts = Math.Min(thisParts, defaultMechPartMax);

            // removes the parts from the mech we're building from inventory
            stats.Add(new SimGameStat(GetItemStatID(mechDef.Description.Id, "MECHPART"), -thisParts));

            // there could still be parts remaining that we need to delete from other variants
            var otherMechParts = new Dictionary<string, int>();
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
                        if (parts > 0 && variant.Description.Id != mechDef.Description.Id)
                        {
                            if (!otherMechParts.ContainsKey(variant.Description.Id))
                                otherMechParts[variant.Description.Id] = 0;

                            otherMechParts[variant.Description.Id]++;

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

            // actually add the stats that will remove the other mech parts
            foreach (var otherMechPartsKVP in otherMechParts)
                stats.Add(new SimGameStat(GetItemStatID(otherMechPartsKVP.Key, "MECHPART"), -otherMechPartsKVP.Value));

            foreach(var stat in stats)
                HBSLog.Log($"Event Stat {stat.name} {stat.value}");

            return new SimGameEventResult[] { new SimGameEventResult
            {
                Stats = stats.ToArray(),
                Scope = EventScope.Company,
                Actions = new SimGameResultAction[0],
                AddedTags = new HBS.Collections.TagSet(),
                RemovedTags = new HBS.Collections.TagSet(),
                ForceEvents = new SimGameForcedEvent[0],
                Requirements = null,
                ResultDuration = 0,
                TemporaryResult = false
            } };
        }

        private static void GenerateMechPopup(SimGameState simGame, string prefabID)
        {
            var mechPieces = new Dictionary<string, int>();
            var variants = GetAllMatchingVariants(simGame.DataManager, prefabID);

            MechDef highestVariant = null;
            int highest = 0;
            foreach (var variant in variants)
            {
                var pieces = GetMechPieces(simGame, variant);

                if (pieces <= 0)
                    continue;

                mechPieces[variant.Description.Id] = pieces;

                if (mechPieces[variant.Description.Id] > highest)
                {
                    highestVariant = variant;
                    highest = mechPieces[variant.Description.Id];
                }

                HBSLog.Log($"{variant.Description.Id} has {mechPieces[variant.Description.Id]} pieces, highest {highest}");
            }

            if (highestVariant == null)
                return;

            // build the result set
            int optionIdx = 0;
            var options = new SimGameEventOption[Math.Min(4, mechPieces.Count + 1)];
            foreach (var variantKVP in mechPieces.OrderByDescending(key => key.Value))
            {
                var variant = variantKVP.Key;
                HBSLog.Log($"Building event option {optionIdx} for {variant}");

                if (optionIdx > 2)
                {
                    HBSLog.Log($"Had more than 3 options, truncating at 3");
                    break;
                }

                var mechDef = simGame.DataManager.MechDefs.Get(variant);
                options[optionIdx++] = new SimGameEventOption
                {
                    Description = new BaseDescriptionDef(variant, $"Build the {mechDef.Description.UIName} ({mechPieces[variant]} Parts)", variant, ""),
                    RequirementList = null,
                    ResultSets = new SimGameEventResultSet[]
                    {
                        new SimGameEventResultSet
                        {
                            Description = new BaseDescriptionDef(variant, variant, $"You tell Yang that you want him to build the {mechDef.Description.UIName} and his eyes light up. \"I can't wait to get started.\"\r\n\r\nHe starts to move behind the pile of scrap, then calls out, \"Oh, and don't forget to submit the work to 'Ready' the 'Mech when you want to get started on the refit.\"", ""),
                            Weight = 100,
                            Results = GetBuildMechEventResult(simGame, mechDef)
                        }
                    }
                };
            }

            // add the option to not build anything
            options[optionIdx] = new SimGameEventOption
            {
                Description = new BaseDescriptionDef("BuildNothing", $"Tell Yang not to build anything right now.", "BuildNothing", ""),
                RequirementList = null,
                ResultSets = new SimGameEventResultSet[]
                {
                    new SimGameEventResultSet
                    {
                        Description = new BaseDescriptionDef("BuildNothing", "BuildNothing", "Yang looks disappointed for a moment, then grins and shrugs, \"Saving these pieces up makes sense, I guess, never know when they might come in handy later on.\"", ""),
                        Weight = 100,
                        Results = new SimGameEventResult[] { new SimGameEventResult
                        {
                            Stats = new SimGameStat[0],
                            Scope = EventScope.Company,
                            Actions = new SimGameResultAction[0],
                            AddedTags = new HBS.Collections.TagSet(),
                            RemovedTags = new HBS.Collections.TagSet(),
                            ForceEvents = new SimGameForcedEvent[0],
                            Requirements = null,
                            ResultDuration = 0,
                            TemporaryResult = false
                        } }
                    }
                }
            };

            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;

            // setup the event string based on the situation
            var eventString = "As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we don't have enough salvage from any single 'Mech to build ourselves a new one, but...\" He pauses dramatically. \"...I could cobble together the salvage from a couple related 'Mechs.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Which one should we build?\"";
            if (mechPieces.Count == 1) // we have only a single option
                eventString = $"As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage from the {highestVariant.Description.UIName} to put it together.\" He pauses, rubbing his beard. \"But, we could save it to build another variant, later.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Should we build it?\"";
            else if (highest >= defaultMechPartMax) // we have enough salvage to build a mech
                eventString = "As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage to build a 'Mech out completely, but...\" He pauses dramatically. \"...I could cobble together the salvage from a couple related 'Mechs if you wanted to build something else.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Which one should we build?\"";

            // build the event itself
            var eventDef = new SimGameEventDef(
                SimGameEventDef.EventPublishState.PUBLISHED,
                SimGameEventDef.SimEventType.UNSELECTABLE,
                EventScope.Company,
                new DescriptionDef(
                    "StarkSalvageEventID",
                    $"Playing With Salvage",
                    eventString,
                    "uixTxrSpot_YangWorking.png",
                    0, 0, false, "", "", ""),
                new RequirementDef { Scope = EventScope.Company },
                new RequirementDef[0],
                new SimGameEventObject[0],
                options,
                1);

            if (!hasInitEventTracker)
            {
                eventTracker.Init(new EventScope[] { EventScope.Company }, 0, 0, SimGameEventDef.SimEventType.NORMAL, simGame);
                hasInitEventTracker = true;
            }

            simGame.InterruptQueue.QueueEventPopup(eventDef, EventScope.Company, eventTracker);
        }

        public static void TryBuildMechs(SimGameState simGame, Dictionary<string, int> mechPieces)
        {
            HBSLog.Log($"TryBuildMechs {mechPieces.Count} mechIDs");
            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;
            var chassisPieces = new Dictionary<string, int>();

            // setup chassis pieces for the pieces that we recieved
            foreach (var mechID in mechPieces.Keys)
            {
                var mechDef = simGame.DataManager.MechDefs.Get(mechID);

                if (!chassisPieces.ContainsKey(mechDef.Chassis.PrefabIdentifier))
                    chassisPieces[mechDef.Chassis.PrefabIdentifier] = 0;

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
