using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Verse;
using static RimLuaKit.ApiHelpers;

namespace RimLuaKit
{
    // Def writers under the owning mod.
    internal static class ApiDefsPlus
    {
        public static void Register()
        {
            ApiRegistry.Register("defs.write_hediff", WriteHediff, "gameplay");
            ApiRegistry.Register("defs.write_recipe", WriteRecipe, "gameplay");
            ApiRegistry.Register("defs.mod_root", ModRoot, "gameplay");
        }

        private static string ModRoot(Dictionary<string, string> args)
        {
            string packageId = Str(args, "package_id");
            ModContentPack pack = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(m => string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            return pack?.RootDir == null ? Err("mod not found") : OkStr(pack.RootDir);
        }

        private static bool TryJail(string packageId, string relativeUnderDefs, out string fullPath, out string err)
        {
            fullPath = null;
            err = null;
            ModContentPack pack = LoadedModManager.RunningModsListForReading
                .FirstOrDefault(m => string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            if (pack?.RootDir == null)
            {
                err = "mod not found: " + packageId;
                return false;
            }
            string candidate = Path.Combine(pack.RootDir, "Defs", relativeUnderDefs);
            if (!LuaSandbox.TryJailUnderRoot(pack.RootDir, candidate, out fullPath, out err))
            {
                return false;
            }
            return true;
        }

        private static string WriteHediff(Dictionary<string, string> args)
        {
            string packageId = Str(args, "package_id");
            string defName = Str(args, "defName");
            if (!LuaSandbox.IsSafeDefName(defName)) return Err("sandbox: bad defName");
            string label = Str(args, "label");
            if (string.IsNullOrEmpty(label)) label = defName;
            if (!TryJail(packageId, Path.Combine("HediffDefs", "RimLua_" + defName + ".xml"), out string full, out string err))
                return Err(err);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Defs>");
            sb.AppendLine("  <HediffDef>");
            sb.AppendLine("    <defName>" + Escape(defName) + "</defName>");
            sb.AppendLine("    <label>" + Escape(label) + "</label>");
            sb.AppendLine("    <hediffClass>HediffWithComps</hediffClass>");
            sb.AppendLine("    <defaultLabelColor>(0.7, 0.9, 1.0)</defaultLabelColor>");
            float sev = Float(args, "initialSeverity");
            if (sev <= 0f) sev = 0.5f;
            sb.AppendLine("    <initialSeverity>" + sev.ToString(System.Globalization.CultureInfo.InvariantCulture) + "</initialSeverity>");
            sb.AppendLine("  </HediffDef>");
            sb.AppendLine("</Defs>");
            File.WriteAllText(full, sb.ToString());
            return OkStr(full);
        }

        private static string WriteRecipe(Dictionary<string, string> args)
        {
            string packageId = Str(args, "package_id");
            string defName = Str(args, "defName");
            if (!LuaSandbox.IsSafeDefName(defName)) return Err("sandbox: bad defName");
            string label = Str(args, "label");
            if (string.IsNullOrEmpty(label)) label = defName;
            string product = Str(args, "product");
            int count = Int(args, "count");
            if (count <= 0) count = 1;
            if (!TryJail(packageId, Path.Combine("RecipeDefs", "RimLua_" + defName + ".xml"), out string full, out string err))
                return Err(err);
            Directory.CreateDirectory(Path.GetDirectoryName(full) ?? ".");
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<Defs>");
            sb.AppendLine("  <RecipeDef>");
            sb.AppendLine("    <defName>" + Escape(defName) + "</defName>");
            sb.AppendLine("    <label>" + Escape(label) + "</label>");
            sb.AppendLine("    <jobString>Making " + Escape(label) + ".</jobString>");
            sb.AppendLine("    <workAmount>400</workAmount>");
            if (!string.IsNullOrEmpty(product))
            {
                sb.AppendLine("    <products>");
                sb.AppendLine("      <" + Escape(product) + ">" + count + "</" + Escape(product) + ">");
                sb.AppendLine("    </products>");
            }
            sb.AppendLine("  </RecipeDef>");
            sb.AppendLine("</Defs>");
            File.WriteAllText(full, sb.ToString());
            return OkStr(full);
        }

        private static string Escape(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
