using System;
using System.IO;
using System.Linq;
using Verse;

namespace RimKit
{
    /// <summary>
    /// Safe mode starts the game without running any Lua, so a player can get past a broken mod.
    /// Any of these turns it on: the command line flag -rimkit-safe, the environment variable RIMKIT_SAFE=1, or an
    /// empty file named rimkit_safe.txt in the game's Config folder (delete the file to turn it off).
    /// </summary>
    internal static class SafeMode
    {
        public const string Flag = "-rimkit-safe";
        public const string EnvVar = "RIMKIT_SAFE";
        public const string MarkerFile = "rimkit_safe.txt";

        public static bool IsActive(out string reason)
        {
            reason = null;
            try
            {
                if (Environment.GetCommandLineArgs().Any(a => string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase)))
                {
                    reason = "the command line flag " + Flag;
                    return true;
                }

                string env = Environment.GetEnvironmentVariable(EnvVar);
                if (env == "1" || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase))
                {
                    reason = "the environment variable " + EnvVar;
                    return true;
                }

                string marker = Path.Combine(GenFilePaths.ConfigFolderPath, MarkerFile);
                if (File.Exists(marker))
                {
                    reason = "the file " + marker;
                    return true;
                }
            }
            catch (Exception)
            {
                // Outside the game (unit tests) there is no config folder and no flag.
            }

            return false;
        }
    }
}
