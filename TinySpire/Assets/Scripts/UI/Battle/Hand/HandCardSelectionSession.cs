using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TinySpire.Battle;

namespace TinySpire.UI.Battle
{
    /// <summary>描述一次手牌选择点击应被忽略、继续收集、取消当前会话或确认合法目标。</summary>
    public enum HandCardSelectionClickAction
    {
        Ignore = 0,
        Cancel = 1,
        Confirm = 2,
        Continue = 3,
    }

    /// <summary>封装一次手牌点击解析后的动作，以及当前已经冻结的目标牌实例集合。</summary>
    public readonly struct HandCardSelectionClickResolution
    {
        /// <summary>本次点击应执行的纯状态动作。</summary>
        public HandCardSelectionClickAction Action { get; }

        /// <summary>兼容单选调用方的目标牌实例；多选确认、忽略或取消时为空。</summary>
        public CardInstanceId? TargetCardId { get; }

        /// <summary>继续收集或确认时按点击顺序冻结的全部目标牌实例。</summary>
        public IReadOnlyList<CardInstanceId> SelectedCardIds { get; }

        /// <summary>创建一份动作与可选目标保持一致的不可变点击解析结果。</summary>
        internal HandCardSelectionClickResolution(
            HandCardSelectionClickAction action,
            CardInstanceId? targetCardId)
            : this(
                action,
                targetCardId.HasValue
                    ? new[] { targetCardId.Value }
                    : Array.Empty<CardInstanceId>())
        {
        }

        /// <summary>复制当前多选结果，避免后续会话点击改写已经返回给调用方的选择集合。</summary>
        internal HandCardSelectionClickResolution(
            HandCardSelectionClickAction action,
            IEnumerable<CardInstanceId> selectedCardIds)
        {
            if (selectedCardIds == null)
                throw new ArgumentNullException(nameof(selectedCardIds));

            var copiedCardIds = new List<CardInstanceId>(selectedCardIds);
            if (action == HandCardSelectionClickAction.Confirm && copiedCardIds.Count == 0)
            {
                throw new ArgumentException(
                    "A confirmed hand-card selection requires at least one target card.",
                    nameof(selectedCardIds));
            }

            if ((action == HandCardSelectionClickAction.Ignore
                    || action == HandCardSelectionClickAction.Cancel)
                && copiedCardIds.Count > 0)
            {
                throw new ArgumentException(
                    "Ignored or cancelled hand-card selections cannot carry target cards.",
                    nameof(selectedCardIds));
            }

            Action = action;
            SelectedCardIds = new ReadOnlyCollection<CardInstanceId>(copiedCardIds);
            TargetCardId = copiedCardIds.Count == 1 ? copiedCardIds[0] : null;
        }
    }

    /// <summary>冻结一次等待手牌点击的 UI 会话及其开始时读取的三份权威快照。</summary>
    public sealed class HandCardSelectionSession
    {
        /// <summary>等待补齐选牌意图的源手牌实例。</summary>
        public CardInstanceId SourceCardId { get; }

        /// <summary>源牌在开始会话时已经确定的战斗参与者目标。</summary>
        public CombatantId? PlayTargetId { get; }

        /// <summary>按调用方顺序复制并冻结的合法目标手牌实例。</summary>
        public IReadOnlyList<CardInstanceId> LegalTargetCardIds { get; }

        /// <summary>本次会话必须收集的精确目标牌数量。</summary>
        public int RequiredCount { get; }

        /// <summary>当前已经按点击顺序收集的目标牌实例。</summary>
        public IReadOnlyList<CardInstanceId> SelectedCardIds { get; }

        /// <summary>开始会话时读取的精确卡区布局引用。</summary>
        public CardZoneLayoutData InitialLayout { get; }

        /// <summary>开始会话时读取的精确回合事实引用。</summary>
        public BattleTurnData InitialTurn { get; }

        /// <summary>开始会话时读取的精确权威队列事实引用。</summary>
        public BattleCommandQueueData InitialQueue { get; }

        private readonly List<CardInstanceId> _selectedCardIds;

        /// <summary>保存已经完成防御性复制与合法性校验的会话事实。</summary>
        private HandCardSelectionSession(
            CardInstanceId sourceCardId,
            CombatantId? playTargetId,
            IReadOnlyList<CardInstanceId> legalTargetCardIds,
            int requiredCount,
            CardZoneLayoutData initialLayout,
            BattleTurnData initialTurn,
            BattleCommandQueueData initialQueue)
        {
            SourceCardId = sourceCardId;
            PlayTargetId = playTargetId;
            LegalTargetCardIds = legalTargetCardIds;
            RequiredCount = requiredCount;
            _selectedCardIds = new List<CardInstanceId>(requiredCount);
            SelectedCardIds = new ReadOnlyCollection<CardInstanceId>(_selectedCardIds);
            InitialLayout = initialLayout;
            InitialTurn = initialTurn;
            InitialQueue = initialQueue;
        }

        /// <summary>校验源牌与候选牌均属于初始 Hand，并冻结候选顺序及三份权威快照。</summary>
        public static HandCardSelectionSession Begin(
            CardInstanceId sourceCardId,
            CombatantId? playTargetId,
            IEnumerable<CardInstanceId> legalTargetCardIds,
            CardZoneLayoutData initialLayout,
            BattleTurnData initialTurn,
            BattleCommandQueueData initialQueue)
        {
            return Begin(
                sourceCardId,
                playTargetId,
                legalTargetCardIds,
                1,
                initialLayout,
                initialTurn,
                initialQueue);
        }

        /// <summary>校验精确多选数量，并冻结候选顺序与三份权威快照。</summary>
        public static HandCardSelectionSession Begin(
            CardInstanceId sourceCardId,
            CombatantId? playTargetId,
            IEnumerable<CardInstanceId> legalTargetCardIds,
            int requiredCount,
            CardZoneLayoutData initialLayout,
            BattleTurnData initialTurn,
            BattleCommandQueueData initialQueue)
        {
            if (legalTargetCardIds == null)
                throw new ArgumentNullException(nameof(legalTargetCardIds));
            if (initialLayout == null)
                throw new ArgumentNullException(nameof(initialLayout));
            if (initialTurn == null)
                throw new ArgumentNullException(nameof(initialTurn));
            if (initialQueue == null)
                throw new ArgumentNullException(nameof(initialQueue));
            if (!ContainsCardId(initialLayout.Hand, sourceCardId))
            {
                throw new ArgumentException(
                    "The source card must belong to the initial hand snapshot.",
                    nameof(sourceCardId));
            }

            var copiedTargetCardIds = new List<CardInstanceId>(legalTargetCardIds);
            if (requiredCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredCount));
            if (copiedTargetCardIds.Count == 0)
            {
                throw new ArgumentException(
                    "A hand-card selection session requires at least one legal target.",
                    nameof(legalTargetCardIds));
            }
            if (requiredCount > copiedTargetCardIds.Count)
            {
                throw new ArgumentException(
                    "The required selection count cannot exceed the legal target count.",
                    nameof(requiredCount));
            }

            var uniqueTargetCardIds = new HashSet<CardInstanceId>();
            foreach (CardInstanceId targetCardId in copiedTargetCardIds)
            {
                if (targetCardId == sourceCardId)
                {
                    throw new ArgumentException(
                        "The source card cannot also be a legal selection target.",
                        nameof(legalTargetCardIds));
                }

                if (!uniqueTargetCardIds.Add(targetCardId))
                {
                    throw new ArgumentException(
                        "Legal hand-card selection targets cannot contain duplicates.",
                        nameof(legalTargetCardIds));
                }

                if (!ContainsCardId(initialLayout.Hand, targetCardId))
                {
                    throw new ArgumentException(
                        "Every legal selection target must belong to the initial hand snapshot.",
                        nameof(legalTargetCardIds));
                }
            }

            return new HandCardSelectionSession(
                sourceCardId,
                playTargetId,
                new ReadOnlyCollection<CardInstanceId>(copiedTargetCardIds),
                requiredCount,
                initialLayout,
                initialTurn,
                initialQueue);
        }

        /// <summary>切换合法候选的选中状态，并只在达到精确数量时确认完整多选集合。</summary>
        public HandCardSelectionClickResolution ResolveClick(CardInstanceId clickedCardId)
        {
            if (clickedCardId == SourceCardId)
            {
                return new HandCardSelectionClickResolution(
                    HandCardSelectionClickAction.Cancel,
                    targetCardId: null);
            }

            if (ContainsCardId(LegalTargetCardIds, clickedCardId))
            {
                int selectedIndex = _selectedCardIds.IndexOf(clickedCardId);
                if (selectedIndex >= 0)
                {
                    _selectedCardIds.RemoveAt(selectedIndex);
                    return new HandCardSelectionClickResolution(
                        HandCardSelectionClickAction.Continue,
                        _selectedCardIds);
                }

                _selectedCardIds.Add(clickedCardId);
                return new HandCardSelectionClickResolution(
                    _selectedCardIds.Count == RequiredCount
                        ? HandCardSelectionClickAction.Confirm
                        : HandCardSelectionClickAction.Continue,
                    _selectedCardIds);
            }

            return new HandCardSelectionClickResolution(
                HandCardSelectionClickAction.Ignore,
                targetCardId: null);
        }

        /// <summary>仅当布局、回合与队列仍是会话开始时的同一不可变发布引用时返回真。</summary>
        public bool MatchesSnapshots(
            CardZoneLayoutData currentLayout,
            BattleTurnData currentTurn,
            BattleCommandQueueData currentQueue)
        {
            return ReferenceEquals(InitialLayout, currentLayout)
                && ReferenceEquals(InitialTurn, currentTurn)
                && ReferenceEquals(InitialQueue, currentQueue);
        }

        /// <summary>按牌实例标识判断指定只读序列是否包含目标牌。</summary>
        private static bool ContainsCardId(
            IReadOnlyList<CardInstanceId> cardIds,
            CardInstanceId targetCardId)
        {
            foreach (CardInstanceId cardId in cardIds)
            {
                if (cardId == targetCardId)
                    return true;
            }

            return false;
        }
    }
}
