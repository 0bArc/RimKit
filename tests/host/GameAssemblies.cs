using System;
using System.IO;
using System.Reflection;

namespace RimKit.Host.Tests
{
    // The host is compiled against RimWorld's assemblies, which are not copied next to the tests. Code that only needs a
    // type name to run (for example an "is this a Thing" check) loads them from the game folder on demand.
    internal static class GameAssemblies
    {
        private static bool installed;

        public static void Ensure()
        {
            if (installed) return;
            installed = true;
            string dir = Path.Combine(Environment.GetEnvironmentVariable("RimWorldDir") ?? @"C:\Program Files (x86)\Steam\steamapps\common\RimWorld", "RimWorldWin64_Data", "Managed");
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                string file = Path.Combine(dir, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };
        }
    }
}
