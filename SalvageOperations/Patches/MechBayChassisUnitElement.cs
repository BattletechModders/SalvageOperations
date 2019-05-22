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
            // if salvage mech icon is shift-clicked, force assembly checking on that chassis
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Main.SalvageFromContract.Clear();
                var chassisId = __instance.ChassisDef.Description.Id;
                Logger.LogDebug($"chassisId selected: {chassisId}");

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