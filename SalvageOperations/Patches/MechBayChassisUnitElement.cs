using BattleTech.UI;
using Harmony;
using UnityEngine;
using BattleTech;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisUnitElement), "OnButtonClicked")]
    public class MechBayChassisUnitElement_OnButtonClicked_Patch
    {
        public static void Prefix(MechBayChassisUnitElement __instance)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Main.Settings.DependsOnArgoUpgrade && !sim.PurchasedArgoUpgrades.Contains(Main.Settings.ArgoUpgrade)
                && sim.Constants.Story.MaximumDebt != 42)
                return;

            // if salvage mech icon is shift-clicked, force assembly checking on that chassis
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Main.SalvageFromContract.Clear();
                var chassisId = __instance.ChassisDef.Description.Id;
                Logger.LogDebug($"chassisId ({chassisId})");

                var mechID = chassisId.Replace("chassisdef", "mechdef");
                Main.TriggeredVariant = mechID;
                Logger.LogDebug($"mechID ({mechID})");
                if (!Main.SalvageFromContract.ContainsKey(mechID))
                    Main.SalvageFromContract.Add(mechID, 1);
                else
                    Main.SalvageFromContract[mechID] = 1;

                Main.SimulateContractSalvage();
                Main.SalvageFromContract.Remove(mechID);
            }
        }
    }
}