using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Save;
using BattleTech.StringInterpolation;
using BattleTech.UI;
using Harmony;
using InControl;
using Localize;
using UnityEngine;
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

    [HarmonyPatch(typeof(SimGameState), "Update")]
    public class SimGameStateUpdate_Patch
    {
        public static void Postfix()
        {
            var hotkeyY = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.Y);
            if (!hotkeyY) return;
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            ShowBuildPopup = true;
            var inventorySalvage = new Dictionary<string, int>(SalvageFromOther);
            var inventory = sim.GetAllInventoryMechDefs();
            foreach (var item in inventory)
            {
                var id = item.Description.Id.Replace("chassisdef", "mechdef");
                var itemCount = sim.GetItemCount(id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                if (!inventorySalvage.ContainsKey(id))
                    inventorySalvage.Add(id, itemCount);
                else
                    inventorySalvage[id] += itemCount;
                LogDebug($">>> Debug: {id} {itemCount} current parts");
            }

            //                if (bar.Description.Id
            //LogDebug($">>> Debug: {bar.Description.Id}");
            //LogDebug($">>> Debug:  global at {SalvageFromOther.Count}");
            //LogDebug($">>> Debug:  inventorySalvage at {inventorySalvage.Count}");
            //var newSalvageDictionary = new Dictionary<string, int>();

            //foreach (var kvp in SalvageFromOther)
            //{
            //    if (inventorySalvage.Keys.Contains(kvp.Key))
            //    {
            //        // sum inventory with global
            //        LogDebug("Matching keys, summing " + kvp.Key + " (" + kvp.Value + ")");
            //
            //        try
            //        {
            //            newSalvageDictionary.Add(kvp.Key, kvp.Value + inventorySalvage[kvp.Key]);
            //        }
            //        catch (Exception ex)
            //        {
            //            Error(ex);
            //        }
            //    }
            //    else
            //    {
            //        try
            //        {
            //            newSalvageDictionary.Add(kvp.Key, kvp.Value);
            //        }
            //        catch (Exception ex)
            //        {
            //            Error(ex);
            //        }
            //    }
            //}

            //foreach (var kvp in inventorySalvage)
            //{
            //    if (SalvageFromOther.Keys.Contains(kvp.Key))
            //    {
            //        LogDebug($"Exists in global too ({kvp.Key}) - ({kvp.Value}) parts");
            //        LogDebug($"Global has {SalvageFromOther[kvp.Key]} parts");
            //    }
            //
            //    if (!newSalvageDictionary.ContainsKey(kvp.Key))
            //    {
            //        try
            //        {
            //            newSalvageDictionary.Add(kvp.Key, 0);
            //            newSalvageDictionary.Add(kvp.Key, kvp.Value + SalvageFromContract[kvp.Key]);
            //        }
            //        catch (Exception ex)
            //        {
            //            Error(ex);
            //        }
            //    }
            //
            //    else
            //    {
            //        LogDebug("Doesn't exist, adding " + kvp.Key);
            //        try
            //        {
            //            newSalvageDictionary.Add(kvp.Key, kvp.Value);
            //        }
            //        catch (Exception ex)
            //        {
            //            Error(ex);
            //        }
            //    }
            //}

            //try
            //{
            //    inventorySalvage = inventorySalvage.Concat(SalvageFromOther).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            //}
            //catch (Exception ex)
            //{
            //    Error(ex);
            //}

            LogDebug($">>> Debug:  inventorySalvage at {inventorySalvage.Count}");
            TryBuildMechs(sim, inventorySalvage, null);
        }
    }

//TODO
//[HarmonyPatch(typeof(MechBayMechStorageWidget), "OnButtonClicked")]
//public class MechBayMechStorageWidget_OnButtonClicked_Patch
//{
//    public static bool Prefix()
//    {
//        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
//        {
//            var sim = UnityGameInstance.BattleTechGame.Simulation;
//            ShowBuildPopup = true;
//            // add existing mechparts to SalvageFromOther
//            var mechInventory = sim.GetAllInventoryMechDefs();
//            foreach (var foo in mechInventory)
//            {
//                LogDebug($">>> Debug: {foo.PrefabIdentifier}");
//            }
//            TryBuildMechs(UnityGameInstance.BattleTechGame.Simulation, SalvageFromOther, null);
//            return false;
//        }
//        return true;
//    }
//}

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

    [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
    public static class SimGameState_ResolveCompleteContract_Patch
    {
        public static void Prefix()
        {
            ContractStart();
        }

        public static void Postfix(SimGameState __instance)
        {
            ShowBuildPopup = true;
            TryBuildMechs(__instance, SalvageFromContract, null);
            ContractEnd();
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