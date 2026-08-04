using System;
using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class BattleCommandPresentationRunnerTests
{
    /// <summary>确认零 cue 计划同步且精确完成一次，不创建 Tween，也不受后续控制调用影响。</summary>
    [Test]
    public void Play_NoVisibleCues_CompletesSynchronouslyExactlyOnce()
    {
        var skipped = new BattleEnemyActionSkippedSettlement(
            order: 0,
            new CombatantId(2001),
            BattleEnemyActionSkipReason.SourceNotAlive);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 1,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { skipped });
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        Func<BattleCommandPrelude, Tween> preludeFactory = _ =>
            throw new AssertionException("零 cue 计划不得创建前奏 Tween。");
        Func<BattleCommandPresentationStep, Tween> stepFactory = _ =>
            throw new AssertionException("零 cue 计划不得创建 settlement Tween。");
        using var runner = new BattleCommandPresentationRunner(preludeFactory, stepFactory);
        var completionCount = 0;

        runner.Play(plan, () => completionCount++);

        Assert.That(completionCount, Is.EqualTo(1));

        runner.Tick(1f);
        runner.CompleteImmediately();

        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认可见前奏与 settlement cue 串行进入同一手动时间线，未走完前不释放 completion。</summary>
    [Test]
    public void Play_VisibleTimeline_WaitsForManualTickAndPreservesCueOrder()
    {
        var moved = new BattleCardMovedSettlement(
            0,
            new CardInstanceId(31),
            BattleCardZone.DrawPile,
            BattleCardZone.Hand);
        var phaseChanged = new BattlePhaseChangedSettlement(
            1,
            BattleTurnPhase.BattleStart,
            BattleTurnPhase.PlayerAction,
            0,
            1,
            null,
            null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 2,
            BattleCommandType.StartBattle,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { moved, phaseChanged });
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var played = new List<string>();
        using var runner = new BattleCommandPresentationRunner(
            prelude => CreateCueTween(prelude.Kind.ToString(), played, 0.1f),
            step => CreateCueTween(step.Kind.ToString(), played, 0.1f));
        var completionCount = 0;

        runner.Play(plan, () => completionCount++);

        Assert.That(completionCount, Is.Zero);

        runner.Tick(0.25f);

        Assert.That(
            played,
            Is.EqualTo(new[]
            {
                BattleCommandPreludeKind.StartBattle.ToString(),
                BattleCommandPresentationStepKind.CardMoved.ToString(),
                BattleCommandPresentationStepKind.PlayerTurnBanner.ToString(),
            }));
        Assert.That(completionCount, Is.Zero);

        runner.Tick(0.25f);

        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认启动 readiness 由同一 runner 持有，门槛满足前不构造 cue，立即完成请求也只在就绪后释放一次。</summary>
    [Test]
    public void Play_StartGateDefersCueConstructionThenCompletesExactlyOnce()
    {
        BattleCommandPresentationPlan plan = CreateStartBattlePlan(cardValue: 23);
        var played = new List<string>();
        bool isReady = false;
        int createdCueCount = 0;
        int completionCount = 0;
        using var runner = new BattleCommandPresentationRunner(
            prelude =>
            {
                createdCueCount++;
                return CreateCueTween(prelude.Kind.ToString(), played, 0.1f);
            },
            step =>
            {
                createdCueCount++;
                return CreateCueTween(step.Kind.ToString(), played, 0.1f);
            });

        runner.Play(plan, () => completionCount++, () => isReady);
        runner.Tick(1f);
        runner.CompleteImmediately();
        runner.CompleteImmediately();

        Assert.That(createdCueCount, Is.Zero);
        Assert.That(played, Is.Empty);
        Assert.That(completionCount, Is.Zero);

        isReady = true;
        runner.Tick(0.01f);
        runner.Tick(1f);
        runner.CompleteImmediately();

        Assert.That(createdCueCount, Is.EqualTo(3));
        Assert.That(
            played,
            Is.EqualTo(new[]
            {
                BattleCommandPreludeKind.StartBattle.ToString(),
                BattleCommandPresentationStepKind.CardMoved.ToString(),
                BattleCommandPresentationStepKind.PlayerTurnBanner.ToString(),
            }));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认 readiness 等待期间销毁 runner 会丢弃 completion，之后即使门槛满足也不创建迟到 cue。</summary>
    [Test]
    public void Dispose_StartGatePending_DropsCompletionWithoutLateCue()
    {
        BattleCommandPresentationPlan plan = CreateStartBattlePlan(cardValue: 29);
        bool isReady = false;
        int createdCueCount = 0;
        int completionCount = 0;
        var runner = new BattleCommandPresentationRunner(
            _ =>
            {
                createdCueCount++;
                return CreateCleanupTrackedCueTween(() => { });
            },
            _ =>
            {
                createdCueCount++;
                return CreateCleanupTrackedCueTween(() => { });
            });

        runner.Play(plan, () => completionCount++, () => isReady);
        runner.Dispose();
        runner.Dispose();
        isReady = true;
        runner.Tick(1f);
        runner.CompleteImmediately();

        Assert.That(createdCueCount, Is.Zero);
        Assert.That(completionCount, Is.Zero);
    }

    /// <summary>确认加速只放大手动时间增量，不改变前奏与 settlement cue 的顺序或 completion 次数。</summary>
    [Test]
    public void Play_AcceleratedTimeline_PreservesCueOrderAndCompletesOnce()
    {
        var moved = new BattleCardMovedSettlement(
            0,
            new CardInstanceId(32),
            BattleCardZone.DrawPile,
            BattleCardZone.Hand);
        var phaseChanged = new BattlePhaseChangedSettlement(
            1,
            BattleTurnPhase.BattleStart,
            BattleTurnPhase.PlayerAction,
            0,
            1,
            null,
            null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 3,
            BattleCommandType.StartBattle,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { moved, phaseChanged });
        BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
        var played = new List<string>();
        using var runner = new BattleCommandPresentationRunner(
            prelude => CreateCueTween(prelude.Kind.ToString(), played, 0.1f),
            step => CreateCueTween(step.Kind.ToString(), played, 0.1f));
        var completionCount = 0;

        runner.SetSpeed(2f);
        runner.Play(plan, () => completionCount++);
        runner.Tick(0.16f);
        runner.Tick(1f);

        Assert.That(
            played,
            Is.EqualTo(new[]
            {
                BattleCommandPreludeKind.StartBattle.ToString(),
                BattleCommandPresentationStepKind.CardMoved.ToString(),
                BattleCommandPresentationStepKind.PlayerTurnBanner.ToString(),
            }));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认定向快进只接受当前精确 cue，旧 cue、未来 cue 与重复请求均保持无效。</summary>
    [Test]
    public void TryCompleteCurrentCue_FastForwardsOnlyExactActiveCueOnce()
    {
        BattleCommandPresentationPlan plan = CreateStartBattlePlan(cardValue: 321);
        var cues = new List<BattleCommandPresentationTween>();
        var started = new List<string>();
        var finished = new List<string>();
        var completionCount = 0;

        // 为每个计划步骤创建可追踪的独立 cue，锁定精确引用与时间区间语义。
        BattleCommandPresentationTween CreateTrackedCue(string cueName)
        {
            var cue = new BattleCommandPresentationTween(
                DOTween.Sequence()
                    .AppendCallback(() => started.Add(cueName))
                    .AppendInterval(1f)
                    .AppendCallback(() => finished.Add(cueName)),
                cleanup: null);
            cues.Add(cue);
            return cue;
        }

        using var runner = new BattleCommandPresentationRunner(
            prelude => CreateTrackedCue(prelude.Kind.ToString()),
            step => CreateTrackedCue(step.Kind.ToString()));

        runner.Play(plan, () => completionCount++);

        Assert.That(cues, Has.Count.EqualTo(3));
        Assert.That(runner.TryCompleteCue(cues[1]), Is.False, "未来 cue 不得被抢先完成。");

        runner.Tick(0.25f);

        Assert.That(started, Does.Contain(BattleCommandPreludeKind.StartBattle.ToString()));
        Assert.That(runner.TryCompleteCue(cues[1]), Is.False, "当前前奏播放时不得快进下一条 settlement cue。");
        Assert.That(runner.TryCompleteCue(cues[0]), Is.True);
        Assert.That(runner.TryCompleteCue(cues[0]), Is.False, "同一 cue 的快进请求只能成功一次。");
        Assert.That(finished, Does.Contain(BattleCommandPreludeKind.StartBattle.ToString()));
        int startedCountAfterPrelude = started.Count;
        Assert.That(runner.TryCompleteCue(cues[1]), Is.False, "只到达边界但尚未推进的下一条 cue 不算活动 cue。");
        Assert.That(started, Has.Count.EqualTo(startedCountAfterPrelude));
        Assert.That(completionCount, Is.Zero);

        runner.Tick(0.1f);

        Assert.That(started, Does.Contain(BattleCommandPresentationStepKind.CardMoved.ToString()));
        Assert.That(runner.TryCompleteCue(cues[0]), Is.False, "旧 cue 不得影响当前时间线。");
        Assert.That(runner.TryCompleteCue(cues[2]), Is.False, "未来 cue 不得被抢先完成。");
        Assert.That(runner.TryCompleteCue(cues[1]), Is.True);
        Assert.That(runner.TryCompleteCue(cues[1]), Is.False);
        Assert.That(finished, Does.Contain(BattleCommandPresentationStepKind.CardMoved.ToString()));
        Assert.That(completionCount, Is.Zero);

        runner.Tick(0.1f);

        Assert.That(started, Does.Contain(BattleCommandPresentationStepKind.PlayerTurnBanner.ToString()));
        Assert.That(runner.TryCompleteCue(cues[2]), Is.True);
        Assert.That(runner.TryCompleteCue(cues[2]), Is.False);
        Assert.That(completionCount, Is.EqualTo(1));

        runner.CompleteImmediately();
        runner.CompleteImmediately();
        runner.Tick(1f);

        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认立即完成执行全部剩余 cue，并在 OnComplete 与兜底路径同时触发时仍只完成一次。</summary>
    [Test]
    public void CompleteImmediately_RunsRemainingCuesAndCompletesExactlyOnce()
    {
        BattleCommandPresentationPlan plan = CreateStartBattlePlan(cardValue: 33);
        var played = new List<string>();
        using var runner = new BattleCommandPresentationRunner(
            prelude => CreateCueTween(prelude.Kind.ToString(), played, 0.1f),
            step => CreateCueTween(step.Kind.ToString(), played, 0.1f));
        var completionCount = 0;

        runner.Play(plan, () => completionCount++);
        runner.CompleteImmediately();
        runner.CompleteImmediately();
        runner.Tick(1f);

        Assert.That(
            played,
            Is.EqualTo(new[]
            {
                BattleCommandPreludeKind.StartBattle.ToString(),
                BattleCommandPresentationStepKind.CardMoved.ToString(),
                BattleCommandPresentationStepKind.PlayerTurnBanner.ToString(),
            }));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认自然完成会精确清理全部 cue lease，后续控制与销毁不会重复清理或完成。</summary>
    [Test]
    public void Play_NaturalCompletion_CleansAllCueLeasesExactlyOnceAndIgnoresLaterControls()
    {
        BattleCommandPresentationPlan plan = CreateStartBattlePlan(cardValue: 331);
        var cleanedCueCount = 0;
        var completionCount = 0;
        var runner = new BattleCommandPresentationRunner(
            _ => CreateCleanupTrackedCueTween(() => cleanedCueCount++),
            _ => CreateCleanupTrackedCueTween(() => cleanedCueCount++));

        runner.Play(plan, () => completionCount++);
        runner.Tick(10f);
        runner.CompleteImmediately();
        runner.Tick(10f);
        runner.Dispose();
        runner.Dispose();

        Assert.That(cleanedCueCount, Is.EqualTo(3));
        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认立即完成会精确清理全部 cue lease，重复立即完成与后续推进保持幂等。</summary>
    [Test]
    public void CompleteImmediately_CleansAllCueLeasesExactlyOnce()
    {
        BattleCommandPresentationPlan plan = CreateStartBattlePlan(cardValue: 332);
        var cleanedCueCount = 0;
        var completionCount = 0;
        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        var runner = new BattleCommandPresentationRunner(
            _ => CreateCleanupTrackedCueTween(() => cleanedCueCount++),
            _ => CreateCleanupTrackedCueTween(() => cleanedCueCount++));

        runner.Play(plan, () => completionCount++);
        runner.CompleteImmediately();
        runner.CompleteImmediately();
        runner.Tick(10f);
        runner.Dispose();

        Assert.That(cleanedCueCount, Is.EqualTo(3));
        Assert.That(completionCount, Is.EqualTo(1));
        Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
    }

    /// <summary>确认 completion 重入播放下一计划前，runner 已释放旧计划所有权。</summary>
    [Test]
    public void Completion_ReentrantPlay_ClearsOldOwnershipBeforeNextPlan()
    {
        BattleCommandPresentationPlan firstPlan = CreateStartBattlePlan(cardValue: 34);
        BattleCommandPresentationPlan secondPlan = CreateStartBattlePlan(cardValue: 35);
        using var runner = new BattleCommandPresentationRunner(
            _ => CreateCueTween("prelude", new List<string>(), 0.01f),
            _ => CreateCueTween("step", new List<string>(), 0.01f));
        var firstCompletionCount = 0;
        var secondCompletionCount = 0;

        runner.Play(firstPlan, () =>
        {
            firstCompletionCount++;
            runner.Play(secondPlan, () => secondCompletionCount++);
        });
        runner.Tick(1f);

        Assert.That(firstCompletionCount, Is.EqualTo(1));
        Assert.That(secondCompletionCount, Is.Zero);

        runner.Tick(1f);

        Assert.That(secondCompletionCount, Is.EqualTo(1));
    }

    /// <summary>确认活动计划期间拒绝替换，但原 completion 与时间线仍可正常完成。</summary>
    [Test]
    public void Play_WhileActive_ThrowsWithoutReplacingOriginalCompletion()
    {
        BattleCommandPresentationPlan firstPlan = CreateStartBattlePlan(cardValue: 36);
        BattleCommandPresentationPlan secondPlan = CreateStartBattlePlan(cardValue: 37);
        using var runner = new BattleCommandPresentationRunner(
            _ => DOTween.Sequence().AppendInterval(0.01f),
            _ => DOTween.Sequence().AppendInterval(0.01f));
        var firstCompletionCount = 0;
        var replacementCompletionCount = 0;

        runner.Play(firstPlan, () => firstCompletionCount++);

        Assert.Throws<InvalidOperationException>(
            () => runner.Play(secondPlan, () => replacementCompletionCount++));

        runner.Tick(1f);

        Assert.That(firstCompletionCount, Is.EqualTo(1));
        Assert.That(replacementCompletionCount, Is.Zero);
    }

    /// <summary>确认 owner 销毁只 Kill 当前父时间线并丢弃旧 completion，后续 Tick 与立即完成均无迟到回调。</summary>
    [Test]
    public void Dispose_ActiveTimeline_KillsOwnedTweensAndDropsCompletion()
    {
        BattleCommandPresentationPlan plan = CreateStartBattlePlan(cardValue: 38);
        var cleanedCueCount = 0;
        var completionCount = 0;
        var runner = new BattleCommandPresentationRunner(
            _ => CreateCleanupTrackedCueTween(() => cleanedCueCount++),
            _ => CreateCleanupTrackedCueTween(() => cleanedCueCount++));

        runner.Play(plan, () => completionCount++);
        runner.Dispose();
        runner.Dispose();
        runner.CompleteImmediately();
        runner.Tick(1f);

        Assert.That(cleanedCueCount, Is.EqualTo(3));
        Assert.That(completionCount, Is.Zero);
    }

    /// <summary>确认同帧销毁活动时间线会立刻释放命令级父 Sequence，不给旧 Scope 留下迟到 Tween。</summary>
    [Test]
    public void Dispose_ActiveTimeline_ReleasesParentSequenceInSameFrame()
    {
        BattleCommandPresentationPlan plan = CreateStartBattlePlan(cardValue: 381);
        var runner = new BattleCommandPresentationRunner(
            _ => CreateCleanupTrackedCueTween(() => { }),
            _ => CreateCleanupTrackedCueTween(() => { }));
        int activeTweenCountBefore = DOTween.TotalActiveTweens();

        runner.Play(plan, () => { });

        Assert.That(DOTween.TotalActiveTweens(), Is.GreaterThan(activeTweenCountBefore));

        runner.Dispose();

        Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
    }

    /// <summary>确认时间线同步构建失败会清理已创建部分、原样抛错且不占用 runner。</summary>
    [Test]
    public void Play_TimelineBuildThrows_KillsPartialAndRethrowsWithoutCompletion()
    {
        BattleCommandPresentationPlan visiblePlan = CreateStartBattlePlan(cardValue: 39);
        BattleCommandPresentationPlan zeroCuePlan = CreateZeroCuePlan();
        var cleanedCueCount = 0;
        var completionCount = 0;
        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        using var runner = new BattleCommandPresentationRunner(
            _ => CreateCleanupTrackedCueTween(() => cleanedCueCount++),
            _ => throw new InvalidOperationException("timeline-build-fault"));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => runner.Play(visiblePlan, () => completionCount++));

        Assert.That(exception.Message, Is.EqualTo("timeline-build-fault"));
        Assert.That(cleanedCueCount, Is.EqualTo(1));
        Assert.That(completionCount, Is.Zero);
        Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));

        runner.Play(zeroCuePlan, () => completionCount++);

        Assert.That(completionCount, Is.EqualTo(1));
    }

    /// <summary>确认两个 runner 的播放级标识互不干扰，销毁一方不会误杀另一方父时间线。</summary>
    [Test]
    public void Dispose_FirstOfTwoActiveRunners_DoesNotKillSecond()
    {
        BattleCommandPresentationPlan firstPlan = CreateStartBattlePlan(cardValue: 391);
        BattleCommandPresentationPlan secondPlan = CreateStartBattlePlan(cardValue: 392);
        int activeTweenCountBefore = DOTween.TotalActiveTweens();
        var firstRunner = new BattleCommandPresentationRunner(
            _ => CreateCleanupTrackedCueTween(() => { }),
            _ => CreateCleanupTrackedCueTween(() => { }));
        var secondRunner = new BattleCommandPresentationRunner(
            _ => CreateCleanupTrackedCueTween(() => { }),
            _ => CreateCleanupTrackedCueTween(() => { }));
        var firstCompletionCount = 0;
        var secondCompletionCount = 0;

        firstRunner.Play(firstPlan, () => firstCompletionCount++);
        secondRunner.Play(secondPlan, () => secondCompletionCount++);

        Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore + 2));

        firstRunner.Dispose();

        Assert.That(firstCompletionCount, Is.Zero);
        Assert.That(secondCompletionCount, Is.Zero);
        Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore + 1));

        secondRunner.Tick(10f);
        secondRunner.Dispose();

        Assert.That(secondCompletionCount, Is.EqualTo(1));
        Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
    }

    /// <summary>建立一个包含 StartBattle 前奏、抽牌和玩家回合横幅的三 cue 计划。</summary>
    private static BattleCommandPresentationPlan CreateStartBattlePlan(int cardValue)
    {
        var moved = new BattleCardMovedSettlement(
            0,
            new CardInstanceId(cardValue),
            BattleCardZone.DrawPile,
            BattleCardZone.Hand);
        var phaseChanged = new BattlePhaseChangedSettlement(
            1,
            BattleTurnPhase.BattleStart,
            BattleTurnPhase.PlayerAction,
            0,
            1,
            null,
            null);
        var result = new BattleCommandExecutionResult(
            authoritySequence: cardValue,
            BattleCommandType.StartBattle,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { moved, phaseChanged });
        return BattleCommandPresentationPlan.Create(result);
    }

    /// <summary>建立一个只有明确 skip entry、没有任何可见 cue 的计划。</summary>
    private static BattleCommandPresentationPlan CreateZeroCuePlan()
    {
        var skipped = new BattleEnemyActionSkippedSettlement(
            0,
            new CombatantId(2001),
            BattleEnemyActionSkipReason.SourceNotAlive);
        var result = new BattleCommandExecutionResult(
            authoritySequence: 100,
            BattleCommandType.CompleteEnemyAction,
            submitterId: null,
            BattleCommandExecutionFailureReason.None,
            new BattleSettlementRecord[] { skipped });
        return BattleCommandPresentationPlan.Create(result);
    }

    /// <summary>创建一个由 runner 明确收口 cleanup 的 concrete 子 Tween lease。</summary>
    private static BattleCommandPresentationTween CreateCleanupTrackedCueTween(Action onCleaned)
    {
        return new BattleCommandPresentationTween(
            DOTween.Sequence().AppendInterval(1f),
            onCleaned);
    }

    /// <summary>创建一个仅用于 runner 顺序证据的手动子 Tween。</summary>
    private static Tween CreateCueTween(
        string cueName,
        ICollection<string> played,
        float durationSeconds)
    {
        return DOTween.Sequence()
            .AppendCallback(() => played.Add(cueName))
            .AppendInterval(durationSeconds);
    }
}
