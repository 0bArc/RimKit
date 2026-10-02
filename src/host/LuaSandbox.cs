using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace RimLuaKit
{
    // Deny OS / process / filesystem through Lua host ops. C# Assemblies/*.dll are unrestricted.
    internal static class LuaSandbox
    {
        private static readonly Regex SafeDefName = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static bool DeveloperReflectEnabled
        {
            get
            {
                if (LuaConfigBridge.Settings?.Bools == null) return false;
                return LuaConfigBridge.Settings.Bools.TryGetValue("rimkit.developer_reflect", out bool v) && v;
            }
        }

        private static readonly string[] BlockedTypePrefixes =
        {
            "System.Diagnostics",
            "System.IO",
            "System.Net",
            "System.Reflection.Emit",
            "System.Runtime.InteropServices",
            "System.Runtime.Remoting",
            "System.Management",
            "System.CodeDom",
            "Microsoft.Win32",
            "Mono.Unix",
            "Interop+",
        };

        private static readonly string[] BlockedExactTypes =
        {
            "System.Diagnostics.Process",
            "System.Diagnostics.ProcessStartInfo",
            "System.IO.File",
            "System.IO.Directory",
            "System.IO.FileInfo",
            "System.IO.DirectoryInfo",
            "System.IO.Path",
            "System.IO.StreamWriter",
            "System.IO.FileStream",
            "System.Reflection.Assembly",
            "System.AppDomain",
            "System.Environment",
            "System.Activator",
            "System.Type",
            "System.Delegate",
            "System.MulticastDelegate",
            "System.Threading.Thread",
            "UnityEngine.Application",
        };

        private static readonly string[] BlockedMethodNames =
        {
            "Start", "Kill", "Execute", "Exec", "ShellExecute",
            "Load", "LoadFrom", "LoadFile", "LoadModule", "GetProcAddress",
            "WriteAllText", "WriteAllBytes", "WriteAllLines", "AppendAllText",
            "Delete", "Move", "Copy", "Create", "CreateDirectory",
            "OpenRead", "OpenWrite", "OpenText",
            "GetMethod", "GetMethods", "InvokeMember", "CreateInstance",
            "Exit", "FailFast", "SetEnvironmentVariable",
            "GetDomain", "CreateInstanceFrom",
        };

        public static bool IsSafeDefName(string name) => !string.IsNullOrEmpty(name) && SafeDefName.IsMatch(name);

        public static bool TryJailUnderRoot(string rootDir, string candidatePath, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;
            try
            {
                string root = Path.GetFullPath(rootDir)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string full = Path.GetFullPath(candidatePath);
                string rootPrefix = root + Path.DirectorySeparatorChar;
                if (!full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                {
                    error = "sandbox: path escapes mod root";
                    return false;
                }
                fullPath = full;
                return true;
            }
            catch (Exception e)
            {
                error = "sandbox: bad path (" + e.Message + ")";
                return false;
            }
        }

        public static string DenyIfReflectBlocked(Type type, string memberOrMethod, bool isCall)
        {
            if (type == null) return "sandbox: null type";
            if (IsBlockedType(type))
            {
                return "sandbox: type blocked: " + (type.FullName ?? type.Name);
            }
            if (typeof(Type).IsAssignableFrom(type) ||
                typeof(Assembly).IsAssignableFrom(type) ||
                typeof(MethodBase).IsAssignableFrom(type) ||
                typeof(Delegate).IsAssignableFrom(type) ||
                typeof(Pointer).IsAssignableFrom(type))
            {
                return "sandbox: meta-type blocked: " + (type.FullName ?? type.Name);
            }
            if (isCall && !string.IsNullOrEmpty(memberOrMethod))
            {
                if (BlockedMethodNames.Any(b => string.Equals(b, memberOrMethod, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!IsGameplayNamespace(type.Namespace) ||
                        IsOsishMethod(memberOrMethod))
                    {
                        return "sandbox: method blocked: " + memberOrMethod;
                    }
                }
            }
            return null;
        }

        public static string DenyIfStaticTypeBlocked(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "sandbox: empty type";
            foreach (string exact in BlockedExactTypes)
            {
                if (string.Equals(exact, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return "sandbox: type blocked: " + typeName;
                }
            }
            foreach (string prefix in BlockedTypePrefixes)
            {
                if (typeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return "sandbox: type blocked: " + typeName;
                }
            }
            if (typeName.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                typeName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) ||
                typeName.StartsWith("Mono.", StringComparison.OrdinalIgnoreCase))
            {
                return "sandbox: system type blocked: " + typeName;
            }
            return null;
        }

        public static bool IsGameplayNamespace(string ns)
        {
            if (string.IsNullOrEmpty(ns)) return false;
            return ns.StartsWith("Verse", StringComparison.Ordinal) ||
                   ns.StartsWith("RimWorld", StringComparison.Ordinal) ||
                   ns.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                   ns.StartsWith("HarmonyLib", StringComparison.Ordinal) ||
                   ns.StartsWith("RimKit", StringComparison.Ordinal);
        }

        private static bool IsOsishMethod(string name)
        {
            return BlockedMethodNames.Any(b =>
                string.Equals(b, name, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(b, "Kill", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsBlockedType(Type t)
        {
            string full = t.FullName ?? t.Name;
            foreach (string exact in BlockedExactTypes)
            {
                if (string.Equals(exact, full, StringComparison.OrdinalIgnoreCase)) return true;
            }
            foreach (string prefix in BlockedTypePrefixes)
            {
                if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            string ns = t.Namespace ?? "";
            if (ns.StartsWith("System", StringComparison.Ordinal) && !IsGameplayNamespace(ns))
            {
                if (t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) ||
                    t == typeof(DateTime) || t == typeof(TimeSpan) || t == typeof(Guid))
                {
                    return false;
                }
                return true;
            }
            return false;
        }
    }
}
