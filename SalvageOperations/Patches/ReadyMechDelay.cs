using BattleTech;
using Harmony;
using BattleTech.UI;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisInfoWidget), "OnReadyClicked")]
    public class MechBayChassisInfoWidget_OnReadyClicked_Patch
    {
        static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;
        private static int MechReadyTime = Sim.Constants.Story.MechReadyTime;
        private static int ReadyTimeState = 0;

        public static void Prefix(ChassisDef ___selectedChassis)
        {
            int maxParts = Sim.Constants.Story.DefaultMechPartMax;
            int i = 1;
            do
            {
                var TempTagName = $"SO-{___selectedChassis.Description.Id}_{i}";
                if (Sim.CompanyTags.Contains(TempTagName))
                {
                    maxParts = i;
                    Sim.CompanyTags.Remove(TempTagName);
                }

                i++;
            } while (i < Sim.Constants.Story.DefaultMechPartMax + 1);

            ReadyTimeState = Sim.Constants.Story.MechReadyTime;
            Sim.Constants.Story.MechReadyTime = MechReadyTime * (Sim.Constants.Story.DefaultMechPartMax + 1 - maxParts);
        }

        public static void Postfix()
        {
            Sim.Constants.Story.MechReadyTime = ReadyTimeState;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ReadyMech")]
    public class SimGameState_ReadyMech_Patch
    {
        static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;
        private static int MechReadyTime = Sim.Constants.Story.MechReadyTime;
        private static int ReadyTimeState = 0;

        public static void Prefix(SimGameState __instance, string id)
        {
            Logger.LogDebug("A");
            int maxParts = Sim.Constants.Story.DefaultMechPartMax;
            int i = 1;
            do
            {
                Logger.LogDebug("B");
                var TempTagName = $"SO-{id}_{i}";
                if (Sim.CompanyTags.Contains(TempTagName))
                {
                    Logger.LogDebug("C");
                    maxParts = i;
                    Sim.CompanyTags.Remove(TempTagName);
                }

                i++;
            } while (i < Sim.Constants.Story.DefaultMechPartMax + 1);

            Logger.LogDebug("D.");

            ReadyTimeState = __instance.Constants.Story.MechReadyTime;
            __instance.Constants.Story.MechReadyTime = MechReadyTime * (Sim.Constants.Story.DefaultMechPartMax + 1 - maxParts);
        }

        public static void Postfix(SimGameState __instance)
        {
            __instance.Constants.Story.MechReadyTime = ReadyTimeState;
        }
    }
}