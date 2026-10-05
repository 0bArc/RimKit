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
    auto invoke_ex = (const char* (*)(int, const char*))GetProcAddress(dll, "rimlua_invoke_hook_ex");
    auto emit_ex = (void (*)(const char*, const char*))GetProcAddress(dll, "rimlua_emit_event_ex");
    auto on_load = (void (*)())GetProcAddress(dll, "rimlua_call_on_load");
    CHECK(init && load && invoke_ex && emit_ex);
    cbs c{on_log, on_log, on_reg, on_unreg, on_inv, on_free};
    CHECK(init(&c) == 0);
    CHECK(load(argv[2]) == 0);
    on_load();

    CHECK(g_regs.size() >= 4);

    // Ids: 1 prefix, 2 postfix, 3 finalizer (patch order prefix, postfix, finalizer), 4 redirect.
    const char* ctx =
        "{\"method\":\"Verse.Thing.TakeDamage\",\"phase\":\"prefix\",\"pawn\":0,\"instance\":{\"$h\":7,\"$k\":\"thing\",\"$t\":\"Verse.Thing\"},"
        "\"args\":[10,{\"$h\":9,\"$k\":\"pawn\"},\"he said \\\"}\\\" {\",{\"x\":1,\"y\":0,\"z\":2}],"
        "\"arg_names\":[\"amount\",\"who\",\"text\",\"cell\"],\"arg_modes\":[\"in\",\"in\",\"in\",\"ref\"],"
        "\"has_result\":true,\"result\":3.5}";
    std::string r = invoke_ex(1, ctx);
    std::printf("RESP1 %s\n", r.c_str());
    // set_result in a prefix does not clear cont here: the host skips the original when a prefix returns a result.
    CHECK(r.find("\"cont\":true") != std::string::npos);
    CHECK(r.find("\"has_result\":true") != std::string::npos);
    CHECK(r.find("\"result\":42") != std::string::npos);
    CHECK(r.find("\"args\":{\"1\":15}") != std::string::npos);
    CHECK(r.find("\"state\":{\"n\":1}") != std::string::npos);

    std::string post = "{\"method\":\"Verse.Thing.TakeDamage\",\"phase\":\"postfix\",\"pawn\":0,\"instance\":null,\"args\":[],\"has_result\":true,\"result\":42,\"state\":{\"n\":1}}";
    r = invoke_ex(2, post.c_str());
    std::printf("RESP2 %s\n", r.c_str());
    CHECK(r.find("\"cont\":true") != std::string::npos);

    std::string fin = "{\"method\":\"Verse.Thing.TakeDamage\",\"phase\":\"finalizer\",\"args\":[],\"has_result\":false,\"exception\":\"InvalidOperationException: boom\"}";
    r = invoke_ex(3, fin.c_str());
    std::printf("RESP3 %s\n", r.c_str());
    CHECK(r.find("\"suppress\":true") != std::string::npos);

    std::string red = "{\"method\":\"Verse.X.Y\",\"phase\":\"replace_call\",\"pawn\":0,\"instance\":null,\"args\":[3,4],\"has_result\":false}";
    r = invoke_ex(4, red.c_str());
    std::printf("RESP4 %s\n", r.c_str());
    CHECK(r.find("\"result\":7") != std::string::npos);

    emit_ex("pawn.died", "{\"pawn\":{\"$h\":9,\"$k\":\"pawn\",\"$t\":\"Verse.Pawn\"},\"damage\":{\"def\":\"Cut\",\"amount\":12.5}}");

    bool sawSubscribe = false;
    for (auto& o : g_ops) if (o.find("events.subscribe") == 0 && o.find("pawn.died") != std::string::npos) sawSubscribe = true;
    CHECK(sawSubscribe);

    bool a = false, b = false, d = false, e = false, f = false;
    for (auto& l : g_logs) {
        if (l.find("T1 amount=10 who=Ana text=he said \"}\" {") != std::string::npos) a = true;
        if (l.find("T1 cell=1,2 mode=ref result=3.5") != std::string::npos) b = true;
        if (l.find("T2 state=1 result=42") != std::string::npos) d = true;
        if (l.find("T3 exception=InvalidOperationException: boom") != std::string::npos) e = true;
        if (l.find("T5 pawn=Ana dmg=Cut 12.5") != std::string::npos) f = true;
    }
    CHECK(a); CHECK(b); CHECK(d); CHECK(e); CHECK(f);
    std::printf(fails ? "FAILED %d\n" : "ALL OK\n", fails);
    return fails;
}
