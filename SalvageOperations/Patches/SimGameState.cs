using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BattleTech;
using BattleTech.StringInterpolation;
using Harmony;
using Localize;
using UnityEngine;
using static Logger;
using Random = System.Random;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(SimGameState), "ScrapActiveMech")]
    public static class SimGameState_ScrapActiveMech_Patch
    {
        public static void Prefix() => LogDebug("ScrapActiveMech prefix");

        public static void Postfix(SimGameState __instance, MechDef def)
        {
            // live mech so definitely going to lose the tag
            LogDebug("ScrapActiveMech RemoveSOTags " + def.Description.Id);
            Main.RemoveSOTags(__instance, def.Description.Id);
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ScrapInactiveMech")]
    public static class SimGameState_ScrapInactiveMech_Patch
    {
        public static void Postfix(SimGameState __instance, string id)
        {
            // tags being removed incorrectly here is a big problems
            // it fires when readying a mech
            // it should only remove a tag when the inactive chassis
            // is not mistaken for a live mech.  I think we can only count it..
            id = id.Replace("chassisdef", "mechdef");
            var chassisTags = __instance.CompanyTags.Count(tag => tag.Contains($"SO_Built_{id}"));
            if (chassisTags > 0)
            {
                LogDebug($"ScrapInactiveMech chassisTags {chassisTags}");
                // what's the highest tag number
                var tags = __instance.CompanyTags.Where(tag => tag.Contains($"SO_Built_{id}"));
                var highest = 0;
                foreach (var tag in tags)
                {
                    var match = Regex.Match(tag, @"SO_Built_mechDef_.+-.+_(\d+)$", RegexOptions.IgnoreCase);
                    var number = int.Parse(match.Groups[1].ToString());
                    highest = number > highest ? number : highest;
                }

                // so if we have a higher tag number than mechs, it's a real inactive mech, untag
                var numberOfTags = __instance.CompanyTags.Count(tag => tag.Contains($"SO_Built_{id}"));

                // count the number of active mechs appearing in the tags
                // TODO does this have to include inactive mechs?
                var numberOfMechs = __instance.ActiveMechs.Count(def => tags.Any(x => x.Contains($"SO_Built_{def.Value.Description.Id}")));
                LogDebug($"numMechs {numberOfMechs} numberOfTags {numberOfTags}");
                if (numberOfTags < numberOfMechs)
                {
                    LogDebug("ScrapInactiveMech RemoveSOTags " + id);
                    Main.RemoveSOTags(__instance, id);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Shop), "SellInventoryItem")]
    [HarmonyPatch(new Type[] {typeof(ShopDefItem)})]
    public class Shop_SellInventoryItem_Patch
    {
        public static void Postfix(Shop __instance, ShopDefItem item)
        {
            LogDebug("Shop sell");

            // see if we have a tag for the mech being sold
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (item.Type == ShopItemType.Mech)
            {
                if (sim.CompanyTags.Any(tag => tag.Contains(item.ID.Replace("chassisdef", "mechdef"))))
                {
                    LogDebug("SellInventoryItem RemoveSOTags");
                    Main.RemoveSOTags(sim, item.ID.Replace("chassisdef", "mechdef"));
                }
            }
        }
    }

    // assemble mechs in variable condition
    [HarmonyPatch(typeof(SimGameState), "CompleteWorkOrder")]
    public static class SimGameState_CompleteWorkOrder_Patch
    {
        private static Random rng = new Random();

        public static void Postfix(SimGameState __instance, WorkOrderEntry entry)
        {
            if (entry.Type != WorkOrderType.MechLabReadyMech) return;
            var workOrder = entry as WorkOrderEntry_MechLab;
            // have to find the GUID of the real mech
            var mechDef = __instance.ActiveMechs.Values.First(value => value.GUID == workOrder.MechID);

            // preventing this from re-running when it's been done once
            // if there are existing tags for this chassis...
            var chassisTags = __instance.CompanyTags.Count(tag => tag.Contains(mechDef.Description.Id));

            // minus 1 because this chassis was just added to ActiveMechs
            var numMechs = Math.Max(0, __instance.ActiveMechs.Count(def => __instance.CompanyTags.Any(tag => tag.Contains(def.Value.Description.Id))) - 1);

            // only add a tag if this mech is untagged... which we guess at by counting?!
            if (numMechs >= chassisTags)
            {
                LogDebug("WorkOrderEntry AddSOTags");
                Main.AddSOTags(__instance, mechDef);
            }

            LogDebug($"numMechs {numMechs} tags {chassisTags}");
            if (chassisTags <= numMechs)
            {
                // optionally damage the mech structure
                if (Main.Settings.StructureDamageLimit > 0)
                {
                    var limbs = new List<LocationLoadoutDef>
                    {
                        mechDef.LeftArm,
                        mechDef.RightArm,
                        mechDef.LeftLeg,
                        mechDef.RightLeg,
                        mechDef.LeftTorso,
                        mechDef.RightTorso,
                        mechDef.CenterTorso,
                        mechDef.Head
                    };

                    limbs.Do(x => x.CurrentInternalStructure *= Math.Max((float) rng.NextDouble(), Main.Settings.StructureDamageLimit));
                }

                // add the default inventory for the mech and damage the components optionally
                Traverse.Create(mechDef).Field("inventory").SetValue(__instance.DataManager.MechDefs.Get(mechDef.Description.Id).Inventory);
                if (Main.Settings.DestroyedChance <= 0) return;
                foreach (var component in mechDef.Inventory)
                {
                    if (rng.NextDouble() <= Main.Settings.DestroyedChance)
                    {
                        if (rng.NextDouble() <= Main.Settings.NonFunctionalChance)
                        {
                            component.DamageLevel = ComponentDamageLevel.NonFunctional;
                            continue;
                        }

                        component.DamageLevel = ComponentDamageLevel.Destroyed;
                        continue;
                    }

                    component.DamageLevel = ComponentDamageLevel.Functional;
                }
            }
        }
    }

    // trigger hotkey
    [HarmonyPatch(typeof(SimGameState), "Update")]
    public static class SimGameState_Update_Patch
    {
        public static void Postfix(SimGameState __instance)
        {
            var Sim = UnityGameInstance.BattleTechGame.Simulation;
            var hotkey = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(Main.Settings.Hotkey);
            if (hotkey)
                Main.GlobalBuild();
            
            
            var hotkeyJ = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.F12);
            if (hotkeyJ)
            {
                if (Sim != null)
                {
                    var sotags = Sim.CompanyTags.Where(tag => tag.Contains("SO-") || tag.Contains("SO_"));
                    sotags.Do(tag => Sim.CompanyTags.Remove(tag));
                }
            }

            var hotkeyK = (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.K);
            if (hotkeyK)
            {
                if (Sim != null)
                {
                    Sim.CompanyTags.Where(tag => tag.Contains("SO-") || tag.Contains("SO_")).Do(LogDebug);
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

            if (!Main.Salvaging)
                Main.TryBuildMechs(new Dictionary<string, int> {{id, 1}});

            return false;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
    public static class SimGameState_ResolveCompleteContract_Patch
    {
        public static void Prefix(SimGameState __instance)
        {
            Main.ContractStart();
            Main.Salvaging = true;
            Main.BuiltMechNames.Clear();
        }

        public static void Postfix(SimGameState __instance)
        {
            foreach (var mechID in Main.SalvageFromContract.Keys)
            {
                var mechDef = __instance.DataManager.MechDefs.Get(mechID);
                if (!Main.BuiltMechNames.Contains(mechDef.Description.Name))
                {
                    Main.TryBuildMechs(new Dictionary<string, int> {{mechID, 1}});
                }
            }

            Main.Salvaging = false;
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