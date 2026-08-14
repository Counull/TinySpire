using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class HandCardMotionTests
{
    private const string CardPrefabPath = "Assets/Arts/Runtime/Card/Prefab/CardView.prefab";

    /// <summary>验证权威 Hand→Discard 发布后，同一 View 立即退出交互手牌并以非交互 transient 保留。</summary>
    [Test]
    public void RebuildCards_HandToDiscard_DetachesNonInteractiveTransientBeforeDestroy()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject containerObject = new GameObject("HandCardMotionContainer");
        GameObject cardObject = Object.Instantiate(prefab);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 1357);
        try
        {
            zones.Draw(1);
            CardInstanceId cardId = zones.Hand[0];
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(40f, -320f), 5f, 0));
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            typeof(HandCardContainer).GetField(
                    "_cardZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(container, zones);
            var interactiveCards = typeof(HandCardContainer).GetField(
                    "_cards",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(container) as List<HandCardVisual>;
            Assert.That(interactiveCards, Is.Not.Null);
            interactiveCards.Add(visual);
            zones.DiscardFromHand(cardId);
            int unexpectedLayoutPublicationCount = 0;
            using IDisposable subscription = zones.Layout
                .Skip(1)
                .Subscribe(_ => unexpectedLayoutPublicationCount++);

            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(container, new object[] { false });

            FieldInfo transientCardsField = typeof(HandCardContainer).GetField(
                "_transientCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(transientCardsField, Is.Not.Null);
            var transientCards = transientCardsField.GetValue(container)
                as Dictionary<CardInstanceId, HandCardVisual>;
            Assert.That(interactiveCards, Is.Empty);
            Assert.That(transientCards, Is.Not.Null);
            Assert.That(transientCards, Does.ContainKey(cardId));
            Assert.That(transientCards[cardId], Is.SameAs(visual));
            Assert.That(canvasGroup.interactable, Is.False);
            Assert.That(canvasGroup.blocksRaycasts, Is.False);
            Assert.That(visual.IsCommandPending, Is.False);
            Assert.That(unexpectedLayoutPublicationCount, Is.Zero);
        }
        finally
        {
            zones.Dispose();
            if (cardObject != null)
                Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>验证出牌前奏只持有 transient，卡牌只在原 Order 的弃牌步骤运动并在 runner 收口时清理一次。</summary>
    [Test]
    public void PlayCardPreludeHoldsTransient_CardMovesOnlyToDiscardThenCleansExactlyOnce()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject containerObject = new GameObject("HandCardMotionPlaybackContainer");
        GameObject cardObject = Object.Instantiate(prefab);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 2468);
        try
        {
            zones.Draw(1);
            CardInstanceId cardId = zones.Hand[0];
            var playerId = new CombatantId(1001);
            var enemyId = new CombatantId(2001);
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, -320f), 0f, 0));
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            int transientDestroyCount = 0;
            container.ConfigureTransientCardDestroyForTesting(_ => transientDestroyCount++);
            typeof(HandCardContainer).GetField(
                    "_cardZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(container, zones);
            var interactiveCards = typeof(HandCardContainer).GetField(
                    "_cards",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(container) as List<HandCardVisual>;
            Assert.That(interactiveCards, Is.Not.Null);
            interactiveCards.Add(visual);
            zones.DiscardFromHand(cardId);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(container, new object[] { false });
            Vector2 originalScreenPosition = visual.GetScreenCenter();
            Vector2 discardScreenPosition = originalScreenPosition + new Vector2(360f, 80f);
            var energySpent = new BattleEnergySpentSettlement(
                order: 0,
                playerId,
                energyBefore: 3,
                energyAfter: 2);
            var damage = new BattleDamageAppliedSettlement(
                order: 1,
                new BattleEffectId(4001),
                playerId,
                enemyId,
                attackValue: 6,
                blockBefore: 0,
                blockAfter: 0,
                healthBefore: 20,
                healthAfter: 14);
            var moved = new BattleCardMovedSettlement(
                order: 2,
                cardId,
                BattleCardZone.Hand,
                BattleCardZone.DiscardPile);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 2,
                BattleCommandType.PlayCard,
                playerId,
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[] { energySpent, damage, moved });
            BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
            var cardMotionFactory = new BattleCardMotionTweenFactory(cue =>
            {
                if (cue.Kind == BattleCardMotionCueKind.PlayCardTransientHold)
                    return container.CreateTransientCardHoldTween(cue);

                return container.CreateTransientCardMotionTween(
                    cue,
                    discardScreenPosition,
                    duration: 0.2f,
                    ease: Ease.Linear);
            });
            using var runner = new BattleCommandPresentationRunner(
                prelude => cardMotionFactory.TryCreate(
                    prelude,
                    out BattleCommandPresentationTween tween)
                        ? tween
                        : CreateZeroDurationTween(),
                step => cardMotionFactory.TryCreate(
                    step,
                    out BattleCommandPresentationTween tween)
                        ? tween
                        : CreateZeroDurationTween());
            int completionCount = 0;
            int unexpectedLayoutPublicationCount = 0;
            using IDisposable subscription = zones.Layout
                .Skip(1)
                .Subscribe(_ => unexpectedLayoutPublicationCount++);

            runner.Play(plan, () => completionCount++);
            runner.Tick(0.1f);

            Vector2 midpoint = visual.GetScreenCenter();
            Assert.That(midpoint.x, Is.GreaterThan(originalScreenPosition.x));
            Assert.That(midpoint.x, Is.LessThan(discardScreenPosition.x));
            Assert.That(midpoint.y, Is.GreaterThan(originalScreenPosition.y));
            Assert.That(midpoint.y, Is.LessThan(discardScreenPosition.y));
            Assert.That(completionCount, Is.Zero);

            runner.CompleteImmediately();

            FieldInfo transientCardsField = typeof(HandCardContainer).GetField(
                "_transientCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var transientCards = transientCardsField?.GetValue(container)
                as Dictionary<CardInstanceId, HandCardVisual>;
            Assert.That(visual.GetScreenCenter().x, Is.EqualTo(discardScreenPosition.x).Within(0.01f));
            Assert.That(visual.GetScreenCenter().y, Is.EqualTo(discardScreenPosition.y).Within(0.01f));
            Assert.That(transientCards, Is.Empty);
            Assert.That(transientDestroyCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(zones.Hand, Is.Empty);
            Assert.That(zones.DiscardPile, Is.EqualTo(new[] { cardId }));
            Assert.That(unexpectedLayoutPublicationCount, Is.Zero);
        }
        finally
        {
            zones.Dispose();
            if (cardObject != null)
                Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>验证无出牌前奏的 Hand→Exhaust 仍使用已脱离手牌的 transient 飞向消耗锚点，并在 runner 收口时只清理一次。</summary>
    [Test]
    public void HandToExhaust_UsesDetachedTransientAndCleansExactlyOnceAtExhaustAnchor()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject containerObject = new GameObject("HandCardExhaustMotionPlaybackContainer");
        GameObject cardObject = Object.Instantiate(prefab);
        var zones = new BattleCardZonesData(new[] { 3261 }, shuffleSeed: 3579);
        try
        {
            zones.Draw(1);
            CardInstanceId cardId = zones.Hand[0];
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, -320f), 0f, 0));
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            int transientDestroyCount = 0;
            container.ConfigureTransientCardDestroyForTesting(_ => transientDestroyCount++);
            typeof(HandCardContainer).GetField(
                    "_cardZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(container, zones);
            var interactiveCards = typeof(HandCardContainer).GetField(
                    "_cards",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(container) as List<HandCardVisual>;
            Assert.That(interactiveCards, Is.Not.Null);
            interactiveCards.Add(visual);

            zones.ExhaustFromHand(cardId);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(container, new object[] { false });
            Vector2 originalScreenPosition = visual.GetScreenCenter();
            Vector2 exhaustScreenPosition = originalScreenPosition + new Vector2(-320f, 120f);
            var moved = new BattleCardMovedSettlement(
                order: 0,
                cardId,
                BattleCardZone.Hand,
                BattleCardZone.ExhaustPile);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 22,
                BattleCommandType.PlayCard,
                new CombatantId(1001),
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[] { moved });
            BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
            var cardMotionFactory = new BattleCardMotionTweenFactory(cue =>
                container.CreateTransientCardMotionTween(
                    cue,
                    exhaustScreenPosition,
                    duration: 0.2f,
                    ease: Ease.Linear));
            using var runner = new BattleCommandPresentationRunner(
                _ => CreateZeroDurationTween(),
                step => cardMotionFactory.TryCreate(
                    step,
                    out BattleCommandPresentationTween tween)
                        ? tween
                        : CreateZeroDurationTween());
            int completionCount = 0;

            runner.Play(plan, () => completionCount++);
            runner.Tick(0.1f);

            Vector2 midpoint = visual.GetScreenCenter();
            Assert.That(midpoint.x, Is.LessThan(originalScreenPosition.x));
            Assert.That(midpoint.x, Is.GreaterThan(exhaustScreenPosition.x));
            Assert.That(midpoint.y, Is.GreaterThan(originalScreenPosition.y));
            Assert.That(midpoint.y, Is.LessThan(exhaustScreenPosition.y));
            Assert.That(completionCount, Is.Zero);

            runner.CompleteImmediately();

            FieldInfo transientCardsField = typeof(HandCardContainer).GetField(
                "_transientCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var transientCards = transientCardsField?.GetValue(container)
                as Dictionary<CardInstanceId, HandCardVisual>;
            Assert.That(visual.GetScreenCenter().x, Is.EqualTo(exhaustScreenPosition.x).Within(0.01f));
            Assert.That(visual.GetScreenCenter().y, Is.EqualTo(exhaustScreenPosition.y).Within(0.01f));
            Assert.That(transientCards, Is.Empty);
            Assert.That(transientDestroyCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(zones.Hand, Is.Empty);
            Assert.That(zones.ExhaustPile, Is.EqualTo(new[] { cardId }));
        }
        finally
        {
            zones.Dispose();
            if (cardObject != null)
                Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>验证后续 settlement cue 同步构造失败时，无位移的出牌前奏仍释放离手 transient。</summary>
    [Test]
    public void PlayCardPreludeHold_LaterCueBuildThrows_ReleasesDetachedTransient()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject containerObject = new GameObject("HandCardMotionBuildFaultContainer");
        GameObject cardObject = Object.Instantiate(prefab);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 8643);
        try
        {
            zones.Draw(1);
            CardInstanceId cardId = zones.Hand[0];
            var playerId = new CombatantId(1001);
            var enemyId = new CombatantId(2001);
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, -320f), 0f, 0));
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            int transientDestroyCount = 0;
            container.ConfigureTransientCardDestroyForTesting(_ => transientDestroyCount++);
            typeof(HandCardContainer).GetField(
                    "_cardZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(container, zones);
            var interactiveCards = typeof(HandCardContainer).GetField(
                    "_cards",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(container) as List<HandCardVisual>;
            Assert.That(interactiveCards, Is.Not.Null);
            interactiveCards.Add(visual);
            zones.DiscardFromHand(cardId);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(container, new object[] { false });
            var damage = new BattleDamageAppliedSettlement(
                order: 0,
                new BattleEffectId(4001),
                playerId,
                enemyId,
                attackValue: 6,
                blockBefore: 0,
                blockAfter: 0,
                healthBefore: 20,
                healthAfter: 14);
            var moved = new BattleCardMovedSettlement(
                order: 1,
                cardId,
                BattleCardZone.Hand,
                BattleCardZone.DiscardPile);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 2,
                BattleCommandType.PlayCard,
                playerId,
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[] { damage, moved });
            BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
            var cardMotionFactory = new BattleCardMotionTweenFactory(
                container.CreateTransientCardHoldTween);
            using var runner = new BattleCommandPresentationRunner(
                prelude => cardMotionFactory.TryCreate(
                    prelude,
                    out BattleCommandPresentationTween tween)
                        ? tween
                        : CreateZeroDurationTween(),
                _ => throw new InvalidOperationException("later-cue-build-fault"));
            int completionCount = 0;
            int activeTweenCountBefore = DOTween.TotalActiveTweens();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => runner.Play(plan, () => completionCount++));

            FieldInfo transientCardsField = typeof(HandCardContainer).GetField(
                "_transientCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var transientCards = transientCardsField?.GetValue(container)
                as Dictionary<CardInstanceId, HandCardVisual>;
            Assert.That(exception.Message, Is.EqualTo("later-cue-build-fault"));
            Assert.That(transientCards, Is.Empty);
            Assert.That(transientDestroyCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.Zero);
            Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
        }
        finally
        {
            zones.Dispose();
            if (cardObject != null)
                Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>验证 Draw→Hand 只移动当前权威手牌 View，并从抽牌锚点回到当前 base pose。</summary>
    [Test]
    public void DrawToHand_UsesAuthoritativeHandViewAndRestoresCurrentBasePose()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject containerObject = new GameObject("HandCardArrivalContainer");
        GameObject cardObject = Object.Instantiate(prefab);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 9753);
        var parentTweenId = new object();
        try
        {
            zones.Draw(1);
            CardInstanceId cardId = zones.Hand[0];
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(80f, -300f), 7f, 0));
            Vector2 baseScreenPosition = visual.GetScreenCenter();
            Vector2 drawScreenPosition = baseScreenPosition + new Vector2(-320f, -120f);
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            typeof(HandCardContainer).GetField(
                    "_cardZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(container, zones);
            var interactiveCards = typeof(HandCardContainer).GetField(
                    "_cards",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(container) as List<HandCardVisual>;
            Assert.That(interactiveCards, Is.Not.Null);
            interactiveCards.Add(visual);
            var cue = new BattleCardMotionCue(
                BattleCardMotionCueKind.DrawToHand,
                cardId,
                settlementOrder: 5);
            int fastForwardRequestCount = 0;
            int unexpectedLayoutPublicationCount = 0;
            using IDisposable subscription = zones.Layout
                .Skip(1)
                .Subscribe(_ => unexpectedLayoutPublicationCount++);

            BattleCommandPresentationTween lease = container.CreateIncomingCardMotionTween(
                cue,
                drawScreenPosition,
                duration: 0.2f,
                ease: Ease.Linear,
                requestFastForward: () => fastForwardRequestCount++);
            Assert.That(visual.GetScreenCenter().x, Is.EqualTo(baseScreenPosition.x).Within(0.01f));
            Assert.That(visual.GetScreenCenter().y, Is.EqualTo(baseScreenPosition.y).Within(0.01f));
            Sequence parent = DOTween.Sequence()
                .SetId(parentTweenId)
                .SetUpdate(UpdateType.Manual)
                .Pause()
                .Append(lease.Tween);
            parent.Play();
            parent.ManualUpdate(0.1f, 0.1f);

            Vector2 midpoint = visual.GetScreenCenter();
            Assert.That(midpoint, Is.Not.EqualTo(baseScreenPosition));
            Assert.That(canvasGroup.interactable, Is.True);
            Assert.That(canvasGroup.blocksRaycasts, Is.True);
            Assert.That(fastForwardRequestCount, Is.Zero);

            parent.Complete(withCallbacks: true);
            lease.Cleanup();

            Assert.That(visual.GetScreenCenter().x, Is.EqualTo(baseScreenPosition.x).Within(0.01f));
            Assert.That(visual.GetScreenCenter().y, Is.EqualTo(baseScreenPosition.y).Within(0.01f));
            Assert.That(interactiveCards, Is.EqualTo(new[] { visual }));
            Assert.That(zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(zones.DrawPile, Is.Empty);
            Assert.That(unexpectedLayoutPublicationCount, Is.Zero);
        }
        finally
        {
            DOTween.Kill(parentTweenId, complete: false);
            zones.Dispose();
            if (cardObject != null)
                Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>验证 Draw→Hand 播放中取消会恢复最新 base pose、关闭快进入口并丢弃 completion。</summary>
    [Test]
    public void DrawToHand_RunnerDisposeMidFlight_RestoresLatestBasePoseWithoutLateCompletion()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject containerObject = new GameObject("HandCardArrivalCancelContainer");
        GameObject cardObject = Object.Instantiate(prefab);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 8642);
        BattleCommandPresentationRunner runner = null;
        try
        {
            zones.Draw(1);
            CardInstanceId cardId = zones.Hand[0];
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(80f, -300f), 7f, 0));
            Vector2 drawScreenPosition = visual.GetScreenCenter() + new Vector2(-320f, -120f);
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            typeof(HandCardContainer).GetField(
                    "_cardZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(container, zones);
            var interactiveCards = typeof(HandCardContainer).GetField(
                    "_cards",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(container) as List<HandCardVisual>;
            Assert.That(interactiveCards, Is.Not.Null);
            interactiveCards.Add(visual);
            var cue = new BattleCardMotionCue(
                BattleCardMotionCueKind.DrawToHand,
                cardId,
                settlementOrder: 0);
            int activeTweenCountBefore = DOTween.TotalActiveTweens();
            BattleCommandPresentationTween lease = container.CreateIncomingCardMotionTween(
                cue,
                drawScreenPosition,
                duration: 0.4f,
                Ease.Linear,
                requestFastForward: () => { });
            var result = new BattleCommandExecutionResult(
                authoritySequence: 1,
                BattleCommandType.EndPlayerAction,
                new CombatantId(1001),
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleCardMovedSettlement(
                        order: 0,
                        cardId,
                        BattleCardZone.DrawPile,
                        BattleCardZone.Hand),
                });
            int completionCount = 0;
            runner = new BattleCommandPresentationRunner(
                _ => CreateZeroDurationTween(),
                _ => lease);

            runner.Play(BattleCommandPresentationPlan.Create(result), () => completionCount++);
            runner.Tick(0.1f);

            Assert.That(visual.IsIncomingCardMotionActive, Is.True);
            var latestPose = new HandCardPose(new Vector2(145f, -265f), -4f, 2);
            visual.SetBasePose(latestPose);

            runner.Dispose();
            runner.Dispose();
            runner.Tick(1f);
            runner.CompleteImmediately();

            Assert.That(
                visual.CardContent.anchoredPosition.x,
                Is.EqualTo(latestPose.AnchoredPosition.x).Within(0.01f));
            Assert.That(
                visual.CardContent.anchoredPosition.y,
                Is.EqualTo(latestPose.AnchoredPosition.y).Within(0.01f));
            Assert.That(
                visual.CardContent.localEulerAngles.z,
                Is.EqualTo(356f).Within(0.01f));
            Assert.That(visual.IsIncomingCardMotionActive, Is.False);
            Assert.That(visual.TryFastForwardIncomingCardMotion(), Is.False);
            Assert.That(interactiveCards, Is.EqualTo(new[] { visual }));
            Assert.That(zones.Hand, Is.EqualTo(new[] { cardId }));
            Assert.That(completionCount, Is.Zero);
            Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
        }
        finally
        {
            runner?.Dispose();
            zones.Dispose();
            if (cardObject != null)
                Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>验证离手 ghost 的场景 owner 销毁会清空映射，随后 runner 取消不重复销毁或补发 completion。</summary>
    [Test]
    public void HandToDiscard_OwnerDestroyedMidFlight_CleansGhostWithoutLateCompletion()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject containerObject = new GameObject("HandCardTransientOwnerCancelContainer");
        GameObject cardObject = Object.Instantiate(prefab);
        var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 6428);
        BattleCommandPresentationRunner runner = null;
        try
        {
            zones.Draw(1);
            CardInstanceId cardId = zones.Hand[0];
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(40f, -300f), 0f, 0));
            Vector2 discardScreenPosition = visual.GetScreenCenter() + new Vector2(360f, 80f);
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            int destroyCount = 0;
            container.ConfigureTransientCardDestroyForTesting(transient =>
            {
                destroyCount++;
                Object.DestroyImmediate(transient);
            });
            typeof(HandCardContainer).GetField(
                    "_cardZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(container, zones);
            var interactiveCards = typeof(HandCardContainer).GetField(
                    "_cards",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(container) as List<HandCardVisual>;
            Assert.That(interactiveCards, Is.Not.Null);
            interactiveCards.Add(visual);
            zones.DiscardFromHand(cardId);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(container, new object[] { false });
            var cue = new BattleCardMotionCue(
                BattleCardMotionCueKind.HandToDiscard,
                cardId,
                settlementOrder: 0);
            int activeTweenCountBefore = DOTween.TotalActiveTweens();
            BattleCommandPresentationTween lease = container.CreateTransientCardMotionTween(
                cue,
                discardScreenPosition,
                duration: 0.4f,
                Ease.Linear);
            var result = new BattleCommandExecutionResult(
                authoritySequence: 1,
                BattleCommandType.EndPlayerAction,
                new CombatantId(1001),
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleCardMovedSettlement(
                        order: 0,
                        cardId,
                        BattleCardZone.Hand,
                        BattleCardZone.DiscardPile),
                });
            int completionCount = 0;
            runner = new BattleCommandPresentationRunner(
                _ => CreateZeroDurationTween(),
                _ => lease);

            runner.Play(BattleCommandPresentationPlan.Create(result), () => completionCount++);
            runner.Tick(0.1f);

            FieldInfo transientCardsField = typeof(HandCardContainer).GetField(
                "_transientCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(transientCardsField, Is.Not.Null);
            var transientCards = transientCardsField.GetValue(container)
                as Dictionary<CardInstanceId, HandCardVisual>;
            Assert.That(transientCards, Does.ContainKey(cardId));
            Assert.That(completionCount, Is.Zero);

            Object.DestroyImmediate(containerObject);
            containerObject = null;
            runner.Dispose();
            runner.Tick(1f);
            runner.CompleteImmediately();

            Assert.That(transientCards, Is.Empty);
            Assert.That(destroyCount, Is.EqualTo(1));
            Assert.That(completionCount, Is.Zero);
            Assert.That(zones.Hand, Is.Empty);
            Assert.That(zones.DiscardPile, Is.EqualTo(new[] { cardId }));
            Assert.That(DOTween.TotalActiveTweens(), Is.EqualTo(activeTweenCountBefore));
        }
        finally
        {
            runner?.Dispose();
            zones.Dispose();
            if (cardObject != null)
                Object.DestroyImmediate(cardObject);
            if (containerObject != null)
                Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>验证 StartBattle 覆盖层播放期间，Layout 订阅只准备权威 Hand View，不得提前显示或补间 opening draw。</summary>
    [Test]
    public void StartBattle_OverlayBeforeOpeningDraw_DoesNotExposeHandBeforeCardMovedCue()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject containerObject = new GameObject("OpeningDrawMotionContainer");
        GameObject cardObject = Object.Instantiate(prefab);
        using var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 5012);
        try
        {
            CardInstanceId cardId = zones.DrawPile[0];
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(80f, -300f), 7f, 0));
            Vector2 drawScreenPosition = visual.GetScreenCenter() + new Vector2(-320f, -120f);
            Canvas cardCanvas = cardObject.GetComponent<Canvas>();
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            typeof(HandCardContainer).GetField(
                    "_cardZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(container, zones);
            var interactiveCards = typeof(HandCardContainer).GetField(
                    "_cards",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(container) as List<HandCardVisual>;
            Assert.That(interactiveCards, Is.Not.Null);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(container, new object[] { true });
            interactiveCards.Add(visual);
            var layoutRebuildCount = 0;
            using IDisposable layoutSubscription = zones.Layout
                .Select(layout => layout.Hand)
                .Skip(1)
                .Subscribe(_ =>
                {
                    layoutRebuildCount++;
                    rebuildCards.Invoke(container, new object[] { false });
                });

            zones.Draw(1);

            Assert.That(layoutRebuildCount, Is.EqualTo(1));
            Assert.That(visual.IsIncomingCardMotionPending, Is.True);
            Assert.That(
                cardCanvas.enabled,
                Is.False,
                "StartBattle 覆盖层完成前，Layout 不得让 opening Hand View 可见。");

            var result = new BattleCommandExecutionResult(
                authoritySequence: 1,
                BattleCommandType.StartBattle,
                submitterId: null,
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleCardMovedSettlement(
                        order: 0,
                        cardId,
                        BattleCardZone.DrawPile,
                        BattleCardZone.Hand),
                });
            BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
            var cardMotionFactory = new BattleCardMotionTweenFactory(cue =>
                container.CreateIncomingCardMotionTween(
                    cue,
                    drawScreenPosition,
                    duration: 0.2f,
                    ease: Ease.Linear,
                    requestFastForward: () => { }));
            using var runner = new BattleCommandPresentationRunner(
                _ => CreateDurationTween(0.2f),
                step => cardMotionFactory.TryCreate(step, out BattleCommandPresentationTween tween)
                    ? tween
                    : CreateZeroDurationTween());

            runner.Play(plan, () => { });
            runner.Tick(0.1f);

            Assert.That(cardCanvas.enabled, Is.False);
            Assert.That(visual.IsIncomingCardMotionActive, Is.False);

            runner.Tick(0.11f);

            Assert.That(cardCanvas.enabled, Is.True);
            Assert.That(visual.IsIncomingCardMotionActive, Is.True);
        }
        finally
        {
            if (cardObject != null)
                Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>验证敌人伤害的数字与抖动均结束前，Layout 不得让下一轮 Draw→Hand 抢跑。</summary>
    [Test]
    public void EnemyAttackBeforeRoundDraw_DoesNotStartHandMotionUntilDamageFeedbackCompletes()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
        GameObject containerObject = new GameObject("EnemyDamageRoundDrawMotionContainer");
        GameObject cardObject = Object.Instantiate(prefab);
        using var zones = new BattleCardZonesData(new[] { 3001 }, shuffleSeed: 5013);
        try
        {
            CardInstanceId cardId = zones.DrawPile[0];
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            CanvasGroup canvasGroup = visual.CardContent.gameObject.AddComponent<CanvasGroup>();
            visual.Initialize(Vector3.one * 0.36f, cardId, canvasGroup);
            visual.SetBasePoseImmediately(new HandCardPose(new Vector2(80f, -300f), 7f, 0));
            Vector2 drawScreenPosition = visual.GetScreenCenter() + new Vector2(-320f, -120f);
            Canvas cardCanvas = cardObject.GetComponent<Canvas>();
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            typeof(HandCardContainer).GetField(
                    "_cardZones",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(container, zones);
            var interactiveCards = typeof(HandCardContainer).GetField(
                    "_cards",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(container) as List<HandCardVisual>;
            Assert.That(interactiveCards, Is.Not.Null);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rebuildCards, Is.Not.Null);
            rebuildCards.Invoke(container, new object[] { true });
            interactiveCards.Add(visual);
            var layoutRebuildCount = 0;
            using IDisposable layoutSubscription = zones.Layout
                .Select(layout => layout.Hand)
                .Skip(1)
                .Subscribe(_ =>
                {
                    layoutRebuildCount++;
                    rebuildCards.Invoke(container, new object[] { false });
                });

            zones.Draw(1);

            Assert.That(layoutRebuildCount, Is.EqualTo(1));
            Assert.That(visual.IsIncomingCardMotionPending, Is.True);
            Assert.That(cardCanvas.enabled, Is.False);

            var result = new BattleCommandExecutionResult(
                authoritySequence: 2,
                BattleCommandType.CompleteEnemyAction,
                submitterId: null,
                BattleCommandExecutionFailureReason.None,
                new BattleSettlementRecord[]
                {
                    new BattleDamageAppliedSettlement(
                        order: 0,
                        new BattleEffectId(4001),
                        new CombatantId(2001),
                        new CombatantId(1001),
                        attackValue: 6,
                        blockBefore: 0,
                        blockAfter: 0,
                        healthBefore: 20,
                        healthAfter: 14),
                    new BattleCardMovedSettlement(
                        order: 1,
                        cardId,
                        BattleCardZone.DrawPile,
                        BattleCardZone.Hand),
                });
            BattleCommandPresentationPlan plan = BattleCommandPresentationPlan.Create(result);
            var cardMotionFactory = new BattleCardMotionTweenFactory(cue =>
                container.CreateIncomingCardMotionTween(
                    cue,
                    drawScreenPosition,
                    duration: 0.2f,
                    ease: Ease.Linear,
                    requestFastForward: () => { }));
            using var runner = new BattleCommandPresentationRunner(
                _ => CreateZeroDurationTween(),
                step => cardMotionFactory.TryCreate(step, out BattleCommandPresentationTween tween)
                    ? tween
                    : CreateDurationTween(0.2f));

            runner.Play(plan, () => { });
            runner.Tick(0.25f);

            Assert.That(cardCanvas.enabled, Is.False);
            Assert.That(visual.IsIncomingCardMotionActive, Is.False);

            runner.Tick(0.25f);

            Assert.That(cardCanvas.enabled, Is.True);
            Assert.That(visual.IsIncomingCardMotionActive, Is.True);
        }
        finally
        {
            if (cardObject != null)
                Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>创建由 runner 手动推进的固定时长测试 cue。</summary>
    private static BattleCommandPresentationTween CreateDurationTween(float duration)
    {
        return new BattleCommandPresentationTween(
            DOTween.Sequence().AppendInterval(duration),
            cleanup: null);
    }

    /// <summary>创建由 runner 统一拥有的零时长非卡牌测试 cue。</summary>
    private static BattleCommandPresentationTween CreateZeroDurationTween()
    {
        return new BattleCommandPresentationTween(
            DOTween.Sequence().AppendCallback(() => { }),
            cleanup: null);
    }
}
