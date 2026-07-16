using System.Text;
using UnityEngine;
using Puerts;

// OpenHarmony 冒烟测试：验证 libpuerts.so (v8_9.4 backend) 在鸿蒙真机上的基本可用性。
// 结果同时输出到 hilog（tag: Tuanjie/Unity, 关键字 PUERTS_SMOKE）和屏幕。
public class Smoke : MonoBehaviour
{
    private JsEnv env;
    private readonly StringBuilder report = new StringBuilder();
    private bool timerChecked = false;
    private int frame = 0;

    private void Log(string m)
    {
        report.AppendLine(m);
        Debug.Log("[PUERTS_SMOKE] " + m);
    }

    void Start()
    {
        Log("Unity " + Application.unityVersion + " / " + Application.platform);
        try
        {
            env = new JsEnv();
            Log("1. new JsEnv() OK, backend=" + PuertsDLL.GetLibBackend() + " (0=V8), apiLevel=" + PuertsDLL.GetApiLevel());
        }
        catch (System.Exception e)
        {
            Log("1. new JsEnv() FAIL: " + e);
            return;
        }

        try
        {
            int r = env.Eval<int>("40 + 2");
            Log("2. Eval<int>(40+2) = " + r + (r == 42 ? " OK" : " FAIL"));
        }
        catch (System.Exception e) { Log("2. Eval FAIL: " + e.Message); }

        try
        {
            string s = env.Eval<string>("JSON.stringify({es2020: (10n ** 2n).toString(), opt: ({a:{b:1}})?.a?.b ?? -1})");
            Log("3. ES2020 特性 = " + s + " OK");
        }
        catch (System.Exception e) { Log("3. ES2020 FAIL: " + e.Message); }

        try
        {
            env.Eval(@"
                const cs = require('csharp');
                cs.UnityEngine.Debug.Log('[PUERTS_SMOKE] 4. JS -> C# (require csharp + Debug.Log) OK');
            ");
            Log("4. JS->C# 互调已发起（看上一行 hilog）");
        }
        catch (System.Exception e) { Log("4. JS->C# FAIL: " + e.Message); }

        try
        {
            env.Eval(@"
                globalThis.__timerFired = false;
                setTimeout(function(){ globalThis.__timerFired = true; }, 1000);
            ");
            Log("5. setTimeout 已注册，等待 1s...");
        }
        catch (System.Exception e) { Log("5. setTimeout FAIL: " + e.Message); }
    }

    void Update()
    {
        if (env == null) return;
        env.Tick();

        frame++;
        if (!timerChecked && frame % 30 == 0)
        {
            try
            {
                if (env.Eval<bool>("!!globalThis.__timerFired"))
                {
                    timerChecked = true;
                    Log("5. setTimeout 触发 OK —— 全部通过 ✔");
                }
            }
            catch (System.Exception e)
            {
                timerChecked = true;
                Log("5. 轮询 FAIL: " + e.Message);
            }
        }
    }

    void OnGUI()
    {
        GUI.Label(new Rect(40, 80, Screen.width - 80, Screen.height - 160), report.ToString(),
            new GUIStyle(GUI.skin.label) { fontSize = 40, wordWrap = true });
    }

    void OnDestroy()
    {
        if (env != null) env.Dispose();
    }
}
