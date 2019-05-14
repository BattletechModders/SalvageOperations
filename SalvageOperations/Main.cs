using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        public static Dictionary<string, int> SalvageFromOther = new Dictionary<string, int>();

        private static SimGameEventTracker eventTracker = new SimGameEventTracker();

        private static bool _hasInitEventTracker;

        // ENTRY POINT
        public static void Init(string modDir, string settings)
        {
            var harmony = HarmonyInstance.Create("io.github.mpstark.SalvageOperations");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            HBSLog = HBS.Logging.Logger.GetLogger("SalvageOperations");
            Settings = ModSettings.ReadSettings(settings);
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
            return $"Item.{type}.{id}";
        }

        // MEAT
        private static SimGameEventResult[] GetBuildMechEventResult(SimGameState simGame, MechDef mechDef)
        {
            HBSLog.Log($"Generate Event Result for {mechDef.Description.Id}");

            var stats = new List<SimGameStat>();

            // adds the flatpacked mech
            stats.Add(new SimGameStat(GetItemStatID(mechDef.Chassis.Description.Id, "MechDef"), 1));

            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;
            var thisParts = GetMechPieces(simGame, mechDef);
            thisParts = Math.Min(thisParts, defaultMechPartMax);

            // removes the parts from the mech we're building from inventory
            stats.Add(new SimGameStat(GetItemStatID(mechDef.Description.Id, "MECHPART"), -thisParts));

            SalvageFromOther[mechDef.Description.Id] -= thisParts;
            LogDebug($">>> Removed {thisParts}");
            foreach (var kvp in SalvageFromOther)
            {
                LogDebug($"{kvp.Key}: {kvp.Value}");
            }

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
                            SalvageFromOther[variant.Description.Id]--;
                            if (numPartsRemaining <= 0)
                                break;
                        }
                    }

                    if (partsRemoved == 0)
                        break;
                }
            }

            // actually add the stats that will remove the other mech parts
            foreach (var mechID in otherMechParts.Keys)
            {
                stats.Add(new SimGameStat(GetItemStatID(mechID, "MECHPART"), -otherMechParts[mechID]));

                SalvageFromOther[mechID] -= otherMechParts[mechID];
            }

            foreach (var stat in stats)
                HBSLog.Log($"Event Stat {stat.name} {stat.value}");

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

        internal static void GenerateMechPopup(SimGameState simGame, string prefabID)
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
            var options = new SimGameEventOption[Math.Min(3, mechPieces.Count + 1)];
            foreach (var variantKVP in mechPieces.OrderByDescending(key => key.Value))
            {
                var variant = variantKVP.Key;
                HBSLog.Log($"Building event option {optionIdx} for {variant}");

                if (optionIdx > 2)
                {
                    HBSLog.Log("Had more than 3 options, truncating at 3");
                    break;
                }

                var mechDef = simGame.DataManager.MechDefs.Get(variant);

                LogDebug($"mechDef: {mechDef.Description.UIName}");
                options[optionIdx++] = new SimGameEventOption
                {
                    Description = new BaseDescriptionDef(variant, $"Build the {mechDef.Description.UIName} ({mechPieces[variant]} Parts)", variant, ""),
                    RequirementList = null,
                    ResultSets = new[]
                    {
                        new SimGameEventResultSet
                        {
                            Description = new BaseDescriptionDef(variant, variant, $"You tell Yang that you want him to build the [[DM.MechDefs[{variant}],{{DM.MechDefs[{variant}].Description.UIName}}]] and his eyes light up. \"I can't wait to get started.\"\r\n\r\nHe starts to move behind the pile of scrap, then calls out, \"Oh, and don't forget to submit a work order to 'Ready' the 'Mech when you want to get started on the refit.\"", ""),
                            Weight = 100,
                            Results = GetBuildMechEventResult(simGame, mechDef)
                        }
                    }
                };
            }

            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;

            // setup the event string based on the situation
            var eventString = "As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we don't have enough salvage from any single 'Mech to build ourselves a new one, but...\" He pauses dramatically. \"...I could cobble together the salvage from a couple related 'Mechs.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Which one should we build?\"";
            if (mechPieces.Count == 1) // we have only a single option
                eventString = $"As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage from the [[DM.MechDefs[{highestVariant}],{highestVariant.Description.UIName} to put it together.\" He pauses, rubbing his beard. \"But, we could save it to build another variant, later.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Should we build it?\"";
            else if (highest >= defaultMechPartMax) // we have enough salvage to build a mech
                eventString = "As you board, Yang asks for you to meet him in the 'Mech Bay. When you arrive, you find him grinning in front of a load of unidentifiable scrap.\r\n\r\n\"Commander, we've got enough salvage to build a 'Mech out completely, but...\" He pauses dramatically. \"...I could cobble together the salvage from a couple related 'Mechs if you wanted to build something else.\"\r\n\r\n\"What do you think?\" He grins like a kid in a candy shop. \"Which one should we build?\"";

            // get rid of null options that throw NRE on popup
            options = options.Where(option => option != null).ToArray();

            foreach (var option in options)
            {
                LogDebug($"option: {option.Description.Name}");
            }

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
            ;

            if (!_hasInitEventTracker)
            {
                eventTracker.Init(new[] {EventScope.Company}, 0, 0, SimGameEventDef.SimEventType.NORMAL, simGame);
                _hasInitEventTracker = true;
            }

            try
            {
                simGame.InterruptQueue.QueueEventPopup(eventDef, EventScope.Company, eventTracker);
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        public static void TryBuildMechs(SimGameState simGame,
            Dictionary<string, int> mechPieces, string id)
        {
            HBSLog.Log($"TryBuildMechs {mechPieces.Count} mechIDs");
            var defaultMechPartMax = simGame.Constants.Story.DefaultMechPartMax;
            var chassisPieces = new Dictionary<string, int>();

            // don't add the piece unless it's actually new - how TODO
            if (id != null)
            {
                LogDebug("\tAdding part: " + id);
                simGame.AddItemStat(id, "MECHPART", false);
            }

            // loop by number of different mechIDs
            foreach (var part in mechPieces)
            {
                // setup chassis pieces for the pieces that we received
                foreach (var mechID in mechPieces.Keys)
                {
                    var mechDef = simGame.DataManager.MechDefs.Get(mechID);

                    if (!chassisPieces.ContainsKey(mechDef.Chassis.PrefabIdentifier))
                        chassisPieces[mechDef.Chassis.PrefabIdentifier] = 0;
                    HBSLog.Log(
                        $"{mechID} has prefabID {mechDef.Chassis.PrefabIdentifier}");
                }

                // try to build each prefab base
                var prefabIDs = chassisPieces.Keys.ToList();
                foreach (var prefabID in prefabIDs)
                {
                    chassisPieces[prefabID] = 0;
                    // accumulate chassis pieces that we already have
                    var matchingMechDefs = GetAllMatchingVariants(simGame.DataManager, prefabID);

                    //Logger.LogDebug($"\tprefabID: {prefabID}, (variants: {matchingMechDefs.Count()})");
                    foreach (var mechDef in matchingMechDefs)
                    {
                        LogDebug($"{mechDef.Description.Id} GetMechPieces: {GetMechPieces(simGame, mechDef)}");
                        chassisPieces[prefabID] += GetMechPieces(simGame, mechDef);
                    }

                    LogDebug($"\tAll variant parts for {prefabID}: " + chassisPieces[prefabID]);
                    HBSLog.Log($"{prefabID} has {chassisPieces[prefabID]} pieces");

                    if (chassisPieces.Values.Any(x=> x>= defaultMechPartMax))
                    {
                        foreach (var foo in chassisPieces)
                        {
                            LogDebug(foo.Key);
                        }
                    }
                    
                    if (chassisPieces[prefabID] >= defaultMechPartMax)
                    {
                        // has enough pieces to build a mech, generate popup
                        HBSLog.Log($"Generating popup for {prefabID}");
                        LogDebug($"[[[Generating popup for {prefabID}]]]");
                        GenerateMechPopup(simGame, prefabID);
                    }
                }
            }
        }
    }
}