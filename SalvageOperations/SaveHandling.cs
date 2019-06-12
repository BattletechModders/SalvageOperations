using System.IO;
using System.Linq;
using BattleTech;
using BattleTech.Save.Test;
using Harmony;
using Newtonsoft.Json;
using UnityEngine;
using static Logger;
using System.Collections.Generic;

namespace SalvageOperations
{
    public class SaveValues
    {
        public Dictionary<string, List<int>> BuiltMechs;
        public Dictionary<string, List<int>> BuildingMechs;

        public SaveValues(Dictionary<string, List<int>> builtMechs, Dictionary<string, List<int>> buildingMechs)
        {
            this.BuiltMechs = builtMechs;
            this.BuildingMechs = buildingMechs;
        }
    }


    public static class SaveHandling
    {
        public static SaveValues SaveValues;
        [HarmonyPatch(typeof(SimGameState), "_OnAttachUXComplete")]
        public static class SimGameState__OnAttachUXComplete_Patch
        {
            public static void Postfix()
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (sim.CompanyTags.Any(x => x.StartsWith("SOSAVE{")))
                {
                    DeserializeSO();
                }
            }
        }

        [HarmonyPatch(typeof(SerializableReferenceContainer), "Save")]
        public static class SerializableReferenceContainer_Save_Patch
        {
            public static void Prefix()
            {
                if (UnityGameInstance.BattleTechGame.Simulation == null) return;
                    SerializeSO();
            }
        }

        [HarmonyPatch(typeof(SerializableReferenceContainer), "Load")]
        public static class SerializableReferenceContainer_Load_Patch
        {
            // get rid of tags before loading because vanilla behaviour doesn't purge them
            public static void Prefix()
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (sim == null) return;
                sim.CompanyTags.Where(tag => tag.StartsWith("SOSAVE")).Do(x => sim.CompanyTags.Remove(x));
            }
        }

        internal static void SerializeSO()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            sim.CompanyTags.Where(tag => tag.StartsWith("SOSAVE")).Do(x => sim.CompanyTags.Remove(x));
            sim.CompanyTags.Add("SOSAVE" + JsonConvert.SerializeObject(SaveValues));
        }

        internal static void DeserializeSO()
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            SaveValues = JsonConvert.DeserializeObject<SaveValues>(sim.CompanyTags.First(x => x.StartsWith("SOSAVE{")).Substring(6));
            //Main.BuildingMechs = SaveValues.BuildingMechs;
            Main.BuiltMechs = SaveValues.BuiltMechs;
        }
    }
}