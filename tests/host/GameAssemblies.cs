using System;
using System.IO;
using System.Reflection;

namespace RimKit.Host.Tests
{
    // The host is compiled against RimWorld's assemblies, which are not copied next to the tests. Code that only needs a
    // type name to run (for example an "is this a Thing" check) loads them from the game folder on demand. Harmony comes
    // from the Steam Workshop copy that RimWorld itself uses (set HarmonyWorkshopDir if it is somewhere else).
    internal static class GameAssemblies
    {
        private static bool installed;

        public static void Ensure()
        {
            if (installed) return;
            installed = true;
            string dir = Path.Combine(Environment.GetEnvironmentVariable("RimWorldDir") ?? @"C:\Program Files (x86)\Steam\steamapps\common\RimWorld", "RimWorldWin64_Data", "Managed");
            string harmony = Path.Combine(Environment.GetEnvironmentVariable("HarmonyWorkshopDir") ?? @"C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077", "Current", "Assemblies");
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                string name = new AssemblyName(e.Name).Name + ".dll";
                foreach (string folder in new[] { dir, harmony })
                {
                    string file = Path.Combine(folder, name);
                    if (File.Exists(file)) return Assembly.LoadFrom(file);
                }

                return null;
            };
        }
    }
}
