#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
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

    /// <summary>验证用刺所在格：底座 (-6, -1) 是基准地板，离 x=8/10 的两名玩家很远。</summary>
    static readonly Vector2Int SpikeCell = new Vector2Int(-6, 0);
    /// <summary>验证用冰块：同样放在远处地板上，只验证"数据 → 实体碰撞"和重置。</summary>
    static readonly Vector2Int IceCell = new Vector2Int(-8, 0);
    const int IceGroup = 5;
    /// <summary>验证用召回道具：放在远处地板，验证拾取解锁和幻影生成。</summary>
    static readonly Vector2Int RecallCell = new Vector2Int(-10, 0);

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

        // 会切到临时场景，先把当前场景的修改问一遍。
        if (!batch && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (!LabLevelScene.CreateSilent(CheckPath))
        {
            Debug.LogError("[LabLevelPlayCheck] 无法生成验证场景。");
            if (batch) EditorApplication.Exit(1);
            return;
        }

        // 故意用非默认值：关卡数据 8/34，分区 bounds 是 -20,-5,60,40，TuningLab 默认 7/30。
        // B 出生点在这个分区里，所以相机应该用分区的 bounds + 关卡数据的 8/34。
        var level = Object.FindAnyObjectByType<LabLevel>();
        if (level != null)
        {
            level.Data.Normalize();
            level.Data.cameraMinSize = 8f;
            level.Data.cameraMaxSize = 34f;
            level.Data.partitions.Clear();
            level.Data.AddPartition(new RectInt(-20, -5, 60, 40), "PlayCheckZone");
            // 离两名玩家很远的地板上一根朝上的刺：只验证"数据 → 物件"这条管子，不验证死亡。
            level.Data.ClearSpikes();
            level.Data.PlaceSpike(SpikeCell, LabSpikeDir.Up);
            level.Data.ClearIces();
            level.Data.PlaceIce(IceCell, IceGroup);
            level.Data.ClearRecalls();
            level.Data.PlaceRecall(RecallCell);

            var lab = Object.FindAnyObjectByType<TuningLab>();
            if (lab != null)
            {
                lab.defaultCameraMinSize = 7f;
                lab.defaultCameraMaxSize = 30f;
                lab.defaultCameraBounds = new Rect(-13f, -1f, 26f, 17f);
                EditorUtility.SetDirty(lab);
            }

            EditorUtility.SetDirty(level);
            var scene = level.gameObject.scene;
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
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
            Check(sb, ref fails, level.SpikeCount == 1, "地刺 = 1，实际 " + level.SpikeCount);
            Check(sb, ref fails, level.IceCount == 1, "冰块 = 1，实际 " + level.IceCount);
            Check(sb, ref fails, level.RecallCount == 1, "召回道具 = 1，实际 " + level.RecallCount);

            Transform generated = lab.transform.Find(LabLevel.GeneratedRootName);
            Check(sb, ref fails, generated != null, "生成根 " + LabLevel.GeneratedRootName + " 存在");
            if (generated != null)
                Check(sb, ref fails, generated.childCount == 4,
                    "生成根直接子物 = 地形合并体 + 绳 + 刺 + 召回道具，实际 " + generated.childCount);

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

        var spikes = Object.FindObjectsByType<LabSpike>();
        Check(sb, ref fails, spikes.Length == 1, "地刺组件 = 1，实际 " + spikes.Length);
        if (spikes.Length == 1)
        {
            LabSpike spike = spikes[0];
            var strip = new LabSpikeSpec(SpikeCell, LabSpikeDir.Up);
            Check(sb, ref fails,
                Mathf.Abs(spike.transform.position.x - strip.StripCenter.x) < 0.0001f &&
                Mathf.Abs(spike.transform.position.y - strip.StripCenter.y) < 0.0001f,
                "地刺生成在 " + strip.StripCenter + "，实际 " + spike.transform.position);

            var spikeBody = spike.GetComponent<BoxCollider2D>();
            Check(sb, ref fails, spikeBody != null && spikeBody.isTrigger
                && Mathf.Abs(spikeBody.size.x - strip.StripSize.x) < 0.0001f
                && Mathf.Abs(spikeBody.size.y - strip.StripSize.y) < 0.0001f,
                "地刺碰撞 = 触发式 " + strip.StripSize);
            Check(sb, ref fails, Physics2D.OverlapPoint(strip.StripCenter) != null,
                "刺带中心查得到碰撞");
            Check(sb, ref fails,
                Physics2D.OverlapPoint(new Vector2(strip.StripCenter.x, strip.StripCenter.y + 0.5f)) == null,
                "刺上方半格没有碰撞（只有那 6 像素 lethal）");
        }

        var ices = Object.FindObjectsByType<LabIce>();
        Check(sb, ref fails, ices.Length == 1, "冰块组件 = 1，实际 " + ices.Length);
        if (ices.Length == 1)
        {
            LabIce ice = ices[0];
            var center = new Vector2(IceCell.x + 0.5f, IceCell.y + 0.5f);
            Check(sb, ref fails,
                Mathf.Abs(ice.transform.position.x - center.x) < 0.0001f &&
                Mathf.Abs(ice.transform.position.y - center.y) < 0.0001f,
                "冰块生成在 " + center + "，实际 " + ice.transform.position);
            Check(sb, ref fails, ice.groupId == IceGroup,
                "冰块组号 = " + IceGroup + "，实际 " + ice.groupId);
            Check(sb, ref fails,
                ice.GetComponent<SpriteRenderer>().color == LabIce.DefaultColor,
                "冰块填充保持默认冰蓝");
            Check(sb, ref fails,
                ice.GroupBorder != null && ice.GroupBorder.color == LabIce.GroupColor(IceGroup),
                "冰块运行时使用组色边框");

            var iceBody = ice.GetComponent<BoxCollider2D>();
            Check(sb, ref fails, iceBody != null && !iceBody.isTrigger
                && Mathf.Abs(iceBody.size.x - 1f) < 0.0001f
                && Mathf.Abs(iceBody.size.y - 1f) < 0.0001f,
                "冰块碰撞 = 实体 1×1");
            Check(sb, ref fails, Physics2D.OverlapPoint(center) != null,
                "冰块中心查得到碰撞");

            // 破坏 → 地图重置恢复，验证 MapReset 管路。
            ice.BreakOne();
            Check(sb, ref fails, ice.Broken && iceBody != null && !iceBody.enabled,
                "破坏后冰块碰撞关闭");
            lab.ResetMap();
            Check(sb, ref fails, !ice.Broken && iceBody != null && iceBody.enabled,
                "MapReset 后冰块恢复");
        }
        var pickups = Object.FindObjectsByType<LabRecallPickup>();
        Check(sb, ref fails, pickups.Length == 1, "召回道具组件 = 1，实际 " + pickups.Length);
        if (pickups.Length == 1)
        {
            LabRecallPickup pickup = pickups[0];
            var center = new Vector2(RecallCell.x + 0.5f, RecallCell.y + 0.5f);
            Check(sb, ref fails,
                Mathf.Abs(pickup.transform.position.x - center.x) < 0.0001f &&
                Mathf.Abs(pickup.transform.position.y - center.y) < 0.0001f,
                "召回道具生成在 " + center + "，实际 " + pickup.transform.position);

            var pickupBody = pickup.GetComponent<BoxCollider2D>();
            Check(sb, ref fails, pickupBody != null && pickupBody.isTrigger
                && Mathf.Abs(pickupBody.size.x - 1f) < 0.0001f
                && Mathf.Abs(pickupBody.size.y - 1f) < 0.0001f,
                "召回道具碰撞 = 触发式 1×1");
            Check(sb, ref fails, pickup.GetComponent<SpriteRenderer>().sprite != null,
                "召回道具看得见");

            Check(sb, ref fails, !lab.RecallUnlocked, "召回默认未解锁");
            Check(sb, ref fails, pickup.TryCollect(lab.PlayerA), "玩家 A 能拾取召回道具");
            Check(sb, ref fails, lab.RecallUnlocked, "拾取后全队解锁召回");

            Check(sb, ref fails, lab.TryRecall(lab.PlayerB), "B 头顶有空位，召回 A");
            Check(sb, ref fails, LabPhantom.All.Count == 1,
                "旧位置生成 1 个幻影，实际 " + LabPhantom.All.Count);
            if (LabPhantom.All.Count == 1)
            {
                var phantom = LabPhantom.All[0];
                Check(sb, ref fails, phantom.Source == lab.PlayerA && phantom.Target == lab.PlayerB,
                    "幻影复制 A，只对 B 生效");
                Check(sb, ref fails, phantom.Form == lab.PlayerA.Form,
                    "幻影形态 = 被召回者形态");
            }

            lab.ResetMap();
            Check(sb, ref fails, LabPhantom.All.Count == 0, "MapReset 清空幻影");
            Check(sb, ref fails, lab.RecallUnlocked, "MapReset 保留召回能力");
        }

        Check(sb, ref fails, lab.PlayerA != null && lab.PlayerB != null,
            "离刺很远的两名玩家没有被误杀");

        // ---- 灵体态（Issue #64）：四条边全碎 = 限时穿透态 ----
        if (lab.PlayerA != null)
        {
            LabPlayer a = lab.PlayerA;
            var aBody = a.GetComponent<BoxCollider2D>();
            a.Teleport(new Vector2(-9.5f, 5.5f));      // 基准场景里确认过的空位

            Check(sb, ref fails, !a.IsIntangible && aBody != null && !aBody.isTrigger,
                "常态：不是灵体，碰撞箱是实心");

            for (int i = 0; i < 4; i++)
                a.SetEdge((LabPlayer.Edge)i, LabPlayer.EdgeState.Spirit);

            Check(sb, ref fails, a.Form == LabForm.Spirit && a.IsIntangible,
                "四条边全碎 = 灵体态");
            Check(sb, ref fails, aBody != null && aBody.isTrigger,
                "灵体把碰撞箱改成触发器：穿实体，刺/门/双人对撞一起失效");
            Check(sb, ref fails, Mathf.Abs(a.SpiritTimeLeft - lab.spiritDuration) < 0.05f,
                "存在时间 = Inspector 的 spiritDuration，实际 " + a.SpiritTimeLeft);

            a.ResetEdges();
            Check(sb, ref fails, !a.IsIntangible && aBody != null && !aBody.isTrigger
                    && a.SpiritTimeLeft <= 0f,
                "形态回到常态后交还实心碰撞箱、计时器清零");

            // 持钥上限：身上已经有一把时，第二把捡不起，钥匙留在原处。
            LabKey first = MakeProbeKey(new Vector2(-9.5f, 3.5f));
            LabKey second = MakeProbeKey(new Vector2(-8.5f, 3.5f));
            Check(sb, ref fails, first.TryCollect(a), "捡起第一把钥匙");
            Check(sb, ref fails, !second.TryCollect(a) && !second.Collected,
                "持钥上限 = 1：第二把捡不起，钥匙本体还在");
            second.TryCollect(a);
            Check(sb, ref fails, !second.Collected,
                "拿着钥匙反复接触仍然无效（Stay 重试不会绕过上限）");
            a.ClearKeys();
            Check(sb, ref fails, second.TryCollect(a), "空出手后同一把钥匙能捡起来");

            // 临时钥匙：登记为临时，其余当普通钥匙用。
            LabKey temp = LabKey.SpawnTemporary(new Vector2(-7.5f, 3.5f));
            Check(sb, ref fails, temp != null && temp.IsTemporary && !temp.Collected,
                "灵体吐出的钥匙登记为临时钥匙");
            Check(sb, ref fails, temp.TryCollect(lab.PlayerB), "临时钥匙和普通钥匙一样能捡");
            Check(sb, ref fails, temp.Collected && lab.PlayerB.KeyCount == 1,
                "捡起后计数落到玩家身上");
            lab.PlayerB.ClearKeys();
        }

        if (lab.Cam != null)
        {
            var follow = lab.Cam.GetComponent<LabCamera>();
            Check(sb, ref fails, follow != null, "相机有 LabCamera");
            if (follow != null && level != null)
            {
                Rect expectedBounds = LabLevelData.ToRect(level.Data.bounds);
                string boundsSource = "单关范围";

                if (level.Data.partitions != null && level.Data.partitions.Count > 0)
                {
                    var partition = level.Data.partitions[0];
                    if (partition != null && lab.PlayerB != null &&
                        LabLevelData.ToRect(partition.bounds).Contains(lab.PlayerB.transform.position))
                    {
                        expectedBounds = LabLevelData.ToRect(partition.bounds);
                        boundsSource = "当前关卡分区";
                    }
                }

                Check(sb, ref fails,
                    Mathf.Abs(follow.bounds.xMin - expectedBounds.xMin) < 0.001f &&
                    Mathf.Abs(follow.bounds.yMin - expectedBounds.yMin) < 0.001f &&
                    Mathf.Abs(follow.bounds.width - expectedBounds.width) < 0.001f &&
                    Mathf.Abs(follow.bounds.height - expectedBounds.height) < 0.001f,
                    "相机 clamp 用了" + boundsSource);

                Check(sb, ref fails,
                    Mathf.Abs(follow.minSize - level.Data.cameraMinSize) < 0.001f,
                    "相机 minSize 用了关卡值");
                Check(sb, ref fails,
                    Mathf.Abs(follow.maxSize - level.Data.cameraMaxSize) < 0.001f,
                    "相机 maxSize 用了关卡值");
            }
        }

        sb.AppendLine(fails == 0 ? "全部通过。" : ("失败 " + fails + " 项。"));
        text = sb.ToString();
        return fails == 0;
    }

    /// <summary>摆一把不属于关卡数据的探针钥匙，只用来验拾取规则。</summary>
    static LabKey MakeProbeKey(Vector2 position)
    {
        var go = new GameObject("ProbeKey");
        go.transform.position = position;
        go.AddComponent<BoxCollider2D>();
        go.AddComponent<SpriteRenderer>();
        LabKey key = go.AddComponent<LabKey>();
        key.ApplyShape();
        return key;
    }

    static void Check(StringBuilder sb, ref int fails, bool condition, string label)
    {
        if (!condition) fails++;
        sb.AppendLine((condition ? "  OK   " : "  FAIL ") + label);
    }
}
#endif
