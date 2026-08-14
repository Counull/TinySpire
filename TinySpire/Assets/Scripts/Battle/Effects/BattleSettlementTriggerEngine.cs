using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TinySpire.Core;

namespace TinySpire.Battle
{
    /// <summary>共享结算触发器当前支持的事实匹配种类。</summary>
    internal enum BattleSettlementTriggerKind
    {
        BlockGained,
        FatalOrBlockBroken,
    }

    /// <summary>共享结算触发器当前支持的冻结动作种类。</summary>
    internal enum BattleSettlementTriggeredActionKind
    {
        RandomEnemyDamage,
        RandomCardPlay,
    }

    /// <summary>一次持久触发注册提交后的前后计数。</summary>
    internal readonly struct BattleSettlementTriggerRegistrationOutcome
    {
        internal int ValueBefore { get; }
        internal int ValueAfter { get; }

        /// <summary>冻结一次注册写入的前后计数。</summary>
        internal BattleSettlementTriggerRegistrationOutcome(int valueBefore, int valueAfter)
        {
            ValueBefore = valueBefore;
            ValueAfter = valueAfter;
        }
    }

    /// <summary>在卡牌父事务首写前冻结的一次持久触发注册。</summary>
    internal sealed class BattlePreparedSettlementTriggerRegistration
    {
        internal BattleSettlementTriggerEngine Owner { get; }
        internal CombatantId CombatantId { get; }
        internal BattleSettlementTriggerKind TriggerKind { get; }
        internal BattleSettlementTriggeredActionKind ActionKind { get; }
        internal int ActionValue { get; }
        internal int RevisionBefore { get; }
        internal int RegistrationCountBefore { get; }
        internal IReadOnlyList<int> CandidateTemplateIds { get; }
        internal bool IsValidated { get; private set; }
        internal bool IsConsumed { get; private set; }

        /// <summary>冻结注册归属、动作和值，并记录共享注册表版本。</summary>
        internal BattlePreparedSettlementTriggerRegistration(
            BattleSettlementTriggerEngine owner,
            CombatantId combatantId,
            BattleSettlementTriggerKind triggerKind,
            BattleSettlementTriggeredActionKind actionKind,
            int actionValue,
            int revisionBefore,
            int registrationCountBefore,
            IEnumerable<int> candidateTemplateIds = null)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (combatantId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(combatantId));
            if (actionKind == BattleSettlementTriggeredActionKind.RandomEnemyDamage &&
                actionValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(actionValue));
            if (actionKind == BattleSettlementTriggeredActionKind.RandomCardPlay &&
                actionValue != 0)
                throw new ArgumentOutOfRangeException(nameof(actionValue));
            if (revisionBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(revisionBefore));
            if (registrationCountBefore < 0)
                throw new ArgumentOutOfRangeException(nameof(registrationCountBefore));

            CombatantId = combatantId;
            TriggerKind = triggerKind;
            ActionKind = actionKind;
            ActionValue = actionValue;
            RevisionBefore = revisionBefore;
            RegistrationCountBefore = registrationCountBefore;
            CandidateTemplateIds = FreezeTemplateIds(candidateTemplateIds);
        }

        /// <summary>冻结候选模板并拒绝无效或重复身份。</summary>
        private static IReadOnlyList<int> FreezeTemplateIds(
            IEnumerable<int> candidateTemplateIds)
        {
            var frozen = new List<int>();
            var seen = new HashSet<int>();
            if (candidateTemplateIds != null)
            {
                foreach (int templateId in candidateTemplateIds)
                {
                    if (templateId <= 0 || !seen.Add(templateId))
                        throw new ArgumentOutOfRangeException(nameof(candidateTemplateIds));
                    frozen.Add(templateId);
                }
            }

            return new ReadOnlyCollection<int>(frozen);
        }

        /// <summary>记录唯一一次首写前校验成功。</summary>
        internal void MarkValidated()
        {
            if (IsValidated || IsConsumed)
                throw new InvalidOperationException("结算触发注册计划已经校验或消费。");
            IsValidated = true;
        }

        /// <summary>消费已经校验的注册计划，禁止重复写入。</summary>
        internal void MarkConsumed()
        {
            if (!IsValidated || IsConsumed)
                throw new InvalidOperationException("结算触发注册计划尚未校验或已经消费。");
            IsConsumed = true;
        }
    }

    /// <summary>从父命令结算记录派生的一条不可变触发意图。</summary>
    internal readonly struct BattleSettlementTriggerIntent
    {
        internal int RegistrationId { get; }
        internal CombatantId OwnerId { get; }
        internal BattleSettlementTriggeredActionKind ActionKind { get; }
        internal int ActionValue { get; }
        internal IReadOnlyList<int> CandidateTemplateIds { get; }

        /// <summary>冻结一次匹配后的注册身份与动作参数。</summary>
        internal BattleSettlementTriggerIntent(
            int registrationId,
            CombatantId ownerId,
            BattleSettlementTriggeredActionKind actionKind,
            int actionValue,
            IEnumerable<int> candidateTemplateIds = null)
        {
            if (registrationId <= 0)
                throw new ArgumentOutOfRangeException(nameof(registrationId));
            if (ownerId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(ownerId));
            if (actionKind == BattleSettlementTriggeredActionKind.RandomEnemyDamage &&
                actionValue <= 0)
                throw new ArgumentOutOfRangeException(nameof(actionValue));

            RegistrationId = registrationId;
            OwnerId = ownerId;
            ActionKind = actionKind;
            ActionValue = actionValue;
            CandidateTemplateIds = new ReadOnlyCollection<int>(
                new List<int>(candidateTemplateIds ?? Array.Empty<int>()));
        }
    }

    /// <summary>一批按父 settlement 顺序和注册顺序冻结的触发意图。</summary>
    internal sealed class BattlePreparedSettlementTriggerBatch
    {
        internal BattleSettlementTriggerEngine Owner { get; }
        internal int RegistrationRevision { get; }
        internal IReadOnlyList<BattleSettlementTriggerIntent> Intents { get; }

        /// <summary>冻结共享注册版本与有序触发意图。</summary>
        internal BattlePreparedSettlementTriggerBatch(
            BattleSettlementTriggerEngine owner,
            int registrationRevision,
            IEnumerable<BattleSettlementTriggerIntent> intents)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (registrationRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(registrationRevision));
            if (intents == null)
                throw new ArgumentNullException(nameof(intents));

            RegistrationRevision = registrationRevision;
            Intents = new ReadOnlyCollection<BattleSettlementTriggerIntent>(
                new List<BattleSettlementTriggerIntent>(intents));
        }
    }

    /// <summary>Queue 内部逐条解析冻结触发意图的系统命令。</summary>
    internal sealed class ResolveSettlementTriggersCommand : BattleCommand
    {
        internal BattlePreparedSettlementTriggerBatch Batch { get; }
        internal int Cursor { get; }
        internal BattleCommand ContinuationAfterBatch { get; }

        /// <summary>结算触发解析没有外部提交者。</summary>
        public override CombatantId? SubmitterId => null;

        /// <summary>返回专用的内部结算触发命令类型。</summary>
        public override BattleCommandType Type => BattleCommandType.ResolveSettlementTriggers;

        /// <summary>冻结批次游标及整批动作完成后的原有 continuation。</summary>
        internal ResolveSettlementTriggersCommand(
            BattlePreparedSettlementTriggerBatch batch,
            int cursor,
            BattleCommand continuationAfterBatch)
        {
            Batch = batch ?? throw new ArgumentNullException(nameof(batch));
            if (cursor < 0 || cursor >= batch.Intents.Count)
                throw new ArgumentOutOfRangeException(nameof(cursor));

            Cursor = cursor;
            ContinuationAfterBatch = continuationAfterBatch;
        }

        /// <summary>返回下一条触发意图，整批完成后恢复父命令原有 continuation。</summary>
        internal BattleCommand CreateContinuation()
        {
            int nextCursor = Cursor + 1;
            return nextCursor < Batch.Intents.Count
                ? new ResolveSettlementTriggersCommand(
                    Batch,
                    nextCursor,
                    ContinuationAfterBatch)
                : ContinuationAfterBatch;
        }
    }

    /// <summary>一次触发动作在首写前冻结的参与者、随机流与伤害结果。</summary>
    internal sealed class BattlePreparedSettlementTriggeredAction
    {
        internal BattleSettlementTriggerEngine Owner { get; }
        internal ResolveSettlementTriggersCommand Command { get; }
        internal BattleSettlementTriggerIntent Intent { get; }
        internal BattleCombatantScalarSnapshot SourceSnapshot { get; }
        internal IReadOnlyList<BattleCombatantScalarSnapshot> EnemySnapshots { get; }
        internal uint RandomStateBefore { get; }
        internal uint RandomStateAfter { get; }
        internal CombatantId? TargetId { get; }
        internal BattleDamageFormulaOutcome? DamageOutcome { get; }
        internal BattleCardZonesData CardZones { get; }
        internal CardZoneLayoutData InitialCardZoneLayout { get; }
        internal int? SelectedTemplateId { get; }
        internal CombatantId? TriggeredCardTargetId { get; }
        internal bool IsValidated { get; private set; }
        internal bool IsConsumed { get; private set; }

        /// <summary>冻结一次触发动作所需的全部事实。</summary>
        internal BattlePreparedSettlementTriggeredAction(
            BattleSettlementTriggerEngine owner,
            ResolveSettlementTriggersCommand command,
            BattleSettlementTriggerIntent intent,
            BattleCombatantScalarSnapshot sourceSnapshot,
            IEnumerable<BattleCombatantScalarSnapshot> enemySnapshots,
            uint randomStateBefore,
            uint randomStateAfter,
            CombatantId? targetId,
            BattleDamageFormulaOutcome? damageOutcome,
            BattleCardZonesData cardZones = null,
            CardZoneLayoutData initialCardZoneLayout = null,
            int? selectedTemplateId = null,
            CombatantId? triggeredCardTargetId = null)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (enemySnapshots == null)
                throw new ArgumentNullException(nameof(enemySnapshots));
            if (randomStateBefore == 0 || randomStateAfter == 0)
                throw new ArgumentOutOfRangeException(nameof(randomStateBefore));
            if (targetId.HasValue != damageOutcome.HasValue)
                throw new ArgumentException("触发动作目标与伤害结果必须同时存在或同时为空。");
            if (selectedTemplateId.HasValue != (cardZones != null) ||
                selectedTemplateId.HasValue != (initialCardZoneLayout != null))
            {
                throw new ArgumentException("随机出牌动作的模板、卡区与布局必须同时存在。");
            }

            Intent = intent;
            SourceSnapshot = sourceSnapshot;
            EnemySnapshots = new ReadOnlyCollection<BattleCombatantScalarSnapshot>(
                new List<BattleCombatantScalarSnapshot>(enemySnapshots));
            RandomStateBefore = randomStateBefore;
            RandomStateAfter = randomStateAfter;
            TargetId = targetId;
            DamageOutcome = damageOutcome;
            CardZones = cardZones;
            InitialCardZoneLayout = initialCardZoneLayout;
            SelectedTemplateId = selectedTemplateId;
            TriggeredCardTargetId = triggeredCardTargetId;
        }

        /// <summary>记录唯一一次首写前校验成功。</summary>
        internal void MarkValidated()
        {
            if (IsValidated || IsConsumed)
                throw new InvalidOperationException("结算触发动作计划已经校验或消费。");
            IsValidated = true;
        }

        /// <summary>消费已校验动作，禁止重复伤害和随机推进。</summary>
        internal void MarkConsumed()
        {
            if (!IsValidated || IsConsumed)
                throw new InvalidOperationException("结算触发动作计划尚未校验或已经消费。");
            IsConsumed = true;
        }
    }

    /// <summary>一次结算触发子事务提交后的有序记录与可选免费出牌请求。</summary>
    internal sealed class BattleSettlementTriggeredActionResult
    {
        internal IReadOnlyList<BattleSettlementRecord> Settlements { get; }
        internal BattleTriggeredCardPlayRequest TriggeredCardPlayRequest { get; }

        /// <summary>冻结子事务记录及其至多一个后续出牌请求。</summary>
        internal BattleSettlementTriggeredActionResult(
            IEnumerable<BattleSettlementRecord> settlements,
            BattleTriggeredCardPlayRequest triggeredCardPlayRequest = null)
        {
            if (settlements == null)
                throw new ArgumentNullException(nameof(settlements));
            Settlements = new ReadOnlyCollection<BattleSettlementRecord>(
                new List<BattleSettlementRecord>(settlements));
            TriggeredCardPlayRequest = triggeredCardPlayRequest;
        }
    }

    /// <summary>隐藏持续注册、结算匹配、随机选敌和 Queue 子事务的共享深模块。</summary>
    internal sealed class BattleSettlementTriggerEngine
    {
        private sealed class Registration
        {
            internal int Id { get; }
            internal CombatantId OwnerId { get; }
            internal BattleSettlementTriggerKind TriggerKind { get; }
            internal BattleSettlementTriggeredActionKind ActionKind { get; }
            internal int ActionValue { get; }
            internal IReadOnlyList<int> CandidateTemplateIds { get; }

            /// <summary>保存一条已经提交的持久触发注册。</summary>
            internal Registration(
                int id,
                CombatantId ownerId,
                BattleSettlementTriggerKind triggerKind,
                BattleSettlementTriggeredActionKind actionKind,
                int actionValue,
                IEnumerable<int> candidateTemplateIds)
            {
                Id = id;
                OwnerId = ownerId;
                TriggerKind = triggerKind;
                ActionKind = actionKind;
                ActionValue = actionValue;
                CandidateTemplateIds = new ReadOnlyCollection<int>(
                    new List<int>(candidateTemplateIds ?? Array.Empty<int>()));
            }
        }

        private readonly BattleCombatantsData _combatants;
        private readonly IReadOnlyList<CombatantId> _enemyIdsInEncounterOrder;
        private readonly BattleCombatantEffectOperations _stateOperations;
        private readonly GameRandom _random;
        private readonly cfg.Tables _tables;
        private readonly IReadOnlyDictionary<CombatantId, BattleCardZonesData> _playerCardZones;
        private readonly List<Registration> _registrations = new List<Registration>();
        private int _revision;
        private int _nextRegistrationId = 1;

        /// <summary>绑定战斗参与者、遭遇顺序与本模块独占的确定性随机流。</summary>
        internal BattleSettlementTriggerEngine(
            BattleCombatantsData combatants,
            IReadOnlyList<CombatantId> enemyIdsInEncounterOrder,
            GameRandom random,
            cfg.Tables tables = null,
            IReadOnlyDictionary<CombatantId, BattleCardZonesData> playerCardZones = null)
        {
            _combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
            _enemyIdsInEncounterOrder = enemyIdsInEncounterOrder
                ?? throw new ArgumentNullException(nameof(enemyIdsInEncounterOrder));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _tables = tables;
            _playerCardZones = playerCardZones ??
                new Dictionary<CombatantId, BattleCardZonesData>();
            _stateOperations = new BattleCombatantEffectOperations(combatants);
        }

        /// <summary>预构建“获得格挡后随机伤害敌人”的持久注册。</summary>
        internal BattlePreparedSettlementTriggerRegistration PrepareBlockGainRandomEnemyDamage(
            CombatantId ownerId,
            int damage)
        {
            if (ownerId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(ownerId));
            if (damage <= 0)
                throw new ArgumentOutOfRangeException(nameof(damage));

            return new BattlePreparedSettlementTriggerRegistration(
                this,
                ownerId,
                BattleSettlementTriggerKind.BlockGained,
                BattleSettlementTriggeredActionKind.RandomEnemyDamage,
                damage,
                _revision,
                CountRegistrations(ownerId, BattleSettlementTriggerKind.BlockGained));
        }

        /// <summary>预构建“致死或破除格挡后随机免费打出候选攻击”的持久注册。</summary>
        internal BattlePreparedSettlementTriggerRegistration
            PrepareFatalOrBlockBrokenRandomCardPlay(
                CombatantId ownerId,
                IEnumerable<int> candidateTemplateIds)
        {
            if (ownerId.Value <= 0)
                throw new ArgumentOutOfRangeException(nameof(ownerId));
            if (candidateTemplateIds == null)
                throw new ArgumentNullException(nameof(candidateTemplateIds));

            return new BattlePreparedSettlementTriggerRegistration(
                this,
                ownerId,
                BattleSettlementTriggerKind.FatalOrBlockBroken,
                BattleSettlementTriggeredActionKind.RandomCardPlay,
                0,
                _revision,
                CountRegistrations(
                    ownerId,
                    BattleSettlementTriggerKind.FatalOrBlockBroken),
                candidateTemplateIds);
        }

        /// <summary>在父事务首写前校验注册表版本并封印注册计划。</summary>
        internal bool ValidatePrepared(BattlePreparedSettlementTriggerRegistration plan)
        {
            if (plan == null || !ReferenceEquals(plan.Owner, this) ||
                plan.IsValidated || plan.IsConsumed || plan.RevisionBefore != _revision)
            {
                return false;
            }

            plan.MarkValidated();
            return true;
        }

        /// <summary>提交一条已经校验的持久注册并返回该触发种类的前后计数。</summary>
        internal BattleSettlementTriggerRegistrationOutcome CommitPrepared(
            BattlePreparedSettlementTriggerRegistration plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("结算触发注册计划不能跨引擎提交。");

            plan.MarkConsumed();
            int valueBefore = CountRegistrations(plan.CombatantId, plan.TriggerKind);
            _registrations.Add(new Registration(
                _nextRegistrationId++,
                plan.CombatantId,
                plan.TriggerKind,
                plan.ActionKind,
                plan.ActionValue,
                plan.CandidateTemplateIds));
            _revision = checked(_revision + 1);
            return new BattleSettlementTriggerRegistrationOutcome(
                valueBefore,
                checked(valueBefore + 1));
        }

        /// <summary>按父 settlement 顺序与注册顺序冻结后续系统命令；无匹配时保留原 continuation。</summary>
        internal BattleCommand CreateTriggeredContinuation(
            IReadOnlyList<BattleSettlementRecord> committedSettlements,
            BattleCommand continuationAfterBatch,
            int? suppressedRegistrationId = null)
        {
            if (committedSettlements == null)
                throw new ArgumentNullException(nameof(committedSettlements));

            var intents = new List<BattleSettlementTriggerIntent>();
            foreach (BattleSettlementRecord settlement in committedSettlements)
            {
                foreach (Registration registration in _registrations)
                {
                    if (suppressedRegistrationId.HasValue &&
                        registration.Id == suppressedRegistrationId.Value)
                        continue;
                    if (!Matches(registration, settlement))
                        continue;

                    intents.Add(new BattleSettlementTriggerIntent(
                        registration.Id,
                        registration.OwnerId,
                        registration.ActionKind,
                        registration.ActionValue,
                        registration.CandidateTemplateIds));
                }
            }

            if (intents.Count == 0)
                return continuationAfterBatch;

            var batch = new BattlePreparedSettlementTriggerBatch(this, _revision, intents);
            return new ResolveSettlementTriggersCommand(batch, 0, continuationAfterBatch);
        }

        /// <summary>按注册种类判断一条已提交记录是否产生一次触发意图。</summary>
        private static bool Matches(
            Registration registration,
            BattleSettlementRecord settlement)
        {
            switch (registration.TriggerKind)
            {
                case BattleSettlementTriggerKind.BlockGained:
                    return settlement is BattleBlockGainedSettlement blockGained &&
                           blockGained.Amount > 0 &&
                           blockGained.TargetId == registration.OwnerId;
                case BattleSettlementTriggerKind.FatalOrBlockBroken:
                    return settlement is BattleDamageAppliedSettlement damage &&
                           damage.SourceId == registration.OwnerId &&
                           (damage.WasFatal ||
                            (damage.BlockBefore > 0 && damage.BlockAfter == 0));
                default:
                    throw new ArgumentOutOfRangeException(nameof(registration.TriggerKind));
            }
        }

        /// <summary>冻结当前游标动作、全部敌方标量与独占随机流的前后状态。</summary>
        internal BattlePreparedSettlementTriggeredAction PrepareAction(
            ResolveSettlementTriggersCommand command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (!ReferenceEquals(command.Batch.Owner, this))
                throw new InvalidOperationException("结算触发命令不能跨引擎执行。");

            BattleSettlementTriggerIntent intent = command.Batch.Intents[command.Cursor];
            if (!_combatants.TryGet(intent.OwnerId, out CombatantData source))
                throw new InvalidOperationException("结算触发来源已经离开当前战斗。");

            var enemySnapshots = new List<BattleCombatantScalarSnapshot>(
                _enemyIdsInEncounterOrder.Count);
            var livingEnemyIds = new List<CombatantId>();
            foreach (CombatantId enemyId in _enemyIdsInEncounterOrder)
            {
                if (!_combatants.TryGet(enemyId, out CombatantData enemy))
                    throw new InvalidOperationException("结算触发遭遇顺序包含不存在的敌人。");

                enemySnapshots.Add(new BattleCombatantScalarSnapshot(enemy));
                if (enemy.IsAlive)
                    livingEnemyIds.Add(enemyId);
            }

            uint randomStateBefore = _random.State;
            uint randomStateAfter = randomStateBefore;
            CombatantId? targetId = null;
            BattleDamageFormulaOutcome? damageOutcome = null;
            BattleCardZonesData cardZones = null;
            CardZoneLayoutData initialCardZoneLayout = null;
            int? selectedTemplateId = null;
            CombatantId? triggeredCardTargetId = null;
            if (intent.ActionKind == BattleSettlementTriggeredActionKind.RandomEnemyDamage &&
                source.IsAlive && livingEnemyIds.Count > 0)
            {
                var candidateRandom = new GameRandom(1u)
                {
                    State = randomStateBefore,
                };
                targetId = livingEnemyIds[candidateRandom.NextInt(livingEnemyIds.Count)];
                randomStateAfter = candidateRandom.State;
                CombatantData target = _combatants.All[targetId.Value];
                BattleEffectFormulaResult formula = BattleEffectFormula.Calculate(
                    new BattleEffectFormulaContext(
                        BattleEffectOperationType.DealDamage,
                        intent.ActionValue,
                        sourceStrength: 0,
                        new BattleEffectTargetSnapshot(
                            target.CurrentHealth,
                            target.CurrentBlock,
                            vulnerable: 0)));
                damageOutcome = formula.DamageOutcome.Value;
            }
            else if (intent.ActionKind == BattleSettlementTriggeredActionKind.RandomCardPlay &&
                     source.IsAlive && livingEnemyIds.Count > 0 &&
                     intent.CandidateTemplateIds.Count > 0)
            {
                if (_tables == null ||
                    !_playerCardZones.TryGetValue(intent.OwnerId, out cardZones))
                {
                    throw new InvalidOperationException(
                        "随机出牌触发器缺少静态卡表或所属玩家卡区。");
                }

                var candidateRandom = new GameRandom(1u)
                {
                    State = randomStateBefore,
                };
                selectedTemplateId = intent.CandidateTemplateIds[
                    candidateRandom.NextInt(intent.CandidateTemplateIds.Count)];
                randomStateAfter = candidateRandom.State;
                cfg.battle.Card selectedTemplate =
                    _tables.TbCard.GetOrDefault(selectedTemplateId.Value);
                if (selectedTemplate == null)
                    throw new InvalidOperationException("随机出牌候选模板已经从静态表中消失。");
                switch (selectedTemplate.TargetRule)
                {
                    case cfg.battle.TargetRule.Self:
                        triggeredCardTargetId = intent.OwnerId;
                        break;
                    case cfg.battle.TargetRule.Enemy:
                        triggeredCardTargetId = livingEnemyIds[0];
                        break;
                    case cfg.battle.TargetRule.RandomEnemy:
                    case cfg.battle.TargetRule.AllEnemies:
                        triggeredCardTargetId = null;
                        break;
                    default:
                        throw new InvalidOperationException("随机出牌候选使用了不支持的目标规则。");
                }

                initialCardZoneLayout = cardZones.Layout.CurrentValue;
            }

            return new BattlePreparedSettlementTriggeredAction(
                this,
                command,
                intent,
                new BattleCombatantScalarSnapshot(source),
                enemySnapshots,
                randomStateBefore,
                randomStateAfter,
                targetId,
                damageOutcome,
                cardZones,
                initialCardZoneLayout,
                selectedTemplateId,
                triggeredCardTargetId);
        }

        /// <summary>在动作首写前校验注册、参与者和随机流均未漂移。</summary>
        internal void ValidatePreparedAction(BattlePreparedSettlementTriggeredAction plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this) || plan.IsValidated || plan.IsConsumed)
                throw new InvalidOperationException("结算触发动作计划归属或阶段无效。");
            if (plan.Command.Batch.RegistrationRevision != _revision ||
                !HasRegistration(plan.Intent))
            {
                throw new InvalidOperationException("结算触发注册在动作提交前发生了漂移。");
            }
            if (_random.State != plan.RandomStateBefore)
                throw new InvalidOperationException("结算触发随机流在动作提交前发生了漂移。");
            if (!_combatants.TryGet(plan.Intent.OwnerId, out CombatantData source) ||
                !plan.SourceSnapshot.Matches(source))
            {
                throw new InvalidOperationException("结算触发来源事实在动作提交前发生了漂移。");
            }
            if (plan.EnemySnapshots.Count != _enemyIdsInEncounterOrder.Count)
                throw new InvalidOperationException("结算触发敌方快照数量无效。");
            for (int index = 0; index < _enemyIdsInEncounterOrder.Count; index++)
            {
                CombatantId enemyId = _enemyIdsInEncounterOrder[index];
                if (!_combatants.TryGet(enemyId, out CombatantData enemy) ||
                    !plan.EnemySnapshots[index].Matches(enemy))
                {
                    throw new InvalidOperationException("结算触发敌方事实在动作提交前发生了漂移。");
                }
            }
            if (plan.SelectedTemplateId.HasValue)
            {
                if (plan.CardZones == null ||
                    !ReferenceEquals(
                        plan.CardZones.Layout.CurrentValue,
                        plan.InitialCardZoneLayout) ||
                    _tables?.TbCard.GetOrDefault(plan.SelectedTemplateId.Value) == null)
                {
                    throw new InvalidOperationException(
                        "随机出牌触发器的卡区或模板在首次写入前发生了漂移。");
                }
            }

            plan.MarkValidated();
        }

        /// <summary>提交冻结伤害并只在成功写入后推进专用随机流。</summary>
        internal BattleSettlementTriggeredActionResult CommitPreparedAction(
            BattlePreparedSettlementTriggeredAction plan)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (!ReferenceEquals(plan.Owner, this))
                throw new InvalidOperationException("结算触发动作计划不能跨引擎提交。");

            plan.MarkConsumed();
            if (plan.SelectedTemplateId.HasValue)
            {
                IReadOnlyList<CardInstanceId> created =
                    plan.CardZones.AddTemporaryToHand(plan.SelectedTemplateId.Value, count: 1);
                if (created.Count != 1)
                    throw new InvalidOperationException("随机出牌触发器未创建唯一临时卡实例。");

                _random.State = plan.RandomStateAfter;
                var request = new BattleTriggeredCardPlayRequest(
                    plan.Intent.OwnerId,
                    created[0],
                    plan.TriggeredCardTargetId,
                    BattleCardZone.Hand,
                    BattleCardPaymentMode.Waived,
                    BattleCardZone.ExhaustPile,
                    depth: 1);
                return new BattleSettlementTriggeredActionResult(
                    new BattleSettlementRecord[]
                    {
                        new BattleCardCreatedSettlement(
                            0,
                            created[0],
                            plan.SelectedTemplateId.Value,
                            BattleCardZone.Hand),
                    },
                    request);
            }
            if (!plan.TargetId.HasValue)
                return new BattleSettlementTriggeredActionResult(
                    Array.Empty<BattleSettlementRecord>());

            BattleCombatantEffectOperationResult result =
                _stateOperations.ApplyPreparedDamage(
                    plan.Intent.OwnerId,
                    plan.TargetId.Value,
                    plan.DamageOutcome.Value);
            if (result.Status != BattleCombatantEffectOperationStatus.Applied)
                throw new InvalidOperationException("已经校验的结算触发伤害提交失败。");

            _random.State = plan.RandomStateAfter;
            BattleDamageFormulaOutcome damage = result.DamageOutcome.Value;
            return new BattleSettlementTriggeredActionResult(
                new BattleSettlementRecord[]
                {
                    new BattleDamageAppliedSettlement(
                        0,
                        effectId: null,
                        plan.Intent.OwnerId,
                        plan.TargetId.Value,
                        damage.AttackValue,
                        damage.BlockBefore,
                        damage.BlockAfter,
                        damage.HealthBefore,
                        damage.HealthAfter),
                });
        }

        /// <summary>统计同一参与者同类触发的已提交注册数。</summary>
        private int CountRegistrations(
            CombatantId ownerId,
            BattleSettlementTriggerKind triggerKind)
        {
            int count = 0;
            foreach (Registration registration in _registrations)
            {
                if (registration.OwnerId == ownerId && registration.TriggerKind == triggerKind)
                    count++;
            }

            return count;
        }

        /// <summary>确认冻结意图仍能对应同一条持久注册。</summary>
        private bool HasRegistration(BattleSettlementTriggerIntent intent)
        {
            foreach (Registration registration in _registrations)
            {
                if (registration.Id == intent.RegistrationId &&
                    registration.OwnerId == intent.OwnerId &&
                    registration.ActionKind == intent.ActionKind &&
                    registration.ActionValue == intent.ActionValue &&
                    TemplateIdsMatch(
                        registration.CandidateTemplateIds,
                        intent.CandidateTemplateIds))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>按冻结顺序比较随机出牌候选身份集合。</summary>
        private static bool TemplateIdsMatch(
            IReadOnlyList<int> left,
            IReadOnlyList<int> right)
        {
            if (left.Count != right.Count)
                return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (left[index] != right[index])
                    return false;
            }

            return true;
        }
    }
}
