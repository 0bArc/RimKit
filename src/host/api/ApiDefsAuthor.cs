using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using static RimKit.ApiHelpers;

namespace RimKit
{
    // Def authoring and XML patches from Lua, and the mod and assembly lookups mods need to hook other mods. Ops: defs.*, patch.*, mods.*.
    internal static class ApiDefsAuthor
    {
        public static void Register()
        {
            R("defs.to_xml", ToXml);
            R("defs.validate_xml", ValidateXml);
            R("defs.write_xml", WriteXml);
            R("patch.build", PatchBuild);
            R("patch.write", PatchWrite);
            R("mods.list", ModsList);
            R("mods.active", ModsActive);
            R("mods.info", ModInfo);
            R("mods.type_exists", ModTypeExists);
        }

        private static void R(string op, Func<Dictionary<string, string>, string> fn) => ApiRegistry.Register(op, a => fn(a), "advanced", "0.9.0");

        // ---- table to XML

        private static void WriteValue(XmlWriter w, string name, object v)
        {
            if (v is Dictionary<string, object> d)
            {
                w.WriteStartElement(name);
                if (d.TryGetValue("_class", out object cls)) w.WriteAttributeString("Class", Convert.ToString(cls));
                if (d.TryGetValue("_attrs", out object attrs) && attrs is Dictionary<string, object> ad)
                    foreach (var kv in ad) w.WriteAttributeString(kv.Key, Convert.ToString(kv.Value, System.Globalization.CultureInfo.InvariantCulture));
                foreach (var kv in d)
                {
                    if (kv.Key == "_class" || kv.Key == "_attrs") continue;
                    WriteValue(w, kv.Key, kv.Value);
                }

                w.WriteEndElement();
            }
            else if (v is List<object> list)
            {
                w.WriteStartElement(name);
                foreach (object item in list) WriteValue(w, "li", item);
                w.WriteEndElement();
            }
            else
            {
                w.WriteStartElement(name);
                if (v is bool b) w.WriteString(b ? "true" : "false");
                else if (v is double dd) w.WriteString(dd.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                else if (v != null) w.WriteString(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
                w.WriteEndElement();
            }
        }

        private static string BuildDefXml(string kind, string defName, Dictionary<string, object> fields, Opts o)
        {
            var sb = new StringBuilder();
            using (var w = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false, Encoding = new UTF8Encoding(false) }))
            {
                w.WriteStartDocument();
                w.WriteStartElement("Defs");
                w.WriteStartElement(kind);
                if (o.Has("parent")) w.WriteAttributeString("ParentName", o.Str("parent"));
                if (o.Has("name")) w.WriteAttributeString("Name", o.Str("name"));
                if (o.Bool("abstract")) w.WriteAttributeString("Abstract", "True");
                if (!string.IsNullOrEmpty(defName)) w.WriteElementString("defName", defName);
                if (fields != null)
                    foreach (var kv in fields) WriteValue(w, kv.Key, kv.Value);
                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndDocument();
            }

            return sb.ToString();
        }

        private static string Build(Dictionary<string, string> a, out string xml)
        {
            xml = null;
            string kind = Str(a, "kind");
            if (string.IsNullOrEmpty(kind)) return Fail("RK1001", "kind is required");
            Opts o = Opts.From(a, "opts");
            Dictionary<string, object> fields = null;
            if (a.TryGetValue("fields", out string json) && Json.TryParse(json, out object parsed)) fields = Json.AsObject(parsed);
            string defName = Str(a, "def");
            if (string.IsNullOrEmpty(defName) && !o.Bool("abstract")) return Fail("RK1001", "def (the defName) is required unless the def is abstract");
            if (!string.IsNullOrEmpty(defName) && !LuaSandbox.IsSafeDefName(defName)) return Fail("RK1001", "defName may only contain letters, digits and underscore");
            xml = BuildDefXml(kind, defName, fields, o);
            return null;
        }

        // game.defs.to_xml(kind, def, fields, opts): the Def XML for a table. opts: parent, name, abstract. A key _class sets the Class attribute.
        private static string ToXml(Dictionary<string, string> a)
        {
            string err = Build(a, out string xml);
            return err ?? OkStr(xml);
        }

        // ---- validation against the game's types

        private static void Check(Type t, XmlNode node, string path, List<string> errors)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element) continue;
                string name = child.Name;
                if (name == "li") continue;
                MemberInfo m = (MemberInfo)AccessTools.Field(t, name) ?? AccessTools.Property(t, name);
                if (m == null) { errors.Add($"{path}/{name}: {t.Name} has no field {name}"); continue; }
                Type ft = m is FieldInfo f ? f.FieldType : ((PropertyInfo)m).PropertyType;
                Type u = Nullable.GetUnderlyingType(ft) ?? ft;
                string text = child.InnerText.Trim();
                bool leaf = !child.ChildNodes.Cast<XmlNode>().Any(c => c.NodeType == XmlNodeType.Element);
                try
                {
                    if (u == typeof(int) && leaf) int.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
                    else if (u == typeof(float) && leaf) float.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
                    else if (u == typeof(bool) && leaf && !(text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("false", StringComparison.OrdinalIgnoreCase))) errors.Add($"{path}/{name}: {text} is not a boolean");
                    else if (u.IsEnum && leaf && !Enum.GetNames(u).Any(n => n.Equals(text, StringComparison.OrdinalIgnoreCase))) errors.Add($"{path}/{name}: {text} is not a {u.Name} (use one of {string.Join(", ", Enum.GetNames(u).Take(8))})");
                    else if (typeof(Def).IsAssignableFrom(u) && leaf && ApiDefsRuntime.FindDef(u, text) == null) errors.Add($"warning {path}/{name}: no {u.Name} named {text} is loaded (fine when your own mod defines it)");
                    else if (!leaf && ft.IsGenericType && typeof(IList).IsAssignableFrom(ft))
                    {
                        Type item = ft.GetGenericArguments()[0];
                        foreach (XmlNode li in child.ChildNodes)
                        {
                            if (li.NodeType != XmlNodeType.Element) continue;
                            string cls = li.Attributes?["Class"]?.Value;
                            Type lt = cls != null ? (GenTypes.GetTypeInAnyAssembly(cls) ?? GenTypes.GetTypeInAnyAssembly("RimWorld." + cls) ?? GenTypes.GetTypeInAnyAssembly("Verse." + cls)) : item;
                            if (lt == null) { errors.Add($"{path}/{name}/li: class {cls} was not found"); continue; }
                            if (!item.IsAssignableFrom(lt)) { errors.Add($"{path}/{name}/li: {lt.Name} is not a {item.Name}"); continue; }
                            if (!lt.IsPrimitive && lt != typeof(string) && !lt.IsEnum && !typeof(Def).IsAssignableFrom(lt) && li.ChildNodes.Cast<XmlNode>().Any(c => c.NodeType == XmlNodeType.Element)) Check(lt, li, path + "/" + name + "/li", errors);
                        }
                    }
                    else if (!leaf && !ft.IsPrimitive && ft != typeof(string) && !typeof(IDictionary).IsAssignableFrom(ft) && !typeof(IList).IsAssignableFrom(ft) && !ft.IsEnum)
                    {
                        string cls = child.Attributes?["Class"]?.Value;
                        Type nt = cls != null ? (GenTypes.GetTypeInAnyAssembly(cls) ?? ft) : ft;
                        Check(nt, child, path + "/" + name, errors);
                    }
                }
                catch (FormatException)
                {
                    errors.Add($"{path}/{name}: {text} is not a valid {u.Name}");
                }
            }
        }

        private static List<string> ValidateText(string xml)
        {
            var errors = new List<string>();
            var doc = new XmlDocument();
            try { doc.LoadXml(xml); }
            catch (XmlException e) { errors.Add("not well formed: " + e.Message); return errors; }
            XmlNode root = doc.DocumentElement;
            if (root == null) { errors.Add("empty document"); return errors; }
            IEnumerable<XmlNode> defs = root.Name == "Defs" ? root.ChildNodes.Cast<XmlNode>().Where(n => n.NodeType == XmlNodeType.Element) : new[] { root };
            foreach (XmlNode def in defs)
            {
                Type t = ApiDefsRuntime.DefType(def.Name);
                if (t == null) { errors.Add($"{def.Name}: not a def type"); continue; }
                bool isAbstract = def.Attributes?["Abstract"]?.Value?.Equals("True", StringComparison.OrdinalIgnoreCase) == true;
                if (!isAbstract && def["defName"] == null) errors.Add($"{def.Name}: a def needs a defName");
                Check(t, def, def.Name, errors);
            }

            return errors;
        }

        // Checks Def XML against the game's own types: well formed, known type and fields, values that parse, enums, def references.
        private static string ValidateXml(Dictionary<string, string> a)
        {
            List<string> all = ValidateText(Str(a, "xml"));
            var errors = all.Where(e => !e.StartsWith("warning ")).ToList();
            var warnings = all.Where(e => e.StartsWith("warning ")).Select(e => e.Substring(8)).ToList();
            return Jb.Obj().B("ok", errors.Count == 0).Raw("errors", Jb.Arr().Also(x => errors.ForEach(e => x.AddS(e))).ToString()).Raw("warnings", Jb.Arr().Also(x => warnings.ForEach(e => x.AddS(e))).ToString()).Ok();
        }

        private static string ModRootFor(string packageId, out string err)
        {
            err = null;
            ModContentPack pack = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => string.Equals(m.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
            if (pack?.RootDir == null) { err = Fail("RK3001", "mod not found: " + packageId); return null; }
            return Path.GetFullPath(pack.RootDir);
        }

        private static string WriteInto(string packageId, string folder, string file, string text, out string err)
        {
            string root = ModRootFor(packageId, out err);
            if (err != null) return null;
            string full = Path.GetFullPath(Path.Combine(root, folder, file));
            if (!full.StartsWith(Path.Combine(root, folder), StringComparison.OrdinalIgnoreCase)) { err = Fail("RK3003", "the path leaves the mod folder"); return null; }
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, text, new UTF8Encoding(false));
            return full;
        }

        // Validates a def and writes it to the mod's Defs folder, loaded the next time the game starts. Returns the file path.
        private static string WriteXml(Dictionary<string, string> a)
        {
            string err = Build(a, out string xml);
            if (err != null) return err;
            var problems = ValidateText(xml).Where(e => !e.StartsWith("warning ")).ToList();
            if (problems.Count > 0) return Fail("RK1001", "the def does not validate: " + string.Join("; ", problems.Take(5)));
            string safeKind = new string(Str(a, "kind").Where(char.IsLetterOrDigit).ToArray());
            string path = WriteInto(Str(a, "package_id"), "Defs", $"RimKit_{safeKind}_{(string.IsNullOrEmpty(Str(a, "def")) ? "abstract" : Str(a, "def"))}.xml", xml, out err);
            return err ?? OkStr(path);
        }

        // ---- XML patches

        private static readonly Dictionary<string, string> PatchClasses = new Dictionary<string, string>
        {
            ["add"] = "PatchOperationAdd", ["replace"] = "PatchOperationReplace", ["remove"] = "PatchOperationRemove", ["insert"] = "PatchOperationInsert",
            ["set_attribute"] = "PatchOperationAttributeSet", ["add_attribute"] = "PatchOperationAttributeAdd", ["remove_attribute"] = "PatchOperationAttributeRemove",
            ["test"] = "PatchOperationTest", ["conditional"] = "PatchOperationConditional", ["sequence"] = "PatchOperationSequence", ["if_mod"] = "PatchOperationFindMod",
            ["add_mod_extension"] = "PatchOperationAddModExtension",
        };

        private static void WriteOps(XmlWriter w, string element, List<object> ops, List<string> errors, string path)
        {
            for (int i = 0; i < ops.Count; i++)
            {
                var d = Json.AsObject(ops[i]);
                if (d == null) { errors.Add($"{path}[{i + 1}]: an operation must be a table"); continue; }
                string op = Json.GetString(d, "op");
                if (!PatchClasses.TryGetValue(op ?? "", out string cls)) { errors.Add($"{path}[{i + 1}]: unknown op {op}, use one of {string.Join(", ", PatchClasses.Keys)}"); continue; }
                w.WriteStartElement(element);
                w.WriteAttributeString("Class", cls);
                if (d.TryGetValue("success", out object succ)) w.WriteElementString("success", Convert.ToString(succ));
                string xpath = Json.GetString(d, "xpath");
                if (xpath != null)
                {
                    try { XPathExpression.Compile(xpath); } catch (XPathException e) { errors.Add($"{path}[{i + 1}]: bad xpath: {e.Message}"); }
                    w.WriteElementString("xpath", xpath);
                }
                else if (op != "sequence" && op != "if_mod") errors.Add($"{path}[{i + 1}]: {op} needs an xpath");

                if (d.TryGetValue("attribute", out object attr)) w.WriteElementString("attribute", Convert.ToString(attr));
                if (op == "set_attribute" || op == "add_attribute") { if (d.TryGetValue("value", out object av)) w.WriteElementString("value", Convert.ToString(av)); }
                else if (d.TryGetValue("value", out object val)) WriteValueNode(w, val, errors, $"{path}[{i + 1}]");
                if (d.TryGetValue("order", out object order)) w.WriteElementString("order", Convert.ToString(order));
                if (op == "if_mod")
                {
                    w.WriteStartElement("mods");
                    foreach (string m in Json.GetStringList(d, "mods")) w.WriteElementString("li", m);
                    w.WriteEndElement();
                }

                if (d.TryGetValue("match", out object match) && match is List<object> ml) { w.WriteStartElement("match"); w.WriteAttributeString("Class", "PatchOperationSequence"); w.WriteStartElement("operations"); WriteOps(w, "li", ml, errors, path + "[" + (i + 1) + "].match"); w.WriteEndElement(); w.WriteEndElement(); }
                if (d.TryGetValue("nomatch", out object nomatch) && nomatch is List<object> nl) { w.WriteStartElement("nomatch"); w.WriteAttributeString("Class", "PatchOperationSequence"); w.WriteStartElement("operations"); WriteOps(w, "li", nl, errors, path + "[" + (i + 1) + "].nomatch"); w.WriteEndElement(); w.WriteEndElement(); }
                if (op == "sequence" && d.TryGetValue("ops", out object inner) && inner is List<object> il) { w.WriteStartElement("operations"); WriteOps(w, "li", il, errors, path + "[" + (i + 1) + "].ops"); w.WriteEndElement(); }
                w.WriteEndElement();
            }
        }

        private static void WriteValueNode(XmlWriter w, object val, List<string> errors, string path)
        {
            w.WriteStartElement("value");
            if (val is string s)
            {
                try
                {
                    var frag = new XmlDocument();
                    frag.LoadXml("<x>" + s + "</x>");
                    foreach (XmlNode n in frag.DocumentElement.ChildNodes) n.WriteTo(w);
                }
                catch (XmlException e)
                {
                    errors.Add(path + ": the value is not well formed XML: " + e.Message);
                }
            }
            else if (val is Dictionary<string, object> d)
            {
                foreach (var kv in d) WriteValue(w, kv.Key, kv.Value);
            }

            w.WriteEndElement();
        }

        private static string BuildPatch(Dictionary<string, string> a, out string xml, out List<string> errors)
        {
            xml = null;
            errors = new List<string>();
            if (!a.TryGetValue("ops", out string json) || !Json.TryParse(json, out object parsed) || !(parsed is List<object> ops) || ops.Count == 0) return Fail("RK1001", "ops must list at least one operation");
            var sb = new StringBuilder();
            using (var w = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) }))
            {
                w.WriteStartDocument();
                w.WriteStartElement("Patch");
                WriteOps(w, "Operation", ops, errors, "ops");
                w.WriteEndElement();
                w.WriteEndDocument();
            }

            xml = sb.ToString();
            return null;
        }

        // game.patch.build(ops): the patch XML for a list of operations. Operations: add, replace, remove, insert, set_attribute, add_attribute,
        // remove_attribute, test, conditional, sequence, if_mod, add_mod_extension. Returns { xml, errors }.
        private static string PatchBuild(Dictionary<string, string> a)
        {
            string err = BuildPatch(a, out string xml, out List<string> errors);
            if (err != null) return err;
            return Jb.Obj().S("xml", xml).Raw("errors", Jb.Arr().Also(x => errors.ForEach(e => x.AddS(e))).ToString()).Ok();
        }

        private static string PatchWrite(Dictionary<string, string> a)
        {
            string err = BuildPatch(a, out string xml, out List<string> errors);
            if (err != null) return err;
            if (errors.Count > 0) return Fail("RK1001", "the patch is not valid: " + string.Join("; ", errors.Take(5)));
            string name = Str(a, "name");
            if (string.IsNullOrEmpty(name) || !name.All(ch => char.IsLetterOrDigit(ch) || ch == '_')) return Fail("RK1001", "name may only contain letters, digits and underscore");
            string path = WriteInto(Str(a, "package_id"), "Patches", "RimKit_" + name + ".xml", xml, out err);
            return err ?? OkStr(path);
        }

        // ---- mods

        private static ModContentPack Pack(string id) => LoadedModManager.RunningModsListForReading.FirstOrDefault(m => string.Equals(m.PackageId, id, StringComparison.OrdinalIgnoreCase));

        private static Jb PackJson(ModContentPack m)
        {
            var asm = Jb.Arr();
            foreach (Assembly a in m.assemblies?.loadedAssemblies ?? new List<Assembly>()) asm.AddS(a.GetName().Name);
            return Jb.Obj().S("package_id", m.PackageId).S("name", m.Name).S("path", m.RootDir).Raw("assemblies", asm.ToString());
        }

        private static string ModsList(Dictionary<string, string> a)
        {
            var arr = Jb.Arr();
            foreach (ModContentPack m in LoadedModManager.RunningModsListForReading) arr.Add(PackJson(m));
            return arr.Ok();
        }

        private static string ModsActive(Dictionary<string, string> a) => OkBool(Pack(Str(a, "package_id")) != null);

        private static string ModInfo(Dictionary<string, string> a)
        {
            ModContentPack m = Pack(Str(a, "package_id"));
            return m == null ? OkJson("null") : PackJson(m).Ok();
        }

        // Whether a mod is active and one of its assemblies has a type with this full name. Use it before hooking another mod's method.
        private static string ModTypeExists(Dictionary<string, string> a)
        {
            ModContentPack m = Pack(Str(a, "package_id"));
            if (m == null) return OkBool(false);
            string name = Str(a, "type");
            foreach (Assembly asm in m.assemblies?.loadedAssemblies ?? new List<Assembly>())
            {
                try { if (asm.GetType(name, false) != null) return OkBool(true); }
                catch (Exception) { }
            }

            return OkBool(false);
        }
    }
}

namespace RimKit
{
    internal static class JbExt2
    {
    }
}
