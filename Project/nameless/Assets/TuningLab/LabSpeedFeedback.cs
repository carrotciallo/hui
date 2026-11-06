using UnityEngine;

/// <summary>
/// 速度反馈（Issue #62）：合速度达到档位后，玩家移动留下残影和拖尾笔触。
///
///   残影 = 矩形剪影（碰撞箱同尺寸、本体基色），按间隔留在路径上、先维持再淡出；
///   拖尾 = 每隔几个物理步向运动反方向甩一个亮色笔触，垂直方向按伪随机相位浮动，
///          沿反方向缓慢拖行，后段才淡出——像画笔拉出的速度线。
///
/// 浮动用生成序号哈希出确定性的幅度与相位，无分配、无随机数状态。
/// 两类元素各自维护一个上限池，满员回收最老的，杜绝连续高速时的分配抖动。
/// 层级压在本体（20）之后、隐藏方块（6）之前；档位与颜色全部从 TuningLab Inspector 读取。
/// 挂在玩家下的 SpeedFx 子物体上：喷发对齐 FixedUpdate，淡出对齐渲染帧。
/// </summary>
public sealed class LabSpeedFeedback : MonoBehaviour
{
    public const int SortingOrder = 18;

    sealed class Entry
    {
        public GameObject go;
        public SpriteRenderer sr;
        public bool isTrail;
        public float age;
        public float life;
        public Color color;
        public Vector2 basePos;
        public Vector2 drift;
        public Vector2 perp;
        public float bobAmp;
        public float bobPhase;
    }

    TuningLab lab;
    LabPlayer player;
    readonly System.Collections.Generic.List<Entry> ghosts = new System.Collections.Generic.List<Entry>();
    readonly System.Collections.Generic.List<Entry> dots = new System.Collections.Generic.List<Entry>();
    float ghostTimer;
    int spawnCounter;
    int fixedStep;

    public void Bind(TuningLab tuningLab, LabPlayer target)
    {
        lab = tuningLab;
        player = target;
    }

    void FixedUpdate()
    {
        fixedStep++;
        TrySpawnGhost();
        TrySpawnTrailDot();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        Expire(ghosts, dt);
        Expire(dots, dt);
    }

    /// <summary>顶档 = tierCount（默认 6 时即 V6）；其余档位共用起始参数。</summary>
    bool IsTopTier(int tier) => lab != null && tier >= Mathf.Max(2, lab.tierCount);

    void TrySpawnGhost()
    {
        if (lab == null || player == null) return;
        if (player.Suspended || !player.IsAlive) return;

        int tier = player.SpeedTier;
        bool top = IsTopTier(tier);
        int count = top ? lab.ghostCountTopTier : lab.ghostCountTier3;
        float life = top ? lab.ghostLifeTopTier : lab.ghostLifeTier3;
        float alpha = top ? lab.ghostAlphaTopTier : lab.ghostAlphaTier3;

        ghostTimer -= Time.fixedDeltaTime;
        if (tier < Mathf.Max(1, lab.speedFxStartTier))
        {
            ghostTimer = 0f;
            return;
        }
        if (count <= 0 || life <= 0f || ghostTimer > 0f) return;

        ghostTimer = life / Mathf.Max(1, count);
        float size = player.pixels / (float)TuningLab.CellPixels;
        int id = spawnCounter++;
        Spawn(ghosts, Acquire(ghosts, count), player.BodyBounds.center, size, size, life,
              player.BaseColor * alpha, Vector2.zero, Vector2.zero, 0f, 0f, id);
    }

    void TrySpawnTrailDot()
    {
        if (lab == null || player == null) return;
        if (player.Suspended || !player.IsAlive) return;

        int tier = player.SpeedTier;
        if (tier < Mathf.Max(1, lab.speedFxStartTier) || lab.trailCap <= 0 || lab.trailLife <= 0f) return;

        int every = Mathf.Max(1, lab.trailEverySteps);
        if (fixedStep % every != 0) return;

        Vector2 velocity = player.Velocity;
        if (velocity.sqrMagnitude < 0.0001f) return;

        bool top = IsTopTier(tier);
        float alpha = top ? lab.trailAlphaTopTier : lab.trailAlphaTier3;
        float dotPixels = Mathf.Max(2, player.pixels / 5);
        float size = dotPixels / (float)TuningLab.CellPixels;
        float box = player.pixels / (float)TuningLab.CellPixels;

        Vector2 back = -velocity.normalized;
        Vector2 perp = new Vector2(-back.y, back.x);

        int id = spawnCounter++;
        // 喷口摊开：沿垂线 ±0.45 个身位 + 身后半个身位起步，不再糊在屁股上。
        float jitter = (Hash01(id) - 0.5f) * 0.9f * box;
        Vector2 here = player.BodyBounds.center;
        Vector2 spawnPos = here
            + back * (box * 0.55f + size * 0.5f)
            + perp * jitter;
        // 反方向缓慢拖行，笔触跟着人往后抽。
        Vector2 drift = back * (Mathf.Min(velocity.magnitude, 40f) * 0.06f);

        Spawn(dots, Acquire(dots, lab.trailCap), spawnPos, size, size * 1.8f, lab.trailLife,
              player.BaseColor * alpha, spawnPos, drift, box * 0.18f, Hash01(id + 977) * Mathf.PI * 2f, id);
    }

    void Spawn(System.Collections.Generic.List<Entry> pool, Entry e, Vector2 center,
               float width, float height, float life, Color color, Vector2 basePos,
               Vector2 drift, float bobAmp, float bobPhase, int id)
    {
        if (e == null) return;
        e.isTrail = ReferenceEquals(pool, dots);
        e.age = 0f;
        e.life = life;
        e.color = color;
        e.basePos = basePos;
        e.drift = drift;
        e.perp = new Vector2(-drift.y, drift.x);
        e.bobAmp = bobAmp;
        e.bobPhase = bobPhase;

        var t = e.go.transform;
        t.position = new Vector3(center.x, center.y, 0f);
        t.localScale = new Vector3(width, height, 1f);
        // 每个笔触有固定但不同的倾斜，笔刷感而非整齐一排。
        float tilt = (Hash01(id + 31) - 0.5f) * 24f;
        t.rotation = Quaternion.Euler(0f, 0f, tilt);
        e.sr.color = color;
        e.go.SetActive(true);
    }

    /// <summary>取一个可复用元素：优先未激活的，其次最老的，都没有就按上限扩池。</summary>
    Entry Acquire(System.Collections.Generic.List<Entry> pool, int cap)
    {
        Entry oldest = null;
        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (e == null || e.go == null) { pool.RemoveAt(i--); continue; }
            if (!e.go.activeSelf) return e;
            if (oldest == null || e.age > oldest.age) oldest = e;
        }
        if (pool.Count < Mathf.Max(1, cap))
        {
            var created = NewEntry();
            pool.Add(created);
            return created;
        }
        return oldest ?? (pool.Count > 0 ? pool[0] : null);
    }

    Entry NewEntry()
    {
        var go = new GameObject("Fx");
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = TuningLab.WhiteSprite;
        sr.sortingOrder = SortingOrder;
        go.SetActive(false);
        return new Entry { go = go, sr = sr };
    }

    void Expire(System.Collections.Generic.List<Entry> pool, float dt)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var e = pool[i];
            if (e == null || e.go == null) { pool.RemoveAt(i--); continue; }
            if (!e.go.activeSelf) continue;

            e.age += dt;
            float t = Mathf.Clamp01(e.age / Mathf.Max(0.0001f, e.life));
            if (t >= 1f) { e.go.SetActive(false); continue; }

            // 残影：前 30% 维持，之后线性淡出；拖尾：前 70% 保持笔触，后段才散。
            float knee = e.isTrail ? 0.7f : 0.3f;
            float k = t <= knee ? 1f : 1f - (t - knee) / (1f - knee);

            var c = e.color;
            c.a *= k;
            e.sr.color = c;

            float bob = Mathf.Sin(e.bobPhase + e.age * 9f) * e.bobAmp * k;
            Vector2 pos = e.basePos + e.drift * e.age + e.perp * bob;
            e.go.transform.position = new Vector3(pos.x, pos.y, 0f);
        }
    }

    static float Hash01(int n)
    {
        uint x = (uint)n * 2654435761u;
        x ^= x >> 13; x *= 1597334677u; x ^= x >> 16;
        return (x & 0xFFFFFF) / 16777215f;
    }
}
