using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BattleTech;
using BattleTech.Data;
using Harmony;
using HBS.Collections;
using HBS.Logging;
using static Logger;

namespace SalvageOperations
{
    public static class Main
    {
        internal static ILog HBSLog;
        internal static ModSettings Settings;

        public static bool IsResolvingContract { get; private set; }
        public static Dictionary<string, int> SalvageFromContract = new Dictionary<string, int>();
        public static Dictionary<string, int> HasBeenBuilt = new Dictionary<string, int>();
        public static Dictionary<string, int> TestBuildAgain = new Dictionary<string, int>();
        public static string TriggeredVariant = "";

        private static SimGameEventTracker eventTracker = new SimGameEventTracker();
        private static bool _hasInitEventTracker;

        // ENTRY POINT
        public static void Init(string modDir, string settings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.SalvageOperations");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            HBSLog = HBS.Logging.Logger.GetLogger("SalvageOperations");
            Settings = ModSettings.ReadSettings(settings);
            // clears the Logger class' logfile
            Clear();
        }

        // CONTRACTS
        public static void ContractStart()
        {
            IsResolvingContract = true;
            SalvageFromContract.Clear();
        }

        public static void ContractEnd()
        {
            IsResolvingContract = false;
        }

        // UTIL
        public static int GetAllVariantMechParts(SimGameState simGame, MechDef mechDef)
        {
            int mechPieces = 0;

            var variants = GetAllMatchingVariants(simGame.DataManager, mechDef);
            foreach (var variant in variants)
                mechPieces += GetMechParts(simGame, variant);

            return mechPieces;
        }

        public static int GetMechParts(SimGameState simGame, MechDef mechDef)
        {
            return simGame.GetItemCount(mechDef.Description.Id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
        }

        private static List<MechDef> GetAllMatchingVariants(DataManager dataManager, string UIName)
        {
            var variants = new List<MechDef>();

            dataManager.MechDefs
                .Where(x => x.Value.Chassis.Description.UIName == UIName)
                .Do(x => variants.Add(x.Value)); // thanks harmony for the do extension method

            return variants;
        }

        private static List<MechDef> GetAllMatchingVariants(DataManager dataManager, MechDef mechDef)
        {
            return GetAllMatchingVariants(dataManager, mechDef.Chassis.Description.UIName);
        }

        private static string GetItemStatID(string id, string type)
        {
            return $"Item.{type}.{id}";
        }

        private static bool CanAssembleVariant(MechDef variant)
        {
            if (Settings.VariantExceptions.Contains(variant.Description.Id))
                return false;

            if (Settings.TagExceptions != null && Settings.TagExceptions.Count > 0)
            {
                foreach (var tag in Settings.TagExceptions)
                {
                    if (variant.MechTags.Contains(tag))
                        return false;
                }
            }

            return true;
        }

        internal static void SimulateContractSalvage()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            foreach (var mechID in SalvageFromContract.Keys)
            {
                var mechDef = sim.DataManager.MechDefs.Get(mechID);
                if (!HasBeenBuilt.ContainsKey(mechDef.Description.Name))
                    TryBuildMechs(sim, new Dictionary<string, int> {{mechID, 1}});
            }

            SalvageFromContract.Clear();
            HasBeenBuilt.Clear();
            sim.CompanyTags.Remove("SO_Salvaging");
        }

        internal static void GlobalBuild()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var inventorySalvage = new Dictionary<string, int>();
            var inventory = sim.GetAllInventoryMechDefs();
            LogDebug($"inventory {inventory.Count}");
            foreach (var item in inventory)
            {
                var itemCount = 0;
                var id = item.Description.Id.Replace("chassisdef", "mechdef");
                if (!inventorySalvage.ContainsKey(id))
                {
                    itemCount = sim.GetItemCount(id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                    inventorySalvage.Add(id, itemCount);
                    LogDebug($"New key: {id} ({itemCount})");
                }
                else
                {
                    itemCount = sim.GetItemCount(id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                    inventorySalvage[id] += itemCount;
                    LogDebug($"New value for {id} ({itemCount})");
                }
            }

            LogDebug($"inventorySalvage ({inventorySalvage.Count})");

            SalvageFromContract = inventorySalvage;
            HasBeenBuilt.Clear();
            TestBuildAgain.Clear();
            SimulateContractSalvage();
        }

        // MEAT
        private static SimGameEventResult[] GetBuildMechEventResult(SimGameState simGame, MechDef mechDef)
        {
            Log($"Generate Event Result for {mechDef.Chassis.Description.UIName}");
            var stats = new List<SimGameStat>();

            // adds the flatpacked mech
            stats.Add(new SimGameStat(GetItemStatID(mechDef.ChassisID, "MechDef"), 1));
            mechDef.Chassis.ChassisTags.Add("SO_Built");

            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;
            var thisParts = GetMechParts(simGame, mechDef);
            thisParts = Math.Min(thisParts, defaultMechPartMax);

            // removes the parts from the mech we're building from inventory
            stats.Add(new SimGameStat(GetItemStatID(mechDef.Description.Id, "MECHPART"), -thisParts));

            // Record how many parts were used to assemble the mech.
            string tagName = $"SO-{mechDef.ChassisID}_{thisParts}";
            int maxParts = 0;
            string removeTagName = "Temp";

            int i = 1;
            do
            {
                var tempTagName = $"SO-{mechDef.ChassisID}_{i}";
                if (simGame.CompanyTags.Contains(tempTagName))
                {
                    maxParts = i;
                    removeTagName = tempTagName;
                }

                i++;
            } while (i < defaultMechPartMax);

            if (thisParts > maxParts)
            {
                if (simGame.CompanyTags.Contains(removeTagName))
                    simGame.CompanyTags.Remove(removeTagName);

                simGame.CompanyTags.Add(tagName);
            }

            // there could still be parts remaining that we need to delete from other variants
            var otherMechParts = new Dictionary<string, int>();
            var numPartsRemaining = simGame.Constants.Story.DefaultMechPartMax - thisParts;
            //Logger.Log($"numPartsRemaining ({numPartsRemaining})");
            if (numPartsRemaining > 0)
            {
                //Logger.Log($"numPartsRemaining ({numPartsRemaining})");
                // delete 1 from each variant until we've gotten all the parts that we need deleted
                var matchingVariants = GetAllMatchingVariants(simGame.DataManager, mechDef);
                int partsRemoved = 0;
                while (numPartsRemaining > 0)
                {
                    foreach (var variant in matchingVariants)
                    {
                        // Logger.Log($"variant ({variant.Description.Id})");

                        var parts = GetMechParts(simGame, variant);
                        // Logger.Log($"\tparts ({parts})");
                        if (parts > 0 && variant.Description.Id != mechDef.Description.Id)
                        {
                            if (!otherMechParts.ContainsKey(variant.Description.Id))
                                otherMechParts[variant.Description.Id] = 0;

                            if (otherMechParts[variant.Description.Id] != parts)
                            {
                                otherMechParts[variant.Description.Id]++;

                                numPartsRemaining--;
                                partsRemoved++;
                                //Logger.Log($"\tnumPartsRemaining ({numPartsRemaining})");
                                //Logger.Log($"\tpartsRemoved ({partsRemoved})");
                            }

                            if (numPartsRemaining <= 0)
                                break;
                        }
                    }

                    if (partsRemoved == 0)
                    {
                        LogDebug("partsRemoved (was zero)");
                        break;
                    }
                }
            }

            // actually add the stats that will remove the other mech parts
            foreach (var mechID in otherMechParts.Keys)
            {
                stats.Add(new SimGameStat(GetItemStatID(mechID, "MECHPART"), -otherMechParts[mechID]));
            }

            return new[]
            {
                new SimGameEventResult
                {
                    Stats = stats.ToArray(),
                    Scope = EventScope.Company,
                    Actions = new SimGameResultAction[0],
                    AddedTags = new TagSet(),
                    RemovedTags = new TagSet(),
                    ForceEvents = new SimGameForcedEvent[0],
                    Requirements = null,
                    ResultDuration = 0,
                    TemporaryResult = false
                }
            };
        }

        private static void GenerateMechPopup(SimGameState simGame, string UIName)
        {
            var mechParts = new Dictionary<string, int>();
            var variants = GetAllMatchingVariants(simGame.DataManager, UIName);

            MechDef highestVariant = null;
            int highest = 0;
            foreach (var variant in variants)
            {
                var parts = GetMechParts(simGame, variant);

                if (parts <= 0)
                    continue;

                mechParts[variant.Description.Id] = parts;

                if (mechParts[variant.Description.Id] > highest)
                {
                    highestVariant = variant;
                    highest = mechParts[variant.Description.Id];
                }

                HBSLog.Log($"{variant.Description.Id} has {mechParts[variant.Description.Id]} pieces, highest {highest}");
            }

            if (highestVariant == null)
                return;

            // build the result set
            int optionIdx = 0;
            var options = new SimGameEventOption[Math.Min(4, mechParts.Count + 1)];
            bool variantAlreadyListed = false;

            foreach (var variantKVP in mechParts.OrderByDescending(key => key.Value))
            {
                var variant = variantKVP.Key;
                var mechDef = simGame.DataManager.MechDefs.Get(variant);

                if (optionIdx > 2)
                {
                    if (TriggeredVariant != "" && !variantAlreadyListed)
                    {
                        LogDebug($"Triggered build ({TriggeredVariant})");
                        // move options 0 and 1, to 1 and 2

                        var tempOption = options[1];
                        options[1] = options[0];
                        options[2] = tempOption;
                    }

                    options[0] = new SimGameEventOption
                    {
                        Description = new BaseDescriptionDef(TriggeredVariant, $"Build the {mechDef.Description.UIName} ({mechParts[TriggeredVariant]} Parts)", TriggeredVariant, ""),
                        RequirementList = null,
                        ResultSets = new[]
                        {
                            new SimGameEventResultSet
                            {
                                Description = new BaseDescriptionDef(TriggeredVariant, TriggeredVariant, $"You tell Yang that you want him to build the [[DM.MechDefs[{TriggeredVariant}],{{DM.MechDefs[{TriggeredVariant}].Description.UIName}}]] and his eyes light up. \"I can't wait to get started.\"\r\n\r\nHe starts to move behind the pile of scrap, then calls out, \"Oh, and don't forget to submit a work order to 'Ready' the 'Mech when you want to get started on the refit. Remember, the less pieces of this variant you gave me, the longer it will take to get to full working order.\"", ""),
                                Weight = 100,
                                Results = GetBuildMechEventResult(simGame, mechDef)
                            }
                        }
                    };

                    HBSLog.Log("Had more than 3 options, truncating at 3");
                    break;
                }

                LogDebug($"Building event option {optionIdx} for {variant}");

                options[optionIdx] = new SimGameEventOption
                {
                    Description = new BaseDescriptionDef(variant, $"Build the {mechDef.Description.UIName} ({mechParts[variant]} Parts)", variant, ""),
                    RequirementList = null,
                    ResultSets = new[]
                    {
                        new SimGameEventResultSet
                        {
                            Description = new BaseDescriptionDef(variant, variant, $"You tell Yang that you want him to build the [[DM.MechDefs[{variant}],{{DM.MechDefs[{variant}].Description.UIName}}]] and his eyes light up. \"I can't wait to get started.\"\r\n\r\nHe starts to move behind the pile of scrap, then calls out, \"Oh, and don't forget to submit a work order to 'Ready' the 'Mech when you want to get started on the refit. Remember, the less pieces of this variant you gave me, the longer it will take to get to full working order.\"", ""),
                            Weight = 100,
                            Results = GetBuildMechEventResult(simGame, mechDef)
                        }
                    }
                };

                if (variant == TriggeredVariant)
                {
                    LogDebug("variantAlreadyListed");
                    variantAlreadyListed = true;
                }

                optionIdx++;
            }

            // add the option to not build anything
            options[optionIdx] = new SimGameEventOption
            {
                Description = new BaseDescriptionDef("BuildNothing", "Tell Yang not to build anything right now.", "BuildNothing", ""),
                RequirementList = null,
                ResultSets = new[]
                {
                    new SimGameEventResultSet
                    {
                        Description = new BaseDescriptionDef("BuildNothing", "BuildNothing", "Yang looks disappointed for a moment, then grins and shrugs, \"Saving these pieces up makes sense, I guess, never know when they might come in handy later on.\"", ""),
                        Weight = 100,
                        Results = new[]
                        {
                            new SimGameEventResult
                            {
                                Stats = new SimGameStat[0],
                                Scope = EventScope.Company,
                                Actions = new SimGameResultAction[0],
                                AddedTags = new TagSet(),
                                RemovedTags = new TagSet(),
                                ForceEvents = new SimGameForcedEvent[0],
                                Requirements = null,
                                ResultDuration = 0,
                                TemporaryResult = false
                            }
                        }
                    }
                }
            };

            // get rid of null options that throw on popup
            options = options.Where(option => option != null).ToArray();

            // put the selected variant at the top regardless of part count
            if (variantAlreadyListed)
            {
                LogDebug(">>> Reordering options");
                LogDebug("Before");
                foreach (var option in options)
                    LogDebug(option.Description.Name);

                var variantModel = Regex.Match(TriggeredVariant, @".+_.+_(.+)").Groups[1].Value;
                LogDebug($"TriggeredVariant {TriggeredVariant} variantModel {variantModel}");
                var tempOptions = options.Where(option => !option.Description.Name.Contains(variantModel)).ToList();

                foreach (var option in options)
                {
                    if (option.Description.Name.Contains(variantModel))
                        tempOptions.Insert(0, option);
                }

                LogDebug("After");
                foreach (var option in tempOptions)
                    LogDebug(option.Description.Name);

                options = tempOptions.ToArray();
                TriggeredVariant = "";
            }

            // setup the event string based on the situation
            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;

            var eventString = "As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we don't have enough salvage from any single 'Mech to build ourselves a new one, but...\" He pauses dramatically. \"...I could cobble together the salvage from a couple related 'Mechs.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Which one should we build?\"";
            if (mechParts.Count == 1) // we have only a single option
                eventString = $"As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage from the [[DM.MechDefs[{highestVariant}],{highestVariant.Description.UIName}]] to put it together.\" He pauses, rubbing his beard. \"But, we could save it to build another variant, later.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Should we build it?\"";
            else if (highest >= defaultMechPartMax) // we have enough salvage to build a mech
                eventString = "As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage to build a 'Mech out completely, but...\" He pauses dramatically. \"...I could cobble together the salvage from a couple related 'Mechs if you wanted to build something else.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Which one should we build?\"";

            // build the event itself
            var eventDef = new SimGameEventDef(
                SimGameEventDef.EventPublishState.PUBLISHED,
                SimGameEventDef.SimEventType.UNSELECTABLE,
                EventScope.Company,
                new DescriptionDef(
                    "SalvageOperationsEventID",
                    "Salvage Operations",
                    eventString,
                    "uixTxrSpot_YangWorking.png",
                    0, 0, false, "", "", ""),
                new RequirementDef {Scope = EventScope.Company},
                new RequirementDef[0],
                new SimGameEventObject[0],
                options, 1);
            if (!_hasInitEventTracker)
            {
                eventTracker.Init(new[] {EventScope.Company}, 0, 0, SimGameEventDef.SimEventType.NORMAL, simGame);
                _hasInitEventTracker = true;
            }

            simGame.InterruptQueue.QueueEventPopup(eventDef, EventScope.Company, eventTracker);
        }

        public static void TryBuildMechs(SimGameState simGame, Dictionary<string, int> mechPieces)
        {
            // Logger.Log($"TryBuildMechs {mechPieces.Count} mechIDs");
            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;

            var chassisPieces = new Dictionary<string, int>();

            // setup chassis pieces for the pieces that we received
            foreach (var mechID in mechPieces.Keys)
            {
                var mechDef = simGame.DataManager.MechDefs.Get(mechID);

                if (!chassisPieces.ContainsKey(mechDef.Chassis.Description.UIName))
                    chassisPieces[mechDef.Chassis.Description.UIName] = 0;

                //   Logger.Log($"{mechID} has UIName {mechDef.Chassis.Description.UIName}");
            }

            // try to build each chassis
            var UINames = chassisPieces.Keys.ToList();
            var UIName = UINames.First();

            // add chassis pieces that we already have
            var matchingMechDefs = GetAllMatchingVariants(simGame.DataManager, UIName);

            string mechName = "name";
            foreach (var mechDef in matchingMechDefs)
            {
                chassisPieces[UIName] += GetMechParts(simGame, mechDef);
                mechName = mechDef.Description.Name;
            }

            // Logger.Log($"{UIName} has {chassisPieces[UIName]} pieces");
            if (chassisPieces[UIName] >= defaultMechPartMax)
            {
                // has enough pieces to build a mech, generate popup
                // Logger.Log($"Generating popup for {UIName}");
                GenerateMechPopup(simGame, UIName);
                if (!HasBeenBuilt.ContainsKey(mechName))
                {
                    HasBeenBuilt[mechName] = 1;
                    // TestBuildAgain[UIName] = 1;
                }
            }
        }
    }
}