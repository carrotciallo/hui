using UnityEngine;

/// <summary>Unity 音量属性最大为 1，这里用 DSP 增益保留设置滑条的上半程。</summary>
public sealed class LabAudioGain : MonoBehaviour
{
    public bool IsSfx;

    void OnAudioFilterRead(float[] samples, int channels)
    {
        float gain = IsSfx ? LabAudio.SfxVolume : LabAudio.MasterVolume;
        if (gain == 1f) return;
        for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
    }
}
