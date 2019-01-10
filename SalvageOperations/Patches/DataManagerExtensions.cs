using BattleTech;
using BattleTech.Data;
using Harmony;

namespace SalvageOperations
{
    // this is to patch the stat display on the event, since it's broken with flatpacked mechs and mech parts
    [HarmonyPatch(typeof(DataManagerExtensions), "GetStatDescDef")]
    public static class DataManagerExtensions_GetStatDescDef_Patch
    {
        public static bool Prefix(DataManager dataManager, SimGameStat simGameStat, ref SimGameStatDescDef __result)
        {
            string text = "SimGameStatDesc_" + simGameStat.name;
            if (!dataManager.Exists(BattleTechResourceType.SimGameStatDescDef, text))
            {
                if (!text.Contains("SimGameStatDesc_Item"))
                    return true;

                var itemStatDesc = dataManager.SimGameStatDescDefs.Get("SimGameStatDesc_Item");
                var split = text.Split('.');

                if (text.Contains("MECHPART"))
                {
                    var statDescDef = new SimGameStatDescDef();
                    var mechDef = dataManager.MechDefs.Get(split[2]);

                    if (mechDef == null)
                        return true;

                    statDescDef.Description.SetName($"{mechDef.Description.UIName} Parts");
                    __result = statDescDef;
                    return false;
                }
                else if (text.Contains("MechDef"))
                {
                    var statDescDef = new SimGameStatDescDef();
                    var chassisDef = dataManager.ChassisDefs.Get(split[2]);

                    if (chassisDef == null)
                        return true;

                    statDescDef.Description.SetName($"{chassisDef.Description.UIName}");
                    __result = statDescDef;
                    return false;
                }
            }

            return true;
        }
    }
}