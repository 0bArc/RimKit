#include <cctype>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <algorithm>
#include <regex>
#include <sstream>
#include <string>
#include <vector>

#include "aliases.hpp"
#include "lua_file.hpp"
#include "tools.hpp"

#define SOL_ALL_SAFETIES_ON 1
#include <sol/sol.hpp>

namespace fs = std::filesystem;

static void usage() {
    std::cout
        << "rimkit - RimKit CLI\n\n"
        << "  rimkit init [name]              Init Lua mod in cwd (or ./name)\n"
        << "  rimkit build                    Build native core + C# host\n"
        << "  rimkit mod create <name> [dir]  Create Lua mod folder\n"
        << "  rimkit mod sync [path]          meta.lua -> About/About.xml, Defs/*.lua and Languages/**/*.lua -> XML\n"
        << "  rimkit mod defs [dir]           Only the Lua to XML step, for a folder with no meta.lua\n"
        << "  rimkit mod ship [path] [mods]   Sync + copy mod into RimWorld Mods/\n"
        << "  rimkit mod check [path]         Validate meta, Lua, Defs, patches, textures and translation keys\n"
        << "  rimkit mod test [path]          Run Tests/*.luau against a mock host (no game needed)\n"
        << "  rimkit mod gen-tests [path]     Write Tests/generated_test.luau from the events the mod registers (--force to overwrite)\n"
        << "  rimkit mod assets [path] [--fix]  Check the Workshop preview and mod icon, make placeholders\n"
        << "  rimkit mod i18n <cmd> [path]    extract | missing <lang> | export <lang> | import <lang> <csv>\n"
        << "  rimkit mod release-check [path] Checklist before publishing: version, changelog, licence, credits, assets\n"
        << "  rimkit publish [path]           Upload to the Steam Workshop (--note, --user, --steamcmd, --dry-run)\n"
        << "  rimkit diag [--fresh]           Zip the log, mod list and profiler numbers for a bug report (no network)\n"
        << "  rimkit help\n";
}

static std::string xml_escape(const std::string& s) {
    std::string out;
    for (char c : s) {
        switch (c) {
            case '&':
                out += "&amp;";
                break;
            case '<':
                out += "&lt;";
                break;
            case '>':
                out += "&gt;";
                break;
            case '"':
                out += "&quot;";
                break;
            default:
                out += c;
                break;
        }
    }
    return out;
}

static std::string default_author() {
    return "Team Stratware.win";
}

static std::string field_str(const sol::table& t, const char* a, const char* b = nullptr) {
    sol::object o = t[a];
    if (!o.valid() || o.get_type() == sol::type::nil) {
        if (b) {
            o = t[b];
        }
    }
    if (!o.valid() || o.get_type() == sol::type::nil) {
        return {};
    }
    if (o.is<std::string>()) {
        return o.as<std::string>();
    }
    if (o.is<double>()) {
        return std::to_string(static_cast<int>(o.as<double>()));
    }
    return o.as<std::string>();
}

static std::vector<std::string> field_string_list(const sol::table& t, const char* a, const char* b = nullptr) {
    std::vector<std::string> out;
    sol::object o = t[a];
    if (!o.valid() || o.get_type() == sol::type::nil) {
        if (b) {
            o = t[b];
        }
    }
    if (!o.valid() || o.get_type() != sol::type::table) {
        if (o.valid() && o.get_type() == sol::type::string) {
            out.push_back(o.as<std::string>());
        }
        return out;
    }
    sol::table arr = o.as<sol::table>();
    for (const auto& kv : arr) {
        sol::object val = kv.second;
        if (val.is<std::string>()) {
            out.push_back(val.as<std::string>());
        } else if (val.get_type() == sol::type::table) {
            sol::table dep = val.as<sol::table>();
            std::string id = field_str(dep, "id", "package_id");
            if (id.empty()) {
                id = field_str(dep, "packageId");
            }
            if (!id.empty()) {
                out.push_back(id);
            }
        }
    }
    return out;
}

struct DepInfo {
    std::string id;
    std::string name;
    std::string steam;
    std::string download;
};

static std::vector<DepInfo> field_deps(const sol::table& t) {
    std::vector<DepInfo> out;
    sol::object o = t["depends"];
    if (!o.valid() || o.get_type() == sol::type::nil) {
        o = t["dependencies"];
    }
    if (!o.valid() || o.get_type() == sol::type::nil) {
        o = t["Depends"];
    }
    if (!o.valid() || o.get_type() != sol::type::table) {
        return out;
    }
    sol::table arr = o.as<sol::table>();
    for (const auto& kv : arr) {
        sol::object val = kv.second;
        DepInfo d;
        if (val.is<std::string>()) {
            d.id = val.as<std::string>();
            if (d.id == "brrainz.harmony") {
                d.name = "Harmony";
                d.steam = "steam://url/CommunityFilePage/2009463077";
            } else if (d.id == "stratware.rimkit" || d.id == "sandk.rimluakit") {
                d.id = "stratware.rimkit";
                d.name = "RimKit";
                d.download = "https://github.com/stratware/RimWorldModKit";
            } else if (d.id == "stratware.pauth") {
                d.name = "pAuth";
                d.download = "https://github.com/stratware/RimWorldModKit";
            } else {
                d.name = d.id;
            }
            out.push_back(d);
        } else if (val.get_type() == sol::type::table) {
            sol::table dep = val.as<sol::table>();
            d.id = field_str(dep, "id", "package_id");
            if (d.id.empty()) {
                d.id = field_str(dep, "packageId");
            }
            d.name = field_str(dep, "name", "displayName");
            d.steam = field_str(dep, "steam", "steamWorkshopUrl");
            d.download = field_str(dep, "download", "downloadUrl");
            if (d.name.empty()) {
                d.name = d.id;
            }
            if (d.download.empty()) {
                if (d.id == "stratware.rimkit" || d.id == "stratware.pauth") {
                    d.download = "https://github.com/stratware/RimWorldModKit";
                } else if (d.id == "brrainz.harmony") {
                    d.download = "https://github.com/pardeike/HarmonyRimWorld";
                    if (d.steam.empty()) {
                        d.steam = "steam://url/CommunityFilePage/2009463077";
                    }
                }
            }
            if (d.name == d.id || d.name.empty()) {
                if (d.id == "stratware.rimkit") d.name = "RimKit";
                if (d.id == "stratware.pauth") d.name = "pAuth";
                if (d.id == "brrainz.harmony") d.name = "Harmony";
            }
            if (!d.id.empty()) {
                out.push_back(d);
            }
        }
    }
    return out;
}

static fs::path find_meta_lua(const fs::path& modDir) {
    const fs::path candidates[] = {
        modDir / "meta.lua",
        modDir / "Lua" / "meta.lua",
        modDir / "About" / "meta.lua",
    };
    for (const auto& p : candidates) {
        if (fs::exists(p)) {
            return p;
        }
    }
    return {};
}

static int write_about_xml(const fs::path& modDir, const sol::table& meta) {
    std::string name = field_str(meta, "name", "Name");
    std::string author = field_str(meta, "author", "Author");
    std::string packageId = field_str(meta, "package_id", "packageId");
    if (packageId.empty()) {
        packageId = field_str(meta, "PackageId");
    }
    std::string description = field_str(meta, "description", "Description");
    std::vector<std::string> versions = field_string_list(meta, "supported_versions", "supportedVersions");
    if (versions.empty()) {
        sol::object vf = meta["version_folders"];
        if (vf.get_type() == sol::type::table) {
            for (const auto& kv : vf.as<sol::table>()) {
                if (kv.first.is<std::string>()) {
                    versions.push_back(kv.first.as<std::string>());
                }
            }
            std::sort(versions.begin(), versions.end());
        }
    }
    if (versions.empty()) {
        std::string ver = field_str(meta, "version", "Version");
        if (!ver.empty()) {
            versions.push_back(ver);
        }
    }
    if (versions.empty()) {
        versions.push_back("1.6");
    }
    auto deps = field_deps(meta);
    auto loadAfter = field_string_list(meta, "load_after", "loadAfter");
    if (loadAfter.empty()) {
        loadAfter = field_string_list(meta, "LoadAfter");
    }
    if (loadAfter.empty()) {
        for (const auto& d : deps) {
            loadAfter.push_back(d.id);
        }
    }

    if (name.empty() || packageId.empty()) {
        std::cerr << "meta.lua missing name/package_id\n";
        return 1;
    }
    if (author.empty()) {
        author = default_author();
    }
    if (description.empty()) {
        description = name;
    }

    fs::path aboutDir = modDir / "About";
    fs::create_directories(aboutDir);
    fs::path outPath = aboutDir / "About.xml";
    std::ofstream f(outPath);
    if (!f) {
        std::cerr << "Cannot write " << outPath << "\n";
        return 1;
    }

    f << "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n";
    f << "<!-- Generated by rimkit mod sync. Do not edit. -->\n";
    f << "<ModMetaData>\n";
    f << "  <name>" << xml_escape(name) << "</name>\n";
    f << "  <author>" << xml_escape(author) << "</author>\n";
    f << "  <supportedVersions>\n";
    for (const auto& v : versions) {
        f << "    <li>" << xml_escape(v) << "</li>\n";
    }
    f << "  </supportedVersions>\n";
    f << "  <packageId>" << xml_escape(packageId) << "</packageId>\n";
    {
        const std::string modVersion = field_str(meta, "mod_version", "modVersion");
        if (!modVersion.empty()) {
            f << "  <modVersion>" << xml_escape(modVersion) << "</modVersion>\n";
        }
        const std::string url = field_str(meta, "url");
        if (!url.empty()) {
            f << "  <url>" << xml_escape(url) << "</url>\n";
        }
    }
    f << "  <description>" << xml_escape(description) << "</description>\n";
    if (!deps.empty()) {
        f << "  <modDependencies>\n";
        for (const auto& d : deps) {
            f << "    <li>\n";
            f << "      <packageId>" << xml_escape(d.id) << "</packageId>\n";
            f << "      <displayName>" << xml_escape(d.name) << "</displayName>\n";
            if (!d.steam.empty()) {
                f << "      <steamWorkshopUrl>" << xml_escape(d.steam) << "</steamWorkshopUrl>\n";
            }
            if (!d.download.empty()) {
                f << "      <downloadUrl>" << xml_escape(d.download) << "</downloadUrl>\n";
            }
            f << "    </li>\n";
        }
        f << "  </modDependencies>\n";
    }
    if (!loadAfter.empty()) {
        f << "  <loadAfter>\n";
        for (const auto& id : loadAfter) {
            f << "    <li>" << xml_escape(id) << "</li>\n";
        }
        f << "  </loadAfter>\n";
    }
    {
        auto before = field_string_list(meta, "load_before", "loadBefore");
        if (!before.empty()) {
            f << "  <loadBefore>\n";
            for (const auto& id : before) {
                f << "    <li>" << xml_escape(id) << "</li>\n";
            }
            f << "  </loadBefore>\n";
        }
        auto incompatible = field_string_list(meta, "incompatible_with", "incompatibleWith");
        if (!incompatible.empty()) {
            f << "  <incompatibleWith>\n";
            for (const auto& id : incompatible) {
                f << "    <li>" << xml_escape(id) << "</li>\n";
            }
            f << "  </incompatibleWith>\n";
        }
    }
    f << "</ModMetaData>\n";
    std::cout << "Wrote " << outPath << "\n";
    return 0;
}

static std::string field_str_table(const sol::table& t, const char* a, const char* b = nullptr) {
    sol::object o = t[a];
    if (o.is<std::string>()) {
        return o.as<std::string>();
    }
    if (b) {
        o = t[b];
        if (o.is<std::string>()) {
            return o.as<std::string>();
        }
    }
    return {};
}

static int write_defs_from_lua(const fs::path& modDir) {
    fs::path defsLua;
    if (fs::exists(modDir / "defs.lua")) {
        defsLua = modDir / "defs.lua";
    } else if (fs::exists(modDir / "Lua" / "defs.lua")) {
        defsLua = modDir / "Lua" / "defs.lua";
    } else {
        return 0;
    }

    sol::state lua;
    rimlua::open_cli_libs(lua);
    sol::protected_function_result result = rimlua::run_lua_file(lua, defsLua.string());
    if (!result.valid()) {
        sol::error e = result;
        std::cerr << "defs.lua error: " << e.what() << "\n";
        return 1;
    }
    if (result.return_count() == 0 || result.get_type(0) != sol::type::table) {
        std::cerr << "defs.lua must return a table\n";
        return 1;
    }
    sol::table root = result.get<sol::table>(0);
    sol::object thingsObj = root["things"];
    if (!thingsObj.is<sol::table>()) {
        return 0;
    }
    sol::table things = thingsObj.as<sol::table>();
    fs::path outDir = modDir / "Defs" / "ThingDefs";
    fs::create_directories(outDir);
    fs::path outPath = outDir / "RimLua_FromDefsLua.xml";
    std::ofstream f(outPath);
    if (!f) {
        std::cerr << "Cannot write " << outPath << "\n";
        return 1;
    }
    f << "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\n";
    f << "<!-- Generated by rimkit mod sync from defs.lua. Do not edit. -->\n";
    f << "<Defs>\n";
    for (const auto& kv : things) {
        if (!kv.second.is<sol::table>()) {
            continue;
        }
        sol::table t = kv.second.as<sol::table>();
        std::string defName = field_str_table(t, "defName", "def_name");
        if (defName.empty()) {
            continue;
        }
        std::string label = field_str_table(t, "label");
        if (label.empty()) {
            label = defName;
        }
        std::string desc = field_str_table(t, "description");
        if (desc.empty()) {
            desc = label;
        }
        std::string tex = field_str_table(t, "texPath", "tex_path");
        if (tex.empty()) {
            tex = "Things/Item/Resource/Steel";
        }
        int stack = 75;
        sol::object stackObj = t["stackLimit"];
        if (stackObj.is<int>()) {
            stack = stackObj.as<int>();
        } else if (stackObj.is<double>()) {
            stack = static_cast<int>(stackObj.as<double>());
        }
        f << "  <ThingDef ParentName=\"ResourceBase\">\n";
        f << "    <defName>" << xml_escape(defName) << "</defName>\n";
        f << "    <label>" << xml_escape(label) << "</label>\n";
        f << "    <description>" << xml_escape(desc) << "</description>\n";
        f << "    <graphicData>\n";
        f << "      <texPath>" << xml_escape(tex) << "</texPath>\n";
        f << "      <graphicClass>Graphic_StackCount</graphicClass>\n";
        f << "    </graphicData>\n";
        f << "    <stackLimit>" << stack << "</stackLimit>\n";
        f << "    <statBases>\n";
        f << "      <MaxHitPoints>50</MaxHitPoints>\n";
        f << "      <MarketValue>2</MarketValue>\n";
        f << "      <Mass>0.05</Mass>\n";
        f << "    </statBases>\n";
        f << "    <thingCategories>\n";
        f << "      <li>ResourcesRaw</li>\n";
        f << "    </thingCategories>\n";
        f << "  </ThingDef>\n";
    }
    f << "</Defs>\n";
    std::cout << "Wrote " << outPath << "\n";
    return 0;
}

// ---------------------------------------------------------------- Defs written in Lua
// Any Defs/*.lua file is a script that calls def(kind, defName, fields, opts). rimkit mod sync turns it into Defs/<name>.xml, so a mod
// can be written entirely in Lua and the game still reads ordinary Def XML. Fields are plain tables: a key _class sets the Class
// attribute, a list becomes <li> items, nested tables become nested elements. opts: parent, name, abstract.
//
//   def("ThingDef", "MyChair", { label = "my chair", statBases = { Comfort = 0.8 } }, { parent = "FurnitureBase" })
//
// Helpers inside the script: vec(x, z) gives "(x,z)", rgb(r, g, b) gives "(r,g,b)".

static const char* kLuaDefsMarker = "<!-- Generated by rimkit mod sync from ";

static void lua_value_to_xml(std::ostringstream& out, const std::string& name, const sol::object& v, int depth, bool is_li = false) {
    const std::string pad(static_cast<size_t>(depth) * 2, ' ');
    auto open_tag = [&](const std::string& extra) { out << pad << "<" << name << extra << ">"; };
    if (v.get_type() == sol::type::table) {
        sol::table t = v.as<sol::table>();
        std::string class_attr;
        sol::object cls = t["_class"];
        if (cls.get_type() == sol::type::string) {
            class_attr = " Class=\"" + xml_escape(cls.as<std::string>()) + "\"";
        }
        std::string attrs;
        sol::object attr_obj = t["_attrs"];
        if (attr_obj.get_type() == sol::type::table) {
            std::vector<std::pair<std::string, std::string>> list;
            for (const auto& kv : attr_obj.as<sol::table>()) {
                if (kv.first.is<std::string>()) {
                    list.emplace_back(kv.first.as<std::string>(), kv.second.is<std::string>() ? kv.second.as<std::string>() : std::to_string(kv.second.as<double>()));
                }
            }
            std::sort(list.begin(), list.end());
            for (const auto& a : list) {
                attrs += " " + a.first + "=\"" + xml_escape(a.second) + "\"";
            }
        }
        const size_t n = t.size();
        bool has_string_key = false;
        for (const auto& kv : t) {
            if (kv.first.is<std::string>()) {
                has_string_key = true;
            }
        }
        open_tag(class_attr + attrs);
        if (n > 0 && !has_string_key) {
            out << "\n";
            for (size_t i = 1; i <= n; ++i) {
                sol::object item = t[i];
                lua_value_to_xml(out, "li", item, depth + 1, true);
            }
            out << pad;
        } else {
            std::vector<std::string> keys;
            for (const auto& kv : t) {
                if (kv.first.is<std::string>()) {
                    const std::string k = kv.first.as<std::string>();
                    if (k != "_class" && k != "_attrs") {
                        keys.push_back(k);
                    }
                }
            }
            std::sort(keys.begin(), keys.end());
            if (!keys.empty()) {
                out << "\n";
            }
            for (const std::string& k : keys) {
                sol::object child = t[k];
                lua_value_to_xml(out, k, child, depth + 1);
            }
            if (!keys.empty()) {
                out << pad;
            }
        }
        out << "</" << name << ">\n";
        return;
    }
    open_tag("");
    if (v.get_type() == sol::type::boolean) {
        out << (v.as<bool>() ? "true" : "false");
    } else if (v.get_type() == sol::type::number) {
        const double d = v.as<double>();
        if (d == static_cast<double>(static_cast<long long>(d))) {
            out << static_cast<long long>(d);
        } else {
            std::ostringstream num;
            num.precision(9);
            num << d;
            out << num.str();
        }
    } else if (v.get_type() == sol::type::string) {
        out << xml_escape(v.as<std::string>());
    }
    out << "</" << name << ">\n";
    (void)is_li;
}

static int write_lua_defs_files(const fs::path& modDir) {
    const fs::path defsDir = modDir / "Defs";
    if (!fs::exists(defsDir)) {
        return 0;
    }
    std::vector<fs::path> scripts;
    std::error_code ec;
    for (auto it = fs::recursive_directory_iterator(defsDir, ec); !ec && it != fs::recursive_directory_iterator(); it.increment(ec)) {
        if (it->is_regular_file(ec) && it->path().extension() == ".lua") {
            scripts.push_back(it->path());
        }
    }
    std::sort(scripts.begin(), scripts.end());
    for (const fs::path& script : scripts) {
        fs::path out_path = script;
        out_path.replace_extension(".xml");
        if (fs::exists(out_path)) {
            std::ifstream existing(out_path);
            std::string first_lines, line;
            for (int i = 0; i < 3 && std::getline(existing, line); ++i) {
                first_lines += line + "\n";
            }
            if (first_lines.find(kLuaDefsMarker) == std::string::npos) {
                std::cerr << out_path.string() << " exists and was not generated from Lua. Delete it or rename the .lua file.\n";
                return 1;
            }
        }
        sol::state lua;
        rimlua::open_cli_libs(lua);
        std::ostringstream body;
        int count = 0;
        std::string failure;
        lua["vec"] = [](double x, double z) {
            std::ostringstream s;
            s << "(" << x << "," << z << ")";
            return s.str();
        };
        lua["rgb"] = [](double r, double g, double b) {
            std::ostringstream s;
            s << "(" << r << "," << g << "," << b << ")";
            return s.str();
        };
        lua["def"] = [&](const std::string& kind, const std::string& def_name, sol::optional<sol::table> fields, sol::optional<sol::table> opts) {
            if (kind.empty() || def_name.empty()) {
                failure = "def needs a kind and a defName";
                return;
            }
            std::string attrs;
            if (opts) {
                sol::table o = *opts;
                sol::object parent = o["parent"], name = o["name"], abstract_obj = o["abstract"];
                if (parent.get_type() == sol::type::string) attrs += " ParentName=\"" + xml_escape(parent.as<std::string>()) + "\"";
                if (name.get_type() == sol::type::string) attrs += " Name=\"" + xml_escape(name.as<std::string>()) + "\"";
                if (abstract_obj.get_type() == sol::type::boolean && abstract_obj.as<bool>()) attrs += " Abstract=\"True\"";
            }
            body << "  <" << kind << attrs << ">\n    <defName>" << xml_escape(def_name) << "</defName>\n";
            if (fields) {
                std::vector<std::string> keys;
                for (const auto& kv : *fields) {
                    if (kv.first.is<std::string>()) keys.push_back(kv.first.as<std::string>());
                }
                std::sort(keys.begin(), keys.end());
                for (const std::string& k : keys) {
                    sol::object child = (*fields)[k];
                    lua_value_to_xml(body, k, child, 2);
                }
            }
            body << "  </" << kind << ">\n\n";
            ++count;
        };
        sol::protected_function_result r = rimlua::run_lua_file(lua, script.string());
        if (!r.valid()) {
            sol::error e = r;
            std::cerr << script.string() << ": " << e.what() << "\n";
            return 1;
        }
        if (!failure.empty()) {
            std::cerr << script.string() << ": " << failure << "\n";
            return 1;
        }
        std::ofstream f(out_path);
        if (!f) {
            std::cerr << "Cannot write " << out_path.string() << "\n";
            return 1;
        }
        f << "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
          << kLuaDefsMarker << script.filename().string() << ". Edit the .lua file, not this one. -->\n<Defs>\n\n"
          << body.str() << "</Defs>\n";
        std::cout << "Wrote " << out_path.string() << " (" << count << " def(s) from " << script.filename().string() << ")\n";
    }
    return 0;
}

// ---------------------------------------------------------------- Strings written in Lua
// Languages/<Language>/Keyed/*.lua returns a table of key = text. rimkit mod sync writes the LanguageData XML next to it, so the
// translation files can be Lua too:   return { MyMod_Hello = "Hello {0}", MyMod_Bye = "Goodbye" }

static int write_lua_language_files(const fs::path& modDir) {
    const fs::path langDir = modDir / "Languages";
    if (!fs::exists(langDir)) {
        return 0;
    }
    std::vector<fs::path> scripts;
    std::error_code ec;
    for (auto it = fs::recursive_directory_iterator(langDir, ec); !ec && it != fs::recursive_directory_iterator(); it.increment(ec)) {
        if (it->is_regular_file(ec) && it->path().extension() == ".lua") {
            scripts.push_back(it->path());
        }
    }
    std::sort(scripts.begin(), scripts.end());
    for (const fs::path& script : scripts) {
        fs::path out_path = script;
        out_path.replace_extension(".xml");
        if (fs::exists(out_path)) {
            std::ifstream existing(out_path);
            std::string first_lines, line;
            for (int i = 0; i < 3 && std::getline(existing, line); ++i) {
                first_lines += line + "\n";
            }
            if (first_lines.find(kLuaDefsMarker) == std::string::npos) {
                std::cerr << out_path.string() << " exists and was not generated from Lua. Delete it or rename the .lua file.\n";
                return 1;
            }
        }
        sol::state lua;
        rimlua::open_cli_libs(lua);
        sol::protected_function_result r = rimlua::run_lua_file(lua, script.string());
        if (!r.valid()) {
            sol::error e = r;
            std::cerr << script.string() << ": " << e.what() << "\n";
            return 1;
        }
        if (r.return_count() == 0 || r.get_type(0) != sol::type::table) {
            std::cerr << script.string() << " must return a table of key = text\n";
            return 1;
        }
        sol::table t = r.get<sol::table>(0);
        std::vector<std::pair<std::string, std::string>> rows;
        for (const auto& kv : t) {
            if (!kv.first.is<std::string>()) {
                continue;
            }
            const std::string key = kv.first.as<std::string>();
            const std::regex valid_key("^[A-Za-z_][A-Za-z0-9_.\\-]*$");
            if (!std::regex_match(key, valid_key)) {
                std::cerr << script.string() << ": \"" << key << "\" is not a valid key (letters, digits, underscore, dot)\n";
                return 1;
            }
            if (!kv.second.is<std::string>()) {
                std::cerr << script.string() << ": " << key << " must be a string\n";
                return 1;
            }
            rows.emplace_back(key, kv.second.as<std::string>());
        }
        std::sort(rows.begin(), rows.end());
        std::ofstream f(out_path);
        if (!f) {
            std::cerr << "Cannot write " << out_path.string() << "\n";
            return 1;
        }
        f << "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
          << kLuaDefsMarker << script.filename().string() << ". Edit the .lua file, not this one. -->\n<LanguageData>\n";
        for (const auto& row : rows) {
            f << "  <" << row.first << ">" << xml_escape(row.second) << "</" << row.first << ">\n";
        }
        f << "</LanguageData>\n";
        std::cout << "Wrote " << out_path.string() << " (" << rows.size() << " string(s) from " << script.filename().string() << ")\n";
    }
    return 0;
}

static int sync_mod(const fs::path& modDir) {
    fs::path metaPath = find_meta_lua(modDir);
    if (metaPath.empty()) {
        std::cerr << "No meta.lua in " << modDir << "\n";
        return 1;
    }

    sol::state lua;
    rimlua::open_cli_libs(lua);

    sol::table metaTable = lua.create_table();
    sol::table host = lua.create_table();
    host["metadata"] = metaTable;
    lua["host"] = host;
    lua["package"]["preload"]["host.metadata"] = [metaTable](sol::this_state) { return metaTable; };

    sol::protected_function_result result = rimlua::run_lua_file(lua, metaPath.string());
    if (!result.valid()) {
        sol::error e = result;
        std::cerr << "meta.lua error: " << e.what() << "\n";
        return 1;
    }

    sol::table meta = metaTable;
    if (result.return_count() > 0 && result.get_type(0) == sol::type::table) {
        meta = result.get<sol::table>(0);
    }
    int rc = write_about_xml(modDir, meta);
    if (rc != 0) {
        return rc;
    }
    rc = rkcli::write_extras(modDir);
    if (rc != 0) {
        return rc;
    }
    rc = write_defs_from_lua(modDir);
    if (rc != 0) {
        return rc;
    }
    rc = write_lua_defs_files(modDir);
    if (rc != 0) {
        return rc;
    }
    return write_lua_language_files(modDir);
}

// Lowercase letters and digits only, for package ids.
static std::string id_slug(const std::string& s, const char* fallback) {
    std::string out;
    for (char c : s) {
        if (std::isalnum(static_cast<unsigned char>(c))) {
            out.push_back(static_cast<char>(std::tolower(static_cast<unsigned char>(c))));
        }
    }
    return out.empty() ? std::string(fallback) : out;
}

// A new mod written the way RimKit mods are written today: game.* names, a translated message, dependencies with download links so the
// game does not warn, an author based package id, and a first test that checks the template's own behaviour.
static int create_mod_at(const fs::path& out, const std::string& name) {
    if (fs::exists(out / "meta.lua")) {
        std::cerr << "Already exists: " << (out / "meta.lua") << "\n";
        return 1;
    }
    const char* author_env = std::getenv("RIMKIT_AUTHOR");
    const std::string author = author_env && *author_env ? author_env : "Your Name";
    const std::string slug = id_slug(name, "mymod");
    const std::string package_id = id_slug(author, "yourname") + "." + slug;
    fs::create_directories(out / "Lua");
    fs::create_directories(out / "Languages" / "English" / "Keyed");
    {
        std::ofstream f(out / "meta.lua");
        f << "local meta = require(\"host.metadata\")\n"
          << "meta.name = \"" << name << "\"\n"
          << "meta.author = \"" << author << "\"\n"
          << "meta.package_id = \"" << package_id << "\"\n"
          << "meta.version = \"1.6\"                -- the RimWorld version the mod is for\n"
          << "meta.mod_version = \"0.1.0\"          -- the mod's own version, bump it for every release\n"
          << "meta.description = \"Replace this with one clear sentence about what the mod does for the player.\"\n"
          << "meta.api_level = 1                 -- strict mode: errors raise, old API names are refused, only declared capabilities work\n"
          << "-- What the Lua may do. hooks: game.hooks and game.tweaks. reflect: game.reflect. files: write Defs and patches. dev: evaluate code.\n"
          << "meta.capabilities = {}\n"
          << "meta.depends = {\n"
          << "  {\n"
          << "    id = \"brrainz.harmony\",\n"
          << "    name = \"Harmony\",\n"
          << "    steam = \"steam://url/CommunityFilePage/2009463077\",\n"
          << "    download = \"https://github.com/pardeike/HarmonyRimWorld\",\n"
          << "  },\n"
          << "  {\n"
          << "    id = \"stratware.rimkit\",\n"
          << "    name = \"RimKit\",\n"
          << "    steam = \"steam://url/CommunityFilePage/3811629229\",\n"
          << "    download = \"https://steamcommunity.com/sharedfiles/filedetails/?id=3811629229\",\n"
          << "  },\n"
          << "}\n"
          << "meta.load_after = { \"ludeon.rimworld\", \"brrainz.harmony\", \"stratware.rimkit\" }\n"
          << "return meta\n";
    }
    rkcli::scaffold_extras(out, name, package_id);
    {
        std::ofstream f(out / "Lua" / "main.luau");
        f << "-- " << name << "\n"
          << "-- Starts here. The game runs this file when a game is loaded.\n"
          << "-- Read the docs for every game.* function: infrastructure/docs/api, or hover over it in VS Code with the RimKit extension.\n\n"
          << "game.events.on_load(function()\n"
          << "  game.log.info(\"[" << name << "] loaded\")\n"
          << "  game.ui.message(game.ui.translate(\"" << slug << "_Loaded\"))\n"
          << "end)\n";
    }
    {
        // Text lives in Keyed files so the mod can be translated. game.ui.translate reads it.
        // Written in Lua like the rest of the mod: rimkit mod sync turns it into the LanguageData XML the game reads.
        std::ofstream f(out / "Languages" / "English" / "Keyed" / (name + ".lua"));
        std::string quoted;
        for (char c : name) {
            if (c == '"' || c == '\\') quoted += '\\';
            quoted += c;
        }
        f << "-- Strings for the English language. rimkit mod sync turns this file into " << name << ".xml, which the game reads.\n"
          << "-- Edit this file, not the XML. {0}, {1} are filled in by game.ui.translate(key, a, b).\n"
          << "return {\n"
          << "  " << slug << "_Loaded = \"" << quoted << " is loaded.\",\n"
          << "}\n";
    }
    std::cout << "Created " << out << "\n";
    std::cout << "Next: set RIMKIT_AUTHOR (or edit meta.lua) so the package id carries your name, then rimkit mod test and rimkit mod ship.\n";
    return sync_mod(out);
}

static void copy_tree(const fs::path& from, const fs::path& to) {
    fs::create_directories(to);
    for (auto it = fs::recursive_directory_iterator(from); it != fs::recursive_directory_iterator(); ++it) {
        const fs::path rel = fs::relative(it->path(), from);
        const fs::path dest = to / rel;
        if (it->is_directory()) {
            fs::create_directories(dest);
        } else if (it->is_regular_file()) {
            fs::create_directories(dest.parent_path());
            fs::copy_file(it->path(), dest, fs::copy_options::overwrite_existing);
        }
    }
}

static bool is_ship_root_name(const std::string& name) {
    static const char* keep[] = {"About", "Assemblies", "Native", "Lua", "Defs", "Patches", "Textures",
                                 "Sounds", "Languages", "News", "Auth", "LoadFolders.xml", "meta.lua",
                                 "LICENSE", "LICENSE.md", "LICENSE.txt", "CREDITS.md", "CHANGELOG.md"};
    for (const char* k : keep) {
        if (name == k) {
            return true;
        }
    }
    return false;
}

static void copy_mod_payload(const fs::path& from, const fs::path& to) {
    fs::create_directories(to);
    for (auto it = fs::directory_iterator(from); it != fs::directory_iterator(); ++it) {
        const std::string name = it->path().filename().string();
        if (name.size() && name[0] == '.') {
            continue;
        }
        if (!is_ship_root_name(name)) {
            continue;
        }
        const fs::path dest = to / name;
        if (it->is_directory()) {
            copy_tree(it->path(), dest);
        } else if (it->is_regular_file()) {
            fs::copy_file(it->path(), dest, fs::copy_options::overwrite_existing);
        }
    }
}

static fs::path default_mods_dir() {
    if (const char* env = std::getenv("RIMWORLD_MODS")) {
        fs::path p(env);
        if (fs::exists(p)) {
            return p;
        }
    }
    const char* home = std::getenv("USERPROFILE");
    std::vector<fs::path> candidates;
    if (home) {
        candidates.push_back(fs::path(home) / "AppData" / "LocalLow" / "Ludeon Studios" / "RimWorld by Ludeon Studios" /
                             "Mods");
    }
    candidates.push_back(R"(C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods)");
    candidates.push_back(R"(C:\Program Files\Steam\steamapps\common\RimWorld\Mods)");
    candidates.push_back(R"(D:\SteamLibrary\steamapps\common\RimWorld\Mods)");
    candidates.push_back(R"(E:\SteamLibrary\steamapps\common\RimWorld\Mods)");
    for (const auto& p : candidates) {
        if (fs::exists(p)) {
            return p;
        }
    }
    return {};
}

static fs::path kit_root_from_exe() {
    fs::path cur = fs::current_path();
    // Repo root: src/native + mod/meta.lua (shippable package lives under mod/).
    if (fs::exists(cur / "mod" / "meta.lua") && fs::exists(cur / "src" / "native")) {
        return cur;
    }
    if (fs::exists(cur / ".." / "mod" / "meta.lua") && fs::exists(cur / ".." / "src" / "native")) {
        return (cur / "..").lexically_normal();
    }
    if (fs::exists(cur / ".." / ".." / ".." / "mod" / "meta.lua")) {
        return (cur / ".." / ".." / "..").lexically_normal();
    }
    // Legacy: package files still at repo root.
    if (fs::exists(cur / "meta.lua") && fs::exists(cur / "src" / "native")) {
        return cur;
    }
    return cur;
}

static fs::path kit_package_dir(const fs::path& root) {
    fs::path nested = root / "mod";
    if (fs::exists(nested / "meta.lua")) {
        return nested;
    }
    return root;
}

// When shipping the kit repo itself (`.` or the repo root), use mod/ as the package.
static fs::path resolve_ship_dir(fs::path dir) {
    fs::path abs = fs::absolute(dir).lexically_normal();
    if (fs::exists(abs / "mod" / "meta.lua") && fs::exists(abs / "src" / "native")) {
        return abs / "mod";
    }
    if (fs::exists(abs / "meta.lua") && fs::exists(abs / "src" / "native") && !fs::exists(abs / "mod" / "meta.lua")) {
        return abs;
    }
    return dir;
}

static bool looks_like_lua(const std::string& text) {
    if (text.find("using ") != std::string::npos && text.find("namespace ") != std::string::npos) {
        return false;
    }
    return text.find("function") != std::string::npos || text.find("rim.") != std::string::npos ||
           text.find("local ") != std::string::npos || text.find("host.metadata") != std::string::npos;
}

static int check_file(const fs::path& path) {
    std::ifstream in(path);
    if (!in) {
        std::cerr << "Cannot read " << path << "\n";
        return 1;
    }
    std::string content((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
    if (!looks_like_lua(content)) {
        std::cerr << "FAIL " << path << "\n";
        return 1;
    }
    std::cout << "OK " << path << "\n";
    return 0;
}

static int cmd_mod_check(const fs::path& target) {
    if (!fs::exists(target)) {
        std::cerr << "Path not found: " << target << "\n";
        return 1;
    }
    if (fs::is_regular_file(target)) {
        return check_file(target);
    }
    int failures = 0;
    fs::path meta = find_meta_lua(target);
    if (!meta.empty()) {
        failures += check_file(meta);
        failures += sync_mod(target) == 0 ? 0 : 1;
    } else {
        std::cerr << "No meta.lua\n";
        failures++;
    }
    failures += rkcli::content_check(target, false) == 0 ? 0 : 1;
    fs::path lua_root = target / "Lua";
    if (!fs::exists(lua_root)) {
        lua_root = target;
    }
    for (auto it = fs::recursive_directory_iterator(lua_root); it != fs::recursive_directory_iterator(); ++it) {
        if (it->is_regular_file() && (it->path().extension() == ".lua" || it->path().extension() == ".luau") && it->path().filename() != "meta.lua") {
            failures += check_file(it->path());
        }
    }
    return failures == 0 ? 0 : 1;
}

static std::string sha256_file_powershell(const fs::path& path) {
    if (!fs::exists(path)) {
        return {};
    }
    // Prefer PowerShell Get-FileHash (always on Win RimWorld hosts).
    fs::path tmp = fs::temp_directory_path() / "rimkit_sha256.txt";
    std::string cmd = "powershell -NoProfile -Command \"(Get-FileHash -LiteralPath '" + path.string() +
                      "' -Algorithm SHA256).Hash.ToLower() | Set-Content -LiteralPath '" + tmp.string() + "'\"";
    if (std::system(cmd.c_str()) != 0) {
        return {};
    }
    std::ifstream in(tmp);
    std::string hash;
    std::getline(in, hash);
    while (!hash.empty() && (hash.back() == '\r' || hash.back() == '\n' || hash.back() == ' ')) {
        hash.pop_back();
    }
    for (char& c : hash) {
        c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    }
    return hash;
}

static fs::path find_harmony_dll() {
    const char* env = std::getenv("HARMONY_PATH");
    if (env && fs::exists(env)) {
        return fs::path(env);
    }
    std::vector<fs::path> candidates = {
        fs::path("C:/Program Files (x86)/Steam/steamapps/workshop/content/294100/2009463077/Current/Assemblies/0Harmony.dll"),
        fs::path("C:/Program Files (x86)/Steam/steamapps/workshop/content/294100/2009463077/1.5/Assemblies/0Harmony.dll"),
        fs::path("C:/Program Files (x86)/Steam/steamapps/workshop/content/294100/2009463077/1.4/Assemblies/0Harmony.dll"),
        fs::path("C:/Program Files (x86)/Steam/steamapps/workshop/content/294100/2009463077/Assemblies/0Harmony.dll"),
    };
    for (const auto& p : candidates) {
        if (fs::exists(p)) {
            return p;
        }
    }
    return {};
}

static int regenerate_allowlist(const fs::path& root) {
    fs::path pkg = kit_package_dir(root);
    fs::path host = pkg / "Assemblies" / "RimLuaHost.dll";
    fs::path native = pkg / "Native" / "rimlua_core.dll";
    fs::path harmony = find_harmony_dll();
    if (!fs::exists(host) || !fs::exists(native)) {
        std::cerr << "allowlist: missing host or native DLL under " << pkg << "\n";
        return 1;
    }
    if (harmony.empty()) {
        std::cerr << "allowlist: Harmony 0Harmony.dll not found. Set HARMONY_PATH.\n";
        return 1;
    }
    std::string hSha = sha256_file_powershell(harmony);
    std::string hostSha = sha256_file_powershell(host);
    std::string nativeSha = sha256_file_powershell(native);
    if (hSha.empty() || hostSha.empty() || nativeSha.empty()) {
        std::cerr << "allowlist: hash failed\n";
        return 1;
    }
    fs::path authDir = pkg / "Auth";
    fs::create_directories(authDir);
    fs::path out = authDir / "allowlist.json";
    std::ofstream f(out);
    f << "{\n"
      << "  \"version\": 1,\n"
      << "  \"note\": \"Accepted SHA256 digests for a Stratware release. Rebuild/ship updates this file.\",\n"
      << "  \"harmony\": [\"" << hSha << "\"],\n"
      << "  \"rimkit_host\": [\"" << hostSha << "\"],\n"
      << "  \"rimkit_native\": [\"" << nativeSha << "\"]\n"
      << "}\n";
    std::cout << "Wrote " << out << "\n";
    std::cout << "  harmony=" << hSha.substr(0, 8) << " host=" << hostSha.substr(0, 8)
              << " native=" << nativeSha.substr(0, 8) << "\n";
    return 0;
}

static int cmd_mod_ship(fs::path modDir, fs::path modsDir) {
    if (sync_mod(modDir) != 0) {
        return 1;
    }
    if (modsDir.empty()) {
        modsDir = default_mods_dir();
    }
    if (modsDir.empty()) {
        std::cerr << "No Mods folder. Pass path or set RIMWORLD_MODS.\n";
        return 1;
    }
    std::string folderName = modDir.filename().string();
    if (folderName.empty() || folderName == "." || folderName == "..") {
        folderName = fs::absolute(modDir).filename().string();
    }
    // Kit package lives in repo/mod/; ship as the repo folder name (or RimKit), not "mod".
    if (folderName == "mod" && fs::exists(modDir / ".." / "src" / "native")) {
        folderName = fs::absolute(modDir / "..").filename().string();
        if (folderName.empty() || folderName == "." || folderName == "..") {
            folderName = "RimKit";
        }
    }
    if (folderName.empty() || folderName == "." || folderName == "..") {
        folderName = "LuaMod";
    }
    fs::path dest = modsDir / folderName;
    // Overwrite payload in place. Avoid remove_all: RimWorld locks Native/*.dll while running.
    try {
        copy_mod_payload(modDir, dest);
    } catch (const std::exception& e) {
        std::cerr << "Ship copy failed (close RimWorld if DLLs locked): " << e.what() << "\n";
        return 1;
    }
    std::cout << "Shipped to " << dest << "\n";
    std::cout << "Close RimWorld before ship if Native DLL copy fails.\n";
    return 0;
}

static int cmd_build() {
    fs::path root = kit_root_from_exe();
    fs::path bat = root / "src" / "native" / "build_release.bat";
    if (!fs::exists(bat)) {
        std::cerr << "build_release.bat not found at " << bat << "\n";
        return 1;
    }
    std::string cmd = "cmd /c \"" + bat.string() + "\"";
    std::cout << "Running " << bat << "\n";
    int rc = std::system(cmd.c_str());
    if (rc != 0) {
        return rc;
    }
    fs::path hostProj = root / "src" / "host" / "RimLuaHost.csproj";
    std::string dotnet = "dotnet build \"" + hostProj.string() + "\" -c Release";
    std::cout << "Running " << dotnet << "\n";
    rc = std::system(dotnet.c_str());
    if (rc != 0) {
        return rc;
    }
    return regenerate_allowlist(root);
}

static int cmd_init(int argc, char** argv) {
    std::string name = "MyLuaMod";
    fs::path out = fs::current_path();
    if (argc >= 3) {
        name = argv[2];
        out = fs::current_path() / name;
    } else if (fs::exists(fs::current_path() / "meta.lua")) {
        std::cout << "meta.lua already here. Run: rimkit mod sync\n";
        return sync_mod(fs::current_path());
    } else {
        // init in cwd without extra folder if name not given? User often wants folder.
        // Prefer creating ./MyLuaMod unless cwd empty of meta - create in cwd with default name folder
        out = fs::current_path() / name;
    }
    return create_mod_at(out, name);
}

// ---------------------------------------------------------------- migrate (UNC)

static bool is_ident_char(char c) {
    return std::isalnum(static_cast<unsigned char>(c)) || c == '_';
}

// Replaces whole-token occurrences of `from` with `to`. A match must not continue an identifier or a field
// path on either side, so "rim.pawn.hunger" does not match inside "rim.pawn.hunger_pct" or "x.rim.pawn.hunger".
static int replace_token(std::string& text, const std::string& from, const std::string& to, char required_prefix) {
    int count = 0;
    size_t pos = 0;
    while ((pos = text.find(from, pos)) != std::string::npos) {
        const size_t end = pos + from.size();
        const char before = pos == 0 ? '\0' : text[pos - 1];
        const char after = end >= text.size() ? '\0' : text[end];
        bool ok = !is_ident_char(after);
        if (required_prefix != '\0') {
            ok = ok && before == required_prefix;
        } else {
            // A name inside a string literal ("config.get" in t.mock) is an op name, not a Lua call, so it is left alone.
            ok = ok && !is_ident_char(before) && before != '.' && before != ':' && before != '"' && before != '\'';
        }
        if (ok) {
            text.replace(pos, from.size(), to);
            pos += to.size();
            ++count;
        } else {
            pos = end;
        }
    }
    return count;
}

struct MigrateResult {
    int replacements = 0;
    std::vector<std::string> notes;
};

static MigrateResult migrate_text(std::string& text) {
    MigrateResult result;
    const rimlua::aliases::Table& table = rimlua::aliases::table();

    std::vector<const rimlua::aliases::PathAlias*> rules;
    for (const auto& a : table.paths) {
        if (a.deprecate) {
            rules.push_back(&a);
        }
    }
    std::sort(rules.begin(), rules.end(),
              [](const auto* a, const auto* b) { return a->old_path.size() > b->old_path.size(); });
    for (const auto* rule : rules) {
        result.replacements += replace_token(text, rule->old_path, rule->new_path, '\0');
    }
    for (const auto& m : table.methods) {
        result.replacements += replace_token(text, m.from, m.to, ':');
    }

    // Events change the handler signature (a payload table instead of a pawn), so only report them.
    for (const auto& e : table.events) {
        for (const char quote : {'"', '\''}) {
            const std::string literal = std::string(1, quote) + e.from + std::string(1, quote);
            if (text.find(literal) != std::string::npos) {
                result.notes.push_back("event " + literal + ": use \"" + e.to + "\" and read e." + e.payload_key +
                                       " (the old name still works with the old handler signature)");
            }
        }
    }
    for (const auto& g : table.globals) {
        if (text.find(g.from) != std::string::npos) {
            result.notes.push_back(g.from + ": use events.on(\"" + g.event + "\", fn)");
        }
    }
    for (const auto& m : table.manual) {
        if (text.find(m.from) != std::string::npos) {
            result.notes.push_back(m.from + " -> " + m.to + ": " + m.note);
        }
    }
    return result;
}

static int cmd_migrate(int argc, char** argv) {
    bool write = false;
    bool check = false;
    fs::path target;
    for (int i = 2; i < argc; ++i) {
        const std::string a = argv[i];
        if (a == "--write") {
            write = true;
        } else if (a == "--check") {
            check = true;
        } else {
            target = a;
        }
    }
    if (target.empty()) {
        target = fs::current_path();
    }
    if (!fs::exists(target)) {
        std::cerr << "Path not found: " << target << "\n";
        return 1;
    }

    std::vector<fs::path> files;
    if (fs::is_regular_file(target)) {
        files.push_back(target);
    } else {
        for (auto it = fs::recursive_directory_iterator(target); it != fs::recursive_directory_iterator(); ++it) {
            const std::string name = it->path().filename().string();
            if (it->is_directory() && (name == ".git" || name == "node_modules" || name == "third_party" ||
                                       name == "build" || name == "bin")) {
                it.disable_recursion_pending();
                continue;
            }
            if (it->is_regular_file() && it->path().extension() == ".lua") {
                files.push_back(it->path());
            }
        }
    }

    int changed_files = 0;
    int total = 0;
    for (const fs::path& file : files) {
        std::ifstream in(file, std::ios::binary);
        std::string original((std::istreambuf_iterator<char>(in)), std::istreambuf_iterator<char>());
        in.close();
        std::string text = original;
        const MigrateResult r = migrate_text(text);
        if (r.replacements == 0 && r.notes.empty()) {
            continue;
        }
        std::cout << file.string() << ": " << r.replacements << " name(s)\n";
        for (const std::string& note : r.notes) {
            std::cout << "  note: " << note << "\n";
        }
        if (r.replacements > 0) {
            ++changed_files;
            total += r.replacements;
            if (write) {
                std::ofstream out(file, std::ios::binary | std::ios::trunc);
                out << text;
            }
        }
    }
    std::cout << (write ? "Rewrote " : "Would rewrite ") << total << " name(s) in " << changed_files << " file(s) ("
              << files.size() << " scanned).\n";
    if (!write && changed_files > 0) {
        std::cout << "Run again with --write to apply.\n";
    }
    return check && changed_files > 0 ? 1 : 0;
}

static int cmd_mod(int argc, char** argv) {
    if (argc < 3) {
        usage();
        return 1;
    }
    const std::string sub = argv[2];
    if (sub == "create") {
        if (argc < 4) {
            std::cerr << "Usage: rimkit mod create <name> [dir]\n";
            return 1;
        }
        const std::string name = argv[3];
        fs::path out = argc >= 5 ? fs::path(argv[4]) / name : fs::path(name);
        return create_mod_at(out, name);
    }
    if (sub == "defs") {
        // Only the Lua to XML step (Defs/*.lua and Languages/**/*.lua) for any folder, with no meta.lua needed.
        const fs::path dir = argc >= 4 ? fs::path(argv[3]) : fs::current_path();
        int rc = write_lua_defs_files(dir);
        return rc != 0 ? rc : write_lua_language_files(dir);
    }
    if (sub == "sync") {
        fs::path dir = argc >= 4 ? fs::path(argv[3]) : fs::current_path();
        return sync_mod(resolve_ship_dir(dir));
    }
    if (sub == "ship") {
        fs::path dir = argc >= 4 ? fs::path(argv[3]) : fs::current_path();
        fs::path mods = argc >= 5 ? fs::path(argv[4]) : fs::path();
        return cmd_mod_ship(resolve_ship_dir(dir), mods);
    }
    if (sub == "check") {
        fs::path dir = argc >= 4 ? fs::path(argv[3]) : fs::current_path();
        return cmd_mod_check(resolve_ship_dir(dir));
    }
    // Commands that take an optional mod folder first, then their own options.
    auto split = [&](int first, fs::path& dir, std::vector<std::string>& rest) {
        dir = fs::current_path();
        for (int i = first; i < argc; ++i) {
            std::string a = argv[i];
            if (i == first && !a.empty() && a[0] != '-' && fs::exists(a) && fs::is_directory(a)) {
                dir = a;
            } else {
                rest.push_back(a);
            }
        }
        dir = resolve_ship_dir(dir);
    };
    if (sub == "test" || sub == "gen-tests" || sub == "assets" || sub == "release-check" || sub == "publish") {
        fs::path dir;
        std::vector<std::string> rest;
        split(3, dir, rest);
        if (sub == "test") return rkcli::cmd_test(dir, rest);
        if (sub == "gen-tests") {
            rest.push_back("--generate");
            return rkcli::cmd_test(dir, rest);
        }
        if (sub == "assets") return rkcli::cmd_assets(dir, rest);
        if (sub == "release-check") return rkcli::cmd_release_check(dir);
        if (sync_mod(dir) != 0) return 1;
        return rkcli::cmd_publish(dir, rest);
    }
    if (sub == "i18n") {
        fs::path dir = fs::current_path();
        std::vector<std::string> rest;
        for (int i = 3; i < argc; ++i) rest.push_back(argv[i]);
        return rkcli::cmd_i18n(resolve_ship_dir(dir), rest);
    }
    if (sub == "diag") {
        std::vector<std::string> rest;
        for (int i = 3; i < argc; ++i) rest.push_back(argv[i]);
        return rkcli::cmd_diag(rest);
    }
    usage();
    return 1;
}

int main(int argc, char** argv) {
    rkcli::copy_payload = [](const fs::path& from, const fs::path& to) { copy_mod_payload(from, to); };
    if (argc < 2) {
        usage();
        return 1;
    }
    const std::string cmd = argv[1];
    if (cmd == "help" || cmd == "-h" || cmd == "--help") {
        usage();
        return 0;
    }
    if (cmd == "init") {
        return cmd_init(argc, argv);
    }
    if (cmd == "build") {
        return cmd_build();
    }
    if (cmd == "mod") {
        return cmd_mod(argc, argv);
    }
    if (cmd == "publish" || cmd == "test" || cmd == "diag") {
        std::vector<char*> fake = {argv[0], const_cast<char*>("mod"), argv[1]};
        for (int i = 2; i < argc; ++i) fake.push_back(argv[i]);
        return cmd_mod(static_cast<int>(fake.size()), fake.data());
    }
    if (cmd == "migrate") {
        return cmd_migrate(argc, argv);
    }
    // aliases
    if (cmd == "create" || cmd == "new-mod") {
        if (argc < 3) {
            usage();
            return 1;
        }
        char* fake[] = {argv[0], (char*)"mod", (char*)"create", argv[2], argc >= 4 ? argv[3] : nullptr};
        return cmd_mod(argc >= 4 ? 5 : 4, fake);
    }
    if (cmd == "sync") {
        fs::path dir = argc >= 3 ? fs::path(argv[2]) : fs::current_path();
        return sync_mod(dir);
    }
    usage();
    return 1;
}
