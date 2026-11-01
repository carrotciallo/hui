using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>
/// 【临时诊断脚本 · 验完即删】删掉 Assets/TuningLab/Diagnostics 整个目录即可。
///
/// 目的：查清"按 Tab 的网格 / 动态相机没起来"到底断在哪一环。
/// 在调试台场景按 Play 后自动运行（不用往场景里拖任何东西），
/// 结果同时打到 Console 和 Project/nameless/Logs/TuningLabDiagnostics.txt。
///
/// 全程只用反射按名字找类型，所以即使调试台脚本缺失/编译不过也不会连带编译失败。
/// </summary>
public sealed class LabProbe : MonoBehaviour
{
    /// <summary>场景名带 TuningLab，或者场景里确实有 TuningLab 组件，才自动接管。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        Type labType = LabDiag.FindType("TuningLab");
        UnityEngine.Object lab = labType != null
            ? UnityEngine.Object.FindAnyObjectByType(labType)
            : null;

        bool relevant = sceneName.IndexOf("TuningLab", StringComparison.OrdinalIgnoreCase) >= 0
                        || lab != null;
        if (!relevant) return;

        new GameObject("[LabProbe]").AddComponent<LabProbe>();
    }

    void Start() => StartCoroutine(Run());

    IEnumerator Run()
    {
        // 等 Awake / BuildAll 跑完
        yield return new WaitForSecondsRealtime(0.3f);

        LabDiag.Begin("调试台诊断（运行时）", SceneManager.GetActiveScene().name);
        LabDiag.Info("activeInputHandler 期望=1（新版 Input System）");

        CheckTypes();

        LabDiag.Head("2. 场景里的 TuningLab 实例");
        Type labType = LabDiag.FindType("TuningLab");
        UnityEngine.Object lab = labType == null
            ? null
            : UnityEngine.Object.FindAnyObjectByType(labType);

        if (lab == null)
        {
            LabDiag.Fail("场景里找不到 TuningLab 组件 —— 网格和动态相机都由它创建，所以两样都不会出现。");
            LabDiag.Info("可能原因：场景是旧的、脚本丢了（Missing Script）、或这不是调试台场景。");
            LabDiag.WriteAndLog();
            yield break;
        }

        var labComp = (Component)lab;
        LabDiag.Pass("找到 TuningLab：" + labComp.gameObject.name);
        LabDiag.Info("autoBuild=" + LabDiag.Get(lab, "autoBuild")
                     + "  freeCamera=" + LabDiag.Get(lab, "freeCamera")
                     + "  showGrid=" + LabDiag.Get(lab, "showGrid")
                     + "  roomScale=" + LabDiag.Get(lab, "roomScale"));

        if (!LabDiag.Bool(lab, "autoBuild"))
            LabDiag.Warn("autoBuild=false：运行时不生成场地（场景是烘焙版才会这样）");
        if (!LabDiag.Bool(lab, "freeCamera"))
            LabDiag.Warn("freeCamera=false：不会创建 LabCamera → 一定没有动态相机");
        if (!LabDiag.Bool(lab, "showGrid"))
            LabDiag.Warn("showGrid=false：网格初始关闭（按 Tab 才打开，属正常值）");

        // ---------- 相机 ----------
        LabDiag.Head("3. 相机");
        var cam = LabDiag.Get(lab, "Cam") as Camera;
        if (cam == null)
        {
            LabDiag.Fail("TuningLab.Cam 为 null —— BuildCamera() 没执行，或相机被删了");
        }
        else
        {
            LabDiag.Pass("相机：" + cam.name
                         + "  orthographic=" + cam.orthographic
                         + "  size=" + cam.orthographicSize.ToString("0.###")
                         + "  enabled=" + cam.enabled
                         + "  activeInHierarchy=" + cam.gameObject.activeInHierarchy);
        }

        // ---------- 动态相机组件 ----------
        LabDiag.Head("4. 动态相机组件 LabCamera");
        Component labCam = null;
        if (cam != null)
        {
            labCam = LabDiag.GetComponentOn(cam.gameObject, "LabCamera");
            if (labCam == null)
            {
                if (LabDiag.Bool(lab, "freeCamera"))
                    LabDiag.Fail("freeCamera=true 但相机上没有 LabCamera 组件 —— 动态相机没被创建");
                else
                    LabDiag.Info("freeCamera=false，本来就不该有 LabCamera（这就是没有动态相机的原因）");
            }
            else
            {
                var a = LabDiag.Get(labCam, "a") as Transform;
                var b = LabDiag.Get(labCam, "b") as Transform;
                LabDiag.Pass("LabCamera 已挂在相机上");
                LabDiag.Info("a=" + NameOf(a) + "  b=" + NameOf(b)
                             + "  minSize=" + LabDiag.Get(labCam, "minSize")
                             + "  maxSize=" + LabDiag.Get(labCam, "maxSize")
                             + "  smoothing=" + LabDiag.Get(labCam, "smoothing"));
                if (a == null || b == null)
                    LabDiag.Fail("LabCamera 的 a / b 没绑定 —— 它每帧直接 return，相机会完全不动");
            }
        }

        // ---------- 网格对象 ----------
        LabDiag.Head("5. 网格线对象");
        int gridTotal, gridEnabled;
        CountGrid(lab, out gridTotal, out gridEnabled);
        if (gridTotal == 0)
        {
            LabDiag.Fail("一条网格线都没有（GridV_* / GridH_* 子物体数为 0）");
        }
        else
        {
            LabDiag.Pass("网格线共 " + gridTotal + " 条，其中启用 " + gridEnabled + " 条");
            if (gridEnabled == 0 && LabDiag.Bool(lab, "showGrid"))
                LabDiag.Fail("showGrid=true 但所有线都是 disabled —— 渲染没跟上");
        }

        // 线的材质信息：判断"画出来了但看不见"
        foreach (var sr in labComp.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            string n = sr.gameObject.name;
            if (!n.StartsWith("GridV_") && !n.StartsWith("GridH_")) continue;
            LabDiag.Info("样例线 " + n + ": color=" + sr.color + "  sortingOrder=" + sr.sortingOrder
                         + "  sprite=" + (sr.sprite != null) + "  scale=" + sr.transform.localScale);
            break;
        }

        // ---------- 网格开关测试（绕开输入，直接翻字段）----------
        LabDiag.Head("6. 网格开关（直接改 showGrid 字段，不经过键盘）");
        bool originalShowGrid = LabDiag.Bool(lab, "showGrid");

        if (!LabDiag.SetField(lab, "showGrid", false))
        {
            LabDiag.Fail("写不进 showGrid 字段（脚本版本对不上？）");
        }
        else
        {
            yield return null;
            yield return null;
            CountGrid(lab, out gridTotal, out gridEnabled);
            if (gridEnabled == 0) LabDiag.Pass("showGrid=false → 网格已隐藏（启用 " + gridEnabled + " 条）");
            else LabDiag.Fail("showGrid=false 但仍有 " + gridEnabled + " 条线启用 —— UpdateGrid 没响应");

            LabDiag.SetField(lab, "showGrid", true);
            yield return null;
            yield return null;
            CountGrid(lab, out gridTotal, out gridEnabled);
            if (gridEnabled > 0) LabDiag.Pass("showGrid=true → 网格已显示（启用 " + gridEnabled + " 条）");
            else LabDiag.Fail("showGrid=true 但一条线都没启用 —— 网格画不出来");
        }

        LabDiag.SetField(lab, "showGrid", originalShowGrid);

        // ---------- 动态相机测试 ----------
        LabDiag.Head("7. 动态相机行为（把两人拉开，看相机缩不缩放）");
        var pa = LabDiag.Get(lab, "PlayerA") as Component;
        var pb = LabDiag.Get(lab, "PlayerB") as Component;

        if (cam == null || pa == null || pb == null)
        {
            LabDiag.Fail("缺相机或玩家，跳过行为测试（cam=" + (cam != null)
                         + " A=" + (pa != null) + " B=" + (pb != null) + "）");
        }
        else
        {
            Vector3 posA = pa.transform.position;
            Vector3 posB = pb.transform.position;
            float size0 = cam.orthographicSize;
            float midX = (posA.x + posB.x) * 0.5f;

            pa.transform.position = new Vector3(midX - 6f, posA.y, posA.z);
            pb.transform.position = new Vector3(midX + 6f, posB.y, posB.z);

            for (int i = 0; i < 40; i++) yield return null;

            float size1 = cam.orthographicSize;
            float camX = cam.transform.position.x;

            if (size1 > size0 + 0.3f)
                LabDiag.Pass("拉开两人后 orthographicSize " + size0.ToString("0.##")
                             + " → " + size1.ToString("0.##") + "（相机在缩放）");
            else
                LabDiag.Fail("拉开两人后 orthographicSize 没变（" + size0.ToString("0.##")
                             + " → " + size1.ToString("0.##") + "）—— 动态相机没在工作");

            if (Mathf.Abs(camX - midX) < 2.5f)
                LabDiag.Pass("相机跟随中点（相机 x=" + camX.ToString("0.##")
                             + "，中点 x=" + midX.ToString("0.##") + "）");
            else
                LabDiag.Fail("相机没跟到中点（相机 x=" + camX.ToString("0.##")
                             + "，中点 x=" + midX.ToString("0.##") + "）");

            pa.transform.position = posA;
            pb.transform.position = posB;
            for (int i = 0; i < 10; i++) yield return null;
        }

        // ---------- 输入链路 ----------
        LabDiag.Head("8. 键盘与 Tab");
        var kb = Keyboard.current;
        if (kb == null)
        {
            LabDiag.Fail("Keyboard.current 为 null —— Input System 没初始化，所有键盘输入都收不到");
        }
        else
        {
            LabDiag.Pass("Keyboard.current 正常：" + kb.displayName);

            // 合成一次 Tab，验证"输入 → TuningLab.Update → showGrid"整条链路
            bool before = LabDiag.Bool(lab, "showGrid");
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Tab));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;

            bool after = LabDiag.Bool(lab, "showGrid");
            if (after != before)
                LabDiag.Pass("合成 Tab 生效：showGrid " + before + " → " + after + "（输入链路完整）");
            else
                LabDiag.Warn("合成 Tab 没让 showGrid 变化（注入在部分版本不生效，以你手按的结果为准）");
            LabDiag.SetField(lab, "showGrid", before);

            // 手按监听
            LabDiag.Info(">>> 现在请手动按几次 Tab（10 秒）<<<");
            int tabSeen = 0;
            int flips = 0;
            bool last = LabDiag.Bool(lab, "showGrid");
            float t = 0f;
            while (t < 10f)
            {
                if (kb.tabKey.wasPressedThisFrame) tabSeen++;
                bool now = LabDiag.Bool(lab, "showGrid");
                if (now != last)
                {
                    flips++;
                    LabDiag.Info("showGrid 翻转 #" + flips + " → " + now + "（此前已收到 Tab " + tabSeen + " 次）");
                    last = now;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            if (tabSeen == 0)
                LabDiag.Info("10 秒内探针没收到 Tab 按键（如果你按了，说明输入层有问题）");
            else if (flips == 0)
                LabDiag.Fail("收到 Tab " + tabSeen + " 次，但 showGrid 一次都没变 —— TuningLab.Update 没在跑");
            else
                LabDiag.Pass("手动 Tab 生效：收到 " + tabSeen + " 次，showGrid 翻转 " + flips + " 次");
        }

        LabDiag.WriteAndLog();
    }

    void CheckTypes()
    {
        LabDiag.Head("1. 脚本类型是否已编译");
        string[] names =
        {
            "TuningLab", "LabCamera", "LabHud", "LabPlayer", "LabVisual", "TierMath", "LabVerticalRope"
        };
        foreach (var n in names)
        {
            var t = LabDiag.FindType(n);
            if (t != null) LabDiag.Pass("类型存在：" + n + "（" + t.Assembly.GetName().Name + "）");
            else LabDiag.Fail("类型缺失：" + n + " —— 脚本没编译进来（文件缺失或编译报错）");
        }
    }

    static void CountGrid(UnityEngine.Object lab, out int total, out int enabled)
    {
        total = 0;
        enabled = 0;
        var comp = lab as Component;
        if (comp == null) return;

        foreach (var sr in comp.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sr == null) continue;
            string n = sr.gameObject.name;
            if (!n.StartsWith("GridV_") && !n.StartsWith("GridH_")) continue;
            total++;
            if (sr.enabled) enabled++;
        }
    }

    static string NameOf(Component c) => c == null ? "<null>" : c.name;
}

/// <summary>报告拼装 + 落盘。运行时的 LabProbe 和编辑器的菜单共用。</summary>
public static class LabDiag
{
    public static readonly StringBuilder Lines = new StringBuilder();
    static int passed, failed, warned;

    public static void Begin(string title, string sceneName)
    {
        Lines.Length = 0;
        passed = failed = warned = 0;

        Lines.AppendLine("=== " + title + " ===");
        Lines.AppendLine("时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        Lines.AppendLine("Unity: " + Application.unityVersion);
        if (!string.IsNullOrEmpty(sceneName)) Lines.AppendLine("场景: " + sceneName);
        Lines.AppendLine();
    }

    public static void Head(string text) => Lines.AppendLine("--- " + text + " ---");

    public static void Info(string text) => Lines.AppendLine("       " + text);

    public static void Pass(string text)
    {
        passed++;
        Lines.AppendLine("[OK]   " + text);
    }

    public static void Fail(string text)
    {
        failed++;
        Lines.AppendLine("[FAIL] " + text);
    }

    public static void Warn(string text)
    {
        warned++;
        Lines.AppendLine("[WARN] " + text);
    }

    /// <summary>补上结论行并落盘，返回完整文本。</summary>
    public static string WriteFile()
    {
        Lines.AppendLine();
        Lines.AppendLine("结果: 通过 " + passed + " / 失败 " + failed + " / 警告 " + warned);
        Lines.AppendLine(failed == 0 ? "结论: 没发现硬故障。" : "结论: 有 " + failed + " 项硬故障，见上面 [FAIL]。");

        string text = Lines.ToString();

        try
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "TuningLabDiagnostics.txt");
            File.WriteAllText(path, text);
            Debug.Log("[LabProbe] 报告已写入: " + path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LabProbe] 写报告失败: " + e.Message);
        }

        return text;
    }

    public static void WriteAndLog() => Debug.Log("[LabProbe]\n" + WriteFile());

    // ---------- 反射工具 ----------

    public static Type FindType(string name)
    {
        var loaded = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < loaded.Length; i++)
        {
            Type t = null;
            try { t = loaded[i].GetType(name, false); }
            catch { /* 某些程序集反射会抛，忽略 */ }
            if (t != null) return t;
        }
        return null;
    }

    public static object Get(object target, string member)
    {
        if (target == null) return null;
        Type t = target.GetType();

        var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        if (f != null) return f.GetValue(target);

        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        return p != null ? p.GetValue(target) : null;
    }

    public static bool Bool(object target, string member) => Get(target, member) is bool b && b;

    public static bool SetField(object target, string member, object value)
    {
        if (target == null) return false;
        var f = target.GetType().GetField(member, BindingFlags.Public | BindingFlags.Instance);
        if (f == null || f.FieldType != value.GetType()) return false;
        f.SetValue(target, value);
        return true;
    }

    public static Component GetComponentOn(GameObject go, string typeName)
    {
        var type = FindType(typeName);
        return type == null ? null : go.GetComponent(type);
    }
}
