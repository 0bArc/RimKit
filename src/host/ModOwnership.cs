using System.Collections.Generic;

namespace RimKit
{
    /// <summary>
    /// Remembers which mod registered each gizmo, alert, status line, tab, column, designator and settings page, so a hot reload
    /// (game.dev.reload) can take away what the old version of the mod added before the new version adds its own.
    /// The native layer adds the running mod's package id as "_mod" to the registering ops.
    /// </summary>
    internal static class ModOwnership
    {
        // registering op -> (argument that names the thing, op that removes it)
        private static readonly Dictionary<string, (string key, string remove)> Registering = new Dictionary<string, (string, string)>
        {
            ["gizmo.add"] = ("id", "gizmo.remove"),
            ["gizmo.add_toggle"] = ("id", "gizmo.remove"),
            ["gizmo.add_slider"] = ("id", "gizmo.remove"),
            ["alert.add"] = ("id", "alert.remove"),
            ["hud.status"] = ("id", "hud.clear_status"),
            ["tabs.add_main"] = ("id", "tabs.remove"),
            ["tabs.add_inspect"] = ("id", "tabs.remove"),
            ["tabs.add_column"] = ("id", "tabs.remove"),
            ["designator.add"] = ("id", "designator.remove"),
            ["options.page"] = ("package_id", "options.remove_page"),
        };

        private static readonly Dictionary<string, HashSet<(string remove, string id)>> Owned = new Dictionary<string, HashSet<(string, string)>>();

        public static bool IsRegistering(string op) => Registering.ContainsKey(op);

        public static void Note(string op, Dictionary<string, string> args, string result)
        {
            if (args == null || !Registering.TryGetValue(op, out var info)) return;
            if (!args.TryGetValue("_mod", out string mod) || string.IsNullOrEmpty(mod)) return;
            if (result == null || result.IndexOf("\"ok\":true", System.StringComparison.Ordinal) < 0) return;
            if (!args.TryGetValue(info.key, out string id) || string.IsNullOrEmpty(id)) return;
            lock (Owned)
            {
                if (!Owned.TryGetValue(mod, out var set)) Owned[mod] = set = new HashSet<(string, string)>();
                set.Add((info.remove, id));
            }
        }

        /// <summary>Removes everything a mod registered. Returns how many things were removed.</summary>
        public static int Release(string mod)
        {
            HashSet<(string remove, string id)> set;
            lock (Owned)
            {
                if (!Owned.TryGetValue(mod, out set)) return 0;
                Owned.Remove(mod);
            }

            int removed = 0;
            foreach (var (remove, id) in set)
            {
                string key = remove == "options.remove_page" ? "package_id" : "id";
                if (ApiRegistry.TryInvoke(remove, new Dictionary<string, string> { [key] = id }, out string result) && result != null && result.Contains("\"v\":true")) removed++;
            }

            return removed;
        }
    }
}
