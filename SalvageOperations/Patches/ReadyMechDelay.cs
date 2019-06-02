using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using BattleTech;
using BattleTech.UI;
using Harmony;
using TMPro.Examples;
using static Logger;

// ReSharper disable InconsistentNaming

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisInfoWidget), "OnReadyClicked")]
    public class MechBayChassisInfoWidget_OnReadyClicked_Patch
    {
        private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;

        public static void Postfix(ChassisDef ___selectedChassis, ref int __state)
        {
            // find matching PartsCounter tag with the highest part count.  worst first
            const string pattern = @"SO_PartsCounter_mechdef_.+-.+_(\d+)_(\d+)$";
            var mechId = ___selectedChassis.Description.Id.Replace("chassisdef", "mechdef");
            var mechDef = Sim.ReadyingMechs.Select(x => x.Value).First(def => def.Description.Id == mechId);
            var highestParts = 0;
            foreach (var tag in Sim.CompanyTags.Where(tag => tag.Contains($"SO_PartsCounter_{mechId}")))
            {
                var tagIndex = int.Parse(Regex.Match(tag, pattern, RegexOptions.IgnoreCase).Groups[1].ToString());
                var parts = int.Parse(Regex.Match(tag, pattern, RegexOptions.IgnoreCase).Groups[2].ToString());
                if (parts > highestParts)
                {
                    highestParts = parts;
                }
            }

            // save to restore in the Postfix (needs ref!)
            __state = Sim.Constants.Story.MechReadyTime;

            Sim.Constants.Story.MechReadyTime =
                (int) (__state * Main.Settings.ReadyMechDelayFactor * (Sim.Constants.Story.DefaultMechPartMax + 1 - highestParts));
            //LogDebug($"MechReadyTime at OnReadyClicked prefix {Sim.Constants.Story.MechReadyTime}");
        }

        public static void Postfix(ChassisDef ___selectedChassis, int __state)
        {
            Sim.Constants.Story.MechReadyTime = __state;
            //LogDebug($"MechReadyTime at OnReadyClicked postfix {Sim.Constants.Story.MechReadyTime}");
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ReadyMech")]
    public class SimGameState_ReadyMech_Patch
    {
        //private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;

        // save the local GUID to a static so we can use it to identify this mech in the postfix
        //public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //{
        //    var codes = instructions.ToList();
        //    var fieldInfo = AccessTools.Field(typeof(Main), "ReadyMechGUID");
        //    var insertIndex = codes.FindIndex(x => x.opcode == OpCodes.Stloc_2);
        //    codes.Insert(insertIndex, new CodeInstruction(OpCodes.Ldarg_2));
        //    codes.Insert(insertIndex, new CodeInstruction(OpCodes.Stsfld, fieldInfo));
        //    Main.ListTheStack(codes);
        //    return codes.AsEnumerable();
        //
        //    //var codes = instructions.ToList();
        //    //var fieldInfo = AccessTools.Field(typeof(Main), "ReadyMechDef");
        //    //
        //    ////var count = 0;
        //    ////var index = 0;
        //    ////for (var i = 0; i < codes.Count; i++)
        //    ////{
        //    ////    if (codes[i].opcode == OpCodes.Stloc_S)
        //    ////    {
        //    ////        count++;
        //    ////
        //    ////        if (count == 2)
        //    ////        {
        //    ////            index = i;
        //    ////            break;
        //    ////        }
        //    ////    }
        //    ////}
        //    //
        //    ////var codeStack = new List<CodeInstruction>();
        //    
        //    //
        //    //var index = codes.FindIndex(x => x.opcode == OpCodes.Newobj) + 2;
        //    //codes.Insert(index, new CodeInstruction(OpCodes.Ldloc_S));
        //    //codes.Insert(index, new CodeInstruction(OpCodes.Stsfld, fieldInfo));
        //    //
        //    //return codes.AsEnumerable();
        //}

        public static void Prefix(SimGameState __instance, string id, int baySlot, ref int __state)
        {
            var Sim = UnityGameInstance.BattleTechGame.Simulation;
            // find matching PartsCounter tag with the highest part count.  worst first
            var pattern = @"SO_PartsCounter_mechdef_.+-.+_(\d+)_(\d+)$";
            var highest = 0;
            foreach (var tag in Sim.CompanyTags.Where(tag => tag.Contains("SO_PartsCounter")))
            {
                var number = int.Parse(Regex.Match(tag, pattern, RegexOptions.IgnoreCase).Groups[2].ToString());
                highest = number > highest ? number : highest;
            }

            // save to restore in the Postfix (needs ref!)
            __state = __instance.Constants.Story.MechReadyTime;

            __instance.Constants.Story.MechReadyTime =
                (int) (__state * Main.Settings.ReadyMechDelayFactor * (Sim.Constants.Story.DefaultMechPartMax + 1 - highest));
            //LogDebug($"MechReadyTime at ReadyMech prefix {__instance.Constants.Story.MechReadyTime}");
        }

        public static void Postfix(SimGameState __instance, string id, int baySlot, int __state)
        {
            var Sim = UnityGameInstance.BattleTechGame.Simulation;
            __instance.Constants.Story.MechReadyTime = __state;

            const string pattern = @"SO_PartsCounter_mechdef_.+-.+_(\d+)_(\d+)$";
            var mechId = id.Replace("Item.MechDef.chassisdef", "mechdef");
            LogDebug("mechId " + mechId);
            //LogDebug(id);
            //var mechDef = new MechDef();

            var highestParts = 0;
            var index = 0;
            foreach (var tag in Sim.CompanyTags.Where(tag => tag.Contains($"SO_PartsCounter_{mechId}")))
            {
                //LogDebug(tag);
                //LogDebug(Regex.Match(tag, pattern, RegexOptions.IgnoreCase).Groups[0].ToString());
                var tagIndex = int.Parse(Regex.Match(tag, pattern, RegexOptions.IgnoreCase).Groups[1].ToString());
                var parts = int.Parse(Regex.Match(tag, pattern, RegexOptions.IgnoreCase).Groups[2].ToString());
                if (parts > highestParts)
                {
                    highestParts = parts;
                    index = tagIndex;
                }
            }

            if (index == 0) return;
            
            var mechDef = Sim.ReadyingMechs[baySlot];//Sim.ReadyingMechs.Select(m => m.Value).First(m => m.GUID == Main.ReadyMechGUID);

            //var tagToAdd = $"SO_PartsCounter_{mechDef.Description.Id}_{index}_{highestParts}";

            LogDebug("Add mechDef tag");
            mechDef.MechTags.Add($"SO_PartsCounter_{mechDef.Description.Id}_{index}_{highestParts}");
            Sim.CompanyTags.Remove($"SO_PartsCounter_{mechDef.Description.Id}_{index}_{highestParts}");
            //mechDef.MechTags.Do(LogDebug);

            //LogDebug($"MechReadyTime at ReadyMech postfix {__state}");
        }
    }

    //[HarmonyPatch(typeof(SimGameState), "ML_ReadyMech")]
    //public class SimGameState_ML_ReadyMech_Patch
    //{
    //    public static void Prefix(SimGameState __instance, WorkOrderEntry_ReadyMech order)
    //    {
    //                doesn't fire??
    //    }
    //}
    [HarmonyPatch(typeof(SimGameState), "Cancel_ML_ReadyMech")]
    public static class SimGameState_Cancel_ML_ReadyMech_Patch
    {
        public static void Postfix(WorkOrderEntry_ReadyMech order)
        {
            var Sim = UnityGameInstance.BattleTechGame.Simulation;
            var tag = order.Mech.MechTags.First(t => t.StartsWith("SO_PartsCounter"));
            Sim.CompanyTags.Add(tag);
        }
    }
}