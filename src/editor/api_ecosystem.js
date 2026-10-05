// Phase 6 helpers for the editor extension. Free of vscode, so infrastructure/tools/check-editor.js can test them with node.
const fs = require("fs");
const path = require("path");

// Def names exported by game.dev.export_defs (the "RimKit: export def names" action in the dev tools). { kinds: { ThingDef: [names] } }
function loadDefs(file) {
  try {
    const data = JSON.parse(fs.readFileSync(file, "utf8"));
    return data && data.kinds ? data.kinds : {};
  } catch (e) {
    return {};
  }
}

// A copy in the workspace wins, then the file the game writes next to its config.
function defsFileCandidates(workspaceRoot) {
  const out = [];
  if (workspaceRoot) out.push(path.join(workspaceRoot, ".rimkit", "defs.json"));
  const home = process.env.USERPROFILE || process.env.HOME;
  if (home) out.push(path.join(home, "AppData", "LocalLow", "Ludeon Studios", "RimWorld by Ludeon Studios", "Config", "rimkit_defs.json"));
  return out;
}

const Q = "[\"']";
const NOT_Q = "[^\"']";
// Which def type a string literal at the end of a line stands for. [pattern, kind or function of the match].
const KIND_RULES = [
  [new RegExp("game\\.defs\\.\\w+\\(\\s*" + Q + "(\\w+)" + Q + "\\s*,\\s*" + Q + NOT_Q + "*$"), (m) => m[1]],
  [new RegExp("game\\.defs\\.(?:of|of_mod)\\(\\s*" + Q + NOT_Q + "*$"), () => "ThingDef"],
  [new RegExp("game\\.things\\.(?:spawn_at|make|thing_set)\\(" + NOT_Q + "*" + Q + NOT_Q + "*$"), () => "ThingDef"],
  [new RegExp("game\\.build\\.(?:instant|blueprint|can_place)\\(" + NOT_Q + "*" + Q + NOT_Q + "*$"), () => "ThingDef"],
  [new RegExp("game\\.pawns\\.(?:give_hediff|remove_hediff|has_hediff)\\(" + NOT_Q + "*" + Q + NOT_Q + "*$"), () => "HediffDef"],
  [new RegExp("game\\.pawns\\.(?:add_thought|remove_thought)\\(" + NOT_Q + "*" + Q + NOT_Q + "*$"), () => "ThoughtDef"],
  [new RegExp("game\\.pawns\\.(?:add_trait|remove_trait|has_trait)\\(" + NOT_Q + "*" + Q + NOT_Q + "*$"), () => "TraitDef"],
  [new RegExp("game\\.pawns\\.(?:skill|skill_info|set_passion|add_skill_xp|set_skill)\\(" + NOT_Q + "*" + Q + NOT_Q + "*$"), () => "SkillDef"],
  [new RegExp("game\\.pawns\\.(?:need|set_need)\\(" + NOT_Q + "*" + Q + NOT_Q + "*$"), () => "NeedDef"],
  [new RegExp("game\\.pawns\\.capacity\\(" + NOT_Q + "*" + Q + NOT_Q + "*$"), () => "PawnCapacityDef"],
  [new RegExp("game\\.pawns\\.(?:add_gene|remove_gene|has_gene)\\(" + NOT_Q + "*" + Q + NOT_Q + "*$"), () => "GeneDef"],
  [new RegExp("game\\.incidents\\.\\w+\\(\\s*" + Q + NOT_Q + "*$"), () => "IncidentDef"],
  [new RegExp("game\\.research\\.\\w+\\(\\s*" + Q + NOT_Q + "*$"), () => "ResearchProjectDef"],
  [new RegExp("game\\.stats\\.\\w+\\(\\s*" + Q + NOT_Q + "*$"), () => "StatDef"],
  [new RegExp("game\\.conditions\\.\\w+\\(\\s*" + Q + NOT_Q + "*$"), () => "GameConditionDef"],
  [new RegExp("game\\.weather\\.\\w+\\(\\s*" + Q + NOT_Q + "*$"), () => "WeatherDef"],
  [new RegExp("game\\.factions\\.\\w+\\(\\s*" + Q + NOT_Q + "*$"), () => "FactionDef"],
];

function defKindForContext(before) {
  for (const [re, kind] of KIND_RULES) {
    const m = re.exec(before);
    if (m) return kind(m);
  }
  return null;
}

// Line (0 based) of `function game.domain.name(` in a stub text, or -1.
function stubLine(text, name) {
  const parts = name.replace(/^game\./, "").split(".");
  if (parts.length !== 2) return -1;
  const re = new RegExp("^function game\\." + parts[0] + "\\." + parts[1] + "\\s*\\(", "m");
  const m = re.exec(text);
  return m ? text.slice(0, m.index).split("\n").length - 1 : -1;
}

module.exports = { loadDefs, defsFileCandidates, defKindForContext, stubLine };
