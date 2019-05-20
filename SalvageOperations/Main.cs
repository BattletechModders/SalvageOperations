using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class Main
    {
        internal static ILog HBSLog;
        internal static ModSettings Settings;
        internal static string modDir;

        internal static bool IsResolvingContract { get; private set; }
        internal static Dictionary<string, int> SalvageFromContract = new Dictionary<string, int>();
        internal static List<string> HasBeenBuilt = new List<string>();

        internal static MechDef TriggeredVariant;
        //internal static string TriggeredVariant;

        private static SimGameEventTracker eventTracker = new SimGameEventTracker();
        private static bool _hasInitEventTracker;
        internal static bool Salvaging;

        // ENTRY POINT
        public static void Init(string directory, string settings)
        {
            modDir = directory;
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

        private static List<MechDef> GetAllMatchingVariants(DataManager dataManager, string prefabId)
        {
            var variants = new List<MechDef>();

            dataManager.MechDefs
                .Where(x => x.Value.Chassis.PrefabIdentifier == prefabId)
                .Do(x => variants.Add(x.Value)); // thanks harmony for the do extension method

            // Do not allow parts from Excluded mechs to be used for builds.
            var allowedVariants = new List<MechDef>(variants);

            // Logger.Log("Scrubbing Started");
            // Logger.Log(ExcludedVariantHolder.Description.UIName);
            //if (Settings.ExcludeVariants)
            //{
            //    if (Settings.VariantExceptionsDefs.Contains(id))
            //    {
            //        allowedVariants.Clear();
            //        allowedVariants.Add(id);
            //        //   Logger.Log("Matched");
            //        //   Logger.Log(ExcludedVariantHolder.Description.UIName);
            //    }
            //    else
            //    {
            //        foreach (MechDef mechdef in variants)
            //        {
            //            if (Settings.VariantExceptionsDefs.Contains(mechdef.Description.Id))
            //            {
            //                //   Logger.Log("Removed");
            //                //    Logger.Log(mechdef.Description.Id);
            //                allowedVariants.Remove(mechdef);
            //            }
            //        }
            //    }
            //}
            //
            //if (Settings.VariantExceptionTags.Contains(tag))
            //{
            //    
            //}

            return allowedVariants;
        }

        private static List<MechDef> GetAllMatchingVariants(DataManager dataManager, MechDef mechDef)
        {
            return GetAllMatchingVariants(dataManager, mechDef.Chassis.PrefabIdentifier);
        }

        private static string GetItemStatID(string id, string type)
        {
            return $"Item.{type}.{id}";
        }

        internal static void SimulateContractSalvage()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            foreach (var mechID in SalvageFromContract.Keys)
            {
                var mechDef = sim.DataManager.MechDefs.Get(mechID);
                LogDebug($"salvage mechID: {mechID} ({mechDef.Description.Name})");

                if (!HasBeenBuilt.Contains(mechDef.Description.Name))
                {
                    LogDebug("Hasn't been built");
                    //ExcludedVariantHolder = mechDef;
                    TryBuildMechs(sim, new Dictionary<string, int> {{mechID, 1}});
                }
            }

            LogDebug("Done contract salvage");
            SalvageFromContract.Clear();
            HasBeenBuilt.Clear();
            Salvaging = false;
        }

        internal static void GlobalBuild()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var inventorySalvage = new Dictionary<string, int>();
            var inventory = sim.GetAllInventoryMechDefs();
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
            LogDebug("SimulateContractSalvage");
            SimulateContractSalvage();
        }

        // MEAT
        private static SimGameEventResult[] GetBuildMechEventResult(SimGameState simGame, MechDef mechDef)
        {
            //LogDebug($"Generate Event Result for {mechDef.Chassis.Description.UIName}");
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
                    // want to log this down below
                    var tempVariant = new MechDef();
                    foreach (var variant in matchingVariants)
                    {
                        tempVariant = variant;
                        var parts = GetMechParts(simGame, variant);
                        LogDebug($"variant {variant.Description.Id}, parts {parts}");
                        if (parts > 0 && variant.Description.Id != mechDef.Description.Id)
                        {
                            if (!otherMechParts.ContainsKey(variant.Description.Id))
                                otherMechParts[variant.Description.Id] = 0;

                            if (otherMechParts[variant.Description.Id] != parts)
                            {
                                otherMechParts[variant.Description.Id]++;

                                numPartsRemaining--;
                                partsRemoved++;
                                Logger.LogDebug($"\tnumPartsRemaining ({numPartsRemaining})");
                                Logger.LogDebug($"\tpartsRemoved ({partsRemoved})");
                            }

                            if (numPartsRemaining <= 0)
                                break;
                        }
                    }

                    if (partsRemoved == 0)
                    {
                        LogDebug($"variant {tempVariant.Description.Id}, parts 0");
                        break;
                    }
                }
            }

            // actually add the stats that will remove the other mech parts
            foreach (var mechID in otherMechParts.Keys)
            {
                LogDebug("Adding MECHPART removal stat " + mechID);
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

        private static void GenerateMechPopup(SimGameState simGame, string prefabId)
        {
            var mechParts = new Dictionary<string, int>();

            var variants = GetAllMatchingVariants(simGame.DataManager, prefabId);

            MechDef highestVariant = null;
            int highest = 0;
            foreach (var variant in variants)
            {
                var partsOfThisVariant = GetMechParts(simGame, variant);

                if (partsOfThisVariant <= 0)
                    continue;

                if (mechParts.ContainsKey(variant.Description.Id))
                    mechParts[variant.Description.Id] = partsOfThisVariant;
                else
                    mechParts.Add(variant.Description.Id, partsOfThisVariant);

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
                    // worry about ordering later
                    if (TriggeredVariant != null && !variantAlreadyListed)
                    {
                        LogDebug($"Triggered build {TriggeredVariant.Description.Id} already appears, placing it first");
                        // move options 0 and 1, to 1 and 2
                        if (options[0].Description.Name != TriggeredVariant.Description.UIName)
                        {
                            var tempOption = options[1];
                            options[1] = options[0];
                            options[2] = tempOption;

                            var mechId = TriggeredVariant.Description.Id;
                            options[0] = new SimGameEventOption
                            {
                                Description = new BaseDescriptionDef(mechId, $"Build the {mechDef.Description.UIName} ({mechParts[mechId]} Parts)", mechId, ""),
                                RequirementList = null,
                                ResultSets = new[]
                                {
                                    new SimGameEventResultSet
                                    {
                                        Description = new BaseDescriptionDef(mechId, mechId, $"You tell Yang that you want him to build the [[DM.MechDefs[{mechId}],{{DM.MechDefs[{mechId}].Description.UIName}}]] and his eyes light up. \"I can't wait to get started.\"\r\n\r\nHe starts to move behind the pile of scrap, then calls out, \"Oh, and don't forget to submit a work order to 'Ready' the 'Mech when you want to get started on the refit. Remember, the less pieces of this variant you gave me, the longer it will take to get to full working order.\"", ""),
                                        Weight = 100,
                                        Results = GetBuildMechEventResult(simGame, mechDef)
                                    }
                                }
                            };
                        }
                        else
                            LogDebug("Already first in the list");
                    }

                    LogDebug("Had more than 3 options, truncating at 3");
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

                // global build
                if (TriggeredVariant != null)
                {
                    LogDebug($"Comparing {variant} and {TriggeredVariant.Description.Id}");
                    if (variant == TriggeredVariant.Description.Id)
                    {
                        LogDebug("variant == TriggeredVariant.Description.Id, variantAlreadyListed = true");
                        variantAlreadyListed = true;
                    }
                }

                optionIdx++;
            }

            // add the option to not build anything, in last place
            // Length property - 1 for the last array element
            LogDebug("Add the option to not build anything");
            options[options.Length - 1] = new SimGameEventOption
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

            var optionList = new List<SimGameEventOption>(options);
            options = options.Where(option => option != null).ToArray();

            // put the selected variant at the top regardless of part count
            LogDebug("variantAlreadyListed? :" + variantAlreadyListed);
            if (variantAlreadyListed)
            {
                LogDebug(">>> Reordering options");
                LogDebug("Before");
                foreach (var option in options)
                    LogDebug(option.Description.Name);

                var variantModel = Regex.Match(TriggeredVariant.Description.Id, @".+_.+_(.+)").Groups[1].Value;
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
                TriggeredVariant = null;
            }

            // setup the event string based on the situation
            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;

            var eventString = "As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we don't have enough salvage from any single 'Mech to build ourselves a new one, but...\" He pauses dramatically. \"...I could cobble together the salvage from a couple related 'Mechs.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Which one should we build?\"";
            if (mechParts.Count == 1 && !Settings.VariantExceptionsDefs.Contains(highestVariant.Description.Id)) // we have only a single option
                eventString = $"As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage from the [[DM.MechDefs[{highestVariant}],{highestVariant.Description.UIName}]] to put it together.\" He pauses, rubbing his beard. \"But, we could save it to build another variant, later.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Should we build it?\"";
            else if (mechParts.Count == 1 && Settings.VariantExceptionsDefs.Contains(highestVariant.Description.Id)) // We have an excluded mech as our option.
                eventString = $"As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage from the [[DM.MechDefs[{highestVariant}],{highestVariant.Description.UIName}]] to put it together.\" He pauses, absolutely giddy. \"This is a truly rare 'Mech - something I've always dreamed of working on!\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Should we build it?\"";
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

                if (!chassisPieces.ContainsKey(mechDef.Chassis.PrefabIdentifier))
                    chassisPieces[mechDef.Chassis.PrefabIdentifier] = 0;

                //   Logger.Log($"{mechID} has UIName {mechDef.Chassis.PrefabIdentifier}");
            }

            // try to build each chassis
            var prefabId = chassisPieces.Keys.First();

            //var prefabId = prefabs.First();
            LogDebug("prefabId " + prefabId);

            // add chassis pieces that we already have
            var matchingMechDefs = GetAllMatchingVariants(simGame.DataManager, prefabId);
            foreach (var mech in matchingMechDefs)
                LogDebug("matchingMechDef " + mech.Description.Id);
            foreach (var mechDef in matchingMechDefs)
            {
                chassisPieces[prefabId] += GetMechParts(simGame, mechDef);

                //  Logger.Log(mechDef.Description.Id);
                //  Logger.Log(chassisPieces[UIName].ToString());
            }

            // need a generic name for below
            var mechName = matchingMechDefs.First().Description.Name;
            LogDebug($"{prefabId} has {chassisPieces[prefabId]} pieces");
            if (chassisPieces[prefabId] >= defaultMechPartMax)
            {
                // has enough pieces to build a mech, generate popup
                LogDebug($"Generating popup for {prefabId}, adding {mechName} to list");
                try
                {
                    GenerateMechPopup(simGame, prefabId);
                }
                catch (Exception ex)
                {
                    Error(ex);
                }

                // build a list of "Atlas" and "Locust"
                // so multiple popups for the same chassis don't appear
                if (!HasBeenBuilt.Contains(mechName))
                    HasBeenBuilt.Add(mechName);
            }
        }
    }
}