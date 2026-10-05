// Benchmark: the same Lua hook called through the JSON path and through the scalar fast path.
// Usage: hookbench rimlua_core.dll hookbench.lua
#include <windows.h>

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <string>

typedef void (*log_fn)(const char*);
typedef int (*reg_fn)(const char*, const char*, int, int, const char*);
typedef void (*unreg_fn)(int);
typedef char* (*inv_fn)(const char*, const char*);
typedef void (*free_fn)(char*);
struct cbs { log_fn log; log_fn message; reg_fn reg; unreg_fn unreg; inv_fn inv; free_fn fr; };

static void on_log(const char*) {}
static int on_reg(const char*, const char*, int, int, const char*) { return 1; }
static void on_unreg(int) {}
static char* on_inv(const char*, const char*) {
    const char* r = "{\"ok\":true,\"t\":\"s\",\"v\":\"\"}";
    char* m = (char*)std::malloc(std::strlen(r) + 1);
    std::strcpy(m, r);
    return m;
}
static void on_free(char* p) { std::free(p); }

static double now_us() {
    LARGE_INTEGER f, c;
    QueryPerformanceFrequency(&f);
    QueryPerformanceCounter(&c);
    return c.QuadPart * 1e6 / f.QuadPart;
}

int main(int argc, char** argv) {
    HMODULE dll = LoadLibraryA(argv[1]);
    if (!dll) { std::printf("cannot load dll\n"); return 2; }
    auto init = (int (*)(const cbs*))GetProcAddress(dll, "rimlua_init");
    auto load = (int (*)(const char*))GetProcAddress(dll, "rimlua_load_script");
    auto invoke_ex = (const char* (*)(int, const char*))GetProcAddress(dll, "rimlua_invoke_hook_ex");
    auto invoke_fast = (int (*)(int, int, int, const double*, const unsigned char*, int, double, unsigned char, double*, int*))
        GetProcAddress(dll, "rimlua_invoke_hook_fast");
    auto set_info = (void (*)(int, const char*, const char*, const char*))GetProcAddress(dll, "rimlua_set_hook_info");
    cbs c{on_log, on_log, on_reg, on_unreg, on_inv, on_free};
    if (init(&c) != 0 || load(argv[2]) != 0) { std::printf("init or load failed\n"); return 2; }
    set_info(1, "Verse.TickManager.TickRateMultiplier", "postfix", "[]");

    const int n = 100000;
    const char* ctx =
        "{\"method\":\"Verse.TickManager.TickRateMultiplier\",\"phase\":\"postfix\",\"pawn\":0,\"instance\":{\"$h\":3,\"$k\":\"object\","
        "\"$t\":\"Verse.TickManager\"},\"args\":[],\"arg_names\":[],\"arg_modes\":[],\"has_result\":true,\"result\":3.0}";

    // warm up
    for (int i = 0; i < 1000; ++i) invoke_ex(1, ctx);
    double t0 = now_us();
    int ok = 0;
    for (int i = 0; i < n; ++i) {
        const char* r = invoke_ex(1, ctx);
        if (r && std::strstr(r, "\"result\":6")) ++ok;
    }
    double json_us = (now_us() - t0) / n;

    double out = 0;
    int flags = 0;
    for (int i = 0; i < 1000; ++i) invoke_fast(1, 0, 0, nullptr, nullptr, 1, 3.0, 0, &out, &flags);
    t0 = now_us();
    int ok2 = 0;
    for (int i = 0; i < n; ++i) {
        if (invoke_fast(1, 0, 0, nullptr, nullptr, 1, 3.0, 0, &out, &flags) == 0 && (flags & 1) && out == 6.0) ++ok2;
    }
    double fast_us = (now_us() - t0) / n;

    std::printf("json path: %.2f us per call (correct %d/%d)\n", json_us, ok, n);
    std::printf("fast path: %.2f us per call (correct %d/%d)\n", fast_us, ok2, n);
    std::printf("speedup: %.2fx\n", json_us / fast_us);
    return (ok == n && ok2 == n) ? 0 : 1;
}
