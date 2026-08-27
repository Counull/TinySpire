using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Run;

/// <summary>验证 G5/G6 共用的 Run 持有物不可变领域事实。</summary>
public sealed class RunHoldingsTests
{
    /// <summary>新 Run 默认持有一百金币，且没有遗物或药水。</summary>
    [Test]
    public void Empty_WithDefaultGold_CreatesEmptyHoldings()
    {
        RunHoldings holdings = RunHoldings.Empty();

        Assert.That(holdings.Gold, Is.EqualTo(100));
        Assert.That(holdings.Relics, Is.Empty);
        Assert.That(holdings.Potions, Is.Empty);
    }

    /// <summary>构造持有物时冻结遗物顺序，并保留调用方给出的稳定实例身份。</summary>
    [Test]
    public void Constructor_FreezesRelicOrderAndStableInstances()
    {
        var source = new[]
        {
            new RunRelic(new RunRelicInstanceId(4), templateId: 5101),
            new RunRelic(new RunRelicInstanceId(9), templateId: 5102),
        };
        var holdings = new RunHoldings(source, Array.Empty<RunPotion>(), gold: 25);

        source[0] = new RunRelic(new RunRelicInstanceId(99), templateId: 5999);

        Assert.That(holdings.Relics.Count, Is.EqualTo(2));
        Assert.That(holdings.Relics[0].InstanceId, Is.EqualTo(new RunRelicInstanceId(4)));
        Assert.That(holdings.Relics[0].TemplateId, Is.EqualTo(5101));
        Assert.That(holdings.Relics[1].InstanceId, Is.EqualTo(new RunRelicInstanceId(9)));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RunRelic>)holdings.Relics).Add(
                new RunRelic(new RunRelicInstanceId(10), templateId: 5103)));
    }

    /// <summary>遗物与药水实例都拒绝默认身份和非正模板引用。</summary>
    [Test]
    public void ItemConstructors_WithInvalidIdentityOrTemplate_AreRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new RunRelic(default, templateId: 5101));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RunRelic(new RunRelicInstanceId(1), templateId: 0));
        Assert.Throws<ArgumentException>(() =>
            new RunPotion(default, templateId: 5201));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RunPotion(new RunPotionInstanceId(1), templateId: -1));
    }

    /// <summary>完整快照拒绝重复身份、重复遗物模板、超出槽位和负金币。</summary>
    [Test]
    public void Constructor_WithBrokenAggregateInvariants_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new RunHoldings(
            new[]
            {
                new RunRelic(new RunRelicInstanceId(1), templateId: 5101),
                new RunRelic(new RunRelicInstanceId(1), templateId: 5102),
            },
            Array.Empty<RunPotion>(),
            gold: 0));
        Assert.Throws<ArgumentException>(() => new RunHoldings(
            new[]
            {
                new RunRelic(new RunRelicInstanceId(1), templateId: 5101),
                new RunRelic(new RunRelicInstanceId(2), templateId: 5101),
            },
            Array.Empty<RunPotion>(),
            gold: 0));
        Assert.Throws<ArgumentException>(() => new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(1), templateId: 5201),
                new RunPotion(new RunPotionInstanceId(1), templateId: 5201),
            },
            gold: 0));
        Assert.Throws<ArgumentException>(() => new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(1), templateId: 5201),
                new RunPotion(new RunPotionInstanceId(2), templateId: 5201),
                new RunPotion(new RunPotionInstanceId(3), templateId: 5202),
                new RunPotion(new RunPotionInstanceId(4), templateId: 5203),
            },
            gold: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RunHoldings.Empty(initialGold: -1));
    }

    /// <summary>药水按槽位顺序冻结且允许同模板副本，遗物与药水身份序号彼此独立。</summary>
    [Test]
    public void Constructor_FreezesPotionOrderAndAllowsSameTemplateCopies()
    {
        var source = new[]
        {
            new RunPotion(new RunPotionInstanceId(1), templateId: 5201),
            new RunPotion(new RunPotionInstanceId(7), templateId: 5201),
        };
        var holdings = new RunHoldings(
            new[] { new RunRelic(new RunRelicInstanceId(1), templateId: 5101) },
            source,
            gold: 40);

        source[0] = new RunPotion(new RunPotionInstanceId(99), templateId: 5999);

        Assert.That(holdings.Relics[0].InstanceId.Sequence, Is.EqualTo(1));
        Assert.That(holdings.Potions[0].InstanceId.Sequence, Is.EqualTo(1));
        Assert.That(holdings.Potions[0].TemplateId, Is.EqualTo(5201));
        Assert.That(holdings.Potions[1].InstanceId, Is.EqualTo(new RunPotionInstanceId(7)));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RunPotion>)holdings.Potions).Add(
                new RunPotion(new RunPotionInstanceId(8), templateId: 5202)));
    }

    /// <summary>获得遗物会在末尾创建当前最大序号加一的新实例，并保持旧快照不变。</summary>
    [Test]
    public void AddRelic_WithNewTemplate_AppendsStableInstanceToNewSnapshot()
    {
        var original = new RunHoldings(
            new[]
            {
                new RunRelic(new RunRelicInstanceId(2), templateId: 5101),
                new RunRelic(new RunRelicInstanceId(8), templateId: 5102),
            },
            new[] { new RunPotion(new RunPotionInstanceId(3), templateId: 5201) },
            gold: 75);

        RunHoldings next = original.AddRelic(templateId: 5103);

        Assert.That(next.Relics.Count, Is.EqualTo(3));
        Assert.That(next.Relics[2].InstanceId, Is.EqualTo(new RunRelicInstanceId(9)));
        Assert.That(next.Relics[2].TemplateId, Is.EqualTo(5103));
        Assert.That(next.Potions[0].InstanceId, Is.EqualTo(new RunPotionInstanceId(3)));
        Assert.That(next.Gold, Is.EqualTo(75));
        Assert.That(original.Relics.Count, Is.EqualTo(2));
    }

    /// <summary>空持有物中的遗物与药水身份各自从一开始分配。</summary>
    [Test]
    public void AddItems_ToEmptyHoldings_StartEachIndependentIdentityAtOne()
    {
        RunHoldings empty = RunHoldings.Empty();

        RunHoldings withRelic = empty.AddRelic(templateId: 5101);
        RunHoldings withPotion = empty.AddPotion(templateId: 5201);

        Assert.That(withRelic.Relics[0].InstanceId, Is.EqualTo(new RunRelicInstanceId(1)));
        Assert.That(withPotion.Potions[0].InstanceId, Is.EqualTo(new RunPotionInstanceId(1)));
        Assert.That(empty.Relics, Is.Empty);
        Assert.That(empty.Potions, Is.Empty);
    }

    /// <summary>无效模板、重复模板和身份溢出均在产生遗物后继前失败。</summary>
    [Test]
    public void AddRelic_WithInvalidDuplicateOrOverflowingInput_IsRejectedAtomically()
    {
        var original = new RunHoldings(
            new[]
            {
                new RunRelic(new RunRelicInstanceId(int.MaxValue), templateId: 5101),
            },
            Array.Empty<RunPotion>(),
            gold: 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => original.AddRelic(templateId: 0));
        Assert.Throws<InvalidOperationException>(() => original.AddRelic(templateId: 5101));
        Assert.Throws<OverflowException>(() => original.AddRelic(templateId: 5102));
        Assert.That(original.Relics.Count, Is.EqualTo(1));
        Assert.That(original.Relics[0].InstanceId.Sequence, Is.EqualTo(int.MaxValue));
        Assert.That(original.Gold, Is.EqualTo(100));
    }

    /// <summary>获得药水会按最大序号加一追加新槽位，并允许同模板副本。</summary>
    [Test]
    public void AddPotion_WithAvailableSlot_AppendsIndependentInstanceToNewSnapshot()
    {
        var original = new RunHoldings(
            new[] { new RunRelic(new RunRelicInstanceId(20), templateId: 5101) },
            new[] { new RunPotion(new RunPotionInstanceId(6), templateId: 5201) },
            gold: 45);

        RunHoldings next = original.AddPotion(templateId: 5201);

        Assert.That(next.Potions.Count, Is.EqualTo(2));
        Assert.That(next.Potions[0].InstanceId, Is.EqualTo(new RunPotionInstanceId(6)));
        Assert.That(next.Potions[1].InstanceId, Is.EqualTo(new RunPotionInstanceId(7)));
        Assert.That(next.Potions[1].TemplateId, Is.EqualTo(5201));
        Assert.That(next.Relics[0].InstanceId.Sequence, Is.EqualTo(20));
        Assert.That(next.Gold, Is.EqualTo(45));
        Assert.That(original.Potions.Count, Is.EqualTo(1));
    }

    /// <summary>无效模板、满槽和身份溢出均在产生药水后继前失败。</summary>
    [Test]
    public void AddPotion_WithInvalidFullOrOverflowingInput_IsRejectedAtomically()
    {
        var full = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(1), templateId: 5201),
                new RunPotion(new RunPotionInstanceId(2), templateId: 5201),
                new RunPotion(new RunPotionInstanceId(3), templateId: 5202),
            },
            gold: 100);
        var overflowing = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[] { new RunPotion(new RunPotionInstanceId(int.MaxValue), templateId: 5201) },
            gold: 100);

        Assert.Throws<ArgumentOutOfRangeException>(() => full.AddPotion(templateId: 0));
        Assert.Throws<InvalidOperationException>(() => full.AddPotion(templateId: 5203));
        Assert.Throws<OverflowException>(() => overflowing.AddPotion(templateId: 5202));
        Assert.That(full.Potions.Count, Is.EqualTo(3));
        Assert.That(overflowing.Potions[0].InstanceId.Sequence, Is.EqualTo(int.MaxValue));
    }

    /// <summary>移除药水只删除指定稳定实例并压紧后续槽位，旧快照保持不变。</summary>
    [Test]
    public void RemovePotion_WithOwnedInstance_RemovesOnlyRequestedPotion()
    {
        var original = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[]
            {
                new RunPotion(new RunPotionInstanceId(3), templateId: 5201),
                new RunPotion(new RunPotionInstanceId(6), templateId: 5201),
                new RunPotion(new RunPotionInstanceId(8), templateId: 5202),
            },
            gold: 55);

        RunHoldings next = original.RemovePotion(new RunPotionInstanceId(6));

        Assert.That(next.Potions.Count, Is.EqualTo(2));
        Assert.That(next.Potions[0].InstanceId, Is.EqualTo(new RunPotionInstanceId(3)));
        Assert.That(next.Potions[1].InstanceId, Is.EqualTo(new RunPotionInstanceId(8)));
        Assert.That(next.Gold, Is.EqualTo(55));
        Assert.That(original.Potions.Count, Is.EqualTo(3));
        Assert.That(original.Potions[1].TemplateId, Is.EqualTo(5201));
    }

    /// <summary>默认或未持有的药水身份不得产生删除后继。</summary>
    [Test]
    public void RemovePotion_WithInvalidOrMissingIdentity_IsRejectedAtomically()
    {
        var original = new RunHoldings(
            Array.Empty<RunRelic>(),
            new[] { new RunPotion(new RunPotionInstanceId(4), templateId: 5201) },
            gold: 100);

        Assert.Throws<ArgumentException>(() => original.RemovePotion(default));
        Assert.Throws<InvalidOperationException>(() =>
            original.RemovePotion(new RunPotionInstanceId(5)));
        Assert.That(original.Potions.Count, Is.EqualTo(1));
        Assert.That(original.Potions[0].InstanceId, Is.EqualTo(new RunPotionInstanceId(4)));
    }

    /// <summary>获得正数金币只改变新快照的余额，并保留全部实例事实。</summary>
    [Test]
    public void GainGold_WithPositiveAmount_IncreasesOnlyNewSnapshotBalance()
    {
        var original = new RunHoldings(
            new[] { new RunRelic(new RunRelicInstanceId(1), templateId: 5101) },
            new[] { new RunPotion(new RunPotionInstanceId(1), templateId: 5201) },
            gold: 80);

        RunHoldings next = original.GainGold(amount: 25);

        Assert.That(next.Gold, Is.EqualTo(105));
        Assert.That(next.Relics[0].InstanceId, Is.EqualTo(new RunRelicInstanceId(1)));
        Assert.That(next.Potions[0].InstanceId, Is.EqualTo(new RunPotionInstanceId(1)));
        Assert.That(original.Gold, Is.EqualTo(80));
    }

    /// <summary>非正获得量与金币溢出均不得产生新余额事实。</summary>
    [Test]
    public void GainGold_WithInvalidOrOverflowingAmount_IsRejectedAtomically()
    {
        RunHoldings original = RunHoldings.Empty(initialGold: int.MaxValue);

        Assert.Throws<ArgumentOutOfRangeException>(() => original.GainGold(amount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.GainGold(amount: -1));
        Assert.Throws<OverflowException>(() => original.GainGold(amount: 1));
        Assert.That(original.Gold, Is.EqualTo(int.MaxValue));
    }

    /// <summary>花费不超过余额的正数金币可精确降至零，旧快照保持不变。</summary>
    [Test]
    public void SpendGold_WithAffordableAmount_DecreasesOnlyNewSnapshotBalance()
    {
        RunHoldings original = RunHoldings.Empty(initialGold: 60);

        RunHoldings partial = original.SpendGold(amount: 25);
        RunHoldings exhausted = original.SpendGold(amount: 60);

        Assert.That(partial.Gold, Is.EqualTo(35));
        Assert.That(exhausted.Gold, Is.Zero);
        Assert.That(original.Gold, Is.EqualTo(60));
    }

    /// <summary>非正花费量与余额不足均不得产生新余额事实。</summary>
    [Test]
    public void SpendGold_WithInvalidOrUnaffordableAmount_IsRejectedAtomically()
    {
        RunHoldings original = RunHoldings.Empty(initialGold: 40);

        Assert.Throws<ArgumentOutOfRangeException>(() => original.SpendGold(amount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => original.SpendGold(amount: -1));
        Assert.Throws<InvalidOperationException>(() => original.SpendGold(amount: 41));
        Assert.That(original.Gold, Is.EqualTo(40));
    }
}
