using BattleTech.Save;
using BattleTech.Save.SaveGameStructure;
using Harmony;

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(GameInstanceSave), "PreSerialization")]
    public class GameInstanceSave_PreSerialization_Patch
    {
        public static void Prefix(GameInstanceSave __instance)
        {
            if (__instance.SaveReason == SaveReason.SIM_GAME_EVENT_RESOLVED)
            {
                Logger.LogDebug(">>> Allowing build popup");
                Main.ShowBuildPopup = true;
            }
        }
    }

    // maybe not needed TODO check it out
    [HarmonyPatch(typeof(GameInstanceSave), "PostDeserialization")]
    public class GameInstanceSave_PostDeserialization_Patch
    {
        public static void Postfix()
        {
            Main.Salvage.Clear();
            Main.ShowBuildPopup = true;
        }
    }
}