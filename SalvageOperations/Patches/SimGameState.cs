using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.StringInterpolation;
using BattleTech.UI;
using Harmony;
using Localize;
using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SalvageOperations.Patches
{
    // trigger hotkey
    [HarmonyPatch(typeof(SimGameState), "Update")]
    public class SimGameState_Update_Patch
    {
        public static void Postfix()
        {
            var hotkey = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.A);
            if (hotkey)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                Main.ShowBuildPopup = true;
                var inventorySalvage = new Dictionary<string, int>(Main.Salvage);
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

                Main.TryBuildMechs(sim, inventorySalvage, null);
            }
        }
    }

    // where the magic starts
    [HarmonyPatch(typeof(SimGameState), "AddMechPart")]
    public static class SimGameState_AddMechPart_Patch
    {
        public static bool Prefix(SimGameState __instance, string id, SimGameInterruptManager ___interruptQueue)
        {
            if (Main.IsResolvingContract)
            {
                if (!Main.SalvageFromContract.ContainsKey(id))
                    Main.SalvageFromContract[id] = 0;

                Main.SalvageFromContract[id]++;
                return false;
            }

            // buffer the incoming salvage to avoid zombies (Problem One)
            if (!Main.Salvage.ContainsKey(id))
                Main.Salvage.Add(id, 0);
            Main.Salvage[id]++;

            Logger.LogDebug($"--------- {id} ----------\nSALVAGE\n--------");
            foreach (var kvp in Main.Salvage)
            {
                Logger.LogDebug($"{kvp.Key}: {kvp.Value}");
            }

            Main.TryBuildMechs(__instance, Main.Salvage, id);
            return false;
        }
    }

    // prevent the popup until we've set it back to True with mech shift-click on Storage
    [HarmonyPatch(typeof(SimGameInterruptManager), "QueueEventPopup")]
    public class SimGameInterruptManager_QueueEventPopup_Patch
    {
        public static bool Prefix(SimGameInterruptManager __instance, SimGameEventDef evt)
        {
            if (evt.Description.Name == "Salvage Operations")
            {
                return Main.ShowBuildPopup;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
    public static class SimGameState_ResolveCompleteContract_Patch
    {
        public static void Prefix(SimGameState __instance)
        {
            Main.ContractStart();
            __instance.CompanyTags.Add("SO_Salvaging");
        }

        public static void Postfix(SimGameState __instance)
        {
            Main.ShowBuildPopup = true;
            Main.TryBuildMechs(__instance, Main.SalvageFromContract, null);
            Main.ContractEnd();
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