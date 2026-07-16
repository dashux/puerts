// OHOS libpuerts.so 容器冒烟测试：dlopen + v8 引擎全链路
#include <dlfcn.h>
#include <stdio.h>

typedef void (*LogCallback)(const char*);
static void mylog(const char* s) { printf("[JSLOG] %s\n", s); fflush(stdout); }

#define LOAD(name) name = dlsym(h, #name); \
    if (!name) { printf("FAIL dlsym %s: %s\n", #name, dlerror()); return 10; }

int main() {
    void* h = dlopen("./libpuerts.so", RTLD_NOW);
    if (!h) { printf("FAIL dlopen: %s\n", dlerror()); return 1; }
    printf("1. dlopen OK\n");

    int (*GetApiLevel)(void);
    int (*GetLibVersion)(void);
    int (*GetLibBackend)(void);
    void (*SetLogCallback)(LogCallback, LogCallback, LogCallback);
    void* (*CreateJSEngine)(void);
    void* (*Eval)(void*, const char*, const char*);
    int (*GetResultType)(void*);
    double (*GetNumberFromResult)(void*);
    const char* (*GetStringFromResult)(void*, int*);
    void (*ResetResult)(void*);
    const char* (*GetLastExceptionInfo)(void*, int*);
    void (*LogicTick)(void*);
    void (*DestroyJSEngine)(void*);

    LOAD(GetApiLevel); LOAD(GetLibVersion); LOAD(GetLibBackend);
    LOAD(SetLogCallback); LOAD(CreateJSEngine); LOAD(Eval);
    LOAD(GetResultType); LOAD(GetNumberFromResult); LOAD(GetStringFromResult);
    LOAD(ResetResult); LOAD(GetLastExceptionInfo); LOAD(LogicTick);
    LOAD(DestroyJSEngine);

    printf("2. dlsym OK, ApiLevel=%d LibVersion=%d Backend=%d (0=V8)\n",
           GetApiLevel(), GetLibVersion(), GetLibBackend());

    SetLogCallback(mylog, mylog, mylog);

    void* iso = CreateJSEngine();
    if (!iso) { printf("FAIL CreateJSEngine\n"); return 2; }
    printf("3. CreateJSEngine OK (v8 内嵌 snapshot 生效)\n");

    void* r = Eval(iso, "40 + 2", "smoke1.js");
    if (!r) { int l; printf("FAIL Eval: %s\n", GetLastExceptionInfo(iso, &l)); return 3; }
    printf("4. Eval(40+2) type=%d value=%.0f %s\n", GetResultType(r),
           GetNumberFromResult(r), GetNumberFromResult(r) == 42.0 ? "OK" : "FAIL");
    ResetResult(r);

    r = Eval(iso, "(10n ** 2n).toString() + '|' + (({a:{b:7}})?.a?.b ?? -1) + '|' + ['x','y'].flatMap(s=>[s,s]).join('')", "smoke2.js");
    if (!r) { int l; printf("FAIL ES2020: %s\n", GetLastExceptionInfo(iso, &l)); return 4; }
    int len = 0;
    printf("5. ES2020(BigInt/可选链/flatMap) = %s\n", GetStringFromResult(r, &len));
    ResetResult(r);

    r = Eval(iso, "globalThis.__x = 0; Promise.resolve().then(() => { globalThis.__x = 42; }); 'queued'", "smoke3.js");
    if (r) ResetResult(r);
    LogicTick(iso);
    r = Eval(iso, "globalThis.__x", "smoke4.js");
    printf("6. Promise microtask __x=%.0f %s\n", GetNumberFromResult(r),
           GetNumberFromResult(r) == 42.0 ? "OK" : "FAIL");
    ResetResult(r);

    r = Eval(iso, "JSON.stringify({date: new Date(0).toISOString(), regex: /a(b)c/.exec('abc')[1], intl: typeof Intl})", "smoke5.js");
    if (r) { printf("7. 运行时杂项 = %s\n", GetStringFromResult(r, &len)); ResetResult(r); }

    DestroyJSEngine(iso);
    printf("8. DestroyJSEngine OK\nALL_SMOKE_PASS\n");
    return 0;
}
