using System;
using DG.Tweening;
using TinySpire.Battle;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 只为携带可见结算的权威结果提供最短展示屏障，不再复制命令生命周期。
    /// </summary>
    public sealed class BattleCommandPresentationAdapter : IBattleCommandPresentation, ITickable, IDisposable
    {
        private const float CardZoneMotionDurationSeconds = 0.22f;
        private const float ReshuffleMotionDurationSeconds = 0.32f;

        private readonly float _cueDurationSeconds;
        private readonly Func<float> _unscaledDeltaTimeProvider;
        private readonly BattleCombatFeedbackTweenFactory _combatFeedbackFactory;
        private readonly BattleFlowFeedbackTweenFactory _flowFeedbackFactory;
        private readonly BattleCardMotionTweenFactory _cardMotionFactory;
        private readonly BattleParticipantPresenter _participantPresenter;
        private readonly Func<HandCardContainer> _handCardContainerProvider;
        private readonly BattleCardPileHudView _cardPileHudView;
        private readonly bool _completeZeroDurationFallbackOnTick;
        private readonly BattleCommandPresentationRunner _runner;

        /// <summary>以 Presenter 唯一映射和不受暂停影响的帧时间创建生产表现屏障。</summary>
        [Inject]
        public BattleCommandPresentationAdapter(
            BattleParticipantPresenter participantPresenter,
            BattleCardPileHudView cardPileHudView,
            IObjectResolver resolver)
            : this(
                cueDurationSeconds: 0f,
                () => Time.unscaledDeltaTime,
                CreateCombatFeedbackFactory(participantPresenter),
                CreateProductionFlowFeedbackFactory(resolver),
                participantPresenter,
                CreateHandCardContainerProvider(resolver),
                cardPileHudView,
                completeZeroDurationFallbackOnTick: false)
        {
        }

        /// <summary>以可控帧时间把 production concrete Presenter 接入同一 runner。</summary>
        internal BattleCommandPresentationAdapter(
            BattleParticipantPresenter participantPresenter,
            Func<float> unscaledDeltaTimeProvider)
            : this(
                cueDurationSeconds: 0f,
                unscaledDeltaTimeProvider,
                CreateCombatFeedbackFactory(participantPresenter),
                flowFeedbackFactory: null,
                participantPresenter,
                handCardContainerProvider: null,
                cardPileHudView: null,
                completeZeroDurationFallbackOnTick: false)
        {
        }

        /// <summary>以现有 Participant、Hand 与 Pile concrete View 把全部 M9E cue 接入同一 runner。</summary>
        internal BattleCommandPresentationAdapter(
            BattleParticipantPresenter participantPresenter,
            HandCardContainer handCardContainer,
            BattleCardPileHudView cardPileHudView,
            Func<float> unscaledDeltaTimeProvider)
            : this(
                participantPresenter,
                handCardContainer == null
                    ? null
                    : new Func<HandCardContainer>(() => handCardContainer),
                cardPileHudView,
                unscaledDeltaTimeProvider)
        {
        }

        /// <summary>以延迟 Hand 解析避免 Queue 与 adapter 的构造环，同时保留同一 hierarchy 实例。</summary>
        internal BattleCommandPresentationAdapter(
            BattleParticipantPresenter participantPresenter,
            Func<HandCardContainer> handCardContainerProvider,
            BattleCardPileHudView cardPileHudView,
            Func<float> unscaledDeltaTimeProvider)
            : this(
                cueDurationSeconds: 0f,
                unscaledDeltaTimeProvider,
                CreateCombatFeedbackFactory(participantPresenter),
                flowFeedbackFactory: null,
                participantPresenter,
                handCardContainerProvider,
                cardPileHudView,
                completeZeroDurationFallbackOnTick: false)
        {
        }

        /// <summary>以当前参与者事实与可控流程 View 工厂验证终局即时映射，不公开或缓存 outcome。</summary>
        internal BattleCommandPresentationAdapter(
            BattleCombatantsData combatants,
            Func<BattleFlowFeedbackCue, BattleCommandPresentationTween> createFlowFeedbackTween,
            Func<float> unscaledDeltaTimeProvider)
            : this(
                cueDurationSeconds: 0f,
                unscaledDeltaTimeProvider,
                combatFeedbackFactory: null,
                CreateFlowFeedbackFactory(combatants, createFlowFeedbackTween),
                participantPresenter: null,
                handCardContainerProvider: null,
                cardPileHudView: null,
                completeZeroDurationFallbackOnTick: false)
        {
        }

        /// <summary>以可控展示时长和帧时间创建 adapter，供定向测试复用。</summary>
        public BattleCommandPresentationAdapter(
            float cueDurationSeconds,
            Func<float> unscaledDeltaTimeProvider)
            : this(
                cueDurationSeconds,
                unscaledDeltaTimeProvider,
                combatFeedbackFactory: null,
                flowFeedbackFactory: null,
                participantPresenter: null,
                handCardContainerProvider: null,
                cardPileHudView: null,
                completeZeroDurationFallbackOnTick: true)
        {
        }

        /// <summary>集中组装测试占位或 production concrete step factory。</summary>
        private BattleCommandPresentationAdapter(
            float cueDurationSeconds,
            Func<float> unscaledDeltaTimeProvider,
            BattleCombatFeedbackTweenFactory combatFeedbackFactory,
            BattleFlowFeedbackTweenFactory flowFeedbackFactory,
            BattleParticipantPresenter participantPresenter,
            Func<HandCardContainer> handCardContainerProvider,
            BattleCardPileHudView cardPileHudView,
            bool completeZeroDurationFallbackOnTick)
        {
            if (cueDurationSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(cueDurationSeconds));

            _cueDurationSeconds = cueDurationSeconds;
            _unscaledDeltaTimeProvider = unscaledDeltaTimeProvider
                ?? throw new ArgumentNullException(nameof(unscaledDeltaTimeProvider));
            _combatFeedbackFactory = combatFeedbackFactory;
            _flowFeedbackFactory = flowFeedbackFactory;
            _participantPresenter = participantPresenter;
            _handCardContainerProvider = handCardContainerProvider;
            _cardPileHudView = cardPileHudView;
            bool hasAnyCardMotionView = participantPresenter != null ||
                                        handCardContainerProvider != null ||
                                        cardPileHudView != null;
            bool hasAllCardMotionViews = participantPresenter != null &&
                                        handCardContainerProvider != null &&
                                        cardPileHudView != null;
            if (hasAnyCardMotionView && combatFeedbackFactory == null)
            {
                throw new InvalidOperationException(
                    "Concrete card motion Views require the concrete combat feedback factory.");
            }

            _cardMotionFactory = hasAllCardMotionViews
                ? new BattleCardMotionTweenFactory(CreateCardMotionTween)
                : null;
            _completeZeroDurationFallbackOnTick = completeZeroDurationFallbackOnTick;
            _runner = new BattleCommandPresentationRunner(
                CreatePreludeCueTween,
                CreateSettlementCueTween);
        }

        /// <summary>保存唯一可见结果，并把 completion 延后到最短展示时间结束。</summary>
        public void Present(BattleCommandExecutionResult result, Action onCompleted)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (onCompleted == null)
                throw new ArgumentNullException(nameof(onCompleted));
            if (result.Settlements.Count == 0)
            {
                throw new ArgumentException(
                    "零可见结算不得进入表现屏障。",
                    nameof(result));
            }
            BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
            Func<bool> canStart = result.CommandType == BattleCommandType.StartBattle &&
                                  _cardMotionFactory != null
                ? IsStartBattleCardMotionReady
                : null;
            _runner.Play(plan, onCompleted, canStart);
        }

        /// <summary>展示时间满足后只回报一次精确 completion，由 Queue 决定后续推进。</summary>
        public void Tick()
        {
            if (_completeZeroDurationFallbackOnTick && _cueDurationSeconds == 0f)
            {
                _runner.CompleteImmediately();
                return;
            }

            _runner.Tick(Math.Max(0f, _unscaledDeltaTimeProvider.Invoke()));
        }

        /// <summary>只调整表现时间倍率，不改变 cue 顺序或 Queue completion 语义。</summary>
        internal void SetPresentationSpeed(float speedMultiplier)
        {
            _runner.SetSpeed(speedMultiplier);
        }

        /// <summary>立即收口当前表现计划，并沿同一 completion 精确完成一次。</summary>
        internal void CompleteImmediately()
        {
            _runner.CompleteImmediately();
        }

        /// <summary>场景销毁时停止尚未完成的展示，不再持有 Queue completion。</summary>
        public void Dispose()
        {
            _runner.Dispose();
        }

        /// <summary>由已注册 Presenter 建立只消费 M9C 步骤的 concrete factory。</summary>
        private static BattleCombatFeedbackTweenFactory CreateCombatFeedbackFactory(
            BattleParticipantPresenter participantPresenter)
        {
            if (participantPresenter == null)
                throw new ArgumentNullException(nameof(participantPresenter));

            return new BattleCombatFeedbackTweenFactory(
                participantPresenter.CreateCombatFeedbackTween);
        }

        /// <summary>以当前参与者事实建立终局延迟映射，结果只在对应步骤构造时即时返回文案键。</summary>
        private static BattleFlowFeedbackTweenFactory CreateFlowFeedbackFactory(
            BattleCombatantsData combatants,
            Func<BattleFlowFeedbackCue, BattleCommandPresentationTween> createFlowFeedbackTween)
        {
            if (combatants == null)
                throw new ArgumentNullException(nameof(combatants));
            if (createFlowFeedbackTween == null)
                throw new ArgumentNullException(nameof(createFlowFeedbackTween));

            return new BattleFlowFeedbackTweenFactory(
                createFlowFeedbackTween,
                () => ResolveBattleOutcomeLocalizationKey(combatants));
        }

        /// <summary>以当前 Scope 的延迟解析建立生产流程反馈，不引入 TurnHud 与 Queue 的构造环。</summary>
        private static BattleFlowFeedbackTweenFactory CreateProductionFlowFeedbackFactory(
            IObjectResolver resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            BattleTurnHudView configuredView = null;
            return new BattleFlowFeedbackTweenFactory(
                cue =>
                {
                    BattleTurnHudView currentView = resolver.Resolve<BattleTurnHudView>();
                    if (currentView == null)
                    {
                        throw new InvalidOperationException(
                            "Current Battle Scope does not contain BattleTurnHudView.");
                    }
                    if (configuredView != currentView)
                    {
                        ConfigureProductionFlowFeedbackView(resolver, currentView);
                        configuredView = currentView;
                    }

                    return currentView.CreateFlowFeedbackTween(cue);
                },
                () => ResolveBattleOutcomeLocalizationKey(
                    resolver.Resolve<BattleSession>().Combatants));
        }

        /// <summary>把现有本地化、同地址场景流与 Application.Quit 接到当前 concrete TurnHud。</summary>
        private static void ConfigureProductionFlowFeedbackView(
            IObjectResolver resolver,
            BattleTurnHudView view)
        {
            LocalizationService localization = resolver.Resolve<LocalizationService>();
            SceneFlowService sceneFlow = resolver.Resolve<SceneFlowService>();
            GameStartupOptions startupOptions = resolver.Resolve<GameStartupOptions>();
            view.ConfigureFlowFeedback(
                key => localization.GetString(key),
                () => sceneFlow.LoadSceneWithLoadingAsync(startupOptions.InitialSceneAddress),
                () => Application.Quit());
        }

        /// <summary>临时调用同程序集终局规则并立即映射正式文案键，不保存第二份 outcome。</summary>
        private static string ResolveBattleOutcomeLocalizationKey(
            BattleCombatantsData combatants)
        {
            switch (new BattleTerminalRules(combatants).Evaluate())
            {
                case BattleTerminalOutcome.Victory:
                    return "battle.ui.result.victory";
                case BattleTerminalOutcome.Defeat:
                    return "battle.ui.result.defeat";
                case BattleTerminalOutcome.Ongoing:
                    throw new InvalidOperationException(
                        "BattleOutcome presentation requires terminal combatant facts.");
                case BattleTerminalOutcome.InvalidFacts:
                    throw new InvalidOperationException(
                        "BattleOutcome presentation cannot map invalid empty-alive-side facts.");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>从当前 Scope 保留延迟解析入口，避免 adapter 构造时递归创建仍依赖 Queue 的 Hand。</summary>
        private static Func<HandCardContainer> CreateHandCardContainerProvider(
            IObjectResolver resolver)
        {
            if (resolver == null)
                throw new ArgumentNullException(nameof(resolver));

            return () => resolver.Resolve<HandCardContainer>();
        }

        /// <summary>优先消费唯一 PlayCard 前奏；StartBattle 继续留给 M9F 的同一前奏入口。</summary>
        private BattleCommandPresentationTween CreatePreludeCueTween(
            BattleCommandPrelude prelude)
        {
            if (_flowFeedbackFactory != null &&
                _flowFeedbackFactory.TryCreate(prelude, out BattleCommandPresentationTween tween))
            {
                return tween;
            }
            if (_cardMotionFactory != null &&
                _cardMotionFactory.TryCreate(prelude, out tween))
            {
                return tween;
            }

            return CreateCueTween();
        }

        /// <summary>优先消费 M9C concrete cue，未来步骤暂沿用同一 runner 的无可见占位。</summary>
        private BattleCommandPresentationTween CreateSettlementCueTween(
            BattleCommandPresentationStep step)
        {
            if (_flowFeedbackFactory != null &&
                _flowFeedbackFactory.TryCreate(step, out BattleCommandPresentationTween tween))
            {
                return tween;
            }
            if (_combatFeedbackFactory != null
                && _combatFeedbackFactory.TryCreate(step, out tween))
            {
                return tween;
            }
            if (_cardMotionFactory != null &&
                _cardMotionFactory.TryCreate(step, out tween))
            {
                return tween;
            }

            return CreateCueTween();
        }

        /// <summary>把冻结卡牌运动 cue 路由到当前 concrete View，不读取卡名、模板或 EffectType。</summary>
        private BattleCommandPresentationTween CreateCardMotionTween(BattleCardMotionCue cue)
        {
            switch (cue.Kind)
            {
                case BattleCardMotionCueKind.PlayCardTransientHold:
                    return ResolveHandCardContainer().CreateTransientCardHoldTween(cue);

                case BattleCardMotionCueKind.HandToDiscard:
                    if (!_cardPileHudView.TryGetPileScreenAnchor(
                            BattleCardZone.DiscardPile,
                            out Vector2 discardScreenPosition))
                    {
                        throw new InvalidOperationException(
                            "Discard pile has no current presentation anchor.");
                    }

                    return ResolveHandCardContainer().CreateTransientCardMotionTween(
                        cue,
                        discardScreenPosition,
                        CardZoneMotionDurationSeconds,
                        Ease.InCubic);

                case BattleCardMotionCueKind.DrawToHand:
                    if (!_cardPileHudView.TryGetPileScreenAnchor(
                            BattleCardZone.DrawPile,
                            out Vector2 drawScreenPosition))
                    {
                        throw new InvalidOperationException(
                            "Draw pile has no current presentation anchor.");
                    }

                    HandCardContainer handCardContainer = ResolveHandCardContainer();
                    BattleCommandPresentationTween incomingLease = null;
                    incomingLease = handCardContainer.CreateIncomingCardMotionTween(
                        cue,
                        drawScreenPosition,
                        CardZoneMotionDurationSeconds,
                        Ease.OutCubic,
                        requestFastForward: () => _runner.TryCompleteCue(incomingLease));
                    return incomingLease;

                case BattleCardMotionCueKind.CardsReshuffled:
                    return _cardPileHudView.CreateReshuffleMotionTween(
                        cue,
                        ReshuffleMotionDurationSeconds,
                        Ease.InOutCubic);

                default:
                    throw new ArgumentOutOfRangeException(nameof(cue));
            }
        }

        /// <summary>只在卡牌 cue 真正需要时解析已注册 Hand，并同步拒绝销毁或缺失的 hierarchy View。</summary>
        private HandCardContainer ResolveHandCardContainer()
        {
            HandCardContainer handCardContainer = _handCardContainerProvider?.Invoke();
            if (handCardContainer == null)
            {
                throw new InvalidOperationException(
                    "Card motion requires the current hierarchy HandCardContainer.");
            }

            return handCardContainer;
        }

        /// <summary>启动命令只等待当前权威 Hand 的真实 View 建成，不读取 Queue 等待事实或写入战斗状态。</summary>
        private bool IsStartBattleCardMotionReady()
        {
            return ResolveHandCardContainer().IsCardMotionReady;
        }

        /// <summary>为尚未接入 concrete View 的 cue 建立命令级测试时间片，不保存任何战斗事实。</summary>
        private BattleCommandPresentationTween CreateCueTween()
        {
            Sequence tween = DOTween.Sequence();
            if (_cueDurationSeconds > 0f)
                tween.AppendInterval(_cueDurationSeconds);
            else
                tween.AppendCallback(() => { });
            return new BattleCommandPresentationTween(tween, cleanup: null);
        }
    }
}
