using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.UI;
using Harmony;
using UnityEngine;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayChassisUnitElement), "OnButtonClicked")]
    public static class MechBayChassisUnitElement_OnButtonClicked_Patch
    {
        public static void Prefix(MechBayChassisUnitElement __instance)
        {
            // if salvage mech icon is shift-clicked, force assembly checking
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                var chassisId = __instance.ChassisDef.Description.Id;
                Main.ShowBuildPopup = true;
                Logger.LogDebug(">>> Allowing popup");
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
                // make an ID that looks like a mech part from this chassisDef
                // chrprfmech_marauderhotd-001
                // mechdef_firestarter_FS9-K
                var fakeMechPart = sim.DataManager.MechDefs
                    .Select(def => def.Value.Chassis.Description.Id)
                    .First(def => def == chassisId);
                fakeMechPart = fakeMechPart.Replace("chassisdef", "mechdef");
                Main.TryBuildMechs(sim, inventorySalvage, fakeMechPart, true);
            }

            //Logger.LogDebug($"chassisDef: {__instance.ChassisDef.PrefabIdentifier}, {__instance.ChassisDef.Description.UIName}");
        }
    }
}