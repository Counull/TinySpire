using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using TinySpire.Battle;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace TinySpire.UI.Battle
{
    /// <summary>
    /// 当前单玩家 BattleScene 的能量、轮次、阶段、命令反馈与结束行动入口。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleTurnHudView : MonoBehaviour
    {
        private const float BattleStartFadeDurationSeconds = 0.12f;
        private const float BattleStartHoldDurationSeconds = 0.36f;
        private const float TurnBannerFadeDurationSeconds = 0.1f;
        private const float TurnBannerHoldDurationSeconds = 0.3f;
        private const float BattleOutcomeRevealDurationSeconds = 0.22f;

        [SerializeField] private Image _energyFill;
        [SerializeField] private Text _energyText;
        [SerializeField] private Text _roundText;
        [SerializeField] private Text _phaseText;
        [SerializeField] private Text _commandStatusText;
        [SerializeField] private Image _playerTurnBanner;
        [SerializeField] private Button _endActionButton;
        [SerializeField] private CanvasGroup _battleStartOverlay;
        [SerializeField] private Text _battleStartText;
        [SerializeField] private CanvasGroup _turnBannerGroup;
        [SerializeField] private Text _turnBannerText;
        [SerializeField] private Color _playerTurnBannerColor = Color.white;
        [SerializeField] private Color _enemyTurnBannerColor = new Color32(210, 88, 88, 255);
        [SerializeField] private CanvasGroup _battleOutcomePanel;
        [SerializeField] private Text _battleOutcomeText;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Text _restartButtonText;
        [SerializeField] private Button _exitButton;
        [SerializeField] private Text _exitButtonText;

        private BattleSession _session;
        private ConfigService _configs;
        private BattleCommandQueue _queue;
        private BattleCommandSubmissionCoordinator _coordinator;
        private BattleParticipantPresenter _participantPresenter;
        private PlayerCombatantData _player;
        private BattleCommandHandle _pendingEndActionHandle;
        private bool _lastParticipantPresentationReady;
        private Func<string, string> _localizeFlowText;
        private Func<UniTask> _restartBattle;
        private Action _quitApplication;
        private bool _terminalActionSubmitted;
        private bool _showLegacyTerminalActions = true;

        /// <summary>接收当前战斗事实、统一命令入口和生命周期协调器。</summary>
        [Inject]
        public void Construct(
            BattleSession session,
            ConfigService configs,
            BattleCommandQueue queue,
            BattleCommandSubmissionCoordinator coordinator,
            BattleParticipantPresenter participantPresenter)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _participantPresenter = participantPresenter
                ?? throw new ArgumentNullException(nameof(participantPresenter));
        }

        /// <summary>配置流程反馈使用的现有本地化、同场重开与退出应用 concrete seam。</summary>
        internal void ConfigureFlowFeedback(
            Func<string, string> localizeFlowText,
            Func<UniTask> restartBattle,
            Action quitApplication,
            bool showLegacyTerminalActions = true)
        {
            _localizeFlowText = localizeFlowText
                ?? throw new ArgumentNullException(nameof(localizeFlowText));
            _restartBattle = restartBattle
                ?? throw new ArgumentNullException(nameof(restartBattle));
            _quitApplication = quitApplication
                ?? throw new ArgumentNullException(nameof(quitApplication));
            _showLegacyTerminalActions = showLegacyTerminalActions;
            HideFlowGroup(_battleStartOverlay);
            HideFlowGroup(_turnBannerGroup);
            HideBattleOutcomePanel();
            ConfigureTerminalButtonListeners();
        }

        /// <summary>把一个冻结流程 cue 转换为只操作本 View 的短生命周期 Tween lease。</summary>
        internal BattleCommandPresentationTween CreateFlowFeedbackTween(
            BattleFlowFeedbackCue cue)
        {
            if (cue == null)
                throw new ArgumentNullException(nameof(cue));
            if (_localizeFlowText == null || _restartBattle == null || _quitApplication == null)
            {
                throw new InvalidOperationException(
                    "BattleTurnHudView flow feedback must be configured before playback.");
            }

            switch (cue.Kind)
            {
                case BattleFlowFeedbackCueKind.BattleStartOverlay:
                    return CreateBattleStartOverlayTween(cue);
                case BattleFlowFeedbackCueKind.PlayerTurnBanner:
                case BattleFlowFeedbackCueKind.EnemyTurnBanner:
                    return CreateTurnBannerTween(cue);
                case BattleFlowFeedbackCueKind.BattleOutcome:
                    return CreateBattleOutcomeTween(cue);
                default:
                    throw new ArgumentOutOfRangeException(nameof(cue), cue.Kind, null);
            }
        }

        /// <summary>创建只在战斗开始前奏期间显示并局部阻断系统指针的覆盖层。</summary>
        private BattleCommandPresentationTween CreateBattleStartOverlayTween(
            BattleFlowFeedbackCue cue)
        {
            if (_battleStartOverlay == null || _battleStartText == null)
            {
                throw new InvalidOperationException(
                    "Battle start overlay references are not configured.");
            }

            Sequence sequence = DOTween.Sequence()
                .Pause()
                .AppendCallback(() =>
                {
                    if (this == null || _battleStartOverlay == null || _battleStartText == null)
                        return;

                    _battleStartText.text = _localizeFlowText.Invoke(cue.LocalizationKey);
                    _battleStartOverlay.alpha = 0f;
                    _battleStartOverlay.interactable = false;
                    _battleStartOverlay.blocksRaycasts = cue.BlocksSystemPointer;
                    _battleStartOverlay.gameObject.SetActive(true);
                })
                .Append(_battleStartOverlay
                    .DOFade(1f, BattleStartFadeDurationSeconds)
                    .SetEase(Ease.OutQuad))
                .AppendInterval(BattleStartHoldDurationSeconds)
                .Append(_battleStartOverlay
                    .DOFade(0f, BattleStartFadeDurationSeconds)
                    .SetEase(Ease.InQuad))
                .AppendCallback(() => HideFlowGroup(_battleStartOverlay));
            return new BattleCommandPresentationTween(
                sequence,
                () => HideFlowGroup(_battleStartOverlay));
        }

        /// <summary>创建复用同一正式横幅 Sprite、按阵营 tint 且不阻断输入的短回合提示。</summary>
        private BattleCommandPresentationTween CreateTurnBannerTween(
            BattleFlowFeedbackCue cue)
        {
            if (_turnBannerGroup == null || _playerTurnBanner == null || _turnBannerText == null)
                throw new InvalidOperationException("Turn banner references are not configured.");
            if (cue.BlocksSystemPointer)
            {
                throw new InvalidOperationException(
                    "Transient turn banners must not block the system pointer.");
            }

            Sequence sequence = DOTween.Sequence()
                .Pause()
                .AppendCallback(() =>
                {
                    if (this == null ||
                        _turnBannerGroup == null ||
                        _playerTurnBanner == null ||
                        _turnBannerText == null)
                    {
                        return;
                    }

                    _turnBannerText.text = _localizeFlowText.Invoke(cue.LocalizationKey);
                    _turnBannerText.raycastTarget = false;
                    _playerTurnBanner.raycastTarget = false;
                    _playerTurnBanner.color = cue.Kind == BattleFlowFeedbackCueKind.PlayerTurnBanner
                        ? _playerTurnBannerColor
                        : _enemyTurnBannerColor;
                    _turnBannerGroup.alpha = 0f;
                    _turnBannerGroup.interactable = false;
                    _turnBannerGroup.blocksRaycasts = false;
                    _turnBannerGroup.gameObject.SetActive(true);
                })
                .Append(_turnBannerGroup
                    .DOFade(1f, TurnBannerFadeDurationSeconds)
                    .SetEase(Ease.OutQuad))
                .AppendInterval(TurnBannerHoldDurationSeconds)
                .Append(_turnBannerGroup
                    .DOFade(0f, TurnBannerFadeDurationSeconds)
                    .SetEase(Ease.InQuad))
                .AppendCallback(() => HideFlowGroup(_turnBannerGroup));
            return new BattleCommandPresentationTween(
                sequence,
                () => HideFlowGroup(_turnBannerGroup));
        }

        /// <summary>创建终局时才显现并在稳定末尾持续阻断下层战斗输入的胜负面板。</summary>
        private BattleCommandPresentationTween CreateBattleOutcomeTween(
            BattleFlowFeedbackCue cue)
        {
            if (_battleOutcomePanel == null ||
                _battleOutcomeText == null ||
                _restartButton == null ||
                _restartButtonText == null ||
                _exitButton == null ||
                _exitButtonText == null)
            {
                throw new InvalidOperationException(
                    "Battle outcome panel references are not configured.");
            }
            if (!cue.BlocksSystemPointer)
            {
                throw new InvalidOperationException(
                    "Battle outcome panel must block lower battle pointer input.");
            }

            bool reachedStableEnd = false;
            Sequence sequence = DOTween.Sequence()
                .Pause()
                .AppendCallback(() =>
                {
                    if (this == null || _battleOutcomePanel == null)
                        return;

                    _battleOutcomeText.text = _localizeFlowText.Invoke(cue.LocalizationKey);
                    _restartButtonText.text = _localizeFlowText.Invoke(
                        "battle.ui.action.restart");
                    _exitButtonText.text = _localizeFlowText.Invoke(
                        "battle.ui.action.exit");
                    _battleOutcomeText.raycastTarget = false;
                    _restartButtonText.raycastTarget = false;
                    _exitButtonText.raycastTarget = false;
                    _terminalActionSubmitted = false;
                    SetTerminalButtonsVisible(_showLegacyTerminalActions);
                    SetTerminalButtonsInteractable(false);
                    _battleOutcomePanel.alpha = 0f;
                    _battleOutcomePanel.interactable = false;
                    _battleOutcomePanel.blocksRaycasts = true;
                    _battleOutcomePanel.gameObject.SetActive(true);
                })
                .Append(_battleOutcomePanel
                    .DOFade(1f, BattleOutcomeRevealDurationSeconds)
                    .SetEase(Ease.OutCubic))
                .AppendCallback(() =>
                {
                    if (this == null || _battleOutcomePanel == null)
                        return;

                    reachedStableEnd = true;
                    _battleOutcomePanel.alpha = 1f;
                    _battleOutcomePanel.interactable = _showLegacyTerminalActions;
                    _battleOutcomePanel.blocksRaycasts = true;
                    SetTerminalButtonsInteractable(_showLegacyTerminalActions);
                });
            return new BattleCommandPresentationTween(
                sequence,
                () =>
                {
                    if (!reachedStableEnd)
                        HideBattleOutcomePanel();
                });
        }

        /// <summary>幂等连接终局按钮的 concrete 场景动作，避免重复配置累积监听。</summary>
        private void ConfigureTerminalButtonListeners()
        {
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(RestartBattle);
                if (_showLegacyTerminalActions)
                    _restartButton.onClick.AddListener(RestartBattle);
            }
            if (_exitButton != null)
            {
                _exitButton.onClick.RemoveListener(QuitApplication);
                if (_showLegacyTerminalActions)
                    _exitButton.onClick.AddListener(QuitApplication);
            }
        }

        /// <summary>首次终局按钮动作经现有场景流重载同一 BattleScene，后续点击保持关闭。</summary>
        private void RestartBattle()
        {
            if (!TryBeginTerminalAction())
                return;

            _restartBattle.Invoke().Forget();
        }

        /// <summary>首次终局按钮动作调用退出应用 thin seam，Editor 与 Player 共用同一接线。</summary>
        private void QuitApplication()
        {
            if (!TryBeginTerminalAction())
                return;

            _quitApplication.Invoke();
        }

        /// <summary>原子占用本地终局场景动作 guard，并同步锁定两个场景按钮。</summary>
        private bool TryBeginTerminalAction()
        {
            if (!_showLegacyTerminalActions ||
                _terminalActionSubmitted ||
                _battleOutcomePanel == null ||
                !_battleOutcomePanel.gameObject.activeInHierarchy ||
                _restartButton == null ||
                _exitButton == null ||
                !_restartButton.interactable ||
                !_exitButton.interactable)
            {
                return false;
            }

            _terminalActionSubmitted = true;
            SetTerminalButtonsInteractable(false);
            return true;
        }

        /// <summary>按 Battle 是否由 Run 托管切换旧终局按钮可见性。</summary>
        private void SetTerminalButtonsVisible(bool visible)
        {
            if (_restartButton != null)
                _restartButton.gameObject.SetActive(visible);
            if (_exitButton != null)
                _exitButton.gameObject.SetActive(visible);
        }

        /// <summary>同步调整两个终局场景按钮，不改变面板对下层战斗输入的阻断。</summary>
        private void SetTerminalButtonsInteractable(bool interactable)
        {
            if (_restartButton != null)
                _restartButton.interactable = interactable;
            if (_exitButton != null)
                _exitButton.interactable = interactable;
        }

        /// <summary>取消未完成终局 cue 时清空面板与按钮 guard；稳定终局不调用本方法。</summary>
        private void HideBattleOutcomePanel()
        {
            _terminalActionSubmitted = false;
            SetTerminalButtonsVisible(_showLegacyTerminalActions);
            SetTerminalButtonsInteractable(false);
            HideFlowGroup(_battleOutcomePanel);
        }

        /// <summary>幂等隐藏一个非权威流程反馈层并释放其局部系统指针锁。</summary>
        private static void HideFlowGroup(CanvasGroup group)
        {
            if (group == null)
                return;

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            group.gameObject.SetActive(false);
        }

        /// <summary>校验静态引用，并订阅权威回合事实和命令反馈。</summary>
        private void Start()
        {
            ValidateReferences();
            if (_session == null || _configs?.GameConfig == null || _queue == null ||
                _coordinator == null || _participantPresenter == null)
            {
                throw new InvalidOperationException(
                    "BattleTurnHudView did not receive all initialized battle dependencies.");
            }

            _player = ResolveCurrentPlayer();
            _lastParticipantPresentationReady = IsParticipantPresentationReady();
            _endActionButton.onClick.AddListener(SubmitEndPlayerAction);
            _queue.Turn.Subscribe(RefreshTurn).AddTo(this);
            _queue.Queue.Subscribe(_ => RefreshTurn(_queue.Turn.CurrentValue)).AddTo(this);
            _coordinator.Lifecycle.Subscribe(HandleCommandLifecycle).AddTo(this);
            RefreshTurn(_queue.Turn.CurrentValue);
        }

        /// <summary>只在参与者映射 readiness 变化时重派生系统指针入口，不轮询或修改战斗事实。</summary>
        private void Update()
        {
            if (_player == null)
                return;

            bool presentationReady = IsParticipantPresentationReady();
            if (presentationReady == _lastParticipantPresentationReady)
                return;

            _lastParticipantPresentationReady = presentationReady;
            RefreshTurn(_queue.Turn.CurrentValue);
        }

        /// <summary>只向统一命令队列提交当前玩家的结束行动意图。</summary>
        private void SubmitEndPlayerAction()
        {
            BattleTurnData turn = _queue.Turn.CurrentValue;
            if (!turn.Players.TryGetValue(_player.Id, out PlayerTurnData playerTurn) ||
                !BattleTurnHudPresentation.CanSubmitEndAction(
                    turn.Phase,
                    playerTurn.HasEndedAction,
                    _pendingEndActionHandle != null,
                    _queue.Queue.CurrentValue.IsFaulted,
                    IsParticipantPresentationReady()))
            {
                return;
            }

            var command = new EndPlayerActionCommand(_player.Id);
            BattleCommandHandle handle = _coordinator.PreRegister(command);
            _pendingEndActionHandle = handle;
            RefreshTurn(turn);

            BattleCommandSubmissionResult submission = _queue.Submit(command);
            if (!submission.Accepted || !submission.AuthoritySequence.HasValue)
            {
                if (ReferenceEquals(_pendingEndActionHandle, handle))
                    _pendingEndActionHandle = null;
                _commandStatusText.text = $"Rejected · {submission.FailureReason}";
                RefreshTurn(_queue.Turn.CurrentValue);
                return;
            }

            RefreshTurn(_queue.Turn.CurrentValue);
        }

        /// <summary>从最新权威快照即时派生能量、轮次、阶段与输入可用性。</summary>
        private void RefreshTurn(BattleTurnData turn)
        {
            if (_player == null)
                return;
            if (!turn.Players.TryGetValue(_player.Id, out PlayerTurnData playerTurn))
            {
                throw new InvalidOperationException(
                    $"Battle turn data does not contain current player {_player.Id.Value}.");
            }

            int energyPerRound = _configs.GameConfig.EnergyPerRound;
            _energyText.text = BattleTurnHudPresentation.FormatEnergy(
                playerTurn.Energy,
                energyPerRound);
            _energyFill.fillAmount = energyPerRound > 0
                ? Mathf.Clamp01((float)playerTurn.Energy / energyPerRound)
                : 0f;
            _roundText.text = BattleTurnHudPresentation.FormatRound(turn.RoundNumber);
            _phaseText.text = BattleTurnHudPresentation.FormatPhase(turn.Phase);
            _endActionButton.interactable = BattleTurnHudPresentation.CanSubmitEndAction(
                turn.Phase,
                playerTurn.HasEndedAction,
                _pendingEndActionHandle != null,
                _queue.Queue.CurrentValue.IsFaulted,
                IsParticipantPresentationReady());
        }

        /// <summary>显示 Queue 生命周期，并只用精确句柄清除结束命令待定状态。</summary>
        private void HandleCommandLifecycle(BattleCommandLifecycleEvent feedback)
        {
            _commandStatusText.text = BattleTurnHudPresentation.FormatFeedback(feedback);
            if (!ReferenceEquals(feedback.Handle, _pendingEndActionHandle) ||
                feedback.Stage == BattleCommandLifecycleStage.Queued)
            {
                return;
            }

            _pendingEndActionHandle = null;
            RefreshTurn(_queue.Turn.CurrentValue);
        }

        /// <summary>从当前生产 Session 中解析唯一玩家，保持单玩家限制只存在于接线层。</summary>
        private PlayerCombatantData ResolveCurrentPlayer()
        {
            PlayerCombatantData resolvedPlayer = null;
            foreach (CombatantData combatant in _session.Combatants.All.Values)
            {
                if (!(combatant is PlayerCombatantData player))
                    continue;
                if (resolvedPlayer != null)
                {
                    throw new InvalidOperationException(
                        "The current BattleTurnHudView supports exactly one production player (DEP-008).");
                }

                resolvedPlayer = player;
            }

            return resolvedPlayer
                ?? throw new InvalidOperationException("Battle session does not contain a player combatant.");
        }

        /// <summary>安全读取同场 Presenter 的非权威映射 readiness；对象销毁后保持关闭。</summary>
        private bool IsParticipantPresentationReady()
        {
            return _participantPresenter != null && _participantPresenter.IsPresentationReady;
        }

        /// <summary>确认 Prefab 已配置 M4D HUD 所需的全部静态引用。</summary>
        private void ValidateReferences()
        {
            if (_energyFill == null ||
                _energyText == null ||
                _roundText == null ||
                _phaseText == null ||
                _commandStatusText == null ||
                _playerTurnBanner == null ||
                _endActionButton == null ||
                _battleStartOverlay == null ||
                _battleStartText == null ||
                _turnBannerGroup == null ||
                _turnBannerText == null ||
                _battleOutcomePanel == null ||
                _battleOutcomeText == null ||
                _restartButton == null ||
                _restartButtonText == null ||
                _exitButton == null ||
                _exitButtonText == null)
            {
                throw new InvalidOperationException(
                    "BattleTurnHudView is missing one or more serialized UI references.");
            }
        }

        /// <summary>销毁 View 时移除结束按钮监听，响应式订阅由 GameObject 生命周期释放。</summary>
        private void OnDestroy()
        {
            if (_endActionButton != null)
                _endActionButton.onClick.RemoveListener(SubmitEndPlayerAction);
            if (_restartButton != null)
                _restartButton.onClick.RemoveListener(RestartBattle);
            if (_exitButton != null)
                _exitButton.onClick.RemoveListener(QuitApplication);
        }
    }
}
