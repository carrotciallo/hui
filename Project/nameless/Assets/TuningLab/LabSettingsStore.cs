using System;
using System.IO;
using UnityEngine;

/// <summary>只包含可持久字段的设置 DTO；不序列化任何 GameObject 或场景对象。</summary>
[Serializable]
public sealed class LabSettingsData
{
    public int version = LabSettingsStore.CurrentVersion;
    // 默认 50%；应用时映射为原来的满音量。
    public float masterVolume = 0.5f;
    public float musicVolume = 0.5f;
    public float sfxVolume = 0.5f;
    public bool fullscreen;
    public int screenWidth;
    public int screenHeight;
    /// <summary>亮度预留位：当前只存取，不参与渲染。</summary>
    public float brightness = 1f;
}

/// <summary>
/// 设置的磁盘读写。位置固定在 Application.persistentDataPath：
/// settings.json / settings.backup.json / settings.tmp。
///
/// 保存：先完整写临时文件，再 File.Replace 正式文件并留下上一份备份。
/// 读取：正式档 → 备份 → 默认值；解析失败不覆盖原文件，保留现场。
/// 测试用 DirectoryOverride 指向临时目录，避免碰真实存档。
/// </summary>
public static class LabSettingsStore
{
    public const int CurrentVersion = 2;
    const string FileName = "settings.json";
    const string BackupFileName = "settings.backup.json";
    const string TempFileName = "settings.tmp";

    /// <summary>测试/工具可改写到临时目录；为空时使用 Application.persistentDataPath。</summary>
    public static string DirectoryOverride;

    public static string DataDirectory =>
        string.IsNullOrEmpty(DirectoryOverride) ? Application.persistentDataPath : DirectoryOverride;

    public static string SavePath => Path.Combine(DataDirectory, FileName);
    public static string BackupPath => Path.Combine(DataDirectory, BackupFileName);
    public static string TempPath => Path.Combine(DataDirectory, TempFileName);

    /// <summary>最近一次 Load 是否退到了备份档。</summary>
    public static bool LastLoadUsedBackup { get; private set; }

    public static LabSettingsData CreateDefault()
    {
        int width = Screen.width >= 640 ? Screen.width : 1280;
        int height = Screen.height >= 360 ? Screen.height : 720;
        return new LabSettingsData
        {
            version = CurrentVersion,
            masterVolume = 0.5f,
            musicVolume = 0.5f,
            sfxVolume = 0.5f,
            fullscreen = Screen.fullScreen,
            screenWidth = width,
            screenHeight = height,
            brightness = 1f,
        };
    }

    /// <summary>没有存档时返回默认值；loadedFromDisk 表示是否真的读到过文件。</summary>
    public static LabSettingsData Load(out bool loadedFromDisk)
    {
        LastLoadUsedBackup = false;
        loadedFromDisk = false;

        if (TryRead(SavePath, out var data))
        {
            loadedFromDisk = true;
            return Sanitize(data);
        }

        if (TryRead(BackupPath, out data))
        {
            loadedFromDisk = true;
            LastLoadUsedBackup = true;
            Debug.LogWarning("LabSettingsStore: 正式设置档不可用，已读取备份。");
            return Sanitize(data);
        }

        return CreateDefault();
    }

    /// <summary>写临时文件 → 替换正式档 → 保留备份；失败返回 false 并保留旧档。</summary>
    public static bool Save(LabSettingsData data)
    {
        if (data == null) return false;
        data = Sanitize(data);
        data.version = CurrentVersion;

        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(TempPath, JsonUtility.ToJson(data, true));

            if (File.Exists(SavePath))
            {
                File.Replace(TempPath, SavePath, BackupPath);
            }
            else
            {
                if (File.Exists(TempPath)) File.Move(TempPath, SavePath);
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LabSettingsStore: 保存设置失败（{SavePath}）：{e.Message}");
            return false;
        }
    }

    /// <summary>把设置应用到运行中的游戏。亮度是预留字段，暂不应用。</summary>
    public static void Apply(LabSettingsData data, bool applyWindow)
    {
        if (data == null) return;
        data = Sanitize(data);

        // 50% 对应原来的 100%；DSP 滤镜实现 0-2 倍增益，避免被 Unity 音量属性钳掉。
        AudioListener.volume = 1f;
        LabAudio.MasterVolume = data.masterVolume * 2f;
        LabAudio.SfxVolume = data.sfxVolume * 2f;
        LabAudio.MusicVolume = data.musicVolume * 2f;

        var listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
        if (listener != null && listener.GetComponent<LabAudioGain>() == null)
            listener.gameObject.AddComponent<LabAudioGain>();

        if (!applyWindow) return;

        int width = Mathf.Max(320, data.screenWidth);
        int height = Mathf.Max(180, data.screenHeight);
        if (Screen.width != width || Screen.height != height || Screen.fullScreen != data.fullscreen)
            Screen.SetResolution(width, height, data.fullscreen);
    }

    /// <summary>缺字段、越界值和损坏文件都在这里收口，调用方不必再判。</summary>
    public static LabSettingsData Sanitize(LabSettingsData data)
    {
        if (data == null) return CreateDefault();

        data.version = Mathf.Max(1, data.version);
        // v1 的 100% 是 v2 的 50%；迁移时保持实际听感不变。
        if (data.version == 1)
        {
            data.masterVolume *= 0.5f;
            data.musicVolume *= 0.5f;
            data.sfxVolume *= 0.5f;
            data.version = 2;
        }
        data.masterVolume = Mathf.Clamp01(data.masterVolume);
        data.musicVolume = Mathf.Clamp01(data.musicVolume);
        data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
        data.brightness = Mathf.Clamp01(data.brightness);

        if (data.screenWidth < 640) data.screenWidth = Screen.width >= 640 ? Screen.width : 1280;
        if (data.screenHeight < 360) data.screenHeight = Screen.height >= 360 ? Screen.height : 720;
        return data;
    }

    static bool TryRead(string path, out LabSettingsData data)
    {
        data = null;
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path);
            data = JsonUtility.FromJson<LabSettingsData>(json);
            if (data != null) return true;
            Debug.LogWarning($"LabSettingsStore: 设置档为空或不是对象（{path}）。");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LabSettingsStore: 读取设置失败（{path}）：{e.Message}");
        }

        data = null;
        return false;
    }
}
