using System.Collections.Generic;
using DG.Tweening;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;

public sealed class BattleFlowFeedbackTweenFactoryTests
{
    private readonly object _testTweenId = new object();

    /// <summary>精确注销定向测试直接创建但没有交给 runner 的 Tween。</summary>
    [TearDown]
    public void TearDown()
    {
        DOTween.Kill(_testTweenId, complete: false);
    }

    /// <summary>确认 StartBattle 前奏只建立一次正式本地化覆盖层并局部阻断系统指针。</summary>
    [Test]
    public void TryCreate_StartBattlePrelude_UsesBlockingLocalizedOverlayWithoutReadingOutcome()
    {
        var captured = new List<BattleFlowFeedbackCue>();
        int outcomeReadCount = 0;
        var factory = new BattleFlowFeedbackTweenFactory(
            cue =>
            {
                captured.Add(cue);
                return CreateTestTween();
            },
            () =>
            {
                outcomeReadCount++;
                return "battle.ui.result.victory";
            });

        bool created = factory.TryCreate(
            new BattleCommandPrelude(BattleCommandPreludeKind.StartBattle),
            out BattleCommandPresentationTween tween);

        Assert.That(created, Is.True);
        Assert.That(tween, Is.Not.Null);
        Assert.That(captured, Has.Count.EqualTo(1));
        Assert.That(captured[0].Kind, Is.EqualTo(BattleFlowFeedbackCueKind.BattleStartOverlay));
        Assert.That(captured[0].LocalizationKey, Is.EqualTo("battle.ui.battle.start"));
        Assert.That(captured[0].BlocksSystemPointer, Is.True);
        Assert.That(outcomeReadCount, Is.Zero);
    }

    /// <summary>确认玩家与敌人横幅使用正式键且不形成覆盖层指针锁，也不提前读取终局。</summary>
    [Test]
    public void TryCreate_PlayerAndEnemyTurnSteps_UseTransientLocalizedBannersWithoutReadingOutcome()
    {
        var captured = new List<BattleFlowFeedbackCue>();
        int outcomeReadCount = 0;
        var factory = new BattleFlowFeedbackTweenFactory(
            cue =>
            {
                captured.Add(cue);
                return CreateTestTween();
            },
            () =>
            {
                outcomeReadCount++;
                return "battle.ui.result.victory";
            });
        var playerPhase = new BattlePhaseChangedSettlement(
            order: 0,
            BattleTurnPhase.BattleStart,
            BattleTurnPhase.PlayerAction,
            roundNumberBefore: 0,
            roundNumberAfter: 1,
            currentActingEnemyIdBefore: null,
            currentActingEnemyIdAfter: null);
        var enemyPhase = new BattlePhaseChangedSettlement(
            order: 1,
            BattleTurnPhase.PlayerAction,
            BattleTurnPhase.EnemyAction,
            roundNumberBefore: 1,
            roundNumberAfter: 1,
            currentActingEnemyIdBefore: null,
            currentActingEnemyIdAfter: new CombatantId(2001));

        Assert.That(
            factory.TryCreate(
                new BattleCommandPresentationStep(
                    BattleCommandPresentationStepKind.PlayerTurnBanner,
                    playerPhase,
                    substepIndex: 0),
                out BattleCommandPresentationTween playerTween),
            Is.True);
        Assert.That(playerTween, Is.Not.Null);
        Assert.That(
            factory.TryCreate(
                new BattleCommandPresentationStep(
                    BattleCommandPresentationStepKind.EnemyTurnBanner,
                    enemyPhase,
                    substepIndex: 0),
                out BattleCommandPresentationTween enemyTween),
            Is.True);
        Assert.That(enemyTween, Is.Not.Null);

        Assert.That(captured, Has.Count.EqualTo(2));
        Assert.That(captured[0].Kind, Is.EqualTo(BattleFlowFeedbackCueKind.PlayerTurnBanner));
        Assert.That(captured[0].LocalizationKey, Is.EqualTo("battle.ui.turn.player"));
        Assert.That(captured[0].BlocksSystemPointer, Is.False);
        Assert.That(captured[1].Kind, Is.EqualTo(BattleFlowFeedbackCueKind.EnemyTurnBanner));
        Assert.That(captured[1].LocalizationKey, Is.EqualTo("battle.ui.turn.enemy"));
        Assert.That(captured[1].BlocksSystemPointer, Is.False);
        Assert.That(outcomeReadCount, Is.Zero);
    }

    /// <summary>确认终局步骤只在构造该步骤时读取一次 adapter 已映射的胜负文案键。</summary>
    [TestCase("battle.ui.result.victory")]
    [TestCase("battle.ui.result.defeat")]
    public void TryCreate_BattleOutcomeStep_ReadsMappedLocalizationKeyExactlyOnce(
        string outcomeLocalizationKey)
    {
        var captured = new List<BattleFlowFeedbackCue>();
        int outcomeReadCount = 0;
        var factory = new BattleFlowFeedbackTweenFactory(
            cue =>
            {
                captured.Add(cue);
                return CreateTestTween();
            },
            () =>
            {
                outcomeReadCount++;
                return outcomeLocalizationKey;
            });
        var battleEnded = new BattlePhaseChangedSettlement(
            order: 0,
            BattleTurnPhase.EnemyAction,
            BattleTurnPhase.BattleEnded,
            roundNumberBefore: 3,
            roundNumberAfter: 3,
            currentActingEnemyIdBefore: new CombatantId(2001),
            currentActingEnemyIdAfter: null);

        bool created = factory.TryCreate(
            new BattleCommandPresentationStep(
                BattleCommandPresentationStepKind.BattleOutcome,
                battleEnded,
                substepIndex: 0),
            out BattleCommandPresentationTween tween);

        Assert.That(created, Is.True);
        Assert.That(tween, Is.Not.Null);
        Assert.That(captured, Has.Count.EqualTo(1));
        Assert.That(captured[0].Kind, Is.EqualTo(BattleFlowFeedbackCueKind.BattleOutcome));
        Assert.That(captured[0].LocalizationKey, Is.EqualTo(outcomeLocalizationKey));
        Assert.That(captured[0].BlocksSystemPointer, Is.True);
        Assert.That(outcomeReadCount, Is.EqualTo(1));
    }

    /// <summary>建立由测试夹具独占的最小 cue Tween。</summary>
    private BattleCommandPresentationTween CreateTestTween()
    {
        Sequence sequence = DOTween.Sequence()
            .SetId(_testTweenId)
            .SetAutoKill(false)
            .Pause()
            .AppendCallback(() => { });
        return new BattleCommandPresentationTween(sequence, cleanup: null);
    }
}
