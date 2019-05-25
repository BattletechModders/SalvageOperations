using BattleTech;
using BattleTech.UI;
using Harmony;
using System.Text.RegularExpressions;
using System.Linq;

// ReSharper disable InconsistentNaming

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisInfoWidget), "OnReadyClicked")]
    public class MechBayChassisInfoWidget_OnReadyClicked_Patch
    {
        private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;

        public static void Prefix(ChassisDef ___selectedChassis, ref int __state)
        {
            // find matching PartsCounter tag with the highest part count.  worst first
            const string pattern = @"SO_PartsCounter_mechdef_.+-.+_(\d+)_(\d+)$";
            var mechId = ___selectedChassis.Description.Id.Replace("chassisdef", "mechdef");
            var highest = 0;
            foreach (var tag in Sim.CompanyTags.Where(tag => tag.Contains($"SO_PartsCounter_{mechId}")))
            {
                var number = int.Parse(Regex.Match(tag, pattern, RegexOptions.IgnoreCase).Groups[2].ToString());
                highest = number > highest ? number : highest;
            }

            // save to restore in the Postfix (needs ref!)
            __state = Sim.Constants.Story.MechReadyTime;

            Sim.Constants.Story.MechReadyTime =
                (int) (__state * Main.Settings.ReadyMechDelayFactor * (Sim.Constants.Story.DefaultMechPartMax + 1 - highest));
            Logger.LogDebug($"MechReadyTime at OnReadyClicked prefix {Sim.Constants.Story.MechReadyTime}");
        }

        public static void Postfix(int __state)
        {
            Sim.Constants.Story.MechReadyTime = __state;
            Logger.LogDebug($"MechReadyTime at OnReadyClicked postfix {Sim.Constants.Story.MechReadyTime}");
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ReadyMech")]
    public class SimGameState_ReadyMech_Patch
    {
        private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;

        public static void Prefix(SimGameState __instance, string id, int baySlot, ref int __state)
        {
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
            Logger.LogDebug($"MechReadyTime at ReadyMech prefix {__instance.Constants.Story.MechReadyTime}");
        }

        public static void Postfix(SimGameState __instance, string id, int baySlot, int __state)
        {
            __instance.Constants.Story.MechReadyTime = __state;
            Logger.LogDebug($"MechReadyTime at ReadyMech postfix {__state}");
        }
    }

    //[HarmonyPatch(typeof(SimGameState), "ML_ReadyMech")]
    //public class SimGameState_ML_ReadyMech_Patch
    //{
    //    public static void Postfix(SimGameState __instance, WorkOrderEntry_ReadyMech order)
    //    {
    //        __instance.CompanyTags.Remove(MechBayChassisInfoWidget_OnReadyClicked_Patch.MechTag);
    //    }
    //}
}