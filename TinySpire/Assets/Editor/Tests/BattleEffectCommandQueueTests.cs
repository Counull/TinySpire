using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;

public sealed class BattleEffectCommandQueueTests
{
    /// <summary>验证 Strength 经唯一队列按能量、Effect、当前卡归弃牌的事务顺序结算。</summary>
    [Test]
    public void Submit_StrengthCard_RecordsEnergyThenEffectThenDiscard()
    {
        JObject strength = CreateCard(
            3001,
            cost: 0,
            cfg.battle.TargetRule.Self,
            4001);
        JObject strengthEffect = CreateEffect(
            4001,
            cfg.battle.EffectType.ModifyAttribute,
            cfg.battle.Attribute.Strength,
            value: 3);
        using (var scenario = new QueueScenario(
                   new[] { strength },
                   new[] { strengthEffect },
                   new[] { 3001 },
                   playerStrength: 1,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3001);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Player.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(3));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var strengthRecord = result.Settlements[1] as BattleAttributeModifiedSettlement;
            var moved = result.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Order, Is.Zero);
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(3));
            Assert.That(strengthRecord, Is.Not.Null);
            Assert.That(strengthRecord.Order, Is.EqualTo(1));
            Assert.That(strengthRecord.EffectId.Value.Value, Is.EqualTo(4001));
            Assert.That(strengthRecord.ValueBefore, Is.EqualTo(1));
            Assert.That(strengthRecord.ValueAfter, Is.EqualTo(4));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(2));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Player.CurrentStrength, Is.EqualTo(4));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Strike 经队列读取来源力量与目标易伤，并先以格挡吸收后再扣生命。</summary>
    [Test]
    public void Submit_StrikeCard_UsesStrengthVulnerableAndBlockBeforeDiscard()
    {
        JObject strike = CreateCard(
            3002,
            cost: 1,
            cfg.battle.TargetRule.Enemy,
            4101);
        JObject setup = CreateCard(
            3998,
            cost: 0,
            cfg.battle.TargetRule.Enemy,
            4198,
            4199);
        JObject damage = CreateEffect(
            4101,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 6);
        JObject block = CreateEffect(
            4198,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 5);
        JObject vulnerable = CreateEffect(
            4199,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            value: 1);
        using (var scenario = new QueueScenario(
                   new[] { strike, setup },
                   new[] { damage, block, vulnerable },
                   new[] { 3002 },
                   playerStrength: 2,
                   energyPerRound: 3))
        {
            scenario.ExecuteSetupCardEffects(3998, scenario.Enemy.Id);
            CardInstanceId cardId = scenario.FindCard(3002);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(3));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var moved = result.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.Order, Is.Zero);
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(2));
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That(damageRecord.Order, Is.EqualTo(1));
            Assert.That(damageRecord.AttackValue, Is.EqualTo(12));
            Assert.That(damageRecord.BlockBefore, Is.EqualTo(5));
            Assert.That(damageRecord.BlockAfter, Is.Zero);
            Assert.That(damageRecord.BlockAbsorbed, Is.EqualTo(5));
            Assert.That(damageRecord.HealthBefore, Is.EqualTo(40));
            Assert.That(damageRecord.HealthAfter, Is.EqualTo(33));
            Assert.That(damageRecord.HealthLoss, Is.EqualTo(7));
            Assert.That(damageRecord.WasFatal, Is.False);
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(2));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(scenario.Enemy.CurrentBlock, Is.Zero);
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(33));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Defend 经队列支付一点能量、增加五点格挡，并最后弃置当前卡牌。</summary>
    [Test]
    public void Submit_DefendCard_GainsBlockBeforeDiscard()
    {
        JObject defend = CreateCard(
            3003,
            cost: 1,
            cfg.battle.TargetRule.Self,
            4201);
        JObject block = CreateEffect(
            4201,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            value: 5);
        using (var scenario = new QueueScenario(
                   new[] { defend },
                   new[] { block },
                   new[] { 3003 },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3003);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Player.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(3));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var blockRecord = result.Settlements[1] as BattleBlockGainedSettlement;
            var moved = result.Settlements[2] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(2));
            Assert.That(blockRecord, Is.Not.Null);
            Assert.That(blockRecord.Order, Is.EqualTo(1));
            Assert.That(blockRecord.BlockBefore, Is.Zero);
            Assert.That(blockRecord.BlockAfter, Is.EqualTo(5));
            Assert.That(blockRecord.Amount, Is.EqualTo(5));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(2));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(scenario.Player.CurrentBlock, Is.EqualTo(5));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证 Bash 严格按伤害、易伤绑定顺序执行，再把卡牌移入弃牌堆。</summary>
    [Test]
    public void Submit_BashCard_RecordsDamageThenVulnerableThenDiscard()
    {
        JObject bash = CreateCard(
            3004,
            cost: 2,
            cfg.battle.TargetRule.Enemy,
            4301,
            4302);
        JObject damage = CreateEffect(
            4301,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 8);
        JObject vulnerable = CreateEffect(
            4302,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            value: 2);
        using (var scenario = new QueueScenario(
                   new[] { bash },
                   new[] { damage, vulnerable },
                   new[] { 3004 },
                   playerStrength: 0,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3004);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(4));
            var energy = result.Settlements[0] as BattleEnergySpentSettlement;
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var statusRecord = result.Settlements[2] as BattleStatusAppliedSettlement;
            var moved = result.Settlements[3] as BattleCardMovedSettlement;
            Assert.That(energy, Is.Not.Null);
            Assert.That(energy.EnergyBefore, Is.EqualTo(3));
            Assert.That(energy.EnergyAfter, Is.EqualTo(1));
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That(damageRecord.Order, Is.EqualTo(1));
            Assert.That(damageRecord.EffectId.Value.Value, Is.EqualTo(4301));
            Assert.That(damageRecord.AttackValue, Is.EqualTo(8));
            Assert.That(damageRecord.HealthAfter, Is.EqualTo(32));
            Assert.That(statusRecord, Is.Not.Null);
            Assert.That(statusRecord.Order, Is.EqualTo(2));
            Assert.That(statusRecord.EffectId.Value.Value, Is.EqualTo(4302));
            Assert.That(statusRecord.Status, Is.EqualTo(BattleStatusType.Vulnerable));
            Assert.That(statusRecord.ValueBefore, Is.Zero);
            Assert.That(statusRecord.ValueAfter, Is.EqualTo(2));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(3));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(scenario.Enemy.CurrentHealth, Is.EqualTo(32));
            Assert.That(scenario.Enemy.CurrentVulnerable, Is.EqualTo(2));
        }
    }

    /// <summary>验证 Bash 首个 Effect 致死后跳过易伤，但命令仍成功并完成卡牌归堆。</summary>
    [Test]
    public void Submit_FatalBash_SkipsVulnerableAndStillDiscards()
    {
        JObject bash = CreateCard(
            3004,
            cost: 2,
            cfg.battle.TargetRule.Enemy,
            4301,
            4302);
        JObject damage = CreateEffect(
            4301,
            cfg.battle.EffectType.DealDamage,
            cfg.battle.Attribute.None,
            value: 8);
        JObject vulnerable = CreateEffect(
            4302,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            value: 2);
        using (var scenario = new QueueScenario(
                   new[] { bash },
                   new[] { damage, vulnerable },
                   new[] { 3004 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   enemyHealth: 8))
        {
            CardInstanceId cardId = scenario.FindCard(3004);

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Enemy.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements.Count, Is.EqualTo(4));
            var damageRecord = result.Settlements[1] as BattleDamageAppliedSettlement;
            var skipped = result.Settlements[2] as BattleOperationSkippedSettlement;
            var moved = result.Settlements[3] as BattleCardMovedSettlement;
            Assert.That(damageRecord, Is.Not.Null);
            Assert.That(damageRecord.WasFatal, Is.True);
            Assert.That(damageRecord.HealthAfter, Is.Zero);
            Assert.That(skipped, Is.Not.Null);
            Assert.That(skipped.Order, Is.EqualTo(2));
            Assert.That(skipped.EffectId.Value.Value, Is.EqualTo(4302));
            Assert.That(skipped.Reason, Is.EqualTo(BattleOperationSkipReason.TargetNotAlive));
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(3));
            Assert.That(moved.CardId, Is.EqualTo(cardId));
            Assert.That(scenario.Enemy.CurrentVulnerable, Is.Zero);
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { cardId }));
        }
    }

    /// <summary>验证开始抽牌、结束弃手与最终敌人完成触发的重洗都进入各自命令记录。</summary>
    [Test]
    public void Submit_PhaseCommands_RecordInitialDrawDiscardAndReshuffle()
    {
        JObject firstCard = CreateCard(3101, 0, cfg.battle.TargetRule.Self);
        JObject secondCard = CreateCard(3102, 0, cfg.battle.TargetRule.Self);
        JObject thirdCard = CreateCard(3103, 0, cfg.battle.TargetRule.Self);
        using (var scenario = new QueueScenario(
                   new[] { firstCard, secondCard, thirdCard },
                   Array.Empty<JObject>(),
                   new[] { 3101, 3102, 3103 },
                   playerStrength: 0,
                   energyPerRound: 3,
                   drawDeckIntoHand: false,
                   initialHandCount: 2))
        {
            BattleCommandExecutionResult start = scenario.Presentation.Results[0];
            Assert.That(start.Succeeded, Is.True);
            Assert.That(start.Settlements.Count, Is.EqualTo(2));
            for (int index = 0; index < start.Settlements.Count; index++)
            {
                var moved = start.Settlements[index] as BattleCardMovedSettlement;
                Assert.That(moved, Is.Not.Null);
                Assert.That(moved.Order, Is.EqualTo(index));
                Assert.That(moved.CardId, Is.EqualTo(scenario.Zones.Hand[index]));
                Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
                Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.Hand));
            }

            CardInstanceId[] handBeforeEnd = new List<CardInstanceId>(scenario.Zones.Hand).ToArray();
            scenario.Queue.Submit(new EndPlayerActionCommand(scenario.Player.Id));
            BattleCommandExecutionResult end = scenario.Presentation.Results[1];
            Assert.That(end.Succeeded, Is.True);
            Assert.That(end.Settlements.Count, Is.EqualTo(2));
            for (int index = 0; index < end.Settlements.Count; index++)
            {
                var moved = end.Settlements[index] as BattleCardMovedSettlement;
                Assert.That(moved, Is.Not.Null);
                Assert.That(moved.Order, Is.EqualTo(index));
                Assert.That(moved.CardId, Is.EqualTo(handBeforeEnd[index]));
                Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.Hand));
                Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            }

            scenario.Presentation.CompleteNext();
            scenario.Queue.Submit(new CompleteEnemyActionCommand(scenario.Enemy.Id));
            BattleCommandExecutionResult completeEnemy = scenario.Presentation.Results[2];
            Assert.That(completeEnemy.Succeeded, Is.True);
            Assert.That(completeEnemy.Settlements.Count, Is.EqualTo(5));
            for (int index = 0; index < completeEnemy.Settlements.Count; index++)
                Assert.That(completeEnemy.Settlements[index].Order, Is.EqualTo(index));

            var residualDraw = completeEnemy.Settlements[0] as BattleCardMovedSettlement;
            var firstRecycled = completeEnemy.Settlements[1] as BattleCardMovedSettlement;
            var secondRecycled = completeEnemy.Settlements[2] as BattleCardMovedSettlement;
            var reshuffled = completeEnemy.Settlements[3] as BattleCardsReshuffledSettlement;
            var continuedDraw = completeEnemy.Settlements[4] as BattleCardMovedSettlement;
            Assert.That(residualDraw, Is.Not.Null);
            Assert.That(residualDraw.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(residualDraw.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(firstRecycled, Is.Not.Null);
            Assert.That(firstRecycled.CardId, Is.EqualTo(handBeforeEnd[0]));
            Assert.That(firstRecycled.FromZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(firstRecycled.ToZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(secondRecycled, Is.Not.Null);
            Assert.That(secondRecycled.CardId, Is.EqualTo(handBeforeEnd[1]));
            Assert.That(reshuffled, Is.Not.Null);
            Assert.That(reshuffled.NewDrawPileOrder.Count, Is.EqualTo(2));
            Assert.That(continuedDraw, Is.Not.Null);
            Assert.That(
                continuedDraw.CardId,
                Is.EqualTo(reshuffled.NewDrawPileOrder[reshuffled.NewDrawPileOrder.Count - 1]));
            Assert.That(continuedDraw.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(continuedDraw.ToZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[]
            {
                residualDraw.CardId,
                continuedDraw.CardId,
            }));
            Assert.That(scenario.Zones.DrawPile, Is.EqualTo(new[]
            {
                reshuffled.NewDrawPileOrder[0],
            }));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        }
    }

    /// <summary>验证后序 Effect 缺表时在支付能量、写参与者与移动卡牌前整体失败。</summary>
    [Test]
    public void Submit_CardWithMissingLaterEffect_FailsBeforeAnyWrite()
    {
        JObject invalidCard = CreateCard(
            3901,
            cost: 1,
            cfg.battle.TargetRule.Self,
            4901,
            499999);
        JObject validFirstEffect = CreateEffect(
            4901,
            cfg.battle.EffectType.ModifyAttribute,
            cfg.battle.Attribute.Strength,
            value: 3);
        using (var scenario = new QueueScenario(
                   new[] { invalidCard },
                   new[] { validFirstEffect },
                   new[] { 3901 },
                   playerStrength: 1,
                   energyPerRound: 3))
        {
            CardInstanceId cardId = scenario.FindCard(3901);
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            var healthBefore = scenario.Player.Health;
            var strengthBefore = scenario.Player.Strength;
            var blockBefore = scenario.Player.Block;
            var vulnerableBefore = scenario.Player.Vulnerable;

            scenario.Queue.Submit(new PlayCardCommand(
                scenario.Player.Id,
                cardId,
                scenario.Player.Id));

            BattleCommandExecutionResult result = scenario.Presentation.Results[1];
            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(BattleCommandExecutionFailureReason.EffectTemplateNotFound));
            Assert.That(result.Settlements, Is.Empty);
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(scenario.Player.Health, Is.SameAs(healthBefore));
            Assert.That(scenario.Player.Strength, Is.SameAs(strengthBefore));
            Assert.That(scenario.Player.Block, Is.SameAs(blockBefore));
            Assert.That(scenario.Player.Vulnerable, Is.SameAs(vulnerableBefore));
            Assert.That(scenario.Player.CurrentHealth, Is.EqualTo(30));
            Assert.That(scenario.Player.CurrentStrength, Is.EqualTo(1));
            Assert.That(scenario.Player.CurrentBlock, Is.Zero);
            Assert.That(scenario.Player.CurrentVulnerable, Is.Zero);
            Assert.That(scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy, Is.EqualTo(3));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.Empty);
        }
    }

    /// <summary>创建一张携带正式有序 Effect 绑定的最小 Card JSON。</summary>
    private static JObject CreateCard(
        int id,
        int cost,
        cfg.battle.TargetRule targetRule,
        params int[] effectIds)
    {
        var bindings = new JArray();
        foreach (int effectId in effectIds)
        {
            bindings.Add(new JObject
            {
                ["argument_key"] = string.Empty,
                ["effect_id"] = effectId,
            });
        }

        return new JObject
        {
            ["id"] = id,
            ["name_i18n_key"] = $"battle.card.test_{id}.name",
            ["description_i18n_key"] = $"battle.card.test_{id}.description",
            ["cost"] = cost,
            ["target_rule"] = (int)targetRule,
            ["effect_bindings"] = bindings,
            ["illustration_key"] = string.Empty,
        };
    }

    /// <summary>创建一条最小 CardEffect JSON。</summary>
    private static JObject CreateEffect(
        int id,
        cfg.battle.EffectType effectType,
        cfg.battle.Attribute attribute,
        int value)
    {
        return new JObject
        {
            ["id"] = id,
            ["effect_type"] = (int)effectType,
            ["attribute"] = (int)attribute,
            ["value"] = value,
        };
    }

    /// <summary>封装公共队列测试所需的唯一事实、静态表和显式表现屏障。</summary>
    private sealed class QueueScenario : IDisposable
    {
        internal BattleCombatantsData Combatants { get; }
        internal PlayerCombatantData Player { get; }
        internal EnemyCombatantData Enemy { get; }
        internal BattleCardZonesData Zones { get; }
        internal BattleEnemyIntentsData EnemyIntents { get; }
        internal cfg.Tables Tables { get; }
        internal ControllableBattleCommandPresentation Presentation { get; }
        internal BattleCommandQueue Queue { get; }

        /// <summary>创建并开始一场仅由公共队列驱动的测试战斗。</summary>
        internal QueueScenario(
            IEnumerable<JObject> cards,
            IEnumerable<JObject> effects,
            IEnumerable<int> deck,
            int playerStrength,
            int energyPerRound,
            int enemyHealth = 40,
            bool drawDeckIntoHand = true,
            int initialHandCount = 0)
        {
            Combatants = new BattleCombatantsData();
            Player = Combatants.AddPlayer(101, 30, playerStrength);
            Enemy = Combatants.AddEnemy(201, enemyHealth, 0);
            Zones = new BattleCardZonesData(deck, shuffleSeed: 1234);
            if (drawDeckIntoHand)
                Zones.Draw(Zones.Cards.Count);
            Tables = CreateTables(cards, effects, Enemy);
            EnemyIntents = new BattleEnemyIntentsData(
                Combatants,
                new[] { Enemy.Id },
                Tables,
                battleSeed: 4321);
            Presentation = new ControllableBattleCommandPresentation();
            Queue = BattleCommandQueueTestFactory.Create(
                Combatants,
                Presentation,
                new Dictionary<CombatantId, BattleCardZonesData>
                {
                    [Player.Id] = Zones,
                },
                enemyCombatantIdsInEncounterOrder: new[] { Enemy.Id },
                energyPerRound: energyPerRound,
                initialHandCount: initialHandCount,
                enemyIntents: EnemyIntents,
                tables: Tables);
            Queue.Submit(new StartBattleCommand());
            Presentation.CompleteNext();
        }

        /// <summary>仅为队列用例建立目标的既有权威状态，仍经正式 Effect module 写入。</summary>
        internal void ExecuteSetupCardEffects(int cardTemplateId, CombatantId targetId)
        {
            cfg.battle.Card card = Tables.TbCard.GetOrDefault(cardTemplateId);
            if (card == null)
                throw new InvalidOperationException($"测试准备卡牌 {cardTemplateId} 不存在。");

            var executor = new BattleEffectExecutor(Tables, Combatants);
            BattleEffectExecutionResult result = executor.Execute(
                new BattleEffectExecutionRequest(
                    Player.Id,
                    targetId,
                    card.EffectBindings));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"测试准备 Effect 执行失败：{result.FailureReason}。");
            }
        }

        /// <summary>按静态模板标识查找当前手牌实例。</summary>
        internal CardInstanceId FindCard(int templateId)
        {
            foreach (CardInstanceId cardId in Zones.Hand)
            {
                if (Zones.Cards[cardId].TemplateId == templateId)
                    return cardId;
            }

            throw new InvalidOperationException($"测试手牌中不存在模板 {templateId}。");
        }

        /// <summary>按所有权逆序释放队列、意图、卡区与参与者。</summary>
        public void Dispose()
        {
            Queue.Dispose();
            EnemyIntents.Dispose();
            Zones.Dispose();
            Combatants.Dispose();
        }

        /// <summary>创建队列、规则和敌人意图共用的最小静态表。</summary>
        private static cfg.Tables CreateTables(
            IEnumerable<JObject> cards,
            IEnumerable<JObject> effects,
            EnemyCombatantData enemy)
        {
            var effectRows = new JArray();
            foreach (JObject effect in effects)
                effectRows.Add(effect);
            effectRows.Add(CreateEffect(
                4999,
                cfg.battle.EffectType.DealDamage,
                cfg.battle.Attribute.None,
                value: 1));

            var data = new Dictionary<string, JArray>
            {
                ["battle_tbhero"] = new JArray(),
                ["battle_tbenemy"] = new JArray(new JObject
                {
                    ["id"] = enemy.TemplateId,
                    ["name_i18n_key"] = "battle.enemy.test.name",
                    ["max_health"] = enemy.MaxHealth,
                    ["base_strength"] = enemy.CurrentStrength,
                    ["view_prefab_address"] = string.Empty,
                    ["behavior_group_id"] = 6001,
                }),
                ["battle_tbdeck"] = new JArray(),
                ["battle_tbcard"] = new JArray(cards),
                ["battle_tbcardeffect"] = effectRows,
                ["battle_tbencounter"] = new JArray(),
                ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                    "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
                ["battle_tbenemybehavior"] = JArray.Parse(
                    "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1,\"effect_id\":4999,\"weight\":1,\"cooldown_selections\":0,\"max_consecutive\":0}]"),
            };
            return new cfg.Tables(tableName => data[tableName]);
        }
    }
}
