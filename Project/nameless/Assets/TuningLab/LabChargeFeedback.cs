using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冲刺充能完成反馈：玩家轮廓闪亮一次，并在头顶喷出少量上浮粒子。
/// 独立于死亡全屏渐晕和传送门粒子，死亡或传送开始时立即清理。
/// </summary>
public sealed class LabChargeFeedback : MonoBehaviour
{
    const int SortingOrder = 22;

    TuningLab lab;
    LabPlayer player;
    SpriteRenderer flash;
    ParticleSystem particles;
    float flashElapsed;

    static readonly Dictionary<int, Sprite> outlineSprites = new Dictionary<int, Sprite>();
    static Material particleMaterial;

    public bool IsActive { get; private set; }
    public int BurstParticleCount => lab != null ? lab.chargeReadyParticleCount : 0;

    public void Bind(TuningLab tuningLab, LabPlayer target)
    {
        if (lab != null) lab.PlayerDied -= OnPlayerDied;
        if (player != null) player.ChargeCompleted -= OnChargeCompleted;

        lab = tuningLab;
        player = target;

        if (player != null) player.ChargeCompleted += OnChargeCompleted;
        if (lab != null) lab.PlayerDied += OnPlayerDied;

        EnsureVisuals();
    }

    /// <summary>播放一次就绪反馈；死亡、暂停、进膛、灵体和传送流程期间拒绝启动。</summary>
    public bool Play()
    {
        if (!CanPlay()) return false;
        EnsureVisuals();

        IsActive = true;
        flashElapsed = 0f;
        if (flash != null) flash.enabled = true;
        UpdateFlash(0f);
        EmitParticles();
        return true;
    }

    void Update()
    {
        if (!IsActive) return;
        if (lab != null && lab.Paused) return;
        if (!CanPlay())
        {
            StopAndClear();
            return;
        }

        flashElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.04f, lab.chargeReadyFlashDuration);
        float t = Mathf.Clamp01(flashElapsed / duration);
        UpdateFlash(t);
        if (t >= 1f)
        {
            IsActive = false;
            if (flash != null) flash.enabled = false;
        }
    }

    bool CanPlay()
    {
        return isActiveAndEnabled
            && lab != null
            && player != null
            && lab.chargeReadyFeedbackEnabled
            && !lab.Paused
            && !lab.TeleportBusy
            && !player.IsDead
            && !player.Suspended
            && !player.IsIntangible;
    }

    void OnChargeCompleted(LabPlayer source)
    {
        if (source == player) Play();
    }

    void OnPlayerDied(LabPlayer deadPlayer)
    {
        if (deadPlayer == player) StopAndClear();
    }

    void EnsureVisuals()
    {
        if (player == null) return;

        if (flash == null)
        {
            var go = new GameObject("ChargeReadyFlash");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            flash = go.AddComponent<SpriteRenderer>();
            flash.sprite = OutlineSprite(player.pixels);
            flash.sortingOrder = SortingOrder;
            flash.enabled = false;
        }

        if (particles == null)
        {
            var go = new GameObject("ChargeReadyParticles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(
                0f, player.pixels / (2f * TuningLab.CellPixels) + 0.2f, 0f);
            particles = go.AddComponent<ParticleSystem>();
            ConfigureParticles(particles);
        }
    }

    void ConfigureParticles(ParticleSystem ps)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = Mathf.Max(0.1f, lab.chargeReadyParticleLife);
        main.startLifetime = new ParticleSystem.MinMaxCurve(lab.chargeReadyParticleLife);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(2f / TuningLab.CellPixels);
        main.startColor = Color.Lerp(player.BaseColor, Color.white, 0.45f);
        main.maxParticles = Mathf.Clamp(lab.chargeReadyParticleCount, 1, 10);
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = false;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.28f, 0.05f, 0f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(fade);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = SortingOrder;
        renderer.sharedMaterial = ParticleMaterial();
    }

    void EmitParticles()
    {
        if (particles == null) return;
        particles.Clear(true);
        particles.Emit(Mathf.Clamp(lab.chargeReadyParticleCount, 1, 10));
    }

    void UpdateFlash(float t)
    {
        if (flash == null) return;
        float alpha = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * lab.chargeReadyFlashAlpha;
        Color color = Color.Lerp(player.BaseColor, Color.white, 0.65f);
        color.a = alpha;
        flash.color = color;
    }

    void StopAndClear()
    {
        IsActive = false;
        flashElapsed = 0f;
        if (flash != null) flash.enabled = false;
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnDisable()
    {
        StopAndClear();
    }

    void OnDestroy()
    {
        if (lab != null) lab.PlayerDied -= OnPlayerDied;
        if (player != null) player.ChargeCompleted -= OnChargeCompleted;
    }

    static Sprite OutlineSprite(int pixels)
    {
        int size = Mathf.Clamp(pixels, 1, TuningLab.CellPixels) + 4;
        if (outlineSprites.TryGetValue(size, out var sprite) && sprite != null) return sprite;

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var colors = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool border = x < 2 || y < 2 || x >= size - 2 || y >= size - 2;
                colors[y * size + x] = border
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            }
        texture.SetPixels32(colors);
        texture.Apply();

        sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), TuningLab.CellPixels);
        outlineSprites[size] = sprite;
        return sprite;
    }

    static Material ParticleMaterial()
    {
        if (particleMaterial != null) return particleMaterial;
        particleMaterial = new Material(Shader.Find("Sprites/Default"));
        particleMaterial.mainTexture = Texture2D.whiteTexture;
        return particleMaterial;
    }
}
