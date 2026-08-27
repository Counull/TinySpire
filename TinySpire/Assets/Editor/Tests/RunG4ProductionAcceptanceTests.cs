using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.Run;
using TinySpire.Run.Map;
using UnityEngine.TestTools;

public sealed class RunG4ProductionAcceptanceTests
{
    /// <summary>两名生产 Hero 都必须跨冷启动保留冻结奖励，并让下一战抽到所选 RunCard 实例。</summary>
    [UnityTest]
    public IEnumerator BothProductionHeroes_ColdRestoreFrozenRewardAndDrawSelectedInstanceNextBattle()
    {
        var configs = new ConfigService();
        yield return configs.InitializeAsync(new GeneratedGameDataTextLoader()).ToCoroutine();

        yield return VerifyHeroRewardLoopAsync(configs, heroTemplateId: 1001).ToCoroutine();
        yield return VerifyHeroRewardLoopAsync(configs, heroTemplateId: 1002).ToCoroutine();
    }

    /// <summary>以生产表完成单名 Hero 的胜利、冻结、冷恢复、选择与下一战实例抽取闭环。</summary>
    private static async UniTask VerifyHeroRewardLoopAsync(
        ConfigService configs,
        int heroTemplateId)
    {
        cfg.battle.Hero hero = configs.Tables.TbHero.GetOrDefault(heroTemplateId)
            ?? throw new InvalidOperationException($"生产 Hero {heroTemplateId} 不存在。");
        var saves = new InMemoryRunSaveStore();
        RunCardRewardId frozenRewardId;
        int[] frozenCandidateIds;

        using (var initialStore = new RunStateStore())
        {
            RunFlowService initialFlow = CreateFlow(
                initialStore,
                configs,
                saves,
                heroTemplateId);
            initialFlow.CreateNewRun(heroTemplateId);
            MapNodeId combatNodeId = GetFirstSelectableNodeId(initialStore.Current);
            await initialFlow.EnterMapNodeAsync(combatNodeId);
            BattleSetupOptions firstSetup = initialFlow.CreateBattleSetupOptions();
            RunBattleId battleId = initialFlow.BindBattleAttempt(firstSetup);

            await initialFlow.HandleBattleResultAsync(
                battleId,
                CreateVictoryResult(hero, settledHealth: hero.MaxHealth - 1));

            PendingCardReward pending = initialStore.Current.PendingCardReward;
            Assert.That(initialStore.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.RewardPending));
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.CandidateTemplateIds, Has.Count.EqualTo(3));
            Assert.That(pending.CandidateTemplateIds.Distinct().Count(), Is.EqualTo(3));
            Assert.That(pending.CandidateTemplateIds, Is.SubsetOf(hero.RewardCardTemplateIds));
            frozenRewardId = pending.Id;
            frozenCandidateIds = pending.CandidateTemplateIds.ToArray();
        }

        using (var restoredStore = new RunStateStore())
        {
            RunFlowService restoredFlow = CreateFlow(
                restoredStore,
                configs,
                saves,
                heroTemplateId);
            RunPersistenceState availability = restoredFlow.RefreshSaveAvailability();
            Assert.That(availability.CanContinue, Is.True);
            Assert.That(restoredStore.Current, Is.Null);

            RunState restored = restoredFlow.ContinueSavedRun();
            Assert.That(restored.PendingCardReward.Id, Is.EqualTo(frozenRewardId));
            Assert.That(
                restored.PendingCardReward.CandidateTemplateIds,
                Is.EqualTo(frozenCandidateIds));

            int selectedTemplateId = frozenCandidateIds[1];
            int deckCountBeforeSelection = restored.RunDeck.Cards.Count;
            RunSaveCommitResult settlement = restoredFlow.SettleCardReward(
                frozenRewardId,
                selectedTemplateId);
            Assert.That(settlement.Status, Is.EqualTo(RunSaveCommitStatus.Success));
            Assert.That(restoredStore.Current.RunDeck.Cards, Has.Count.EqualTo(deckCountBeforeSelection + 1));
            RunCard rewardedCard = restoredStore.Current.RunDeck.Cards.Last();
            Assert.That(rewardedCard.TemplateId, Is.EqualTo(selectedTemplateId));
            Assert.That(rewardedCard.UpgradeLevel, Is.Zero);
            Assert.That(
                restoredStore.Current.RunDeck.Cards.Count(card => card.InstanceId == rewardedCard.InstanceId),
                Is.EqualTo(1));

            await EnterNextBattleThroughRequiredNonCombatNodesAsync(
                restoredFlow,
                restoredStore);
            BattleSetupOptions nextSetup = restoredFlow.CreateBattleSetupOptions();
            restoredFlow.BindBattleAttempt(nextSetup);
            using BattleSession nextSession = BattleSession.FromConfig(configs.Tables, nextSetup);
            nextSession.CardZones.Draw(nextSession.CardZones.Cards.Count);

            CardInstanceData[] drawnRewardInstances = nextSession.CardZones.Hand
                .Select(cardId => RequireCard(nextSession.CardZones, cardId))
                .Where(card => card.OriginRunCardInstanceId == rewardedCard.InstanceId)
                .ToArray();
            Assert.That(drawnRewardInstances, Has.Length.EqualTo(1));
            Assert.That(drawnRewardInstances[0].TemplateId, Is.EqualTo(selectedTemplateId));
            Assert.That(drawnRewardInstances[0].UpgradeLevel, Is.Zero);
        }
    }

    /// <summary>按生产地图的类型化语义完成战斗间非战斗节点，直到下一场战斗输入被冻结。</summary>
    private static async UniTask EnterNextBattleThroughRequiredNonCombatNodesAsync(
        RunFlowService flow,
        RunStateStore store)
    {
        const int MaxTraversalSteps = 8;
        for (int step = 0; step < MaxTraversalSteps; step++)
        {
            await flow.EnterMapNodeAsync(GetFirstSelectableNodeId(store.Current));
            if (store.Current.ProgressPhase == RunProgressPhase.InBattle)
                return;

            Assert.That(store.Current.ProgressPhase, Is.EqualTo(RunProgressPhase.NodeVisitPending));
            PendingRunNodeVisit pending = store.Current.PendingNodeVisit;
            Assert.That(pending, Is.Not.Null);

            RunSaveCommitResult result;
            switch (pending.Kind)
            {
                case MapNodeKind.Rest:
                    result = flow.SettleRestHeal(pending.Id);
                    break;
                case MapNodeKind.Chest:
                    result = flow.SettleChestSkip(pending.Id);
                    break;
                case MapNodeKind.Shop:
                    result = flow.SettleShopLeave(pending.Id);
                    break;
                case MapNodeKind.Event:
                    result = flow.SettleEventChoice(
                        pending.Id,
                        RunEventChoiceKind.GainGold);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"下一场战斗前遇到未定义的非战斗节点 {pending.Kind}。");
            }

            Assert.That(result.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        }

        Assert.Fail($"生产地图在 {MaxTraversalSteps} 步内未进入下一场战斗。");
    }

    /// <summary>以确定 Run 身份与根 seed 创建生产 Flow，场景切换使用无副作用测试端口。</summary>
    private static RunFlowService CreateFlow(
        RunStateStore store,
        ConfigService configs,
        IRunSaveStore saves,
        int heroTemplateId)
    {
        Guid runGuid = heroTemplateId == 1001
            ? Guid.Parse("11111111-2222-3333-4444-555555555555")
            : Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa");
        uint randomRootSeed = heroTemplateId == 1001 ? 41001u : 41002u;
        return new RunFlowService(
            store,
            configs,
            new NoOpSceneFlow(),
            new FixedRunEntropySource(new RunEntropy(new RunId(runGuid), randomRootSeed)),
            saves);
    }

    /// <summary>返回当前冻结地图按普通规则排序后的第一个可选节点。</summary>
    private static MapNodeId GetFirstSelectableNodeId(RunState state)
    {
        return MapReachability.GetSelectableNodeIds(
                state.MapDefinition,
                state.CurrentNodeId,
                MapTraversalMode.Ordinary)
            .First();
    }

    /// <summary>冻结一份与指定生产 Hero 匹配的普通战斗胜利结果。</summary>
    private static BattleResult CreateVictoryResult(
        cfg.battle.Hero hero,
        int settledHealth)
    {
        return new BattleResult(
            BattleResultKind.Victory,
            authoritySequence: 1,
            roundNumber: 1,
            new[]
            {
                new BattleResultPlayerSnapshot(
                    new CombatantId(1),
                    hero.Id,
                    settledHealth,
                    hero.MaxHealth),
            });
    }

    /// <summary>按战斗实例 ID 读取不可变卡牌数据，缺失时让验收立即失败。</summary>
    private static CardInstanceData RequireCard(
        BattleCardZonesData cardZones,
        CardInstanceId cardId)
    {
        if (!cardZones.TryGetCard(cardId, out CardInstanceData card))
            throw new InvalidOperationException($"战斗卡牌实例 {cardId} 不存在。");

        return card;
    }

    /// <summary>为生产配置验收返回固定且可复现的 Run 随机根输入。</summary>
    private sealed class FixedRunEntropySource : IRunEntropySource
    {
        private readonly RunEntropy _entropy;

        /// <summary>保存本次验收应使用的确定 Run 身份与根 seed。</summary>
        internal FixedRunEntropySource(RunEntropy entropy)
        {
            _entropy = entropy;
        }

        /// <summary>返回已冻结的 Run 随机根输入。</summary>
        public RunEntropy Next()
        {
            return _entropy;
        }
    }

    /// <summary>只完成场景编排 await，不加载或修改任何真实场景。</summary>
    private sealed class NoOpSceneFlow : ISceneFlowService
    {
        /// <summary>保持生产 Flow 的异步边界，同时不执行真实场景切换。</summary>
        public UniTask LoadSceneWithLoadingAsync(string targetSceneAddress)
        {
            return UniTask.CompletedTask;
        }
    }

    /// <summary>从当前生成目录异步读取生产 GameData 文本。</summary>
    private sealed class GeneratedGameDataTextLoader : IConfigTextLoader
    {
        /// <summary>读取 ConfigService 请求的稳定 Assets/GameData 地址。</summary>
        public async UniTask<string> LoadTextAsync(string address)
        {
            await UniTask.Yield();
            return File.ReadAllText(address);
        }
    }
}
