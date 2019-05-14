using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.StringInterpolation;
using BattleTech.UI;
using Harmony;
using Localize;
using static Logger;
using static SalvageOperations.Main;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(SimGameState), "AddMechPart")]
    public static class SimGameState_AddMechPart_Patch
    {
        public static bool Prefix(SimGameState __instance, string id, SimGameInterruptManager ___interruptQueue)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            var mechDef = sim.DataManager.MechDefs
                .Where(def => def.Value.ChassisID == id.Replace("mechdef", "chassisdef"))
                .Select(def => def.Value).FirstOrDefault();
            LogDebug($"Removed {mechDef.Chassis.PrefabIdentifier} from OfferedChassis");

            OfferedChassis.Remove(mechDef.Chassis.PrefabIdentifier);

            if (!SalvageFromOther.ContainsKey(id))
                SalvageFromOther.Add(id, 0);
            SalvageFromOther[id]++;

            LogDebug($"--------- {id} ----------\nSALVAGE\n--------");
            foreach (var kvp in SalvageFromOther)
            {
                LogDebug($"{kvp.Key}: {kvp.Value}");
            }

            TryBuildMechs(__instance, SalvageFromOther, id);
            // this function replaces the function from SimGameState, prefix return false
            // GET THE NUMBER OF VARIANT PIECES USE IT TO LOOP THROUGH AND REMOVE ENOUGH
            //   var mechDef = new MechDef(__instance.DataManager.MechDefs.Get(id), __instance.GenerateSimGameUID(), __instance.Constants.Salvage.EquipMechOnSalvage);
            //   var allVariantMechPieces = Main.GetAllVariantMechPieces(__instance, mechDef);
            //   var defaultMechPartMax = __instance.Constants.Story.DefaultMechPartMax;
            //   // plus 1 because we haven't added the part yet
            //
            //   // this part will make a variant match possible?
            //   if (allVariantMechPieces + 1 >= defaultMechPartMax)
            //   {
            //       // we'll be able to construct a mech so add the last piece and go ahead
            //       LogDebug($"Mech {mechDef.Name} (parts: {allVariantMechPieces + 1})");
            //
            //       // if this is going to be more than n - fucking adjust
            //       if (allVariantMechPieces + 1 > defaultMechPartMax)
            //       {
            //           var partsRemoved = 0;
            //           var mechDefs = Main.GetAllMatchingVariants(__instance.DataManager, mechDef);
            //           while (partsRemoved != defaultMechPartMax)
            //           {
            //               LogDebug("\tpartsRemoved: " + partsRemoved);
            //               // go through all variant pieces and remove to satisfy n
            //               foreach (var mech in mechDefs)
            //               {
            //                   // we have this many pieces of this variant, clamped
            //                   var variantParts = __instance.GetItemCount(mech.ChassisID.Replace("chassisdef", "mechdef"), "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
            //                   var partCount = Math.Min(defaultMechPartMax, variantParts);
            //                   LogDebug($"\t{mech.ChassisID} ({partCount})");
            //
            //                   // only loop to n (MaxParts)
            //                   while (partCount != 0)
            //                   {
            //                       try
            //                       {
            //                           LogDebug($"\t\t-");
            //                           var removeItemStat = AccessTools.Method(typeof(SimGameState), "RemoveItemStat", new[] {typeof(string), typeof(string), typeof(bool)});
            //                           removeItemStat.Invoke(__instance, new object[] {mech.ChassisID, "MECHPART", false});
            //                           partsRemoved++;
            //                           partCount--;
            //                       }
            //                       catch (Exception ex)
            //                       {
            //                           Error(ex);
            //                       }
            //                   }
            //
            //                   // escape foreach before moving to next mech incorrectly?
            //                   if (partsRemoved == defaultMechPartMax) break;
            //               }
            //           }
            //
            //           //__instance.AddItemStat(id, "MECHPART", false);
            //
            //           LogDebug("Done removing variant parts");
            //           // make shit pop up and stop things (does nothing!)  find another example!
            //           //     ___interruptQueue.DisplayIfAvailable();
            //           //     __instance.MessageCenter.PublishMessage(new SimGameMechAddedMessage(mechDef, 0, false));
            //
            //           //var stats = new List<SimGameStat>();
            //           LogDebug($"Flat-packing mech {mechDef.Chassis.Description.Id}");
            //           //__instance.AddMech(0, mechDefs.First(), false, false, true);
            //
            //           // flat-pack a mech
            //           var flatPack = AccessTools.Method(typeof(SimGameState), "AddItemStat", new[] {typeof(string), typeof(string), typeof(bool)});
            //           var itemId = mechDef.Chassis.Description.Id;
            //           flatPack.Invoke(__instance, new object[] {itemId, "MechDef", false});
            //
            //           // TODO completely unpack and repack mechs to get ride of duplicate icons?
            //
            //           try
            //           {
            //               //stats.Add(new SimGameStat(itemId, 1));
            //               //LogDebug($"itemId: {itemId} (id: {id})");
            //           }
            //           catch (Exception ex)
            //           {
            //               Error(ex);
            //           }
            //
            //           //var eventResults = new[]
            //           //{
            //           //    new SimGameEventResult
            //           //    {
            //           //        Stats = stats.ToArray(),
            //           //        Scope = EventScope.Company,
            //           //        Actions = new SimGameResultAction[0],
            //           //        AddedTags = new HBS.Collections.TagSet(),
            //           //        RemovedTags = new HBS.Collections.TagSet(),
            //           //        ForceEvents = new SimGameForcedEvent[0],
            //           //        Requirements = null,
            //           //        ResultDuration = 0,
            //           //        TemporaryResult = false
            //           //    }
            //           //};
            //           //
            //           //foreach (var item in stats)
            //           //{
            //           //    LogDebug(item.name);
            //           //}
            //
            //           //LogDebug("BuildSimGameResults");
            //           //__instance.BuildSimGameResults(eventResults, __instance.Context);
            //
            //           // we're in the middle of resolving a contract, add the piece to contract
            //           if (Main.IsResolvingContract)
            //           {
            //               if (!Main.SalvageFromContract.ContainsKey(id))
            //                   Main.SalvageFromContract[id] = 0;
            //
            //               Main.SalvageFromContract[id]++;
            //               return false;
            //           }
            //
            //           // TODO: what happens when you buy multiple pieces from the store at once and can build for each?
            //           // not in contract, just try to build with what we have
            //           Main.TryBuildMechs(__instance, new Dictionary<string, int> {{id, 1}});
            //       }
            //   }
            //   else
            //   {
            //       __instance.AddItemStat(id, "MECHPART", false);
            //   }

            return false;
        }
    }
    
    [HarmonyPatch(typeof(SimGameInterruptManager), "QueueEventPopup")]
    public class PatchPopup
    {
        public static bool Prefix(SimGameInterruptManager __instance, SimGameEventDef evt)
        {
            if (evt.Description.Name == "Salvage Operations")
            {
                return ShowBuildPopup;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "OnEventDismissed")]
    public class TestPatch
    {
        public static void OnEventDismissed(SimGameInterruptManager.EventPopupEntry entry)
        {
            LogDebug("Event dismissed");
            ShowBuildPopup = true;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
    public static class SimGameState_ResolveCompleteContract_Patch
    {
        public static void Prefix()
        {
            ContractStart();
        }

        public static void Postfix(SimGameState __instance)
        {
            TryBuildMechs(__instance, SalvageFromContract, null);
            ContractEnd();
        }
    }

    [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
    public class SimGameState_Rehydrate_Patch
    {
        public static void Postfix()
        {
            SalvageFromOther.Clear();
            ShowBuildPopup = true;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "BuildSimGameStatsResults")]
    public static class SimGameState_BuildSimGameStatsResults_Patch
    {
        public static void Postfix(List<ResultDescriptionEntry> __result, SimGameStat[] stats, GameContext context, string prefix)
        {
            if (stats.All(stat => !stat.name.StartsWith("Item.MECHPART.") && !stat.name.StartsWith("Item.MechDef.")))
                return;

            // remove blank result descriptions
            var removeResultDescription = new List<ResultDescriptionEntry>();
            foreach (var descriptionEntry in __result)
            {
                if (descriptionEntry.Text.ToString(false).Contains("[[DM.SimGameStatDescDefs[], ]]"))
                    removeResultDescription.Add(descriptionEntry);
            }

            foreach (var entry in removeResultDescription)
                __result.Remove(entry);

            // add "real" descriptions for MECHPARTs or MechDefs
            var gameContext = new GameContext(context);
            foreach (var stat in stats)
            {
                if (!stat.name.StartsWith("Item.MECHPART.") && !stat.name.StartsWith("Item.MechDef."))
                    continue;
                var text = "";
                var split = stat.name.Split('.');
                var type = split[1];
                var mechID = split[2];
                var num = int.Parse(stat.value);
                switch (type)
                {
                    case "MechDef":
                        text = $"Added [[DM.ChassisDefs[{mechID}],{{DM.ChassisDefs[{mechID}].Description.UIName}}]] to 'Mech storage";
                        break;
                    case "MECHPART":
                        if (num > 0)
                            text = $"Added {num} [[DM.MechDefs[{mechID}],{{DM.MechDefs[{mechID}].Description.UIName}}]] Parts";
                        else
                            text = $"Removed {num * -1} [[DM.MechDefs[{mechID}],{{DM.MechDefs[{mechID}].Description.UIName}}]] Parts";
                        break;
                }

                __result.Add(new ResultDescriptionEntry(new Text($"{prefix} {Interpolator.Interpolate(text, gameContext, false)}"), gameContext, stat.name));
            }
        }
    }
}