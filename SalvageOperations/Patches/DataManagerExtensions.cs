using BattleTech;
using BattleTech.Data;
using Harmony;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SalvageOperations.Patches
{
    // this is to patch the stat display on the event, since it's broken with flatpacked mechs and mech parts
    [HarmonyPatch(typeof(DataManagerExtensions), "GetStatDescDef")]
    public static class DataManagerExtensions_GetStatDescDef_Patch
    {
        public static bool Prefix(DataManager dataManager, SimGameStat simGameStat, ref SimGameStatDescDef __result)
        {
            var text = "SimGameStatDesc_" + simGameStat.name;

            if (dataManager.Exists(BattleTechResourceType.SimGameStatDescDef, text) || !text.Contains("SimGameStatDesc_Item"))
                return true;

            var split = text.Split('.');
            var mechID = split[2];

            if (text.Contains("MECHPART"))
            {
                var statDescDef = new SimGameStatDescDef();
                var mechDef = dataManager.MechDefs.Get(mechID);

                if (mechDef == null)
                    return true;

                statDescDef.Description.SetName($"{mechDef.Description.UIName} Parts");
                __result = statDescDef;
                return false;
            }

            if (text.Contains("MechDef"))
            {
                var statDescDef = new SimGameStatDescDef();
                var chassisDef = dataManager.ChassisDefs.Get(mechID);

                if (chassisDef == null)
                    return true;

                statDescDef.Description.SetName($"{chassisDef.Description.UIName}");
                __result = statDescDef;
                return false;
            }

            return true;
        }
    }
}