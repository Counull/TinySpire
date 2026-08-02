using System;
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
        [SerializeField] private Image _energyFill;
        [SerializeField] private Text _energyText;
        [SerializeField] private Text _roundText;
        [SerializeField] private Text _phaseText;
        [SerializeField] private Text _commandStatusText;
        [SerializeField] private Image _playerTurnBanner;
        [SerializeField] private Button _endActionButton;

        private BattleSession _session;
        private ConfigService _configs;
        private BattleCommandQueue _queue;
        private BattleCommandSubmissionCoordinator _coordinator;
        private PlayerCombatantData _player;
        private BattleCommandHandle _pendingEndActionHandle;

        /// <summary>接收当前战斗事实、统一命令入口和生命周期协调器。</summary>
        [Inject]
        public void Construct(
            BattleSession session,
            ConfigService configs,
            BattleCommandQueue queue,
            BattleCommandSubmissionCoordinator coordinator)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        /// <summary>校验静态引用，并订阅权威回合事实和命令反馈。</summary>
        private void Start()
        {
            ValidateReferences();
            if (_session == null || _configs?.GameConfig == null || _queue == null || _coordinator == null)
            {
                throw new InvalidOperationException(
                    "BattleTurnHudView did not receive all initialized battle dependencies.");
            }

            _player = ResolveCurrentPlayer();
            _endActionButton.onClick.AddListener(SubmitEndPlayerAction);
            _queue.Turn.Subscribe(RefreshTurn).AddTo(this);
            _queue.Queue.Subscribe(_ => RefreshTurn(_queue.Turn.CurrentValue)).AddTo(this);
            _coordinator.Lifecycle.Subscribe(HandleCommandLifecycle).AddTo(this);
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
                    _queue.Queue.CurrentValue.IsFaulted))
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
            _playerTurnBanner.gameObject.SetActive(turn.Phase == BattleTurnPhase.PlayerAction);
            _endActionButton.interactable = BattleTurnHudPresentation.CanSubmitEndAction(
                turn.Phase,
                playerTurn.HasEndedAction,
                _pendingEndActionHandle != null,
                _queue.Queue.CurrentValue.IsFaulted);
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

        /// <summary>确认 Prefab 已配置 M4D HUD 所需的全部静态引用。</summary>
        private void ValidateReferences()
        {
            if (_energyFill == null ||
                _energyText == null ||
                _roundText == null ||
                _phaseText == null ||
                _commandStatusText == null ||
                _playerTurnBanner == null ||
                _endActionButton == null)
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
        }
    }
}
