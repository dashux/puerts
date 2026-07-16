# OHOS libpuerts.so 容器冒烟测试（无真机验证方案）

原理：OpenHarmony 与 Alpine Linux 同为 musl libc + aarch64，ELF 加载器路径相同
（`/lib/ld-musl-aarch64.so.1`）。在 Apple Silicon 的 Docker 中用 Alpine arm64 容器
即可真实运行 OHOS 交叉编译产物，验证 v8 引擎全链路（dlopen、内嵌 snapshot、
Eval、ES2020、Promise microtask）。

OHOS musl 的私有扩展符号（FORTIFY `_chk` 系列、`__at_fini` 等）由 `ohos_shim.c`
垫平（签名取自 OHOS sysroot `usr/include/fortify/*.h`）。

## 运行

```bash
NDK="/Applications/Tuanjie/Hub/Editor/2022.3.62t11/PlaybackEngines/OpenHarmonyPlayer/SDK/23/native"
DIR=$(mktemp -d)
cp host_test.c ohos_shim.c "$DIR"
cp ../../Assets/Puerts/Plugins/OpenHarmony/libs/arm64-v8a/libpuerts.so "$DIR"
cp "$NDK/llvm/lib/aarch64-linux-ohos/libc++_shared.so" "$DIR"

# host 程序用 OHOS 工具链编译（保证与真机同一 target）
"$NDK/llvm/bin/clang" --target=aarch64-linux-ohos --sysroot="$NDK/sysroot" \
    "$DIR/host_test.c" -o "$DIR/host_test"

docker run --rm --platform linux/arm64 -v "$DIR":/t alpine sh -c '
    apk add -q gcc musl-dev &&
    gcc -shared -fPIC /t/ohos_shim.c -o /t/libohos_shim.so &&
    for l in libc.so libm.so libdl.so libpthread.so librt.so; do
        ln -sf /lib/ld-musl-aarch64.so.1 /lib/$l; done;
    cd /t && LD_PRELOAD=./libohos_shim.so LD_LIBRARY_PATH=. ./host_test'
```

期望输出以 `ALL_SMOKE_PASS` 结尾（2026-07-16 实测通过，ApiLevel=19，Backend=0）。

局限：验证不了 OHOS 真实系统库环境与团结运行时集成，上线前仍需真机跑一次
`unity/build` 的冒烟 HAP（见 `unity/doc/ohos-integration.md` 第五节）。
