using System;
using System.Collections.Generic;
using TinySpire.Battle;
using UnityEngine;

namespace TinySpire.UI.Battle
{
    /// <summary>一个合法参与者在当前帧投影得到的屏幕矩形候选。</summary>
    public readonly struct BattleTargetScreenCandidate
    {
        /// <summary>候选对应的本场参与者标识。</summary>
        public CombatantId CombatantId { get; }

        /// <summary>候选角色边界投影并加入表现 padding 后的屏幕矩形。</summary>
        public Rect ScreenRect { get; }

        /// <summary>保存按 Encounter 稳定顺序传入的候选标识与屏幕矩形。</summary>
        public BattleTargetScreenCandidate(CombatantId combatantId, Rect screenRect)
        {
            CombatantId = combatantId;
            ScreenRect = screenRect;
        }
    }

    /// <summary>只按当前屏幕矩形选择目标，不读取或写入任何战斗事实。</summary>
    public static class BattleTargetScreenSelector
    {
        /// <summary>在包含指针的候选中选择中心最近者，距离相同则保留传入顺序首项。</summary>
        public static CombatantId? Select(
            Vector2 pointerScreenPosition,
            IReadOnlyList<BattleTargetScreenCandidate> candidates)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            CombatantId? selectedId = null;
            float selectedDistance = float.PositiveInfinity;
            foreach (BattleTargetScreenCandidate candidate in candidates)
            {
                if (!candidate.ScreenRect.Contains(pointerScreenPosition))
                    continue;

                float distance = (candidate.ScreenRect.center - pointerScreenPosition).sqrMagnitude;
                if (selectedId.HasValue && distance >= selectedDistance)
                    continue;

                selectedId = candidate.CombatantId;
                selectedDistance = distance;
            }

            return selectedId;
        }
    }
}
