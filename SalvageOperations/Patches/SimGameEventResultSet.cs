using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BattleTech;
using Harmony;
using HBS.Collections;
using static Logger;

namespace SalvageOperations.Patches
{
    public class SimGameEventResultSet
    {
        [HarmonyPatch(typeof(SimGameEventTracker), "OnOptionSelected")]
        public static class SimGameEventTracker_OnOptionSelected_Patch
        {
            public static void Prefix(SimGameEventOption option)
            {
                LogDebug("OptionSelected");
                LogDebug(option.Description.Name);
                if (option.Description.Name.StartsWith("Build the") &&
                    option.Description.Name.EndsWith("Parts)"))
                {
                    var sim = UnityGameInstance.BattleTechGame.Simulation;

                    // where are the unselected options? changed the event to a global...
                    var otherOptions = Main.EventDef.Options.Where(opt => !opt.Description.Id.Contains(option.Description.Id));
                    foreach (var opt in otherOptions)
                    {
                        var mechName = Regex.Match(opt.Description.Id, @"mechdef_(.+)_.+-.+", RegexOptions.IgnoreCase).Groups[1].ToString();
                        // go through all company tags to remove any from this mech type which would have hopefully just been added and need removal
                        foreach (var tag in sim.CompanyTags.Where(tag => tag.Contains("SO_PartsCounter")))
                        {
                            if (tag.Contains($"SO_PartsCounter_{opt.Description.Id}"))
                            {
                                LogDebug($"tag {tag} match, removing");
                                sim.CompanyTags.Remove(tag);
                            }
                            else
                            {
                                LogDebug($"tag {tag} didn't match");
                            }
                        }
                    }

                    if (Main.VariantPartCounter.ContainsKey(option.Description.Id))
                        sim.CompanyTags.Add($"SO_PartsCounter_{option.Description.Id}_{Main.VariantPartCounter[option.Description.Id]}");
                }
            }
        }
    }
}