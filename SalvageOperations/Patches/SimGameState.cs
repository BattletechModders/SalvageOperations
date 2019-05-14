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
            // buffer the incoming salvage to avoid zombies
            if (!SalvageFromOther.ContainsKey(id))
                SalvageFromOther.Add(id, 0);
            SalvageFromOther[id]++;

            LogDebug($"--------- {id} ----------\nSALVAGE\n--------");
            foreach (var kvp in SalvageFromOther)
            {
                LogDebug($"{kvp.Key}: {kvp.Value}");
            }

            TryBuildMechs(__instance, SalvageFromOther, id);
            return false;
        }
    }

    // hotkey to force checking for assembly options, necessary because Problem One
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
            }
            
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


    // prevent the popup until we've set it back to True with SimGameState hotkey
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