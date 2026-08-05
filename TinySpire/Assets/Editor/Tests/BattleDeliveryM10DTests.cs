using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class BattleDeliveryM10DTests
{
    /// <summary>确认交付基线能在固定回放场景中给出两次可比较的采样、分配和清理证据。</summary>
    [Test]
    public void DefaultBattleDeliveryBaseline_ReportsRepeatableSamplesAndCleanup()
    {
        M10DeliveryEvidence firstWindow = M10DeliveryBaseline.Measure();
        M10DeliveryEvidence secondWindow = M10DeliveryBaseline.Measure();

        Assert.That(firstWindow.HasCompleteSampleSets, Is.True);
        Assert.That(secondWindow.HasCompleteSampleSets, Is.True);
        Assert.That(firstWindow.AllSamplesMatchAuthoritativeTrace, Is.True);
        Assert.That(secondWindow.AllSamplesMatchAuthoritativeTrace, Is.True);
        Assert.That(firstWindow.AllSamplesReleasedTweens, Is.True);
        Assert.That(secondWindow.AllSamplesReleasedTweens, Is.True);

        TestContext.Progress.WriteLine(M10DeliveryEvidence.FormatComparison(firstWindow, secondWindow));
    }
}

/// <summary>保存一次固定默认战斗回放采样窗口的环境、帧率分组和清理事实，仅供 M10D Editor 测试报告使用。</summary>
internal sealed class M10DeliveryEvidence
{
    /// <summary>测试执行环境的可读描述，不将其当作性能预算。</summary>
    internal string EnvironmentDescription { get; }

    /// <summary>所有帧率样本开始前的 DOTween 活动数量。</summary>
    internal int TweenBaseline { get; }

    /// <summary>按指定表现推进帧率收集的固定顺序样本组。</summary>
    internal IReadOnlyList<M10DeliveryCadenceEvidence> Cadences { get; }

    /// <summary>确认每个帧率都采集到计划数量的样本，而非把缺失采样伪装为零。</summary>
    internal bool HasCompleteSampleSets => Cadences.All(cadence => cadence.Samples.Count == M10DeliveryBaseline.MeasurementSampleCount);

    /// <summary>确认所有采样都与同一份 60 FPS 默认权威回放轨迹相同。</summary>
    internal bool AllSamplesMatchAuthoritativeTrace => Cadences.All(cadence => cadence.AllSamplesMatchAuthoritativeTrace);

    /// <summary>确认每个采样结束后的 DOTween 数量已回到窗口开始基线。</summary>
    internal bool AllSamplesReleasedTweens => Cadences.All(cadence => cadence.AllSamplesReleasedTweens);

    /// <summary>冻结单个采样窗口，供测试和文档输出使用而不持有任何运行时战斗对象。</summary>
    internal M10DeliveryEvidence(
        string environmentDescription,
        int tweenBaseline,
        IEnumerable<M10DeliveryCadenceEvidence> cadences)
    {
        if (string.IsNullOrWhiteSpace(environmentDescription))
            throw new ArgumentException("Environment description is required.", nameof(environmentDescription));
        if (cadences == null)
            throw new ArgumentNullException(nameof(cadences));

        EnvironmentDescription = environmentDescription;
        TweenBaseline = tweenBaseline;
        Cadences = cadences.ToArray();
    }

    /// <summary>把两次同环境采样窗口及其相对差异压缩为可复制的测试输出，而不宣称任何性能阈值。</summary>
    internal static string FormatComparison(M10DeliveryEvidence firstWindow, M10DeliveryEvidence secondWindow)
    {
        if (firstWindow == null)
            throw new ArgumentNullException(nameof(firstWindow));
        if (secondWindow == null)
            throw new ArgumentNullException(nameof(secondWindow));

        var fragments = new List<string>
        {
            "M10D_PERF_BASELINE",
            firstWindow.EnvironmentDescription,
            $"samples={M10DeliveryBaseline.MeasurementSampleCount}",
            $"tweenBaseline={firstWindow.TweenBaseline}",
        };

        foreach (M10DeliveryCadenceEvidence firstCadence in firstWindow.Cadences)
        {
            M10DeliveryCadenceEvidence secondCadence = secondWindow.Cadences.Single(
                cadence => cadence.FrameRate == firstCadence.FrameRate);
            fragments.Add(
                $"fps={firstCadence.FrameRate}:firstMs={firstCadence.MedianElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)}:secondMs={secondCadence.MedianElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)}:deltaMs={(secondCadence.MedianElapsedMilliseconds - firstCadence.MedianElapsedMilliseconds).ToString("F3", CultureInfo.InvariantCulture)}:firstAlloc={firstCadence.MedianAllocatedBytes}:secondAlloc={secondCadence.MedianAllocatedBytes}:deltaAlloc={secondCadence.MedianAllocatedBytes - firstCadence.MedianAllocatedBytes}:trace={firstCadence.AllSamplesMatchAuthoritativeTrace && secondCadence.AllSamplesMatchAuthoritativeTrace}:tweenReleased={firstCadence.AllSamplesReleasedTweens && secondCadence.AllSamplesReleasedTweens}");
        }

        return string.Join(" | ", fragments);
    }
}

/// <summary>保存一个表现帧率下的多次回放采样，时间和分配值只描述该 Editor 测试窗口。</summary>
internal sealed class M10DeliveryCadenceEvidence
{
    /// <summary>该组回放使用的表现 Tick 帧率。</summary>
    internal int FrameRate { get; }

    /// <summary>固定帧率下按顺序采集的完整回放样本。</summary>
    internal IReadOnlyList<M10DeliverySample> Samples { get; }

    /// <summary>样本耗时的中位数，避免把单次 Editor 抖动伪装为稳定帧时间。</summary>
    internal double MedianElapsedMilliseconds => Median(Samples.Select(sample => sample.ElapsedMilliseconds));

    /// <summary>样本当前线程分配的中位数，仅用于同环境两窗口差异报告。</summary>
    internal long MedianAllocatedBytes => Median(Samples.Select(sample => sample.AllocatedBytes));

    /// <summary>确认所有样本都仍符合 M10C 的默认权威轨迹。</summary>
    internal bool AllSamplesMatchAuthoritativeTrace => Samples.All(sample => sample.MatchesAuthoritativeTrace);

    /// <summary>确认每个样本的 adapter dispose 后都没有额外存活 Tween。</summary>
    internal bool AllSamplesReleasedTweens => Samples.All(sample => sample.TweenCountAfter == sample.TweenBaseline);

    /// <summary>冻结一个帧率样本组，避免把可变 List 暴露给测试断言外部。</summary>
    internal M10DeliveryCadenceEvidence(int frameRate, IEnumerable<M10DeliverySample> samples)
    {
        if (frameRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(frameRate));
        if (samples == null)
            throw new ArgumentNullException(nameof(samples));

        FrameRate = frameRate;
        Samples = samples.ToArray();
    }

    /// <summary>计算 double 样本的中位数，偶数数量时取两个中间样本的平均。</summary>
    private static double Median(IEnumerable<double> values)
    {
        double[] ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
            throw new InvalidOperationException("Cannot calculate a median from an empty sample set.");

        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 1 ? ordered[middle] : (ordered[middle - 1] + ordered[middle]) / 2d;
    }

    /// <summary>计算 long 样本的中位数，偶数数量时使用 checked 平均避免静默溢出。</summary>
    private static long Median(IEnumerable<long> values)
    {
        long[] ordered = values.OrderBy(value => value).ToArray();
        if (ordered.Length == 0)
            throw new InvalidOperationException("Cannot calculate a median from an empty sample set.");

        int middle = ordered.Length / 2;
        return ordered.Length % 2 == 1 ? ordered[middle] : checked((ordered[middle - 1] + ordered[middle]) / 2);
    }
}

/// <summary>保存一次真实 Submit 回放的耗时、当前线程分配和 Tween 清理读数，不保存任何权威战斗事实副本。</summary>
internal readonly struct M10DeliverySample
{
    /// <summary>该样本从 Stopwatch 得到的完整两轮回放墙钟耗时。</summary>
    internal double ElapsedMilliseconds { get; }

    /// <summary>该样本在当前 Editor 测试线程上的托管分配字节数。</summary>
    internal long AllocatedBytes { get; }

    /// <summary>样本开始前的 DOTween 活动数量。</summary>
    internal int TweenBaseline { get; }

    /// <summary>样本回放和 adapter dispose 后的 DOTween 活动数量。</summary>
    internal int TweenCountAfter { get; }

    /// <summary>该样本产出的权威轨迹是否等于窗口的固定参照轨迹。</summary>
    internal bool MatchesAuthoritativeTrace { get; }

    /// <summary>冻结单次样本的指标和只读比较结论，避免后续测试读取已释放的战斗对象。</summary>
    internal M10DeliverySample(
        double elapsedMilliseconds,
        long allocatedBytes,
        int tweenBaseline,
        int tweenCountAfter,
        bool matchesAuthoritativeTrace)
    {
        ElapsedMilliseconds = elapsedMilliseconds;
        AllocatedBytes = allocatedBytes;
        TweenBaseline = tweenBaseline;
        TweenCountAfter = tweenCountAfter;
        MatchesAuthoritativeTrace = matchesAuthoritativeTrace;
    }
}

/// <summary>使用既有 M10C Submit 回放夹具生成交付级的非持久性能和清理观测，不设置通过阈值。</summary>
internal static class M10DeliveryBaseline
{
    private static readonly int[] FrameRates = { 30, 60, 120 };

    /// <summary>每个帧率在预热后采集的固定样本数量，取奇数以获得明确中位数。</summary>
    internal const int MeasurementSampleCount = 5;

    /// <summary>采集一次完整基线窗口；每次回放仍只调用既有 M10C 的 Submit/只读事实夹具。</summary>
    internal static M10DeliveryEvidence Measure()
    {
        M10BattleReplayTrace referenceTrace = M10BattleReplayHarness.Replay(frameRate: 60);
        int tweenBaseline = DOTween.TotalActiveTweens();
        var cadences = new List<M10DeliveryCadenceEvidence>(FrameRates.Length);

        foreach (int frameRate in FrameRates)
        {
            M10BattleReplayHarness.Replay(frameRate);
            var samples = new List<M10DeliverySample>(MeasurementSampleCount);
            for (int sampleIndex = 0; sampleIndex < MeasurementSampleCount; sampleIndex++)
                samples.Add(MeasureSample(frameRate, referenceTrace));

            cadences.Add(new M10DeliveryCadenceEvidence(frameRate, samples));
        }

        return new M10DeliveryEvidence(CreateEnvironmentDescription(), tweenBaseline, cadences);
    }

    /// <summary>测量一次完整默认回放，记录当前线程分配、墙钟耗时和 Tween 清理后的只读结论。</summary>
    private static M10DeliverySample MeasureSample(int frameRate, M10BattleReplayTrace referenceTrace)
    {
        int tweenBaseline = DOTween.TotalActiveTweens();
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        Stopwatch stopwatch = Stopwatch.StartNew();
        M10BattleReplayTrace actualTrace = M10BattleReplayHarness.Replay(frameRate);
        stopwatch.Stop();
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        return new M10DeliverySample(
            stopwatch.Elapsed.TotalMilliseconds,
            allocatedBytes,
            tweenBaseline,
            DOTween.TotalActiveTweens(),
            actualTrace.Equals(referenceTrace));
    }

    /// <summary>记录本机与 Editor 采样上下文；窗口值明确标为非 Game View 性能指标。</summary>
    private static string CreateEnvironmentDescription()
    {
        Resolution desktopResolution = Screen.currentResolution;
        return string.Join(
            ";",
            $"unity={Application.unityVersion}",
            $"platform={Application.platform}",
            $"os={SystemInfo.operatingSystem}",
            $"cpu={SystemInfo.processorType}",
            $"memoryMb={SystemInfo.systemMemorySize}",
            $"gpu={SystemInfo.graphicsDeviceName}",
            $"desktop={desktopResolution.width}x{desktopResolution.height}@{desktopResolution.refreshRateRatio.value.ToString("F2", CultureInfo.InvariantCulture)}",
            "window=EditorTest_NoGameView",
            "tool=Stopwatch+GC.GetAllocatedBytesForCurrentThread+DOTween.TotalActiveTweens");
    }
}
