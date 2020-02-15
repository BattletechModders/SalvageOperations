using BattleTech.UI;
using Harmony;
using UnityEngine;
using BattleTech;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(MechBayPanel), "ViewMechStorage")]
    public class MechBayPanel_ViewMechStorage_Patch
    {
        public static bool Prefix()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            if (Main.Settings.DependsOnArgoUpgrade && !sim.PurchasedArgoUpgrades.Contains(Main.Settings.ArgoUpgrade))
                return true;

            // if Storage is shift-clicked, force assembly checking
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                Main.GlobalBuild();
                return false;
            }

            return true;
        }
    }
}