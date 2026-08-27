using System;
using System.Collections.Generic;
using System.Linq;
using R3;
using TinySpire.Battle;
using TinySpire.Run.Map;

namespace TinySpire.Run
{
    /// <summary>一次已经纯计算完成、等待存档成功后发布的战斗结果后继。</summary>
    internal sealed class RunBattleResultSettlement
    {
        /// <summary>预览时仍处于 InBattle 的精确 Store 快照。</summary>
        internal RunState Source { get; }

        /// <summary>由同一结果、消费集合与冻结奖励构造的稳定后继。</summary>
        internal RunState Successor { get; }

        /// <summary>冻结预览来源与唯一后继，防止重试重新解释结果。</summary>
        internal RunBattleResultSettlement(RunState source, RunState successor)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Successor = successor ?? throw new ArgumentNullException(nameof(successor));
            if (source.ProgressPhase != RunProgressPhase.InBattle ||
                source.ActiveBattle == null ||
                successor.ActiveBattle != null ||
                successor.BattleAttemptSequence != source.BattleAttemptSequence)
            {
                throw new ArgumentException("Battle result settlement snapshots are inconsistent.");
            }
        }
    }

    /// <summary>一次已经权威冻结、等待存档成功后以来源引用 CAS 发布的非战斗进入后继。</summary>
    internal sealed class RunNodeVisitEntrySettlement
    {
        /// <summary>预览时仍停在 MapReady 的精确 Store 快照。</summary>
        internal RunState Source { get; }

        /// <summary>与已保存文档逐字段相同的唯一 NodeVisitPending 后继。</summary>
        internal RunState Successor { get; }

        /// <summary>冻结来源与后继，拒绝重试时重新读取配置或解释节点。</summary>
        internal RunNodeVisitEntrySettlement(RunState source, RunState successor)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Successor = successor ?? throw new ArgumentNullException(nameof(successor));
            if (source.ProgressPhase != RunProgressPhase.MapReady ||
                successor.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                successor.PendingNodeVisit == null ||
                successor.RunId != source.RunId ||
                !successor.PathNodeIds.SequenceEqual(source.PathNodeIds))
            {
                throw new ArgumentException("Node visit entry settlement snapshots are inconsistent.");
            }
        }
    }

    /// <summary>一次已纯计算完成、等待存档成功后以来源引用 CAS 发布的休息点后继。</summary>
    internal sealed class RunRestSettlement
    {
        /// <summary>预览时仍处于同一 Rest Pending 的精确 Store 快照。</summary>
        internal RunState Source { get; }

        /// <summary>清除 Pending、完成路径并冻结治疗或升级结果的唯一后继。</summary>
        internal RunState Successor { get; }

        /// <summary>冻结来源与完成后继，并拒绝不一致的 Rest 结算形状。</summary>
        internal RunRestSettlement(RunState source, RunState successor)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Successor = successor ?? throw new ArgumentNullException(nameof(successor));
            PendingRunNodeVisit pending = source.PendingNodeVisit;
            if (source.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                pending == null ||
                pending.Kind != MapNodeKind.Rest ||
                successor.ProgressPhase != RunProgressPhase.MapReady ||
                successor.PendingNodeVisit != null ||
                successor.RunId != source.RunId ||
                successor.PathNodeIds.Count != source.PathNodeIds.Count + 1 ||
                !successor.PathNodeIds.Take(source.PathNodeIds.Count)
                    .SequenceEqual(source.PathNodeIds) ||
                successor.PathNodeIds[successor.PathNodeIds.Count - 1] != pending.NodeId)
            {
                throw new ArgumentException("Rest settlement snapshots are inconsistent.");
            }
        }
    }

    /// <summary>一次已纯计算完成、等待存档成功后以来源引用 CAS 发布的宝箱后继。</summary>
    internal sealed class RunChestSettlement
    {
        /// <summary>预览时仍处于同一 Chest Pending 的精确 Store 快照。</summary>
        internal RunState Source { get; }

        /// <summary>清除 Pending、完成路径并冻结领取或跳过结果的唯一后继。</summary>
        internal RunState Successor { get; }

        /// <summary>冻结来源与完成后继，并拒绝不一致的 Chest 结算形状。</summary>
        internal RunChestSettlement(RunState source, RunState successor)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Successor = successor ?? throw new ArgumentNullException(nameof(successor));
            PendingRunNodeVisit pending = source.PendingNodeVisit;
            if (source.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                pending == null ||
                pending.Kind != MapNodeKind.Chest ||
                successor.ProgressPhase != RunProgressPhase.MapReady ||
                successor.PendingNodeVisit != null ||
                successor.RunId != source.RunId ||
                successor.PathNodeIds.Count != source.PathNodeIds.Count + 1 ||
                !successor.PathNodeIds.Take(source.PathNodeIds.Count)
                    .SequenceEqual(source.PathNodeIds) ||
                successor.PathNodeIds[successor.PathNodeIds.Count - 1] != pending.NodeId)
            {
                throw new ArgumentException("Chest settlement snapshots are inconsistent.");
            }
        }
    }

    /// <summary>一次已纯计算完成、等待存档成功后以来源引用 CAS 发布的商店后继。</summary>
    internal sealed class RunShopSettlement
    {
        /// <summary>预览时仍处于同一 Shop Pending 的精确 Store 快照。</summary>
        internal RunState Source { get; }

        /// <summary>一次购买后的 Shop Pending 或离开后的 MapReady 唯一后继。</summary>
        internal RunState Successor { get; }

        /// <summary>冻结来源与唯一后继，并拒绝路径或访问身份不一致的 Shop 结算形状。</summary>
        internal RunShopSettlement(RunState source, RunState successor)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Successor = successor ?? throw new ArgumentNullException(nameof(successor));
            PendingRunNodeVisit pending = source.PendingNodeVisit;
            if (source.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                pending == null ||
                pending.Kind != MapNodeKind.Shop ||
                successor.RunId != source.RunId)
            {
                throw new ArgumentException("Shop settlement source is inconsistent.");
            }

            bool purchaseSuccessor =
                successor.ProgressPhase == RunProgressPhase.NodeVisitPending &&
                successor.PendingNodeVisit != null &&
                successor.PendingNodeVisit.Kind == MapNodeKind.Shop &&
                successor.PendingNodeVisit.Id == pending.Id &&
                successor.PathNodeIds.SequenceEqual(source.PathNodeIds);
            bool leaveSuccessor =
                successor.ProgressPhase == RunProgressPhase.MapReady &&
                successor.PendingNodeVisit == null &&
                successor.PathNodeIds.Count == source.PathNodeIds.Count + 1 &&
                successor.PathNodeIds.Take(source.PathNodeIds.Count)
                    .SequenceEqual(source.PathNodeIds) &&
                successor.PathNodeIds[successor.PathNodeIds.Count - 1] == pending.NodeId;
            if (!purchaseSuccessor && !leaveSuccessor)
                throw new ArgumentException("Shop settlement successor is inconsistent.");
        }
    }

    /// <summary>一次已纯计算完成、等待存档成功后以来源引用 CAS 发布的事件选择后继。</summary>
    internal sealed class RunEventChoiceSettlement
    {
        /// <summary>预览时仍处于同一 Event Pending 的精确 Store 快照。</summary>
        internal RunState Source { get; }

        /// <summary>清除 Pending、完成路径并冻结金币或治疗结果的唯一后继。</summary>
        internal RunState Successor { get; }

        /// <summary>冻结来源与完成后继，并拒绝不一致的 Event 结算形状。</summary>
        internal RunEventChoiceSettlement(RunState source, RunState successor)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Successor = successor ?? throw new ArgumentNullException(nameof(successor));
            PendingRunNodeVisit pending = source.PendingNodeVisit;
            if (source.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                pending == null ||
                pending.Kind != MapNodeKind.Event ||
                successor.ProgressPhase != RunProgressPhase.MapReady ||
                successor.PendingNodeVisit != null ||
                successor.RunId != source.RunId ||
                successor.PathNodeIds.Count != source.PathNodeIds.Count + 1 ||
                !successor.PathNodeIds.Take(source.PathNodeIds.Count)
                    .SequenceEqual(source.PathNodeIds) ||
                successor.PathNodeIds[successor.PathNodeIds.Count - 1] != pending.NodeId)
            {
                throw new ArgumentException("Event choice settlement snapshots are inconsistent.");
            }
        }
    }

    /// <summary>跨场景 Run 业务事实的唯一写入所有者。</summary>
    public sealed class RunStateStore : IDisposable
    {
        private readonly ReactiveProperty<RunState> _state;

        /// <summary>当前 Run 完整不可变事实的只读响应式视图。</summary>
        public ReadOnlyReactiveProperty<RunState> State { get; }

        /// <summary>当前 Run 的不可变快照；创建前为空。</summary>
        public RunState Current => State.CurrentValue;

        /// <summary>建立初始为空的唯一 Run 事实容器。</summary>
        public RunStateStore()
        {
            _state = new ReactiveProperty<RunState>(null);
            State = _state.ToReadOnlyReactiveProperty();
        }

        /// <summary>创建并发布当前进程内唯一的一局新 Run。</summary>
        public RunState CreateNewRun(RunCreationOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (Current != null)
                throw new InvalidOperationException("An active run already exists.");

            Publish(new RunState(options));
            return Current;
        }

        /// <summary>只在没有 active Run 时发布一份已验证的地图稳定读档状态。</summary>
        public RunState RestoreRun(RunRestoreOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (Current != null)
                throw new InvalidOperationException("An active run already exists.");

            Publish(new RunState(options));
            return Current;
        }

        /// <summary>只在没有战斗暂存事实的稳定页显式销毁当前进程 Run。</summary>
        public void ClearStableRun()
        {
            RunState state = Current;
            if (state == null)
                return;
            if (state.ActiveBattle != null ||
                (state.ProgressPhase != RunProgressPhase.MapReady &&
                 state.ProgressPhase != RunProgressPhase.BossGateReached &&
                 state.ProgressPhase != RunProgressPhase.Terminal))
            {
                throw new InvalidOperationException(
                    "Only a map-stable Run can be cleared outside battle.");
            }

            _state.Value = null;
        }

        /// <summary>按普通可达性提交下一节点；Combat 进入承诺态，Boss 只抵达稳定门。</summary>
        public RunState CommitNode(MapNodeId nodeId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.MapReady ||
                state.ActiveBattle != null ||
                state.CommittedNodeId != null)
            {
                throw new InvalidOperationException("The Run is not ready to select a map node.");
            }

            IReadOnlyList<MapNodeId> selectableNodeIds = MapReachability.GetSelectableNodeIds(
                state.MapDefinition,
                state.CurrentNodeId,
                MapTraversalMode.Ordinary);
            if (!selectableNodeIds.Contains(nodeId))
                throw new InvalidOperationException("The requested map node is not ordinarily selectable.");

            MapNode node = state.MapDefinition.GetNode(nodeId);
            switch (node.Kind)
            {
                case MapNodeKind.Combat:
                    Publish(new RunState(
                        state,
                        state.CurrentHealth,
                        state.PathNodeIds,
                        RunProgressPhase.EncounterCommitted,
                        node.Id,
                        state.BattleAttemptSequence,
                        activeBattle: null,
                        terminalReason: null));
                    return Current;
                case MapNodeKind.Boss:
                    var reachedPath = state.PathNodeIds.Concat(new[] { node.Id }).ToArray();
                    Publish(new RunState(
                        state,
                        state.CurrentHealth,
                        reachedPath,
                        RunProgressPhase.BossGateReached,
                        committedNodeId: null,
                        battleAttemptSequence: state.BattleAttemptSequence,
                        activeBattle: null,
                        terminalReason: null));
                    return Current;
                default:
                    throw new InvalidOperationException(
                        "Non-combat nodes must enter through the durable node visit seam.");
            }
        }

        /// <summary>从当前 Run 与目标节点权威冻结 Pending 快照，但不发布业务事实。</summary>
        internal RunNodeVisitEntrySettlement PreviewNodeVisitEntry(
            MapNodeId nodeId,
            IRunNodeVisitEntryCatalog catalog)
        {
            RunState source = RequireCurrent();
            RunState successor = CreateNodeVisitPending(nodeId, catalog);
            return new RunNodeVisitEntrySettlement(source, successor);
        }

        /// <summary>仅当 Store 仍是预览来源时发布同一冻结后继，重复或过期提交保持零写入。</summary>
        internal RunState CommitNodeVisitEntry(RunNodeVisitEntrySettlement settlement)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));
            if (!ReferenceEquals(Current, settlement.Source))
            {
                throw new InvalidOperationException(
                    "The node visit entry settlement is stale or already committed.");
            }

            Publish(settlement.Successor);
            return Current;
        }

        /// <summary>仅在英雄受伤时按冻结治疗量纯计算 Rest 完成后继，不提前发布。</summary>
        internal RunRestSettlement PreviewRestHealSettlement(RunNodeVisitId visitId)
        {
            RunState source = RequireRestPending(visitId);
            if (source.CurrentHealth >= source.MaxHealth)
                throw new InvalidOperationException("A full-health hero cannot choose Rest healing.");

            long healed = (long)source.CurrentHealth + source.PendingNodeVisit.RestPayload.HealAmount;
            int settledHealth = (int)Math.Min(source.MaxHealth, healed);
            return new RunRestSettlement(
                source,
                CreateCompletedRestState(source, settledHealth, source.RunDeck));
        }

        /// <summary>要求冻结候选命中并以当前配置终审下一等级后，纯计算 Rest 升级后继。</summary>
        internal RunRestSettlement PreviewRestUpgradeSettlement(
            RunNodeVisitId visitId,
            RunCardInstanceId cardInstanceId,
            IRunCardUpgradeConfigurationCatalog catalog)
        {
            RunState source = RequireRestPending(visitId);
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (!source.PendingNodeVisit.RestPayload.UpgradeCandidateInstanceIds.Contains(
                    cardInstanceId))
            {
                throw new InvalidOperationException(
                    "The card instance is not a frozen Rest upgrade candidate.");
            }

            RunDeck settledDeck = source.RunDeck.UpgradeInstanceOneLevel(
                cardInstanceId,
                catalog.IsCardUpgradeLevelValid);
            return new RunRestSettlement(
                source,
                CreateCompletedRestState(source, source.CurrentHealth, settledDeck));
        }

        /// <summary>仅当 Store 仍是预览来源时发布同一 Rest 后继，重复或过期提交零写入。</summary>
        internal RunState CommitRestSettlement(RunRestSettlement settlement)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));
            if (!ReferenceEquals(Current, settlement.Source))
            {
                throw new InvalidOperationException(
                    "The Rest settlement is stale or already committed.");
            }

            Publish(settlement.Successor);
            return Current;
        }

        /// <summary>只从当前 Chest Pending 的冻结模板领取药水，并纯计算容量与实例身份终审后的后继。</summary>
        internal RunChestSettlement PreviewChestClaimSettlement(RunNodeVisitId visitId)
        {
            RunState source = RequireChestPending(visitId);
            RunHoldings settledHoldings = source.Holdings.AddPotion(
                source.PendingNodeVisit.ChestPayload.PotionTemplateId);
            return new RunChestSettlement(
                source,
                CreateCompletedChestState(source, settledHoldings));
        }

        /// <summary>纯计算保持持有物不变但完成同一 Chest Pending 的跳过后继。</summary>
        internal RunChestSettlement PreviewChestSkipSettlement(RunNodeVisitId visitId)
        {
            RunState source = RequireChestPending(visitId);
            return new RunChestSettlement(
                source,
                CreateCompletedChestState(source, source.Holdings));
        }

        /// <summary>仅当 Store 仍是预览来源时发布同一 Chest 后继，重复或过期提交零写入。</summary>
        internal RunState CommitChestSettlement(RunChestSettlement settlement)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));
            if (!ReferenceEquals(Current, settlement.Source))
            {
                throw new InvalidOperationException(
                    "The Chest settlement is stale or already committed.");
            }

            Publish(settlement.Successor);
            return Current;
        }

        /// <summary>终审冻结库存与当前配置后，纯计算一次购买且保持同一 Shop Pending 的原子后继。</summary>
        internal RunShopSettlement PreviewShopPurchaseSettlement(
            RunNodeVisitId visitId,
            int stockEntryId,
            IRunNodeVisitEntryCatalog catalog)
        {
            RunState source = RequireShopPending(visitId);
            if (stockEntryId <= 0)
                throw new ArgumentOutOfRangeException(nameof(stockEntryId));
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));

            RunShopStockEntry entry = source.PendingNodeVisit.ShopPayload.Entries
                .SingleOrDefault(value => value.EntryId == stockEntryId);
            if (entry == null)
                throw new InvalidOperationException("The Shop stock entry does not exist.");
            if (entry.Purchased)
                throw new InvalidOperationException("The Shop stock entry was already purchased.");
            if (source.Holdings.Gold < entry.Price)
                throw new InvalidOperationException("The Shop purchase exceeds the current gold balance.");

            RunHoldings settledHoldings = source.Holdings;
            RunDeck settledDeck = source.RunDeck;
            switch (entry.Kind)
            {
                case RunShopStockKind.Relic:
                    if (!catalog.RelicExists(entry.TemplateId))
                        throw new InvalidOperationException("The Shop Relic template no longer exists.");
                    settledHoldings = settledHoldings.AddRelic(entry.TemplateId);
                    break;
                case RunShopStockKind.Potion:
                    if (!catalog.PotionExists(entry.TemplateId))
                        throw new InvalidOperationException("The Shop Potion template no longer exists.");
                    settledHoldings = settledHoldings.AddPotion(entry.TemplateId);
                    break;
                case RunShopStockKind.Card:
                    HeroCardRewardPool pool = catalog.CreateHeroCardRewardPool(source.HeroTemplateId);
                    if (pool == null ||
                        pool.HeroTemplateId != source.HeroTemplateId ||
                        !pool.Candidates.Any(candidate => candidate.TemplateId == entry.TemplateId))
                    {
                        throw new InvalidOperationException(
                            "The Shop Card is no longer in the current Hero reward pool.");
                    }
                    settledDeck = settledDeck.AppendNewInstance(entry.TemplateId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.Kind));
            }

            settledHoldings = settledHoldings.SpendGold(entry.Price);
            return new RunShopSettlement(
                source,
                CreateShopPurchaseState(
                    source,
                    entry.EntryId,
                    settledHoldings,
                    settledDeck));
        }

        /// <summary>纯计算保留全部既有购买事实并完成当前 Shop 节点的离开后继。</summary>
        internal RunShopSettlement PreviewShopLeaveSettlement(RunNodeVisitId visitId)
        {
            RunState source = RequireShopPending(visitId);
            return new RunShopSettlement(source, CreateCompletedShopState(source));
        }

        /// <summary>仅当 Store 仍是预览来源时发布同一 Shop 后继，重复或过期提交保持零写入。</summary>
        internal RunState CommitShopSettlement(RunShopSettlement settlement)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));
            if (!ReferenceEquals(Current, settlement.Source))
            {
                throw new InvalidOperationException(
                    "The Shop settlement is stale or already committed.");
            }

            Publish(settlement.Successor);
            return Current;
        }

        /// <summary>按冻结 Event payload 纯计算金币或付费治疗的完整完成后继，不提前发布。</summary>
        internal RunEventChoiceSettlement PreviewEventChoiceSettlement(
            RunNodeVisitId visitId,
            RunEventChoiceKind choice)
        {
            RunState source = RequireEventPending(visitId);
            if (!Enum.IsDefined(typeof(RunEventChoiceKind), choice))
                throw new ArgumentOutOfRangeException(nameof(choice));

            RunEventNodeVisitPayload payload = source.PendingNodeVisit.EventPayload;
            RunHoldings settledHoldings = source.Holdings;
            int settledHealth = source.CurrentHealth;
            switch (choice)
            {
                case RunEventChoiceKind.GainGold:
                    settledHoldings = settledHoldings.GainGold(payload.GainGoldAmount);
                    break;
                case RunEventChoiceKind.PaidHeal:
                    if (source.CurrentHealth >= source.MaxHealth)
                    {
                        throw new InvalidOperationException(
                            "A full-health hero cannot choose the Event paid heal.");
                    }

                    settledHoldings = settledHoldings.SpendGold(payload.PaidHealCost);
                    long healed = (long)source.CurrentHealth + payload.PaidHealAmount;
                    settledHealth = (int)Math.Min(source.MaxHealth, healed);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(choice));
            }

            return new RunEventChoiceSettlement(
                source,
                CreateCompletedEventState(source, settledHealth, settledHoldings));
        }

        /// <summary>仅当 Store 仍是预览来源时发布同一 Event 后继，重复或过期提交保持零写入。</summary>
        internal RunState CommitEventChoiceSettlement(RunEventChoiceSettlement settlement)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));
            if (!ReferenceEquals(Current, settlement.Source))
            {
                throw new InvalidOperationException(
                    "The Event choice settlement is stale or already committed.");
            }

            Publish(settlement.Successor);
            return Current;
        }

        /// <summary>为已承诺的 Combat 节点签发唯一 attempt 与冻结 Encounter 输入。</summary>
        public RunBattleInput BeginCommittedBattle()
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.EncounterCommitted ||
                state.CommittedNodeId == null ||
                state.ActiveBattle != null)
            {
                throw new InvalidOperationException("The Run does not have a committed Combat node.");
            }

            int attemptSequence = checked(state.BattleAttemptSequence + 1);
            uint battleSeed = DeriveBattleSeed(state.RandomRootSeed, attemptSequence);
            var input = new RunBattleInput(state, attemptSequence, battleSeed);
            Publish(new RunState(
                state,
                state.CurrentHealth,
                state.PathNodeIds,
                RunProgressPhase.InBattle,
                state.CommittedNodeId,
                attemptSequence,
                input,
                terminalReason: null));
            return input;
        }

        /// <summary>只为当前 attempt 生成一次奖励，并原子冻结胜利生命与 RewardPending。</summary>
        public RunState RecordVictoryAndFreezeReward(
            RunBattleId battleId,
            int heroTemplateId,
            int settledHealth,
            int maxHealth,
            Func<RunBattleInput, PendingCardReward> rewardFactory)
        {
            RunBattleResultSettlement settlement = PreviewBattleResultSettlement(
                battleId,
                BattleResultKind.Victory,
                heroTemplateId,
                settledHealth,
                maxHealth,
                Array.Empty<RunPotionInstanceId>(),
                rewardFactory);
            return CommitBattleResultSettlement(settlement);
        }

        /// <summary>纯计算胜负后继；先移除已消费药水，再由 Store 冻结首战附着掉落。</summary>
        internal RunBattleResultSettlement PreviewBattleResultSettlement(
            RunBattleId battleId,
            BattleResultKind kind,
            int heroTemplateId,
            int settledHealth,
            int maxHealth,
            IEnumerable<RunPotionInstanceId> consumedPotionInstanceIds,
            Func<RunBattleInput, PendingCardReward> rewardFactory = null)
        {
            RunState state = RequireActiveBattle(battleId);
            ValidateResultPlayer(state, heroTemplateId, settledHealth, maxHealth);
            if (consumedPotionInstanceIds == null)
                throw new ArgumentNullException(nameof(consumedPotionInstanceIds));

            RunHoldings settledHoldings = RemoveConsumedPotions(
                state,
                consumedPotionInstanceIds);
            RunState successor;
            switch (kind)
            {
                case BattleResultKind.Victory:
                    if (settledHealth <= 0)
                        throw new ArgumentOutOfRangeException(nameof(settledHealth));
                    if (rewardFactory == null)
                        throw new ArgumentNullException(nameof(rewardFactory));

                    PendingCardReward pendingCardReward = rewardFactory(state.ActiveBattle)
                        ?? throw new InvalidOperationException("Victory reward factory returned null.");
                    if (pendingCardReward.Id.BattleId != battleId)
                    {
                        throw new InvalidOperationException(
                            "Victory reward id does not match the active run battle.");
                    }
                    pendingCardReward = FreezeAttachedLoot(
                        state,
                        settledHoldings,
                        pendingCardReward);

                    successor = new RunState(
                        state,
                        settledHealth,
                        state.PathNodeIds,
                        RunProgressPhase.RewardPending,
                        committedNodeId: state.CommittedNodeId,
                        battleAttemptSequence: state.BattleAttemptSequence,
                        activeBattle: null,
                        terminalReason: null,
                        pendingCardReward: pendingCardReward,
                        holdings: settledHoldings);
                    break;
                case BattleResultKind.Defeat:
                    if (settledHealth != 0)
                    {
                        throw new InvalidOperationException(
                            "A defeat result must settle the Run hero at zero health.");
                    }
                    if (rewardFactory != null)
                        throw new ArgumentException("Defeat cannot carry a reward factory.", nameof(rewardFactory));

                    successor = new RunState(
                        previous: state,
                        currentHealth: 0,
                        pathNodeIds: state.PathNodeIds,
                        progressPhase: RunProgressPhase.Terminal,
                        committedNodeId: state.CommittedNodeId,
                        battleAttemptSequence: state.BattleAttemptSequence,
                        activeBattle: null,
                        terminalReason: RunTerminalReason.Defeat,
                        holdings: settledHoldings);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return new RunBattleResultSettlement(state, successor);
        }

        /// <summary>只在 Store 仍是预览来源时发布同一冻结后继，重复或过期提交保持零写入。</summary>
        internal RunState CommitBattleResultSettlement(RunBattleResultSettlement settlement)
        {
            if (settlement == null)
                throw new ArgumentNullException(nameof(settlement));
            if (!ReferenceEquals(Current, settlement.Source))
            {
                throw new InvalidOperationException(
                    "The battle result settlement is stale or already committed.");
            }

            Publish(settlement.Successor);
            return Current;
        }

        /// <summary>校验奖励身份与候选并构造结算后快照，但不发布任何 Run 业务事实。</summary>
        internal RunState PreviewCardRewardSettlement(
            RunCardRewardId rewardId,
            int? selectedCardTemplateId)
        {
            return CreateCardRewardSettlement(rewardId, selectedCardTemplateId);
        }

        /// <summary>再次校验同一奖励命令，并通过唯一写入口发布已完成节点的稳定快照。</summary>
        internal RunState CommitCardRewardSettlement(
            RunCardRewardId rewardId,
            int? selectedCardTemplateId)
        {
            RunState settled = CreateCardRewardSettlement(rewardId, selectedCardTemplateId);
            Publish(settled);
            return Current;
        }

        /// <summary>在地图稳定态预演指定实例恰好升一级，但不发布任何 Run 业务事实。</summary>
        internal RunState PreviewCardUpgrade(
            RunCardInstanceId instanceId,
            IRunCardUpgradeConfigurationCatalog catalog)
        {
            return CreateCardUpgrade(instanceId, catalog);
        }

        /// <summary>再次校验指定实例的下一等级，并通过唯一写入口发布升级后的稳定快照。</summary>
        internal RunState CommitCardUpgrade(
            RunCardInstanceId instanceId,
            IRunCardUpgradeConfigurationCatalog catalog)
        {
            RunState upgraded = CreateCardUpgrade(instanceId, catalog);
            Publish(upgraded);
            return Current;
        }

        /// <summary>记录当前普通战斗失败并原子进入不可继续的 Terminal(Defeat)。</summary>
        public RunState RecordDefeat(
            RunBattleId battleId,
            int heroTemplateId,
            int settledHealth,
            int maxHealth)
        {
            RunBattleResultSettlement settlement = PreviewBattleResultSettlement(
                battleId,
                BattleResultKind.Defeat,
                heroTemplateId,
                settledHealth,
                maxHealth,
                Array.Empty<RunPotionInstanceId>());
            return CommitBattleResultSettlement(settlement);
        }

        /// <summary>释放 Run 事实的只读视图与唯一可写属性。</summary>
        public void Dispose()
        {
            State.Dispose();
            _state.Dispose();
        }

        /// <summary>通过唯一写入口发布下一份完整 Run 事实。</summary>
        private void Publish(RunState state)
        {
            _state.Value = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>读取已存在的当前 Run；尚未创建时立即拒绝迁移。</summary>
        private RunState RequireCurrent()
        {
            return Current ?? throw new InvalidOperationException("No active run exists.");
        }

        /// <summary>确认结果只结算当前仍处于战斗中的 attempt。</summary>
        private RunState RequireActiveBattle(RunBattleId battleId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.InBattle ||
                state.ActiveBattle == null ||
                state.CommittedNodeId == null ||
                state.ActiveBattle.BattleId != battleId)
            {
                throw new InvalidOperationException("The battle result does not match the active run battle.");
            }

            return state;
        }

        /// <summary>验证消费身份属于本 attempt setup，并按原槽位相对顺序构造移除后的持有物。</summary>
        private static RunHoldings RemoveConsumedPotions(
            RunState state,
            IEnumerable<RunPotionInstanceId> consumedPotionInstanceIds)
        {
            var requestedIds = new HashSet<RunPotionInstanceId>();
            RunHoldings settled = state.Holdings;
            foreach (RunPotionInstanceId instanceId in consumedPotionInstanceIds)
            {
                if (instanceId.Sequence <= 0 || !requestedIds.Add(instanceId))
                {
                    throw new InvalidOperationException(
                        "Consumed potion ids cannot be empty or duplicated.");
                }

                RunPotion battlePotion = state.ActiveBattle.Holdings.Potions
                    .SingleOrDefault(potion => potion.InstanceId == instanceId);
                RunPotion currentPotion = state.Holdings.Potions
                    .SingleOrDefault(potion => potion.InstanceId == instanceId);
                if (battlePotion == null ||
                    currentPotion == null ||
                    battlePotion.TemplateId != currentPotion.TemplateId)
                {
                    throw new InvalidOperationException(
                        "Consumed potion id does not belong to the active battle setup.");
                }

                settled = settled.RemovePotion(instanceId);
            }

            return settled;
        }

        /// <summary>从当前唯一 Pending 纯计算选择或跳过，并精确发放已冻结附着掉落。</summary>
        private RunState CreateCardRewardSettlement(
            RunCardRewardId rewardId,
            int? selectedCardTemplateId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.RewardPending ||
                state.PendingCardReward == null ||
                state.CommittedNodeId == null)
            {
                throw new InvalidOperationException("The Run does not have a pending card reward.");
            }
            if (state.PendingCardReward.Id != rewardId)
                throw new InvalidOperationException("The card reward id is stale or forged.");
            if (selectedCardTemplateId.HasValue &&
                !state.PendingCardReward.CandidateTemplateIds.Contains(
                    selectedCardTemplateId.Value))
            {
                throw new InvalidOperationException(
                    "The selected card template is not a frozen reward candidate.");
            }

            RunDeck settledDeck = selectedCardTemplateId.HasValue
                ? state.RunDeck.AppendNewInstance(selectedCardTemplateId.Value)
                : state.RunDeck;
            RunHoldings settledHoldings = ApplyAttachedLoot(
                state.Holdings,
                state.PendingCardReward.AttachedLoot);
            MapNodeId[] settledPath = state.PathNodeIds
                .Concat(new[] { state.CommittedNodeId.Value })
                .ToArray();
            return new RunState(
                state,
                state.CurrentHealth,
                settledPath,
                RunProgressPhase.MapReady,
                committedNodeId: null,
                battleAttemptSequence: state.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: null,
                runDeck: settledDeck,
                holdings: settledHoldings);
        }

        /// <summary>验证战斗次数与已完成普通战斗路径一致，并只在首战按结算后持有物冻结掉落。</summary>
        private static PendingCardReward FreezeAttachedLoot(
            RunState state,
            RunHoldings settledHoldings,
            PendingCardReward pendingCardReward)
        {
            if (pendingCardReward.AttachedLoot.RelicTemplateId.HasValue ||
                pendingCardReward.AttachedLoot.PotionTemplateId.HasValue)
            {
                throw new InvalidOperationException(
                    "Victory reward factories cannot provide attached loot.");
            }

            int completedOrdinaryCombatCount = state.PathNodeIds.Count(nodeId =>
                state.MapDefinition.GetNode(nodeId).Kind == MapNodeKind.Combat);
            int expectedAttemptSequence = checked(completedOrdinaryCombatCount + 1);
            if (state.ActiveBattle == null ||
                state.BattleAttemptSequence != expectedAttemptSequence ||
                state.ActiveBattle.BattleId.AttemptSequence != expectedAttemptSequence)
            {
                throw new InvalidOperationException(
                    "Battle attempt sequence does not match the completed ordinary Combat path.");
            }

            int? relicTemplateId = null;
            int? potionTemplateId = null;
            if (completedOrdinaryCombatCount == 0)
            {
                if (!settledHoldings.Relics.Any(relic =>
                        relic.TemplateId == RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattleRelic))
                {
                    relicTemplateId = RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattleRelic;
                }
                if (settledHoldings.Potions.Count < 3)
                    potionTemplateId = RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattlePotion;
            }

            return new PendingCardReward(
                pendingCardReward.Id,
                pendingCardReward.CandidateTemplateIds,
                new RunCardRewardAttachedLoot(relicTemplateId, potionTemplateId));
        }

        /// <summary>按遗物后药水的固定顺序把 Pending 中的附着模板追加到不可变持有物。</summary>
        private static RunHoldings ApplyAttachedLoot(
            RunHoldings holdings,
            RunCardRewardAttachedLoot attachedLoot)
        {
            RunHoldings settled = holdings;
            if (attachedLoot.RelicTemplateId.HasValue)
                settled = settled.AddRelic(attachedLoot.RelicTemplateId.Value);
            if (attachedLoot.PotionTemplateId.HasValue)
                settled = settled.AddPotion(attachedLoot.PotionTemplateId.Value);
            return settled;
        }

        /// <summary>从当前 MapReady 快照纯计算一份不推进路径的非战斗 Pending。</summary>
        private RunState CreateNodeVisitPending(
            MapNodeId nodeId,
            IRunNodeVisitEntryCatalog catalog)
        {
            RunState state = RequireCurrent();
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (state.ProgressPhase != RunProgressPhase.MapReady ||
                state.ActiveBattle != null ||
                state.CommittedNodeId != null ||
                state.PendingCardReward != null ||
                state.PendingNodeVisit != null)
            {
                throw new InvalidOperationException(
                    "The Run is not ready to enter a non-combat node visit.");
            }

            IReadOnlyList<MapNodeId> selectableNodeIds = MapReachability.GetSelectableNodeIds(
                state.MapDefinition,
                state.CurrentNodeId,
                MapTraversalMode.Ordinary);
            if (!selectableNodeIds.Contains(nodeId))
                throw new InvalidOperationException("The node visit is not ordinarily selectable.");

            MapNode node = state.MapDefinition.GetNode(nodeId);
            if (!IsNonCombatNodeKind(node.Kind))
                throw new InvalidOperationException(
                    "Only an ordinarily selectable non-combat node can enter a node visit.");
            PendingRunNodeVisit pendingNodeVisit = RunNodeVisitEntryFactory.Create(
                state,
                node,
                catalog);

            return new RunState(
                state,
                state.CurrentHealth,
                state.PathNodeIds,
                RunProgressPhase.NodeVisitPending,
                committedNodeId: null,
                battleAttemptSequence: state.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingNodeVisit: pendingNodeVisit);
        }

        /// <summary>终审当前状态恰好是命令所引用的 Rest Pending。</summary>
        private RunState RequireRestPending(RunNodeVisitId visitId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                state.PendingNodeVisit == null ||
                state.PendingNodeVisit.Kind != MapNodeKind.Rest ||
                state.PendingNodeVisit.Id != visitId)
            {
                throw new InvalidOperationException(
                    "The Rest command does not match the current pending node visit.");
            }

            return state;
        }

        /// <summary>终审当前状态恰好是命令所引用的 Chest Pending。</summary>
        private RunState RequireChestPending(RunNodeVisitId visitId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                state.PendingNodeVisit == null ||
                state.PendingNodeVisit.Kind != MapNodeKind.Chest ||
                state.PendingNodeVisit.Id != visitId)
            {
                throw new InvalidOperationException(
                    "The Chest command does not match the current pending node visit.");
            }

            return state;
        }

        /// <summary>终审当前状态恰好是命令所引用的 Shop Pending。</summary>
        private RunState RequireShopPending(RunNodeVisitId visitId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                state.PendingNodeVisit == null ||
                state.PendingNodeVisit.Kind != MapNodeKind.Shop ||
                state.PendingNodeVisit.Id != visitId)
            {
                throw new InvalidOperationException(
                    "The Shop command does not match the current pending node visit.");
            }

            return state;
        }

        /// <summary>终审当前状态恰好是命令所引用的 Event Pending。</summary>
        private RunState RequireEventPending(RunNodeVisitId visitId)
        {
            RunState state = RequireCurrent();
            if (state.ProgressPhase != RunProgressPhase.NodeVisitPending ||
                state.PendingNodeVisit == null ||
                state.PendingNodeVisit.Kind != MapNodeKind.Event ||
                state.PendingNodeVisit.Id != visitId)
            {
                throw new InvalidOperationException(
                    "The Event command does not match the current pending node visit.");
            }

            return state;
        }

        /// <summary>复制三项冻结库存并仅翻转目标 Purchased，同时原子冻结扣款与所得内容。</summary>
        private static RunState CreateShopPurchaseState(
            RunState source,
            int purchasedEntryId,
            RunHoldings settledHoldings,
            RunDeck settledDeck)
        {
            PendingRunNodeVisit pending = source.PendingNodeVisit;
            RunShopStockEntry[] settledEntries = pending.ShopPayload.Entries
                .Select(entry => new RunShopStockEntry(
                    entry.EntryId,
                    entry.Kind,
                    entry.TemplateId,
                    entry.Price,
                    entry.Purchased || entry.EntryId == purchasedEntryId))
                .ToArray();
            PendingRunNodeVisit settledPending = PendingRunNodeVisit.CreateShop(
                pending.Id,
                pending.ContentId,
                settledEntries);
            return new RunState(
                source,
                source.CurrentHealth,
                source.PathNodeIds,
                RunProgressPhase.NodeVisitPending,
                committedNodeId: null,
                battleAttemptSequence: source.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: null,
                runDeck: settledDeck,
                holdings: settledHoldings,
                pendingNodeVisit: settledPending);
        }

        /// <summary>保留当前牌组与持有物、清除 Shop Pending 并把节点路径恰好追加一次。</summary>
        private static RunState CreateCompletedShopState(RunState source)
        {
            MapNodeId[] settledPath = source.PathNodeIds
                .Concat(new[] { source.PendingNodeVisit.NodeId })
                .ToArray();
            return new RunState(
                source,
                source.CurrentHealth,
                settledPath,
                RunProgressPhase.MapReady,
                committedNodeId: null,
                battleAttemptSequence: source.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: null,
                runDeck: source.RunDeck,
                holdings: source.Holdings,
                pendingNodeVisit: null);
        }

        /// <summary>清除 Event Pending、追加一次节点路径并原子冻结生命与金币结果。</summary>
        private static RunState CreateCompletedEventState(
            RunState source,
            int settledHealth,
            RunHoldings settledHoldings)
        {
            if (settledHoldings == null)
                throw new ArgumentNullException(nameof(settledHoldings));

            MapNodeId[] settledPath = source.PathNodeIds
                .Concat(new[] { source.PendingNodeVisit.NodeId })
                .ToArray();
            return new RunState(
                source,
                settledHealth,
                settledPath,
                RunProgressPhase.MapReady,
                committedNodeId: null,
                battleAttemptSequence: source.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: null,
                runDeck: source.RunDeck,
                holdings: settledHoldings,
                pendingNodeVisit: null);
        }

        /// <summary>清除 Rest Pending、追加一次节点路径并冻结生命与牌组结果。</summary>
        private static RunState CreateCompletedRestState(
            RunState source,
            int settledHealth,
            RunDeck settledDeck)
        {
            MapNodeId[] settledPath = source.PathNodeIds
                .Concat(new[] { source.PendingNodeVisit.NodeId })
                .ToArray();
            return new RunState(
                source,
                settledHealth,
                settledPath,
                RunProgressPhase.MapReady,
                committedNodeId: null,
                battleAttemptSequence: source.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: null,
                runDeck: settledDeck,
                pendingNodeVisit: null);
        }

        /// <summary>清除 Chest Pending、追加一次节点路径并冻结领取或跳过后的持有物。</summary>
        private static RunState CreateCompletedChestState(
            RunState source,
            RunHoldings settledHoldings)
        {
            if (settledHoldings == null)
                throw new ArgumentNullException(nameof(settledHoldings));

            MapNodeId[] settledPath = source.PathNodeIds
                .Concat(new[] { source.PendingNodeVisit.NodeId })
                .ToArray();
            return new RunState(
                source,
                source.CurrentHealth,
                settledPath,
                RunProgressPhase.MapReady,
                committedNodeId: null,
                battleAttemptSequence: source.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: null,
                holdings: settledHoldings,
                pendingNodeVisit: null);
        }

        /// <summary>从当前 MapReady 快照纯计算一次实例升级后继，不引入篝火或其他选择入口。</summary>
        private RunState CreateCardUpgrade(
            RunCardInstanceId instanceId,
            IRunCardUpgradeConfigurationCatalog catalog)
        {
            RunState state = RequireCurrent();
            if (catalog == null)
                throw new ArgumentNullException(nameof(catalog));
            if (state.ProgressPhase != RunProgressPhase.MapReady ||
                state.ActiveBattle != null ||
                state.CommittedNodeId != null ||
                state.PendingCardReward != null)
            {
                throw new InvalidOperationException(
                    "Run cards can only be upgraded from a map-stable Run snapshot.");
            }

            RunDeck upgradedDeck = state.RunDeck.UpgradeInstanceOneLevel(
                instanceId,
                catalog.IsCardUpgradeLevelValid);
            return new RunState(
                state,
                state.CurrentHealth,
                state.PathNodeIds,
                RunProgressPhase.MapReady,
                committedNodeId: null,
                battleAttemptSequence: state.BattleAttemptSequence,
                activeBattle: null,
                terminalReason: null,
                pendingCardReward: null,
                runDeck: upgradedDeck);
        }

        /// <summary>判断节点是否属于本轮统一访问契约覆盖的四种非战斗节点。</summary>
        private static bool IsNonCombatNodeKind(MapNodeKind kind)
        {
            return kind == MapNodeKind.Rest ||
                   kind == MapNodeKind.Chest ||
                   kind == MapNodeKind.Shop ||
                   kind == MapNodeKind.Event;
        }

        /// <summary>确认单玩家结果身份、生命与当前 Run 的冻结上限一致。</summary>
        private static void ValidateResultPlayer(
            RunState state,
            int heroTemplateId,
            int health,
            int maxHealth)
        {
            if (heroTemplateId != state.HeroTemplateId)
                throw new InvalidOperationException("The battle result hero does not match the run hero.");
            if (maxHealth != state.MaxHealth)
                throw new InvalidOperationException("The battle result max health does not match the run hero.");
            if (health < 0 || health > maxHealth)
                throw new ArgumentOutOfRangeException(nameof(health));
        }

        /// <summary>以互素步进从 Run 根种子派生正整数空间内不重复的本战种子。</summary>
        internal static uint DeriveBattleSeed(uint randomRootSeed, int attemptSequence)
        {
            if (randomRootSeed == 0)
                throw new ArgumentOutOfRangeException(nameof(randomRootSeed));
            if (attemptSequence <= 0)
                throw new ArgumentOutOfRangeException(nameof(attemptSequence));

            const ulong seedModulus = int.MaxValue;
            const ulong coprimeStep = 1640531527u;
            ulong root = randomRootSeed % seedModulus;
            ulong attemptOffset = (ulong)(attemptSequence - 1) * coprimeStep;
            return (uint)(((root + attemptOffset) % seedModulus) + 1u);
        }
    }
}
