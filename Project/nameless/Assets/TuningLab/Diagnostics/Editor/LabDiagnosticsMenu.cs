using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 【临时诊断脚本 · 验完即删】删掉 Assets/TuningLab/Diagnostics 整个目录即可。
///
/// 静态检查：脚本文件在不在、meta 的 GUID 对不对、场景有没有引用到组件、
/// 场景里那几个关键字段有没有序列化（老场景会缺字段）。
/// 菜单：GameJam → Diagnose Tuning Lab
/// 命令行：-executeMethod LabDiagnosticsMenu.RunSilent
///
/// 只读文件，不改任何东西。结果同时打到 Console 和 Project/nameless/Logs/TuningLabDiagnostics.txt。
/// </summary>
public static class LabDiagnosticsMenu
{
    const string ScenePath = "Assets/Scenes/TuningLab.unity";

    static readonly string[] ScriptFiles =
    {
        "Assets/TuningLab/TuningLab.cs",
        "Assets/TuningLab/LabCamera.cs",
        "Assets/TuningLab/LabHud.cs",
        "Assets/TuningLab/LabPlayer.cs",
        "Assets/TuningLab/LabVisual.cs",
        "Assets/TuningLab/LabVerticalRope.cs",
        "Assets/TuningLab/TierMath.cs",
        "Assets/TuningLab/Editor/TuningLabMenu.cs",
        "Assets/TuningLab/Editor/TuningLabSelfCheck.cs",
        "Assets/TuningLab/Editor/TuningLabPlayVerification.cs",
    };

    static readonly string[] CriticalFields =
    {
        "showGrid", "freeCamera", "autoBuild", "roomScale",
    };

    [MenuItem("GameJam/Diagnose Tuning Lab")]
    public static void Run()
    {
        string report = BuildReport();
        Debug.Log("[LabProbe]\n" + report);
        EditorUtility.DisplayDialog("Lab 诊断", "结果已打到 Console，也写进了 Logs/TuningLabDiagnostics.txt。", "好");
    }

    /// <summary>给 -executeMethod 用：不弹窗。</summary>
    public static void RunSilent()
    {
        Debug.Log("[LabProbe]\n" + BuildReport());
    }

    static string BuildReport()
    {
        LabDiag.Begin("调试台诊断（静态检查）", ScenePath);

        CheckVersions();
        CheckScriptFiles();
        CheckScene();

        LabDiag.Lines.AppendLine();
        LabDiag.Lines.AppendLine("提示：静态检查只证明「文件与序列化」没问题。");
        LabDiag.Lines.AppendLine("      网格与动态相机的实际行为要在 Play 里跑（LabProbe 会自动跑，结果同路径）。");

        return LabDiag.WriteFile();
    }

    // ---------------------------------------------------------------- 版本

    static void CheckVersions()
    {
        LabDiag.Head("1. 版本");
        LabDiag.Info("编辑器: " + Application.unityVersion);

        string versionFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            "ProjectSettings/ProjectVersion.txt"));
        if (!File.Exists(versionFile))
        {
            LabDiag.Warn("找不到 ProjectSettings/ProjectVersion.txt");
        }
        else
        {
            string recorded = FirstMatch(File.ReadAllText(versionFile), @"m_EditorVersion:\s*(\S+)");
            LabDiag.Info("工程记录: " + (recorded ?? "?"));
            if (recorded != null && !Application.unityVersion.StartsWith(recorded))
                LabDiag.Warn("编辑器版本与工程记录不一致（" + Application.unityVersion + " vs " + recorded
                             + "）—— 全组约定 6000.6.4f1");
            else
                LabDiag.Pass("Unity 版本与工程记录一致");
        }

        string lockFile = Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            "Packages/packages-lock.json"));
        if (File.Exists(lockFile))
        {
            string text = File.ReadAllText(lockFile);
            int idx = text.IndexOf("\"com.unity.inputsystem\"", StringComparison.Ordinal);
            string ver = idx >= 0
                ? FirstMatch(text.Substring(idx, Math.Min(200, text.Length - idx)), @"""version"":\s*""([^""]+)""")
                : null;
            LabDiag.Info("Input System: " + (ver ?? "?"));
        }
    }

    // ---------------------------------------------------------------- 文件

    static void CheckScriptFiles()
    {
        LabDiag.Head("2. 脚本文件与 meta");

        foreach (var assetPath in ScriptFiles)
        {
            if (!File.Exists(assetPath))
            {
                LabDiag.Fail("文件缺失: " + assetPath);
                continue;
            }

            string typeName = Path.GetFileNameWithoutExtension(assetPath);
            var type = LabDiag.FindType(typeName);
            if (type == null) LabDiag.Fail("文件在，但类型没编译进来: " + typeName + "（" + assetPath + "）");

            string metaPath = assetPath + ".meta";
            if (!File.Exists(metaPath))
            {
                LabDiag.Fail("meta 缺失: " + metaPath);
                continue;
            }

            string meta = File.ReadAllText(metaPath);
            string guid = FirstMatch(meta, @"guid:\s*([0-9a-fA-F]{32})");
            bool hasImporter = meta.Contains("MonoImporter:");

            if (guid == null) LabDiag.Fail("meta 里没有合法 guid: " + metaPath);
            else if (!hasImporter)
                LabDiag.Warn(assetPath + " 的 meta 缺 MonoImporter 段（GUID " + guid + " 仍在，但格式不标准）");
            else if (type != null)
                LabDiag.Pass(assetPath + " 正常（GUID " + guid.Substring(0, 8) + "…）");
            else
                LabDiag.Info(assetPath + " 文件与 meta 在（GUID " + guid.Substring(0, 8) + "…）");
        }
    }

    // ---------------------------------------------------------------- 场景

    static void CheckScene()
    {
        LabDiag.Head("3. 场景 " + ScenePath);

        if (!File.Exists(ScenePath))
        {
            LabDiag.Fail("场景文件不存在: " + ScenePath + "（菜单 GameJam → Build Tuning Lab 生成）");
            return;
        }

        string yaml = File.ReadAllText(ScenePath);
        string labMeta = File.Exists("Assets/TuningLab/TuningLab.cs.meta")
            ? File.ReadAllText("Assets/TuningLab/TuningLab.cs.meta")
            : null;
        string labGuid = labMeta != null ? FirstMatch(labMeta, @"guid:\s*([0-9a-fA-F]{32})") : null;

        int scriptIdx = labGuid != null
            ? yaml.IndexOf("m_Script: {fileID: 11500000, guid: " + labGuid, StringComparison.Ordinal)
            : -1;

        if (scriptIdx < 0)
        {
            LabDiag.Fail("场景里没有引用 TuningLab 组件（m_Script guid 对不上 TuningLab.cs.meta）"
                         + " —— 运行时会显示 Missing Script，网格与动态相机都不会创建");
            return;
        }

        LabDiag.Pass("场景引用的 TuningLab 脚本 GUID 与 TuningLab.cs.meta 一致");

        // 截出这个 MonoBehaviour 的整段
        int end = yaml.IndexOf("\n--- !u!", scriptIdx, StringComparison.Ordinal);
        string block = end > scriptIdx ? yaml.Substring(scriptIdx, end - scriptIdx) : yaml.Substring(scriptIdx);

        var missing = new System.Collections.Generic.List<string>();
        foreach (var field in CriticalFields)
            if (!Regex.IsMatch(block, @"(?m)^\s{2}" + field + @":\s"))
                missing.Add(field);

        if (missing.Count > 0)
        {
            LabDiag.Warn("场景里缺这些字段: " + string.Join(", ", missing)
                         + " —— 场景停留在旧版本，运行时的值不可控");
            LabDiag.Info("修法：GameJam → Build Tuning Lab 重新生成，再把 Assets/Scenes/TuningLab.unity 一起提交");
        }
        else
        {
            LabDiag.Pass("关键字段都已序列化: " + string.Join(", ", CriticalFields));
        }

        foreach (var field in new[] { "timeScale", "gravity", "freeCamera", "showGrid", "autoBuild", "roomScale", "maxFallSpeed" })
        {
            string value = ValueOf(block, field);
            if (value != null) LabDiag.Info(field + " = " + value);
        }

        LabDiag.Info("LabCamera 是运行时 AddComponent 加上的，场景里本来就没有它，属正常");

        bool inBuild = false;
        foreach (var s in EditorBuildSettings.scenes)
            if (s.path == ScenePath) inBuild = true;

        if (inBuild) LabDiag.Pass("场景在 Build Settings 里");
        else LabDiag.Warn("场景不在 Build Settings 里（Play 不影响，但打包会缺）");
    }

    // ---------------------------------------------------------------- 小工具

    static string FirstMatch(string text, string pattern)
    {
        var m = Regex.Match(text, pattern);
        return m.Success ? m.Groups[1].Value : null;
    }

    static string ValueOf(string block, string field)
    {
        var m = Regex.Match(block, @"(?m)^\s{2}" + field + @":\s*(\S*)");
        return m.Success ? m.Groups[1].Value : null;
    }
}
