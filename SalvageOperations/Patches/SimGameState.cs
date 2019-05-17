using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.StringInterpolation;
using Harmony;
using Localize;
using UnityEngine;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SalvageOperations.Patches
{
      
    // trigger hotkey
    [HarmonyPatch(typeof(SimGameState), "Update")]
    public static class SimGameState_Update_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            var hotkey = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(Main.Settings.Hotkey);
            if (hotkey)
            {
               // Logger.Log("Hotkey Triggered");
                Main.SalvageFromContract.Clear();
                Main.HasBeenBuilt.Clear();
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                //Main.ShowBuildPopup = true;
                var inventorySalvage = new Dictionary<string, int>( /*Main.Salvage*/);
                var inventory = sim.GetAllInventoryMechDefs();
                foreach (var item in inventory)
                {
                    var inventoryName = item.Description.Id.Replace("chassisdef", "mechdef");
                    inventorySalvage[inventoryName] = 1;
                   // Logger.Log(inventoryName);
                    var itemCount = sim.GetItemCount(inventoryName, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                 //   Logger.Log(itemCount.ToString());
                    if (itemCount == 0 || itemCount == 99)
                        continue;
                    if (!Main.HasBeenBuilt.ContainsKey(item.Description.Name))
                        Main.TryBuildMechs(sim, new Dictionary<string, int> { { inventoryName, 1 } });
                  //  Logger.Log("Post-Build");
                    
                }
            }
        }
    }

    // where the mayhem starts
    [HarmonyPatch(typeof(SimGameState), "AddMechPart")]
    public static class SimGameState_AddMechPart_Patch
    {
        public static bool Prefix(SimGameState __instance, string id)
        {
            // this function replaces the function from SimGameState, prefix return false
            // just add the piece
            if (id != null)
                __instance.AddItemStat(id, "MECHPART", false);

            // we're in the middle of resolving a contract, add the piece to contract
            if (Main.IsResolvingContract)
            {
                if (!Main.SalvageFromContract.ContainsKey(id))
                    Main.SalvageFromContract[id] = 0;

                Main.SalvageFromContract[id]++;
                return false;
            }

            // TODO: what happens when you buy multiple pieces from the store at once and can build for each?
            // not in contract, just try to build with what we have
            if (!__instance.CompanyTags.Contains("SO_Salvaging"))
            {
                Main.TryBuildMechs(__instance, new Dictionary<string, int> {{id, 1}});
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
    public static class SimGameState_ResolveCompleteContract_Patch
    {
        public static void Prefix(SimGameState __instance)
        {
            Main.ContractStart();
            __instance.CompanyTags.Add("SO_Salvaging");
            Main.HasBeenBuilt.Clear();
        }

        public static void Postfix(SimGameState __instance)
        {
            foreach (var mechID in Main.SalvageFromContract.Keys)
            {
                var mechDef = __instance.DataManager.MechDefs.Get(mechID);
                if (!Main.HasBeenBuilt.ContainsKey(mechDef.Description.Name))
                    Main.TryBuildMechs(__instance, new Dictionary<string, int> {{mechID, 1}});
            }

            __instance.CompanyTags.Remove("SO_Salvaging");
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

                __result.Add(new ResultDescriptionEntry(new Text(
                    $"{prefix} {Interpolator.Interpolate(text, gameContext, false)}"), gameContext, stat.name));
            }
        }
    }
}