using System;

namespace TinySpire.UI.Battle
{
    /// <summary>M9F 战斗流程反馈的具体类别。</summary>
    internal enum BattleFlowFeedbackCueKind
    {
        BattleStartOverlay,
        PlayerTurnBanner,
        EnemyTurnBanner,
        BattleOutcome,
    }

    /// <summary>冻结一次流程反馈所需的本地化键与局部系统指针策略。</summary>
    internal sealed class BattleFlowFeedbackCue
    {
        /// <summary>具体流程反馈类别。</summary>
        public BattleFlowFeedbackCueKind Kind { get; }

        /// <summary>由现有 LocalizationService 解析的正式文本键。</summary>
        public string LocalizationKey { get; }

        /// <summary>该反馈期间是否只在 UGUI 层局部阻断系统指针。</summary>
        public bool BlocksSystemPointer { get; }

        /// <summary>冻结一次 concrete 流程反馈。</summary>
        internal BattleFlowFeedbackCue(
            BattleFlowFeedbackCueKind kind,
            string localizationKey,
            bool blocksSystemPointer)
        {
            if (string.IsNullOrWhiteSpace(localizationKey))
                throw new ArgumentException("本地化键不能为空。", nameof(localizationKey));

            Kind = kind;
            LocalizationKey = localizationKey;
            BlocksSystemPointer = blocksSystemPointer;
        }
    }

    /// <summary>把已冻结的命令前奏映射为 concrete 战斗流程反馈 Tween。</summary>
    internal sealed class BattleFlowFeedbackTweenFactory
    {
        private const string BattleStartLocalizationKey = "battle.ui.battle.start";
        private const string PlayerTurnLocalizationKey = "battle.ui.turn.player";
        private const string EnemyTurnLocalizationKey = "battle.ui.turn.enemy";

        private readonly Func<BattleFlowFeedbackCue, BattleCommandPresentationTween> _createTween;
        private readonly Func<string> _outcomeLocalizationKeyProvider;

        /// <summary>以 concrete View 工厂和延迟终局文案映射创建流程反馈工厂。</summary>
        internal BattleFlowFeedbackTweenFactory(
            Func<BattleFlowFeedbackCue, BattleCommandPresentationTween> createTween,
            Func<string> outcomeLocalizationKeyProvider)
        {
            _createTween = createTween ?? throw new ArgumentNullException(nameof(createTween));
            _outcomeLocalizationKeyProvider = outcomeLocalizationKeyProvider
                ?? throw new ArgumentNullException(nameof(outcomeLocalizationKeyProvider));
        }

        /// <summary>只消费 StartBattle 前奏；PlayCard 继续交给卡牌运动工厂。</summary>
        internal bool TryCreate(
            BattleCommandPrelude prelude,
            out BattleCommandPresentationTween tween)
        {
            if (prelude == null)
                throw new ArgumentNullException(nameof(prelude));

            if (prelude.Kind != BattleCommandPreludeKind.StartBattle)
            {
                tween = null;
                return false;
            }

            tween = _createTween.Invoke(new BattleFlowFeedbackCue(
                BattleFlowFeedbackCueKind.BattleStartOverlay,
                BattleStartLocalizationKey,
                blocksSystemPointer: true));
            return true;
        }

        /// <summary>只消费真实阶段变化派生的玩家或敌人回合横幅步骤。</summary>
        internal bool TryCreate(
            BattleCommandPresentationStep step,
            out BattleCommandPresentationTween tween)
        {
            if (step == null)
                throw new ArgumentNullException(nameof(step));

            if (!(step.Settlement is TinySpire.Battle.BattlePhaseChangedSettlement))
            {
                tween = null;
                return false;
            }

            switch (step.Kind)
            {
                case BattleCommandPresentationStepKind.PlayerTurnBanner:
                    tween = _createTween.Invoke(new BattleFlowFeedbackCue(
                        BattleFlowFeedbackCueKind.PlayerTurnBanner,
                        PlayerTurnLocalizationKey,
                        blocksSystemPointer: false));
                    return true;

                case BattleCommandPresentationStepKind.EnemyTurnBanner:
                    tween = _createTween.Invoke(new BattleFlowFeedbackCue(
                        BattleFlowFeedbackCueKind.EnemyTurnBanner,
                        EnemyTurnLocalizationKey,
                        blocksSystemPointer: false));
                    return true;

                case BattleCommandPresentationStepKind.BattleOutcome:
                    string outcomeLocalizationKey = _outcomeLocalizationKeyProvider.Invoke();
                    tween = _createTween.Invoke(new BattleFlowFeedbackCue(
                        BattleFlowFeedbackCueKind.BattleOutcome,
                        outcomeLocalizationKey,
                        blocksSystemPointer: true));
                    return true;

                default:
                    tween = null;
                    return false;
            }
        }
    }
}
