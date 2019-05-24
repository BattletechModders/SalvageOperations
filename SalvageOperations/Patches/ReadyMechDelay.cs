using BattleTech;
using BattleTech.UI;
using Harmony;

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
            Sim.Constants.Story.MechReadyTime = (int) (MechReadyTime * Main.Settings.ReadyMechDelayFactor * (Sim.Constants.Story.DefaultMechPartMax + 1 - maxParts));
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
        private static int readyTimeState;

        public static void Prefix(SimGameState __instance, string id, int baySlot)
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
            __instance.Constants.Story.MechReadyTime = (int) (MechReadyTime * Main.Settings.ReadyMechDelayFactor * (Sim.Constants.Story.DefaultMechPartMax + 1 - maxParts));
        }

        public static void Postfix(SimGameState __instance, string id, int baySlot)
        {
            __instance.Constants.Story.MechReadyTime = readyTimeState;
            var mech = __instance.ReadyingMechs[baySlot];
            foreach (var component in mech.Inventory)
            {
                component.DamageLevel = ComponentDamageLevel.Destroyed;
            }
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ML_ReadyMech")]
    public class SimGameState_ML_ReadyMech_Patch
    {
        public static void Postfix(SimGameState __instance, WorkOrderEntry_ReadyMech order)
        {
            string id = order.Mech.Description.Id;
            int maxParts = __instance.Constants.Story.DefaultMechPartMax;
            int i = 1;
            do
            {
                var tempTagName = $"SO-{id}_{i}";
                if (__instance.CompanyTags.Contains(tempTagName))
                {
                    maxParts = i;
                    __instance.CompanyTags.Remove(tempTagName);
                }

                i++;
            } while (i < maxParts + 1);
        }
    }
}