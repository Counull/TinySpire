using System;
using System.IO;
using System.Text;
using TinySpire.Presentation.Audio;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 生成 TinySpire 原创 UI 音频：48 kHz、单声道、16-bit PCM。
/// hover 0.09s 上扬双谐波，click 0.12s 双瞬态加确定性纹理，confirm 0.22s 两段和声音阶，
/// error 0.28s 下滑基频加非整数泛音；每项使用独立 attack/release 分层包络而非单频占位 beep。
/// </summary>
public static class UiAudioAssetGenerator
{
    internal const int SampleRate = 48000;
    private const double Tau = Math.PI * 2.0;
    private const double TargetPeak = 0.78;

    /// <summary>确定性生成目录四个原创 WAV，并同步导入为 Unity 资源。</summary>
    [MenuItem("TinySpire/Audio/Generate Deterministic UI Audio")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(AddressablesBuildTools.UiAudioAssetRoot);
        for (int index = 0; index < UiAudioCatalog.Ordered.Count; index++)
        {
            UiAudioCueDefinition definition = UiAudioCatalog.Ordered[index];
            string assetPath = AddressablesBuildTools.UiAudioAssetRoot + "/" +
                               definition.Key + ".wav";
            File.WriteAllBytes(assetPath, BuildWaveBytes(definition.Cue));
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log(
            $"Generated {UiAudioCatalog.Ordered.Count} deterministic UI audio WAV assets at " +
            AddressablesBuildTools.UiAudioAssetRoot + ".");
    }

    /// <summary>把指定 cue 合成为确定性 PCM 并封装标准 RIFF/WAVE 字节。</summary>
    internal static byte[] BuildWaveBytes(UiAudioCue cue)
    {
        int sampleCount = (int)Math.Round(
            GetDurationSeconds(cue) * SampleRate,
            MidpointRounding.AwayFromZero);
        var samples = new double[sampleCount];
        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            double time = sampleIndex / (double)SampleRate;
            samples[sampleIndex] = GenerateSample(cue, sampleIndex, time);
        }

        Normalize(samples, TargetPeak);
        return WritePcmWave(samples);
    }

    /// <summary>返回每个短 cue 的冻结时长，未知身份立即拒绝。</summary>
    private static double GetDurationSeconds(UiAudioCue cue)
    {
        switch (cue)
        {
            case UiAudioCue.Hover:
                return 0.09;
            case UiAudioCue.Click:
                return 0.12;
            case UiAudioCue.Confirm:
                return 0.22;
            case UiAudioCue.Error:
                return 0.28;
            default:
                throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
        }
    }

    /// <summary>按 cue 选择原创分层合成算法。</summary>
    private static double GenerateSample(
        UiAudioCue cue,
        int sampleIndex,
        double time)
    {
        switch (cue)
        {
            case UiAudioCue.Hover:
                return GenerateHoverSample(time);
            case UiAudioCue.Click:
                return GenerateClickSample(sampleIndex, time);
            case UiAudioCue.Confirm:
                return GenerateConfirmSample(time);
            case UiAudioCue.Error:
                return GenerateErrorSample(sampleIndex, time);
            default:
                throw new ArgumentOutOfRangeException(nameof(cue), cue, null);
        }
    }

    /// <summary>合成短促上扬基频、轻微二次谐波与柔和尾部的 hover 层。</summary>
    private static double GenerateHoverSample(double time)
    {
        const double duration = 0.09;
        double progress = time / duration;
        double phase = Tau * (520.0 * time + 0.5 * 360.0 * time * progress);
        double body = Math.Sin(phase) + 0.28 * Math.Sin(phase * 2.03 + 0.35);
        double envelope = AttackReleaseEnvelope(time, duration, attackSeconds: 0.006, releasePower: 2.1);
        return envelope * body;
    }

    /// <summary>合成两个不同衰减速度的点击瞬态，并叠加低电平确定性宽带纹理。</summary>
    private static double GenerateClickSample(int sampleIndex, double time)
    {
        const double duration = 0.12;
        double fastTransient = Math.Exp(-42.0 * time) * Math.Sin(Tau * 1180.0 * time);
        double bodyEnvelope = AttackReleaseEnvelope(
            time,
            duration,
            attackSeconds: 0.0025,
            releasePower: 3.4);
        double body = 0.58 * Math.Sin(Tau * 720.0 * time) +
                      0.24 * Math.Sin(Tau * 1640.0 * time + 0.7);
        double textureEnvelope = AttackReleaseEnvelope(
            time,
            duration,
            attackSeconds: 0.0008,
            releasePower: 4.8);
        double texture = 0.11 * DeterministicNoise(sampleIndex, seed: 0x5a17u) *
                         textureEnvelope * Math.Exp(-55.0 * time);
        return fastTransient + bodyEnvelope * body + texture;
    }

    /// <summary>合成先后进入的两个和声音阶层，使 confirm 具有上行而非单音提示。</summary>
    private static double GenerateConfirmSample(double time)
    {
        double first = NoteLayer(
            localTime: time,
            duration: 0.145,
            frequency: 620.0,
            harmonicRatio: 1.5);
        double second = NoteLayer(
            localTime: time - 0.072,
            duration: 0.148,
            frequency: 830.0,
            harmonicRatio: 1.25);
        return first + 0.92 * second;
    }

    /// <summary>合成下滑基频、非整数泛音与轻微脉冲纹理的 error 双层提示。</summary>
    private static double GenerateErrorSample(int sampleIndex, double time)
    {
        const double duration = 0.28;
        double progress = time / duration;
        double phase = Tau * (340.0 * time - 0.5 * 150.0 * time * progress);
        double body = Math.Sin(phase) + 0.43 * Math.Sin(phase * 1.47 + 0.9);
        double pulse = 0.78 + 0.22 * Math.Sin(Tau * 11.0 * time);
        double envelope = AttackReleaseEnvelope(
            time,
            duration,
            attackSeconds: 0.008,
            releasePower: 1.65);
        double textureEnvelope = AttackReleaseEnvelope(
            time,
            duration,
            attackSeconds: 0.0015,
            releasePower: 2.6);
        double texture = 0.035 * DeterministicNoise(sampleIndex, seed: 0xc0deu) *
                         textureEnvelope * Math.Exp(-9.0 * time);
        return envelope * pulse * body + texture;
    }

    /// <summary>生成带基频与非八度泛音、拥有独立 attack/release 的单个音阶层。</summary>
    private static double NoteLayer(
        double localTime,
        double duration,
        double frequency,
        double harmonicRatio)
    {
        if (localTime < 0.0 || localTime >= duration)
            return 0.0;

        double envelope = AttackReleaseEnvelope(
            localTime,
            duration,
            attackSeconds: 0.006,
            releasePower: 2.35);
        double fundamental = Math.Sin(Tau * frequency * localTime);
        double harmonic = Math.Sin(Tau * frequency * harmonicRatio * localTime + 0.42);
        return envelope * (fundamental + 0.31 * harmonic);
    }

    /// <summary>生成平滑起音与幂次收尾的零边界包络，避免 WAV 首尾爆音。</summary>
    private static double AttackReleaseEnvelope(
        double time,
        double duration,
        double attackSeconds,
        double releasePower)
    {
        if (time < 0.0 || time >= duration)
            return 0.0;

        double attack = Math.Min(1.0, time / attackSeconds);
        attack = attack * attack * (3.0 - 2.0 * attack);
        double remaining = Math.Max(0.0, 1.0 - time / duration);
        return attack * Math.Pow(remaining, releasePower);
    }

    /// <summary>用整数混合函数生成跨运行一致的 -1～1 纹理值，不依赖 Unity Random。</summary>
    private static double DeterministicNoise(int sampleIndex, uint seed)
    {
        uint value = unchecked((uint)(sampleIndex + 1) * 747796405u + seed);
        value ^= value >> 16;
        value = unchecked(value * 2246822519u);
        value ^= value >> 13;
        return (value & 0xffffu) / 32767.5 - 1.0;
    }

    /// <summary>按固定峰值等比归一化全部层，保留各 cue 内部动态关系并避免削波。</summary>
    private static void Normalize(double[] samples, double targetPeak)
    {
        double peak = 0.0;
        for (int index = 0; index < samples.Length; index++)
            peak = Math.Max(peak, Math.Abs(samples[index]));
        if (peak <= double.Epsilon)
            throw new InvalidOperationException("Generated UI audio cue is silent.");

        double scale = targetPeak / peak;
        for (int index = 0; index < samples.Length; index++)
            samples[index] *= scale;
    }

    /// <summary>把归一化样本写成标准 44 字节头的 little-endian PCM RIFF/WAVE。</summary>
    private static byte[] WritePcmWave(double[] samples)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        const short blockAlign = channels * bitsPerSample / 8;
        int byteRate = SampleRate * blockAlign;
        int dataSize = samples.Length * blockAlign;

        using var stream = new MemoryStream(capacity: 44 + dataSize);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataSize);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(SampleRate);
        writer.Write(byteRate);
        writer.Write(blockAlign);
        writer.Write(bitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataSize);

        for (int index = 0; index < samples.Length; index++)
        {
            double clamped = Math.Max(-1.0, Math.Min(1.0, samples[index]));
            short pcm = (short)Math.Round(
                clamped * short.MaxValue,
                MidpointRounding.AwayFromZero);
            writer.Write(pcm);
        }

        writer.Flush();
        return stream.ToArray();
    }
}
