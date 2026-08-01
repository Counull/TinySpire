using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEngine;

public sealed class BattleParticipantTargetingTests
{
    /// <summary>验证异步角色 View 尚未建立时，开始、更新和结束目标选择都安全返回空命中。</summary>
    [Test]
    public void TargetSelection_BeforeViewsExist_ReturnsNullAndEndsSafely()
    {
        var presenterObject = new GameObject("TargetSelectionPresenter");
        using (var combatants = new BattleCombatantsData())
        {
            try
            {
                EnemyCombatantData enemy = combatants.AddEnemy(201, 20, 0);
                BattleParticipantPresenter presenter =
                    presenterObject.AddComponent<BattleParticipantPresenter>();

                presenter.BeginTargetSelection(new[] { enemy.Id });
                CombatantId? selected = presenter.UpdateTargetSelection(Vector2.zero);
                presenter.EndTargetSelection();
                presenter.EndTargetSelection();

                Assert.That(selected, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }
    }
}
