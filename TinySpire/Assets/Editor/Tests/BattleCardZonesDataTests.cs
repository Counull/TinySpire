using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.Core;
using TinySpire.Run;

public sealed class BattleCardZonesDataTests
{
    /// <summary>RunCard 投影进入战斗后保留稳定来源、模板、等级与输入顺序，同时分配独立战内身份。</summary>
    [Test]
    public void CreatingFromRunCards_PreservesOriginOrderTemplateAndOpaqueUpgradeFacts()
    {
        var runCards = new[]
        {
            new RunCard(new RunCardInstanceId(41), templateId: 3002, upgradeLevel: 0),
            new RunCard(new RunCardInstanceId(42), templateId: 3002, upgradeLevel: 2),
            new RunCard(new RunCardInstanceId(99), templateId: 3003, upgradeLevel: 1),
        };

        using var zones = new BattleCardZonesData(runCards, shuffleSeed: 1234);
        CardInstanceData[] projected = zones.Cards
            .OrderBy(pair => pair.Key.Value)
            .Select(pair => pair.Value)
            .ToArray();

        Assert.That(projected.Select(card => card.Id.Value), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(
            projected.Select(card => card.OriginRunCardInstanceId?.Sequence),
            Is.EqualTo(new int?[] { 41, 42, 99 }));
        Assert.That(projected.Select(card => card.TemplateId), Is.EqualTo(new[] { 3002, 3002, 3003 }));
        Assert.That(projected.Select(card => card.UpgradeLevel), Is.EqualTo(new[] { 0, 2, 1 }));
    }

    [Test]
    public void CreatingDeck_CreatesDistinctInstancesInTheShuffledDrawPile()
    {
        var zones = new BattleCardZonesData(
            new[] { 3002, 3002, 3003, 3004 },
            shuffleSeed: 1234);

        Assert.That(zones.Cards.Count, Is.EqualTo(4));
        Assert.That(zones.DrawPile.Count, Is.EqualTo(4));
        Assert.That(zones.Hand, Is.Empty);
        Assert.That(zones.DiscardPile, Is.Empty);
        Assert.That(zones.ExhaustPile, Is.Empty);

        var distinctIds = new HashSet<CardInstanceId>(zones.DrawPile);
        Assert.That(distinctIds.Count, Is.EqualTo(4));
    }

    [Test]
    public void Drawing_WithTheSameSeed_ProducesTheSameTemplateOrder()
    {
        int[] templates = { 3001, 3002, 3003, 3004, 3005, 3006 };
        var firstZones = new BattleCardZonesData(templates, shuffleSeed: 2468);
        var secondZones = new BattleCardZonesData(templates, shuffleSeed: 2468);

        firstZones.Draw(templates.Length);
        secondZones.Draw(templates.Length);

        Assert.That(GetTemplateOrder(secondZones), Is.EqualTo(GetTemplateOrder(firstZones)));
    }

    [Test]
    public void Draw_WhenDrawPileIsEmpty_ReshufflesDiscardPileWithoutLosingCards()
    {
        var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003, 3004 },
            shuffleSeed: 1357);

        BattleCardZoneOperationResult initialDraw = zones.Draw(4);
        BattleCardZoneOperationResult discard = zones.DiscardHand();
        Assert.That(initialDraw.MovedCardCount, Is.EqualTo(4));
        Assert.That(discard.MovedCardCount, Is.EqualTo(4));
        Assert.That(zones.DrawPile, Is.Empty);
        Assert.That(zones.DiscardPile.Count, Is.EqualTo(4));
        CardInstanceId[] discardOrder = new List<CardInstanceId>(zones.DiscardPile).ToArray();

        BattleCardZoneOperationResult redrawn = zones.Draw(4);

        Assert.That(zones.Hand.Count, Is.EqualTo(4));
        Assert.That(zones.DrawPile, Is.Empty);
        Assert.That(zones.DiscardPile, Is.Empty);
        Assert.That(zones.ExhaustPile, Is.Empty);
        Assert.That(redrawn.Succeeded, Is.True);
        Assert.That(redrawn.Settlements.Count, Is.EqualTo(9));
        for (int index = 0; index < discardOrder.Length; index++)
        {
            var moved = redrawn.Settlements[index] as BattleCardMovedSettlement;
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(index));
            Assert.That(moved.CardId, Is.EqualTo(discardOrder[index]));
            Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.DrawPile));
        }

        var reshuffled = redrawn.Settlements[4] as BattleCardsReshuffledSettlement;
        Assert.That(reshuffled, Is.Not.Null);
        Assert.That(reshuffled.Order, Is.EqualTo(4));
        Assert.That(reshuffled.NewDrawPileOrder, Is.EquivalentTo(discardOrder));
        for (int index = 0; index < reshuffled.NewDrawPileOrder.Count; index++)
        {
            var moved = redrawn.Settlements[index + 5] as BattleCardMovedSettlement;
            CardInstanceId expectedDrawnId =
                reshuffled.NewDrawPileOrder[reshuffled.NewDrawPileOrder.Count - 1 - index];
            Assert.That(moved, Is.Not.Null);
            Assert.That(moved.Order, Is.EqualTo(index + 5));
            Assert.That(moved.CardId, Is.EqualTo(expectedDrawnId));
            Assert.That(moved.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
            Assert.That(moved.ToZone, Is.EqualTo(BattleCardZone.Hand));
        }

        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证 Draw 先抽完残余抽牌堆，再按弃牌顺序重洗，并从指定命令序号连续记录。</summary>
    [Test]
    public void PreparedDraw_ReshufflesWithoutPrepareWritesAndCommitsOneFrozenLayout()
    {
        using var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003, 3004 },
            shuffleSeed: 2468);
        zones.Draw(4);
        zones.DiscardHand();
        CardZoneLayoutData layoutBefore = zones.Layout.CurrentValue;
        uint shuffleRandomBefore = zones.ShuffleRandomState;

        BattlePreparedDraw plan = zones.PrepareDraw(
            count: 2,
            startingOrder: 7,
            handLimit: 10);

        Assert.That(plan.Owner, Is.SameAs(zones));
        Assert.That(plan.InitialLayout, Is.SameAs(layoutBefore));
        Assert.That(plan.ShuffleRandomStateBefore, Is.EqualTo(shuffleRandomBefore));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(zones.ValidatePreparedDraw(plan), Is.True);

        int layoutPublicationCount = 0;
        BattleCardZoneOperationResult result;
        using (zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            result = zones.CommitPreparedDraw(plan);

        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(zones.Hand, Has.Count.EqualTo(2));
        Assert.That(zones.DiscardPile, Is.Empty);
        Assert.That(zones.DrawPile, Has.Count.EqualTo(2));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(plan.ShuffleRandomStateAfter));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(7, result.Settlements.Count)));
        Assert.That(result.Settlements.OfType<BattleCardsReshuffledSettlement>().Count(), Is.EqualTo(1));
        Assert.That(result.Settlements.OfType<BattleCardMovedSettlement>()
            .Count(item => item.FromZone == BattleCardZone.DrawPile &&
                item.ToZone == BattleCardZone.Hand), Is.EqualTo(2));
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证抽牌计划拒绝跨聚合、布局漂移与重复提交，且手满零抽不发布布局。</summary>
    [Test]
    public void PreparedDraw_RejectsCrossOwnerDriftAndRepeatWhileZeroDrawPublishesNothing()
    {
        using (var owner = new BattleCardZonesData(new[] { 3001, 3002 }, shuffleSeed: 1357))
        using (var other = new BattleCardZonesData(new[] { 4001 }, shuffleSeed: 2468))
        {
            BattlePreparedDraw plan = owner.PrepareDraw(1);
            CardZoneLayoutData otherLayoutBefore = other.Layout.CurrentValue;
            uint otherRandomBefore = other.ShuffleRandomState;

            Assert.That(other.ValidatePreparedDraw(plan), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => other.CommitPreparedDraw(plan));
            Assert.That(other.Layout.CurrentValue, Is.SameAs(otherLayoutBefore));
            Assert.That(other.ShuffleRandomState, Is.EqualTo(otherRandomBefore));

            owner.CommitPreparedDraw(plan);
            CardZoneLayoutData ownerLayoutAfter = owner.Layout.CurrentValue;
            uint ownerRandomAfter = owner.ShuffleRandomState;
            Assert.That(owner.ValidatePreparedDraw(plan), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => owner.CommitPreparedDraw(plan));
            Assert.That(owner.Layout.CurrentValue, Is.SameAs(ownerLayoutAfter));
            Assert.That(owner.ShuffleRandomState, Is.EqualTo(ownerRandomAfter));
        }

        using (var drifted = new BattleCardZonesData(new[] { 5001, 5002 }, shuffleSeed: 7531))
        {
            BattlePreparedDraw plan = drifted.PrepareDraw(1);
            drifted.Draw(1);
            CardZoneLayoutData layoutAtReject = drifted.Layout.CurrentValue;
            uint randomAtReject = drifted.ShuffleRandomState;

            Assert.That(drifted.ValidatePreparedDraw(plan), Is.False);
            Assert.Throws<System.InvalidOperationException>(() => drifted.CommitPreparedDraw(plan));
            Assert.That(drifted.Layout.CurrentValue, Is.SameAs(layoutAtReject));
            Assert.That(drifted.ShuffleRandomState, Is.EqualTo(randomAtReject));
        }

        using (var fullHand = new BattleCardZonesData(Enumerable.Range(6001, 11), shuffleSeed: 8642))
        {
            fullHand.Draw(10);
            CardZoneLayoutData layoutBefore = fullHand.Layout.CurrentValue;
            uint randomBefore = fullHand.ShuffleRandomState;
            BattlePreparedDraw plan = fullHand.PrepareDraw(count: 3, handLimit: 10);
            int publicationCount = 0;
            BattleCardZoneOperationResult result;
            using (fullHand.Layout.Skip(1).Subscribe(_ => publicationCount++))
                result = fullHand.CommitPreparedDraw(plan);

            Assert.That(publicationCount, Is.Zero);
            Assert.That(fullHand.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(fullHand.ShuffleRandomState, Is.EqualTo(randomBefore));
            Assert.That(result.Settlements, Is.Empty);
        }
    }

    /// <summary>验证抽牌会先消耗残余抽牌堆，再按旧弃牌顺序重洗。</summary>
    [Test]
    public void Draw_WithResidualDrawPile_PreservesMixedOperationOrderAndOffset()
    {
        var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003, 3004 },
            shuffleSeed: 8642);
        zones.Draw(3);
        zones.DiscardHand();
        CardInstanceId residualDrawCard = zones.DrawPile[0];
        CardInstanceId[] discardOrder = new List<CardInstanceId>(zones.DiscardPile).ToArray();

        int layoutPublicationCount = 0;
        BattleCardZoneOperationResult result;
        using (zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            result = zones.Draw(3, startingOrder: 7);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(result.MovedCardCount, Is.EqualTo(6));
        Assert.That(result.Settlements.Count, Is.EqualTo(7));
        var residualMove = result.Settlements[0] as BattleCardMovedSettlement;
        Assert.That(residualMove, Is.Not.Null);
        Assert.That(residualMove.Order, Is.EqualTo(7));
        Assert.That(residualMove.CardId, Is.EqualTo(residualDrawCard));
        Assert.That(residualMove.FromZone, Is.EqualTo(BattleCardZone.DrawPile));
        Assert.That(residualMove.ToZone, Is.EqualTo(BattleCardZone.Hand));
        for (int index = 0; index < discardOrder.Length; index++)
        {
            var reshuffleMove = result.Settlements[index + 1] as BattleCardMovedSettlement;
            Assert.That(reshuffleMove, Is.Not.Null);
            Assert.That(reshuffleMove.Order, Is.EqualTo(index + 8));
            Assert.That(reshuffleMove.CardId, Is.EqualTo(discardOrder[index]));
            Assert.That(reshuffleMove.FromZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(reshuffleMove.ToZone, Is.EqualTo(BattleCardZone.DrawPile));
        }

        var reshuffled = result.Settlements[4] as BattleCardsReshuffledSettlement;
        Assert.That(reshuffled, Is.Not.Null);
        Assert.That(reshuffled.Order, Is.EqualTo(11));
        Assert.That(result.Settlements[5].Order, Is.EqualTo(12));
        Assert.That(result.Settlements[6].Order, Is.EqualTo(13));
        Assert.That(zones.Hand.Count, Is.EqualTo(3));
        Assert.That(zones.DrawPile.Count, Is.EqualTo(1));
        Assert.That(zones.DiscardPile, Is.Empty);
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证同一种子重洗会冻结完全相同的新抽牌堆顺序，而非仅保证卡牌集合相同。</summary>
    [Test]
    public void Draw_WithSameSeed_RecordsExactSameReshuffledOrder()
    {
        int[] templates = { 3001, 3002, 3003, 3004 };
        var firstZones = new BattleCardZonesData(templates, shuffleSeed: 7531);
        var secondZones = new BattleCardZonesData(templates, shuffleSeed: 7531);
        firstZones.Draw(templates.Length);
        secondZones.Draw(templates.Length);
        firstZones.DiscardHand();
        secondZones.DiscardHand();

        BattleCardZoneOperationResult firstResult = firstZones.Draw(2);
        BattleCardZoneOperationResult secondResult = secondZones.Draw(2);

        var firstReshuffle = firstResult.Settlements[4] as BattleCardsReshuffledSettlement;
        var secondReshuffle = secondResult.Settlements[4] as BattleCardsReshuffledSettlement;
        Assert.That(firstReshuffle, Is.Not.Null);
        Assert.That(secondReshuffle, Is.Not.Null);
        Assert.That(
            secondReshuffle.NewDrawPileOrder,
            Is.EqualTo(firstReshuffle.NewDrawPileOrder));
        Assert.That(secondZones.Hand, Is.EqualTo(firstZones.Hand));
        Assert.That(secondZones.DrawPile, Is.EqualTo(firstZones.DrawPile));
        Assert.That(secondZones.ShuffleRandomState, Is.EqualTo(firstZones.ShuffleRandomState));
    }

    [Test]
    public void DiscardAndExhaust_MoveOnlyTheRequestedHandInstances()
    {
        var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003 },
            shuffleSeed: 9753);
        zones.Draw(3);
        CardInstanceId discardedId = zones.Hand[0];
        CardInstanceId exhaustedId = zones.Hand[1];

        BattleCardZoneOperationResult discarded = zones.DiscardFromHand(discardedId);
        BattleCardZoneOperationResult exhausted = zones.ExhaustFromHand(exhaustedId);

        Assert.That(discarded.Succeeded, Is.True);
        Assert.That(discarded.Settlements.Count, Is.EqualTo(1));
        Assert.That(discarded.Settlements[0], Is.TypeOf<BattleCardMovedSettlement>());
        Assert.That(exhausted.Succeeded, Is.True);
        Assert.That(exhausted.Settlements.Count, Is.EqualTo(1));
        Assert.That(exhausted.Settlements[0], Is.TypeOf<BattleCardMovedSettlement>());
        Assert.That(zones.Hand.Count, Is.EqualTo(1));
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { discardedId }));
        Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { exhaustedId }));
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证指定牌不在手牌中时返回空失败结果，且不发布布局或推进随机流。</summary>
    [Test]
    public void DiscardFromHand_WhenCardIsMissing_ReturnsEmptyWithoutWrites()
    {
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 2468);
        zones.Draw(1);
        CardInstanceId cardId = zones.Hand[0];
        zones.DiscardFromHand(cardId);
        CardZoneLayoutData layoutBefore = zones.Layout.CurrentValue;
        uint randomBefore = zones.ShuffleRandomState;
        int layoutPublicationCount = 0;

        BattleCardZoneOperationResult result;
        using (zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            result = zones.DiscardFromHand(cardId, startingOrder: 7);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.MovedCardCount, Is.Zero);
        Assert.That(result.Settlements, Is.Empty);
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(randomBefore));
        Assert.That(layoutPublicationCount, Is.Zero);
    }

    [Test]
    public void Layout_PublishesTheNewHandAfterMovingACard()
    {
        var zones = new BattleCardZonesData(new[] { 3001, 3002 }, shuffleSeed: 9753);
        IReadOnlyList<CardInstanceId> observedHand = null;

        using (zones.Layout.Skip(1).Subscribe(layout => observedHand = layout.Hand))
        {
            Assert.That(zones.Draw(1).MovedCardCount, Is.EqualTo(1));
            CardInstanceId cardId = zones.Hand[0];
            Assert.That(zones.DiscardFromHand(cardId).Succeeded, Is.True);
        }

        Assert.That(zones.Hand, Is.Empty);
        Assert.That(observedHand, Is.Empty);
    }

    /// <summary>验证能力牌从手牌移出后只进入本局能力区，不会重新参与普通抽弃牌循环。</summary>
    [Test]
    public void MoveToPowerFromHand_MovesOnlyTheRequestedInstanceToPowerPile()
    {
        var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003 },
            shuffleSeed: 9753);
        zones.Draw(3);
        CardInstanceId powerCardId = zones.Hand[1];

        BattleCardZoneOperationResult result = zones.MoveToPowerFromHand(powerCardId, startingOrder: 4);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Settlements, Has.Count.EqualTo(1));
        var settlement = result.Settlements[0] as BattleCardMovedSettlement;
        Assert.That(settlement, Is.Not.Null);
        Assert.That(settlement.Order, Is.EqualTo(4));
        Assert.That(settlement.CardId, Is.EqualTo(powerCardId));
        Assert.That(settlement.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(settlement.ToZone, Is.EqualTo(BattleCardZone.PowerPile));
        Assert.That(zones.Hand.Contains(powerCardId), Is.False);
        Assert.That(zones.PowerPile, Is.EqualTo(new[] { powerCardId }));
        Assert.That(zones.DrawPile, Is.Empty);
        Assert.That(zones.DiscardPile, Is.Empty);
        Assert.That(zones.ExhaustPile, Is.Empty);
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证抽牌在到达职业手牌上限时停止，且不会多移动或重洗卡牌。</summary>
    [Test]
    public void Draw_WithHandLimit_StopsAtCapacity()
    {
        var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003, 3004 },
            shuffleSeed: 9753);
        zones.Draw(2, handLimit: 3);
        CardInstanceId[] handBefore = new List<CardInstanceId>(zones.Hand).ToArray();
        CardInstanceId[] drawBefore = new List<CardInstanceId>(zones.DrawPile).ToArray();

        BattleCardZoneOperationResult result = zones.Draw(2, handLimit: 3);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.MovedCardCount, Is.EqualTo(1));
        Assert.That(zones.Hand, Has.Count.EqualTo(3));
        Assert.That(zones.Hand.Take(2), Is.EqualTo(handBefore));
        Assert.That(zones.DrawPile, Has.Count.EqualTo(drawBefore.Length - 1));
        Assert.That(zones.DiscardPile, Is.Empty);
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证回合结束时可以按实例标识保留指定手牌，并保持其余弃牌的原手牌顺序。</summary>
    [Test]
    public void DiscardHandExcept_PreservesSelectedInstanceAndDiscardsTheRestInHandOrder()
    {
        var zones = new BattleCardZonesData(
            new[] { 3001, 3002, 3003, 3004 },
            shuffleSeed: 9753);
        zones.Draw(4);
        CardInstanceId[] handBefore = new List<CardInstanceId>(zones.Hand).ToArray();
        CardInstanceId firstRetainedCardId = handBefore[1];
        CardInstanceId secondRetainedCardId = handBefore[3];

        BattleCardZoneOperationResult result = zones.DiscardHandExcept(
            new[] { secondRetainedCardId, firstRetainedCardId },
            startingOrder: 8);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(
            zones.Hand,
            Is.EqualTo(new[] { firstRetainedCardId, secondRetainedCardId }));
        Assert.That(
            zones.DiscardPile,
            Is.EqualTo(new[] { handBefore[0], handBefore[2] }));
        Assert.That(result.Settlements, Has.Count.EqualTo(2));
        for (int index = 0; index < result.Settlements.Count; index++)
        {
            var settlement = result.Settlements[index] as BattleCardMovedSettlement;
            Assert.That(settlement, Is.Not.Null);
            Assert.That(settlement.Order, Is.EqualTo(8 + index));
            Assert.That(settlement.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(settlement.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        }

        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证职业生成的临时卡拥有本局唯一实例标识，并只追加到当前手牌而不改写初始牌组。</summary>
    [Test]
    public void AddTemporaryToHand_CreatesDistinctSessionInstances()
    {
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 9753);

        IReadOnlyList<CardInstanceId> created = zones.AddTemporaryToHand(templateId: 3201, count: 2);

        Assert.That(created, Has.Count.EqualTo(2));
        Assert.That(created[0], Is.Not.EqualTo(created[1]));
        Assert.That(zones.Cards, Has.Count.EqualTo(3));
        Assert.That(zones.Hand, Is.EqualTo(created));
        foreach (CardInstanceId cardId in created)
        {
            Assert.That(zones.TryGetCard(cardId, out CardInstanceData card), Is.True);
            Assert.That(card.TemplateId, Is.EqualTo(3201));
            Assert.That(card.OriginRunCardInstanceId, Is.Null);
            Assert.That(card.UpgradeLevel, Is.Zero);
        }

        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证手牌选择计划准备阶段零写，并以冻结序号一次发布被选牌与来源牌的最终归宿。</summary>
    [Test]
    public void HandCardSelectionResolution_PrepareWritesNothingAndCommitPublishesOneFrozenLayout()
    {
        using var zones = new BattleCardZonesData(
            new[] { 3244, 3203, 3204 },
            shuffleSeed: 8642);
        zones.Draw(3);
        CardInstanceId playedCardId = zones.Hand
            .Single(cardId => zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId selectedCardId = zones.Hand
            .Single(cardId => zones.Cards[cardId].TemplateId == 3203);
        CardInstanceId remainingCardId = zones.Hand
            .Single(cardId => zones.Cards[cardId].TemplateId == 3204);
        CardZoneLayoutData layoutBefore = zones.Layout.CurrentValue;
        CardInstanceId[] cardIdsBefore = zones.Cards.Keys.ToArray();
        uint randomBefore = zones.ShuffleRandomState;
        int publicationCount = 0;
        using var subscription = zones.Layout.Skip(1).Subscribe(_ => publicationCount++);

        BattlePreparedHandCardSelectionResolution plan =
            zones.PrepareHandCardSelectionResolution(
                selectedCardId,
                BattleCardZone.ExhaustPile,
                playedCardId,
                BattleCardZone.DiscardPile,
                selectedStartingOrder: 1,
                playedCardStartingOrder: 3);

        Assert.That(plan.Owner, Is.SameAs(zones));
        Assert.That(plan.InitialLayout, Is.SameAs(layoutBefore));
        Assert.That(plan.SelectedCardMovement.Order, Is.EqualTo(1));
        Assert.That(plan.PlayedCardDeparture.Order, Is.EqualTo(3));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(zones.Cards.Keys, Is.EquivalentTo(cardIdsBefore));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(randomBefore));
        Assert.That(publicationCount, Is.Zero);
        Assert.That(zones.ValidatePreparedHandCardSelectionResolution(plan), Is.True);

        BattleCardZoneOperationResult result =
            zones.CommitPreparedHandCardSelectionResolution(plan);

        Assert.That(publicationCount, Is.EqualTo(1));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(plan.NextLayout));
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Settlements, Has.Count.EqualTo(2));
        Assert.That(result.Settlements.Select(item => item.Order), Is.EqualTo(new[] { 1, 3 }));
        var selectedMovement = result.Settlements[0] as BattleCardMovedSettlement;
        Assert.That(selectedMovement, Is.Not.Null);
        Assert.That(selectedMovement.CardId, Is.EqualTo(selectedCardId));
        Assert.That(selectedMovement.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(selectedMovement.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        var playedCardDeparture = result.Settlements[1] as BattleCardMovedSettlement;
        Assert.That(playedCardDeparture, Is.Not.Null);
        Assert.That(playedCardDeparture.CardId, Is.EqualTo(playedCardId));
        Assert.That(playedCardDeparture.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(playedCardDeparture.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
        Assert.That(zones.Hand, Is.EqualTo(new[] { remainingCardId }));
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { playedCardId }));
        Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { selectedCardId }));
        Assert.That(zones.Cards.Keys, Is.EquivalentTo(cardIdsBefore));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(randomBefore));
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证手牌选择计划拒绝跨聚合、布局漂移与重复提交，且每次拒绝均不写实例、随机流或布局。</summary>
    [Test]
    public void HandCardSelectionResolution_RejectsCrossOwnerLayoutDriftAndDuplicateWithoutWrites()
    {
        using (var owner = new BattleCardZonesData(new[] { 3244, 3203 }, shuffleSeed: 1357))
        using (var other = new BattleCardZonesData(new[] { 3244, 3203 }, shuffleSeed: 2468))
        {
            owner.Draw(2);
            other.Draw(2);
            CardInstanceId playedCardId = owner.Hand
                .Single(cardId => owner.Cards[cardId].TemplateId == 3244);
            CardInstanceId selectedCardId = owner.Hand
                .Single(cardId => owner.Cards[cardId].TemplateId == 3203);
            BattlePreparedHandCardSelectionResolution plan =
                owner.PrepareHandCardSelectionResolution(
                    selectedCardId,
                    BattleCardZone.ExhaustPile,
                    playedCardId,
                    BattleCardZone.DiscardPile,
                    selectedStartingOrder: 1,
                    playedCardStartingOrder: 3);
            CardZoneLayoutData otherLayoutBefore = other.Layout.CurrentValue;
            CardInstanceId[] otherCardIdsBefore = other.Cards.Keys.ToArray();
            uint otherRandomBefore = other.ShuffleRandomState;
            int crossOwnerPublicationCount = 0;
            using (other.Layout.Skip(1).Subscribe(_ => crossOwnerPublicationCount++))
            {
                Assert.That(other.ValidatePreparedHandCardSelectionResolution(plan), Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => other.CommitPreparedHandCardSelectionResolution(plan));
            }

            Assert.That(other.Layout.CurrentValue, Is.SameAs(otherLayoutBefore));
            Assert.That(other.Cards.Keys, Is.EquivalentTo(otherCardIdsBefore));
            Assert.That(other.ShuffleRandomState, Is.EqualTo(otherRandomBefore));
            Assert.That(crossOwnerPublicationCount, Is.Zero);

            owner.CommitPreparedHandCardSelectionResolution(plan);
            CardZoneLayoutData ownerLayoutAfterCommit = owner.Layout.CurrentValue;
            CardInstanceId[] ownerCardIdsAfterCommit = owner.Cards.Keys.ToArray();
            uint ownerRandomAfterCommit = owner.ShuffleRandomState;
            int duplicatePublicationCount = 0;
            using (owner.Layout.Skip(1).Subscribe(_ => duplicatePublicationCount++))
            {
                Assert.That(owner.ValidatePreparedHandCardSelectionResolution(plan), Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => owner.CommitPreparedHandCardSelectionResolution(plan));
            }

            Assert.That(owner.Layout.CurrentValue, Is.SameAs(ownerLayoutAfterCommit));
            Assert.That(owner.Cards.Keys, Is.EquivalentTo(ownerCardIdsAfterCommit));
            Assert.That(owner.ShuffleRandomState, Is.EqualTo(ownerRandomAfterCommit));
            Assert.That(duplicatePublicationCount, Is.Zero);
            AssertEveryCardHasExactlyOneZone(owner);
        }

        using (var drifted = new BattleCardZonesData(
                   new[] { 3244, 3203, 3204 },
                   shuffleSeed: 7531))
        {
            drifted.Draw(3);
            CardInstanceId playedCardId = drifted.Hand
                .Single(cardId => drifted.Cards[cardId].TemplateId == 3244);
            CardInstanceId selectedCardId = drifted.Hand
                .Single(cardId => drifted.Cards[cardId].TemplateId == 3203);
            CardInstanceId driftCardId = drifted.Hand
                .Single(cardId => drifted.Cards[cardId].TemplateId == 3204);
            BattlePreparedHandCardSelectionResolution plan =
                drifted.PrepareHandCardSelectionResolution(
                    selectedCardId,
                    BattleCardZone.ExhaustPile,
                    playedCardId,
                    BattleCardZone.DiscardPile,
                    selectedStartingOrder: 1,
                    playedCardStartingOrder: 3);
            Assert.That(drifted.DiscardFromHand(driftCardId).Succeeded, Is.True);
            CardZoneLayoutData layoutAtReject = drifted.Layout.CurrentValue;
            CardInstanceId[] cardIdsAtReject = drifted.Cards.Keys.ToArray();
            uint randomAtReject = drifted.ShuffleRandomState;
            int publicationCount = 0;
            using (drifted.Layout.Skip(1).Subscribe(_ => publicationCount++))
            {
                Assert.That(drifted.ValidatePreparedHandCardSelectionResolution(plan), Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => drifted.CommitPreparedHandCardSelectionResolution(plan));
            }

            Assert.That(drifted.Layout.CurrentValue, Is.SameAs(layoutAtReject));
            Assert.That(drifted.Cards.Keys, Is.EquivalentTo(cardIdsAtReject));
            Assert.That(drifted.ShuffleRandomState, Is.EqualTo(randomAtReject));
            Assert.That(publicationCount, Is.Zero);
            AssertEveryCardHasExactlyOneZone(drifted);
        }
    }

    /// <summary>验证可选手牌消耗、旧弃牌重洗、抽二与来源牌归宿共享同一冻结计划，并且只能原子提交一次。</summary>
    [Test]
    public void SelectedHandCardDrawAndPlayedCardDeparture_PreparesWithoutWritesAndCommitsOneFrozenLayoutOnce()
    {
        using var zones = new BattleCardZonesData(
            new[] { 3125, 3201, 3202, 3203 },
            shuffleSeed: 8642);
        zones.Draw(4);
        CardInstanceId playedCardId = zones.Hand
            .Single(cardId => zones.Cards[cardId].TemplateId == 3125);
        CardInstanceId selectedCardId = zones.Hand
            .Single(cardId => zones.Cards[cardId].TemplateId == 3201);
        CardInstanceId[] oldDiscard = new[] { 3202, 3203 }
            .Select(templateId => zones.Hand
                .Single(cardId => zones.Cards[cardId].TemplateId == templateId))
            .ToArray();
        foreach (CardInstanceId cardId in oldDiscard)
            Assert.That(zones.DiscardFromHand(cardId).Succeeded, Is.True);

        CardZoneLayoutData layoutBefore = zones.Layout.CurrentValue;
        CardInstanceId[] cardIdsBefore = zones.Cards.Keys.ToArray();
        uint randomBefore = zones.ShuffleRandomState;
        int publicationCount = 0;
        using var subscription = zones.Layout.Skip(1).Subscribe(_ => publicationCount++);

        BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture plan =
            zones.PrepareSelectedHandCardDrawAndPlayedCardDeparture(
                selectedCardId,
                BattleCardZone.ExhaustPile,
                drawCount: 2,
                handLimit: 10,
                playedCardId,
                BattleCardZone.DiscardPile,
                startingOrder: 4);

        Assert.That(plan.Owner, Is.SameAs(zones));
        Assert.That(plan.InitialLayout, Is.SameAs(layoutBefore));
        Assert.That(plan.ShuffleRandomStateBefore, Is.EqualTo(randomBefore));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(zones.Cards.Keys, Is.EquivalentTo(cardIdsBefore));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(randomBefore));
        Assert.That(publicationCount, Is.Zero);
        Assert.That(
            zones.ValidatePreparedSelectedHandCardDrawAndPlayedCardDeparture(plan),
            Is.True);

        BattleCardZoneOperationResult result =
            zones.CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(plan);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Settlements, Has.Count.EqualTo(7));
        Assert.That(
            result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(4, 7)));
        var selectedMovement = result.Settlements[0] as BattleCardMovedSettlement;
        Assert.That(selectedMovement, Is.Not.Null);
        Assert.That(selectedMovement.CardId, Is.EqualTo(selectedCardId));
        Assert.That(selectedMovement.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(selectedMovement.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));

        BattleCardMovedSettlement[] reshuffleMovements = result.Settlements
            .Skip(1)
            .Take(2)
            .Cast<BattleCardMovedSettlement>()
            .ToArray();
        Assert.That(
            reshuffleMovements.Select(item => item.CardId),
            Is.EqualTo(oldDiscard));
        Assert.That(
            reshuffleMovements.All(item =>
                item.FromZone == BattleCardZone.DiscardPile &&
                item.ToZone == BattleCardZone.DrawPile),
            Is.True);
        var reshuffled = result.Settlements[3] as BattleCardsReshuffledSettlement;
        Assert.That(reshuffled, Is.Not.Null);
        Assert.That(reshuffled.NewDrawPileOrder, Is.EquivalentTo(oldDiscard));
        Assert.That(reshuffled.NewDrawPileOrder.Contains(selectedCardId), Is.False);
        Assert.That(reshuffled.NewDrawPileOrder.Contains(playedCardId), Is.False);

        BattleCardMovedSettlement[] drawnMovements = result.Settlements
            .Skip(4)
            .Take(2)
            .Cast<BattleCardMovedSettlement>()
            .ToArray();
        Assert.That(
            drawnMovements.Select(item => item.CardId),
            Is.EquivalentTo(oldDiscard));
        Assert.That(
            drawnMovements.All(item =>
                item.FromZone == BattleCardZone.DrawPile &&
                item.ToZone == BattleCardZone.Hand),
            Is.True);
        var playedCardDeparture = result.Settlements[6] as BattleCardMovedSettlement;
        Assert.That(playedCardDeparture, Is.Not.Null);
        Assert.That(playedCardDeparture.CardId, Is.EqualTo(playedCardId));
        Assert.That(playedCardDeparture.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(playedCardDeparture.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));

        Assert.That(publicationCount, Is.EqualTo(1));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(plan.NextLayout));
        Assert.That(zones.Hand, Is.EquivalentTo(oldDiscard));
        Assert.That(zones.DrawPile, Is.Empty);
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { playedCardId }));
        Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { selectedCardId }));
        Assert.That(zones.Cards.Keys, Is.EquivalentTo(cardIdsBefore));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(plan.ShuffleRandomStateAfter));
        AssertEveryCardHasExactlyOneZone(zones);

        CardZoneLayoutData layoutAfterCommit = zones.Layout.CurrentValue;
        uint randomAfterCommit = zones.ShuffleRandomState;
        Assert.That(
            zones.ValidatePreparedSelectedHandCardDrawAndPlayedCardDeparture(plan),
            Is.False);
        Assert.Throws<System.InvalidOperationException>(
            () => zones.CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(plan));
        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutAfterCommit));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(randomAfterCommit));
        Assert.That(publicationCount, Is.EqualTo(1));
    }

    /// <summary>验证选择抽牌归宿计划拒绝跨聚合、布局漂移、洗牌随机漂移与重复提交，且每次拒绝均不写实例、随机流或布局。</summary>
    [Test]
    public void SelectedHandCardDrawAndPlayedCardDeparture_RejectsCrossOwnerLayoutShuffleRandomDriftAndRepeatWithoutWrites()
    {
        using (var owner = new BattleCardZonesData(new[] { 3125, 3201 }, shuffleSeed: 1357))
        using (var other = new BattleCardZonesData(new[] { 3125, 3201 }, shuffleSeed: 1357))
        {
            owner.Draw(2);
            other.Draw(2);
            CardInstanceId playedCardId = owner.Hand
                .Single(cardId => owner.Cards[cardId].TemplateId == 3125);
            CardInstanceId selectedCardId = owner.Hand
                .Single(cardId => owner.Cards[cardId].TemplateId == 3201);
            BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture plan =
                owner.PrepareSelectedHandCardDrawAndPlayedCardDeparture(
                    selectedCardId,
                    BattleCardZone.ExhaustPile,
                    drawCount: 2,
                    handLimit: 10,
                    playedCardId,
                    BattleCardZone.DiscardPile,
                    startingOrder: 0);
            CardZoneLayoutData layoutAtReject = other.Layout.CurrentValue;
            CardInstanceId[] cardIdsAtReject = other.Cards.Keys.ToArray();
            uint randomAtReject = other.ShuffleRandomState;
            int publicationCount = 0;
            using (other.Layout.Skip(1).Subscribe(_ => publicationCount++))
            {
                Assert.That(
                    other.ValidatePreparedSelectedHandCardDrawAndPlayedCardDeparture(plan),
                    Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => other.CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(plan));
            }

            Assert.That(other.Layout.CurrentValue, Is.SameAs(layoutAtReject));
            Assert.That(other.Cards.Keys, Is.EquivalentTo(cardIdsAtReject));
            Assert.That(other.ShuffleRandomState, Is.EqualTo(randomAtReject));
            Assert.That(publicationCount, Is.Zero);
        }

        using (var layoutDrift = new BattleCardZonesData(
                   new[] { 3125, 3201, 3202 },
                   shuffleSeed: 2468))
        {
            layoutDrift.Draw(3);
            CardInstanceId playedCardId = layoutDrift.Hand
                .Single(cardId => layoutDrift.Cards[cardId].TemplateId == 3125);
            CardInstanceId selectedCardId = layoutDrift.Hand
                .Single(cardId => layoutDrift.Cards[cardId].TemplateId == 3201);
            CardInstanceId driftCardId = layoutDrift.Hand
                .Single(cardId => layoutDrift.Cards[cardId].TemplateId == 3202);
            BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture plan =
                layoutDrift.PrepareSelectedHandCardDrawAndPlayedCardDeparture(
                    selectedCardId,
                    BattleCardZone.ExhaustPile,
                    drawCount: 2,
                    handLimit: 10,
                    playedCardId,
                    BattleCardZone.DiscardPile,
                    startingOrder: 0);
            Assert.That(layoutDrift.DiscardFromHand(driftCardId).Succeeded, Is.True);
            CardZoneLayoutData layoutAtReject = layoutDrift.Layout.CurrentValue;
            CardInstanceId[] cardIdsAtReject = layoutDrift.Cards.Keys.ToArray();
            uint randomAtReject = layoutDrift.ShuffleRandomState;
            int publicationCount = 0;
            using (layoutDrift.Layout.Skip(1).Subscribe(_ => publicationCount++))
            {
                Assert.That(
                    layoutDrift.ValidatePreparedSelectedHandCardDrawAndPlayedCardDeparture(plan),
                    Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => layoutDrift.CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(plan));
            }

            Assert.That(layoutDrift.Layout.CurrentValue, Is.SameAs(layoutAtReject));
            Assert.That(layoutDrift.Cards.Keys, Is.EquivalentTo(cardIdsAtReject));
            Assert.That(layoutDrift.ShuffleRandomState, Is.EqualTo(randomAtReject));
            Assert.That(publicationCount, Is.Zero);
        }

        using (var randomDrift = new BattleCardZonesData(
                   new[] { 3125, 3201 },
                   shuffleSeed: 7531))
        {
            randomDrift.Draw(2);
            CardInstanceId playedCardId = randomDrift.Hand
                .Single(cardId => randomDrift.Cards[cardId].TemplateId == 3125);
            CardInstanceId selectedCardId = randomDrift.Hand
                .Single(cardId => randomDrift.Cards[cardId].TemplateId == 3201);
            BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture plan =
                randomDrift.PrepareSelectedHandCardDrawAndPlayedCardDeparture(
                    selectedCardId,
                    BattleCardZone.ExhaustPile,
                    drawCount: 2,
                    handLimit: 10,
                    playedCardId,
                    BattleCardZone.DiscardPile,
                    startingOrder: 0);
            var shuffleRandomField = typeof(BattleCardZonesData).GetField(
                "_shuffleRandom",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(shuffleRandomField, Is.Not.Null);
            var shuffleRandom = shuffleRandomField.GetValue(randomDrift) as GameRandom;
            Assert.That(shuffleRandom, Is.Not.Null);
            shuffleRandom.State = plan.ShuffleRandomStateBefore == uint.MaxValue
                ? 1u
                : plan.ShuffleRandomStateBefore + 1u;
            CardZoneLayoutData layoutAtReject = randomDrift.Layout.CurrentValue;
            CardInstanceId[] cardIdsAtReject = randomDrift.Cards.Keys.ToArray();
            uint randomAtReject = randomDrift.ShuffleRandomState;
            int publicationCount = 0;
            using (randomDrift.Layout.Skip(1).Subscribe(_ => publicationCount++))
            {
                Assert.That(
                    randomDrift.ValidatePreparedSelectedHandCardDrawAndPlayedCardDeparture(plan),
                    Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => randomDrift.CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(plan));
            }

            Assert.That(randomDrift.Layout.CurrentValue, Is.SameAs(layoutAtReject));
            Assert.That(randomDrift.Cards.Keys, Is.EquivalentTo(cardIdsAtReject));
            Assert.That(randomDrift.ShuffleRandomState, Is.EqualTo(randomAtReject));
            Assert.That(publicationCount, Is.Zero);
        }

        using (var repeated = new BattleCardZonesData(
                   new[] { 3125, 3201 },
                   shuffleSeed: 8642))
        {
            repeated.Draw(2);
            CardInstanceId playedCardId = repeated.Hand
                .Single(cardId => repeated.Cards[cardId].TemplateId == 3125);
            CardInstanceId selectedCardId = repeated.Hand
                .Single(cardId => repeated.Cards[cardId].TemplateId == 3201);
            BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture plan =
                repeated.PrepareSelectedHandCardDrawAndPlayedCardDeparture(
                    selectedCardId,
                    BattleCardZone.ExhaustPile,
                    drawCount: 2,
                    handLimit: 10,
                    playedCardId,
                    BattleCardZone.DiscardPile,
                    startingOrder: 0);
            repeated.CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(plan);
            CardZoneLayoutData layoutAtReject = repeated.Layout.CurrentValue;
            CardInstanceId[] cardIdsAtReject = repeated.Cards.Keys.ToArray();
            uint randomAtReject = repeated.ShuffleRandomState;
            int publicationCount = 0;
            using (repeated.Layout.Skip(1).Subscribe(_ => publicationCount++))
            {
                Assert.That(
                    repeated.ValidatePreparedSelectedHandCardDrawAndPlayedCardDeparture(plan),
                    Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => repeated.CommitPreparedSelectedHandCardDrawAndPlayedCardDeparture(plan));
            }

            Assert.That(repeated.Layout.CurrentValue, Is.SameAs(layoutAtReject));
            Assert.That(repeated.Cards.Keys, Is.EquivalentTo(cardIdsAtReject));
            Assert.That(repeated.ShuffleRandomState, Is.EqualTo(randomAtReject));
            Assert.That(publicationCount, Is.Zero);
            AssertEveryCardHasExactlyOneZone(repeated);
        }
    }

    /// <summary>验证出牌离手后按十张上限补牌，准备阶段零写且提交只发布一次不超过上限的最终布局。</summary>
    [Test]
    public void PlayedCardDepartureAndDrawToHandLimit_UsesPostDepartureCapacityWithoutTransientOverflow()
    {
        using (var oneCardHand = new BattleCardZonesData(
                   Enumerable.Range(3001, 11),
                   shuffleSeed: 8642))
        {
            oneCardHand.Draw(1);
            CardInstanceId playedCardId = oneCardHand.Hand.Single();
            CardZoneLayoutData layoutBefore = oneCardHand.Layout.CurrentValue;
            uint randomBefore = oneCardHand.ShuffleRandomState;

            var plan = oneCardHand.PreparePlayedCardDepartureAndDrawToHandLimit(
                playedCardId,
                BattleCardZone.DiscardPile,
                handLimit: 10,
                startingOrder: 7);

            Assert.That(oneCardHand.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(oneCardHand.ShuffleRandomState, Is.EqualTo(randomBefore));
            int publicationCount = 0;
            int maximumObservedHand = oneCardHand.Hand.Count;
            BattleCardZoneOperationResult result;
            using (oneCardHand.Layout.Skip(1).Subscribe(layout =>
                   {
                       publicationCount++;
                       maximumObservedHand = System.Math.Max(maximumObservedHand, layout.Hand.Count);
                   }))
            {
                result = oneCardHand.CommitPreparedPlayedCardDepartureAndDraw(plan);
            }

            Assert.That(publicationCount, Is.EqualTo(1));
            Assert.That(maximumObservedHand, Is.EqualTo(10));
            Assert.That(oneCardHand.Hand, Has.Count.EqualTo(10));
            Assert.That(oneCardHand.DiscardPile, Is.EqualTo(new[] { playedCardId }));
            Assert.That(oneCardHand.ShuffleRandomState, Is.EqualTo(randomBefore));
            Assert.That(result.Settlements.Select(item => item.Order),
                Is.EqualTo(Enumerable.Range(7, result.Settlements.Count)));
            BattleCardMovedSettlement departure = result.Settlements[0] as BattleCardMovedSettlement;
            Assert.That(departure, Is.Not.Null);
            Assert.That(departure.CardId, Is.EqualTo(playedCardId));
            Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            AssertEveryCardHasExactlyOneZone(oneCardHand);
        }

        using (var fullHand = new BattleCardZonesData(
                   Enumerable.Range(4001, 11),
                   shuffleSeed: 9753))
        {
            fullHand.Draw(10);
            CardInstanceId playedCardId = fullHand.Hand[4];
            uint randomBefore = fullHand.ShuffleRandomState;
            var plan = fullHand.PreparePlayedCardDepartureAndDrawToHandLimit(
                playedCardId,
                BattleCardZone.DiscardPile,
                handLimit: 10,
                startingOrder: 0);

            BattleCardZoneOperationResult result =
                fullHand.CommitPreparedPlayedCardDepartureAndDraw(plan);

            Assert.That(fullHand.Hand, Has.Count.EqualTo(10));
            Assert.That(fullHand.Hand.Contains(playedCardId), Is.False);
            Assert.That(fullHand.DiscardPile, Is.EqualTo(new[] { playedCardId }));
            Assert.That(fullHand.DrawPile, Is.Empty);
            Assert.That(fullHand.ShuffleRandomState, Is.EqualTo(randomBefore));
            Assert.That(result.Settlements.OfType<BattleCardsReshuffledSettlement>(), Is.Empty);
            AssertEveryCardHasExactlyOneZone(fullHand);
        }
    }

    /// <summary>验证牌源不足时只重洗旧弃牌并抽取可用牌，当前结算卡始终排除在同次重洗之外。</summary>
    [Test]
    public void PlayedCardDepartureAndDrawToHandLimit_ReshufflesOldDiscardButExcludesResolvingCard()
    {
        using var zones = new BattleCardZonesData(
            Enumerable.Range(5001, 7),
            shuffleSeed: 1357);
        zones.Draw(5);
        CardInstanceId playedCardId = zones.Hand[0];
        CardInstanceId[] oldDiscard = zones.Hand.Skip(1).ToArray();
        foreach (CardInstanceId cardId in oldDiscard)
            zones.DiscardFromHand(cardId);
        Assert.That(zones.Hand, Is.EqualTo(new[] { playedCardId }));
        Assert.That(zones.DrawPile, Has.Count.EqualTo(2));
        Assert.That(zones.DiscardPile, Is.EqualTo(oldDiscard));
        uint randomBefore = zones.ShuffleRandomState;
        int publicationCount = 0;

        var plan = zones.PreparePlayedCardDepartureAndDrawToHandLimit(
            playedCardId,
            BattleCardZone.DiscardPile,
            handLimit: 10,
            startingOrder: 3);
        BattleCardZoneOperationResult result;
        using (zones.Layout.Skip(1).Subscribe(_ => publicationCount++))
            result = zones.CommitPreparedPlayedCardDepartureAndDraw(plan);

        BattleCardsReshuffledSettlement reshuffle = result.Settlements
            .OfType<BattleCardsReshuffledSettlement>()
            .Single();
        Assert.That(publicationCount, Is.EqualTo(1));
        Assert.That(zones.Hand, Has.Count.EqualTo(6));
        Assert.That(zones.DiscardPile, Is.EqualTo(new[] { playedCardId }));
        Assert.That(zones.DrawPile, Is.Empty);
        Assert.That(zones.ShuffleRandomState, Is.Not.EqualTo(randomBefore));
        Assert.That(reshuffle.NewDrawPileOrder, Is.EquivalentTo(oldDiscard));
        Assert.That(reshuffle.NewDrawPileOrder.Contains(playedCardId), Is.False);
        Assert.That(result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .Where(item => item.FromZone == BattleCardZone.DiscardPile)
            .Select(item => item.CardId),
            Is.EquivalentTo(oldDiscard));
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(3, result.Settlements.Count)));
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证准备后布局或洗牌随机流发生漂移时提交会拒绝，且拒绝本身不再发布布局或推进随机流。</summary>
    [Test]
    public void PlayedCardDepartureAndDrawToHandLimit_RejectsPreparedStateDriftWithoutCommitWrites()
    {
        using (var layoutDrift = new BattleCardZonesData(
                   Enumerable.Range(6001, 4),
                   shuffleSeed: 2468))
        {
            layoutDrift.Draw(1);
            CardInstanceId playedCardId = layoutDrift.Hand.Single();
            var plan = layoutDrift.PreparePlayedCardDepartureAndDrawToHandLimit(
                playedCardId,
                BattleCardZone.DiscardPile,
                handLimit: 10,
                startingOrder: 0);
            layoutDrift.Draw(1);
            CardZoneLayoutData layoutAtCommit = layoutDrift.Layout.CurrentValue;
            uint randomAtCommit = layoutDrift.ShuffleRandomState;
            int publicationCount = 0;

            using (layoutDrift.Layout.Skip(1).Subscribe(_ => publicationCount++))
            {
                Assert.That(
                    layoutDrift.ValidatePreparedPlayedCardDepartureAndDraw(plan),
                    Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => layoutDrift.CommitPreparedPlayedCardDepartureAndDraw(plan));
            }

            Assert.That(layoutDrift.Layout.CurrentValue, Is.SameAs(layoutAtCommit));
            Assert.That(layoutDrift.ShuffleRandomState, Is.EqualTo(randomAtCommit));
            Assert.That(publicationCount, Is.Zero);
        }

        using (var shuffleDrift = new BattleCardZonesData(
                   Enumerable.Range(7001, 5),
                   shuffleSeed: 7531))
        {
            shuffleDrift.Draw(5);
            CardInstanceId playedCardId = shuffleDrift.Hand[0];
            foreach (CardInstanceId cardId in shuffleDrift.Hand.Skip(1).ToArray())
                shuffleDrift.DiscardFromHand(cardId);
            var plan = shuffleDrift.PreparePlayedCardDepartureAndDrawToHandLimit(
                playedCardId,
                BattleCardZone.DiscardPile,
                handLimit: 10,
                startingOrder: 0);
            shuffleDrift.Draw(1);
            CardZoneLayoutData layoutAtCommit = shuffleDrift.Layout.CurrentValue;
            uint randomAtCommit = shuffleDrift.ShuffleRandomState;
            int publicationCount = 0;

            using (shuffleDrift.Layout.Skip(1).Subscribe(_ => publicationCount++))
            {
                Assert.That(
                    shuffleDrift.ValidatePreparedPlayedCardDepartureAndDraw(plan),
                    Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => shuffleDrift.CommitPreparedPlayedCardDepartureAndDraw(plan));
            }

            Assert.That(shuffleDrift.Layout.CurrentValue, Is.SameAs(layoutAtCommit));
            Assert.That(shuffleDrift.ShuffleRandomState, Is.EqualTo(randomAtCommit));
            Assert.That(publicationCount, Is.Zero);
        }
    }

    /// <summary>验证换手机枪牌会在准备阶段保持零写，并在提交时按原手牌顺序弃牌、消耗来源、创建等量临时牌且只发布一次布局。</summary>
    [Test]
    public void PlayedCardDepartureDiscardHandAndCreate_CommitsOneAtomicLayoutWithExplicitCreationSettlements()
    {
        using var zones = new BattleCardZonesData(
            new[] { 3261, 3201, 3202, 3203 },
            shuffleSeed: 8642);
        zones.Draw(4);
        CardInstanceId resolvingCardId = zones.Hand
            .Single(cardId => zones.Cards[cardId].TemplateId == 3261);
        CardInstanceId[] discardedCardIds = zones.Hand
            .Where(cardId => cardId != resolvingCardId)
            .ToArray();
        CardInstanceId[] originalCardIds = zones.Cards.Keys.ToArray();
        CardZoneLayoutData layoutBefore = zones.Layout.CurrentValue;
        uint shuffleRandomBefore = zones.ShuffleRandomState;

        BattlePreparedPlayedCardDepartureDiscardHandAndCreate plan =
            zones.PreparePlayedCardDepartureDiscardHandAndCreate(
                resolvingCardId,
                BattleCardZone.ExhaustPile,
                createdTemplateId: 3263,
                startingOrder: 7);

        Assert.That(zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
        Assert.That(zones.Cards.Keys, Is.EquivalentTo(originalCardIds));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        Assert.That(zones.ValidatePreparedPlayedCardDepartureDiscardHandAndCreate(plan), Is.True);

        int layoutPublicationCount = 0;
        BattleCardZoneOperationResult result;
        using (zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            result = zones.CommitPreparedPlayedCardDepartureDiscardHandAndCreate(plan);

        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Settlements.Select(item => item.Order),
            Is.EqualTo(Enumerable.Range(7, 7)));
        BattleCardMovedSettlement[] moves = result.Settlements
            .OfType<BattleCardMovedSettlement>()
            .ToArray();
        Assert.That(moves, Has.Length.EqualTo(4));
        Assert.That(moves[0].Order, Is.EqualTo(7));
        Assert.That(moves[0].CardId, Is.EqualTo(resolvingCardId));
        Assert.That(moves[0].FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(moves[0].ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(moves.Skip(1).Select(item => item.Order), Is.EqualTo(new[] { 8, 9, 10 }));
        Assert.That(moves.Skip(1).Select(item => item.CardId), Is.EqualTo(discardedCardIds));
        Assert.That(moves.Skip(1).All(item =>
            item.FromZone == BattleCardZone.Hand &&
            item.ToZone == BattleCardZone.DiscardPile), Is.True);

        BattleCardCreatedSettlement[] created = result.Settlements
            .OfType<BattleCardCreatedSettlement>()
            .ToArray();
        Assert.That(created, Has.Length.EqualTo(3));
        Assert.That(created.Select(item => item.Order), Is.EqualTo(new[] { 11, 12, 13 }));
        Assert.That(created.Select(item => item.TemplateId), Is.EqualTo(new[] { 3263, 3263, 3263 }));
        Assert.That(created.All(item => item.ToZone == BattleCardZone.Hand), Is.True);
        Assert.That(created.Select(item => item.CardId).Distinct().Count(), Is.EqualTo(3));
        Assert.That(zones.Hand, Is.EqualTo(created.Select(item => item.CardId)));
        Assert.That(zones.DiscardPile, Is.EqualTo(discardedCardIds));
        Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { resolvingCardId }));
        Assert.That(zones.Cards, Has.Count.EqualTo(7));
        foreach (BattleCardCreatedSettlement settlement in created)
        {
            Assert.That(zones.TryGetCard(settlement.CardId, out CardInstanceData card), Is.True);
            Assert.That(card.TemplateId, Is.EqualTo(3263));
        }

        Assert.That(zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证手中只有当前机枪牌时零换零仍合法，只消耗来源牌且不创建实例、不推进随机。</summary>
    [Test]
    public void PlayedCardDepartureDiscardHandAndCreate_AllowsZeroReplacement()
    {
        using var zones = new BattleCardZonesData(new[] { 3261 }, shuffleSeed: 9753);
        zones.Draw(1);
        CardInstanceId resolvingCardId = zones.Hand.Single();
        uint shuffleRandomBefore = zones.ShuffleRandomState;
        int cardCountBefore = zones.Cards.Count;
        int layoutPublicationCount = 0;

        BattlePreparedPlayedCardDepartureDiscardHandAndCreate plan =
            zones.PreparePlayedCardDepartureDiscardHandAndCreate(
                resolvingCardId,
                BattleCardZone.ExhaustPile,
                createdTemplateId: 3263,
                startingOrder: 3);
        BattleCardZoneOperationResult result;
        using (zones.Layout.Skip(1).Subscribe(_ => layoutPublicationCount++))
            result = zones.CommitPreparedPlayedCardDepartureDiscardHandAndCreate(plan);

        Assert.That(layoutPublicationCount, Is.EqualTo(1));
        Assert.That(result.Settlements, Has.Count.EqualTo(1));
        BattleCardMovedSettlement departure = result.Settlements.Single()
            as BattleCardMovedSettlement;
        Assert.That(departure, Is.Not.Null);
        Assert.That(departure.Order, Is.EqualTo(3));
        Assert.That(departure.CardId, Is.EqualTo(resolvingCardId));
        Assert.That(departure.FromZone, Is.EqualTo(BattleCardZone.Hand));
        Assert.That(departure.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
        Assert.That(result.Settlements.OfType<BattleCardCreatedSettlement>(), Is.Empty);
        Assert.That(zones.Hand, Is.Empty);
        Assert.That(zones.DiscardPile, Is.Empty);
        Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { resolvingCardId }));
        Assert.That(zones.Cards, Has.Count.EqualTo(cardCountBefore));
        Assert.That(zones.ShuffleRandomState, Is.EqualTo(shuffleRandomBefore));
        AssertEveryCardHasExactlyOneZone(zones);
    }

    /// <summary>验证冻结换手计划拒绝跨聚合、状态漂移与重复提交，且每次拒绝本身都不再写布局、实例或随机流。</summary>
    [Test]
    public void PlayedCardDepartureDiscardHandAndCreate_RejectsCrossOwnerDriftAndDuplicateWithoutWrites()
    {
        using (var owner = new BattleCardZonesData(new[] { 3261, 3201 }, shuffleSeed: 1357))
        using (var other = new BattleCardZonesData(new[] { 3261, 3201 }, shuffleSeed: 2468))
        {
            owner.Draw(2);
            other.Draw(2);
            CardInstanceId resolvingCardId = owner.Hand
                .Single(cardId => owner.Cards[cardId].TemplateId == 3261);
            BattlePreparedPlayedCardDepartureDiscardHandAndCreate plan =
                owner.PreparePlayedCardDepartureDiscardHandAndCreate(
                    resolvingCardId,
                    BattleCardZone.ExhaustPile,
                    createdTemplateId: 3263,
                    startingOrder: 0);
            CardZoneLayoutData otherLayoutBefore = other.Layout.CurrentValue;
            int otherCardCountBefore = other.Cards.Count;
            uint otherRandomBefore = other.ShuffleRandomState;
            int otherPublicationCount = 0;

            using (other.Layout.Skip(1).Subscribe(_ => otherPublicationCount++))
            {
                Assert.That(other.ValidatePreparedPlayedCardDepartureDiscardHandAndCreate(plan), Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => other.CommitPreparedPlayedCardDepartureDiscardHandAndCreate(plan));
            }

            Assert.That(other.Layout.CurrentValue, Is.SameAs(otherLayoutBefore));
            Assert.That(other.Cards, Has.Count.EqualTo(otherCardCountBefore));
            Assert.That(other.ShuffleRandomState, Is.EqualTo(otherRandomBefore));
            Assert.That(otherPublicationCount, Is.Zero);

            owner.CommitPreparedPlayedCardDepartureDiscardHandAndCreate(plan);
            CardZoneLayoutData ownerLayoutAfterCommit = owner.Layout.CurrentValue;
            int ownerCardCountAfterCommit = owner.Cards.Count;
            uint ownerRandomAfterCommit = owner.ShuffleRandomState;
            int duplicatePublicationCount = 0;
            using (owner.Layout.Skip(1).Subscribe(_ => duplicatePublicationCount++))
            {
                Assert.That(owner.ValidatePreparedPlayedCardDepartureDiscardHandAndCreate(plan), Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => owner.CommitPreparedPlayedCardDepartureDiscardHandAndCreate(plan));
            }

            Assert.That(owner.Layout.CurrentValue, Is.SameAs(ownerLayoutAfterCommit));
            Assert.That(owner.Cards, Has.Count.EqualTo(ownerCardCountAfterCommit));
            Assert.That(owner.ShuffleRandomState, Is.EqualTo(ownerRandomAfterCommit));
            Assert.That(duplicatePublicationCount, Is.Zero);
        }

        using (var drifted = new BattleCardZonesData(new[] { 3261, 3201 }, shuffleSeed: 7531))
        {
            drifted.Draw(2);
            CardInstanceId resolvingCardId = drifted.Hand
                .Single(cardId => drifted.Cards[cardId].TemplateId == 3261);
            BattlePreparedPlayedCardDepartureDiscardHandAndCreate plan =
                drifted.PreparePlayedCardDepartureDiscardHandAndCreate(
                    resolvingCardId,
                    BattleCardZone.ExhaustPile,
                    createdTemplateId: 3263,
                    startingOrder: 0);
            drifted.AddTemporaryToHand(templateId: 9999, count: 1);
            CardZoneLayoutData layoutAtReject = drifted.Layout.CurrentValue;
            int cardCountAtReject = drifted.Cards.Count;
            uint randomAtReject = drifted.ShuffleRandomState;
            int publicationCount = 0;

            using (drifted.Layout.Skip(1).Subscribe(_ => publicationCount++))
            {
                Assert.That(drifted.ValidatePreparedPlayedCardDepartureDiscardHandAndCreate(plan), Is.False);
                Assert.Throws<System.InvalidOperationException>(
                    () => drifted.CommitPreparedPlayedCardDepartureDiscardHandAndCreate(plan));
            }

            Assert.That(drifted.Layout.CurrentValue, Is.SameAs(layoutAtReject));
            Assert.That(drifted.Cards, Has.Count.EqualTo(cardCountAtReject));
            Assert.That(drifted.ShuffleRandomState, Is.EqualTo(randomAtReject));
            Assert.That(publicationCount, Is.Zero);
        }
    }

    /// <summary>按当前手牌顺序返回对应模板标识，供确定性抽牌断言复用。</summary>
    private static IReadOnlyList<int> GetTemplateOrder(BattleCardZonesData zones)
    {
        var templateIds = new List<int>(zones.Hand.Count);
        foreach (CardInstanceId cardId in zones.Hand)
            templateIds.Add(zones.Cards[cardId].TemplateId);

        return templateIds;
    }

    /// <summary>确认每张本局卡牌实例恰好属于一个权威卡区。</summary>
    private static void AssertEveryCardHasExactlyOneZone(BattleCardZonesData zones)
    {
        var occurrences = new Dictionary<CardInstanceId, int>();
        foreach (CardInstanceId cardId in zones.Cards.Keys)
            occurrences.Add(cardId, 0);

        CountZone(zones.DrawPile, occurrences);
        CountZone(zones.Hand, occurrences);
        CountZone(zones.DiscardPile, occurrences);
        CountZone(zones.ExhaustPile, occurrences);
        CountZone(zones.PowerPile, occurrences);

        foreach (int count in occurrences.Values)
            Assert.That(count, Is.EqualTo(1));
    }

    /// <summary>把指定卡区中的实例出现次数累加到共享计数表。</summary>
    private static void CountZone(
        IReadOnlyList<CardInstanceId> zone,
        IDictionary<CardInstanceId, int> occurrences)
    {
        foreach (CardInstanceId cardId in zone)
            occurrences[cardId]++;
    }
}
