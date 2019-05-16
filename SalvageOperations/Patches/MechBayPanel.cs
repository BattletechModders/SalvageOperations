using System.Collections.Generic;
using BattleTech;
using BattleTech.UI;
using Harmony;
using UnityEngine;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayPanel), "ViewMechStorage")]
    public class MechBayPanel_OnButtonClicked_Patch
    {
        public static bool Prefix()
        {
            // if Storage is shift-clicked, force assembly checking
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                Main.ShowBuildPopup = true;
                var inventorySalvage = new Dictionary<string, int>(Main.Salvage);
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

                Main.ShowBuildPopup = true;
                Main.TryBuildMechs(sim, inventorySalvage, null);
                return false;
            }

            return true;
        }
    }
}