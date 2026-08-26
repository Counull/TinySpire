using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinySpire.Run;

/// <summary>验证 G4 的 Run-owned 有序牌组与稳定实例身份。</summary>
public sealed class RunDeckG4Tests
{
    /// <summary>构造牌组时冻结输入顺序，并把同模板副本保留为不同 Run 实例。</summary>
    [Test]
    public void Constructor_FreezesOrderAndKeepsSameTemplateCopiesDistinct()
    {
        var source = new[]
        {
            new RunCard(new RunCardInstanceId(1), templateId: 3002, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(2), templateId: 3002, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(3), templateId: 3003, upgradeLevel: 1),
        };

        var deck = new RunDeck(source);
        source[0] = new RunCard(new RunCardInstanceId(99), templateId: 9999, upgradeLevel: 9);

        Assert.That(deck.Cards.Count, Is.EqualTo(3));
        Assert.That(deck.Cards[0].InstanceId, Is.EqualTo(new RunCardInstanceId(1)));
        Assert.That(deck.Cards[1].InstanceId, Is.EqualTo(new RunCardInstanceId(2)));
        Assert.That(deck.Cards[0].TemplateId, Is.EqualTo(deck.Cards[1].TemplateId));
        Assert.That(deck.Cards[0].InstanceId, Is.Not.EqualTo(deck.Cards[1].InstanceId));
        Assert.That(deck.Cards[2].UpgradeLevel, Is.EqualTo(1));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<RunCard>)deck.Cards).Add(
                new RunCard(new RunCardInstanceId(4), templateId: 3004, upgradeLevel: 0)));
    }

    /// <summary>值类型默认值不得绕过正数构造器成为伪造的 RunCard 实例身份。</summary>
    [Test]
    public void RunCard_WithDefaultInstanceId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new RunCard(default, templateId: 3002, upgradeLevel: 0));
    }

    /// <summary>升级命令只替换指定实例，并保持同模板副本、身份与顺序不变。</summary>
    [Test]
    public void UpgradeInstanceOneLevel_ChangesOnlyRequestedSameTemplateCopy()
    {
        var firstId = new RunCardInstanceId(1);
        var secondId = new RunCardInstanceId(2);
        var deck = new RunDeck(new[]
        {
            new RunCard(firstId, templateId: 3002, upgradeLevel: 0),
            new RunCard(secondId, templateId: 3002, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(3), templateId: 3123, upgradeLevel: 2),
        });

        RunDeck upgraded = deck.UpgradeInstanceOneLevel(
            secondId,
            (templateId, nextLevel) => templateId == 3002 && nextLevel == 1);

        Assert.That(upgraded.Cards[0].InstanceId, Is.EqualTo(firstId));
        Assert.That(upgraded.Cards[0].UpgradeLevel, Is.Zero);
        Assert.That(upgraded.Cards[1].InstanceId, Is.EqualTo(secondId));
        Assert.That(upgraded.Cards[1].TemplateId, Is.EqualTo(3002));
        Assert.That(upgraded.Cards[1].UpgradeLevel, Is.EqualTo(1));
        Assert.That(upgraded.Cards[2].InstanceId, Is.EqualTo(new RunCardInstanceId(3)));
        Assert.That(upgraded.Cards[2].UpgradeLevel, Is.EqualTo(2));
        Assert.That(deck.Cards[1].UpgradeLevel, Is.Zero);
    }

    /// <summary>不存在的实例或配置拒绝的下一等级均不得产生局部牌组后继。</summary>
    [Test]
    public void UpgradeInstanceOneLevel_InvalidIdentityOrNextLevel_IsRejected()
    {
        var deck = new RunDeck(new[]
        {
            new RunCard(new RunCardInstanceId(1), templateId: 3002, upgradeLevel: 1),
        });

        Assert.Throws<InvalidOperationException>(() => deck.UpgradeInstanceOneLevel(
            new RunCardInstanceId(2),
            (_, __) => true));
        Assert.Throws<InvalidOperationException>(() => deck.UpgradeInstanceOneLevel(
            new RunCardInstanceId(1),
            (_, __) => false));
        Assert.That(deck.Cards[0].UpgradeLevel, Is.EqualTo(1));
    }
}
