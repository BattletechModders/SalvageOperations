using BattleTech.Save;
using BattleTech.Save.SaveGameStructure;
using Harmony;

namespace SalvageOperations.Patches
{
    //[HarmonyPatch(typeof(GameInstanceSave), MethodType.Constructor)]
    public class GameInstanceSave_Ctor_Patch
    {
        public static void Prefix(GameInstanceSave __instance)
        {
            //if (__instance.SaveReason == SaveReason.SIM_GAME_EVENT_RESOLVED)
            //{
                Logger.LogDebug(">>> Allowing popup");
                Main.ShowBuildPopup = true;
            //}
        }
    }
    
    [HarmonyPatch(typeof(GameInstanceSave), "PostDeserialization")]
    public class GameInstanceSaveHydrate_Patch
    {
        public static void Postfix()
        {
            Main.SalvageFromOther.Clear();
            Main.ShowBuildPopup = true;
            Logger.LogDebug("Loaded - SalvageFromOther cleared");
        }
    }
}