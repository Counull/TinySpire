using System;
using TinySpire.Battle;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 无状态地派生当前玩家的能量、轮次、阶段和命令反馈文本。
    /// </summary>
    public static class BattleTurnHudPresentation
    {
        /// <summary>把当前能量与每轮基础能量格式化为稳定展示文本。</summary>
        public static string FormatEnergy(int energy, int energyPerRound)
        {
            if (energy < 0)
                throw new ArgumentOutOfRangeException(nameof(energy));
            if (energyPerRound < 0)
                throw new ArgumentOutOfRangeException(nameof(energyPerRound));

            return $"{energy} / {energyPerRound}";
        }

        /// <summary>把权威轮次格式化为 HUD 文本。</summary>
        public static string FormatRound(int roundNumber)
        {
            if (roundNumber < 0)
                throw new ArgumentOutOfRangeException(nameof(roundNumber));

            return $"Round {roundNumber}";
        }

        /// <summary>直接使用稳定阶段枚举名显示当前权威阶段。</summary>
        public static string FormatPhase(BattleTurnPhase phase)
        {
            return phase.ToString();
        }

        /// <summary>将 Queue 唯一生命周期格式化为彼此明确区分的状态文本。</summary>
        public static string FormatFeedback(BattleCommandLifecycleEvent feedback)
        {
            if (feedback == null)
                throw new ArgumentNullException(nameof(feedback));

            switch (feedback.Stage)
            {
                case BattleCommandLifecycleStage.Queued:
                    return $"Queued #{feedback.AuthoritySequence} · {feedback.CommandType}";
                case BattleCommandLifecycleStage.ExecutionFailed:
                    return $"Failed #{feedback.AuthoritySequence} · {feedback.CommandType} · {feedback.FailureReason}";
                case BattleCommandLifecycleStage.ExecutionCompleted:
                    return $"Completed #{feedback.AuthoritySequence} · {feedback.CommandType}";
                case BattleCommandLifecycleStage.Faulted:
                    return $"Faulted #{feedback.AuthoritySequence} · {feedback.CommandType} · {feedback.Fault.Reason}";
                default:
                    throw new ArgumentOutOfRangeException(nameof(feedback));
            }
        }

        /// <summary>仅在玩家行动阶段、尚未结束且没有待定结束命令时允许点击结束行动。</summary>
        public static bool CanSubmitEndAction(
            BattleTurnPhase phase,
            bool hasEndedAction,
            bool hasPendingEndAction,
            bool queueFaulted = false)
        {
            return phase == BattleTurnPhase.PlayerAction &&
                   !hasEndedAction &&
                   !hasPendingEndAction &&
                   !queueFaulted;
        }
    }
}
