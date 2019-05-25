using System.Linq;
using System.Text.RegularExpressions;
using BattleTech;
using Harmony;
using static SalvageOperations.Main;

// ReSharper disable InconsistentNaming

namespace SalvageOperations.Patches
{
    public class SimGameEventResultSet
    {
        [HarmonyPatch(typeof(SimGameEventTracker), "OnOptionSelected")]
        public static class SimGameEventTracker_OnOptionSelected_Patch
        {
            public static void Prefix(SimGameEventOption option)
            {
                // TODO some better way to filter this, in case the strings change for instance
                if (option.Description.Name.StartsWith("Build the") &&
                    option.Description.Name.EndsWith("Parts)"))
                {
                    var sim = UnityGameInstance.BattleTechGame.Simulation;
                    // tag our option
                    // find the highest indexed
                    var highest = 0;
                    foreach (var tag in sim.CompanyTags.Where(tag => tag.Contains("SO_PartsCounter")))
                    {
                        var match = Regex.Match(tag, @"SO_PartsCounter_mechdef_.+-.+_(\d+)_(\d+)$", RegexOptions.IgnoreCase);
                        var number = int.Parse(match.Groups[1].ToString());
                        highest = number > highest ? number : highest;
                    }

                    // make it a 1 if it's still a 0
                    highest = highest == 0 ? 1 : highest + 1;
                    // use this madness to pull the stored part count out of the dictionary global
                    sim.CompanyTags.Add(
                        $"SO_PartsCounter_{option.Description.Id}_{highest}_{VariantPartCounter[option.Description.Id]}");

                    // we've picked an option so...
                    VariantPartCounter.Clear();

                    // make a collection of options that weren't selected
                    //var otherOptions = EventDef.Options.Where(opt => !opt.Description.Id.Contains(option.Description.Id)).ToList();
                    //foreach (var opt in otherOptions)
                    //{
                    //    // extract the common mech name Locust
                    //    var mechName = Regex.Match(opt.Description.Id, @"mechdef_(.+)_.+-.+", RegexOptions.IgnoreCase).Groups[1].ToString();
                    //
                    //    // go through all company tags to remove any from this mech type which would have hopefully just been added and need removal
                    //    foreach (var tag in sim.CompanyTags.Where(tag => tag.Contains("SO_PartsCounter")))
                    //    {
                    //        if (tag.Contains($"SO_PartsCounter_{opt.Description.Id}"))
                    //        {
                    //            // only let it remove one tag
                    //            sim.CompanyTags.Remove(tag);
                    //            break;
                    //        }
                    //    }
                    //}

                    //if (option.Description.Id == "BuildNothing")
                    //{
                    //    // remove the tag which was created by the event running
                    //    // have to look at the other options to figure out what mech was being built
                    //    // the first is hopefully not 'BuildNothing'
                    //    
                    //    var mechName = Regex.Match(otherOptions.First().Description.Id, @"mechdef_(.+)_.+-.+", RegexOptions.IgnoreCase).Groups[1].ToString();
                    //    // we have Locust
                    //    // now we have to find ALL Locust
                    //}
                }
            }
        }
    }
}