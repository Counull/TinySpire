using System;
using System.IO;
using System.Linq;
using cfg.battle;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Run;

/// <summary>验证 G4 普通战斗卡牌奖励的纯领域规则。</summary>
public sealed class RunCardRewardG4Tests
{
    /// <summary>奖励附着掉落必须显式表达 Empty，并只接受固定样本域中的正数模板。</summary>
    [Test]
    public void AttachedLoot_EmptyAndPositiveTemplates_AreImmutableDomainFacts()
    {
        RunCardRewardAttachedLoot empty = RunCardRewardAttachedLoot.Empty;
        var attached = new RunCardRewardAttachedLoot(
            RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattleRelic,
            RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattlePotion);
        var rewardId = new RunCardRewardId(new RunBattleId(
            new RunId(Guid.Parse("11000000-0000-0000-0000-000000000001")),
            attemptSequence: 1,
            nodeId: new TinySpire.Run.Map.MapNodeId("L01-S00")));
        var pending = new PendingCardReward(
            rewardId,
            new[] { 3105, 3123, 3157 },
            attached);

        Assert.That(empty, Is.Not.Null);
        Assert.That(empty.RelicTemplateId, Is.Null);
        Assert.That(empty.PotionTemplateId, Is.Null);
        Assert.That(
            RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattleRelic,
            Is.EqualTo(8001));
        Assert.That(
            RunCardRewardAttachedLootTemplateIds.FirstOrdinaryBattlePotion,
            Is.EqualTo(9001));
        Assert.That(pending.AttachedLoot, Is.Not.Null);
        Assert.That(pending.AttachedLoot.RelicTemplateId, Is.EqualTo(8001));
        Assert.That(pending.AttachedLoot.PotionTemplateId, Is.EqualTo(9001));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RunCardRewardAttachedLoot(relicTemplateId: 0, potionTemplateId: null));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RunCardRewardAttachedLoot(relicTemplateId: null, potionTemplateId: -1));
    }

    /// <summary>同一 Hero 奖励池与 seed 必须冻结相同顺序的三个不同模板。</summary>
    [Test]
    public void Generate_WithLegalPool_FreezesThreeDistinctTemplatesDeterministically()
    {
        var pool = new HeroCardRewardPool(
            heroTemplateId: 1001,
            new CardRewardRarityWeights(commonWeight: 60, uncommonWeight: 37, rareWeight: 3),
            new[]
            {
                new CardRewardCandidate(3105, CardRarity.Common),
                new CardRewardCandidate(3108, CardRarity.Common),
                new CardRewardCandidate(3123, CardRarity.Uncommon),
                new CardRewardCandidate(3157, CardRarity.Rare),
            });
        var rewardId = new RunCardRewardId(new RunBattleId(
            new RunId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
            attemptSequence: 1,
            nodeId: new TinySpire.Run.Map.MapNodeId("L1N0")));

        PendingCardReward first = RunCardRewardGenerator.Generate(rewardId, pool, seed: 2468u);
        PendingCardReward second = RunCardRewardGenerator.Generate(rewardId, pool, seed: 2468u);

        Assert.That(first.Id, Is.EqualTo(rewardId));
        Assert.That(first.CandidateTemplateIds.Count, Is.EqualTo(3));
        Assert.That(first.CandidateTemplateIds.Distinct().Count(), Is.EqualTo(3));
        Assert.That(second.CandidateTemplateIds, Is.EqualTo(first.CandidateTemplateIds));
        Assert.That(first.AttachedLoot, Is.Not.Null);
        Assert.That(first.AttachedLoot.RelicTemplateId, Is.Null);
        Assert.That(first.AttachedLoot.PotionTemplateId, Is.Null);
    }

    /// <summary>Reward seed 派生必须稳定、按 attempt 分离且不复用 Map/Battle 域。</summary>
    [Test]
    public void DeriveRewardSeed_SeparatesRewardFromOtherRandomDomains()
    {
        const uint rootSeed = 123456789u;

        uint first = RunRandomDomains.DeriveRewardSeed(rootSeed, attemptSequence: 1);
        uint repeated = RunRandomDomains.DeriveRewardSeed(rootSeed, attemptSequence: 1);
        uint nextAttempt = RunRandomDomains.DeriveRewardSeed(rootSeed, attemptSequence: 2);

        Assert.That(first, Is.Not.Zero);
        Assert.That(repeated, Is.EqualTo(first));
        Assert.That(nextAttempt, Is.Not.EqualTo(first));
        Assert.That(first, Is.Not.EqualTo(RunRandomDomains.DeriveMapSeed(rootSeed)));
        Assert.That(first, Is.Not.EqualTo(RunStateStore.DeriveBattleSeed(rootSeed, 1)));
    }

    /// <summary>零权重档不能充当三张奖励所需的可抽模板数量。</summary>
    [Test]
    public void Constructor_WithFewerThanThreeWeightedTemplates_RejectsBeforeGeneration()
    {
        Assert.Throws<ArgumentException>(() => new HeroCardRewardPool(
            heroTemplateId: 1001,
            new CardRewardRarityWeights(commonWeight: 60, uncommonWeight: 37, rareWeight: 0),
            new[]
            {
                new CardRewardCandidate(3105, CardRarity.Common),
                new CardRewardCandidate(3108, CardRarity.Common),
                new CardRewardCandidate(3157, CardRarity.Rare),
            }));
    }

    /// <summary>生产适配器必须按 Hero 显式数组建立两个不相交池，并排除 3263 临时卡。</summary>
    [Test]
    public void TablesCatalog_CreatesTwoIndependentProductionPools()
    {
        cfg.Tables tables = LoadProductionTables();

        HeroCardRewardPool ironclad = TablesHeroCardRewardPoolCatalog.Create(tables, 1001);
        HeroCardRewardPool machineGunner = TablesHeroCardRewardPoolCatalog.Create(tables, 1002);

        Assert.That(ironclad.Candidates.Count, Is.EqualTo(12));
        Assert.That(machineGunner.Candidates.Count, Is.EqualTo(76));
        Assert.That(ironclad.RarityWeights.CommonWeight, Is.EqualTo(60));
        Assert.That(ironclad.RarityWeights.UncommonWeight, Is.EqualTo(37));
        Assert.That(ironclad.RarityWeights.RareWeight, Is.EqualTo(3));
        Assert.That(machineGunner.RarityWeights.CommonWeight, Is.EqualTo(60));
        Assert.That(machineGunner.RarityWeights.UncommonWeight, Is.EqualTo(37));
        Assert.That(machineGunner.RarityWeights.RareWeight, Is.EqualTo(3));
        Assert.That(
            machineGunner.Candidates.Any(candidate => candidate.TemplateId == 3263),
            Is.False);
        Assert.That(
            ironclad.Candidates.Select(candidate => candidate.TemplateId)
                .Intersect(machineGunner.Candidates.Select(candidate => candidate.TemplateId)),
            Is.Empty);
    }

    /// <summary>从当前生成 Hero/Card JSON 建立仅供生产池适配器测试的 Luban Tables。</summary>
    private static cfg.Tables LoadProductionTables()
    {
        JObject heroes = JObject.Parse(File.ReadAllText("Assets/GameData/battle_tbhero.json"));
        JObject cards = JObject.Parse(File.ReadAllText("Assets/GameData/battle_tbcard.json"));
        return new cfg.Tables(tableName => tableName switch
        {
            "battle_tbhero" => new JArray(heroes.Properties().Select(property => property.Value)),
            "battle_tbcard" => new JArray(cards.Properties().Select(property => property.Value)),
            _ => new JArray(),
        });
    }
}
