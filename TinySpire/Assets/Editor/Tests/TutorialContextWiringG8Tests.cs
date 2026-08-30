using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Profile;
using TinySpire.UI.Run;

public sealed class TutorialContextWiringG8Tests
{
    /// <summary>所有真实玩法页面必须投影到唯一、冻结的教程上下文。</summary>
    [TestCase(RunEntryPage.MainMenu, TutorialContext.MainMenu)]
    [TestCase(RunEntryPage.HeroSelection, TutorialContext.HeroSelection)]
    [TestCase(RunEntryPage.Map, TutorialContext.ActMap)]
    [TestCase(RunEntryPage.CardReward, TutorialContext.CardReward)]
    [TestCase(RunEntryPage.Rest, TutorialContext.NonCombatNode)]
    [TestCase(RunEntryPage.Chest, TutorialContext.NonCombatNode)]
    [TestCase(RunEntryPage.Shop, TutorialContext.NonCombatNode)]
    [TestCase(RunEntryPage.Event, TutorialContext.NonCombatNode)]
    [TestCase(RunEntryPage.Failure, TutorialContext.RunOutcome)]
    public void RunEntryPlayablePage_ObservesExpectedTutorialContext(
        RunEntryPage page,
        TutorialContext expected)
    {
        var observed = new List<TutorialContext>();
        int clearCount = 0;

        RunEntryPresenter.ObserveTutorialContextForPage(
            page,
            observed.Add,
            () => clearCount++);

        Assert.That(observed, Is.EqualTo(new[] { expected }));
        Assert.That(clearCount, Is.Zero);
    }

    /// <summary>设置、统计、图鉴与流程故障页面必须清除旧上下文且不得观察伪玩法上下文。</summary>
    [TestCase(RunEntryPage.Settings)]
    [TestCase(RunEntryPage.Statistics)]
    [TestCase(RunEntryPage.Compendium)]
    [TestCase(RunEntryPage.AbandonConfirmation)]
    [TestCase(RunEntryPage.SaveFailure)]
    [TestCase(RunEntryPage.RollbackConfirmation)]
    public void RunEntryNonGameplayPage_ClearsTutorialContextWithoutObserving(RunEntryPage page)
    {
        var observed = new List<TutorialContext>();
        int clearCount = 0;

        RunEntryPresenter.ObserveTutorialContextForPage(
            page,
            observed.Add,
            () => clearCount++);

        Assert.That(observed, Is.Empty);
        Assert.That(clearCount, Is.EqualTo(1));
    }

    /// <summary>Battle 场景生命周期入口启动时必须精确观察一次 Battle 教程上下文。</summary>
    [Test]
    public void BattleTutorialContextDriver_StartObservesBattleOnce()
    {
        var observed = new List<TutorialContext>();
        var driver = new BattleTutorialContextDriver(observed.Add);

        driver.Start();

        Assert.That(observed, Is.EqualTo(new[] { TutorialContext.Battle }));
    }

    /// <summary>Battle 教程入口必须拒绝缺失的全局观察动作。</summary>
    [Test]
    public void BattleTutorialContextDriver_RejectsMissingObserver()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BattleTutorialContextDriver((Action<TutorialContext>)null));
    }
}
