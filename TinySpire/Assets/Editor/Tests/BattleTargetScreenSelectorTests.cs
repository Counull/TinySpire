using System;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEngine;

public sealed class BattleTargetScreenSelectorTests
{
    /// <summary>验证重叠屏幕矩形先按中心距离选择，距离相同时保留 Encounter 顺序中的首项。</summary>
    [Test]
    public void Select_OverlappingRects_UsesClosestCenterThenStableOrder()
    {
        using (var combatants = new BattleCombatantsData())
        {
            EnemyCombatantData firstEnemy = combatants.AddEnemy(201, 20, 0);
            EnemyCombatantData secondEnemy = combatants.AddEnemy(202, 20, 0);
            var candidates = new[]
            {
                new BattleTargetScreenCandidate(firstEnemy.Id, new Rect(0f, 0f, 100f, 100f)),
                new BattleTargetScreenCandidate(secondEnemy.Id, new Rect(40f, 0f, 100f, 100f))
            };

            CombatantId? closest = BattleTargetScreenSelector.Select(
                new Vector2(90f, 50f),
                candidates);
            CombatantId? tied = BattleTargetScreenSelector.Select(
                new Vector2(70f, 50f),
                candidates);

            Assert.That(closest, Is.EqualTo(secondEnemy.Id));
            Assert.That(tied, Is.EqualTo(firstEnemy.Id));
        }
    }

    /// <summary>验证空候选或指针位于全部屏幕矩形之外时不会猜测目标。</summary>
    [Test]
    public void Select_EmptyOrOutsideCandidates_ReturnsNull()
    {
        using (var combatants = new BattleCombatantsData())
        {
            EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);
            var candidates = new[]
            {
                new BattleTargetScreenCandidate(enemy.Id, new Rect(0f, 0f, 100f, 100f))
            };

            Assert.That(
                BattleTargetScreenSelector.Select(Vector2.zero, Array.Empty<BattleTargetScreenCandidate>()),
                Is.Null);
            Assert.That(
                BattleTargetScreenSelector.Select(new Vector2(200f, 200f), candidates),
                Is.Null);
        }
    }
}
