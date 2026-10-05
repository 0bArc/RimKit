#!/usr/bin/env node
// Generates the Lua-backed game classes (src/host/LuaClasses.g.cs) from the table in src/api/classes.cls.
// A mod normally subclasses a game class in C# to add behavior. RimKit ships one generated proxy per class instead: every
// override calls a Lua function of the same job, registered with game.classes.define. Adding a family is a few lines in the table.
//
// Table syntax (one entry per line, "#" starts a comment):
//   @class <family> <ClassName> : <BaseClass> key=<C# expression for the Lua class name>
//   @props <PropsClass> : <PropsBase> ctor=<C# statement run in the props constructor>   optional XML properties class with a luaClass field
//   @ctor <parameters> : <base call>                                                     constructor, may repeat
//   @field <C# member line>                                                              added to the class as written
//   @raw <C# member line>                                                                added to the class as written
//   [protected ]<ret> <Name>(<params>) : <luafn>(<args>) [-> <default>] [!base]
//       one override. The Lua function gets the args. ret is void, bool, string, float, int, AcceptanceReport or Job.
//       "-> base" uses the base method result when Lua gives none, "!base" runs the base method first (void methods).
// Run: node infrastructure/tools/gen-lua-classes.js [--check]
const fs = require("fs");
const path = require("path");

const root = path.join(__dirname, "../..");
const srcPath = path.join(root, "src/api/classes.cls");
const outPath = path.join(root, "src/host/LuaClasses.g.cs");
const lines = fs.readFileSync(srcPath, "utf8").replace(/\r/g, "").split("\n");

const classes = [];
let cur = null;
let problems = 0;
const fail = (n, msg) => {
  console.log(`lua classes: classes.cls:${n}: ${msg}`);
  problems++;
};

function splitTop(text) {
  const out = [];
  let depth = 0, token = "";
  for (const ch of text) {
    if ("([{<".includes(ch)) depth++;
    if (")]}>".includes(ch)) depth--;
    if (ch === "," && depth === 0) { out.push(token.trim()); token = ""; } else token += ch;
  }
  if (token.trim()) out.push(token.trim());
  return out;
}

lines.forEach((raw, i) => {
  const n = i + 1;
  const line = raw.trim();
  if (!line || line.startsWith("#")) return;
  let m;
  if ((m = /^@class\s+(\w+)\s+(\w+)\s*:\s*([\w.]+)\s+key=(.+)$/.exec(line))) {
    cur = { family: m[1], name: m[2], base: m[3], key: m[4].trim(), props: null, ctors: [], members: [], methods: [], line: n };
    classes.push(cur);
    return;
  }
  if (!cur) return fail(n, "entry before any @class");
  if ((m = /^@props\s+(\w+)\s*:\s*([\w.]+)\s+ctor=(.+)$/.exec(line))) { cur.props = { name: m[1], base: m[2], ctor: m[3].trim() }; return; }
  if ((m = /^@ctor\s*(.*?)\s*:\s*(base\(.*\))\s*$/.exec(line))) { cur.ctors.push({ params: m[1], base: m[2] }); return; }
  if ((m = /^@(field|raw)\s+(.+)$/.exec(line))) { cur.members.push(m[2]); return; }
  m = /^(protected\s+)?(void|bool|string|float|int|AcceptanceReport|Job)\s+(\w+)\((.*?)\)\s*:\s*(\w+)\((.*?)\)\s*(?:->\s*(.+?))?\s*(!base)?\s*$/.exec(line);
  if (!m) return fail(n, "cannot read: " + line);
  cur.methods.push({ vis: m[1] ? "protected" : "public", ret: m[2], name: m[3], params: m[4], fn: m[5], args: splitTop(m[6]), dflt: m[7], callBase: !!m[8] });
});

const paramNames = (p) =>
  splitTop(p).map((x) => {
    const parts = x.trim().split(/\s+/);
    const name = parts.pop();
    return parts[0] === "ref" || parts[0] === "out" ? parts[0] + " " + name : name;
  });
const defaults = { void: "", bool: "false", string: "null", float: "0f", int: "0", AcceptanceReport: "true", Job: "null" };

function methodCode(c, mt) {
  const names = paramNames(mt.params).join(", ");
  const callArgs = mt.args.length ? ", " + mt.args.join(", ") : "";
  const sig = `${mt.vis} override ${mt.ret} ${mt.name}(${mt.params})`;
  const lua = `L("${mt.fn}"${callArgs})`;
  const baseCall = `base.${mt.name}(${names})`;
  const lines = [`        ${sig}`, "        {"];
  if (mt.ret === "void") {
    if (mt.callBase) lines.push(`            ${baseCall};`);
    lines.push(`            ${lua};`);
  } else {
    const dflt = mt.dflt === "base" ? baseCall : mt.dflt || defaults[mt.ret];
    lines.push(`            object r = ${lua};`);
    switch (mt.ret) {
      case "bool": lines.push(`            return LuaClasses.IsBool(r) ? (bool)r : ${dflt};`); break;
      case "string": lines.push(`            return LuaClasses.Str(r, ${dflt});`); break;
      case "float": lines.push(`            return LuaClasses.IsNumber(r) ? LuaClasses.Float(r, 0f) : ${dflt};`); break;
      case "int": lines.push(`            return LuaClasses.IsNumber(r) ? LuaClasses.Int(r, 0) : ${dflt};`); break;
      case "AcceptanceReport": lines.push(`            return LuaClasses.Report(r, ${dflt});`); break;
      case "Job": lines.push(`            Job job = LuaClasses.MakeJob(r);`, `            return job ?? ${dflt};`); break;
    }
  }

  lines.push("        }", "");
  return lines.join("\n");
}

let out = `// Generated by infrastructure/tools/gen-lua-classes.js from src/api/classes.cls. Do not edit by hand.
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimKit
{
`;
for (const c of classes) {
  if (c.props) {
    out += `    public class ${c.props.name} : ${c.props.base}
    {
        public string luaClass;

        public ${c.props.name}()
        {
            ${c.props.ctor}
        }
    }

`;
  }

  out += `    // Lua family "${c.family}". Register functions with game.classes.define("${c.family}", name, { ... }).
    public class ${c.name} : ${c.base}
    {
        private object L(string fn, params object[] a) => LuaClasses.Call("${c.family}", ${c.key}, fn, a);

`;
  for (const ct of c.ctors) out += `        public ${c.name}(${ct.params}) : ${ct.base}\n        {\n        }\n\n`;
  for (const mem of c.members) out += `        ${mem}\n`;
  if (c.members.length) out += "\n";
  for (const mt of c.methods) out += methodCode(c, mt);
  out += "    }\n\n";
}

out = out.replace(/\n+$/, "\n") + "}\n";
// the family list the host exposes
out += `
namespace RimKit
{
    internal static class LuaClassFamilies
    {
        public static readonly string[] All = { ${[...new Set(classes.map((c) => `"${c.family}"`))].join(", ")} };
    }
}
`;

if (process.argv.includes("--check")) {
  const current = fs.existsSync(outPath) ? fs.readFileSync(outPath, "utf8").replace(/\r/g, "") : "";
  if (current !== out) { console.log("lua classes: src/host/LuaClasses.g.cs is out of date. Run node infrastructure/tools/gen-lua-classes.js"); problems++; }
} else if (!problems) fs.writeFileSync(outPath, out);
console.log(`lua classes: ${classes.length} classes, ${classes.reduce((n, c) => n + c.methods.length, 0)} generated overrides, ${problems} problem(s)`);
process.exit(problems ? 1 : 0);
