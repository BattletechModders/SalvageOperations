using BattleTech;
using BattleTech.UI;
using Harmony;
using UnityEngine;

// ReSharper disable InconsistentNaming

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisUnitElement), "OnButtonClicked")]
    public class MechBayChassisUnitElement_OnButtonClicked_Patch
    {
        public static void Prefix(MechBayChassisUnitElement __instance)
        {
            SimGameState sim = UnityGameInstance.BattleTechGame.Simulation;
            // if salvage mech icon is shift-clicked, force assembly checking on that chassis
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                // are we clicking on an assembled mech?
                // this causes problems since you can assemble a variant for which 
                // you have no pieces, just an assembled inactive mech
                if (__instance.PartsCount >= __instance.PartsMax)
                    return;
                Main.SalvageFromContract.Clear();
                var chassisId = __instance.ChassisDef.Description.Id;
                Main.HBSLog.LogDebug($"chassisId selected: {chassisId}");

                var mechId = chassisId.Replace("chassisdef", "mechdef");
                Main.TriggeredVariant = UnityGameInstance.BattleTechGame.DataManager.MechDefs.Get(mechId);

                Main.SalvageFromContract.Clear();
                Main.SalvageFromContract.Add(mechId, 1);
                Main.SimulateContractSalvage();
                Main.SalvageFromContract.Remove(mechId);
            }
        }
    }
}