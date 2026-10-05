// Native test for api/kits.json: every promised function exists, and arguments reach the host with the right keys.
// Usage: kits_test rimlua_core.dll kits_expect.lua kits_args.lua
#include <windows.h>

#include <filesystem>
#include <fstream>

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>
#include <vector>

typedef void (*log_fn)(const char*);
typedef int (*reg_fn)(const char*, const char*, int, int, const char*);
typedef void (*unreg_fn)(int);
typedef char* (*inv_fn)(const char*, const char*);
typedef void (*free_fn)(char*);
struct cbs { log_fn log; log_fn message; reg_fn reg; unreg_fn unreg; inv_fn inv; free_fn fr; };

static std::vector<std::string> g_logs, g_ops, g_regs;
static void on_log(const char* m) { g_logs.push_back(m); }
static int on_reg(const char* type, const char* method, int, int, const char*) { g_regs.push_back(std::string(type) + "." + method); return 1; }
static void on_unreg(int) {}
static char* on_inv(const char* op, const char* args) {
    g_ops.push_back(std::string(op) + " " + args);
    std::string r = "{\"ok\":true,\"t\":\"s\",\"v\":\"ok\"}";
    if (std::string(op) == "util.game_version") r = "{\"ok\":true,\"t\":\"j\",\"v\":{\"major\":1,\"minor\":6,\"build\":4871,\"text\":\"1.6.4871\"}}";
    if (std::string(op) == "faction.leader") r = "{\"ok\":true,\"t\":\"h\",\"v\":7}";
    if (std::string(op) == "faction.members") r = "{\"ok\":true,\"t\":\"a\",\"v\":[7,8]}";
    if (std::string(op) == "map.terrain") r = "{\"ok\":false,\"e\":\"RK1001: cell is outside the map\"}";
    char* m = (char*)std::malloc(r.size() + 1);
    std::memcpy(m, r.c_str(), r.size() + 1);
    return m;
}
static void on_free(char* p) { std::free(p); }

static const char* (*g_ui_call)(int, const char*) = nullptr;
static std::string ui_call(int id, const char* arg) {
    const char* r = g_ui_call ? g_ui_call(id, arg) : nullptr;
    return r ? r : "";
}

#define CHECK(c) do { if (!(c)) { std::printf("FAIL line %d: %s\n", __LINE__, #c); fails++; } } while (0)

static bool has_op(const std::string& prefix, const std::string& must) {
    for (auto& o : g_ops) if (o.rfind(prefix, 0) == 0 && o.find(must) != std::string::npos) return true;
    return false;
}
static bool logged(const std::string& needle) {
    for (auto& l : g_logs) if (l.find(needle) != std::string::npos) return true;
    return false;
}

int main(int argc, char** argv) {
    int fails = 0;
    HMODULE dll = LoadLibraryA(argv[1]);
    if (!dll) { std::printf("cannot load dll\n"); return 2; }
    auto init = (int (*)(const cbs*))GetProcAddress(dll, "rimlua_init");
    auto load = (int (*)(const char*))GetProcAddress(dll, "rimlua_load_script");
    g_ui_call = (const char* (*)(int, const char*))GetProcAddress(dll, "rimlua_ui_call");
    cbs c{on_log, on_log, on_reg, on_unreg, on_inv, on_free};
    CHECK(init(&c) == 0);
    if (auto set_test = (void (*)(int))GetProcAddress(dll, "rimlua_set_test_mode")) set_test(1);
    CHECK(load(argv[2]) == 0);
    CHECK(logged("KITS OK"));
    CHECK(load(argv[3]) == 0);

    // subject goes to "h", positional arguments to the named keys
    CHECK(has_op("map.set_roof", "\"h\":5"));
    CHECK(has_op("map.set_roof", "\"x\":3") && has_op("map.set_roof", "\"z\":4") && has_op("map.set_roof", "\"def\":\"RoofConstructed\""));
    // an optional argument left out stays out of the request
    CHECK(!has_op("map.set_roof_none", "x"));
    bool noDef = false;
    for (auto& o : g_ops) if (o.rfind("map.set_roof", 0) == 0 && o.find("\"def\"") == std::string::npos) noDef = true;
    CHECK(noDef);
    // table arguments travel as a JSON string, wrapped objects inside become handles
    CHECK(has_op("query.things", "opts"));
    CHECK(has_op("query.things", "Steel"));
    CHECK(has_op("pawn.set_assignments", "pawns"));
    CHECK(has_op("pawn.set_assignments", "\"hour\":3"));
    // a function with no subject does not need one
    CHECK(has_op("pawn.kinds", "{}"));
    // an RK error from the host raises in Lua
    CHECK(logged("RESULT error RK1001"));
    // typed results: handles come back as wrapped game objects
    CHECK(logged("RESULT leader userdata"));
    CHECK(logged("RESULT members userdata 2"));
    CHECK(has_op("faction.info", "\"h\":7"));
    // version helpers and version-aware hooks
    CHECK(logged("RESULT at_least 1.5 true"));
    CHECK(logged("RESULT at_least 1.7 false"));
    bool hasNew = false, hasOld = false, hasFuture = false;
    for (auto& r : g_regs) { if (r == "A.New") hasNew = true; if (r == "A.Old") hasOld = true; if (r == "A.Future") hasFuture = true; }
    CHECK(hasNew && !hasOld && !hasFuture);
    CHECK(logged("RESULT none nil"));
    // function arguments become callback ids, and the host can call them back with JSON
    CHECK(has_op("widgets.open", "\"view\":1"));
    CHECK(has_op("widgets.open", "\"on_event\":2"));
    if (g_ui_call) {
        std::string view = ui_call(1, "{\"state\":{\"n\":3}}");
        CHECK(view.find("\"type\":\"label\"") != std::string::npos);
        CHECK(view.find("n=3") != std::string::npos);
        std::string none = ui_call(987, "{}");
        CHECK(none.empty());
        std::string num = ui_call(3, "5");
        CHECK(num == "10");
    }
    CHECK(logged("RESULT json {\"a\":1}"));
    // Lua classes and tweaks
    CHECK(has_op("classes.register_fn", "\"family\":\"comp\""));
    CHECK(has_op("classes.register_fn", "\"fn\":\"tick\""));
    CHECK(logged("RESULT defined 1"));
    if (g_ui_call) {
        std::string packed = ui_call(4, "{\"n\":3,\"a\":[1,null,3]}");
        CHECK(packed == "3");
    }
    bool hasTweak = false;
    for (auto& r : g_regs) if (r == "RimWorld.Need_Food.get_FoodFallPerTick") hasTweak = true;
    CHECK(hasTweak);
    CHECK(logged("RESULT tweak RK3001 13"));
    // a missing subject raises RK2001 instead of calling the host
    CHECK(logged("RESULT nosubject RK2001"));

    // legacy functions take a wrapped object or a plain handle
    CHECK(logged("RESULT handles done"));
    CHECK(has_op("pawn.set_drafted", "\"h\":7") && has_op("pawn.set_drafted", "\"h\":21"));
    CHECK(has_op("pawn.give_hediff", "\"h\":7"));
    CHECK(has_op("thing.set_hp", "\"h\":7"));
    CHECK(has_op("anomaly.knock_out", "\"h\":7"));
    CHECK(has_op("anomaly.start_capture", "\"entity\":22") && has_op("anomaly.start_capture", "\"platform\":23"));
    CHECK(has_op("work.set_priority", "\"h\":7"));

    // older bind_op functions take a wrapped object as the subject, not as an argument table
    CHECK(has_op("pawn.drafted", "\"h\":7") && has_op("thing.def", "\"h\":7") && has_op("map.width", "\"h\":7"));

    // needs from Lua: the def name and the options reach the host
    CHECK(has_op("need.define", "\"def\":\"MyNeed\"") && has_op("need.define", "fall_per_day"));
    CHECK(has_op("need.remove", "\"def\":\"MyNeed\""));

    // Ecosystem: the framework test file runs its own checks and logs TESTS pass=N fail=M
    if (argc > 4) {
        size_t before = g_logs.size();
        int rc = load(argv[4]);
        CHECK(rc == 0);
        bool ok = false;
        for (size_t i = before; i < g_logs.size(); ++i) {
            if (g_logs[i].find("TEST FAIL") != std::string::npos) std::printf("%s\n", g_logs[i].c_str());
            if (g_logs[i].find("TESTS pass=") != std::string::npos && g_logs[i].find("fail=0") != std::string::npos) ok = true;
        }
        CHECK(ok);
    }

    // Capabilities: a mod that declares only "files" may not use reflect or hooks, a mod with no declaration may.
    {
        namespace fs = std::filesystem;
        auto setctx = (void (*)(const char*))GetProcAddress(dll, "rimlua_set_mod_context");
        auto loaddir = (int (*)(const char*))GetProcAddress(dll, "rimlua_load_directory");
        fs::path base = fs::temp_directory_path() / "rimkit_cap_test";
        fs::remove_all(base);
        const char* lua =
            "local ok, err = pcall(function() return game.reflect.type(1) end)\n"
            "log.info('RESULT cap_reflect ' .. tostring(ok) .. ' ' .. (tostring(err):match('RK%d+') or 'none'))\n"
            "local ok2, err2 = pcall(function() game.hooks.postfix('A', 'B', function() end) end)\n"
            "log.info('RESULT cap_hooks ' .. tostring(ok2) .. ' ' .. (tostring(err2):match('RK%d+') or 'none'))\n";
        fs::create_directories(base / "strict" / "Lua");
        fs::create_directories(base / "strict" / "About");
        fs::create_directories(base / "legacy" / "Lua");
        fs::create_directories(base / "lvl1" / "Lua");
        fs::create_directories(base / "lvl1" / "About");
        const char* lvl1 =
            "local ok, err = pcall(function() return rim.pawn.name(1) end)\n"
            "log.info('RESULT lvl1_old ' .. tostring(ok) .. ' ' .. (tostring(err):match('RK%d+') or 'none'))\n"
            "local ok2, err2 = pcall(function() return game.pawns.name(1) end)\n"
            "log.info('RESULT lvl1_new ' .. tostring(ok2))\n"
            "local ok3, err3 = pcall(function() return game.reflect.type(1) end)\n"
            "log.info('RESULT lvl1_caps ' .. tostring(ok3) .. ' ' .. (tostring(err3):match('RK%d+') or 'none'))\n"
            "local okg, errg = pcall(game.gizmos.add, 'lvl1g', { label = 'G' }, function() end)\n"
            "log.info('RESULT lvl1_gizmo ' .. tostring(okg) .. ' ' .. tostring(errg))\n";
        std::ofstream(base / "lvl1" / "About" / "RimKit.json") << "{\"api_level\":1,\"version\":\"1.0.0\"}";
        std::ofstream(base / "lvl1" / "Lua" / "main.lua") << lvl1;
        std::ofstream(base / "strict" / "About" / "RimKit.json") << "{\"capabilities\":[\"files\"],\"version\":\"1.0.0\"}";
        std::ofstream(base / "strict" / "Lua" / "main.lua") << lua;
        std::ofstream(base / "legacy" / "Lua" / "main.lua") << lua;
        CHECK(setctx != nullptr && loaddir != nullptr);
        if (setctx && loaddir) {
            setctx("cap.strict");
            loaddir((base / "strict" / "Lua").string().c_str());
            CHECK(logged("RESULT cap_reflect false RK4001"));
            CHECK(logged("RESULT cap_hooks false RK4001"));
            g_logs.clear();
            setctx("cap.legacy");
            loaddir((base / "legacy" / "Lua").string().c_str());
            CHECK(logged("RESULT cap_reflect true") && logged("RESULT cap_hooks true"));
            g_logs.clear();
            setctx("cap.lvl1");
            loaddir((base / "lvl1" / "Lua").string().c_str());
            CHECK(logged("RESULT lvl1_old false RK1002"));
            CHECK(logged("RESULT lvl1_new true"));
            CHECK(logged("RESULT lvl1_caps false RK4001"));
            // ops that add something to the screen carry the mod id, so a hot reload can remove it again
            CHECK(has_op("gizmo.add", "\"_mod\":\"cap.lvl1\""));
            setctx(nullptr);
        }
        fs::remove_all(base);
    }

    std::printf(fails ? "FAILED %d\n" : "ALL OK\n", fails);
    return fails;
}
