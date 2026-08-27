using System;
using TinySpire.Battle;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 无状态地派生当前玩家的能量、轮次、阶段和命令反馈文本。
    /// </summary>
    public static class BattleTurnHudPresentation
    {
        private const int VisiblePotionSlotLimit = 3;

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

        /// <summary>仅在参与者表现就绪、玩家行动阶段、尚未结束且没有待定结束命令时允许点击结束行动。</summary>
        public static bool CanSubmitEndAction(
            BattleTurnPhase phase,
            bool hasEndedAction,
            bool hasPendingEndAction,
            bool queueFaulted = false,
            bool participantPresentationReady = true)
        {
            return participantPresentationReady &&
                   phase == BattleTurnPhase.PlayerAction &&
                   !hasEndedAction &&
                   !hasPendingEndAction &&
                   !queueFaulted;
        }

        /// <summary>把本战冻结的正数治疗量格式化为最小药水按钮文本。</summary>
        public static string FormatPotion(int healAmount)
        {
            if (healAmount <= 0)
                throw new ArgumentOutOfRangeException(nameof(healAmount));

            return $"Potion +{healAmount} HP";
        }

        /// <summary>把账本槽位数限制为本切片可见的三个药水入口。</summary>
        public static int GetVisiblePotionSlotCount(int ledgerEntryCount)
        {
            if (ledgerEntryCount < 0)
                throw new ArgumentOutOfRangeException(nameof(ledgerEntryCount));

            return Math.Min(ledgerEntryCount, VisiblePotionSlotLimit);
        }

        /// <summary>只在完整玩家行动窗口、受伤存活且实例仍可用时开放药水系统指针。</summary>
        public static bool CanSubmitPotion(
            BattleTurnPhase phase,
            bool hasEndedAction,
            int currentHealth,
            int maxHealth,
            bool isConsumed,
            bool hasPendingPotion,
            bool queueFaulted = false,
            bool participantPresentationReady = true)
        {
            if (maxHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            if (currentHealth < 0 || currentHealth > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(currentHealth));

            return participantPresentationReady &&
                   phase == BattleTurnPhase.PlayerAction &&
                   !hasEndedAction &&
                   currentHealth > 0 &&
                   currentHealth < maxHealth &&
                   !isConsumed &&
                   !hasPendingPotion &&
                   !queueFaulted;
        }
    }
}
