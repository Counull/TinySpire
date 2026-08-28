using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Infrastructure.Persistence;
using TinySpire.Run;
using TinySpire.Run.Map;

public sealed class AtomicJsonRunSaveStoreTests
{
    private string _testDirectory;

    /// <summary>为每个测试建立独立的系统临时目录，避免与玩家真实存档交叉。</summary>
    [SetUp]
    public void SetUp()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "TinySpire.AtomicJsonRunSaveStoreTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    /// <summary>只清理当前测试明确创建的临时目录。</summary>
    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrWhiteSpace(_testDirectory) && Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, recursive: true);
    }

    /// <summary>真实临时目录中的单槽存档可完成提交、读取与幂等删除闭环。</summary>
    [Test]
    public void CommitLoadDelete_RealTemporaryDirectory_RoundTripsSingleSlot()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument document = CreateDocument(
            "11111111-aaaa-bbbb-cccc-222222222222",
            RunSaveProgressPhase.MapReady,
            currentHealth: 73);

        RunSaveCommitResult commit = store.Commit(document);
        RunSaveLoadResult load = store.Load();
        RunSaveDeleteResult delete = store.Delete();
        RunSaveLoadResult afterDelete = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.RunId, Is.EqualTo(document.RunId));
        Assert.That(load.Document.CurrentHealth, Is.EqualTo(73));
        Assert.That(delete.Status, Is.EqualTo(RunSaveDeleteStatus.Success));
        Assert.That(afterDelete.Status, Is.EqualTo(RunSaveLoadStatus.NotFound));
    }

    /// <summary>冻结奖励检查点必须原样保存奖励身份与三个有序候选。</summary>
    [Test]
    public void CommitLoad_RewardPending_PreservesRewardIdentityAndCandidateOrder()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument document = CreateDocument(
            "18181818-aaaa-bbbb-cccc-282828282828",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 41);

        RunSaveCommitResult commit = CommitRewardPendingFromStablePredecessor(store, document);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(load.Document.PendingCardReward.RewardId,
            Is.EqualTo(document.PendingCardReward.RewardId));
        Assert.That(load.Document.PendingCardReward.CandidateTemplateIds,
            Is.EqualTo(new[] { 3105, 3123, 3157 }));
        Assert.That(File.Exists(GetRewardIntentPath()), Is.True);
    }

    /// <summary>没有可验证 live MapReady 前驱时不得把任意 RewardPending 作为首个检查点写入单槽。</summary>
    [Test]
    public void Commit_RewardPendingWithoutLivePredecessor_ReturnsInvalidDocument()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateDocument(
            "18181818-bbbb-cccc-dddd-282828282828",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 41);

        RunSaveCommitResult commit = store.Commit(pending);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.NotFound));
        Assert.That(File.Exists(GetRewardIntentPath()), Is.False);
    }

    /// <summary>冻结奖励在通用临时档写入前失败时，冷启动仍必须从 durable intent 恢复同一候选。</summary>
    [Test]
    public void Commit_RewardPendingTemporaryWriteFails_ColdLoadReturnsFrozenIntent()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "19191919-aaaa-bbbb-cccc-292929292929",
            RunSaveProgressPhase.MapReady,
            currentHealth: 80);
        Assert.That(initialStore.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument pending = CreateDocument(
            oldLive.RunId,
            RunSaveProgressPhase.RewardPending,
            currentHealth: 43);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new TemporaryWriteFailingFileSystem(GetTemporaryPath()));

        RunSaveCommitResult commit = failingStore.Commit(pending);
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(File.Exists(GetRewardIntentPath()), Is.True);
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(coldLoad.Document.PendingCardReward.RewardId,
            Is.EqualTo(pending.PendingCardReward.RewardId));
        Assert.That(coldLoad.Document.PendingCardReward.CandidateTemplateIds,
            Is.EqualTo(pending.PendingCardReward.CandidateTemplateIds));
    }

    /// <summary>源 MapReady 与 durable Pending 的持有物若漂移，冷启动必须拒绝猜测哪份可恢复。</summary>
    [Test]
    public void Load_RewardPendingIntentWithHoldingsDrift_ReturnsInterruptedCommit()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "19191919-cccc-dddd-eeee-292929292929",
            RunSaveProgressPhase.MapReady,
            currentHealth: 80);
        Assert.That(initialStore.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument pending = CreateDocument(
            oldLive.RunId,
            RunSaveProgressPhase.RewardPending,
            currentHealth: 43);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new TemporaryWriteFailingFileSystem(GetTemporaryPath()));

        RunSaveCommitResult commit = failingStore.Commit(pending);
        RunSaveDocument driftedLive = CreateDocumentWithHoldingsDrift(
            oldLive,
            HoldingsDrift.Gold);
        File.WriteAllText(GetLivePath(), RunSaveDocumentCodec.Serialize(driftedLive));
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(File.Exists(GetRewardIntentPath()), Is.True);
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.InterruptedCommit));
        Assert.That(coldLoad.Document, Is.Null);
        Assert.That(coldLoad.Detail, Does.Contain("conflict").IgnoreCase);
    }

    /// <summary>首次奖励意图可只移除已消费药水，并在替换失败后按原相对顺序冷恢复。</summary>
    [Test]
    public void Load_RewardPendingIntentWithPotionSubsequence_RecoversFrozenPending()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "19191919-dddd-eeee-ffff-292929292929",
            RunSaveProgressPhase.MapReady,
            currentHealth: 80);
        Assert.That(initialStore.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument pending = CreateDocumentWithRemovedPotion(
            CreateDocument(
                oldLive.RunId,
                RunSaveProgressPhase.RewardPending,
                currentHealth: 43),
            removedInstanceId: 1);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new TemporaryWriteFailingFileSystem(GetTemporaryPath()));

        RunSaveCommitResult commit = failingStore.Commit(pending);
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(
            coldLoad.Document.Potions.Select(potion => potion.InstanceId),
            Is.EqualTo(new[] { 2 }));
        Assert.That(coldLoad.Document.Gold, Is.EqualTo(oldLive.Gold));
        Assert.That(coldLoad.Document.Relics.Select(relic => relic.InstanceId),
            Is.EqualTo(oldLive.Relics.Select(relic => relic.InstanceId)));
    }

    /// <summary>战斗内回血后即使临时档写入失败，冷启动也必须以 durable intent 恢复同一奖励。</summary>
    [Test]
    public void Commit_HealedRewardPendingTemporaryWriteFails_ColdLoadReturnsFrozenIntent()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "19191919-bbbb-cccc-dddd-292929292929",
            RunSaveProgressPhase.MapReady,
            currentHealth: 20);
        Assert.That(initialStore.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument pending = CreateDocument(
            oldLive.RunId,
            RunSaveProgressPhase.RewardPending,
            currentHealth: 30);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new TemporaryWriteFailingFileSystem(GetTemporaryPath()));

        RunSaveCommitResult commit = failingStore.Commit(pending);
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(coldLoad.Document.CurrentHealth, Is.EqualTo(30));
        Assert.That(coldLoad.Document.PendingCardReward.RewardId,
            Is.EqualTo(pending.PendingCardReward.RewardId));
    }

    /// <summary>战斗内回血后的首次 RewardPending 若正式替换失败，冷启动仍必须恢复源 Pending。</summary>
    [Test]
    public void Commit_HealedRewardPendingReplacementFails_ColdLoadReturnsFrozenIntent()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "19191919-cccc-dddd-eeee-292929292929",
            RunSaveProgressPhase.MapReady,
            currentHealth: 20);
        Assert.That(initialStore.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument pending = CreateDocument(
            oldLive.RunId,
            RunSaveProgressPhase.RewardPending,
            currentHealth: 30);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem());

        RunSaveCommitResult commit = failingStore.Commit(pending);
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(coldLoad.Document.CurrentHealth, Is.EqualTo(30));
        Assert.That(File.Exists(GetRewardIntentPath()), Is.True);
    }

    /// <summary>奖励结算 Replace 失败时必须恢复源 Pending，不能发布未成功替换的选择结果。</summary>
    [Test]
    public void Commit_RewardSettlementReplacementFails_ColdLoadReturnsSourcePending()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateDocument(
            "1a1a1a1a-aaaa-bbbb-cccc-2a2a2a2a2a2a",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 39);
        Assert.That(
            CommitRewardPendingFromStablePredecessor(initialStore, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument settled = CreateSettledRewardDocument(pending, selectedTemplateId: 3123);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem());

        RunSaveCommitResult commit = failingStore.Commit(settled);
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(coldLoad.Document.PendingCardReward.RewardId,
            Is.EqualTo(pending.PendingCardReward.RewardId));
        Assert.That(File.Exists(GetRewardIntentPath()), Is.True);
    }

    /// <summary>奖励附着掉落只允许按冻结模板与最大实例号加一精确追加到两个持有物末尾。</summary>
    [Test]
    public void Commit_RewardSettlementWithAttachedLoot_AppendsExactTailsAndLoadsSettledLive()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateDocument(
            "1a2a1a2a-aaaa-bbbb-cccc-2a3a2a3a2a3a",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 41,
            attachedRelicTemplateId: 8001,
            attachedPotionTemplateId: 9001);
        Assert.That(
            CommitRewardPendingFromStablePredecessor(store, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument settled = CreateSettledRewardDocument(pending, selectedTemplateId: null);

        RunSaveCommitResult commit = store.Commit(settled);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.Relics.Select(relic =>
                (relic.InstanceId, relic.TemplateId)),
            Is.EqualTo(new[] { (1, 4101), (2, 4102), (3, 8001) }));
        Assert.That(load.Document.Potions.Select(potion =>
                (potion.InstanceId, potion.TemplateId)),
            Is.EqualTo(new[] { (1, 5101), (2, 5102), (3, 9001) }));
    }

    /// <summary>附着掉落的少发、多发、错模板、错身份或插入前缀都必须保留源 Pending。</summary>
    [TestCase(AttachedLootSettlementDrift.MissingRelicTail)]
    [TestCase(AttachedLootSettlementDrift.ExtraRelicTail)]
    [TestCase(AttachedLootSettlementDrift.WrongRelicInstanceId)]
    [TestCase(AttachedLootSettlementDrift.WrongRelicTemplateId)]
    [TestCase(AttachedLootSettlementDrift.RelicInsertedBeforePrefix)]
    [TestCase(AttachedLootSettlementDrift.MissingPotionTail)]
    [TestCase(AttachedLootSettlementDrift.WrongPotionInstanceId)]
    [TestCase(AttachedLootSettlementDrift.WrongPotionTemplateId)]
    [TestCase(AttachedLootSettlementDrift.PotionInsertedBeforePrefix)]
    public void Commit_RewardSettlementWithAttachedLootDrift_ReturnsInvalidDocument(
        AttachedLootSettlementDrift drift)
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateDocument(
            "1a3a1a3a-aaaa-bbbb-cccc-2a4a2a4a2a4a",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 42,
            attachedRelicTemplateId: 8001,
            attachedPotionTemplateId: 9001);
        Assert.That(
            CommitRewardPendingFromStablePredecessor(store, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument drifted = CreateAttachedLootSettlementDrift(
            CreateSettledRewardDocument(pending, selectedTemplateId: null),
            drift);

        RunSaveCommitResult commit = store.Commit(drifted);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(load.Document.PendingCardReward.RewardId,
            Is.EqualTo(pending.PendingCardReward.RewardId));
    }

    /// <summary>某一掉落模板未冻结时，对应持有物必须全等，不能借另一类合法追加夹带实例。</summary>
    [Test]
    public void Commit_RewardSettlementWithUnattachedPotionAppend_ReturnsInvalidDocument()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateDocument(
            "1a4a1a4a-aaaa-bbbb-cccc-2a5a2a5a2a5a",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 43,
            attachedRelicTemplateId: 8001,
            attachedPotionTemplateId: null);
        Assert.That(
            CommitRewardPendingFromStablePredecessor(store, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument settled = CreateSettledRewardDocument(pending, selectedTemplateId: null);
        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(settled));
        ((JArray)raw["potions"]).Add(new JObject
        {
            ["instanceId"] = 3,
            ["templateId"] = 9001,
        });
        RunSaveDocument drifted = RequireParseableDocument(raw, "unattached potion append");

        RunSaveCommitResult commit = store.Commit(drifted);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
    }

    /// <summary>伪造满三槽却仍冻结药水的 Pending 时，结算不得少发或越过容量追加。</summary>
    [Test]
    public void Commit_RewardSettlementWithAttachedPotionAtFullCapacity_ReturnsInvalidDocument()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        JObject pendingRaw = JObject.Parse(RunSaveDocumentCodec.Serialize(CreateDocument(
            "1a5a1a5a-aaaa-bbbb-cccc-2a6a2a6a2a6a",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 44,
            attachedRelicTemplateId: null,
            attachedPotionTemplateId: 9001)));
        ((JArray)pendingRaw["potions"]).Add(new JObject
        {
            ["instanceId"] = 3,
            ["templateId"] = 5103,
        });
        RunSaveDocument pending = RequireParseableDocument(pendingRaw, "full potion pending");
        Assert.That(
            CommitRewardPendingFromStablePredecessor(store, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument missingTail = CreateRewardSettlementWithoutAttachedLoot(pending);

        RunSaveCommitResult commit = store.Commit(missingTail);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
    }

    /// <summary>来源最大遗物实例号为 int.MaxValue 时不存在合法尾项，任何可解析替代号都必须拒绝。</summary>
    [Test]
    public void Commit_RewardSettlementWithRelicInstanceOverflow_ReturnsInvalidDocument()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        JObject pendingRaw = JObject.Parse(RunSaveDocumentCodec.Serialize(CreateDocument(
            "1a6a1a6a-aaaa-bbbb-cccc-2a7a2a7a2a7a",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 45,
            attachedRelicTemplateId: 8001,
            attachedPotionTemplateId: null)));
        pendingRaw["relics"][1]["instanceId"] = int.MaxValue;
        RunSaveDocument pending = RequireParseableDocument(pendingRaw, "overflow relic pending");
        Assert.That(
            CommitRewardPendingFromStablePredecessor(store, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        JObject targetRaw = JObject.Parse(RunSaveDocumentCodec.Serialize(
            CreateRewardSettlementWithoutAttachedLoot(pending)));
        ((JArray)targetRaw["relics"]).Add(new JObject
        {
            ["instanceId"] = 2,
            ["templateId"] = 8001,
        });
        RunSaveDocument target = RequireParseableDocument(targetRaw, "overflow relic settlement");

        RunSaveCommitResult commit = store.Commit(target);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
    }

    /// <summary>卡牌奖励结算前后任何持有物字段都不得变化，否则必须保留源 Pending。</summary>
    [TestCase(HoldingsDrift.Gold)]
    [TestCase(HoldingsDrift.RelicOrder)]
    [TestCase(HoldingsDrift.RelicInstanceId)]
    [TestCase(HoldingsDrift.RelicTemplateId)]
    [TestCase(HoldingsDrift.PotionOrder)]
    [TestCase(HoldingsDrift.PotionInstanceId)]
    [TestCase(HoldingsDrift.PotionTemplateId)]
    public void Commit_RewardSettlementWithHoldingsDrift_ReturnsInvalidDocument(
        HoldingsDrift drift)
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateDocument(
            "1b1b1b1b-aaaa-bbbb-cccc-2b2b2b2b2b2b",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 47);
        Assert.That(
            CommitRewardPendingFromStablePredecessor(store, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument driftedSettlement = CreateDocumentWithHoldingsDrift(
            CreateSettledRewardDocument(pending, selectedTemplateId: null),
            drift);

        RunSaveCommitResult commit = store.Commit(driftedSettlement);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(load.Document.Relics.Select(relic => relic.InstanceId),
            Is.EqualTo(pending.Relics.Select(relic => relic.InstanceId)));
        Assert.That(load.Document.Potions.Select(potion => potion.InstanceId),
            Is.EqualTo(pending.Potions.Select(potion => potion.InstanceId)));
        Assert.That(load.Document.Gold, Is.EqualTo(pending.Gold));
    }

    /// <summary>奖励结算正式发布成功后必须清除源 intent，并只保留选择后的 MapReady 正式档。</summary>
    [Test]
    public void Commit_RewardSettlementSucceeds_DeletesSourceIntentAndLoadsSettledLive()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateDocument(
            "1d1d1d1d-aaaa-bbbb-cccc-2d2d2d2d2d2d",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 58);
        Assert.That(
            CommitRewardPendingFromStablePredecessor(store, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument settled = CreateSettledRewardDocument(pending, selectedTemplateId: null);

        RunSaveCommitResult commit = store.Commit(settled);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(File.Exists(GetRewardIntentPath()), Is.False);
        Assert.That(File.Exists(GetTemporaryPath()), Is.False);
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(load.Document.PathNodeIds, Is.EqualTo(settled.PathNodeIds));
        Assert.That(load.Document.RunCards.Select(card => card.InstanceId),
            Is.EqualTo(pending.RunCards.Select(card => card.InstanceId)));
    }

    /// <summary>正式结算已替换但 intent 清理失败时，冷启动必须识别合法后继且不再次给奖励。</summary>
    [Test]
    public void Commit_RewardSettlementIntentDeleteFails_ColdLoadReturnsSettledLive()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateDocument(
            "1b1b1b1b-aaaa-bbbb-cccc-2b2b2b2b2b2b",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 51);
        Assert.That(
            CommitRewardPendingFromStablePredecessor(initialStore, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument settled = CreateSettledRewardDocument(pending, selectedTemplateId: 3105);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new RewardIntentDeleteFailingFileSystem(GetRewardIntentPath()));

        RunSaveCommitResult commit = failingStore.Commit(settled);
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(coldLoad.Document.PathNodeIds,
            Is.EqualTo(settled.PathNodeIds));
        Assert.That(coldLoad.Document.RunCards.Select(card => card.InstanceId),
            Is.EqualTo(settled.RunCards.Select(card => card.InstanceId)));
        Assert.That(coldLoad.Document.PendingCardReward, Is.Null);
    }

    /// <summary>结算已发布但旧 intent 清理失败时，下一检查点须先安全收尾旧 intent，失败可重试且不能覆盖 live。</summary>
    [Test]
    public void Commit_ResidualSettledRewardIntent_BlocksThenAllowsNextRewardCheckpointRetry()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateDocument(
            "1b1b1b1b-bbbb-cccc-dddd-2b2b2b2b2b2b",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 51);
        Assert.That(
            CommitRewardPendingFromStablePredecessor(initialStore, pending).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument settled = CreateSettledRewardDocument(pending, selectedTemplateId: 3105);
        var cleanupFailingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new RewardIntentDeleteFailingFileSystem(GetRewardIntentPath()));
        Assert.That(
            cleanupFailingStore.Commit(settled).Status,
            Is.EqualTo(RunSaveCommitStatus.IoFailure));
        byte[] settledLiveBytes = File.ReadAllBytes(GetLivePath());
        RunSaveDocument nextPending = CreateNextPendingRewardDocument(settled);

        RunSaveCommitResult blocked = cleanupFailingStore.Commit(nextPending);

        Assert.That(blocked.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(File.ReadAllBytes(GetLivePath()), Is.EqualTo(settledLiveBytes));
        Assert.That(File.Exists(GetRewardIntentPath()), Is.True);

        RunSaveCommitResult retry = new AtomicJsonRunSaveStore(_testDirectory).Commit(nextPending);
        RunSaveLoadResult load = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(retry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.RewardPending));
        Assert.That(load.Document.PendingCardReward.RewardId,
            Is.EqualTo(nextPending.PendingCardReward.RewardId));
    }

    /// <summary>损坏奖励 intent 且磁盘仅剩战前 MapReady 时无法唯一判定候选，必须 fail-closed。</summary>
    [Test]
    public void Load_CorruptRewardIntentWithOldMapReadyLive_ReturnsInterruptedCommit()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "1c1c1c1c-aaaa-bbbb-cccc-2c2c2c2c2c2c",
            RunSaveProgressPhase.MapReady,
            currentHealth: 80);
        Assert.That(store.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        File.WriteAllText(GetRewardIntentPath(), "{");

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.InterruptedCommit));
        Assert.That(load.Document, Is.Null);
        Assert.That(load.HasPendingTemporaryFile, Is.True);
        Assert.That(load.Detail, Does.Contain("reward intent"));
    }

    /// <summary>真实文件系统的第二次提交必须通过 replace 发布 S1，并移除提交临时文件。</summary>
    [Test]
    public void Commit_SecondCheckpoint_RealTemporaryDirectory_ReplacesLiveFile()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument initial = CreateDocument(
            "10101010-aaaa-bbbb-cccc-202020202020",
            RunSaveProgressPhase.MapReady,
            currentHealth: 80);
        RunSaveDocument completed = CreateDocument(
            "10101010-aaaa-bbbb-cccc-202020202020",
            RunSaveProgressPhase.MapReady,
            currentHealth: 23);

        Assert.That(store.Commit(initial).Status, Is.EqualTo(RunSaveCommitStatus.Success));

        RunSaveCommitResult replacement = store.Commit(completed);
        RunSaveLoadResult load = store.Load();

        Assert.That(replacement.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(load.Document.CurrentHealth, Is.EqualTo(23));
        Assert.That(File.Exists(GetTemporaryPath()), Is.False);
    }

    /// <summary>首次 move 发布失败时必须保留完整临时档且不能产生正式档。</summary>
    [Test]
    public void Commit_FirstMoveFails_LeavesValidatedTemporaryFileWithoutLiveFile()
    {
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new MoveFailingFileSystem());
        RunSaveDocument document = CreateDocument(
            "30303030-aaaa-bbbb-cccc-404040404040",
            RunSaveProgressPhase.MapReady,
            currentHealth: 80);

        RunSaveCommitResult commit = store.Commit(document);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(File.Exists(GetLivePath()), Is.False);
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetTemporaryPath())).Status,
            Is.EqualTo(RunSaveDocumentReadStatus.Success));
    }

    /// <summary>已确认删除遇到 IO 故障时必须返回 typed failure 并保留正式档。</summary>
    [Test]
    public void Delete_FileSystemFails_ReturnsIoFailureAndPreservesLiveFile()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        Assert.That(
            initialStore.Commit(CreateDocument(
                "50505050-aaaa-bbbb-cccc-606060606060",
                RunSaveProgressPhase.MapReady,
                currentHealth: 80)).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new DeleteFailingFileSystem());

        RunSaveDeleteResult delete = failingStore.Delete();

        Assert.That(delete.Status, Is.EqualTo(RunSaveDeleteStatus.IoFailure));
        Assert.That(delete.Detail, Is.Not.Empty);
        Assert.That(File.Exists(GetLivePath()), Is.True);
    }

    /// <summary>已有有效正式档时替换失败必须报告 IO 故障，并逐字节保留旧档。</summary>
    [Test]
    public void Commit_ReplacementFails_PreservesByteIdenticalOldLiveFile()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldDocument = CreateDocument(
            "33333333-aaaa-bbbb-cccc-444444444444",
            RunSaveProgressPhase.MapReady,
            currentHealth: 80);
        Assert.That(
            initialStore.Commit(oldDocument).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        string livePath = GetLivePath();
        byte[] oldBytes = File.ReadAllBytes(livePath);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem());
        RunSaveDocument newDocument = CreateDocument(
            "55555555-aaaa-bbbb-cccc-666666666666",
            RunSaveProgressPhase.MapReady,
            currentHealth: 29);

        RunSaveCommitResult commit = failingStore.Commit(newDocument);
        byte[] currentBytes = File.ReadAllBytes(livePath);
        RunSaveLoadResult load = initialStore.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(currentBytes, Is.EqualTo(oldBytes));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.RunId, Is.EqualTo(oldDocument.RunId));
        Assert.That(load.HasPendingTemporaryFile, Is.True);
    }

    /// <summary>Rest 结算替换失败后冷启动仍恢复同一 Pending，重试同一后继才发布治疗与单次路径追加。</summary>
    [Test]
    public void Commit_RestSettlementReplacementFailsThenExactRetry_PublishesSameSuccessor()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateNodeVisitDocument(
            "34343434-aaaa-bbbb-cccc-565656565656",
            MapNodeKind.Rest);
        Assert.That(initialStore.Commit(pending).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument successor = CreateRestHealSettlementDocument(pending);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem());

        RunSaveCommitResult failed = failingStore.Commit(successor);
        RunSaveLoadResult coldPending = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldPending.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldPending.HasPendingTemporaryFile, Is.True);
        Assert.That(coldPending.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.NodeVisitPending));
        Assert.That(coldPending.Document.CurrentHealth, Is.EqualTo(68));
        Assert.That(coldPending.Document.PathNodeIds, Is.EqualTo(pending.PathNodeIds));
        Assert.That(coldPending.Document.PendingNodeVisit.VisitId,
            Is.EqualTo(pending.PendingNodeVisit.VisitId));
        Assert.That(coldPending.Document.PendingNodeVisit.RestPayload.HealAmount, Is.EqualTo(24));

        var retryStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveCommitResult retry = retryStore.Commit(successor);
        RunSaveLoadResult coldSuccessor = retryStore.Load();

        Assert.That(retry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(coldSuccessor.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldSuccessor.HasPendingTemporaryFile, Is.False);
        Assert.That(coldSuccessor.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(coldSuccessor.Document.CurrentHealth, Is.EqualTo(80));
        Assert.That(coldSuccessor.Document.PendingNodeVisit, Is.Null);
        Assert.That(
            coldSuccessor.Document.PathNodeIds,
            Is.EqualTo(pending.PathNodeIds.Concat(new[] { pending.PendingNodeVisit.NodeId })));
        Assert.That(coldSuccessor.Document.RunCards.Select(card => card.UpgradeLevel),
            Is.EqualTo(pending.RunCards.Select(card => card.UpgradeLevel)));
    }

    /// <summary>Chest 领取或跳过替换失败都保留源 Pending，重试同一后继才发布持有物与单次路径完成。</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Commit_ChestSettlementReplacementFailsThenExactRetry_PublishesSameSuccessor(
        bool claim)
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateNodeVisitDocument(
            claim
                ? "35353535-aaaa-bbbb-cccc-575757575751"
                : "35353535-aaaa-bbbb-cccc-575757575752",
            MapNodeKind.Chest);
        Assert.That(initialStore.Commit(pending).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument successor = CreateChestSettlementDocument(pending, claim);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem());

        RunSaveCommitResult failed = failingStore.Commit(successor);
        RunSaveLoadResult coldPending = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldPending.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldPending.HasPendingTemporaryFile, Is.True);
        Assert.That(coldPending.Document.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.NodeVisitPending));
        Assert.That(coldPending.Document.PendingNodeVisit.VisitId,
            Is.EqualTo(pending.PendingNodeVisit.VisitId));
        Assert.That(coldPending.Document.Potions.Select(potion => potion.InstanceId),
            Is.EqualTo(new[] { 1, 2 }));

        var retryStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveCommitResult retry = retryStore.Commit(successor);
        RunSaveLoadResult coldSuccessor = retryStore.Load();

        Assert.That(retry.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(coldSuccessor.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldSuccessor.HasPendingTemporaryFile, Is.False);
        Assert.That(coldSuccessor.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(coldSuccessor.Document.PendingNodeVisit, Is.Null);
        Assert.That(coldSuccessor.Document.PathNodeIds,
            Is.EqualTo(pending.PathNodeIds.Concat(new[] { pending.PendingNodeVisit.NodeId })));
        Assert.That(coldSuccessor.Document.Potions.Select(potion => potion.InstanceId),
            Is.EqualTo(claim ? new[] { 1, 2, 3 } : new[] { 1, 2 }));
        Assert.That(coldSuccessor.Document.Potions.Last().TemplateId,
            Is.EqualTo(claim ? pending.PendingNodeVisit.ChestPayload.PotionTemplateId : 5102));
    }

    /// <summary>Shop 购买与 Leave 的原子替换失败都保留精确 live，重试后才依次发布已购 Pending 与单次路径完成。</summary>
    [Test]
    public void Commit_ShopPurchaseAndLeaveReplacementFailThenExactRetry_PublishEachSuccessor()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateNodeVisitDocument(
            "36363636-aaaa-bbbb-cccc-585858585858",
            MapNodeKind.Shop);
        Assert.That(initialStore.Commit(pending).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument purchased = CreateShopCardPurchaseDocument(pending);

        RunSaveCommitResult purchaseFailed = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem()).Commit(purchased);
        RunSaveLoadResult coldOriginal = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(purchaseFailed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldOriginal.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldOriginal.HasPendingTemporaryFile, Is.True);
        Assert.That(coldOriginal.Document.Gold, Is.EqualTo(pending.Gold));
        Assert.That(coldOriginal.Document.RunCards, Has.Count.EqualTo(pending.RunCards.Count));
        Assert.That(coldOriginal.Document.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, true, false }));

        var purchaseRetryStore = new AtomicJsonRunSaveStore(_testDirectory);
        Assert.That(purchaseRetryStore.Commit(purchased).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveLoadResult coldPurchased = purchaseRetryStore.Load();
        Assert.That(coldPurchased.HasPendingTemporaryFile, Is.False);
        Assert.That(coldPurchased.Document.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.NodeVisitPending));
        Assert.That(coldPurchased.Document.PathNodeIds, Is.EqualTo(pending.PathNodeIds));
        Assert.That(coldPurchased.Document.Gold, Is.EqualTo(pending.Gold - 75));
        Assert.That(coldPurchased.Document.RunCards, Has.Count.EqualTo(pending.RunCards.Count + 1));
        Assert.That(coldPurchased.Document.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, true, true }));

        RunSaveDocument left = CreateShopLeaveSettlementDocument(purchased);
        RunSaveCommitResult leaveFailed = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem()).Commit(left);
        RunSaveLoadResult coldStillPurchased = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(leaveFailed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldStillPurchased.Document.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.NodeVisitPending));
        Assert.That(coldStillPurchased.Document.PendingNodeVisit.ShopPayload.Entries
                .Select(entry => entry.Purchased),
            Is.EqualTo(new[] { false, true, true }));
        Assert.That(coldStillPurchased.Document.Gold, Is.EqualTo(purchased.Gold));

        var leaveRetryStore = new AtomicJsonRunSaveStore(_testDirectory);
        Assert.That(leaveRetryStore.Commit(left).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveLoadResult coldLeft = leaveRetryStore.Load();
        Assert.That(coldLeft.HasPendingTemporaryFile, Is.False);
        Assert.That(coldLeft.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(coldLeft.Document.PendingNodeVisit, Is.Null);
        Assert.That(coldLeft.Document.PathNodeIds,
            Is.EqualTo(pending.PathNodeIds.Concat(new[] { pending.PendingNodeVisit.NodeId })));
        Assert.That(coldLeft.Document.Gold, Is.EqualTo(purchased.Gold));
        Assert.That(coldLeft.Document.RunCards, Has.Count.EqualTo(purchased.RunCards.Count));
    }

    /// <summary>Event 两种选择替换失败都保留精确 Pending，重试同一后继才发布金币或治疗与单次路径完成。</summary>
    [TestCase(RunEventChoiceKind.GainGold)]
    [TestCase(RunEventChoiceKind.PaidHeal)]
    public void Commit_EventChoiceReplacementFailsThenExactRetry_PublishesSameSuccessor(
        RunEventChoiceKind choice)
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument pending = CreateNodeVisitDocument(
            choice == RunEventChoiceKind.GainGold
                ? "37373737-aaaa-bbbb-cccc-595959595951"
                : "37373737-aaaa-bbbb-cccc-595959595952",
            MapNodeKind.Event);
        Assert.That(initialStore.Commit(pending).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument successor = CreateEventChoiceSettlementDocument(pending, choice);

        RunSaveCommitResult failed = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem()).Commit(successor);
        RunSaveLoadResult coldPending = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(failed.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldPending.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldPending.HasPendingTemporaryFile, Is.True);
        Assert.That(coldPending.Document.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.NodeVisitPending));
        Assert.That(coldPending.Document.CurrentHealth, Is.EqualTo(pending.CurrentHealth));
        Assert.That(coldPending.Document.Gold, Is.EqualTo(pending.Gold));
        Assert.That(coldPending.Document.PathNodeIds, Is.EqualTo(pending.PathNodeIds));
        Assert.That(coldPending.Document.PendingNodeVisit.EventPayload.GainGoldAmount,
            Is.EqualTo(45));
        Assert.That(coldPending.Document.PendingNodeVisit.EventPayload.PaidHealCost,
            Is.EqualTo(30));
        Assert.That(coldPending.Document.PendingNodeVisit.EventPayload.PaidHealAmount,
            Is.EqualTo(18));

        var retryStore = new AtomicJsonRunSaveStore(_testDirectory);
        Assert.That(retryStore.Commit(successor).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveLoadResult coldSuccessor = retryStore.Load();

        Assert.That(coldSuccessor.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldSuccessor.HasPendingTemporaryFile, Is.False);
        Assert.That(coldSuccessor.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(coldSuccessor.Document.PendingNodeVisit, Is.Null);
        Assert.That(coldSuccessor.Document.PathNodeIds,
            Is.EqualTo(pending.PathNodeIds.Concat(new[] { pending.PendingNodeVisit.NodeId })));
        Assert.That(coldSuccessor.Document.CurrentHealth, Is.EqualTo(successor.CurrentHealth));
        Assert.That(coldSuccessor.Document.Gold, Is.EqualTo(successor.Gold));
    }

    /// <summary>BossGate 只允许同一路径末尾 Boss 产生 Victory 或 Defeat，且终局提交后不可继续。</summary>
    [TestCase(RunSaveOutcomeKind.Victory, 37)]
    [TestCase(RunSaveOutcomeKind.Defeat, 0)]
    public void Commit_BossGateBattleOutcome_AcceptsClosedTerminalSuccessor(
        RunSaveOutcomeKind outcomeKind,
        int terminalHealth)
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument gate = CreateBossGateDocument(
            outcomeKind == RunSaveOutcomeKind.Victory
                ? "10101010-aaaa-bbbb-cccc-101010101010"
                : "20202020-aaaa-bbbb-cccc-202020202020",
            currentHealth: 58);
        Assert.That(store.Commit(gate).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument terminal = CreateTerminalOutcomeSuccessor(
            gate,
            outcomeKind,
            terminalHealth);

        RunSaveCommitResult commit = store.Commit(terminal);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.OutcomeKind, Is.EqualTo(outcomeKind));
        Assert.That(load.Document.PathNodeIds, Is.EqualTo(gate.PathNodeIds));
        Assert.That(load.Document.CommittedNodeId, Is.EqualTo(gate.PathNodeIds.Last()));
    }

    /// <summary>主动放弃只允许稳定 RunEntry 前驱且必须逐字段保留生命、路径、牌组与持有物。</summary>
    [Test]
    public void Commit_MapReadyAbandoned_AcceptsExactStableSuccessorOnly()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument live = CreateDocument(
            "30303030-aaaa-bbbb-cccc-303030303030",
            RunSaveProgressPhase.MapReady,
            currentHealth: 46);
        Assert.That(store.Commit(live).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument abandoned = CreateTerminalOutcomeSuccessor(
            live,
            RunSaveOutcomeKind.Abandoned,
            terminalHealth: live.CurrentHealth);

        Assert.That(store.Commit(abandoned).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        Assert.That(store.Load().Document.OutcomeKind, Is.EqualTo(RunSaveOutcomeKind.Abandoned));
    }

    /// <summary>MapReady 不得伪造 Victory，BossGate 也不得用非路径末项充当终局 Battle 节点。</summary>
    [Test]
    public void Commit_ForgedOutcomePredecessorOrBossNode_ReturnsInvalidDocument()
    {
        var mapReadyStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument mapReady = CreateDocument(
            "40404040-aaaa-bbbb-cccc-404040404040",
            RunSaveProgressPhase.MapReady,
            currentHealth: 55);
        Assert.That(mapReadyStore.Commit(mapReady).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument forgedVictory = CreateTerminalOutcomeSuccessor(
            mapReady,
            RunSaveOutcomeKind.Victory,
            terminalHealth: 30);
        Assert.That(
            mapReadyStore.Commit(forgedVictory).Status,
            Is.EqualTo(RunSaveCommitStatus.InvalidDocument));

        mapReadyStore.Delete();
        RunSaveDocument gate = CreateBossGateDocument(
            "50505050-aaaa-bbbb-cccc-505050505050",
            currentHealth: 55);
        Assert.That(mapReadyStore.Commit(gate).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument validVictory = CreateTerminalOutcomeSuccessor(
            gate,
            RunSaveOutcomeKind.Victory,
            terminalHealth: 30);
        RunSaveDocument forgedBossNode = CopyCommittedNode(
            validVictory,
            committedNodeId: "layer-99-slot-0");

        Assert.That(
            mapReadyStore.Commit(forgedBossNode).Status,
            Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
    }

    /// <summary>普通战败只能绑定 live 当前节点的普通直达 Combat/Elite，间接战斗或 Boss 都不得覆盖正式档。</summary>
    [TestCase("L02-S00")]
    [TestCase("L03-S00")]
    public void Commit_MapReadyDefeatWithForgedBattleNode_ReturnsInvalidDocument(
        string committedNodeId)
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument live = CreateDocument(
            "51515151-aaaa-bbbb-cccc-515151515151",
            RunSaveProgressPhase.MapReady,
            currentHealth: 55);
        Assert.That(store.Commit(live).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument defeat = CreateTerminalOutcomeSuccessor(
            live,
            RunSaveOutcomeKind.Defeat,
            terminalHealth: 0);
        RunSaveDocument forged = CopyCommittedNode(defeat, committedNodeId);

        RunSaveCommitResult commit = store.Commit(forged);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(load.Document.CurrentHealth, Is.EqualTo(live.CurrentHealth));
    }

    /// <summary>冻结普通奖励也只能绑定 live 当前节点的普通直达 Combat/Elite，不能伪造间接节点或 Boss。</summary>
    [TestCase("L02-S00")]
    [TestCase("L03-S00")]
    public void Commit_MapReadyRewardWithForgedBattleNode_ReturnsInvalidDocument(
        string committedNodeId)
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument live = CreateDocument(
            "52525252-aaaa-bbbb-cccc-525252525252",
            RunSaveProgressPhase.MapReady,
            currentHealth: 55);
        Assert.That(store.Commit(live).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument pending = CreateDocument(
            live.RunId,
            RunSaveProgressPhase.RewardPending,
            currentHealth: 44);
        RunSaveDocument forged = CopyCommittedNode(pending, committedNodeId);

        RunSaveCommitResult commit = store.Commit(forged);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(load.Document.CurrentHealth, Is.EqualTo(live.CurrentHealth));
    }

    /// <summary>残留 reward intent 即使逐字段等于提交候选，也必须重新证明它是当前 live 的合法直达奖励后继。</summary>
    [Test]
    public void Commit_ForgedRewardMatchingResidualIntent_ReturnsInvalidDocument()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument live = CreateDocument(
            "53535353-aaaa-bbbb-cccc-535353535353",
            RunSaveProgressPhase.MapReady,
            currentHealth: 55);
        Assert.That(store.Commit(live).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument pending = CreateDocument(
            live.RunId,
            RunSaveProgressPhase.RewardPending,
            currentHealth: 44);
        RunSaveDocument forged = CopyCommittedNode(pending, committedNodeId: "L02-S00");
        File.WriteAllText(GetRewardIntentPath(), RunSaveDocumentCodec.Serialize(forged));

        RunSaveCommitResult commit = store.Commit(forged);
        RunSaveLoadResult load = store.Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.InterruptedCommit));
        Assert.That(File.ReadAllText(GetLivePath()), Is.EqualTo(RunSaveDocumentCodec.Serialize(live)));
    }

    /// <summary>终局替换失败后冷启动必须恢复已校验临时终局，不能回退到旧可继续档。</summary>
    [Test]
    public void Commit_TerminalReplacementFails_ColdLoadReturnsTerminalTemporary()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldDocument = CreateDocument(
            "12121212-aaaa-bbbb-cccc-343434343434",
            RunSaveProgressPhase.MapReady,
            currentHealth: 46);
        Assert.That(
            initialStore.Commit(oldDocument).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        byte[] oldLiveBytes = File.ReadAllBytes(GetLivePath());
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem());
        RunSaveDocument terminalDocument = CreateDocument(
            oldDocument.RunId,
            RunSaveProgressPhase.Terminal,
            currentHealth: 0);

        RunSaveCommitResult commit = failingStore.Commit(terminalDocument);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(File.ReadAllBytes(GetLivePath()), Is.EqualTo(oldLiveBytes));
        Assert.That(File.Exists(GetTerminalIntentPath()), Is.True);
        File.Delete(GetTemporaryPath());
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(coldLoad.Document.TerminalReason, Is.EqualTo(RunSaveTerminalReason.Defeat));
        Assert.That(coldLoad.Document.CurrentHealth, Is.Zero);
        Assert.That(coldLoad.HasPendingTemporaryFile, Is.True);
    }

    /// <summary>Terminal 后继不得借战斗结果重排药水槽，非法文档必须在写 intent 前拒绝。</summary>
    [Test]
    public void Commit_TerminalWithPotionReorder_IsRejectedBeforeIntentWrite()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument live = CreateDocument(
            "12121212-bbbb-cccc-dddd-343434343434",
            RunSaveProgressPhase.MapReady,
            currentHealth: 46);
        Assert.That(store.Commit(live).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument reorderedTerminal = CreateDocumentWithHoldingsDrift(
            CreateDocument(
                live.RunId,
                RunSaveProgressPhase.Terminal,
                currentHealth: 0),
            HoldingsDrift.PotionOrder);

        RunSaveCommitResult commit = store.Commit(reorderedTerminal);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.Exists(GetTerminalIntentPath()), Is.False);
        Assert.That(File.Exists(GetTemporaryPath()), Is.False);
        RunSaveLoadResult load = store.Load();
        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(
            load.Document.Potions.Select(potion => potion.InstanceId),
            Is.EqualTo(new[] { 1, 2 }));
    }

    /// <summary>Terminal 后继允许移除已消费药水，并在替换失败后从 durable intent 恢复剩余子序列。</summary>
    [Test]
    public void Commit_TerminalWithPotionSubsequence_ReplacementFailureRecoversTerminal()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument live = CreateDocument(
            "12121212-cccc-dddd-eeee-343434343434",
            RunSaveProgressPhase.MapReady,
            currentHealth: 46);
        Assert.That(initialStore.Commit(live).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument terminal = CreateDocumentWithRemovedPotion(
            CreateDocument(
                live.RunId,
                RunSaveProgressPhase.Terminal,
                currentHealth: 0),
            removedInstanceId: 1);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem());

        RunSaveCommitResult commit = failingStore.Commit(terminal);
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(
            coldLoad.Document.Potions.Select(potion => potion.InstanceId),
            Is.EqualTo(new[] { 2 }));
        Assert.That(coldLoad.Document.Gold, Is.EqualTo(live.Gold));
    }

    /// <summary>终局意图必须先耐久写入并严格回读；意图损坏时不能开始通用临时档或覆盖旧正式档。</summary>
    [Test]
    public void Commit_TerminalIntentWriteIsCorrupt_StopsBeforeTemporaryAndLivePublication()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument liveDocument = CreateDocument(
            "13131313-aaaa-bbbb-cccc-353535353535",
            RunSaveProgressPhase.MapReady,
            currentHealth: 42);
        Assert.That(initialStore.Commit(liveDocument).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        byte[] liveBytes = File.ReadAllBytes(GetLivePath());
        var corruptingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new CorruptingTerminalIntentWriteFileSystem(GetTerminalIntentPath()));
        RunSaveDocument terminalDocument = CreateDocument(
            liveDocument.RunId,
            RunSaveProgressPhase.Terminal,
            currentHealth: 0);

        RunSaveCommitResult commit = corruptingStore.Commit(terminalDocument);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.ReadAllBytes(GetLivePath()), Is.EqualTo(liveBytes));
        Assert.That(File.Exists(GetTemporaryPath()), Is.False);
        Assert.That(File.ReadAllText(GetTerminalIntentPath()), Is.EqualTo("{"));
    }

    /// <summary>同一终局提交重试必须复用既有有效意图，不能用第二次写入风险覆盖可冷恢复的证据。</summary>
    [Test]
    public void Commit_TerminalRetryWithValidatedIntent_ReusesIntentBeforeAnotherReplaceFailure()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "17171717-aaaa-bbbb-cccc-393939393939",
            RunSaveProgressPhase.MapReady,
            currentHealth: 26);
        Assert.That(initialStore.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument terminalDocument = CreateDocument(
            oldLive.RunId,
            RunSaveProgressPhase.Terminal,
            currentHealth: 0);
        var firstAttempt = new AtomicJsonRunSaveStore(
            _testDirectory,
            new ReplaceFailingFileSystem());
        Assert.That(
            firstAttempt.Commit(terminalDocument).Status,
            Is.EqualTo(RunSaveCommitStatus.IoFailure));
        byte[] durableIntentBytes = File.ReadAllBytes(GetTerminalIntentPath());
        var retry = new AtomicJsonRunSaveStore(
            _testDirectory,
            new CorruptingIntentAndReplaceFailingFileSystem(GetTerminalIntentPath()));

        RunSaveCommitResult retryCommit = retry.Commit(terminalDocument);
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(retryCommit.Status, Is.EqualTo(RunSaveCommitStatus.IoFailure));
        Assert.That(File.ReadAllBytes(GetTerminalIntentPath()), Is.EqualTo(durableIntentBytes));
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(coldLoad.Document.TerminalReason, Is.EqualTo(RunSaveTerminalReason.Defeat));
    }

    /// <summary>存在有效终局意图时必须恢复失败页事实，即使磁盘上仍是旧的可继续正式档。</summary>
    [Test]
    public void Load_ValidTerminalIntentWithOldLive_ReturnsTerminal()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "14141414-aaaa-bbbb-cccc-363636363636",
            RunSaveProgressPhase.MapReady,
            currentHealth: 55);
        Assert.That(store.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument terminalDocument = CreateDocument(
            oldLive.RunId,
            RunSaveProgressPhase.Terminal,
            currentHealth: 0);
        File.WriteAllText(
            GetTerminalIntentPath(),
            RunSaveDocumentCodec.Serialize(terminalDocument));

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(load.Document.TerminalReason, Is.EqualTo(RunSaveTerminalReason.Defeat));
        Assert.That(load.Document.CurrentHealth, Is.Zero);
        Assert.That(load.HasPendingTemporaryFile, Is.True);
    }

    /// <summary>结构合法但不是正式档封闭后继的终局意图也必须 fail-closed，不能伪造 Victory。</summary>
    [Test]
    public void Load_ForgedVictoryIntentWithMapReadyLive_ReturnsInterruptedCommit()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument live = CreateDocument(
            "61616161-aaaa-bbbb-cccc-616161616161",
            RunSaveProgressPhase.MapReady,
            currentHealth: 64);
        Assert.That(store.Commit(live).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument forgedVictory = CreateTerminalOutcomeSuccessor(
            live,
            RunSaveOutcomeKind.Victory,
            terminalHealth: 32);
        File.WriteAllText(
            GetTerminalIntentPath(),
            RunSaveDocumentCodec.Serialize(forgedVictory));

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.InterruptedCommit));
        Assert.That(load.Document, Is.Null);
        Assert.That(load.Detail, Does.Contain("legal terminal successor"));
        Assert.That(File.Exists(GetLivePath()), Is.True);
    }

    /// <summary>损坏的终局意图不能回退读取旧可继续正式档，必须 fail-closed 为中断提交。</summary>
    [Test]
    public void Load_CorruptTerminalIntentWithValidOldLive_ReturnsInterruptedCommit()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "15151515-aaaa-bbbb-cccc-373737373737",
            RunSaveProgressPhase.MapReady,
            currentHealth: 64);
        Assert.That(store.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        File.WriteAllText(GetTerminalIntentPath(), "{");

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.InterruptedCommit));
        Assert.That(load.Document, Is.Null);
        Assert.That(load.HasStoredData, Is.True);
        Assert.That(load.HasPendingTemporaryFile, Is.True);
        Assert.That(load.Detail, Does.Contain("terminal intent"));
        Assert.That(File.Exists(GetLivePath()), Is.True);
    }

    /// <summary>删除正式档失败时不得先清终局意图或临时档，冷启动仍必须恢复终局而非旧 Continue。</summary>
    [Test]
    public void Delete_LiveDeleteFails_PreservesTerminalArtifactsAndColdLoadReturnsTerminal()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldLive = CreateDocument(
            "16161616-aaaa-bbbb-cccc-383838383838",
            RunSaveProgressPhase.MapReady,
            currentHealth: 31);
        Assert.That(store.Commit(oldLive).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        RunSaveDocument terminalDocument = CreateDocument(
            oldLive.RunId,
            RunSaveProgressPhase.Terminal,
            currentHealth: 0);
        string terminalJson = RunSaveDocumentCodec.Serialize(terminalDocument);
        File.WriteAllText(GetTerminalIntentPath(), terminalJson);
        File.WriteAllText(GetTemporaryPath(), terminalJson);
        var failingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new LiveDeleteFailingFileSystem(GetLivePath()));

        RunSaveDeleteResult delete = failingStore.Delete();
        RunSaveLoadResult coldLoad = new AtomicJsonRunSaveStore(_testDirectory).Load();

        Assert.That(delete.Status, Is.EqualTo(RunSaveDeleteStatus.IoFailure));
        Assert.That(File.Exists(GetLivePath()), Is.True);
        Assert.That(File.Exists(GetTerminalIntentPath()), Is.True);
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(coldLoad.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(coldLoad.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(coldLoad.Document.TerminalReason, Is.EqualTo(RunSaveTerminalReason.Defeat));
    }

    /// <summary>临时文件若在写入边界损坏，提交必须重读识别并在替换前拒绝它。</summary>
    [Test]
    public void Commit_TemporaryJsonIsInvalid_ValidatesBeforeReplacingLiveFile()
    {
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument oldDocument = CreateDocument(
            "77777777-aaaa-bbbb-cccc-888888888888",
            RunSaveProgressPhase.MapReady,
            currentHealth: 61);
        Assert.That(
            initialStore.Commit(oldDocument).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        string livePath = GetLivePath();
        byte[] oldBytes = File.ReadAllBytes(livePath);
        var corruptingStore = new AtomicJsonRunSaveStore(
            _testDirectory,
            new CorruptingTemporaryWriteFileSystem(GetTemporaryPath()));
        RunSaveDocument newDocument = CreateDocument(
            "99999999-aaaa-bbbb-cccc-aaaaaaaaaaaa",
            RunSaveProgressPhase.MapReady,
            currentHealth: 11);

        RunSaveCommitResult commit = corruptingStore.Commit(newDocument);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.ReadAllBytes(livePath), Is.EqualTo(oldBytes));
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(File.ReadAllText(GetTemporaryPath()), Is.EqualTo("{"));
    }

    /// <summary>临时档即使仍可解析，只要 RunCard 顺序、实例身份或等级漂移就必须拒绝发布。</summary>
    [TestCase(RunCardDrift.Order)]
    [TestCase(RunCardDrift.InstanceId)]
    [TestCase(RunCardDrift.UpgradeLevel)]
    public void Commit_ParseableRunCardDrift_RejectsBeforeLivePublication(RunCardDrift drift)
    {
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new DriftingRunCardTemporaryWriteFileSystem(drift));
        RunSaveDocument document = CreateDocument(
            "9a9a9a9a-aaaa-bbbb-cccc-abababababab",
            RunSaveProgressPhase.MapReady,
            currentHealth: 67);

        RunSaveCommitResult commit = store.Commit(document);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.Exists(GetLivePath()), Is.False);
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetTemporaryPath())).Status,
            Is.EqualTo(RunSaveDocumentReadStatus.Success));
    }

    /// <summary>临时档任一持有物字段即使仍合法可解析，只要漂移就必须拒绝发布。</summary>
    [TestCase(HoldingsDrift.Gold)]
    [TestCase(HoldingsDrift.RelicOrder)]
    [TestCase(HoldingsDrift.RelicInstanceId)]
    [TestCase(HoldingsDrift.RelicTemplateId)]
    [TestCase(HoldingsDrift.PotionOrder)]
    [TestCase(HoldingsDrift.PotionInstanceId)]
    [TestCase(HoldingsDrift.PotionTemplateId)]
    public void Commit_ParseableHoldingsDrift_RejectsBeforeLivePublication(
        HoldingsDrift drift)
    {
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new DriftingHoldingsTemporaryWriteFileSystem(drift));
        RunSaveDocument document = CreateDocument(
            "9b9b9b9b-aaaa-bbbb-cccc-acacacacacac",
            RunSaveProgressPhase.MapReady,
            currentHealth: 68);

        RunSaveCommitResult commit = store.Commit(document);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.Exists(GetLivePath()), Is.False);
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetTemporaryPath())).Status,
            Is.EqualTo(RunSaveDocumentReadStatus.Success));
    }

    /// <summary>临时档任一 NodeVisit envelope 或 payload 字段漂移都必须拒绝正式发布。</summary>
    [TestCase(NodeVisitDrift.NodeIdAndVisitId)]
    [TestCase(NodeVisitDrift.ContentId)]
    [TestCase(NodeVisitDrift.EnvelopeKind)]
    [TestCase(NodeVisitDrift.RestHealAmount)]
    [TestCase(NodeVisitDrift.RestCandidateOrder)]
    [TestCase(NodeVisitDrift.ChestPotionTemplateId)]
    [TestCase(NodeVisitDrift.ShopEntryOrder)]
    [TestCase(NodeVisitDrift.ShopEntryId)]
    [TestCase(NodeVisitDrift.ShopKind)]
    [TestCase(NodeVisitDrift.ShopTemplateId)]
    [TestCase(NodeVisitDrift.ShopPrice)]
    [TestCase(NodeVisitDrift.ShopPurchased)]
    [TestCase(NodeVisitDrift.EventGainGold)]
    [TestCase(NodeVisitDrift.EventPaidHealCost)]
    [TestCase(NodeVisitDrift.EventPaidHealAmount)]
    public void Commit_ParseableNodeVisitDrift_RejectsBeforeLivePublication(
        NodeVisitDrift drift)
    {
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new DriftingNodeVisitTemporaryWriteFileSystem(drift));
        RunSaveDocument document = CreateNodeVisitDocument(
            "9c9c9c9c-aaaa-bbbb-cccc-adadadadadad",
            GetNodeVisitKind(drift));

        RunSaveCommitResult commit = store.Commit(document);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.Exists(GetLivePath()), Is.False);
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetTemporaryPath())).Status,
            Is.EqualTo(RunSaveDocumentReadStatus.Success));
    }

    /// <summary>临时档候选顺序即使仍合法可解析，只要偏离冻结奖励就必须拒绝发布。</summary>
    [Test]
    public void Commit_ParseablePendingRewardDrift_RejectsBeforeLivePublication()
    {
        RunSaveDocument document = CreateDocument(
            "29292929-aaaa-bbbb-cccc-393939393939",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 52);
        RunSaveDocument predecessor = CreateRewardPendingPredecessor(document);
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        Assert.That(
            initialStore.Commit(predecessor).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new DriftingPendingRewardWriteFileSystem(GetTemporaryPath()));

        RunSaveCommitResult commit = store.Commit(document);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.Exists(GetLivePath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetLivePath())).Document.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetTemporaryPath())).Status,
            Is.EqualTo(RunSaveDocumentReadStatus.Success));
    }

    /// <summary>临时档奖励附着掉落即使仍可解析，只要偏离冻结事实就必须拒绝发布。</summary>
    [Test]
    public void Commit_ParseablePendingRewardAttachedLootDrift_RejectsBeforeLivePublication()
    {
        RunSaveDocument document = CreateDocument(
            "30303030-aaaa-bbbb-cccc-404040404040",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 53);
        RunSaveDocument predecessor = CreateRewardPendingPredecessor(document);
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        Assert.That(
            initialStore.Commit(predecessor).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new DriftingPendingRewardAttachedLootWriteFileSystem(GetTemporaryPath()));

        RunSaveCommitResult commit = store.Commit(document);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.Exists(GetLivePath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetLivePath())).Document.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetTemporaryPath())).Status,
            Is.EqualTo(RunSaveDocumentReadStatus.Success));
    }

    /// <summary>源 reward intent 的候选顺序若在 durable 写入边界漂移，必须在创建临时档前拒绝。</summary>
    [Test]
    public void Commit_ParseablePendingRewardIntentDrift_RejectsBeforeTemporaryWrite()
    {
        RunSaveDocument document = CreateDocument(
            "29292929-bbbb-cccc-dddd-393939393939",
            RunSaveProgressPhase.RewardPending,
            currentHealth: 52);
        RunSaveDocument predecessor = CreateRewardPendingPredecessor(document);
        var initialStore = new AtomicJsonRunSaveStore(_testDirectory);
        Assert.That(
            initialStore.Commit(predecessor).Status,
            Is.EqualTo(RunSaveCommitStatus.Success));
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new DriftingPendingRewardWriteFileSystem(GetRewardIntentPath()));

        RunSaveCommitResult commit = store.Commit(document);

        Assert.That(commit.Status, Is.EqualTo(RunSaveCommitStatus.InvalidDocument));
        Assert.That(File.Exists(GetLivePath()), Is.True);
        Assert.That(
            RunSaveDocumentCodec.Read(File.ReadAllText(GetLivePath())).Document.ProgressPhase,
            Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(File.Exists(GetTemporaryPath()), Is.False);
        Assert.That(File.Exists(GetRewardIntentPath()), Is.True);
    }

    /// <summary>坏 JSON 与未知 schema 必须保持各自分类，且不得静默删除正式档。</summary>
    [TestCase("{", RunSaveLoadStatus.InvalidJson)]
    [TestCase("{\"schemaVersion\":999}", RunSaveLoadStatus.UnsupportedSchema)]
    public void Load_UnusableLiveFile_ClassifiesExplicitlyAndLeavesFileOnDisk(
        string json,
        RunSaveLoadStatus expectedStatus)
    {
        string livePath = GetLivePath();
        File.WriteAllText(livePath, json);
        var store = new AtomicJsonRunSaveStore(_testDirectory);

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(expectedStatus));
        Assert.That(load.Document, Is.Null);
        Assert.That(load.Detail, Is.Not.Empty);
        Assert.That(load.HasStoredData, Is.True);
        Assert.That(File.Exists(livePath), Is.True);
        Assert.That(File.ReadAllText(livePath), Is.EqualTo(json));
    }

    /// <summary>非法 UTF-8 也是不可解析的 JSON，必须类型化失败并原样保留正式档。</summary>
    [Test]
    public void Load_InvalidUtf8LiveFile_ReturnsInvalidJsonAndLeavesBytesOnDisk()
    {
        string livePath = GetLivePath();
        byte[] invalidUtf8 = { 0xc3, 0x28 };
        File.WriteAllBytes(livePath, invalidUtf8);
        var store = new AtomicJsonRunSaveStore(_testDirectory);

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.InvalidJson));
        Assert.That(load.Document, Is.Null);
        Assert.That(load.Detail, Is.Not.Empty);
        Assert.That(load.HasStoredData, Is.True);
        Assert.That(File.ReadAllBytes(livePath), Is.EqualTo(invalidUtf8));
    }

    /// <summary>正式档读取失败时仍必须保留已经发现的残留临时档诊断事实。</summary>
    [Test]
    public void Load_LiveReadFailsWithTemporaryFile_PreservesTemporaryDiagnosticFlag()
    {
        File.WriteAllText(GetLivePath(), "{}");
        File.WriteAllText(GetTemporaryPath(), "{}");
        var store = new AtomicJsonRunSaveStore(
            _testDirectory,
            new LiveReadFailingFileSystem(GetLivePath()));

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.IoFailure));
        Assert.That(load.HasStoredData, Is.True);
        Assert.That(load.HasPendingTemporaryFile, Is.True);
        Assert.That(File.Exists(GetLivePath()), Is.True);
        Assert.That(File.Exists(GetTemporaryPath()), Is.True);
    }

    /// <summary>只有残留临时文件时必须显式报告中断提交，不能擅自晋升或删除。</summary>
    [Test]
    public void Load_TemporaryFileOnly_ReturnsInterruptedCommitWithoutMutation()
    {
        string temporaryPath = GetTemporaryPath();
        string json = RunSaveDocumentCodec.Serialize(CreateDocument(
            "bbbbbbbb-aaaa-bbbb-cccc-cccccccccccc",
            RunSaveProgressPhase.MapReady,
            currentHealth: 80));
        File.WriteAllText(temporaryPath, json);
        var store = new AtomicJsonRunSaveStore(_testDirectory);

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.InterruptedCommit));
        Assert.That(load.Document, Is.Null);
        Assert.That(load.Detail, Is.Not.Empty);
        Assert.That(load.HasStoredData, Is.True);
        Assert.That(load.HasPendingTemporaryFile, Is.True);
        Assert.That(File.Exists(GetLivePath()), Is.False);
        Assert.That(File.ReadAllText(temporaryPath), Is.EqualTo(json));
    }

    /// <summary>只有完整终局临时档时也必须恢复失败页所需事实，同时保留临时物供后续确认删除。</summary>
    [Test]
    public void Load_ValidatedTerminalTemporaryOnly_ReturnsTerminalWithoutMutation()
    {
        RunSaveDocument terminalDocument = CreateDocument(
            "cdcdcdcd-aaaa-bbbb-cccc-efefefefefef",
            RunSaveProgressPhase.Terminal,
            currentHealth: 0);
        string terminalJson = RunSaveDocumentCodec.Serialize(terminalDocument);
        File.WriteAllText(GetTemporaryPath(), terminalJson);
        var store = new AtomicJsonRunSaveStore(_testDirectory);

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.Terminal));
        Assert.That(load.Document.TerminalReason, Is.EqualTo(RunSaveTerminalReason.Defeat));
        Assert.That(load.HasPendingTemporaryFile, Is.True);
        Assert.That(File.Exists(GetLivePath()), Is.False);
        Assert.That(File.ReadAllText(GetTemporaryPath()), Is.EqualTo(terminalJson));
    }

    /// <summary>损坏临时物不能遮蔽或改写有效正式档，正式稳定态仍可读取并携带诊断标记。</summary>
    [Test]
    public void Load_CorruptTemporaryWithValidLive_ReturnsUnchangedLiveDocument()
    {
        var store = new AtomicJsonRunSaveStore(_testDirectory);
        RunSaveDocument liveDocument = CreateDocument(
            "abababab-aaaa-bbbb-cccc-cdcdcdcdcdcd",
            RunSaveProgressPhase.MapReady,
            currentHealth: 37);
        Assert.That(store.Commit(liveDocument).Status, Is.EqualTo(RunSaveCommitStatus.Success));
        byte[] liveBytes = File.ReadAllBytes(GetLivePath());
        File.WriteAllText(GetTemporaryPath(), "{");

        RunSaveLoadResult load = store.Load();

        Assert.That(load.Status, Is.EqualTo(RunSaveLoadStatus.Success));
        Assert.That(load.Document.RunId, Is.EqualTo(liveDocument.RunId));
        Assert.That(load.Document.ProgressPhase, Is.EqualTo(RunSaveProgressPhase.MapReady));
        Assert.That(load.Document.CurrentHealth, Is.EqualTo(37));
        Assert.That(load.HasPendingTemporaryFile, Is.True);
        Assert.That(load.Detail, Does.Contain("temporary"));
        Assert.That(File.ReadAllBytes(GetLivePath()), Is.EqualTo(liveBytes));
        Assert.That(File.ReadAllText(GetTemporaryPath()), Is.EqualTo("{"));
    }

    /// <summary>先发布与 Pending 同身份的真实 MapReady 前驱，再提交奖励检查点供事务测试使用。</summary>
    private static RunSaveCommitResult CommitRewardPendingFromStablePredecessor(
        AtomicJsonRunSaveStore store,
        RunSaveDocument pending)
    {
        if (store == null)
            throw new ArgumentNullException(nameof(store));
        if (pending == null || pending.ProgressPhase != RunSaveProgressPhase.RewardPending)
            throw new ArgumentException("A RewardPending fixture is required.", nameof(pending));

        RunSaveDocument predecessor = CreateRewardPendingPredecessor(pending);
        RunSaveCommitResult predecessorCommit = store.Commit(predecessor);
        Assert.That(
            predecessorCommit.Status,
            Is.EqualTo(RunSaveCommitStatus.Success),
            predecessorCommit.Detail);
        return store.Commit(pending);
    }

    /// <summary>从冻结奖励 fixture 还原同身份、同持有物且位于合法战斗前的稳定 MapReady 前驱。</summary>
    private static RunSaveDocument CreateRewardPendingPredecessor(RunSaveDocument pending)
    {
        if (pending == null || pending.ProgressPhase != RunSaveProgressPhase.RewardPending)
            throw new ArgumentException("A RewardPending fixture is required.", nameof(pending));

        return new RunSaveDocument(
            pending.SchemaVersion,
            pending.RunId,
            pending.HeroTemplateId,
            pending.CurrentHealth,
            pending.MaxHealth,
            pending.RunCards,
            pending.LegacyDeckTemplateId,
            pending.RandomRootSeed,
            pending.MapProfileId,
            pending.MapGeneratorVersion,
            pending.MapSeed,
            pending.MapFingerprint,
            pending.PathNodeIds,
            RunSaveProgressPhase.MapReady,
            committedNodeId: null,
            outcomeKind: null,
            pendingCardReward: null,
            pending.Relics,
            pending.Potions,
            pending.Gold,
            pendingNodeVisit: null);
    }

    /// <summary>返回 Adapter 契约冻结的版本化正式文件路径。</summary>
    private string GetLivePath()
    {
        return Path.Combine(_testDirectory, AtomicJsonRunSaveStore.LiveFileName);
    }

    /// <summary>返回与正式文件同目录的提交临时文件路径。</summary>
    private string GetTemporaryPath()
    {
        return Path.Combine(_testDirectory, AtomicJsonRunSaveStore.TemporaryFileName);
    }

    /// <summary>返回与正式文件同目录的终局意图日志路径。</summary>
    private string GetTerminalIntentPath()
    {
        return Path.Combine(_testDirectory, AtomicJsonRunSaveStore.TerminalIntentFileName);
    }

    /// <summary>返回奖励事务源 Pending 的 durable intent 路径。</summary>
    private string GetRewardIntentPath()
    {
        return Path.Combine(_testDirectory, AtomicJsonRunSaveStore.RewardIntentFileName);
    }

    /// <summary>建立 Boss 已恰好追加到路径末尾的稳定 BossGate 检查点。</summary>
    private static RunSaveDocument CreateBossGateDocument(string runId, int currentHealth)
    {
        RunSaveDocument source = CreateDocument(
            runId,
            RunSaveProgressPhase.MapReady,
            currentHealth);
        return new RunSaveDocument(
            source.SchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            source.CurrentHealth,
            source.MaxHealth,
            source.RunCards,
            source.LegacyDeckTemplateId,
            source.RandomRootSeed,
            source.MapProfileId,
            source.MapGeneratorVersion,
            source.MapSeed,
            source.MapFingerprint,
            new[] { "start", "layer-8-slot-0" },
            RunSaveProgressPhase.BossGateReached,
            committedNodeId: null,
            outcomeKind: null,
            pendingCardReward: null,
            source.Relics,
            source.Potions,
            source.Gold,
            pendingNodeVisit: null);
    }

    /// <summary>从稳定前驱复制只允许 outcome、生命、承诺节点与战斗药水消费变化的终局候选。</summary>
    private static RunSaveDocument CreateTerminalOutcomeSuccessor(
        RunSaveDocument source,
        RunSaveOutcomeKind outcomeKind,
        int terminalHealth)
    {
        string committedNodeId = outcomeKind == RunSaveOutcomeKind.Abandoned
            ? null
            : source.ProgressPhase == RunSaveProgressPhase.BossGateReached
                ? source.PathNodeIds[source.PathNodeIds.Count - 1]
                : "L01-S00";
        return new RunSaveDocument(
            source.SchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            terminalHealth,
            source.MaxHealth,
            source.RunCards,
            source.LegacyDeckTemplateId,
            source.RandomRootSeed,
            source.MapProfileId,
            source.MapGeneratorVersion,
            source.MapSeed,
            source.MapFingerprint,
            source.PathNodeIds,
            RunSaveProgressPhase.Terminal,
            committedNodeId,
            outcomeKind,
            pendingCardReward: null,
            source.Relics,
            source.Potions,
            source.Gold,
            pendingNodeVisit: null);
    }

    /// <summary>仅替换候选文档的 committed node，供奖励与终局来源闭合测试伪造节点身份。</summary>
    private static RunSaveDocument CopyCommittedNode(
        RunSaveDocument source,
        string committedNodeId)
    {
        return new RunSaveDocument(
            source.SchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            source.CurrentHealth,
            source.MaxHealth,
            source.RunCards,
            source.LegacyDeckTemplateId,
            source.RandomRootSeed,
            source.MapProfileId,
            source.MapGeneratorVersion,
            source.MapSeed,
            source.MapFingerprint,
            source.PathNodeIds,
            source.ProgressPhase,
            committedNodeId,
            source.OutcomeKind,
            source.PendingCardReward,
            source.Relics,
            source.Potions,
            source.Gold,
            source.PendingNodeVisit);
    }

    /// <summary>建立字段稳定且互不共享的测试存档文档。</summary>
    private static RunSaveDocument CreateDocument(
        string runId,
        RunSaveProgressPhase progressPhase,
        int currentHealth,
        int? attachedRelicTemplateId = null,
        int? attachedPotionTemplateId = null)
    {
        const uint mapSeed = 987654321u;
        MapDefinition map = ActMapGenerator.Generate(
            TinySpireActMapProfiles.LegacyG3V1,
            mapSeed);
        MapNodeId startNodeId = map.Nodes.Single(node => node.Kind == MapNodeKind.Start).Id;
        string firstDirectNodeId = MapReachability.GetSelectableNodeIds(
                map,
                startNodeId,
                MapTraversalMode.Ordinary)
            .First()
            .Value;
        bool isTerminal = progressPhase == RunSaveProgressPhase.Terminal;
        bool isRewardPending = progressPhase == RunSaveProgressPhase.RewardPending;
        string committedNodeId = isTerminal || isRewardPending ? firstDirectNodeId : null;
        RunSavePendingCardRewardDocument pendingCardReward = isRewardPending
            ? new RunSavePendingCardRewardDocument(
                $"{Guid.ParseExact(runId, "D"):N}:1:{committedNodeId}",
                new[] { 3105, 3123, 3157 },
                new RunSaveCardRewardAttachedLootDocument(
                    attachedRelicTemplateId,
                    attachedPotionTemplateId))
            : null;
        return new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            runId,
            heroTemplateId: 1001,
            currentHealth,
            maxHealth: 80,
            runCards: new[]
            {
                new RunSaveCardDocument(instanceId: 1, templateId: 3002, upgradeLevel: 0),
                new RunSaveCardDocument(instanceId: 2, templateId: 3002, upgradeLevel: 0),
                new RunSaveCardDocument(instanceId: 3, templateId: 3003, upgradeLevel: 0),
            },
            legacyDeckTemplateId: null,
            randomRootSeed: 123456789u,
            mapProfileId: map.ProfileId,
            mapGeneratorVersion: map.GeneratorVersion,
            mapSeed,
            mapFingerprint: map.Fingerprint,
            pathNodeIds: new[] { startNodeId.Value },
            progressPhase,
            committedNodeId,
            terminalReason: isTerminal ? RunSaveTerminalReason.Defeat : (RunSaveTerminalReason?)null,
            pendingCardReward,
            relics: new[]
            {
                new RunSaveRelicDocument(instanceId: 1, templateId: 4101),
                new RunSaveRelicDocument(instanceId: 2, templateId: 4102),
            },
            potions: new[]
            {
                new RunSavePotionDocument(instanceId: 1, templateId: 5101),
                new RunSavePotionDocument(instanceId: 2, templateId: 5102),
            },
            gold: 321,
            pendingNodeVisit: null);
    }

    /// <summary>建立携带指定四类 payload 之一的完整 NodeVisitPending 原子提交文档。</summary>
    private static RunSaveDocument CreateNodeVisitDocument(
        string runId,
        MapNodeKind kind)
    {
        RunSaveDocument source = CreateDocument(
            runId,
            RunSaveProgressPhase.MapReady,
            currentHealth: 68);
        const string nodeId = "node-visit-1";
        RunSaveRestNodeVisitPayloadDocument restPayload = null;
        RunSaveChestNodeVisitPayloadDocument chestPayload = null;
        RunSaveShopNodeVisitPayloadDocument shopPayload = null;
        RunSaveEventNodeVisitPayloadDocument eventPayload = null;
        switch (kind)
        {
            case MapNodeKind.Rest:
                restPayload = new RunSaveRestNodeVisitPayloadDocument(
                    healAmount: 24,
                    upgradeCandidateInstanceIds: new[] { 1, 2 });
                break;
            case MapNodeKind.Chest:
                chestPayload = new RunSaveChestNodeVisitPayloadDocument(
                    potionTemplateId: 5101);
                break;
            case MapNodeKind.Shop:
                shopPayload = new RunSaveShopNodeVisitPayloadDocument(new[]
                {
                    new RunSaveShopStockEntryDocument(
                        1,
                        RunShopStockKind.Relic,
                        4101,
                        150,
                        purchased: false),
                    new RunSaveShopStockEntryDocument(
                        2,
                        RunShopStockKind.Potion,
                        5101,
                        60,
                        purchased: true),
                    new RunSaveShopStockEntryDocument(
                        3,
                        RunShopStockKind.Card,
                        3105,
                        75,
                        purchased: false),
                });
                break;
            case MapNodeKind.Event:
                eventPayload = new RunSaveEventNodeVisitPayloadDocument(
                    gainGoldAmount: 45,
                    paidHealCost: 30,
                    paidHealAmount: 18);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }

        int contentId;
        switch (kind)
        {
            case MapNodeKind.Rest:
                contentId = 7101;
                break;
            case MapNodeKind.Chest:
                contentId = 7201;
                break;
            case MapNodeKind.Shop:
                contentId = 7301;
                break;
            case MapNodeKind.Event:
                contentId = 7401;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind));
        }
        var pendingNodeVisit = new RunSavePendingNodeVisitDocument(
            $"{source.RunId}/{nodeId}",
            nodeId,
            contentId,
            kind,
            restPayload,
            chestPayload,
            shopPayload,
            eventPayload);
        return new RunSaveDocument(
            source.SchemaVersion,
            source.RunId,
            source.HeroTemplateId,
            source.CurrentHealth,
            source.MaxHealth,
            source.RunCards,
            source.LegacyDeckTemplateId,
            source.RandomRootSeed,
            source.MapProfileId,
            source.MapGeneratorVersion,
            source.MapSeed,
            source.MapFingerprint,
            source.PathNodeIds,
            RunSaveProgressPhase.NodeVisitPending,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            source.Relics,
            source.Potions,
            source.Gold,
            pendingNodeVisit);
    }

    /// <summary>从冻结 Rest Pending 建立只应用治疗、清 Pending 并追加一次精确节点的 MapReady 后继。</summary>
    private static RunSaveDocument CreateRestHealSettlementDocument(RunSaveDocument pending)
    {
        if (pending == null ||
            pending.ProgressPhase != RunSaveProgressPhase.NodeVisitPending ||
            pending.PendingNodeVisit?.Kind != MapNodeKind.Rest)
        {
            throw new ArgumentException("A Rest NodeVisitPending source document is required.", nameof(pending));
        }

        int settledHealth = Math.Min(
            pending.MaxHealth,
            checked(pending.CurrentHealth + pending.PendingNodeVisit.RestPayload.HealAmount));
        return new RunSaveDocument(
            pending.SchemaVersion,
            pending.RunId,
            pending.HeroTemplateId,
            settledHealth,
            pending.MaxHealth,
            pending.RunCards,
            pending.LegacyDeckTemplateId,
            pending.RandomRootSeed,
            pending.MapProfileId,
            pending.MapGeneratorVersion,
            pending.MapSeed,
            pending.MapFingerprint,
            pending.PathNodeIds.Concat(new[] { pending.PendingNodeVisit.NodeId }).ToArray(),
            RunSaveProgressPhase.MapReady,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            pending.Relics,
            pending.Potions,
            pending.Gold,
            pendingNodeVisit: null);
    }

    /// <summary>从冻结 Chest Pending 建立领取或跳过后清 Pending、追加一次路径的 MapReady 后继。</summary>
    private static RunSaveDocument CreateChestSettlementDocument(
        RunSaveDocument pending,
        bool claim)
    {
        if (pending == null ||
            pending.ProgressPhase != RunSaveProgressPhase.NodeVisitPending ||
            pending.PendingNodeVisit?.Kind != MapNodeKind.Chest)
        {
            throw new ArgumentException("A Chest NodeVisitPending source document is required.", nameof(pending));
        }

        RunSavePotionDocument[] potions = claim
            ? pending.Potions.Concat(new[]
            {
                new RunSavePotionDocument(
                    checked(pending.Potions.Max(potion => potion.InstanceId) + 1),
                    pending.PendingNodeVisit.ChestPayload.PotionTemplateId),
            }).ToArray()
            : pending.Potions.ToArray();
        return new RunSaveDocument(
            pending.SchemaVersion,
            pending.RunId,
            pending.HeroTemplateId,
            pending.CurrentHealth,
            pending.MaxHealth,
            pending.RunCards,
            pending.LegacyDeckTemplateId,
            pending.RandomRootSeed,
            pending.MapProfileId,
            pending.MapGeneratorVersion,
            pending.MapSeed,
            pending.MapFingerprint,
            pending.PathNodeIds.Concat(new[] { pending.PendingNodeVisit.NodeId }).ToArray(),
            RunSaveProgressPhase.MapReady,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            pending.Relics,
            potions,
            pending.Gold,
            pendingNodeVisit: null);
    }

    /// <summary>从冻结 Shop Pending 建立扣除卡价、追加卡实例且只翻转目标库存的仍 Pending 后继。</summary>
    private static RunSaveDocument CreateShopCardPurchaseDocument(RunSaveDocument pending)
    {
        if (pending == null ||
            pending.ProgressPhase != RunSaveProgressPhase.NodeVisitPending ||
            pending.PendingNodeVisit?.Kind != MapNodeKind.Shop)
        {
            throw new ArgumentException("A Shop NodeVisitPending source document is required.", nameof(pending));
        }

        RunSaveShopStockEntryDocument target = pending.PendingNodeVisit.ShopPayload.Entries
            .Single(entry => entry.EntryId == 3 && entry.Kind == RunShopStockKind.Card);
        RunSaveShopStockEntryDocument[] entries = pending.PendingNodeVisit.ShopPayload.Entries
            .Select(entry => new RunSaveShopStockEntryDocument(
                entry.EntryId,
                entry.Kind,
                entry.TemplateId,
                entry.Price,
                purchased: entry.EntryId == target.EntryId || entry.Purchased))
            .ToArray();
        RunSaveCardDocument[] cards = pending.RunCards.Concat(new[]
        {
            new RunSaveCardDocument(
                checked(pending.RunCards.Max(card => card.InstanceId) + 1),
                target.TemplateId,
                upgradeLevel: 0),
        }).ToArray();
        var visit = new RunSavePendingNodeVisitDocument(
            pending.PendingNodeVisit.VisitId,
            pending.PendingNodeVisit.NodeId,
            pending.PendingNodeVisit.ContentId,
            pending.PendingNodeVisit.Kind,
            restPayload: null,
            chestPayload: null,
            new RunSaveShopNodeVisitPayloadDocument(entries),
            eventPayload: null);
        return new RunSaveDocument(
            pending.SchemaVersion,
            pending.RunId,
            pending.HeroTemplateId,
            pending.CurrentHealth,
            pending.MaxHealth,
            cards,
            pending.LegacyDeckTemplateId,
            pending.RandomRootSeed,
            pending.MapProfileId,
            pending.MapGeneratorVersion,
            pending.MapSeed,
            pending.MapFingerprint,
            pending.PathNodeIds,
            RunSaveProgressPhase.NodeVisitPending,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            pending.Relics,
            pending.Potions,
            checked(pending.Gold - target.Price),
            visit);
    }

    /// <summary>从已购 Shop Pending 建立保留余额与内容、清 Pending 并追加一次路径的 MapReady 后继。</summary>
    private static RunSaveDocument CreateShopLeaveSettlementDocument(RunSaveDocument purchased)
    {
        if (purchased == null ||
            purchased.ProgressPhase != RunSaveProgressPhase.NodeVisitPending ||
            purchased.PendingNodeVisit?.Kind != MapNodeKind.Shop)
        {
            throw new ArgumentException(
                "A purchased Shop NodeVisitPending source document is required.",
                nameof(purchased));
        }

        return new RunSaveDocument(
            purchased.SchemaVersion,
            purchased.RunId,
            purchased.HeroTemplateId,
            purchased.CurrentHealth,
            purchased.MaxHealth,
            purchased.RunCards,
            purchased.LegacyDeckTemplateId,
            purchased.RandomRootSeed,
            purchased.MapProfileId,
            purchased.MapGeneratorVersion,
            purchased.MapSeed,
            purchased.MapFingerprint,
            purchased.PathNodeIds.Concat(new[] { purchased.PendingNodeVisit.NodeId }).ToArray(),
            RunSaveProgressPhase.MapReady,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            purchased.Relics,
            purchased.Potions,
            purchased.Gold,
            pendingNodeVisit: null);
    }

    /// <summary>从冻结 Event Pending 建立指定闭合选择完成后的 MapReady 文档。</summary>
    private static RunSaveDocument CreateEventChoiceSettlementDocument(
        RunSaveDocument pending,
        RunEventChoiceKind choice)
    {
        if (pending == null ||
            pending.ProgressPhase != RunSaveProgressPhase.NodeVisitPending ||
            pending.PendingNodeVisit?.Kind != MapNodeKind.Event)
        {
            throw new ArgumentException(
                "An Event NodeVisitPending source document is required.",
                nameof(pending));
        }

        int settledHealth = pending.CurrentHealth;
        int settledGold = pending.Gold;
        switch (choice)
        {
            case RunEventChoiceKind.GainGold:
                settledGold = checked(
                    pending.Gold + pending.PendingNodeVisit.EventPayload.GainGoldAmount);
                break;
            case RunEventChoiceKind.PaidHeal:
                settledGold = checked(
                    pending.Gold - pending.PendingNodeVisit.EventPayload.PaidHealCost);
                settledHealth = Math.Min(
                    pending.MaxHealth,
                    checked(
                        pending.CurrentHealth +
                        pending.PendingNodeVisit.EventPayload.PaidHealAmount));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(choice));
        }

        return new RunSaveDocument(
            pending.SchemaVersion,
            pending.RunId,
            pending.HeroTemplateId,
            settledHealth,
            pending.MaxHealth,
            pending.RunCards,
            pending.LegacyDeckTemplateId,
            pending.RandomRootSeed,
            pending.MapProfileId,
            pending.MapGeneratorVersion,
            pending.MapSeed,
            pending.MapFingerprint,
            pending.PathNodeIds.Concat(new[] { pending.PendingNodeVisit.NodeId }).ToArray(),
            RunSaveProgressPhase.MapReady,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            pending.Relics,
            pending.Potions,
            settledGold,
            pendingNodeVisit: null);
    }

    /// <summary>按漂移字段选择能承载该字段的合法 NodeVisit 类型。</summary>
    private static MapNodeKind GetNodeVisitKind(NodeVisitDrift drift)
    {
        switch (drift)
        {
            case NodeVisitDrift.RestHealAmount:
            case NodeVisitDrift.RestCandidateOrder:
                return MapNodeKind.Rest;
            case NodeVisitDrift.ChestPotionTemplateId:
                return MapNodeKind.Chest;
            case NodeVisitDrift.EventGainGold:
            case NodeVisitDrift.EventPaidHealCost:
            case NodeVisitDrift.EventPaidHealAmount:
                return MapNodeKind.Event;
            default:
                return MapNodeKind.Shop;
        }
    }

    /// <summary>从源 Pending 构造选择或跳过后的唯一合法 MapReady 后继文档。</summary>
    private static RunSaveDocument CreateSettledRewardDocument(
        RunSaveDocument pending,
        int? selectedTemplateId)
    {
        if (pending == null || pending.ProgressPhase != RunSaveProgressPhase.RewardPending)
            throw new ArgumentException("A RewardPending source document is required.", nameof(pending));

        var cards = new List<RunSaveCardDocument>(pending.RunCards);
        if (selectedTemplateId.HasValue)
        {
            cards.Add(new RunSaveCardDocument(
                pending.RunCards.Max(card => card.InstanceId) + 1,
                selectedTemplateId.Value,
                upgradeLevel: 0));
        }
        var relics = new List<RunSaveRelicDocument>(pending.Relics);
        if (pending.PendingCardReward.AttachedLoot.RelicTemplateId.HasValue)
        {
            relics.Add(new RunSaveRelicDocument(
                checked(pending.Relics.Max(relic => relic.InstanceId) + 1),
                pending.PendingCardReward.AttachedLoot.RelicTemplateId.Value));
        }
        var potions = new List<RunSavePotionDocument>(pending.Potions);
        if (pending.PendingCardReward.AttachedLoot.PotionTemplateId.HasValue)
        {
            potions.Add(new RunSavePotionDocument(
                checked(pending.Potions.Max(potion => potion.InstanceId) + 1),
                pending.PendingCardReward.AttachedLoot.PotionTemplateId.Value));
        }

        return new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            pending.RunId,
            pending.HeroTemplateId,
            pending.CurrentHealth,
            pending.MaxHealth,
            cards,
            legacyDeckTemplateId: null,
            pending.RandomRootSeed,
            pending.MapProfileId,
            pending.MapGeneratorVersion,
            pending.MapSeed,
            pending.MapFingerprint,
            pending.PathNodeIds.Concat(new[] { pending.CommittedNodeId }).ToArray(),
            RunSaveProgressPhase.MapReady,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            relics,
            potions,
            gold: pending.Gold,
            pendingNodeVisit: null);
    }

    /// <summary>只完成 RewardPending 信封与路径，不应用附着掉落，供非法后继 fixture 精确造形。</summary>
    private static RunSaveDocument CreateRewardSettlementWithoutAttachedLoot(
        RunSaveDocument pending)
    {
        if (pending == null || pending.ProgressPhase != RunSaveProgressPhase.RewardPending)
            throw new ArgumentException("A RewardPending source document is required.", nameof(pending));

        return new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            pending.RunId,
            pending.HeroTemplateId,
            pending.CurrentHealth,
            pending.MaxHealth,
            pending.RunCards,
            legacyDeckTemplateId: null,
            pending.RandomRootSeed,
            pending.MapProfileId,
            pending.MapGeneratorVersion,
            pending.MapSeed,
            pending.MapFingerprint,
            pending.PathNodeIds.Concat(new[] { pending.CommittedNodeId }).ToArray(),
            RunSaveProgressPhase.MapReady,
            committedNodeId: null,
            terminalReason: null,
            pendingCardReward: null,
            pending.Relics,
            pending.Potions,
            pending.Gold,
            pendingNodeVisit: null);
    }

    /// <summary>从已结算 MapReady 构造下一场普通战斗的冻结奖励检查点。</summary>
    private static RunSaveDocument CreateNextPendingRewardDocument(RunSaveDocument settled)
    {
        if (settled == null || settled.ProgressPhase != RunSaveProgressPhase.MapReady)
            throw new ArgumentException("A settled MapReady document is required.", nameof(settled));

        ActMapProfile profile = TinySpireActMapProfiles.GetById(settled.MapProfileId)
            ?? throw new InvalidOperationException("The settled fixture profile is not registered.");
        MapDefinition map = ActMapGenerator.Generate(profile, settled.MapSeed);
        Assert.That(map.Fingerprint, Is.EqualTo(settled.MapFingerprint));
        string committedNodeId = MapReachability.GetSelectableNodeIds(
                map,
                new MapNodeId(settled.PathNodeIds[settled.PathNodeIds.Count - 1]),
                MapTraversalMode.Ordinary)
            .First()
            .Value;
        var pendingReward = new RunSavePendingCardRewardDocument(
            $"{Guid.ParseExact(settled.RunId, "D"):N}:2:{committedNodeId}",
            new[] { 3105, 3123, 3157 });
        return new RunSaveDocument(
            RunSaveDocument.CurrentSchemaVersion,
            settled.RunId,
            settled.HeroTemplateId,
            settled.CurrentHealth,
            settled.MaxHealth,
            settled.RunCards,
            legacyDeckTemplateId: null,
            settled.RandomRootSeed,
            settled.MapProfileId,
            settled.MapGeneratorVersion,
            settled.MapSeed,
            settled.MapFingerprint,
            settled.PathNodeIds,
            RunSaveProgressPhase.RewardPending,
            committedNodeId,
            terminalReason: null,
            pendingReward,
            settled.Relics,
            settled.Potions,
            settled.Gold,
            pendingNodeVisit: null);
    }

    /// <summary>复制完整文档并只漂移一种仍可解析的持有物事实。</summary>
    private static RunSaveDocument CreateDocumentWithHoldingsDrift(
        RunSaveDocument source,
        HoldingsDrift drift)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(source));
        ApplyHoldingsDrift(raw, drift);
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());
        if (read.Status != RunSaveDocumentReadStatus.Success)
        {
            throw new InvalidOperationException(
                $"The requested holdings drift must remain parseable: {read.Detail}");
        }

        return read.Document;
    }

    /// <summary>从合法附着结算复制并制造一种仍可解析的尾项或前缀漂移。</summary>
    private static RunSaveDocument CreateAttachedLootSettlementDrift(
        RunSaveDocument source,
        AttachedLootSettlementDrift drift)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(source));
        var relics = (JArray)raw["relics"];
        var potions = (JArray)raw["potions"];
        switch (drift)
        {
            case AttachedLootSettlementDrift.MissingRelicTail:
                relics.Last.Remove();
                break;
            case AttachedLootSettlementDrift.ExtraRelicTail:
                relics.Add(new JObject
                {
                    ["instanceId"] = 4,
                    ["templateId"] = 8002,
                });
                break;
            case AttachedLootSettlementDrift.WrongRelicInstanceId:
                relics.Last["instanceId"] = 4;
                break;
            case AttachedLootSettlementDrift.WrongRelicTemplateId:
                relics.Last["templateId"] = 8002;
                break;
            case AttachedLootSettlementDrift.RelicInsertedBeforePrefix:
                JToken relicTail = relics.Last;
                relicTail.Remove();
                relics.Insert(0, relicTail);
                break;
            case AttachedLootSettlementDrift.MissingPotionTail:
                potions.Last.Remove();
                break;
            case AttachedLootSettlementDrift.WrongPotionInstanceId:
                potions.Last["instanceId"] = 4;
                break;
            case AttachedLootSettlementDrift.WrongPotionTemplateId:
                potions.Last["templateId"] = 9002;
                break;
            case AttachedLootSettlementDrift.PotionInsertedBeforePrefix:
                JToken potionTail = potions.Last;
                potionTail.Remove();
                potions.Insert(0, potionTail);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(drift), drift, null);
        }

        return RequireParseableDocument(raw, $"attached loot drift {drift}");
    }

    /// <summary>把 JSON fixture 重新读成合法文档，否则让测试准备阶段立即暴露错误。</summary>
    private static RunSaveDocument RequireParseableDocument(JObject raw, string fixtureName)
    {
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());
        if (read.Status != RunSaveDocumentReadStatus.Success)
        {
            throw new InvalidOperationException(
                $"The {fixtureName} fixture must remain parseable: {read.Detail}");
        }

        return read.Document;
    }

    /// <summary>复制完整文档并只移除一个已存在药水实例，保持其余槽位稳定相对顺序。</summary>
    private static RunSaveDocument CreateDocumentWithRemovedPotion(
        RunSaveDocument source,
        int removedInstanceId)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        JObject raw = JObject.Parse(RunSaveDocumentCodec.Serialize(source));
        var potions = (JArray)raw["potions"];
        JToken removed = potions.SingleOrDefault(
            token => token.Value<int>("instanceId") == removedInstanceId);
        if (removed == null)
            throw new ArgumentOutOfRangeException(nameof(removedInstanceId));

        removed.Remove();
        RunSaveDocumentReadResult read = RunSaveDocumentCodec.Read(raw.ToString());
        if (read.Status != RunSaveDocumentReadStatus.Success)
        {
            throw new InvalidOperationException(
                $"The potion removal fixture must remain parseable: {read.Detail}");
        }

        return read.Document;
    }

    /// <summary>在 JSON 测试边界逐字段制造一种合法但不等值的持有物漂移。</summary>
    private static void ApplyHoldingsDrift(JObject document, HoldingsDrift drift)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        var relics = (JArray)document["relics"];
        var potions = (JArray)document["potions"];
        switch (drift)
        {
            case HoldingsDrift.Gold:
                document["gold"] = document.Value<int>("gold") + 1;
                break;
            case HoldingsDrift.RelicOrder:
                JToken firstRelic = relics[0].DeepClone();
                JToken secondRelic = relics[1].DeepClone();
                relics[0] = secondRelic;
                relics[1] = firstRelic;
                break;
            case HoldingsDrift.RelicInstanceId:
                relics[0]["instanceId"] = 71;
                break;
            case HoldingsDrift.RelicTemplateId:
                relics[0]["templateId"] = 4199;
                break;
            case HoldingsDrift.PotionOrder:
                JToken firstPotion = potions[0].DeepClone();
                JToken secondPotion = potions[1].DeepClone();
                potions[0] = secondPotion;
                potions[1] = firstPotion;
                break;
            case HoldingsDrift.PotionInstanceId:
                potions[0]["instanceId"] = 81;
                break;
            case HoldingsDrift.PotionTemplateId:
                potions[0]["templateId"] = 5199;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(drift), drift, null);
        }
    }

    /// <summary>在 JSON 写入边界制造一种仍合法可解析的 NodeVisit 字段漂移。</summary>
    private static void ApplyNodeVisitDrift(JObject document, NodeVisitDrift drift)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        var pending = (JObject)document["pendingNodeVisit"];
        switch (drift)
        {
            case NodeVisitDrift.NodeIdAndVisitId:
                pending["nodeId"] = "node-visit-2";
                pending["visitId"] = $"{document.Value<string>("runId")}/node-visit-2";
                break;
            case NodeVisitDrift.ContentId:
                pending["contentId"] = pending.Value<int>("contentId") + 1;
                break;
            case NodeVisitDrift.EnvelopeKind:
                pending["kind"] = nameof(MapNodeKind.Rest);
                pending["restPayload"] = new JObject
                {
                    ["healAmount"] = 24,
                    ["upgradeCandidateInstanceIds"] = new JArray(1, 2),
                };
                pending["shopPayload"] = JValue.CreateNull();
                break;
            case NodeVisitDrift.RestHealAmount:
                pending["restPayload"]["healAmount"] = 25;
                break;
            case NodeVisitDrift.RestCandidateOrder:
            {
                var candidates = (JArray)pending["restPayload"]["upgradeCandidateInstanceIds"];
                JToken first = candidates[0].DeepClone();
                candidates[0] = candidates[1].DeepClone();
                candidates[1] = first;
                break;
            }
            case NodeVisitDrift.ChestPotionTemplateId:
                pending["chestPayload"]["potionTemplateId"] = 5199;
                break;
            case NodeVisitDrift.ShopEntryOrder:
            {
                var entries = (JArray)pending["shopPayload"]["entries"];
                JToken first = entries[0].DeepClone();
                entries[0] = entries[1].DeepClone();
                entries[1] = first;
                break;
            }
            case NodeVisitDrift.ShopEntryId:
                pending["shopPayload"]["entries"][0]["entryId"] = 71;
                break;
            case NodeVisitDrift.ShopKind:
                pending["shopPayload"]["entries"][0]["kind"] =
                    nameof(RunShopStockKind.Card);
                break;
            case NodeVisitDrift.ShopTemplateId:
                pending["shopPayload"]["entries"][0]["templateId"] = 4199;
                break;
            case NodeVisitDrift.ShopPrice:
                pending["shopPayload"]["entries"][0]["price"] = 151;
                break;
            case NodeVisitDrift.ShopPurchased:
                pending["shopPayload"]["entries"][0]["purchased"] = true;
                break;
            case NodeVisitDrift.EventGainGold:
                pending["eventPayload"]["gainGoldAmount"] = 46;
                break;
            case NodeVisitDrift.EventPaidHealCost:
                pending["eventPayload"]["paidHealCost"] = 31;
                break;
            case NodeVisitDrift.EventPaidHealAmount:
                pending["eventPayload"]["paidHealAmount"] = 19;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(drift), drift, null);
        }
    }

    private class TestRunSaveFileSystem : IRunSaveFileSystem
    {
        /// <summary>在测试目录中创建持久化目录。</summary>
        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }

        /// <summary>查询指定测试文件是否存在。</summary>
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>按完整文本读取指定测试文件。</summary>
        public virtual string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        /// <summary>在测试边界写入完整文本；耐久刷新由生产实现的真实目录测试覆盖。</summary>
        public virtual void WriteAllTextDurably(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        /// <summary>以仅允许新目标的移动模拟首次提交。</summary>
        public virtual void MoveFile(string sourcePath, string destinationPath)
        {
            File.Move(sourcePath, destinationPath);
        }

        /// <summary>以平台原子替换模拟已有正式档提交。</summary>
        public virtual void ReplaceFile(string sourcePath, string destinationPath)
        {
            File.Replace(sourcePath, destinationPath, destinationBackupFileName: null);
        }

        /// <summary>删除测试边界内的指定文件。</summary>
        public virtual void DeleteFile(string path)
        {
            File.Delete(path);
        }
    }

    private sealed class ReplaceFailingFileSystem : TestRunSaveFileSystem
    {
        /// <summary>稳定注入正式文件替换失败，证明旧档不会被覆盖。</summary>
        public override void ReplaceFile(string sourcePath, string destinationPath)
        {
            throw new IOException("Injected replace failure.");
        }
    }

    private sealed class MoveFailingFileSystem : TestRunSaveFileSystem
    {
        /// <summary>稳定注入首次正式发布失败。</summary>
        public override void MoveFile(string sourcePath, string destinationPath)
        {
            throw new IOException("Injected move failure.");
        }
    }

    private sealed class DeleteFailingFileSystem : TestRunSaveFileSystem
    {
        /// <summary>稳定注入已确认删除失败，并保留目标文件。</summary>
        public override void DeleteFile(string path)
        {
            throw new IOException("Injected delete failure.");
        }
    }

    private sealed class CorruptingTemporaryWriteFileSystem : TestRunSaveFileSystem
    {
        private readonly string _temporaryPath;

        /// <summary>建立只损坏通用临时档、不影响奖励 intent 的故障 fake。</summary>
        public CorruptingTemporaryWriteFileSystem(string temporaryPath)
        {
            _temporaryPath = temporaryPath;
        }

        /// <summary>忽略待写正文并落下坏 JSON，迫使 Adapter 重读临时文件。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            File.WriteAllText(path, path == _temporaryPath ? "{" : contents);
        }
    }

    private sealed class TemporaryWriteFailingFileSystem : TestRunSaveFileSystem
    {
        private readonly string _temporaryPath;

        /// <summary>建立只拒绝通用临时档写入、允许 durable reward intent 的故障 fake。</summary>
        public TemporaryWriteFailingFileSystem(string temporaryPath)
        {
            _temporaryPath = temporaryPath;
        }

        /// <summary>指定临时档写入失败，其他 durable 写入保持正常。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            if (path == _temporaryPath)
                throw new IOException("Injected temporary write failure.");

            base.WriteAllTextDurably(path, contents);
        }
    }

    private sealed class RewardIntentDeleteFailingFileSystem : TestRunSaveFileSystem
    {
        private readonly string _rewardIntentPath;

        /// <summary>建立只拒绝结算后 reward intent 清理的故障 fake。</summary>
        public RewardIntentDeleteFailingFileSystem(string rewardIntentPath)
        {
            _rewardIntentPath = rewardIntentPath;
        }

        /// <summary>奖励 intent 删除稳定失败，正式档替换与其他文件操作保持正常。</summary>
        public override void DeleteFile(string path)
        {
            if (path == _rewardIntentPath)
                throw new IOException("Injected reward intent delete failure.");

            base.DeleteFile(path);
        }
    }

    public enum RunCardDrift
    {
        Order,
        InstanceId,
        UpgradeLevel,
    }

    public enum HoldingsDrift
    {
        Gold,
        RelicOrder,
        RelicInstanceId,
        RelicTemplateId,
        PotionOrder,
        PotionInstanceId,
        PotionTemplateId,
    }

    public enum AttachedLootSettlementDrift
    {
        MissingRelicTail,
        ExtraRelicTail,
        WrongRelicInstanceId,
        WrongRelicTemplateId,
        RelicInsertedBeforePrefix,
        MissingPotionTail,
        WrongPotionInstanceId,
        WrongPotionTemplateId,
        PotionInsertedBeforePrefix,
    }

    public enum NodeVisitDrift
    {
        NodeIdAndVisitId,
        ContentId,
        EnvelopeKind,
        RestHealAmount,
        RestCandidateOrder,
        ChestPotionTemplateId,
        ShopEntryOrder,
        ShopEntryId,
        ShopKind,
        ShopTemplateId,
        ShopPrice,
        ShopPurchased,
        EventGainGold,
        EventPaidHealCost,
        EventPaidHealAmount,
    }

    private sealed class DriftingRunCardTemporaryWriteFileSystem : TestRunSaveFileSystem
    {
        private readonly RunCardDrift _drift;

        /// <summary>建立只改写一种 RunCard durable equality 事实的可解析临时档 fake。</summary>
        public DriftingRunCardTemporaryWriteFileSystem(RunCardDrift drift)
        {
            _drift = drift;
        }

        /// <summary>写入合法 JSON，但按测试要求漂移卡牌顺序、实例 ID 或升级等级。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            JObject document = JObject.Parse(contents);
            var runCards = (JArray)document["runCards"];
            switch (_drift)
            {
                case RunCardDrift.Order:
                    JToken first = runCards[0].DeepClone();
                    JToken second = runCards[1].DeepClone();
                    runCards[0] = second;
                    runCards[1] = first;
                    break;
                case RunCardDrift.InstanceId:
                    runCards[0]["instanceId"] = 71;
                    break;
                case RunCardDrift.UpgradeLevel:
                    runCards[0]["upgradeLevel"] = 4;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(_drift));
            }

            File.WriteAllText(path, document.ToString());
        }
    }

    private sealed class DriftingHoldingsTemporaryWriteFileSystem : TestRunSaveFileSystem
    {
        private readonly HoldingsDrift _drift;

        /// <summary>建立只改写一种 durable holdings 等值事实的可解析临时档 fake。</summary>
        public DriftingHoldingsTemporaryWriteFileSystem(HoldingsDrift drift)
        {
            _drift = drift;
        }

        /// <summary>写入合法 JSON，但按测试要求漂移一个持有物字段。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            JObject document = JObject.Parse(contents);
            ApplyHoldingsDrift(document, _drift);
            File.WriteAllText(path, document.ToString());
        }
    }

    private sealed class DriftingNodeVisitTemporaryWriteFileSystem : TestRunSaveFileSystem
    {
        private readonly NodeVisitDrift _drift;

        /// <summary>建立只改写一种 durable NodeVisit 等值事实的可解析临时档 fake。</summary>
        public DriftingNodeVisitTemporaryWriteFileSystem(NodeVisitDrift drift)
        {
            _drift = drift;
        }

        /// <summary>写入合法 JSON，但按测试要求漂移一个 NodeVisit envelope 或 payload 字段。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            JObject document = JObject.Parse(contents);
            ApplyNodeVisitDrift(document, _drift);
            File.WriteAllText(path, document.ToString());
        }
    }

    private sealed class DriftingPendingRewardWriteFileSystem : TestRunSaveFileSystem
    {
        private readonly string _targetPath;

        /// <summary>建立只在指定 durable 文件路径交换奖励候选的故障 fake。</summary>
        public DriftingPendingRewardWriteFileSystem(string targetPath)
        {
            _targetPath = targetPath;
        }

        /// <summary>写入合法 JSON，但交换两个奖励候选以模拟静默顺序漂移。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            if (path != _targetPath)
            {
                base.WriteAllTextDurably(path, contents);
                return;
            }

            JObject document = JObject.Parse(contents);
            var candidateTemplateIds =
                (JArray)document["pendingCardReward"]?["candidateTemplateIds"];
            JToken first = candidateTemplateIds[0].DeepClone();
            JToken second = candidateTemplateIds[1].DeepClone();
            candidateTemplateIds[0] = second;
            candidateTemplateIds[1] = first;
            File.WriteAllText(path, document.ToString());
        }
    }

    private sealed class DriftingPendingRewardAttachedLootWriteFileSystem :
        TestRunSaveFileSystem
    {
        private readonly string _targetPath;

        /// <summary>建立只在指定 durable 文件路径改写奖励附着遗物的故障 fake。</summary>
        public DriftingPendingRewardAttachedLootWriteFileSystem(string targetPath)
        {
            _targetPath = targetPath;
        }

        /// <summary>写入合法 JSON，但把 Empty 附着掉落改成一个遗物模板。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            if (path != _targetPath)
            {
                base.WriteAllTextDurably(path, contents);
                return;
            }

            JObject document = JObject.Parse(contents);
            document["pendingCardReward"]["attachedLoot"]["relicTemplateId"] = 8001;
            File.WriteAllText(path, document.ToString());
        }
    }

    private sealed class CorruptingTerminalIntentWriteFileSystem : TestRunSaveFileSystem
    {
        private readonly string _terminalIntentPath;

        /// <summary>建立只损坏终局意图写入、不影响后续通用临时档的故障 fake。</summary>
        public CorruptingTerminalIntentWriteFileSystem(string terminalIntentPath)
        {
            _terminalIntentPath = terminalIntentPath;
        }

        /// <summary>在终局意图边界写入坏 JSON，其他路径仍按正常文本写入。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            if (path == _terminalIntentPath)
            {
                File.WriteAllText(path, "{");
                return;
            }

            base.WriteAllTextDurably(path, contents);
        }
    }

    private sealed class CorruptingIntentAndReplaceFailingFileSystem : TestRunSaveFileSystem
    {
        private readonly string _terminalIntentPath;

        /// <summary>建立会损坏重复意图写入并再次拒绝正式替换的组合故障 fake。</summary>
        public CorruptingIntentAndReplaceFailingFileSystem(string terminalIntentPath)
        {
            _terminalIntentPath = terminalIntentPath;
        }

        /// <summary>若生产错误地重写既有意图则立刻损坏它；其他写入保持正常。</summary>
        public override void WriteAllTextDurably(string path, string contents)
        {
            if (path == _terminalIntentPath)
            {
                File.WriteAllText(path, "{");
                return;
            }

            base.WriteAllTextDurably(path, contents);
        }

        /// <summary>再次拒绝正式替换，使冷启动只能依赖第一次留下的有效终局意图。</summary>
        public override void ReplaceFile(string sourcePath, string destinationPath)
        {
            throw new IOException("Injected retry replace failure.");
        }
    }

    private sealed class LiveDeleteFailingFileSystem : TestRunSaveFileSystem
    {
        private readonly string _livePath;

        /// <summary>建立只拒绝正式档删除、允许观察错误顺序的故障 fake。</summary>
        public LiveDeleteFailingFileSystem(string livePath)
        {
            _livePath = livePath;
        }

        /// <summary>正式档删除稳定失败；若生产代码错误地先删其他文件则测试会留下证据。</summary>
        public override void DeleteFile(string path)
        {
            if (path == _livePath)
                throw new IOException("Injected live delete failure.");

            base.DeleteFile(path);
        }
    }

    private sealed class LiveReadFailingFileSystem : TestRunSaveFileSystem
    {
        private readonly string _livePath;

        /// <summary>建立只在读取指定正式档时故障的文件系统 fake。</summary>
        public LiveReadFailingFileSystem(string livePath)
        {
            _livePath = livePath;
        }

        /// <summary>正式档读取抛出 IO 故障，临时档仍保持可发现。</summary>
        public override string ReadAllText(string path)
        {
            if (path == _livePath)
                throw new IOException("Injected live read failure.");

            return base.ReadAllText(path);
        }
    }
}
