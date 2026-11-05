using UnityEngine;

/// <summary>
/// 速度反馈（Issue #62）：合速度达到档位后，玩家移动留下残影和拖尾亮点。
///
///   残影 = 矩形剪影（碰撞箱同尺寸、本体基色），按间隔生成、线性淡出；
///   拖尾 = 每物理帧向运动反方向洒一个小亮点，同样淡出。
///
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
        public float age;
        public float life;
        public Color color;
    }

    TuningLab lab;
    LabPlayer player;
    readonly System.Collections.Generic.List<Entry> ghosts = new System.Collections.Generic.List<Entry>();
    readonly System.Collections.Generic.List<Entry> dots = new System.Collections.Generic.List<Entry>();
    Transform body;
    float ghostTimer;

    public void Bind(TuningLab tuningLab, LabPlayer target)
    {
        lab = tuningLab;
        player = target;
    }

    void FixedUpdate()
    {
        TrySpawnGhost();
        TrySpawnTrailDot();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        Expire(ghosts, dt);
        Expire(dots, dt);
    }

    /// <summary>顶档 = tierCount（默认 4 时即 V4）；其余档位共用起始参数。</summary>
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
        Spawn(ghosts, Acquire(ghosts, count), player.BodyBounds.center, size, life,
              player.BaseColor * alpha);
    }

    void TrySpawnTrailDot()
    {
        if (lab == null || player == null) return;
        if (player.Suspended || !player.IsAlive) return;

        int tier = player.SpeedTier;
        if (tier < Mathf.Max(1, lab.speedFxStartTier) || lab.trailCap <= 0 || lab.trailLife <= 0f) return;

        Vector2 velocity = player.Velocity;
        if (velocity.sqrMagnitude < 0.0001f) return;

        bool top = IsTopTier(tier);
        float alpha = top ? lab.trailAlphaTopTier : lab.trailAlphaTier3;
        float dotPixels = Mathf.Max(2, player.pixels / 5);
        float size = dotPixels / (float)TuningLab.CellPixels;

        Vector2 rear = -velocity.normalized *
            (player.pixels / (2f * TuningLab.CellPixels) + size * 0.5f);
        Bounds body = player.BodyBounds;
        Spawn(dots, Acquire(dots, lab.trailCap),
              new Vector2(body.center.x + rear.x, body.center.y + rear.y),
              size, lab.trailLife, player.BaseColor * alpha);
    }

    static void Spawn(System.Collections.Generic.List<Entry> pool, Entry e,
                      Vector2 center, float size, float life, Color color)
    {
        e.age = 0f;
        e.life = life;
        e.color = color;
        e.go.transform.position = new Vector3(center.x, center.y, 0f);
        e.go.transform.localScale = new Vector3(size, size, 1f);
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
        var go = new GameObject("Ghost");
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
            float k = 1f - Mathf.Clamp01(e.age / Mathf.Max(0.0001f, e.life));
            if (k <= 0f) { e.go.SetActive(false); continue; }

            var c = e.color;
            c.a *= k;
            e.sr.color = c;
        }
    }
}
