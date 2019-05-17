using System.Collections.Generic;
using BattleTech;
using BattleTech.UI;
using Harmony;
using UnityEngine;

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
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var chassisId = __instance.ChassisDef.Description.Id;
                var inventorySalvage = new Dictionary<string, int>( /*Main.Salvage*/);
                var inventory = sim.GetAllInventoryMechDefs();
                foreach (var item in inventory)
                {
                    var id = item.Description.Id.Replace("chassisdef", "mechdef");
                    var itemCount = sim.GetItemCount(id, "MECHPART", SimGameState.ItemCountType.UNDAMAGED_ONLY);
                    if (!inventorySalvage.ContainsKey(id))
                        inventorySalvage.Add(id, itemCount);
                    else
                        inventorySalvage[id] += itemCount;
                }

                var mechID = chassisId.Replace("chassisdef", "mechdef");
                Main.TriggeredVariant = mechID;
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