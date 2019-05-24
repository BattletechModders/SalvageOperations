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

// ReSharper disable InconsistentNaming

namespace SalvageOperations
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class Main
    {
        internal static ILog HBSLog;
        internal static ModSettings Settings;
        private static string modDir;

        internal static bool IsResolvingContract { get; private set; }

        internal static Dictionary<string, int> SalvageFromContract = new Dictionary<string, int>();
        internal static readonly List<string> HasBeenBuilt = new List<string>();
        internal static bool Salvaging;
        internal static MechDef TriggeredVariant;
        private static readonly SimGameEventTracker eventTracker = new SimGameEventTracker();
        private static bool _hasInitEventTracker;

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
            int mechParts = 0;

            var variants = GetAllMatchingVariants(simGame.DataManager, mechDef);
            foreach (var variant in variants)
                mechParts += GetMechParts(simGame, variant);

            return mechParts;
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

            return variants;
        }

        internal static void RemoveSOTags(SimGameState __instance, string id)
        {
            // capture trailing the number on a string like SO_Built_mechdef_locust_LCT-1E_1
            var tags = __instance.CompanyTags.Where(tag => tag.Contains($"SO_Built_{id}"));

            // find the highest numbered tag for this chassis and remove it
            // this is intended to counter multiple mechs with the same chassis causing issues
            var highest = 0;
            foreach (var tag in tags)
            {
                // TODO maybe use a string split array for performance/simplicity?
                var match = Regex.Match(tag, @"SO_Built_mechDef_.+-.+_(\d+)$", RegexOptions.IgnoreCase);
                var number = int.Parse(match.Groups[1].ToString());
                highest = number > highest ? number : highest;
            }

            if (__instance.CompanyTags.Contains($"SO_Built_{id}_{highest}"))
                __instance.CompanyTags.Remove($"SO_Built_{id}_{highest}");
        }

        internal static void AddSOTags(SimGameState __instance, MechDef def)
        {
            var id = def.Description.Id;
            var tags = __instance.CompanyTags.Where(tag => tag.Contains($"SO_Built_{id}"));

            // find the highest numbered tag for this chassis, increment and tag
            // this is intended to counter multiple mechs with the same chassis causing issues
            var highest = 0;
            foreach (var tag in tags)
            {
                // TODO maybe use a string split array for performance/simplicity?
                // not sure if this will throw on tag find failures, either

                var match = Regex.Match(tag, @"SO_Built_mechDef_.+-.+_(\d+)$", RegexOptions.IgnoreCase);
                var number = int.Parse(match.Groups[1].ToString());
                highest = number > highest ? number : highest;
            }

            // make it a 1 if it's still a 0
            highest = highest == 0 ? 1 : highest + 1;
            __instance.CompanyTags.Add($"SO_Built_{def.Description.Id}_{highest}");
        }

        private static List<MechDef> ExcludeVariants(List<MechDef> variants)
        {
            // if it's an excluded variant it can only build with itself
            if (TriggeredVariant != null &&
                Settings.ExcludeVariantsById &&
                Settings.ExcludedMechIds.Any(id => id == TriggeredVariant.Description.Id))
            {
                LogDebug(">>> Selected excluded mech id: " + TriggeredVariant.Description.Id);
                return new List<MechDef>() {TriggeredVariant};
            }

            if (TriggeredVariant != null &&
                Settings.ExcludeVariantsByTag &&
                Settings.ExcludedMechTags.Any(tag => TriggeredVariant.MechTags.Any(x => x == tag)))
            {
                LogDebug(">>> Selected excluded mech tag: " + TriggeredVariant.Description.Id);
                return new List<MechDef>() {TriggeredVariant};
            }

            // remove all variants appearing in the exclusion list
            var allowedVariants = new List<MechDef>(variants);
            foreach (var variant in variants.Where(x => x != null))
            {
                if (Settings.ExcludeVariantsById &&
                    Settings.ExcludedMechIds.Any(id => id == variant.Description.Id) ||
                    Settings.ExcludeVariantsByTag &&
                    Settings.ExcludedMechTags.Any(tag => variant.MechTags.Any(t => t == tag)))
                {
                    LogDebug($">>> Removing variant {variant.Description.Id}");
                    allowedVariants.Remove(variant);
                }
            }

            return allowedVariants;
        }

        private static List<MechDef> GetAllMatchingVariants(DataManager dataManager, MechDef mechDef)
        {
            return GetAllMatchingVariants(dataManager, mechDef.Chassis.PrefabIdentifier);
        }

        private static string GetItemStatID(string id, string type) => $"Item.{type}.{id}";

        internal static void SimulateContractSalvage()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            foreach (var mechID in SalvageFromContract.Keys)
            {
                var mechDef = sim.DataManager.MechDefs.Get(mechID);
                LogDebug($"Salvage mechID: {mechID} ({mechDef.Description.Name})");

                if (!HasBeenBuilt.Contains(mechDef.Description.Name))
                {
                    LogDebug("Hasn't been built");
                    TryBuildMechs(new Dictionary<string, int> {{mechID, 1}});
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
            var inventoryChassis = new Dictionary<string, int>();

            var inventory = sim.GetAllInventoryMechDefs();
            foreach (var chassis in inventory)
            {
                var itemCount = 0;
                var mechId = chassis.Description.Id.Replace("chassisdef", "mechdef");
                if (!inventoryChassis.ContainsKey(mechId))
                {
                    itemCount = sim.GetItemCount(mechId, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                    inventoryChassis.Add(mechId, itemCount);
                    LogDebug($"New key: {mechId} ({itemCount})");
                }
                else
                {
                    // I don't think this is ever reached
                    itemCount = sim.GetItemCount(mechId, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                    inventoryChassis[mechId] += itemCount;
                    LogDebug($"New value for {mechId} ({itemCount})");
                }
            }

            LogDebug($"Chassis in inventory ({inventoryChassis.Count})");
            SalvageFromContract = inventoryChassis;
            HasBeenBuilt.Clear();
            LogDebug("SimulateContractSalvage");
            SimulateContractSalvage();
        }

        // MEAT
        private static SimGameEventResult[] GetBuildMechEventResult(SimGameState simGame, MechDef mechDef)
        {
            var stats = new List<SimGameStat>();

            // adds the flatpacked mech
            stats.Add(new SimGameStat(GetItemStatID(mechDef.ChassisID, "MechDef"), 1));
            mechDef.Chassis.ChassisTags.Add("SO_Built");
            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;
            var mechPartCount = GetMechParts(simGame, mechDef);
            mechPartCount = Math.Min(mechPartCount, defaultMechPartMax);

            // removes the parts from the mech we're building from inventory
            stats.Add(new SimGameStat(GetItemStatID(mechDef.Description.Id, "MECHPART"), -mechPartCount));

            // TODO check if this is creating multiple tags per event/whatever/Problem One
            // SO-chassisdef_locust_LCT-1M_1
            // SO-chassisdef_locust_LCT-1S_2
            // SO-chassisdef_locust_LCT-3V_1

            // Record how many parts were used to assemble the mech.
            string tagName = $"SO_PartsCounter_{mechDef.ChassisID}_{mechPartCount}";
            int maxParts = 0;
            string removeTagName = "Temp";

            int i = 1;
            do

            {
                var tempTagName = $"SO_PartsCounter_{mechDef.ChassisID}_{i}";
                if (simGame.CompanyTags.Contains(tempTagName))
                {
                    maxParts = i;
                    removeTagName = tempTagName;
                }

                i++;
            } while (i < defaultMechPartMax);

            if (mechPartCount > maxParts)
            {
                if (simGame.CompanyTags.Contains(removeTagName))
                    simGame.CompanyTags.Remove(removeTagName);

                simGame.CompanyTags.Add(tagName);
            }

            // there could still be parts remaining that we need to delete from other variants
            var otherMechParts = new Dictionary<string, int>();

            var numPartsRemaining = simGame.Constants.Story.DefaultMechPartMax - mechPartCount;
            if (numPartsRemaining > 0)
            {
                // delete 1 from each variant until we've gotten all the parts that we need deleted
                var matchingVariants = GetAllMatchingVariants(simGame.DataManager, mechDef);
                matchingVariants = ExcludeVariants(matchingVariants);
                int partsRemoved = 0;
                while (numPartsRemaining > 0)
                {
                    // want to log this down below
                    var tempVariant = new MechDef();
                    foreach (var variant in matchingVariants)
                    {
                        tempVariant = variant;
                        var parts = GetMechParts(simGame, variant);
                        LogDebug($"Variant {variant.Description.Id}, {parts} parts");
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
                    {
                        LogDebug($"ABORT Variant {tempVariant.Description.Id}, 0 parts");
                        break;
                    }
                }
            }

            // actually add the stats that will remove the other mech parts
            foreach (var mechID in otherMechParts.Keys)
            {
                LogDebug($"Adding MECHPART removal stat {mechID}, {otherMechParts[mechID]} parts");
                try
                {
                    stats.Add(new SimGameStat(GetItemStatID(mechID, "MECHPART"), -otherMechParts[mechID]));
                }
                catch (Exception ex)
                {
                    Error(ex);
                }
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
            var variantParts = new Dictionary<string, int>();

            var variants = GetAllMatchingVariants(simGame.DataManager, prefabId);
            if (Settings.ExcludeVariantsById)
                variants = ExcludeVariants(variants);

            MechDef highestVariant = null;

            int mostParts = 0;
            foreach (var variant in variants)
            {
                var partsOfThisVariant = GetMechParts(simGame, variant);
                LogDebug($"Using for assembly: {variant.Description.Id}: {partsOfThisVariant} parts");

                if (partsOfThisVariant <= 0)
                    continue;

                if (variantParts.ContainsKey(variant.Description.Id))
                    variantParts[variant.Description.Id] = partsOfThisVariant;
                else
                    variantParts.Add(variant.Description.Id, partsOfThisVariant);

                if (variantParts[variant.Description.Id] > mostParts)
                {
                    highestVariant = variant;
                    mostParts = variantParts[variant.Description.Id];
                }

                HBSLog.Log($"{variant.Description.Id} has {variantParts[variant.Description.Id]} parts, highest {mostParts}");
            }

            if (TriggeredVariant != null && !variantParts.ContainsKey(TriggeredVariant.Description.Id))
            {
                LogDebug("Add back triggered");
                variantParts.Add(TriggeredVariant.Description.Id, GetMechParts(simGame, TriggeredVariant));
            }

            // if there are insufficient parts after exclusions
            if (highestVariant == null ||
                variantParts.Values.Sum() < UnityGameInstance.BattleTechGame.Simulation.Constants.Story.DefaultMechPartMax)
            {
                LogDebug("Insufficient parts to complete build");
                return;
            }

            // force our triggered variant to #1
            const int artificialInflation = 1_000_000;
            if (TriggeredVariant != null && variantParts.ContainsKey(TriggeredVariant.Description.Id))
                variantParts[TriggeredVariant.Description.Id] += artificialInflation;

            // build the result set
            int optionIdx = 0;

            var options = new SimGameEventOption[Math.Min(4, variantParts.Count + 1)];
            foreach (var variantKVP in variantParts.OrderByDescending(key => key.Value))
            {
                var variant = variantKVP.Key;
                var mechDef = simGame.DataManager.MechDefs.Get(variant);

                if (optionIdx > 2)
                {
                    LogDebug("Had more than 3 options, truncating at 3");
                    HBSLog.Log("Had more than 3 options, truncating at 3");
                    break;
                }

                // remove artificialInflation so it displays nicely (and doesn't give 1 miiiiillion mech parts)
                LogDebug($"Building event option {optionIdx} for {variant}");
                if (TriggeredVariant != null && variant == TriggeredVariant.Description.Id)
                {
                    variantParts[variant] -= artificialInflation;
                }

                options[optionIdx] = new SimGameEventOption
                {
                    Description = new BaseDescriptionDef(variant, $"Build the {mechDef.Description.UIName} ({variantParts[variant]} Parts)", variant, ""),
                    RequirementList = null,
                    ResultSets = new[]
                    {
                        new SimGameEventResultSet
                        {
                            Description = new BaseDescriptionDef(variant, variant, $"You tell Yang that you want him to build the [[DM.MechDefs[{variant}],{{DM.MechDefs[{variant}].Description.UIName}}]] and his eyes light up. \"I can't wait to get started.\"\r\n\r\nHe starts to move behind the pile of scrap, then calls out, \"Oh, and don't forget to submit a work order to 'Ready' the 'Mech when you want to get started on the refit. Remember, the less parts of this variant we use, the longer it will take to get to full working order.\"", ""),
                            Weight = 100,
                            Results = GetBuildMechEventResult(simGame, mechDef)
                        }
                    }
                };

                optionIdx++;
            }

            // add the option to not build anything, in last place
            // Length property - 1 for the last array element
            options[options.Length - 1] = new SimGameEventOption
            {
                Description = new BaseDescriptionDef("BuildNothing", "Tell Yang not to build anything right now.", "BuildNothing", ""),
                RequirementList = null,
                ResultSets = new[]
                {
                    new SimGameEventResultSet
                    {
                        Description = new BaseDescriptionDef("BuildNothing", "BuildNothing", "Yang looks disappointed for a moment, then grins and shrugs, \"Saving these parts up makes sense, I guess, never know when they might come in handy later on.\"", ""),
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
            TriggeredVariant = null;
            LogDebug("OPTIONS\n=======");
            options.Where(option => option != null).Do(x => LogDebug(Regex.Match(x.ResultSets[0].Description.Details, @"mechdef_.+_(.+)]\.").Groups[1].ToString()));

            // setup the event string based on the situation
            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;

            var eventString = "As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we don't have enough salvage from any single 'Mech to build ourselves a new one, but...\" He pauses dramatically. \"...I could cobble together the salvage from a couple related 'Mechs.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Which one should we build?\"";
            if (variantParts.Count == 1 && !Settings.ExcludedMechIds.Contains(highestVariant.Description.Id)) // we have only a single option
                eventString = $"As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage from the [[DM.MechDefs[{highestVariant}],{highestVariant.Description.UIName}]] to put it together.\" He pauses, rubbing his beard. \"But, we could save it to build another variant, later.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Should we build it?\"";
            else if (variantParts.Count == 1 && Settings.ExcludedMechIds.Contains(highestVariant.Description.Id)) // We have an excluded mech as our option.
                eventString = $"As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage from the [[DM.MechDefs[{highestVariant}],{highestVariant.Description.UIName}]] to put it together.\" He pauses, absolutely giddy. \"This is a truly rare 'Mech - something I've always dreamed of working on!\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Should we build it?\"";
            else if (mostParts >= defaultMechPartMax) // we have enough salvage to build a mech
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
                options.Where(option => option != null).ToArray(), 1);
            if (!_hasInitEventTracker)
            {
                eventTracker.Init(new[] {EventScope.Company}, 0, 0, SimGameEventDef.SimEventType.NORMAL, simGame);
                _hasInitEventTracker = true;
            }

            simGame.InterruptQueue.QueueEventPopup(eventDef, EventScope.Company, eventTracker);
        }

        public static void TryBuildMechs(Dictionary<string, int> mechParts)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var defaultMechPartMax = sim.Constants.Story.DefaultMechPartMax;

            var chassisParts = new Dictionary<string, int>();

            // setup chassis parts for the parts that we received
            foreach (var mechID in mechParts.Keys)
            {
                var mechDef = sim.DataManager.MechDefs.Get(mechID);

                if (!chassisParts.ContainsKey(mechDef.Chassis.PrefabIdentifier))
                    chassisParts[mechDef.Chassis.PrefabIdentifier] = 0;
            }

            // try to build each chassis
            var prefabId = chassisParts.Keys.First();
            LogDebug("prefabId " + prefabId);

            // add chassis parts that we already have
            var matchingMechDefs = GetAllMatchingVariants(sim.DataManager, prefabId);
            foreach (var mechDef in matchingMechDefs)
            {
                chassisParts[prefabId] += GetMechParts(sim, mechDef);
            }

            // need a generic name for below
            var mechName = matchingMechDefs.First().Description.Name;
            LogDebug($"{prefabId} has {chassisParts[prefabId]} pieces");
            if (chassisParts[prefabId] >= defaultMechPartMax)
            {
                // has enough parts to build a mech, generate popup
                LogDebug($"Generating popup for {prefabId}, adding {mechName} to list");
                try
                {
                    GenerateMechPopup(sim, prefabId);
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