using UnityEngine;

/// <summary>
/// 运行时音效播放器。素材放在 Assets/Sounds/Resources/&lt;NN_用途&gt;/*.mp3，
/// 整目录 Resources.LoadAll 取，因此不碰场景、不接 Inspector、不改工程设置
/// （和 LabHud 的 IMGUI 一样是零场景依赖的路子）。
///
/// 静态入口 Play(Cue)：没有实例时直接返回——无头测试和编辑器预览都不会因此炸。
/// 变体轮换：同一目录里按下标循环取用，避免连续两次完全一样的声音。
/// 冷却：同一 Cue 在 CooldownSeconds 内只响一次，防止一帧里的多个接触点叠成爆音。
/// </summary>
public class LabAudio : MonoBehaviour
{
    /// <summary>音效位；顺序必须和 Folders 一致。</summary>
    public enum Cue
    {
        Jump,
        ImpactLight,
        ImpactMedium,
        ImpactHeavy,
        CrateFall,
        IceBreak,
        IceMelt,
        PortalEnter,
        PortalUi,
        CannonLoad,
        CannonFire,
        RopeBounce,
        LightBridgePass,
        LightBridgeLand,
        LightBridgeSpawn,
        LightBridgeDespawn,
    }

    // 光桥四个新 Cue 暂时复用现有开源素材；保留独立 Cue 名，
    // 后续有专用音频时只换目录内容，不改玩法侧调用点。
    static readonly string[] Folders =
    {
        "01_Jump",
        "02_Collision_Light",
        "03_Collision_Medium",
        "04_Collision_Heavy",
        "05_CrateFall",
        "06_IceBreak",
        "07_IceMelt",
        "08_PortalEnter",
        "09_PortalUI",
        "10_CannonLoad",
        "11_CannonFire",
        "12_RopeBounce",
        "07_IceMelt",
        "02_Collision_Light",
        "08_PortalEnter",
        "07_IceMelt",
    };

    // 撞击以跳跃音量为基准逐级抬高；UI 保持压低。
    static readonly float[] Volumes =
    {
        0.55f, 0.55f, 0.65f, 0.85f, 0.5f, 0.65f, 0.4f, 0.7f, 0.35f, 0.4f, 0.85f, 0.5f,
        0.35f, 0.25f, 0.5f, 0.3f,
    };

    // 短音效给一点音高抖动，长音和 UI 保持原样。
    static readonly float[] PitchJitter =
    {
        0.06f, 0.08f, 0.08f, 0.05f, 0.06f, 0.07f, 0f, 0f, 0.02f, 0.04f, 0.03f, 0.06f,
        0.03f, 0.05f, 0.02f, 0f,
    };

    const float CooldownSeconds = 0.05f;
    const int VoiceCount = 16;

    static LabAudio current;

    /// <summary>静音开关（关掉后所有 Cue 都不再发声，已开始的尾音不管）。</summary>
    public static bool Muted;

    /// <summary>音效音量倍率，由设置界面写入；主音量仍由 AudioListener.volume 统一控制。</summary>
    public static float SfxVolume = 1f;
    /// <summary>音乐音量倍率。当前没有音乐轨，先存值给后续音乐系统使用。</summary>
    public static float MusicVolume = 1f;

    readonly AudioClip[][] clips = new AudioClip[Folders.Length][];
    readonly int[] cursor = new int[Folders.Length];
    readonly float[] lastPlayed = new float[Folders.Length];

    AudioSource[] voices;
    int nextVoice;

    TuningLab lab;

    void Awake()
    {
        current = this;
        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            // 屏幕空间玩法，不做 3D 衰减；音源自己带音量，PlayOneShot 用 1。
            source.spatialBlend = 0f;
            voices[i] = source;
        }
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }

    /// <summary>绑定规则台：撞击音订阅现成的 PlayerHit，不在 LabPlayer 里加任何东西。</summary>
    public void Bind(TuningLab target)
    {
        if (lab != null) lab.PlayerHit -= OnPlayerHit;
        UnsubscribeBridgeEvents();
        lab = target;
        if (lab == null) return;

        lab.PlayerHit += OnPlayerHit;
        LabPhantom.LightBridgeSpawned += OnLightBridgeSpawned;
        LabPhantom.LightBridgeDespawned += OnLightBridgeDespawned;
        SubscribeExistingBridges();
    }

    void OnDisable()
    {
        if (lab != null) lab.PlayerHit -= OnPlayerHit;
        UnsubscribeBridgeEvents();
    }

    void OnPlayerHit(TuningLab.HitInfo info)
    {
        // 玩家对玩家的撞击：取结算后的挡位，跟撞地形同一套音色。
        PlayImpact(info.tierAfter);
    }

    void OnLightBridgeSpawned(LabPhantom phantom)
    {
        SubscribeBridgeContacts(phantom);
        Play(Cue.LightBridgeSpawn);
    }

    void OnLightBridgeDespawned(LabPhantom phantom)
    {
        UnsubscribeBridgeContacts(phantom);
        Play(Cue.LightBridgeDespawn);
    }

    void OnLightBridgeContact(LabLightBridgeBlock.BridgeContact contact)
    {
        LabLightBridgeBlock block = contact.block;
        if (block == null) return;

        if (block.Vertical)
        {
            if (contact.bounced) Play(Cue.RopeBounce);
            else if (contact.passed) Play(Cue.LightBridgePass);
            return;
        }

        Play(contact.passed ? Cue.LightBridgePass : Cue.LightBridgeLand);
    }

    void SubscribeExistingBridges()
    {
        for (int i = 0; i < LabPhantom.All.Count; i++)
            SubscribeBridgeContacts(LabPhantom.All[i]);
    }

    static void SubscribeBridgeContacts(LabPhantom phantom)
    {
        if (phantom == null) return;
        for (int i = 0; i < phantom.Bridge.Count; i++)
        {
            LabLightBridgeBlock block = phantom.Bridge[i];
            if (block != null) block.Contacted += OnBridgeContactStatic;
        }
    }

    static void UnsubscribeBridgeContacts(LabPhantom phantom)
    {
        if (phantom == null) return;
        for (int i = 0; i < phantom.Bridge.Count; i++)
        {
            LabLightBridgeBlock block = phantom.Bridge[i];
            if (block != null) block.Contacted -= OnBridgeContactStatic;
        }
    }

    static void OnBridgeContactStatic(LabLightBridgeBlock.BridgeContact contact)
    {
        if (current != null) current.OnLightBridgeContact(contact);
    }

    void UnsubscribeBridgeEvents()
    {
        LabPhantom.LightBridgeSpawned -= OnLightBridgeSpawned;
        LabPhantom.LightBridgeDespawned -= OnLightBridgeDespawned;
        for (int i = 0; i < LabPhantom.All.Count; i++)
            UnsubscribeBridgeContacts(LabPhantom.All[i]);
    }

    public static void Play(Cue cue)
    {
        if (current == null || Muted) return;
        current.PlayNow(cue);
    }

    /// <summary>
    /// 撞击音按挡位取档：0-1 轻、2 中、3 及以上重。
    /// 红热边吃到的撞击已经在 LabPlayer 里升到对应挡位，这里不再单独判红热。
    /// </summary>
    public static void PlayImpact(int tier)
    {
        Play(tier >= 3 ? Cue.ImpactHeavy : tier == 2 ? Cue.ImpactMedium : Cue.ImpactLight);
    }

    void PlayNow(Cue cue)
    {
        int index = (int)cue;
        // 用非缩放时间：HUD 的时间缩放能到 0.05，用 Time.time 会把冷却拉成一秒。
        float now = Time.unscaledTime;
        if (now - lastPlayed[index] < CooldownSeconds) return;

        var bag = LoadClipFolder(index);
        if (bag == null || bag.Length == 0) return;

        AudioClip clip = bag[cursor[index] % bag.Length];
        cursor[index]++;
        lastPlayed[index] = now;
        if (clip == null) return;

        var voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % VoiceCount;
        voice.volume = Volumes[index] * Mathf.Clamp01(SfxVolume);
        float jitter = PitchJitter[index];
        voice.pitch = jitter > 0f ? 1f + Random.Range(-jitter, jitter) : 1f;
        voice.PlayOneShot(clip, 1f);
    }

    AudioClip[] LoadClipFolder(int index)
    {
        if (clips[index] != null) return clips[index];

        var found = Resources.LoadAll<AudioClip>(Folders[index]);
        if (found == null || found.Length == 0)
        {
            // 缓存空数组：缺目录时只在第一次报一次，不在每次撞击里刷日志。
            clips[index] = new AudioClip[0];
            Debug.LogWarning($"LabAudio: 找不到音效目录 Resources/{Folders[index]}");
            return clips[index];
        }

        // 同一目录内按下标排序，保证每次运行取变体的顺序一致。
        System.Array.Sort(found, (a, b) => string.CompareOrdinal(a.name, b.name));
        clips[index] = found;
        return clips[index];
    }
}
