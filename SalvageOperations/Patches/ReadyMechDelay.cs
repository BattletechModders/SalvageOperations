using BattleTech;
using BattleTech.UI;
using Harmony;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisInfoWidget), "OnReadyClicked")]
    public class MechBayChassisInfoWidget_OnReadyClicked_Patch
    {
        private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;
        private static readonly int MechReadyTime = Sim.Constants.Story.MechReadyTime;
        private static int readyTimeState;

        public static void Prefix(ChassisDef ___selectedChassis)
        {
            var readyMech = ___selectedChassis.Description.Id.Replace("chassisdef", "mechdef");
            int parts = Main.BuiltMechs[readyMech].Max();
            readyTimeState = Sim.Constants.Story.MechReadyTime;
            Sim.Constants.Story.MechReadyTime = (int)(MechReadyTime * Main.Settings.ReadyMechDelayFactor * (Sim.Constants.Story.DefaultMechPartMax + 1 - parts));
        }

        public static void Postfix()
        {
            Sim.Constants.Story.MechReadyTime = readyTimeState;
        }
    }

    //This happens second.
    [HarmonyPatch(typeof(SimGameState), "ReadyMech")]
    public class SimGameState_ReadyMech_Patch
    {
        private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;
        private static readonly int MechReadyTime = Sim.Constants.Story.MechReadyTime;
        private static int readyTimeState;
        public static string BuildingString;

        public static void Prefix(SimGameState __instance, string id)
        {
            var readyMech = id.Replace("chassisdef", "mechdef");
            int parts = Main.BuiltMechs[readyMech].Max();

            readyTimeState = __instance.Constants.Story.MechReadyTime;
            __instance.Constants.Story.MechReadyTime = (int)(MechReadyTime * Main.Settings.ReadyMechDelayFactor * (Sim.Constants.Story.DefaultMechPartMax + 1 - parts));

            BuildingString = "SO-Building-" + readyMech + "-" + parts;

            if (!Main.BuildingMechs.Keys.Contains(readyMech))
            {
                var tempList = new List<int>() { parts };
                Main.BuildingMechs.Add(readyMech, tempList);
            }
            else
            {
                Main.BuildingMechs[readyMech].Add(parts);
            }
            Main.BuiltMechs[readyMech].Remove(parts);
        }

        public static void Postfix(SimGameState __instance, int baySlot)
        {
            var mechDef = __instance.ReadyingMechs[baySlot];
            __instance.Constants.Story.MechReadyTime = readyTimeState;
            mechDef.MechTags.Add(BuildingString);
        }
    }

    //[HarmonyPatch(typeof(SimGameState), "ML_ReadyMech")]
    //public class SimGameState_ML_ReadyMech_Patch
    //{
    //    private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;

    //    public static void Postfix(WorkOrderEntry_ReadyMech order)
    //    {

    //        string id = order.Mech.Description.Id;
    //        int maxParts = Sim.Constants.Story.DefaultMechPartMax;
    //        int i = 1;
    //        do
    //        {
    //            var tempTagName = $"SO-{id}_{i}";
    //            if (Sim.CompanyTags.Contains(tempTagName))
    //            {
    //                maxParts = i;
    //                Sim.CompanyTags.Remove(tempTagName);
    //            }

    //            i++;
    //        } while (i < maxParts + 1);
    //    }
    //}

    [HarmonyPatch(typeof(SimGameState), "Cancel_ML_ReadyMech")]
    public class SimGameState_Cancel_ML_ReadyMech_Patch
    {
        public static void Prefix(WorkOrderEntry_ReadyMech order)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            MechDef mech = order.Mech;
            string tempTagName = mech.MechTags.First(x => x.StartsWith($"SO-Building-"));
           
            var match = Regex.Match(tempTagName, @"SO-Building-(\d)-(\d)$");
            var MDString = match.Groups[1].ToString();
            var MDCount = int.Parse(match.Groups[2].ToString());

            if (!Main.BuiltMechs.Keys.Contains(MDString))
            {
                var templist = new List<int>() { MDCount };
                Main.BuiltMechs.Add(MDString, templist);
            }
            else
            {
                Main.BuiltMechs[MDString].Add(MDCount);
            }
        }
    }
    [HarmonyPatch(typeof(SimGameState), "UnreadyMech")]
    public class SimGameState_UnreadyMech_Patch
    {
        public static void Postfix(SimGameState __instance, MechDef def)
        {
            string StorageTag = "SO-Assembled-" + def.Description.Id + "-" + __instance.Constants.Story.DefaultMechPartMax;
            __instance.CompanyTags.Add(StorageTag);
        }
    }
}