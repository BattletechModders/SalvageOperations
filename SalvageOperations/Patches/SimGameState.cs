using System.Collections.Generic;
using BattleTech;
using Harmony;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace SalvageOperations.Patches
{
    [HarmonyPatch(typeof(SimGameState), "AddMechPart")]
    public static class SimGameState_AddMechPart_Patch
    {
        public static bool Prefix(SimGameState __instance, string id)
        {
            // this function replaces the function from SimGameState, prefix return false
            // just add the piece
            __instance.AddItemStat(id, "MECHPART", false);

            // we're in the middle of resolving a contract, add the piece to contract
            if (Main.IsResolvingContract)
            {
                if (!Main.SalvageFromContract.ContainsKey(id))
                    Main.SalvageFromContract[id] = 0;

                Main.SalvageFromContract[id]++;
                return false;
            }

            // TODO: what happens when you buy multiple pieces from the store at once and can build for each?
            // not in contract, just try to build with what we have
            Main.TryBuildMechs(__instance, new Dictionary<string, int> { { id, 1 } });
            return false;
        }
    }

    [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract")]
    public static class SimGameState_ResolveCompleteContract_Patch
    {
        public static void Prefix()
        {
            Main.ContractStart();
        }

        public static void Postfix(SimGameState __instance)
        {
            Main.TryBuildMechs(__instance, Main.SalvageFromContract);
            Main.ContractEnd();
        }
    }
}