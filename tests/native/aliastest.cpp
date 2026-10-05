// Native smoke test for the hook protocol: no game needed.
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

static std::vector<std::string> g_logs;
static std::vector<std::string> g_regs;
static std::vector<std::string> g_ops;
static void on_log(const char* m) { g_logs.push_back(m); std::printf("LOG %s\n", m); }
static int on_reg(const char* t, const char* m, int kind, int id, const char* opts) {
    char buf[512];
    std::snprintf(buf, sizeof(buf), "%s.%s kind=%d id=%d opts=%s", t, m, kind, id, opts);
    g_regs.push_back(buf);
    std::printf("REG %s\n", buf);
    return 1;
}
static void on_unreg(int id) { std::printf("UNREG %d\n", id); }
static char* on_inv(const char* op, const char* args) {
    g_ops.push_back(std::string(op) + " " + args);
    std::printf("OP %s %s\n", op, args);
    std::string r = "{\"ok\":true,\"t\":\"s\",\"v\":\"Ana\"}";
    if (std::string(op) == "pawn.skills") r = "{\"ok\":true,\"t\":\"j\",\"v\":[{\"def\":\"Shooting\",\"level\":7,\"passion\":\"Major\"}]}";
    if (std::string(op) == "ui.translate") r = "{\"ok\":true,\"t\":\"s\",\"v\":\"translated\"}";
    if (std::string(op) == "events.list") r = "{\"ok\":true,\"t\":\"j\",\"v\":[{\"name\":\"pawn.died\",\"hot\":false}]}";
    char* m = (char*)std::malloc(r.size() + 1);
    std::memcpy(m, r.c_str(), r.size() + 1);
    return m;
}
static void on_free(char* p) { std::free(p); }

#define CHECK(c) do { if (!(c)) { std::printf("FAIL line %d: %s\n", __LINE__, #c); fails++; } } while (0)

int main(int argc, char** argv) {
    int fails = 0;
    HMODULE dll = LoadLibraryA(argv[1]);
    if (!dll) { std::printf("cannot load dll\n"); return 2; }
    auto init = (int (*)(const cbs*))GetProcAddress(dll, "rimlua_init");
    auto load = (int (*)(const char*))GetProcAddress(dll, "rimlua_load_script");
    auto emit_ex = (void (*)(const char*, const char*))GetProcAddress(dll, "rimlua_emit_event_ex");
    auto on_load = (void (*)())GetProcAddress(dll, "rimlua_call_on_load");
    cbs c{on_log, on_log, on_reg, on_unreg, on_inv, on_free};
    CHECK(init(&c) == 0);
    CHECK(load(argv[2]) == 0);
    on_load();
    emit_ex("pawn.died", "{\"pawn\":{\"$h\":9,\"$k\":\"pawn\",\"$t\":\"Verse.Pawn\"},\"culprit\":{\"$h\":11,\"$k\":\"object\",\"$t\":\"Verse.Hediff\",\"def\":\"Burn\"}}");

    int depr = 0, oldname = 0, canon = 0, legacy = 0, ok = 0;
    for (auto& l : g_logs) {
        if (l.find("deprecated:") != std::string::npos) depr++;
        if (l.find("deprecated: rim.pawn.name is now game.pawns.name") != std::string::npos) oldname++;
        if (l.find("ALIAS OK") != std::string::npos) ok++;
        if (l.find("C Ana") != std::string::npos) canon++;
        if (l.find("L Ana") != std::string::npos) legacy++;
    }
    std::printf("deprecation lines=%d\n", depr);
    int pawnsOk = 0, degree = 0;
    for (auto& l : g_logs) if (l.find("PAWNS OK") != std::string::npos) pawnsOk++;
    for (auto& o : g_ops) if (o.find("pawn.add_trait") == 0 && o.find("\"degree\":2") != std::string::npos) degree++;
    CHECK(pawnsOk == 1);
    CHECK(degree == 1);
    CHECK(ok == 1);
    CHECK(oldname == 1);
    CHECK(depr == 2);  // rim.pawn.name once, legacy event name once
    int culpritDef = 0;
    for (auto& l : g_logs) if (l.find("DEF Burn") != std::string::npos) culpritDef++;
    CHECK(culpritDef == 1);
    CHECK(canon == 1);
    CHECK(legacy == 1);
    std::printf(fails ? "FAILED %d\n" : "ALL OK\n", fails);
    return fails;
}
