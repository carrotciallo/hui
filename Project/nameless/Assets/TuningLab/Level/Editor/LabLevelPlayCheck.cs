#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 进 Play 验证"派生关卡场景确实按 LabLevel 数据生成"：
/// 玩家、相机、合并后的地形矩形、竖绳、出生点位置，全部和基准数据对得上。
///
/// 菜单 GameJam → Run Level Play Check；批处理：
///   -executeMethod LabLevelPlayCheck.RunFromCommandLine
/// 通过退出码 0，失败 1。会临时生成 Assets/Scenes/Levels/__PlayCheck.unity，跑完删掉。
///
/// 状态放在 SessionState 里：进 Play 可能触发一次脚本重载，静态字段会被清空，
/// 光靠 static 变量会在重载后卡死。
/// </summary>
[InitializeOnLoad]
public static class LabLevelPlayCheck
{
    const string CheckPath = LabLevelScene.LevelFolder + "/__PlayCheck.unity";
    const double WaitSeconds = 0.8;
    const double TimeoutSeconds = 30.0;

    const string ActiveKey = "LabLevelPlayCheck.Active";
    const string StartedKey = "LabLevelPlayCheck.Started";
    const string PlayAtKey = "LabLevelPlayCheck.PlayAt";
    const string FinishedKey = "LabLevelPlayCheck.Finished";
    const string QuitKey = "LabLevelPlayCheck.Quit";
    const string ExitCodeKey = "LabLevelPlayCheck.ExitCode";
    const string ReportKey = "LabLevelPlayCheck.Report";

    static LabLevelPlayCheck()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        Attach();
    }

    [MenuItem("GameJam/Run Level Play Check")]
    public static void Run()
    {
        Launch(false);
    }

    public static void RunFromCommandLine()
    {
        Launch(true);
    }

    static void Launch(bool batch)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[LabLevelPlayCheck] 请先退出 Play。");
            if (batch) EditorApplication.Exit(1);
            return;
        }

        if (SessionState.GetBool(ActiveKey, false))
        {
            Debug.LogError("[LabLevelPlayCheck] 上一次检查还没结束。重新打开编辑器再试。");
            if (batch) EditorApplication.Exit(1);
            return;
        }

        if (!LabLevelScene.CreateSilent(CheckPath))
        {
            Debug.LogError("[LabLevelPlayCheck] 无法生成验证场景。");
            if (batch) EditorApplication.Exit(1);
            return;
        }

        ResetSession();
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetBool(QuitKey, batch);
        SessionState.SetFloat(StartedKey, (float)EditorApplication.timeSinceStartup);
        SessionState.SetFloat(PlayAtKey, -1f);

        Attach();
        EditorApplication.EnterPlaymode();
    }

    static void Attach()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void Detach()
    {
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    static void ResetSession()
    {
        SessionState.SetBool(ActiveKey, false);
        SessionState.SetBool(FinishedKey, false);
        SessionState.SetBool(QuitKey, false);
        SessionState.SetFloat(StartedKey, -1f);
        SessionState.SetFloat(PlayAtKey, -1f);
        SessionState.SetInt(ExitCodeKey, 1);
        SessionState.SetString(ReportKey, "");
    }

    static void Tick()
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (SessionState.GetBool(FinishedKey, false)) return;

        float started = SessionState.GetFloat(StartedKey, -1f);
        float now = (float)EditorApplication.timeSinceStartup;

        if (!EditorApplication.isPlaying)
        {
            if (started >= 0f && now - started > TimeoutSeconds) Finish(false, "进入 Play 超时。");
            return;
        }

        float playAt = SessionState.GetFloat(PlayAtKey, -1f);
        if (playAt < 0f)
        {
            SessionState.SetFloat(PlayAtKey, now);
            return;
        }

        if (now - playAt < WaitSeconds) return;

        bool ok;
        string text;
        try
        {
            ok = Check(out text);
        }
        catch (System.Exception e)
        {
            ok = false;
            text = "检查过程抛异常：\n" + e;
        }

        Finish(ok, text);
    }

    static void Finish(bool ok, string text)
    {
        if (SessionState.GetBool(FinishedKey, false)) return;
        SessionState.SetBool(FinishedKey, true);
        SessionState.SetInt(ExitCodeKey, ok ? 0 : 1);
        SessionState.SetString(ReportKey, text);

        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        else CleanupAndExit();
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredEditMode) return;
        if (!SessionState.GetBool(FinishedKey, false)) return;
        CleanupAndExit();
    }

    static void CleanupAndExit()
    {
        Detach();

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(CheckPath) != null)
            AssetDatabase.DeleteAsset(CheckPath);

        Debug.Log("[LabLevelPlayCheck]\n" + SessionState.GetString(ReportKey, ""));

        bool quit = SessionState.GetBool(QuitKey, false);
        int exitCode = SessionState.GetInt(ExitCodeKey, 1);
        ResetSession();

        if (quit) EditorApplication.Exit(exitCode);
    }

    static bool Check(out string text)
    {
        var sb = new StringBuilder();
        int fails = 0;

        TuningLab lab = Object.FindAnyObjectByType<TuningLab>();
        if (lab == null)
        {
            text = "FAIL 找不到 TuningLab。";
            return false;
        }

        LabLevel level = lab.GetComponent<LabLevel>();
        Check(sb, ref fails, level != null, "场景里有 LabLevel");
        Check(sb, ref fails, lab.level == level, "TuningLab.level 已绑定");
        Check(sb, ref fails, lab.PlayerA != null && lab.PlayerB != null, "两名玩家已生成");
        Check(sb, ref fails, lab.Cam != null, "相机已生成");
        Check(sb, ref fails, lab.Hud != null, "HUD 已生成");

        if (level != null)
        {
            Check(sb, ref fails, level.SolidCellCount == 73,
                "实心格 = 73，实际 " + level.SolidCellCount);
            Check(sb, ref fails, level.MergedRectCount == 8,
                "合并矩形 = 8，实际 " + level.MergedRectCount);
            Check(sb, ref fails, level.RopeCount == 1, "绳 = 1");

            Transform generated = lab.transform.Find(LabLevel.GeneratedRootName);
            Check(sb, ref fails, generated != null, "生成根 " + LabLevel.GeneratedRootName + " 存在");
            if (generated != null)
                Check(sb, ref fails, generated.childCount == 9,
                    "生成物 = 8 块地形 + 1 根绳，实际 " + generated.childCount);

            Check(sb, ref fails,
                Mathf.Abs(level.Data.spawnA.x - TuningLab.SpawnAFeet.x) < 0.0001f &&
                Mathf.Abs(level.Data.spawnA.y - TuningLab.SpawnAFeet.y) < 0.0001f,
                "A 关卡落点 = 基准");
            Check(sb, ref fails,
                Mathf.Abs(level.Data.spawnB.x - TuningLab.SpawnBFeet.x) < 0.0001f &&
                Mathf.Abs(level.Data.spawnB.y - TuningLab.SpawnBFeet.y) < 0.0001f,
                "B 关卡落点 = 基准");
        }

        if (lab.PlayerA != null)
        {
            Check(sb, ref fails, lab.PlayerA.Grounded, "A 落地");
            Check(sb, ref fails, Mathf.Abs(lab.PlayerA.transform.position.x - 8f) < 0.05f,
                "A 在 x=8，实际 " + lab.PlayerA.transform.position.x);
            Check(sb, ref fails, Mathf.Abs(lab.PlayerA.FootY) < 0.05f,
                "A 脚底在 y=0，实际 " + lab.PlayerA.FootY);
        }

        if (lab.PlayerB != null)
        {
            Check(sb, ref fails, lab.PlayerB.Grounded, "B 落地");
            Check(sb, ref fails, Mathf.Abs(lab.PlayerB.transform.position.x - 10f) < 0.05f,
                "B 在 x=10，实际 " + lab.PlayerB.transform.position.x);
            Check(sb, ref fails, Mathf.Abs(lab.PlayerB.FootY) < 0.05f,
                "B 脚底在 y=0，实际 " + lab.PlayerB.FootY);
        }

        // 真实物理里点一下地面和空位，证明碰撞几何来自关卡数据
        Check(sb, ref fails, Physics2D.OverlapPoint(new Vector2(0f, -0.5f)) != null,
            "地板 (0, -0.5) 有碰撞");
        Check(sb, ref fails, Physics2D.OverlapPoint(new Vector2(4.5f, 1.5f)) != null,
            "台阶 (4.5, 1.5) 有碰撞");
        Check(sb, ref fails, Physics2D.OverlapPoint(new Vector2(-9.5f, 5.5f)) == null,
            "空位 (-9.5, 5.5) 没有碰撞");

        var ropes = Object.FindObjectsByType<LabVerticalRope>();
        Check(sb, ref fails, ropes.Length == 1, "竖绳组件 = 1，实际 " + ropes.Length);
        if (ropes.Length == 1)
            Check(sb, ref fails, ropes[0].cells == TuningLab.RopeCells,
                "竖绳长度 = " + TuningLab.RopeCells);

        if (lab.Cam != null)
        {
            var follow = lab.Cam.GetComponent<LabCamera>();
            Check(sb, ref fails, follow != null, "相机有 LabCamera");
            if (follow != null && level != null)
                Check(sb, ref fails,
                    Mathf.Abs(follow.bounds.xMin - level.Data.bounds.x) < 0.001f &&
                    Mathf.Abs(follow.bounds.yMin - level.Data.bounds.y) < 0.001f,
                    "相机 clamp 用了关卡范围");
        }

        sb.AppendLine(fails == 0 ? "全部通过。" : ("失败 " + fails + " 项。"));
        text = sb.ToString();
        return fails == 0;
    }

    static void Check(StringBuilder sb, ref int fails, bool condition, string label)
    {
        if (!condition) fails++;
        sb.AppendLine((condition ? "  OK   " : "  FAIL ") + label);
    }
}
#endif
