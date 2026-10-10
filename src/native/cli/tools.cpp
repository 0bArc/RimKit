// rimkit mod test, diag, publish and the meta file reader.
#include "tools.hpp"
#include "lua_file.hpp"
#include "json_lite.hpp"

#include <algorithm>
#include <cctype>
#include <chrono>
#include <cstring>
#include <cstdlib>
#include <ctime>
#include <fstream>
#include <iostream>
#include <regex>
#include <sstream>

#define SOL_ALL_SAFETIES_ON 1
#include <sol/sol.hpp>

#ifdef _WIN32
#include <windows.h>
#endif

namespace rkcli {

std::function<void(const fs::path&, const fs::path&)> copy_payload;

// ---------------------------------------------------------------- meta

namespace {

std::string str_of(const sol::table& t, const char* a, const char* b = nullptr) {
    sol::object o = t[a];
    if ((!o.valid() || o.get_type() == sol::type::nil) && b) {
        o = t[b];
    }
    if (o.is<std::string>()) {
        return o.as<std::string>();
    }
    if (o.is<double>()) {
        return std::to_string(static_cast<long long>(o.as<double>()));
    }
    return std::string();
}

bool has_key(const sol::table& t, const char* a) {
    sol::object o = t[a];
    return o.valid() && o.get_type() != sol::type::nil;
}

std::vector<std::string> list_of(const sol::table& t, const char* a, const char* b = nullptr) {
    std::vector<std::string> out;
    sol::object o = t[a];
    if ((!o.valid() || o.get_type() == sol::type::nil) && b) {
        o = t[b];
    }
    if (o.is<std::string>()) {
        out.push_back(o.as<std::string>());
    } else if (o.get_type() == sol::type::table) {
        sol::table arr = o.as<sol::table>();
        for (size_t i = 1; i <= arr.size(); ++i) {
            sol::object v = arr[i];
            if (v.is<std::string>()) {
                out.push_back(v.as<std::string>());
            } else if (v.get_type() == sol::type::table) {
                sol::table d = v.as<sol::table>();
                std::string id = str_of(d, "id", "package_id");
                if (!id.empty()) {
                    out.push_back(id);
                }
            }
        }
    }
    return out;
}

fs::path find_meta(const fs::path& dir) {
    for (const char* rel : {"meta.lua", "Lua/meta.lua", "About/meta.lua"}) {
        if (fs::exists(dir / rel)) {
            return dir / rel;
        }
    }
    return {};
}

std::string read_all(const fs::path& p) {
    std::ifstream f(p, std::ios::binary);
    std::stringstream ss;
    ss << f.rdbuf();
    return ss.str();
}

std::string json_escape(const std::string& s) {
    std::string out;
    for (char c : s) {
        if (c == '"' || c == '\\') {
            out += '\\';
            out += c;
        } else if (static_cast<unsigned char>(c) < 32) {
            out += ' ';
        } else {
            out += c;
        }
    }
    return out;
}

std::string xml_esc(const std::string& s) {
    std::string out;
    for (char c : s) {
        if (c == '&') out += "&amp;";
        else if (c == '<') out += "&lt;";
        else if (c == '>') out += "&gt;";
        else out += c;
    }
    return out;
}

std::string timestamp() {
    std::time_t t = std::time(nullptr);
    std::tm tm{};
#ifdef _WIN32
    localtime_s(&tm, &t);
#else
    localtime_r(&t, &tm);
#endif
    char buf[32];
    std::strftime(buf, sizeof(buf), "%Y%m%d_%H%M%S", &tm);
    return buf;
}

fs::path locallow() {
    const char* home = std::getenv("USERPROFILE");
    if (!home) {
        return {};
    }
    return fs::path(home) / "AppData" / "LocalLow" / "Ludeon Studios" / "RimWorld by Ludeon Studios";
}

}  // namespace

bool load_meta(const fs::path& mod_dir, ModMeta& m, std::string& err) {
    fs::path path = find_meta(mod_dir);
    if (path.empty()) {
        err = "no meta.lua in " + mod_dir.string();
        return false;
    }
    sol::state lua;
    rimlua::open_cli_libs(lua);
    sol::table meta_table = lua.create_table();
    sol::table host = lua.create_table();
    host["metadata"] = meta_table;
    lua["host"] = host;
    lua["package"]["preload"]["host.metadata"] = [meta_table](sol::this_state) { return meta_table; };
    sol::protected_function_result r = rimlua::run_lua_file(lua, path.string());
    if (!r.valid()) {
        sol::error e = r;
        err = std::string("meta.lua: ") + e.what();
        return false;
    }
    sol::table t = meta_table;
    if (r.return_count() > 0 && r.get_type(0) == sol::type::table) {
        t = r.get<sol::table>(0);
    }
    m.name = str_of(t, "name", "Name");
    m.author = str_of(t, "author", "Author");
    m.package_id = str_of(t, "package_id", "packageId");
    m.description = str_of(t, "description", "Description");
    m.mod_version = str_of(t, "mod_version", "modVersion");
    m.url = str_of(t, "url");
    m.license = str_of(t, "license");
    m.changelog = str_of(t, "changelog");
    m.workshop_id = str_of(t, "workshop_id");
    m.game_versions = list_of(t, "supported_versions", "supportedVersions");
    if (m.game_versions.empty()) {
        std::string v = str_of(t, "version", "Version");
        if (!v.empty()) {
            m.game_versions.push_back(v);
        }
    }
    m.caps_declared = has_key(t, "capabilities");
    m.capabilities = list_of(t, "capabilities");
    m.tags = list_of(t, "tags");
    m.depends = list_of(t, "depends", "dependencies");
    m.load_after = list_of(t, "load_after", "loadAfter");
    m.load_before = list_of(t, "load_before", "loadBefore");
    m.incompatible = list_of(t, "incompatible_with", "incompatibleWith");
    sol::object pb = t["perf_budget_us"];
    if (pb.is<double>()) {
        m.perf_budget_us = static_cast<int>(pb.as<double>());
    }
    sol::object al = t["api_level"];
    if (al.is<double>()) {
        m.api_level = static_cast<int>(al.as<double>());
    }
    sol::object vf = t["version_folders"];
    if (vf.get_type() == sol::type::table) {
        for (const auto& kv : vf.as<sol::table>()) {
            if (!kv.first.is<std::string>() || kv.second.get_type() != sol::type::table) {
                continue;
            }
            std::vector<std::string> folders;
            sol::table arr = kv.second.as<sol::table>();
            for (size_t i = 1; i <= arr.size(); ++i) {
                sol::object v = arr[i];
                if (v.is<std::string>()) {
                    folders.push_back(v.as<std::string>());
                }
            }
            m.version_folders[kv.first.as<std::string>()] = folders;
        }
    }
    return true;
}

int write_extras(const fs::path& mod_dir) {
    ModMeta m;
    std::string err;
    if (!load_meta(mod_dir, m, err)) {
        std::cerr << err << "\n";
        return 1;
    }
    fs::create_directories(mod_dir / "About");
    {
        std::ofstream f(mod_dir / "About" / "RimKit.json");
        f << "{\"name\":\"" << json_escape(m.name) << "\",\"version\":\"" << json_escape(m.mod_version) << "\",\"perf_budget_us\":"
          << m.perf_budget_us;
        if (m.api_level > 0) {
            f << ",\"api_level\":" << m.api_level;
        }
        if (m.caps_declared) {
            f << ",\"capabilities\":[";
            for (size_t i = 0; i < m.capabilities.size(); ++i) {
                f << (i ? "," : "") << "\"" << json_escape(m.capabilities[i]) << "\"";
            }
            f << "]";
        }
        f << "}\n";
    }
    if (!m.version_folders.empty()) {
        std::ofstream f(mod_dir / "LoadFolders.xml");
        f << "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<!-- Generated by rimkit mod sync from meta.version_folders. Do not edit. -->\n<loadFolders>\n";
        for (const auto& kv : m.version_folders) {
            f << "  <v" << kv.first << ">\n";
            for (const std::string& folder : kv.second) {
                f << "    <li>" << xml_esc(folder) << "</li>\n";
            }
            f << "  </v" << kv.first << ">\n";
        }
        f << "</loadFolders>\n";
    }
    return 0;
}

// ---------------------------------------------------------------- test

#ifdef _WIN32

namespace {

struct TestState {
    int pass = 0;
    int fail = 0;
    bool saw_summary = false;
    bool verbose = false;
    std::string generated;  // lines the mod's registrations produced under rimkit mod gen-tests
};
TestState g_test;

void on_log(const char* m) {
    const std::string s = m ? m : "";
    if (s.rfind("RKGEN:", 0) == 0) {
        g_test.generated += s.substr(6) + "\n";
    } else if (s.rfind("TEST PASS ", 0) == 0) {
        std::cout << "  ok    " << s.substr(10) << "\n";
    } else if (s.rfind("TEST FAIL ", 0) == 0) {
        std::cout << "  FAIL  " << s.substr(10) << "\n";
    } else if (s.rfind("TESTS pass=", 0) == 0) {
        std::smatch mm;
        if (std::regex_search(s, mm, std::regex("pass=(\\d+) fail=(\\d+)"))) {
            g_test.pass = std::stoi(mm[1]);
            g_test.fail = std::stoi(mm[2]);
            g_test.saw_summary = true;
        }
    } else if (g_test.verbose || s.find("error") != std::string::npos || s.find("Error") != std::string::npos) {
        std::cout << "  log   " << s << "\n";
    }
}
int on_reg(const char*, const char*, int, int, const char*) { return 1; }
void on_unreg(int) {}
char* on_inv(const char*, const char*) {
    const char* r = "{\"ok\":true,\"t\":\"j\",\"v\":null}";
    char* m = static_cast<char*>(std::malloc(std::strlen(r) + 1));
    std::memcpy(m, r, std::strlen(r) + 1);
    return m;
}
void on_free(char* p) { std::free(p); }

struct Callbacks {
    void (*log)(const char*);
    void (*message)(const char*);
    int (*reg)(const char*, const char*, int, int, const char*);
    void (*unreg)(int);
    char* (*inv)(const char*, const char*);
    void (*fr)(char*);
};

fs::path exe_dir() {
    char buf[MAX_PATH];
    DWORD n = GetModuleFileNameA(nullptr, buf, MAX_PATH);
    return fs::path(std::string(buf, n)).parent_path();
}

fs::path find_core(const std::vector<std::string>& args) {
    for (size_t i = 0; i + 1 < args.size(); ++i) {
        if (args[i] == "--core") {
            return args[i + 1];
        }
    }
    if (const char* env = std::getenv("RIMKIT_CORE")) {
        if (fs::exists(env)) {
            return env;
        }
    }
    const fs::path here = exe_dir();
    for (const fs::path& c : {here / "rimlua_core.dll", here / ".." / "mod" / "Native" / "rimlua_core.dll", here / ".." / "Native" / "rimlua_core.dll",
                              here / ".." / "src" / "native" / "build" / "rimlua_core.dll"}) {
        if (fs::exists(c)) {
            return fs::weakly_canonical(c);
        }
    }
    return {};
}

}  // namespace

namespace {

std::string env_or(const char* name, const std::string& fallback) {
    char buf[1024];
    const DWORD n = GetEnvironmentVariableA(name, buf, sizeof(buf));
    return (n > 0 && n < sizeof(buf)) ? std::string(buf, n) : fallback;
}

std::string option_value(const std::vector<std::string>& args, const std::string& name, const std::string& fallback = std::string()) {
    for (size_t i = 0; i + 1 < args.size(); ++i) {
        if (args[i] == name) return args[i + 1];
    }
    return fallback;
}

std::string lower(std::string s) {
    for (char& c : s) c = static_cast<char>(std::tolower(static_cast<unsigned char>(c)));
    return s;
}

}  // namespace

// rimkit mod test --in-game: starts RimWorld on its own save data folder, runs the mod's Tests/Game scripts inside the game and
// reads the JSON report they write. It never touches the ModsConfig.xml, settings or saves of the person running it: -savedatafolder
// points the game at a throwaway folder with its own mod list. The mod under test must already be somewhere the game finds mods
// (the game's Mods folder, a junction to your source folder, or the Workshop). The command does not copy anything into the game folder.
static int cmd_test_in_game(const fs::path& mod_dir, const std::vector<std::string>& args) {
    std::cout << std::unitbuf;
    ModMeta meta;
    std::string err;
    if (!load_meta(mod_dir, meta, err) || meta.package_id.empty()) {
        std::cerr << "Cannot read meta.lua in " << mod_dir.string() << (err.empty() ? "" : ": " + err) << "\n";
        return 2;
    }
    const fs::path game_tests = mod_dir / "Tests" / "Game";
    if (!fs::is_directory(game_tests)) {
        std::cerr << "No Tests/Game folder in " << mod_dir.string() << ".\n"
                  << "Put scripts that call game.itest.suite(...) there, see docs/guide/testing.md (Testing in the game).\n";
        return 2;
    }
    fs::path game_dir = option_value(args, "--game", env_or("RIMWORLD_DIR", ""));
    if (game_dir.empty()) game_dir = "C:/Program Files (x86)/Steam/steamapps/common/RimWorld";
    const fs::path exe = game_dir / "RimWorldWin64.exe";
    if (!fs::exists(exe)) {
        std::cerr << "RimWorldWin64.exe not found in " << game_dir.string() << ". Pass --game <folder> or set RIMWORLD_DIR.\n";
        return 2;
    }

    const int timeout_s = std::atoi(option_value(args, "--timeout", "300").c_str());
    const std::string filter = option_value(args, "--filter");
    const bool keep_open = std::find(args.begin(), args.end(), "--keep-open") != args.end();
    const bool headless = std::find(args.begin(), args.end(), "--headless") != args.end();

    // Own save data folder with its own mod list: harmony, RimKit and the mod under test, plus whatever --with names.
    const fs::path data = fs::temp_directory_path() / ("rimkit_ingame_" + timestamp());
    fs::create_directories(data / "Config");
    std::vector<std::string> active = {"ludeon.rimworld", "brrainz.harmony", "stratware.rimkit"};
    for (const std::string& dep : meta.depends) active.push_back(lower(dep));
    const std::string with = option_value(args, "--with");
    for (size_t pos = 0; pos < with.size();) {
        size_t comma = with.find(',', pos);
        if (comma == std::string::npos) comma = with.size();
        if (comma > pos) active.push_back(lower(with.substr(pos, comma - pos)));
        pos = comma + 1;
    }
    active.push_back(lower(meta.package_id));
    std::vector<std::string> unique;
    for (const std::string& id : active) {
        if (std::find(unique.begin(), unique.end(), id) == unique.end()) unique.push_back(id);
    }
    {
        std::ofstream cfg(data / "Config" / "ModsConfig.xml", std::ios::binary);
        cfg << "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<ModsConfigData>\n  <version>1.6</version>\n  <activeMods>\n";
        for (const std::string& id : unique) cfg << "    <li>" << id << "</li>\n";
        cfg << "  </activeMods>\n  <knownExpansions />\n</ModsConfigData>\n";
    }

    const fs::path report = data / "report.json";
    SetEnvironmentVariableA("RIMKIT_TEST_MODS", meta.package_id.c_str());
    SetEnvironmentVariableA("RIMKIT_TEST_REPORT", report.string().c_str());
    SetEnvironmentVariableA("RIMKIT_TEST_SOURCE", fs::absolute(mod_dir).string().c_str());
    SetEnvironmentVariableA("RIMKIT_TEST_FILTER", filter.empty() ? nullptr : filter.c_str());
    SetEnvironmentVariableA("RIMKIT_TEST_QUIT", keep_open ? "0" : "1");

    std::string cmdline = "\"" + exe.string() + "\" -quicktest -savedatafolder=\"" + data.string() + "\"";
    // No window and no GPU. Helm uses the same flags for the same reason.
    if (headless) cmdline += " -batchmode -nographics -logFile \"" + (data / "Player.log").string() + "\"";
    std::cout << "rimkit test --in-game: " << meta.name << " (" << meta.package_id << ")\n"
              << "  game        " << exe.string() << "\n"
              << "  save data   " << data.string() << " (throwaway, your own settings and saves are not used)\n"
              << "  mod list    " << unique.size() << " mods, the game must be able to find " << meta.package_id << "\n"
              << "  timeout     " << timeout_s << " s\n";

    STARTUPINFOA si{};
    si.cb = sizeof(si);
    PROCESS_INFORMATION pi{};
    std::vector<char> mutable_cmd(cmdline.begin(), cmdline.end());
    mutable_cmd.push_back('\0');
    if (!CreateProcessA(nullptr, mutable_cmd.data(), nullptr, nullptr, FALSE, 0, nullptr, game_dir.string().c_str(), &si, &pi)) {
        std::cerr << "Could not start the game (error " << GetLastError() << ").\n";
        return 2;
    }

    const auto started = std::chrono::steady_clock::now();
    bool have_report = false;
    bool exited = false;
    while (true) {
        std::error_code ec;
        if (fs::exists(report, ec) && fs::file_size(report, ec) > 0) {
            have_report = true;
            break;
        }
        if (WaitForSingleObject(pi.hProcess, 1000) == WAIT_OBJECT_0) {
            exited = true;
            // The game may have written the report just before it closed.
            have_report = fs::exists(report, ec) && fs::file_size(report, ec) > 0;
            break;
        }
        if (std::chrono::steady_clock::now() - started > std::chrono::seconds(timeout_s)) {
            break;
        }
    }
    if (!exited && !keep_open) {
        // The run asked the game to quit; give it a moment, then close it.
        if (WaitForSingleObject(pi.hProcess, 20000) != WAIT_OBJECT_0) TerminateProcess(pi.hProcess, 1);
    }
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);

    if (!have_report) {
        std::cerr << (exited ? "The game closed" : "Timed out") << " without a test report.\n"
                  << "Look at the game log (Player.log) for lines that start with RKTEST or [RimKit]. Common causes: the game could not find the mod "
                  << meta.package_id << " (put it in the game's Mods folder, or a junction to it), or a mod in the list failed to load.\n";
        return 2;
    }
    std::ifstream in(report, std::ios::binary);
    std::stringstream ss;
    ss << in.rdbuf();
    rimlua::json::Value root;
    if (!rimlua::json::parse(ss.str(), root) || !root.is_object()) {
        std::cerr << "The report is not valid JSON: " << report.string() << "\n";
        return 2;
    }
    const rimlua::json::Value* rows = root.find("tests");
    if (rows && rows->is_array()) {
        for (const rimlua::json::Value& row : rows->items) {
            const std::string status = row.get_string("status");
            const char* tag = status == "pass" ? "ok  " : status == "skip" ? "skip" : "FAIL";
            std::cout << "  " << tag << "  " << row.get_string("suite") << " > " << row.get_string("name");
            const std::string message = row.get_string("message");
            if (!message.empty()) std::cout << " :: " << message;
            std::cout << "\n";
        }
    }
    const long long pass = root.get_int("pass"), fail = root.get_int("fail"), skip = root.get_int("skip");
    std::cout << "pass=" << pass << " fail=" << fail << " skip=" << skip << "\n";
    std::error_code ec;
    fs::copy_file(report, fs::current_path() / "rimkit_ingame_report.json", fs::copy_options::overwrite_existing, ec);
    if (!ec) std::cout << "report: " << (fs::current_path() / "rimkit_ingame_report.json").string() << "\n";
    fs::remove_all(data, ec);
    return fail == 0 ? 0 : 1;
}

int cmd_test(const fs::path& mod_dir, const std::vector<std::string>& args) {
    if (std::find(args.begin(), args.end(), "--in-game") != args.end()) {
        return cmd_test_in_game(mod_dir, args);
    }
    std::cout << std::unitbuf;
    g_test = TestState();
    g_test.verbose = std::find(args.begin(), args.end(), "--verbose") != args.end();
    const bool generate = std::find(args.begin(), args.end(), "--generate") != args.end();
    const bool force = std::find(args.begin(), args.end(), "--force") != args.end();
    ModMeta meta;
    std::string err;
    const bool have_meta = load_meta(mod_dir, meta, err);
    fs::path tests = fs::exists(mod_dir / "Tests") ? mod_dir / "Tests" : mod_dir / "tests";
    if (!fs::exists(tests) && !generate) {
        std::cerr << "No Tests folder in " << mod_dir.string() << ". Add Tests/my_test.lua, see docs/guide/testing.md.\n";
        return 2;
    }
    fs::path core = find_core(args);
    if (core.empty()) {
        std::cerr << "rimlua_core.dll not found. Put rimkit.exe next to it, or pass --core <path>, or set RIMKIT_CORE.\n";
        return 2;
    }
    HMODULE dll = LoadLibraryA(core.string().c_str());
    if (!dll) {
        std::cerr << "Cannot load " << core.string() << "\n";
        return 2;
    }
    auto init = reinterpret_cast<int (*)(const Callbacks*)>(GetProcAddress(dll, "rimlua_init"));
    auto shutdown = reinterpret_cast<void (*)()>(GetProcAddress(dll, "rimlua_shutdown"));
    auto load_dir = reinterpret_cast<int (*)(const char*)>(GetProcAddress(dll, "rimlua_load_directory"));
    auto set_ctx = reinterpret_cast<void (*)(const char*)>(GetProcAddress(dll, "rimlua_set_mod_context"));
    auto set_test = reinterpret_cast<void (*)(int)>(GetProcAddress(dll, "rimlua_set_test_mode"));
    if (!init || !load_dir || !set_ctx || !set_test) {
        std::cerr << "This rimlua_core.dll is too old for rimkit mod test.\n";
        return 2;
    }
    Callbacks cb{on_log, on_log, on_reg, on_unreg, on_inv, on_free};
    if (init(&cb) != 0) {
        std::cerr << "rimlua_init failed\n";
        return 2;
    }
    set_test(1);

    // Everything runs against the mock host: no game is needed and no real op is ever called.
    fs::path scratch = fs::temp_directory_path() / ("rimkit_test_" + timestamp());
    fs::create_directories(scratch / "pre");
    fs::create_directories(scratch / "post");
    std::ofstream(scratch / "pre" / "pre.lua") << "__rk_mock_enable(true)\ngame.test.capture()\ngame.test.globals()\n";
    std::ofstream(scratch / "post" / "run.lua") << "game.test.run()\n";
    load_dir((scratch / "pre").string().c_str());

    int rc = 0;
    if (fs::exists(mod_dir / "Lua")) {
        set_ctx(have_meta ? meta.package_id.c_str() : "");
        rc = load_dir((mod_dir / "Lua").string().c_str());
        set_ctx("");
        if (rc != 0) {
            std::cout << "  note  the mod's Lua had load errors, see above\n";
        }
    }
    if (generate) {
        const std::string mod_name = have_meta ? meta.name : mod_dir.filename().string();
        std::ofstream(scratch / "post" / "run.lua") << "game.test.print_generated(\"" << mod_name << "\")\n";
        load_dir((scratch / "post").string().c_str());
        std::error_code gec;
        fs::remove_all(scratch, gec);
        const fs::path out_file = mod_dir / "Tests" / "generated_test.luau";
        if (g_test.generated.empty()) {
            std::cerr << "Nothing to generate: the mod registered no events.\n";
            return 1;
        }
        if (fs::exists(out_file) && !force) {
            std::ifstream existing(out_file);
            std::string first;
            std::getline(existing, first);
            if (first.rfind("-- Generated by rimkit mod gen-tests", 0) != 0) {
                std::cerr << out_file.string() << " was changed by hand. Pass --force to overwrite it.\n";
                return 1;
            }
        }
        fs::create_directories(out_file.parent_path());
        std::ofstream(out_file, std::ios::binary) << g_test.generated;
        std::cout << "Wrote " << out_file.string() << "\n";
        return 0;
    }
    std::cout << "rimkit test: " << (have_meta ? meta.name : mod_dir.filename().string()) << "\n";
    load_dir(tests.string().c_str());
    load_dir((scratch / "post").string().c_str());
    std::error_code ec;
    fs::remove_all(scratch, ec);
    if (!g_test.saw_summary) {
        std::cout << "No tests ran. Use game.test.describe and game.test.it in Tests/*.lua.\n";
        return 2;
    }
    std::cout << "pass=" << g_test.pass << " fail=" << g_test.fail << "\n";
    return g_test.fail == 0 ? 0 : 1;
}

#else

int cmd_test(const fs::path&, const std::vector<std::string>&) {
    std::cerr << "rimkit mod test needs Windows (rimlua_core.dll).\n";
    return 2;
}

#endif

// ---------------------------------------------------------------- diag

int cmd_diag(const std::vector<std::string>& args) {
    // Newest bundle written by game.dev.bundle, or a fresh one made from the log and the mod list.
    const fs::path ll = locallow();
    fs::path bundle;
    const fs::path root = ll.empty() ? fs::path() : ll / "RimKitDiagnostics";
    if (!root.empty() && fs::exists(root)) {
        fs::file_time_type newest{};
        for (auto& e : fs::directory_iterator(root)) {
            if (e.is_directory() && e.last_write_time() > newest) {
                newest = e.last_write_time();
                bundle = e.path();
            }
        }
    }
    const bool fresh = std::find(args.begin(), args.end(), "--fresh") != args.end();
    if (bundle.empty() || fresh) {
        bundle = fs::temp_directory_path() / ("rimkit_diag_" + timestamp());
        fs::create_directories(bundle);
        std::ofstream rep(bundle / "report.txt");
        rep << "RimKit diagnostics made outside the game, " << timestamp() << "\n";
        rep << "For the mod list in load order and profiler numbers, use the Actions tab of the dev tools (F11) in game.\n";
        for (const char* name : {"Player.log", "Player-prev.log"}) {
            if (!ll.empty() && fs::exists(ll / name)) {
                std::error_code ec;
                fs::copy_file(ll / name, bundle / name, fs::copy_options::overwrite_existing, ec);
            }
        }
        if (!ll.empty() && fs::exists(ll / "Config" / "ModsConfig.xml")) {
            std::error_code ec;
            fs::copy_file(ll / "Config" / "ModsConfig.xml", bundle / "ModsConfig.xml", fs::copy_options::overwrite_existing, ec);
        }
    }
    fs::path zip = fs::current_path() / ("rimkit_diag_" + timestamp() + ".zip");
    const std::string cmd = "tar -a -c -f \"" + zip.string() + "\" -C \"" + bundle.string() + "\" .";
    if (std::system(cmd.c_str()) != 0) {
        std::cout << "Could not zip. The folder is ready to attach as is: " << bundle.string() << "\n";
        return 1;
    }
    std::cout << "Wrote " << zip.string() << "\n";
    std::cout << "It holds: report.txt, the game log, ModsConfig.xml and (when made in game) profiler.json.\n";
    std::cout << "Nothing was uploaded. Read it before you attach it to a bug report: the log can contain your user name in file paths.\n";
    return 0;
}

// ---------------------------------------------------------------- publish

namespace {

std::string vdf_escape(const std::string& s) {
    std::string out;
    for (char c : s) {
        if (c == '"') out += "\\\"";
        else if (c == '\n') out += "\\n";
        else if (c == '\r') continue;
        else out += c;
    }
    return out;
}

fs::path find_steamcmd(const std::vector<std::string>& args) {
    for (size_t i = 0; i + 1 < args.size(); ++i) {
        if (args[i] == "--steamcmd") {
            return args[i + 1];
        }
    }
    if (const char* env = std::getenv("STEAMCMD")) {
        if (fs::exists(env)) {
            return env;
        }
    }
    for (const char* c : {"C:/steamcmd/steamcmd.exe", "C:/Program Files (x86)/Steam/steamcmd.exe", "C:/Program Files/steamcmd/steamcmd.exe"}) {
        if (fs::exists(c)) {
            return c;
        }
    }
    return {};
}

std::string arg_value(const std::vector<std::string>& args, const std::string& name, const std::string& fallback = std::string()) {
    for (size_t i = 0; i + 1 < args.size(); ++i) {
        if (args[i] == name) {
            return args[i + 1];
        }
    }
    return fallback;
}

}  // namespace

int cmd_publish(const fs::path& mod_dir, const std::vector<std::string>& args) {
    ModMeta m;
    std::string err;
    if (!load_meta(mod_dir, m, err)) {
        std::cerr << err << "\n";
        return 1;
    }
    const bool dry = std::find(args.begin(), args.end(), "--dry-run") != args.end();
    const bool force = std::find(args.begin(), args.end(), "--force") != args.end();
    std::cout << "rimkit publish: " << m.name << " " << m.mod_version << "\n";
    if (!force && cmd_release_check(mod_dir) != 0) {
        std::cerr << "Fix the failed checks above, or pass --force.\n";
        return 1;
    }
    const std::string note = arg_value(args, "--note", m.changelog.empty() ? "Update " + m.mod_version : m.changelog);
    const fs::path stage = fs::temp_directory_path() / "rimkit_publish" / m.package_id / m.name;
    std::error_code ec;
    fs::remove_all(stage.parent_path(), ec);
    fs::create_directories(stage);
    if (copy_payload) {
        copy_payload(mod_dir, stage);
    }
    for (const char* extra : {"LICENSE", "LICENSE.md", "LICENSE.txt", "CREDITS.md", "CHANGELOG.md"}) {
        if (fs::exists(mod_dir / extra)) {
            fs::copy_file(mod_dir / extra, stage / extra, fs::copy_options::overwrite_existing, ec);
        }
    }

    std::string id = m.workshop_id;
    const fs::path id_file = mod_dir / "About" / "PublishedFileId.txt";
    if (id.empty() && fs::exists(id_file)) {
        id = read_all(id_file);
        while (!id.empty() && std::isspace(static_cast<unsigned char>(id.back()))) {
            id.pop_back();
        }
    }
    std::string description = m.description;
    if (fs::exists(mod_dir / "Workshop.md")) {
        description = read_all(mod_dir / "Workshop.md");
    }
    std::string tags = "Mod";
    for (const std::string& v : m.game_versions) {
        tags += "," + v;
    }
    for (const std::string& t : m.tags) {
        tags += "," + t;
    }
    const fs::path preview = mod_dir / "About" / "Preview.png";
    const fs::path vdf = fs::temp_directory_path() / "rimkit_publish" / (m.package_id + ".vdf");
    {
        std::ofstream f(vdf);
        f << "\"workshopitem\"\n{\n";
        f << "  \"appid\" \"294100\"\n";
        f << "  \"publishedfileid\" \"" << (id.empty() ? "0" : id) << "\"\n";
        f << "  \"contentfolder\" \"" << vdf_escape(stage.string()) << "\"\n";
        if (fs::exists(preview)) {
            f << "  \"previewfile\" \"" << vdf_escape(preview.string()) << "\"\n";
        }
        f << "  \"visibility\" \"" << arg_value(args, "--visibility", "0") << "\"\n";
        f << "  \"title\" \"" << vdf_escape(m.name) << "\"\n";
        f << "  \"description\" \"" << vdf_escape(description) << "\"\n";
        f << "  \"changenote\" \"" << vdf_escape(note) << "\"\n";
        f << "  \"tags\" \"" << vdf_escape(tags) << "\"\n";
        f << "}\n";
    }

    const fs::path steamcmd = find_steamcmd(args);
    const std::string user = arg_value(args, "--user");
    std::string command = "\"" + (steamcmd.empty() ? std::string("steamcmd") : steamcmd.string()) + "\" +login " + (user.empty() ? "<your steam user>" : user) +
                          " +workshop_build_item \"" + vdf.string() + "\" +quit";
    std::cout << "  content   " << stage.string() << "\n  item file " << vdf.string() << "\n  workshop id " << (id.empty() ? "(new item)" : id) << "\n  tags      " << tags << "\n";
    if (dry || steamcmd.empty() || user.empty()) {
        std::cout << "\nNot uploaded. ";
        if (steamcmd.empty()) {
            std::cout << "steamcmd was not found (pass --steamcmd <path> or set STEAMCMD). ";
        }
        if (user.empty()) {
            std::cout << "Pass --user <steam name>. ";
        }
        std::cout << "\nRun this yourself when ready:\n  " << command << "\n";
        return dry ? 0 : 1;
    }
    std::cout << "Uploading with steamcmd. It may ask for your Steam Guard code.\n";
    const int rc = std::system(("\"" + command + "\"").c_str());
    if (rc != 0) {
        std::cerr << "steamcmd failed (exit " << rc << ").\n";
        return 1;
    }
    // steamcmd writes the new item id back into the item file.
    std::smatch mm;
    const std::string after = read_all(vdf);
    if (std::regex_search(after, mm, std::regex("\"publishedfileid\"\\s+\"(\\d+)\"")) && mm[1] != "0" && id.empty()) {
        std::ofstream(id_file) << mm[1];
        std::cout << "New workshop item " << mm[1] << " saved to About/PublishedFileId.txt. Keep that file in your repo.\n";
    }
    std::cout << "Done.\n";
    return 0;
}

}  // namespace rkcli
