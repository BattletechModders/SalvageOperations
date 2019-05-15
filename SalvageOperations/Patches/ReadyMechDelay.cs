using BattleTech;
using Harmony;
using BattleTech.UI;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisInfoWidget), "OnReadyClicked")]
    public class MechBayChassisInfoWidget_OnReadyClicked_Patch
    {
        private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;
        private static readonly int MechReadyTime = Sim.Constants.Story.MechReadyTime;
        private static int readyTimeState = 0;

        public static void Prefix(ChassisDef ___selectedChassis)
        {
            int maxParts = Sim.Constants.Story.DefaultMechPartMax;
            int i = 1;
            do
            {
                var tempTagName = $"SO-{___selectedChassis.Description.Id}_{i}";
                if (Sim.CompanyTags.Contains(tempTagName))
                {
                    maxParts = i;
                }

                i++;
            } while (i < Sim.Constants.Story.DefaultMechPartMax + 1);

            readyTimeState = Sim.Constants.Story.MechReadyTime;
            Sim.Constants.Story.MechReadyTime = MechReadyTime * (Sim.Constants.Story.DefaultMechPartMax + 1 - maxParts);
        }

        public static void Postfix()
        {
            Sim.Constants.Story.MechReadyTime = readyTimeState;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ReadyMech")]
    public class SimGameState_ReadyMech_Patch
    {
        private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;
        private static readonly int MechReadyTime = Sim.Constants.Story.MechReadyTime;
        private static int readyTimeState = 0;

        public static void Prefix(SimGameState __instance, string id)
        {
            int maxParts = Sim.Constants.Story.DefaultMechPartMax;
            int i = 1;
            do
            {
                var tempTagName = $"SO-{id}_{i}";
                if (Sim.CompanyTags.Contains(tempTagName))
                {
                    maxParts = i;
                }

                i++;
            } while (i < Sim.Constants.Story.DefaultMechPartMax + 1);

            readyTimeState = __instance.Constants.Story.MechReadyTime;
            __instance.Constants.Story.MechReadyTime = MechReadyTime * (Sim.Constants.Story.DefaultMechPartMax + 1 - maxParts);
        }

        public static void Postfix(SimGameState __instance)
        {
            __instance.Constants.Story.MechReadyTime = readyTimeState;
        }
    }
    
    [HarmonyPatch(typeof(SimGameState), "ML_ReadyMech")]
    public class SimGameState_ML_ReadyMech_Patch
    {
        private static readonly SimGameState Sim = UnityGameInstance.BattleTechGame.Simulation;

        public static void Postfix(WorkOrderEntry_ReadyMech order)
        {
            string id = order.Mech.Description.Id;
            int maxParts = Sim.Constants.Story.DefaultMechPartMax;
            int i = 1;
            do
            {
                var tempTagName = $"SO-{id}_{i}";
                if (Sim.CompanyTags.Contains(tempTagName))
                {
                    maxParts = i;
                    Sim.CompanyTags.Remove(tempTagName);
                }

                i++;
            } while (i < Sim.Constants.Story.DefaultMechPartMax + 1);

        }
    }

}