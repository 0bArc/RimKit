// Project templates for "RimKit: New mod". rimkit mod create writes the base (meta.lua, About, licence, tests); a template adds
// or replaces files on top. {{name}} and {{id}} are replaced.
const templates = [
  {
    id: "basic",
    label: "Basic mod",
    detail: "A Lua file that runs when the game loads. Start here.",
    capabilities: [],
    files: {},
  },
  {
    id: "comp",
    label: "Building with a Lua component",
    detail: "A lamp whose behaviour is a Lua class (game.classes).",
    capabilities: [],
    files: {
      "Lua/main.lua": [
        "-- {{name}}: a component written in Lua. Defs/Building.xml names the class \"{{id}}_comp\".",
        "game.classes.define(\"comp\", \"{{id}}_comp\", {",
        "  tick_rare = function(thing)",
        "    local n = tonumber(game.classes.data_get(thing, \"count\") or \"0\") or 0",
        "    game.classes.data_set(thing, \"count\", tostring(n + 1))",
        "  end,",
        "  inspect_string = function(thing)",
        "    return \"Count: \" .. (game.classes.data_get(thing, \"count\") or \"0\")",
        "  end,",
        "})",
        "",
      ].join("\n"),
      "Defs/Building.xml": [
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
        "<Defs>",
        "  <ThingDef ParentName=\"LampBase\">",
        "    <defName>{{name}}Lamp</defName>",
        "    <label>{{name}} lamp</label>",
        "    <description>A lamp with a Lua component.</description>",
        "    <graphicData><texPath>Things/Building/Furniture/LampStanding</texPath></graphicData>",
        "    <costList><Steel>20</Steel></costList>",
        "    <comps>",
        "      <li Class=\"CompProperties_Power\"><compClass>CompPowerTrader</compClass><basePowerConsumption>30</basePowerConsumption></li>",
        "      <li Class=\"CompProperties_Glower\"><glowRadius>10</glowRadius><glowColor>(120,200,230,0)</glowColor></li>",
        "      <li Class=\"RimKit.CompProperties_Lua\"><luaClass>{{id}}_comp</luaClass></li>",
        "    </comps>",
        "  </ThingDef>",
        "</Defs>",
        "",
      ].join("\n"),
    },
  },
  {
    id: "incident",
    label: "Incident",
    detail: "A new storyteller incident with a Lua worker.",
    capabilities: [],
    files: {
      "Lua/main.lua": [
        "-- {{name}}: an incident written in Lua.",
        "game.classes.define(\"incident\", \"{{id}}_incident\", {",
        "  can_fire = function(parms) return parms.map ~= nil end,",
        "  execute = function(parms)",
        "    game.hud.letter(\"{{name}}\", \"Something happened.\", { \"OK\" }, \"{{id}}\")",
        "    return true",
        "  end,",
        "})",
        "game.dev.action(\"{{name}}: fire now\", function() game.incidents.fire(\"{{name}}Incident\") end)",
        "",
      ].join("\n"),
      "Defs/Incident.xml": [
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>",
        "<Defs>",
        "  <IncidentDef>",
        "    <defName>{{name}}Incident</defName>",
        "    <label>{{name}}</label>",
        "    <category>Misc</category>",
        "    <targetTags><li>Map_PlayerHome</li></targetTags>",
        "    <workerClass>RimKit.IncidentWorker_Lua</workerClass>",
        "    <baseChance>0.5</baseChance>",
        "    <modExtensions><li Class=\"RimKit.DefModExtension_Lua\"><luaClass>{{id}}_incident</luaClass></li></modExtensions>",
        "  </IncidentDef>",
        "</Defs>",
        "",
      ].join("\n"),
    },
  },
  {
    id: "tweaks",
    label: "Balance tweaks",
    detail: "Change game numbers with the tweak catalog (needs the hooks capability).",
    capabilities: ["hooks"],
    files: {
      "Lua/main.lua": [
        "-- {{name}}: colonists get hungry at half speed. See game.tweaks.list() for every tweak point.",
        "game.tweaks.on(\"pawn.hunger_rate\", function(t)",
        "  return t.value * 0.5",
        "end)",
        "",
      ].join("\n"),
    },
  },
  {
    id: "window",
    label: "Window",
    detail: "A key opens a window built from tables.",
    capabilities: [],
    files: {
      "Lua/main.lua": [
        "-- {{name}}: F9 opens a window.",
        "game.input.register_key(\"{{id}}_open\", \"Open {{name}}\", \"F9\")",
        "game.events.on_tick(function()",
        "  if game.input.binding_just_pressed(\"{{id}}_open\") then",
        "    game.widgets.open(\"{{name}}\", function()",
        "      return { type = \"column\", children = { { type = \"label\", text = \"Hello from {{name}}\" } } }",
        "    end, nil, { width = 320, height = 160 })",
        "  end",
        "end)",
        "",
      ].join("\n"),
    },
  },
];

function fill(text, name, id) {
  return text.replace(/\{\{name\}\}/g, name).replace(/\{\{id\}\}/g, id);
}

module.exports = { templates, fill };
