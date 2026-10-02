#include <cctype>
#include <cstdlib>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string>
#include <vector>

#define SOL_ALL_SAFETIES_ON 1
#include <sol/sol.hpp>

namespace fs = std::filesystem;

static void usage() {
    std::cout
        << "rimkit - RimKit CLI\n\n"
        << "  rimkit init [name]              Init Lua mod in cwd (or ./name)\n"
        << "  rimkit build                    Build native core + C# host\n"
        << "  rimkit mod create <name> [dir]  Create Lua mod folder\n"
        << "  rimkit mod sync [path]          meta.lua -> About/About.xml\n"
        << "  rimkit mod ship [path] [mods]   Sync + copy mod into RimWorld Mods/\n"
        << "  rimkit mod check [path]         Validate meta + Lua\n"
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

static std::string to_package_id(const std::string& name) {
    std::string out;
    for (char c : name) {
        if (std::isalnum(static_cast<unsigned char>(c))) {
            out.push_back(static_cast<char>(std::tolower(static_cast<unsigned char>(c))));
        } else if (c == '_' || c == '-' || c == '.') {
            out.push_back('.');
        }
    }
    if (out.empty()) {
        out = "mymod";
    }
    return "stratware." + out;
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
    lua.open_libraries(sol::lib::base, sol::lib::package, sol::lib::string, sol::lib::table, sol::lib::math);
    sol::protected_function_result result = lua.safe_script_file(defsLua.string(), sol::script_pass_on_error);
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

static int sync_mod(const fs::path& modDir) {
    fs::path metaPath = find_meta_lua(modDir);
    if (metaPath.empty()) {
        std::cerr << "No meta.lua in " << modDir << "\n";
        return 1;
    }

    sol::state lua;
    lua.open_libraries(sol::lib::base, sol::lib::package, sol::lib::string, sol::lib::table, sol::lib::math);

    sol::table metaTable = lua.create_table();
    sol::table host = lua.create_table();
    host["metadata"] = metaTable;
    lua["host"] = host;
    lua["package"]["preload"]["host.metadata"] = [metaTable](sol::this_state) { return metaTable; };

    sol::protected_function_result result = lua.safe_script_file(metaPath.string(), sol::script_pass_on_error);
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
    return write_defs_from_lua(modDir);
}

static int create_mod_at(const fs::path& out, const std::string& name) {
    if (fs::exists(out / "meta.lua")) {
        std::cerr << "Already exists: " << (out / "meta.lua") << "\n";
        return 1;
    }
    fs::create_directories(out / "Lua");
    fs::create_directories(out / "Languages" / "English" / "Keyed");
    const std::string package_id = to_package_id(name);
    {
        std::ofstream f(out / "meta.lua");
        f << "local meta = require(\"host.metadata\")\n"
          << "meta.name = \"" << name << "\"\n"
          << "meta.author = \"Team Stratware.win\"\n"
          << "meta.package_id = \"" << package_id << "\"\n"
          << "meta.version = \"1.6\"\n"
          << "meta.description = \"Lua mod powered by RimKit.\"\n"
          << "meta.depends = { \"brrainz.harmony\", \"stratware.rimkit\" }\n"
          << "meta.load_after = { \"brrainz.harmony\", \"stratware.rimkit\" }\n"
          << "return meta\n";
    }
    {
        std::ofstream f(out / "Lua" / "main.lua");
        f << "rim.on_load(function()\n"
          << "  rim.log(\"[" << name << "] loaded\")\n"
          << "  rim.message(\"[" << name << "] loaded\")\n"
          << "end)\n";
    }
    {
        // RimWorld ignores Lua/; Keyed file marks mod as having content.
        std::ofstream f(out / "Languages" / "English" / "Keyed" / "RimLua.xml");
        f << "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
          << "<LanguageData>\n"
          << "  <RimLuaModMarker>" << name << "</RimLuaModMarker>\n"
          << "</LanguageData>\n";
    }
    std::cout << "Created " << out << "\n";
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
                                 "Sounds", "Languages", "News", "Auth", "LoadFolders.xml", "meta.lua"};
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
    if (fs::exists(cur / "meta.lua") && fs::exists(cur / "src" / "native")) {
        return cur;
    }
    if (fs::exists(cur / ".." / "meta.lua") && fs::exists(cur / ".." / "src" / "native")) {
        return (cur / "..").lexically_normal();
    }
    if (fs::exists(cur / ".." / ".." / ".." / "meta.lua")) {
        return (cur / ".." / ".." / "..").lexically_normal();
    }
    return cur;
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
    fs::path lua_root = target / "Lua";
    if (!fs::exists(lua_root)) {
        lua_root = target;
    }
    for (auto it = fs::recursive_directory_iterator(lua_root); it != fs::recursive_directory_iterator(); ++it) {
        if (it->is_regular_file() && it->path().extension() == ".lua" && it->path().filename() != "meta.lua") {
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
    fs::path host = root / "Assemblies" / "RimLuaHost.dll";
    fs::path native = root / "Native" / "rimlua_core.dll";
    fs::path harmony = find_harmony_dll();
    if (!fs::exists(host) || !fs::exists(native)) {
        std::cerr << "allowlist: missing host or native DLL under " << root << "\n";
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
    fs::path authDir = root / "Auth";
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
        if (folderName.empty() || folderName == "." || folderName == "..") {
            folderName = "LuaMod";
        }
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
    if (sub == "sync") {
        fs::path dir = argc >= 4 ? fs::path(argv[3]) : fs::current_path();
        return sync_mod(dir);
    }
    if (sub == "ship") {
        fs::path dir = argc >= 4 ? fs::path(argv[3]) : fs::current_path();
        fs::path mods = argc >= 5 ? fs::path(argv[4]) : fs::path();
        return cmd_mod_ship(dir, mods);
    }
    if (sub == "check") {
        fs::path dir = argc >= 4 ? fs::path(argv[3]) : fs::current_path();
        return cmd_mod_check(dir);
    }
    usage();
    return 1;
}

int main(int argc, char** argv) {
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
