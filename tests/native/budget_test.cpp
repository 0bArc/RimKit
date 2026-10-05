// Native tests for Phase 0: per-mod error budget, strict errors, explicit binder keys, hook cost report.
// Usage: budget_test rimlua_core.dll budget_test.lua
#include <windows.h>

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

static std::vector<std::string> g_logs, g_ops, g_unreg_ids;
static int g_unreg = 0;
static int g_messages = 0;
static void on_log(const char* m) { g_logs.push_back(m); }
static void on_message(const char*) { ++g_messages; }
static int on_reg(const char*, const char*, int, int, const char*) { return 1; }
static void on_unreg(int id) { ++g_unreg; g_unreg_ids.push_back(std::to_string(id)); }
static char* on_inv(const char* op, const char* args) {
    g_ops.push_back(std::string(op) + " " + args);
    std::string r = "{\"ok\":true,\"t\":\"s\",\"v\":\"ok\"}";
    if (std::string(op) == "pawn.name") r = "{\"ok\":false,\"e\":\"RK2001: pawn handle is stale or null\"}";
    if (std::string(op) == "hooks.stats") r = "{\"ok\":true,\"t\":\"j\",\"v\":[{\"id\":1,\"calls\":42,\"total_us\":210.5,\"avg_us\":5.01,\"fast\":false}]}";
    char* m = (char*)std::malloc(r.size() + 1);
    std::memcpy(m, r.c_str(), r.size() + 1);
    return m;
}
static void on_free(char* p) { std::free(p); }

#define CHECK(c) do { if (!(c)) { std::printf("FAIL line %d: %s\n", __LINE__, #c); fails++; } } while (0)

static bool logged(const std::string& needle) {
    for (auto& l : g_logs) if (l.find(needle) != std::string::npos) return true;
    return false;
}
static int count_logged(const std::string& needle) {
    int n = 0;
    for (auto& l : g_logs) if (l.find(needle) != std::string::npos) ++n;
    return n;
}

int main(int argc, char** argv) {
    int fails = 0;
    HMODULE dll = LoadLibraryA(argv[1]);
    if (!dll) { std::printf("cannot load dll\n"); return 2; }
    auto init = (int (*)(const cbs*))GetProcAddress(dll, "rimlua_init");
    auto load = (int (*)(const char*))GetProcAddress(dll, "rimlua_load_script");
    auto set_mod = (void (*)(const char*))GetProcAddress(dll, "rimlua_set_mod_context");
    auto mod_disabled = (int (*)(const char*))GetProcAddress(dll, "rimlua_mod_disabled");
    auto invoke_ex = (const char* (*)(int, const char*))GetProcAddress(dll, "rimlua_invoke_hook_ex");
    auto emit_ex = (void (*)(const char*, const char*))GetProcAddress(dll, "rimlua_emit_event_ex");
    auto on_load = (void (*)())GetProcAddress(dll, "rimlua_call_on_load");
    auto on_tick = (void (*)())GetProcAddress(dll, "rimlua_call_on_tick");
    auto version = (const char* (*)())GetProcAddress(dll, "rimlua_version");
    cbs c{on_log, on_message, on_reg, on_unreg, on_inv, on_free};
    CHECK(init(&c) == 0);
    CHECK(version && std::strlen(version()) > 0);

    // A noisy mod and a quiet one. Callbacks they register are charged to their package ids.
    set_mod("test.noisy");
    CHECK(load(argv[2]) == 0);          // registers hook 1, an event handler and an on_tick that all raise
    set_mod("test.quiet");
    CHECK(load(argv[3]) == 0);          // registers an event handler that counts
    set_mod(nullptr);
    on_load();
    CHECK(mod_disabled("test.noisy") == 0);

    const char* ctx = "{\"method\":\"X.Y\",\"phase\":\"postfix\",\"pawn\":0,\"instance\":null,\"args\":[],\"has_result\":false}";
    for (int i = 0; i < 40; ++i) {
        invoke_ex(1, ctx);
        emit_ex("thing.spawned", "{}");
        on_tick();
    }
    CHECK(mod_disabled("test.noisy") == 1);
    CHECK(mod_disabled("test.quiet") == 0);
    CHECK(logged("test.noisy was switched off after 20 Lua errors"));
    CHECK(count_logged("was switched off") == 1);     // only once
    CHECK(g_unreg >= 1);
    CHECK(g_messages == 1);                           // player sees one message

    // The quiet mod keeps running: its handler counted every event.
    emit_ex("thing.despawned", "{}");
    CHECK(logged("QUIET handled thing.despawned"));
    int quietCalls = count_logged("QUIET handled thing.spawned");
    CHECK(quietCalls == 40);

    // Strict errors: a failing legacy op raises when strict is on, returns nil otherwise.
    CHECK(logged("RESULT lenient=nil"));
    CHECK(logged("RESULT strict raised RK2001"));

    // Explicit binder keys: has_patch sends both type and method.
    bool sentBoth = false;
    for (auto& o : g_ops) {
        if (o.find("harmony.has_patch") == 0 && o.find("\"type\":\"Verse.Thing\"") != std::string::npos &&
            o.find("\"method\":\"TakeDamage\"") != std::string::npos) sentBoth = true;
    }
    CHECK(sentBoth);

    // Hook cost report merges the host's numbers into game.hooks.list rows.
    CHECK(logged("RESULT hooks calls=42"));

    std::printf(fails ? "FAILED %d\n" : "ALL OK\n", fails);
    return fails;
}
