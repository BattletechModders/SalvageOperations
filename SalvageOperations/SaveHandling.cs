using System.IO;
using System.Linq;
using BattleTech;
using BattleTech.Save.Test;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;
using static Logger;
using System.Collections.Generic;
using BattleTech.Save;

namespace SalvageOperations
{
    public class SaveValues
    {
        public Dictionary<string, List<int>> BuiltMechs;
        public Dictionary<string, int> EquippedMechs;

        public SaveValues(Dictionary<string, List<int>> builtMechs, Dictionary<string, int> equippedMechs)
        {
            this.BuiltMechs = builtMechs;
            this.EquippedMechs = equippedMechs;
        }
    }

    public static class SaveHandling
    {
        public static SaveValues SaveValues;
        [HarmonyPatch(typeof(SimGameState), "Rehydrate")]
        public static class SimGameState_Rehydrate_Patch
        {
            static void Postfix(SimGameState __instance, GameInstanceSave gameInstanceSave)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (Main.Settings.DependsOnArgoUpgrade && !sim.PurchasedArgoUpgrades.Contains(Main.Settings.ArgoUpgrade)
                    && sim.Constants.Story.MaximumDebt != 42)
                    return;

                if (sim.CompanyTags.Any(x => x.StartsWith("SOSAVE{")))
                    DeserializeSO();
            }
        }
        internal static void DeserializeSO()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            SaveValues = JsonConvert.DeserializeObject<SaveValues>(sim.CompanyTags.First(x => x.StartsWith("SOSAVE{")).Substring(6));
            Main.BuiltMechs = SaveValues.BuiltMechs;
            Main.EquippedMechs = SaveValues.EquippedMechs;
        }
       
        [HarmonyPatch(typeof(SimGameState), "Dehydrate")]
        public static class SimGameState_Dehydrate_Patch
        {
            public static void Prefix(SimGameState __instance)
            {
                if (Main.Settings.DependsOnArgoUpgrade && !__instance.PurchasedArgoUpgrades.Contains(Main.Settings.ArgoUpgrade)
                    && __instance.Constants.Story.MaximumDebt != 42)
                    return;

                SerializeSO();
            }
        }
        internal static void SerializeSO()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            sim.CompanyTags.Where(tag => tag.StartsWith("SOSAVE")).Do(x => sim.CompanyTags.Remove(x));
            sim.CompanyTags.Add("SOSAVE" + JsonConvert.SerializeObject(SaveValues));
        }
    }
}