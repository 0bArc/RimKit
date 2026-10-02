using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Verse;
using static RimLuaKit.ApiHelpers;

namespace RimLuaKit
{
    // Folders / export helpers (fixed roots only).
    internal static class ApiUtil
    {
        public static void Register()
        {
            ApiRegistry.Register("util.open_folder", OpenFolder, "util");
            ApiRegistry.Register("util.write_export", WriteExport, "util");
            ApiRegistry.Register("util.mod_export_dir", ExportDir, "util");
        }

        private static string ResolveTarget(string target, string packageId, out string err)
        {
            err = null;
            switch ((target ?? "").ToLowerInvariant())
            {
                case "modroot":
                case "mod_root":
                {
                    ModContentPack pack = LoadedModManager.RunningModsListForReading
                        .FirstOrDefault(m => string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
                    if (pack?.RootDir == null)
                    {
                        err = "mod not found";
                        return null;
                    }
                    return pack.RootDir;
                }
                case "playerlog":
                case "log":
                    return GenFilePaths.SaveDataFolderPath; // parent; log beside saves on some installs
                case "saves":
                    return GenFilePaths.SaveDataFolderPath;
                case "rimkit":
                {
                    ModContentPack pack = LoadedModManager.RunningModsListForReading
                        .FirstOrDefault(m => string.Equals(m.PackageId, "stratware.rimkit", StringComparison.OrdinalIgnoreCase));
                    return pack?.RootDir;
                }
                default:
                    err = "target must be ModRoot|PlayerLog|Saves|RimKit";
                    return null;
            }
        }

        private static string OpenFolder(Dictionary<string, string> args)
        {
            string path = ResolveTarget(Str(args, "target"), Str(args, "package_id"), out string err);
            if (path == null) return Err(err ?? "bad target");
            if (!Directory.Exists(path))
            {
                try { Directory.CreateDirectory(path); }
                catch (Exception e) { return Err(e.Message); }
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
                return OkBool(true);
            }
            catch (Exception e)
            {
                return Err("open failed: " + e.Message);
            }
        }

        private static string ExportDir(Dictionary<string, string> args)
        {
            string packageId = Str(args, "package_id");
            ModContentPack pack = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(m => string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            if (pack?.RootDir == null) return Err("mod not found");
            string dir = Path.Combine(pack.RootDir, "Export");
            return OkStr(dir);
        }

        private static string WriteExport(Dictionary<string, string> args)
        {
            string packageId = Str(args, "package_id");
            string fileName = Str(args, "file");
            string content = Str(args, "content");
            if (string.IsNullOrEmpty(packageId) || string.IsNullOrEmpty(fileName)) return Err("package_id and file required");
            if (fileName.IndexOfAny(new[] { '/', '\\', ':', '*' }) >= 0 || fileName.Contains(".."))
                return Err("sandbox: bad file name");
            ModContentPack pack = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(m => string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            if (pack?.RootDir == null) return Err("mod not found");
            string dir = Path.Combine(pack.RootDir, "Export");
            string candidate = Path.Combine(dir, fileName);
            if (!LuaSandbox.TryJailUnderRoot(pack.RootDir, candidate, out string full, out string jailErr))
                return Err(jailErr);
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(full, content ?? "", Encoding.UTF8);
                return OkStr(full);
            }
            catch (Exception e)
            {
                return Err(e.Message);
            }
        }
    }
}
