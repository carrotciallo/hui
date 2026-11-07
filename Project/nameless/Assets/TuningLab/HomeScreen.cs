using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏主页：纯黑背景、三个纯图标入口，以及不参与玩法的装饰动画。
/// 主页只负责进入正式关卡和打开现有设置界面，不复制关卡或玩法规则。
/// </summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class HomeScreen : MonoBehaviour
{
    public const string LevelSceneName = "Final Map";

    enum HomeAction
    {
        NewGame,
        LoadGame,
        Settings
    }

    sealed class BackgroundSquare
    {
        public Vector2 position;
    }

    sealed class FloatingArt
    {
        public Texture2D texture;
        public Vector2 position;
        public Vector2 size;
        public float rotation;
        public float rotationSpeed;
        public float moveSpeed;
    }

    sealed class PendingArt
    {
        public Texture2D texture;
        public float scale;
        public float moveSpeed;
        public float rotationSpeed;
        public float height;
        public float dueAt;
    }

    static readonly string[] ButtonIconNames =
    {
        "KeyBindings",
        "Resolution",
        "Settings"
    };

    static readonly Color GridColor = new Color(0.36f, 0.89f, 1f, 0.58f);
    static readonly Color ButtonFill = new Color(0.015f, 0.025f, 0.035f, 0.88f);
    static readonly Color ButtonBorder = new Color(0.36f, 0.89f, 1f, 0.82f);
    static readonly Color HoverBorder = new Color(0.93f, 0.98f, 1f, 1f);
    static readonly Color ArtTint = new Color(1f, 1f, 1f, 0.72f);

    readonly List<Texture2D> artPool = new List<Texture2D>();
    readonly List<BackgroundSquare> backgroundSquares = new List<BackgroundSquare>();
    readonly List<FloatingArt> floatingArt = new List<FloatingArt>();
    readonly List<PendingArt> pendingArt = new List<PendingArt>();
    readonly Texture2D[] buttonIcons = new Texture2D[3];
    readonly float[] hoverTimes = new float[3];

    LabSettingsUI settingsUI;
    System.Random random;
    Texture2D fallbackTexture;
    bool ready;
    bool navigating;
    int hoveredButton = -1;
    int lastScreenWidth;
    int lastScreenHeight;
    float visualClock;
    float nextBatchAt;
    float transitionElapsed;

    bool SettingsOpen => settingsUI != null && settingsUI.IsOpen;
    bool MotionPaused => SettingsOpen || navigating;

    void Awake()
    {
        EnsureCamera();
        EnsureSettingsUI();
        LoadIcons();
        random = new System.Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
        RebuildBackground();
        ready = true;
    }

    void Start()
    {
        // 让主页也应用已保存的音量/窗口设置；设置界面随后只负责修改和保存。
        settingsUI.Bind(null);
        nextBatchAt = 0f;
    }

    void Update()
    {
        if (!ready) return;

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            RebuildBackground();
        }

        bool blocked = MotionPaused;
        int nextHover = blocked ? -1 : FindHoveredButton(MouseGuiPosition());
        if (nextHover != hoveredButton)
        {
            hoveredButton = nextHover;
            for (int i = 0; i < hoverTimes.Length; i++) hoverTimes[i] = 0f;
        }

        if (blocked) return;

        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        visualClock += dt;
        if (hoveredButton >= 0) hoverTimes[hoveredButton] += dt;

        UpdatePendingArt();
        UpdateFloatingArt(dt);

        if (visualClock >= nextBatchAt)
        {
            ScheduleBatch();
            nextBatchAt = visualClock + HomeScreenSpec.NextBatchDelay(random);
        }
    }

    void OnDestroy()
    {
        if (fallbackTexture != null) Destroy(fallbackTexture);
    }

    void OnGUI()
    {
        if (!ready) return;

        DrawSolid(new Rect(0f, 0f, Screen.width, Screen.height), Color.black);
        for (int i = 0; i < backgroundSquares.Count; i++)
        {
            var square = backgroundSquares[i];
            DrawOutline(new Rect(square.position.x, square.position.y,
                HomeScreenSpec.BackgroundSquareSize, HomeScreenSpec.BackgroundSquareSize),
                HomeScreenSpec.BackgroundSquareStroke, GridColor);
        }

        for (int i = 0; i < floatingArt.Count; i++) DrawFloatingArt(floatingArt[i]);

        if (!navigating)
        {
            Rect[] rects = ButtonRects();
            for (int i = 0; i < rects.Length; i++) DrawButton((HomeAction)i, rects[i]);
        }

        if (navigating) DrawCloseTransition();
    }

    void DrawButton(HomeAction action, Rect baseRect)
    {
        bool interactive = !SettingsOpen && !navigating;
        bool hovered = interactive && hoveredButton == (int)action;
        float breath = hovered ? HomeScreenSpec.ButtonBreathScale(hoverTimes[(int)action]) : 1f;
        Rect buttonRect = ScaleRect(baseRect, breath);

        DrawSolid(buttonRect, hovered ? new Color(0.025f, 0.045f, 0.06f, 0.96f) : ButtonFill);
        DrawOutline(buttonRect, 2f, hovered ? HoverBorder : ButtonBorder);

        Texture2D icon = buttonIcons[(int)action] != null ? buttonIcons[(int)action] : fallbackTexture;
        if (icon != null)
        {
            Rect iconRect = new Rect(
                buttonRect.center.x - HomeScreenSpec.IconSize * 0.5f,
                buttonRect.center.y - HomeScreenSpec.IconSize * 0.5f,
                HomeScreenSpec.IconSize,
                HomeScreenSpec.IconSize);

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            if (hovered)
            {
                float flip = HomeScreenSpec.IconFlipScale(hoverTimes[(int)action]);
                GUIUtility.ScaleAroundPivot(new Vector2(flip, 1f), iconRect.center);
            }
            GUI.color = hovered ? Color.white : new Color(0.88f, 0.94f, 0.96f, 1f);
            GUI.DrawTexture(iconRect, icon, ScaleMode.StretchToFill, true);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        bool wasEnabled = GUI.enabled;
        GUI.enabled = interactive;
        if (GUI.Button(baseRect, GUIContent.none, GUIStyle.none)) Activate(action);
        GUI.enabled = wasEnabled;
    }

    void Activate(HomeAction action)
    {
        if (!HomeScreenSpec.CanStartNavigation(SettingsOpen, navigating)) return;

        if (action == HomeAction.Settings)
        {
            hoveredButton = -1;
            if (HomeScreenSpec.CanOpenSettings(navigating)) settingsUI.Open();
            return;
        }

        // 存档功能尚未实现；读取存档本轮精确等同于新游戏。
        if (action == HomeAction.NewGame || action == HomeAction.LoadGame)
        {
            hoveredButton = -1;
            if (!Application.CanStreamedLevelBeLoaded(LevelSceneName))
            {
                Debug.LogError($"HomeScreen: 构建场景列表中没有可加载的场景“{LevelSceneName}”。");
                return;
            }
            StartCoroutine(CloseAndLoadLevel());
        }
    }

    IEnumerator CloseAndLoadLevel()
    {
        if (!HomeScreenSpec.CanStartNavigation(SettingsOpen, navigating)) yield break;

        navigating = true;
        // 转场期间连 Esc 也不能把设置面板重新打开。
        if (settingsUI != null) settingsUI.enabled = false;
        transitionElapsed = 0f;
        while (transitionElapsed < HomeScreenSpec.TransitionDuration)
        {
            transitionElapsed = Mathf.Min(
                HomeScreenSpec.TransitionDuration,
                transitionElapsed + Time.unscaledDeltaTime);
            yield return null;
        }

        if (Application.CanStreamedLevelBeLoaded(LevelSceneName))
        {
            SceneManager.LoadScene(LevelSceneName);
            yield break;
        }

        Debug.LogError($"HomeScreen: 无法加载场景“{LevelSceneName}”。");
        navigating = false;
        if (settingsUI != null) settingsUI.enabled = true;
        transitionElapsed = 0f;
    }

    void DrawCloseTransition()
    {
        float width = Screen.width;
        float height = Screen.height;
        Rect aperture = HomeScreenSpec.TransitionAperture(transitionElapsed, width, height);
        if (aperture.width <= 0f || aperture.height <= 0f)
        {
            DrawSolid(new Rect(0f, 0f, width, height), Color.black);
            return;
        }

        DrawSolid(new Rect(0f, 0f, width, aperture.y), Color.black);
        DrawSolid(new Rect(0f, aperture.yMax, width, height - aperture.yMax), Color.black);
        DrawSolid(new Rect(0f, aperture.y, aperture.x, aperture.height), Color.black);
        DrawSolid(new Rect(aperture.xMax, aperture.y,
            width - aperture.xMax, aperture.height), Color.black);
    }

    void DrawFloatingArt(FloatingArt art)
    {
        if (art.texture == null) return;

        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;
        Rect rect = new Rect(
            art.position.x - art.size.x * 0.5f,
            art.position.y - art.size.y * 0.5f,
            art.size.x,
            art.size.y);

        GUIUtility.RotateAroundPivot(art.rotation, art.position);
        GUI.color = ArtTint;
        GUI.DrawTexture(rect, art.texture, ScaleMode.StretchToFill, true);
        GUI.color = oldColor;
        GUI.matrix = oldMatrix;
    }

    void UpdatePendingArt()
    {
        for (int i = pendingArt.Count - 1; i >= 0; i--)
        {
            var pending = pendingArt[i];
            if (pending.dueAt > visualClock) continue;

            floatingArt.Add(new FloatingArt
            {
                texture = pending.texture,
                size = new Vector2(
                    Mathf.Max(1f, pending.texture.width * pending.scale),
                    Mathf.Max(1f, pending.texture.height * pending.scale)),
                position = new Vector2(
                    -pending.texture.width * pending.scale * 0.5f - RandomRange(0f, 48f),
                    pending.height),
                rotation = RandomRange(0f, 360f),
                rotationSpeed = pending.rotationSpeed,
                moveSpeed = pending.moveSpeed
            });
            pendingArt.RemoveAt(i);
        }
    }

    void UpdateFloatingArt(float dt)
    {
        for (int i = floatingArt.Count - 1; i >= 0; i--)
        {
            var art = floatingArt[i];
            art.position.x += art.moveSpeed * dt;
            art.rotation += art.rotationSpeed * dt;
            if (art.position.x - art.size.x * 0.5f > Screen.width + 24f)
                floatingArt.RemoveAt(i);
        }
    }

    void ScheduleBatch()
    {
        if (artPool.Count == 0) return;

        int count = HomeScreenSpec.NextBatchCount(random);
        for (int i = 0; i < count; i++)
        {
            Texture2D texture = artPool[random.Next(artPool.Count)];
            float scale = HomeScreenSpec.NextArtScale(random);
            float halfHeight = texture.height * scale * 0.5f;
            float minHeight = Mathf.Min(Screen.height * 0.5f, halfHeight + 12f);
            float maxHeight = Mathf.Max(minHeight, Screen.height - halfHeight - 12f);
            pendingArt.Add(new PendingArt
            {
                texture = texture,
                scale = scale,
                moveSpeed = HomeScreenSpec.NextArtMoveSpeed(random),
                rotationSpeed = HomeScreenSpec.NextArtRotationSpeed(random) *
                    (random.Next(2) == 0 ? -1f : 1f),
                height = RandomRange(minHeight, maxHeight),
                dueAt = visualClock + HomeScreenSpec.NextArtStagger(random)
            });
        }
    }

    void LoadIcons()
    {
        for (int i = 0; i < ButtonIconNames.Length; i++)
            buttonIcons[i] = Resources.Load<Texture2D>("SettingsIcons/" + ButtonIconNames[i]);

        var mapArt = Resources.LoadAll<Texture2D>("HuiArt");
        var uiArt = Resources.LoadAll<Texture2D>("SettingsIcons");
        AddArt(mapArt);
        AddArt(uiArt);
        artPool.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        if (artPool.Count == 0)
        {
            fallbackTexture = MakeFallbackTexture();
            artPool.Add(fallbackTexture);
            Debug.LogWarning("HomeScreen: 未找到主页漂浮素材，已使用占位图。");
        }
        else
        {
            fallbackTexture = MakeFallbackTexture();
        }
    }

    void AddArt(Texture2D[] textures)
    {
        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            if (texture != null && !artPool.Contains(texture)) artPool.Add(texture);
        }
    }

    static Texture2D MakeFallbackTexture()
    {
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = "HomeFallback",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[16 * 16];
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
            pixels[y * 16 + x] = (x < 2 || y < 2 || x > 13 || y > 13)
                ? new Color32(92, 227, 255, 255)
                : new Color32(8, 24, 32, 210);
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    void RebuildBackground()
    {
        lastScreenWidth = Mathf.Max(1, Screen.width);
        lastScreenHeight = Mathf.Max(1, Screen.height);
        backgroundSquares.Clear();

        Rect[] buttons = ButtonRects();
        Rect buttonRegion = buttons[0];
        for (int i = 1; i < buttons.Length; i++) buttonRegion = Union(buttonRegion, buttons[i]);
        buttonRegion = Expand(buttonRegion, 72f);

        int count = HomeScreenSpec.NextBackgroundSquareCount(random);
        for (int i = 0; i < count; i++)
        {
            Rect candidate = Rect.zero;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                float maxX = Mathf.Max(12f,
                    lastScreenWidth - HomeScreenSpec.BackgroundSquareSize - 12f);
                float maxY = Mathf.Max(12f,
                    lastScreenHeight - HomeScreenSpec.BackgroundSquareSize - 12f);
                candidate = new Rect(
                    Mathf.Round(RandomRange(12f, maxX)),
                    Mathf.Round(RandomRange(12f, maxY)),
                    HomeScreenSpec.BackgroundSquareSize,
                    HomeScreenSpec.BackgroundSquareSize);
                if (!candidate.Overlaps(buttonRegion)) break;
            }

            backgroundSquares.Add(new BackgroundSquare { position = candidate.position });
        }
    }

    Rect[] ButtonRects()
    {
        float width = Screen.width;
        float height = Screen.height;
        float rowWidth = HomeScreenSpec.ButtonSize * 3f + HomeScreenSpec.ButtonGap * 2f;
        float left = Mathf.Max(12f, (width - rowWidth) * 0.5f);
        float top = Mathf.Max(12f, (height - HomeScreenSpec.ButtonSize) * 0.5f);
        var result = new Rect[3];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new Rect(
                left + i * (HomeScreenSpec.ButtonSize + HomeScreenSpec.ButtonGap),
                top,
                HomeScreenSpec.ButtonSize,
                HomeScreenSpec.ButtonSize);
        }
        return result;
    }

    int FindHoveredButton(Vector2 pointer)
    {
        Rect[] rects = ButtonRects();
        for (int i = 0; i < rects.Length; i++)
            if (rects[i].Contains(pointer)) return i;
        return -1;
    }

    static Vector2 MouseGuiPosition()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return new Vector2(-1f, -1f);
        Vector2 screen = mouse.position.ReadValue();
        return new Vector2(screen.x, Screen.height - screen.y);
    }

    float RandomRange(float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    void EnsureSettingsUI()
    {
        settingsUI = GetComponent<LabSettingsUI>();
        if (settingsUI == null) settingsUI = gameObject.AddComponent<LabSettingsUI>();
    }

    static void EnsureCamera()
    {
        if (Camera.main != null) return;

        var cameraObject = new GameObject("Home Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.orthographic = true;
        camera.cullingMask = 0;
        cameraObject.AddComponent<AudioListener>();
    }

    static Rect ScaleRect(Rect rect, float scale)
    {
        Vector2 center = rect.center;
        float width = rect.width * scale;
        float height = rect.height * scale;
        return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
    }

    static Rect Expand(Rect rect, float amount)
    {
        return new Rect(
            rect.x - amount,
            rect.y - amount,
            rect.width + amount * 2f,
            rect.height + amount * 2f);
    }

    static Rect Union(Rect a, Rect b)
    {
        float xMin = Mathf.Min(a.xMin, b.xMin);
        float yMin = Mathf.Min(a.yMin, b.yMin);
        float xMax = Mathf.Max(a.xMax, b.xMax);
        float yMax = Mathf.Max(a.yMax, b.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    static void DrawOutline(Rect rect, float stroke, Color color)
    {
        DrawSolid(new Rect(rect.x, rect.y, rect.width, stroke), color);
        DrawSolid(new Rect(rect.x, rect.yMax - stroke, rect.width, stroke), color);
        DrawSolid(new Rect(rect.x, rect.y, stroke, rect.height), color);
        DrawSolid(new Rect(rect.xMax - stroke, rect.y, stroke, rect.height), color);
    }

    static void DrawSolid(Rect rect, Color color)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = oldColor;
    }
}

/// <summary>主页随机范围与动画曲线的单一事实来源，便于测试和后续调参。</summary>
public static class HomeScreenSpec
{
    public const int BackgroundSquareCountMin = 6;
    public const int BackgroundSquareCountMax = 13;
    public const float BackgroundSquareSize = 64f;
    public const float BackgroundSquareStroke = 8f;

    public const float ButtonSize = 144f;
    public const float IconSize = 60f;
    public const float ButtonGap = 54f;
    public const float ButtonBreathDuration = 1.2f;
    public const float ButtonBreathMaxScale = 1.08f;
    public const float IconFlipDuration = 0.75f;
    public const float IconFlipHoldDuration = 0.75f;

    public const int ArtBatchCountMin = 1;
    public const int ArtBatchCountMax = 7;
    public const float ArtBatchDelayMin = 0.625f;
    public const float ArtBatchDelayMax = 2.5f;
    public const float ArtScaleMin = 2f;
    public const float ArtScaleMax = 2.5f;
    public const float ArtMoveSpeedMin = 80f;
    public const float ArtMoveSpeedMax = 220f;
    public const float ArtRotationSpeedMin = 12f;
    public const float ArtRotationSpeedMax = 45f;
    public const float ArtStaggerMin = 0f;
    public const float ArtStaggerMax = 0.18f;

    public const float TransitionCollapseDuration = 0.40f;
    public const float TransitionHoldDuration = 0.12f;
    public const float TransitionStretchDuration = 0.18f;
    public const float TransitionDuration =
        TransitionCollapseDuration + TransitionHoldDuration + TransitionStretchDuration;

    public static bool CanOpenSettings(bool navigating) => !navigating;

    public static bool CanStartNavigation(bool settingsOpen, bool navigating)
    {
        return !settingsOpen && !navigating;
    }

    public static int NextBackgroundSquareCount(System.Random random)
    {
        return random.Next(BackgroundSquareCountMin, BackgroundSquareCountMax + 1);
    }

    public static int NextBatchCount(System.Random random)
    {
        return random.Next(ArtBatchCountMin, ArtBatchCountMax + 1);
    }

    public static float NextBatchDelay(System.Random random)
    {
        return Range(random, ArtBatchDelayMin, ArtBatchDelayMax);
    }

    public static float NextArtScale(System.Random random)
    {
        return Range(random, ArtScaleMin, ArtScaleMax);
    }

    public static float NextArtMoveSpeed(System.Random random)
    {
        return Range(random, ArtMoveSpeedMin, ArtMoveSpeedMax);
    }

    public static float NextArtRotationSpeed(System.Random random)
    {
        return Range(random, ArtRotationSpeedMin, ArtRotationSpeedMax);
    }

    public static float NextArtStagger(System.Random random)
    {
        return Range(random, ArtStaggerMin, ArtStaggerMax);
    }

    public static float ButtonBreathScale(float hoverTime)
    {
        if (hoverTime <= 0f) return 1f;
        float phase = Mathf.Repeat(hoverTime, ButtonBreathDuration) / ButtonBreathDuration;
        float wave = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
        return Mathf.Lerp(1f, ButtonBreathMaxScale, wave);
    }

    public static float IconFlipScale(float hoverTime)
    {
        if (hoverTime <= 0f) return 1f;
        float phase = Mathf.Repeat(hoverTime, IconFlipDuration + IconFlipHoldDuration);
        if (phase >= IconFlipDuration) return 1f;
        return Mathf.Cos(phase / IconFlipDuration * Mathf.PI * 6f);
    }

    public static Rect TransitionAperture(float elapsed, float screenWidth, float screenHeight)
    {
        float width = Mathf.Max(0f, screenWidth);
        float height = Mathf.Max(0f, screenHeight);
        float narrowWidth = width * 0.08f;
        float narrowHeight = Mathf.Min(3f, height);

        float apertureWidth;
        float apertureHeight;
        if (elapsed <= TransitionCollapseDuration)
        {
            float t = SmoothStep01(elapsed / TransitionCollapseDuration);
            apertureWidth = Mathf.Lerp(width, narrowWidth, t);
            apertureHeight = Mathf.Lerp(height, narrowHeight, t);
        }
        else if (elapsed <= TransitionCollapseDuration + TransitionHoldDuration)
        {
            apertureWidth = narrowWidth;
            apertureHeight = narrowHeight;
        }
        else
        {
            float t = Mathf.Clamp01(
                (elapsed - TransitionCollapseDuration - TransitionHoldDuration) /
                TransitionStretchDuration);
            apertureWidth = Mathf.Lerp(narrowWidth, Mathf.Min(width, narrowWidth * 10f), t);
            apertureHeight = narrowHeight;
        }

        return new Rect(
            (width - apertureWidth) * 0.5f,
            (height - apertureHeight) * 0.5f,
            apertureWidth,
            apertureHeight);
    }

    static float Range(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
