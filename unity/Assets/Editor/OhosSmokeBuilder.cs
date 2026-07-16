using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// OpenHarmony 冒烟包构建脚本（批处理模式使用）：
//   Tuanjie -batchmode -quit -projectPath <unity目录> -buildTarget OpenHarmony -executeMethod OhosSmokeBuilder.Build
public static class OhosSmokeBuilder
{
    // 探查团结 OpenHarmony 编辑器 API（首次运行用，输出到日志）
    public static void Probe()
    {
        var targets = Enum.GetNames(typeof(BuildTarget)).Where(n => n.ToLower().Contains("harmony")).ToArray();
        Debug.Log("[PROBE] BuildTarget: " + string.Join(",", targets));
        var groups = Enum.GetNames(typeof(BuildTargetGroup)).Where(n => n.ToLower().Contains("harmony")).ToArray();
        Debug.Log("[PROBE] BuildTargetGroup: " + string.Join(",", groups));

        var oh = typeof(PlayerSettings).GetNestedType("OpenHarmony");
        if (oh != null)
        {
            Debug.Log("[PROBE] PlayerSettings.OpenHarmony 静态属性: " + string.Join(" ; ",
                oh.GetProperties(BindingFlags.Public | BindingFlags.Static)
                  .Select(p => p.PropertyType.Name + " " + p.Name)));
        }
        else
        {
            Debug.Log("[PROBE] 未找到 PlayerSettings.OpenHarmony");
        }

        var eubs = typeof(EditorUserBuildSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.Name.ToLower().Contains("harmony") || p.Name.ToLower().Contains("export"))
            .Select(p => p.PropertyType.Name + " " + p.Name).ToArray();
        Debug.Log("[PROBE] EditorUserBuildSettings: " + string.Join(" ; ", eubs));
    }

    private static Scene PrepareScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var go = new GameObject("PuertsSmoke");
        go.AddComponent<Smoke>();
        Directory.CreateDirectory("Assets/OhosSmoke");
        EditorSceneManager.SaveScene(scene, "Assets/OhosSmoke/SmokeScene.unity");
        return scene;
    }

    // 通过内部设置类指定 Node.js 根目录（hvigor 打包依赖），路径可用环境变量 OHOS_NODEJS 覆盖
    private static void TrySetNodejsRoot()
    {
        string nodeRoot = Environment.GetEnvironmentVariable("OHOS_NODEJS");
        if (string.IsNullOrEmpty(nodeRoot)) nodeRoot = "/opt/homebrew/opt/node@18";
        if (!Directory.Exists(nodeRoot)) { Debug.Log("[SMOKE_BUILD] nodeRoot 不存在: " + nodeRoot); return; }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = null;
            try { type = asm.GetTypes().FirstOrDefault(t => t.Name.Contains("OpenHarmonyExternalToolsSettings")); }
            catch { continue; }
            if (type == null) continue;

            Debug.Log("[SMOKE_BUILD] 找到 " + type.FullName + " @ " + asm.GetName().Name);
            var prop = type.GetProperty("nodejsRootPath",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            if (prop == null)
            {
                Debug.Log("[SMOKE_BUILD] 成员: " + string.Join("; ",
                    type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                        .Select(p => p.Name)));
                continue;
            }

            object instance = null;
            if (!(prop.GetGetMethod(true) ?? prop.GetSetMethod(true)).IsStatic)
            {
                var instProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                            ?? type.GetProperty("instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var instField = type.GetField("s_Instance", BindingFlags.NonPublic | BindingFlags.Static);
                instance = instProp != null ? instProp.GetValue(null) : instField != null ? instField.GetValue(null) : null;
                if (instance == null)
                {
                    var getMethod = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                        .FirstOrDefault(m => m.ReturnType == type && m.GetParameters().Length == 0);
                    if (getMethod != null) instance = getMethod.Invoke(null, null);
                }
                if (instance == null) { Debug.Log("[SMOKE_BUILD] 未取到实例"); continue; }
            }

            prop.SetValue(instance, nodeRoot);
            Debug.Log("[SMOKE_BUILD] nodejsRootPath 已设置为 " + prop.GetValue(instance));
            return;
        }
        Debug.Log("[SMOKE_BUILD] 未找到 OpenHarmonyExternalToolsSettings");
    }

    public static void Build()
    {
        var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), "OpenHarmony");
        var group = (BuildTargetGroup)Enum.Parse(typeof(BuildTargetGroup), "OpenHarmony");

        TrySetNodejsRoot();

        PrepareScene();

        PlayerSettings.SetApplicationIdentifier(group, "com.puerts.ohossmoke");
        PlayerSettings.companyName = "puerts";
        PlayerSettings.productName = "PuertsOhosSmoke";
        PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);

        // targetArchitectures = ARM64（枚举来自团结扩展，反射设置避免硬编码依赖）
        var ohSettings = typeof(PlayerSettings).GetNestedType("OpenHarmony");
        var archProp = ohSettings.GetProperty("targetArchitectures", BindingFlags.Public | BindingFlags.Static);
        var archEnum = archProp.PropertyType;
        archProp.SetValue(null, Enum.Parse(archEnum, "ARM64"));

        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../build/ohos"));
        Directory.CreateDirectory(output);

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/OhosSmoke/SmokeScene.unity" },
            locationPathName = output,
            target = target,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("[SMOKE_BUILD] result=" + report.summary.result
            + " errors=" + report.summary.totalErrors
            + " output=" + report.summary.outputPath);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorApplication.Exit(1);
        }
    }
}
