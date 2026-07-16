# puerts v1.4.3 鸿蒙（OpenHarmony）适配与接入指南

> 分支：`v1.4.3-ohos`（基于官方 `Unity_v1.4.3` tag）
> 适用工程：fujian（团结引擎 2022.3.62t11 / Tuanjie 1.9.3）
> 日期：2026-07

## 一、结论摘要

本分支为 puerts v1.4.3 新增 **OpenHarmony arm64** 平台支持：

- **Android / iOS / WebGL 的现有产物与代码零改动**。所有新增内容都是 OHOS 专属文件或被 `PLATFORM_OHOS` / `elseif (OHOS)` 隔离的构建分支。
- OHOS 后端使用 **v8 9.4.146.24**（官方 `V8_9.4.146.24_240430` release 的 OHOS 预编译库）。这与 Android/iOS 现用的 nodejs_16 后端内嵌的 V8 是**同一个版本号**，JS 语言行为一致。
- v8 在 OHOS 上以 **jitless 模式**运行（与 iOS 相同），性能档位对齐 iOS。
- 产物：`unity/Assets/Puerts/Plugins/OpenHarmony/libs/arm64-v8a/libpuerts.so`（已 strip，约 23MB），依赖仅 `libc++_shared.so` 与 `libc.so`（团结 OpenHarmony 运行时 libtuanjie.so 自身依赖 libc++_shared，出包必然包含）。
- 已验证：90 个 `PuertsDLL.cs` P/Invoke 入口全部在 so 导出中命中；fujian 内嵌 Puerts 的 `PuertsDLL.cs` 与本分支完全一致，ABI 匹配。

## 二、改动清单（相对 Unity_v1.4.3）

| 文件 | 改动 |
|---|---|
| `unity/native_src/make.mts` | 新增 `ohos` 平台（arm64），使用 `OHOS_NDK` 环境变量 + `ohos.toolchain.cmake` + Ninja；快捷命令支持 `vh8` |
| `unity/native_src/CMakeLists.txt` | 新增 `elseif (OHOS)` 分支：v8 静态库 whole-archive 链接、`PLATFORM_OHOS`/`PLATFORM_OHOS_ARM64` 宏、产物 llvm-strip |
| `unity/native_src/Src/JSEngine.cpp` | OHOS 加入 jitless（同 iOS）；OHOS 下跳过显式 SetSnapshotDataBlob（240430 包的 libwee8.a 已内嵌 snapshot，且包内不再提供 Blob 头文件） |
| `unity/native_src/cmake/v8_9.4_ohos/` | 新增独立后端配置（backend.json + backend.rc），与现有 `v8_9.4`/`quickjs`/`nodejs_16` 配置完全隔离 |
| `unity/Assets/Puerts/Plugins/OpenHarmony/` | so 产物 + 团结 OpenHarmony 平台的 PluginImporter .meta（CPU: ARM64） |
| `unity/Assets/OhosSmoke/`、`unity/Assets/Editor/OhosSmokeBuilder.cs` | 冒烟验证场景与批处理构建脚本（仅 demo 用，不需要拷入业务工程） |

官方参照：本适配移植自 puerts 主仓库 2024 年为旧架构 native_src 做的鸿蒙支持提交（`c959f71c`、`debd1c25`、`c172814d`、`3584dadd`、`b90519c1`）。

## 三、重新编译 libpuerts.so（如需）

环境要求：
- macOS/Windows + OpenHarmony native SDK。**团结引擎自带**，无需安装 DevEco Studio：
  `/Applications/Tuanjie/Hub/Editor/2022.3.62t11/PlaybackEngines/OpenHarmonyPlayer/SDK/23/native`
- Node.js ≥ 22.6（node 24 可直接执行 .mts）

步骤：

```bash
cd unity/native_src
npm install

# 下载后端包（地址见 cmake/v8_9.4_ohos/backend.rc）并解压，保证以下布局：
#   native_src/v8_9.4_ohos/Inc/...
#   native_src/v8_9.4_ohos/Lib/OHOS/arm64-v8a/libwee8.a
curl -L -o /tmp/v8_bin.tgz https://github.com/puerts/backend-v8/releases/download/V8_9.4.146.24_240430/v8_bin_9.4.146.24.tgz
mkdir -p v8_9.4_ohos && tar -xzf /tmp/v8_bin.tgz -C /tmp && cp -R /tmp/v8_9.4.146.24/Inc /tmp/v8_9.4.146.24/Lib v8_9.4_ohos/

export OHOS_NDK="/Applications/Tuanjie/Hub/Editor/2022.3.62t11/PlaybackEngines/OpenHarmonyPlayer/SDK/23/native"
node make.mts --platform ohos --arch arm64 --backend v8_9.4_ohos --config Release
# 产物自动输出到 unity/Assets/Puerts/Plugins/OpenHarmony/libs/arm64-v8a/libpuerts.so
```

## 四、fujian 接入步骤（Unity 侧）

1. **拷贝插件**：把本分支 `unity/Assets/Puerts/Plugins/OpenHarmony/` 整个目录（含全部 `.meta`）拷贝到
   `fujian/Assets/Puerts/Plugins/OpenHarmony/`。不触碰 Plugins 下任何现有文件。
2. **确认导入设置**：团结编辑器中选中 `libpuerts.so`，Inspector 应显示仅勾选 OpenHarmony 平台、CPU=ARM64（meta 已配置好，正常无需手动修改）。
3. **工程设置**（Build Settings 切到 OpenHarmony）：
   - Scripting Backend：IL2CPP；Target Architectures：ARM64
   - 签名：Player Settings → Publishing Settings 配置 certificate（.cer/.p12 相关按团结文档）与 profile（.p7b）；日常调试也可 `Export Project` 后用 DevEco Studio 自动签名
   - 网络权限：勾选 Internet 权限（`forceInternetPermission`）或在导出工程的 `module.json5` 中确认 `ohos.permission.INTERNET`
4. **业务代码不需要任何改动**：OHOS 上 `DllImport("puerts")` 与 Android 加载 so 的方式一致，`new JsEnv(loader, port)` 原样可用。

### OHOS 与 Android/iOS 的运行时差异（重要）

| 差异点 | Android/iOS（nodejs_16 后端） | OHOS（v8_9.4 后端） |
|---|---|---|
| `PuertsDLL.GetLibBackend()` | 1（NodeJS） | 0（V8） |
| JsEnv 初始化路径 | 执行 `nodepatch.mjs` | 执行 `polyfill.mjs` + `cjsload.mjs` + `modular.mjs`（fujian 的 Resources 已内置全部所需模块，无缺失） |
| CommonJS `require` | Node 原生 | puerts 的 cjsload/modular 实现，走 ILoader 解析 |
| Node 内置模块（fs/net/http/buffer/crypto/ws...） | 可用 | **不可用**——业务 JS 需要按附录改造打包 target |
| JIT | 有 | 无（jitless，同 iOS 档位） |
| 调试 | inspector | inspector 保留（`WITH_INSPECTOR` 已编入）：`new JsEnv(loader, 9222)` 后 `hdc fport tcp:9222 tcp:9222`，Chrome `devtools://` 连接 |

## 五、冒烟验证（demo 工程）

本分支 `unity/` 即是一个可打开的团结工程：

```bash
# 批处理出包（或在编辑器里打开 unity/ 工程，切 OpenHarmony 平台构建）
"/Applications/Tuanjie/Hub/Editor/2022.3.62t11/Tuanjie.app/Contents/MacOS/Tuanjie" \
  -batchmode -quit -projectPath <本仓库>/unity -buildTarget OpenHarmony \
  -executeMethod OhosSmokeBuilder.Build -logFile /tmp/ohos_build.log
```

注意事项（已实测）：
- 团结 OpenHarmony 出包需要 **Node.js 18**（hvigor 使用）。编辑器里在 Preferences → External Tools 配置；批处理模式下 `OhosSmokeBuilder` 会自动设置（默认 `/opt/homebrew/opt/node@18`，可用环境变量 `OHOS_NODEJS` 覆盖）。
- 未配置证书时团结使用**默认调试签名**，产物 `build/ohos/entry-default-signed.hap` 可直接安装到开了开发者模式的真机。
- HAP 会自动打入 `libc++_shared.so`（libpuerts.so 的运行时依赖，无需手工处理）。

真机安装与查看：

```bash
HDC="/Applications/Tuanjie/Hub/Editor/2022.3.62t11/PlaybackEngines/OpenHarmonyPlayer/SDK/23/toolchains/hdc"
$HDC list targets                       # 确认设备已连接
$HDC install -r unity/build/ohos/entry-default-signed.hap
$HDC shell aa start -b com.puerts.ohossmoke -a EntryAbility
$HDC shell "hilog | grep PUERTS_SMOKE"  # 或直接看屏幕输出
```

真机屏幕上应依次显示：JsEnv 创建（backend=0）、Eval 算术、ES2020 特性、JS→C# 互调、setTimeout 触发，全部 OK。

## 六、附录：unity-typescript 新增 ohos 打包 target 改造路线（JS 侧，后续阶段）

fujian 的游戏 JS（`main.cjs`）在 Android/iOS 上依赖 Node 运行时（打包目标 `DISTRIBUTE_PLATFORM=app`），OHOS 没有 Node。参照微信小游戏 target（`wechat`）的成熟做法新增 `ohos` target：

1. **esbuild 配置**（`scripts/esbuild.ts`）：
   - `DISTRIBUTE_PLATFORM` 增加 `'ohos'`；`resolveEsbuildPlatform()` 对 ohos 返回 `'browser'`（触发依赖库的 browser 分支 + 纯算法类 Node 模块走 polyfill：buffer/events/stream/util 等用 esbuild 的 node-polyfill 插件注入）
   - 新增入口 `entry/core/ohos.ts`、`entry/update/ohos.ts`
2. **适配器**（`src/adapters/ohos/`，逐个替换 `entry/core/app.ts` 中的 NodeJs 实现）：

   | gamePlatform 接口 | app（Node）实现 | ohos 建议实现 |
   |---|---|---|
   | `websocket` | `ws` | C# 桥：`System.Net.WebSockets.ClientWebSocket` 封装成 W3C WebSocket 接口注入 JS |
   | `mqtt` | mqtt.js over ws | mqtt.js 支持自定义 WebSocket 类，复用上行 |
   | `request` | node http/https | C# 桥：`UnityWebRequest`（照 `adapters/wechat/request.ts` 的接口形状） |
   | `fs` | node fs | C# 桥：`System.IO`（persistentDataPath） |
   | `downloader` | node fs+http | C# 桥：`UnityWebRequest` + `System.IO` |
   | `jwt` | jsonwebtoken（node crypto） | 纯 JS 实现（crypto-js/jose）或 C# 签名桥 |
   | `logger`/`recorder`/`location`/`backgroundMusic`/`nativeBridge` | 各 Node/原生实现 | 按 OHOS 能力逐个评估，首版可空实现/降级 |

3. **入口文件加载**：fujian 的 `RemoteLoader` 用 `Application.persistentDataPath` 读热更 `main.cjs`、`Resources` 读内置模块——这两条路径在 OHOS 上行为与 Android 一致，无需改造。
4. **验证顺序建议**：先用 Unity 侧冒烟（第五节）确认引擎层 OK → 再出 ohos bundle 跑到登录/大厅 → 最后长链路（mqtt 对局）联调。

## 七、已知限制

- 仅 arm64-v8a（HarmonyOS 5 真机均为 arm64；如需模拟器 x86_64 或 armv7 可按 make.mts 模式自行补充，后端包内有对应 libwee8.a）。
- jitless 模式下无 WebAssembly、JS 执行性能低于带 JIT 的 Android（与 iOS 同档）。
- `--stack_size=856` 等 v8 flags 与 Android/iOS 保持一致，未针对 OHOS 调整。
- 其他原生 SDK（GME 语音、穿山甲广告、登录/支付等）的鸿蒙适配不在本次范围。
