using System;
using Newtonsoft.Json;
using UnityEngine;

namespace SalvageOperations
{
    internal class ModSettings
    {
        public bool SalvageValueUsesSellPrice = true;
        public bool ReplaceMechSalvageLogic = true;
        public KeyCode Hotkey = KeyCode.A; 
        public bool Debug = false;
        
        public static ModSettings ReadSettings(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<ModSettings>(json);
            }
            catch (Exception e)
            {
                Main.HBSLog.Log($"Reading settings failed: {e.Message}");
                return new ModSettings();
            }
        }
    }
}
