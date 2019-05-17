using System.Collections.Generic;
using BattleTech;

namespace SalvageOperations
{
    class Holder
    {
        public static bool BuiltAgain = false;
        public static Dictionary<ChassisDef, int> MechPartHolder = new Dictionary<ChassisDef, int>();
    }
}