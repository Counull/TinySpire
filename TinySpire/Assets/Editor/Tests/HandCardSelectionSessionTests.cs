using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DG.Tweening;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using R3;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>验证手牌额外选择会话只冻结 UI 交互事实，不复制或写入权威战斗状态。</summary>
public sealed class HandCardSelectionSessionTests
{
    /// <summary>验证通用配置卡从规则请求经 UI 选牌会话回传 SelectedCardIds，并由唯一 Queue seam 完成结算。</summary>
    [Test]
    public void BurningPact_RulesSelectionSessionAndQueue_UseGenericSelectedCardProtocol()
    {
        /// <summary>从正式生成 JSON 读取 Luban 表行，避免测试夹具重写 3125 契约。</summary>
        JArray LoadRows(string tableName)
        {
            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                $"Assets/GameData/{tableName}.json");
            Assert.That(asset, Is.Not.Null, $"Missing generated table {tableName}.");
            JObject rows = JObject.Parse(asset.text);
            return new JArray(rows.Properties().Select(property => property.Value));
        }

        cfg.Tables tables = new cfg.Tables(LoadRows);
        using var combatants = new BattleCombatantsData();
        PlayerCombatantData player = combatants.AddPlayer(1001, 80, 0);
        EnemyCombatantData enemy = combatants.AddEnemy(2001, 20, 0);
        using var zones = new BattleCardZonesData(Enumerable.Repeat(3125, 4), shuffleSeed: 1234);
        using var enemyIntents = new BattleEnemyIntentsData(
            combatants,
            new[] { enemy.Id },
            tables,
            battleSeed: 4321);
        var presentation = new ControllableBattleCommandPresentation();
        using BattleCommandQueue queue = BattleCommandQueueTestFactory.Create(
            combatants,
            presentation,
            new Dictionary<CombatantId, BattleCardZonesData> { [player.Id] = zones },
            enemyCombatantIdsInEncounterOrder: new[] { enemy.Id },
            energyPerRound: 3,
            initialHandCount: 2,
            enemyIntents: enemyIntents,
            tables: tables);
        queue.Submit(new StartBattleCommand());
        presentation.CompleteNext();
        CardInstanceId sourceCardId = zones.Hand[0];
        CardInstanceId selectedCardId = zones.Hand[1];
        CardInstanceId firstDrawnCardId = zones.DrawPile[zones.DrawPile.Count - 1];
        CardInstanceId secondDrawnCardId = zones.DrawPile[zones.DrawPile.Count - 2];
        CardZoneLayoutData initialLayout = zones.Layout.CurrentValue;
        BattleTurnData initialTurn = queue.Turn.CurrentValue;
        BattleCommandQueueData initialQueue = queue.Queue.CurrentValue;

        var emptySelection = new PlayCardCommand(player.Id, sourceCardId, player.Id);
        BattleCardPlayEvaluation evaluation = queue.CardPlayRules.Evaluate(initialTurn, emptySelection);
        BattleHandCardSelectionRequest request = evaluation.HandCardSelectionRequest;

        Assert.That(evaluation.FailureReason,
            Is.EqualTo(BattleCommandExecutionFailureReason.CardSelectionRequired));
        Assert.That(request, Is.Not.Null);
        Assert.That(request.RequiredCount, Is.EqualTo(1));
        Assert.That(request.LegalCardIds, Is.EqualTo(new[] { selectedCardId }));

        HandCardSelectionSession session = HandCardSelectionSession.Begin(
            sourceCardId,
            player.Id,
            request.LegalCardIds,
            initialLayout,
            initialTurn,
            initialQueue);
        Assert.That(session.SourceCardId, Is.EqualTo(sourceCardId));
        Assert.That(session.LegalTargetCardIds, Is.EqualTo(new[] { selectedCardId }));
        Assert.That(session.MatchesSnapshots(initialLayout, initialTurn, initialQueue), Is.True);
        HandCardSelectionClickResolution resolution = session.ResolveClick(selectedCardId);
        Assert.That(resolution.Action, Is.EqualTo(HandCardSelectionClickAction.Confirm));
        Assert.That(resolution.TargetCardId, Is.EqualTo(selectedCardId));

        var command = new PlayCardCommand(
            player.Id,
            sourceCardId,
            player.Id,
            new[] { resolution.TargetCardId.Value });
        Assert.That(command.SelectedCardIds, Is.EqualTo(new[] { selectedCardId }));
        using BattleCommandLifecycleExecutionRecorder recorder = queue.RecordExecutionLifecycle();
        BattleCommandSubmissionResult submission = queue.Submit(command);
        BattleCommandLifecycleEvent terminal = recorder.RequireTerminal(submission);
        BattleCardMovedSettlement[] moves = terminal.Settlements
            .OfType<BattleCardMovedSettlement>()
            .ToArray();

        Assert.That(submission.Accepted, Is.True);
        Assert.That(terminal.FailureReason, Is.EqualTo(BattleCommandExecutionFailureReason.None));
        Assert.That(
            moves.Select(move => (move.CardId, move.FromZone, move.ToZone)),
            Is.EqualTo(new[]
            {
                (selectedCardId, BattleCardZone.Hand, BattleCardZone.ExhaustPile),
                (firstDrawnCardId, BattleCardZone.DrawPile, BattleCardZone.Hand),
                (secondDrawnCardId, BattleCardZone.DrawPile, BattleCardZone.Hand),
                (sourceCardId, BattleCardZone.Hand, BattleCardZone.DiscardPile),
            }));
    }

    /// <summary>冻结开始时的来源、候选与权威引用，并只把合法候选解析为确认动作。</summary>
    [Test]
    public void Begin_FreezesFactsAndResolvesOnlyLegalCard()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 2);
        using var otherScenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId legalCardId = scenario.Zones.Hand.Single(cardId => cardId != sourceCardId);
        var legalCardIds = new[] { legalCardId };
        CardZoneLayoutData initialLayout = scenario.Zones.Layout.CurrentValue;
        BattleTurnData initialTurn = scenario.Queue.Turn.CurrentValue;
        BattleCommandQueueData initialQueue = scenario.Queue.Queue.CurrentValue;

        HandCardSelectionSession session = HandCardSelectionSession.Begin(
            sourceCardId,
            playTargetId: null,
            legalCardIds,
            initialLayout,
            initialTurn,
            initialQueue);
        legalCardIds[0] = sourceCardId;

        Assert.That(session.SourceCardId, Is.EqualTo(sourceCardId));
        Assert.That(session.PlayTargetId, Is.Null);
        Assert.That(session.LegalTargetCardIds, Is.EqualTo(new[] { legalCardId }));
        Assert.That(session.InitialLayout, Is.SameAs(initialLayout));
        Assert.That(session.InitialTurn, Is.SameAs(initialTurn));
        Assert.That(session.InitialQueue, Is.SameAs(initialQueue));
        Assert.That(session.MatchesSnapshots(initialLayout, initialTurn, initialQueue), Is.True);
        Assert.That(
            session.MatchesSnapshots(
                initialLayout,
                initialTurn,
                otherScenario.Queue.Queue.CurrentValue),
            Is.False);

        HandCardSelectionClickResolution sourceResolution = session.ResolveClick(sourceCardId);
        HandCardSelectionClickResolution unknownResolution =
            session.ResolveClick(new CardInstanceId(int.MaxValue));
        HandCardSelectionClickResolution legalResolution = session.ResolveClick(legalCardId);

        Assert.That(sourceResolution.Action, Is.EqualTo(HandCardSelectionClickAction.Cancel));
        Assert.That(sourceResolution.TargetCardId, Is.Null);
        Assert.That(unknownResolution.Action, Is.EqualTo(HandCardSelectionClickAction.Ignore));
        Assert.That(unknownResolution.TargetCardId, Is.Null);
        Assert.That(legalResolution.Action, Is.EqualTo(HandCardSelectionClickAction.Confirm));
        Assert.That(legalResolution.TargetCardId, Is.EqualTo(legalCardId));
        Assert.That(
            () => ((IList<CardInstanceId>)session.LegalTargetCardIds)[0] = sourceCardId,
            Throws.TypeOf<NotSupportedException>());
    }

    /// <summary>精确双选会话会在第一张后继续等待，并按点击顺序冻结两张确认牌。</summary>
    [Test]
    public void ResolveClick_RequiredTwoWaitsThenConfirmsBothCardsInOrder()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3244, 3201, 3202 },
            initialHandCount: 3,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId firstTargetId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3201);
        CardInstanceId secondTargetId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3202);
        HandCardSelectionSession session = HandCardSelectionSession.Begin(
            sourceCardId,
            scenario.Player.Id,
            new[] { firstTargetId, secondTargetId },
            2,
            scenario.Zones.Layout.CurrentValue,
            scenario.Queue.Turn.CurrentValue,
            scenario.Queue.Queue.CurrentValue);

        HandCardSelectionClickResolution first = session.ResolveClick(firstTargetId);
        HandCardSelectionClickResolution second = session.ResolveClick(secondTargetId);

        Assert.That(first.Action, Is.EqualTo(HandCardSelectionClickAction.Continue));
        Assert.That(first.SelectedCardIds, Is.EqualTo(new[] { firstTargetId }));
        Assert.That(second.Action, Is.EqualTo(HandCardSelectionClickAction.Confirm));
        Assert.That(second.TargetCardId, Is.Null);
        Assert.That(second.SelectedCardIds, Is.EqualTo(new[] { firstTargetId, secondTargetId }));
        Assert.That(session.SelectedCardIds, Is.EqualTo(second.SelectedCardIds));
    }

    /// <summary>越线释放需要额外选牌的 Self 牌时只开始选择会话，不登记或提交权威命令。</summary>
    [Test]
    public void ContainerRelease_VentHeatBeginsSelectionWithoutRegisteringCommand()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId legalCardId = scenario.Zones.Hand.Single(cardId => cardId != sourceCardId);
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject sourceObject = Object.Instantiate(cardPrefab);
        GameObject legalObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("VentHeatSelectionPresenter");
        var presentationObjects = new List<GameObject>();
        HandCardVisual sourceVisual = null;
        HandCardVisual legalVisual = null;
        try
        {
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            typeof(BattleParticipantPresenter).GetField(
                "_session",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(presenter, scenario.Session);
            foreach (KeyValuePair<CombatantId, CombatantData> entry in scenario.Session.Combatants.All)
            {
                var world = new GameObject($"VentHeatWorld_{entry.Key.Value}");
                var hudObject = new GameObject($"VentHeatHud_{entry.Key.Value}");
                presentationObjects.Add(world);
                presentationObjects.Add(hudObject);
                presenter.RegisterParticipantView(entry.Key, world);
                presenter.RegisterParticipantHud(entry.Key, hudObject.AddComponent<ParticipantHudView>());
            }

            HandCardContainer container = handObject.GetComponent<HandCardContainer>();
            sourceVisual = sourceObject.GetComponent<HandCardVisual>();
            legalVisual = legalObject.GetComponent<HandCardVisual>();
            sourceVisual.Initialize(
                Vector3.one * 0.36f,
                sourceCardId,
                sourceVisual.CardContent.gameObject.AddComponent<CanvasGroup>());
            legalVisual.Initialize(
                Vector3.one * 0.36f,
                legalCardId,
                legalVisual.CardContent.gameObject.AddComponent<CanvasGroup>());
            sourceVisual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, 0f), 0f, 1));
            legalVisual.SetBasePoseImmediately(new HandCardPose(new Vector2(260f, -380f), 0f, 0));
            typeof(HandCardContainer).GetField(
                "_player",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Player);
            typeof(HandCardContainer).GetField(
                "_cardZones",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Zones);
            typeof(HandCardContainer).GetField(
                "_commandQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Queue);
            typeof(HandCardContainer).GetField(
                "_participantPresenter",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
            typeof(HandCardContainer).GetField(
                "_cardPlayRules",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                container,
                scenario.CardPlayRules);
            typeof(HandCardContainer).GetField(
                "playLineY",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, -10000f);
            var cards = typeof(HandCardContainer).GetField(
                "_cards",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container)
                as List<HandCardVisual>;
            Assert.That(cards, Is.Not.Null);
            cards.Add(sourceVisual);
            cards.Add(legalVisual);

            CardZoneLayoutData layoutBefore = scenario.Zones.Layout.CurrentValue;
            BattleTurnData turnBefore = scenario.Queue.Turn.CurrentValue;
            BattleCommandQueueData queueBefore = scenario.Queue.Queue.CurrentValue;
            int resultCountBefore = scenario.Results.Count;
            using BattleCommandLifecycleExecutionRecorder lifecycle =
                scenario.Queue.RecordExecutionLifecycle();

            container.HandleBeginDrag(sourceVisual);
            container.HandleEndDrag(sourceVisual, new PointerEventData(eventSystem: null));

            HandCardSelectionSession selection = container.ActiveHandCardSelection;
            Assert.That(selection, Is.Not.Null);
            Assert.That(selection.SourceCardId, Is.EqualTo(sourceCardId));
            Assert.That(selection.PlayTargetId, Is.EqualTo(scenario.Player.Id));
            Assert.That(selection.LegalTargetCardIds, Is.EqualTo(new[] { legalCardId }));
            Assert.That(
                typeof(HandCardContainer).GetField(
                    "_draggingCard",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container),
                Is.Null);
            Assert.That(
                typeof(HandCardContainer).GetField(
                    "_dragPhase",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container),
                Is.EqualTo(HandCardDragPhase.Idle));
            Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore));
            Assert.That(lifecycle.Events, Is.Empty);
            Assert.That(scenario.Queue.Queue.CurrentValue, Is.SameAs(queueBefore));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBefore));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(scenario.Zones.Hand, Is.EqualTo(new[] { sourceCardId, legalCardId }));
            Assert.That(sourceVisual.IsCommandPending, Is.False);
            Assert.That(legalVisual.IsCommandPending, Is.False);
        }
        finally
        {
            sourceVisual?.CancelTargetFocus();
            legalVisual?.CancelTargetFocus();
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(legalObject);
            Object.DestroyImmediate(handObject);
            Object.DestroyImmediate(presenterObject);
            foreach (GameObject presentationObject in presentationObjects)
                Object.DestroyImmediate(presentationObject);
            DOTween.KillAll(complete: false);
        }
    }

    /// <summary>点击合法候选时关闭选择会话，并以冻结选择经唯一 Queue seam 完成散热结算。</summary>
    [Test]
    public void ContainerClick_LegalVentHeatCandidateConfirmsAndSubmitsSelectedCard()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId legalCardId = scenario.Zones.Hand.Single(cardId => cardId != sourceCardId);
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject sourceObject = Object.Instantiate(cardPrefab);
        GameObject legalObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("VentHeatSelectionConfirmPresenter");
        var presentationObjects = new List<GameObject>();
        HandCardVisual sourceVisual = null;
        HandCardVisual legalVisual = null;
        IDisposable layoutSubscription = null;
        IDisposable lifecycleSubscription = null;
        try
        {
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            typeof(BattleParticipantPresenter).GetField(
                "_session",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(presenter, scenario.Session);
            foreach (KeyValuePair<CombatantId, CombatantData> entry in scenario.Session.Combatants.All)
            {
                var world = new GameObject($"VentHeatConfirmWorld_{entry.Key.Value}");
                var hudObject = new GameObject($"VentHeatConfirmHud_{entry.Key.Value}");
                presentationObjects.Add(world);
                presentationObjects.Add(hudObject);
                presenter.RegisterParticipantView(entry.Key, world);
                presenter.RegisterParticipantHud(entry.Key, hudObject.AddComponent<ParticipantHudView>());
            }

            HandCardContainer container = handObject.GetComponent<HandCardContainer>();
            sourceVisual = sourceObject.GetComponent<HandCardVisual>();
            legalVisual = legalObject.GetComponent<HandCardVisual>();
            sourceVisual.Initialize(
                Vector3.one * 0.36f,
                sourceCardId,
                sourceVisual.CardContent.gameObject.AddComponent<CanvasGroup>());
            legalVisual.Initialize(
                Vector3.one * 0.36f,
                legalCardId,
                legalVisual.CardContent.gameObject.AddComponent<CanvasGroup>());
            sourceVisual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, 0f), 0f, 1));
            legalVisual.SetBasePoseImmediately(new HandCardPose(new Vector2(260f, -380f), 0f, 0));
            typeof(HandCardContainer).GetField(
                "_player",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Player);
            typeof(HandCardContainer).GetField(
                "_cardZones",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Zones);
            typeof(HandCardContainer).GetField(
                "_commandQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Queue);
            typeof(HandCardContainer).GetField(
                "_participantPresenter",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
            typeof(HandCardContainer).GetField(
                "_cardPlayRules",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                container,
                scenario.CardPlayRules);
            typeof(HandCardContainer).GetField(
                "playLineY",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, -10000f);
            var cards = typeof(HandCardContainer).GetField(
                "_cards",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container)
                as List<HandCardVisual>;
            Assert.That(cards, Is.Not.Null);
            cards.Add(sourceVisual);
            cards.Add(legalVisual);
            MethodInfo handleLifecycle = typeof(HandCardContainer).GetMethod(
                "HandleCommandLifecycle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo rebuildCards = typeof(HandCardContainer).GetMethod(
                "RebuildCards",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handleLifecycle, Is.Not.Null);
            Assert.That(rebuildCards, Is.Not.Null);
            layoutSubscription = scenario.Zones.Layout
                .Skip(1)
                .Subscribe(_ => rebuildCards.Invoke(container, new object[] { false }));
            lifecycleSubscription = scenario.Queue.Lifecycle.Subscribe(lifecycle =>
                handleLifecycle.Invoke(container, new object[] { lifecycle }));

            container.HandleBeginDrag(sourceVisual);
            container.HandleEndDrag(sourceVisual, new PointerEventData(eventSystem: null));
            Assert.That(container.ActiveHandCardSelection, Is.Not.Null);
            int resultCountBefore = scenario.Results.Count;
            int energyBefore = scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy;
            var clickEvent = new PointerEventData(eventSystem: null)
            {
                button = PointerEventData.InputButton.Left,
            };

            container.HandlePointerClick(legalVisual, clickEvent);

            Assert.That(container.ActiveHandCardSelection, Is.Null);
            Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBefore + 1));
            BattleCommandExecutionResult result = scenario.Results[resultCountBefore];
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Settlements, Has.Count.EqualTo(4));
            Assert.That(
                result.Settlements.Select(settlement => settlement.Order),
                Is.EqualTo(Enumerable.Range(0, 4)));
            BattleEnergySpentSettlement spent = result.Settlements[0]
                as BattleEnergySpentSettlement;
            BattleCardMovedSettlement selectedMove = result.Settlements[1]
                as BattleCardMovedSettlement;
            BattleEnergyGainedSettlement gained = result.Settlements[2]
                as BattleEnergyGainedSettlement;
            BattleCardMovedSettlement sourceMove = result.Settlements[3]
                as BattleCardMovedSettlement;
            Assert.That(spent, Is.Not.Null);
            Assert.That(spent.Amount, Is.Zero);
            Assert.That(selectedMove, Is.Not.Null);
            Assert.That(selectedMove.CardId, Is.EqualTo(legalCardId));
            Assert.That(selectedMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(selectedMove.ToZone, Is.EqualTo(BattleCardZone.ExhaustPile));
            Assert.That(gained, Is.Not.Null);
            Assert.That(gained.Amount, Is.EqualTo(1));
            Assert.That(gained.EnergyBefore, Is.EqualTo(energyBefore));
            Assert.That(gained.EnergyAfter, Is.EqualTo(energyBefore + 1));
            Assert.That(sourceMove, Is.Not.Null);
            Assert.That(sourceMove.CardId, Is.EqualTo(sourceCardId));
            Assert.That(sourceMove.FromZone, Is.EqualTo(BattleCardZone.Hand));
            Assert.That(sourceMove.ToZone, Is.EqualTo(BattleCardZone.DiscardPile));
            Assert.That(
                scenario.Queue.Turn.CurrentValue.Players[scenario.Player.Id].Energy,
                Is.EqualTo(energyBefore + 1));
            Assert.That(scenario.Zones.Hand, Is.Empty);
            Assert.That(scenario.Zones.ExhaustPile, Is.EqualTo(new[] { legalCardId }));
            Assert.That(scenario.Zones.DiscardPile, Is.EqualTo(new[] { sourceCardId }));
            Assert.That(scenario.Queue.Queue.CurrentValue.PendingCount, Is.Zero);
            Assert.That(sourceVisual.IsCommandPending, Is.False);
            Assert.That(legalVisual.IsCommandPending, Is.False);
        }
        finally
        {
            layoutSubscription?.Dispose();
            lifecycleSubscription?.Dispose();
            sourceVisual?.CancelTargetFocus();
            legalVisual?.CancelTargetFocus();
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(legalObject);
            Object.DestroyImmediate(handObject);
            Object.DestroyImmediate(presenterObject);
            foreach (GameObject presentationObject in presentationObjects)
                Object.DestroyImmediate(presentationObject);
            DOTween.KillAll(complete: false);
        }
    }

    /// <summary>卡牌交互组件实现 Unity 点击接口，并把原卡牌与同一事件对象交给容器解析。</summary>
    [Test]
    public void InteractionPointerClick_ForwardsSameCardAndEventToContainer()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId legalCardId = scenario.Zones.Hand.Single(cardId => cardId != sourceCardId);
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
        GameObject cardObject = Object.Instantiate(cardPrefab);
        var containerObject = new GameObject("HandCardInteractionClickContainer");
        try
        {
            HandCardVisual visual = cardObject.GetComponent<HandCardVisual>();
            visual.Initialize(
                Vector3.one * 0.36f,
                sourceCardId,
                visual.CardContent.gameObject.AddComponent<CanvasGroup>());
            HandCardContainer container = containerObject.AddComponent<HandCardContainer>();
            typeof(HandCardContainer).GetField(
                "_cardZones",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Zones);
            typeof(HandCardContainer).GetField(
                "_commandQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Queue);
            HandCardSelectionSession selection = HandCardSelectionSession.Begin(
                sourceCardId,
                scenario.Player.Id,
                new[] { legalCardId },
                scenario.Zones.Layout.CurrentValue,
                scenario.Queue.Turn.CurrentValue,
                scenario.Queue.Queue.CurrentValue);
            typeof(HandCardContainer).GetField(
                "_activeHandCardSelection",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, selection);
            HandCardInteraction interaction = cardObject.AddComponent<HandCardInteraction>();
            interaction.Initialize(container, visual);
            IPointerClickHandler clickHandler = interaction;
            var eventData = new PointerEventData(eventSystem: null)
            {
                button = PointerEventData.InputButton.Right,
            };

            clickHandler.OnPointerClick(eventData);

            Assert.That(container.ActiveHandCardSelection, Is.Null);
            typeof(HandCardContainer).GetField(
                "_activeHandCardSelection",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, selection);
            eventData.button = PointerEventData.InputButton.Left;

            clickHandler.OnPointerClick(eventData);

            Assert.That(container.ActiveHandCardSelection, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(cardObject);
            Object.DestroyImmediate(containerObject);
        }
    }

    /// <summary>验证活动选牌态投影候选角色、阻止再次拖拽，并允许右键零写取消后恢复普通角色。</summary>
    [Test]
    public void ContainerSelectionPresentation_RightClickCancelsWithoutWritesAndBlocksDrag()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId legalCardId = scenario.Zones.Hand.Single(cardId => cardId != sourceCardId);
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject sourceObject = Object.Instantiate(cardPrefab);
        GameObject legalObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("VentHeatSelectionPresentationPresenter");
        var presentationObjects = new List<GameObject>();
        HandCardVisual sourceVisual = null;
        HandCardVisual legalVisual = null;
        try
        {
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            typeof(BattleParticipantPresenter).GetField(
                "_session",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(presenter, scenario.Session);
            foreach (KeyValuePair<CombatantId, CombatantData> entry in scenario.Session.Combatants.All)
            {
                var world = new GameObject($"VentHeatSelectionPresentationWorld_{entry.Key.Value}");
                var hudObject = new GameObject($"VentHeatSelectionPresentationHud_{entry.Key.Value}");
                presentationObjects.Add(world);
                presentationObjects.Add(hudObject);
                presenter.RegisterParticipantView(entry.Key, world);
                presenter.RegisterParticipantHud(entry.Key, hudObject.AddComponent<ParticipantHudView>());
            }

            HandCardContainer container = handObject.GetComponent<HandCardContainer>();
            sourceVisual = sourceObject.GetComponent<HandCardVisual>();
            legalVisual = legalObject.GetComponent<HandCardVisual>();
            sourceVisual.Initialize(
                Vector3.one * 0.36f,
                sourceCardId,
                sourceVisual.CardContent.gameObject.AddComponent<CanvasGroup>());
            legalVisual.Initialize(
                Vector3.one * 0.36f,
                legalCardId,
                legalVisual.CardContent.gameObject.AddComponent<CanvasGroup>());
            sourceVisual.SetBasePoseImmediately(new HandCardPose(new Vector2(0f, 0f), 0f, 1));
            legalVisual.SetBasePoseImmediately(new HandCardPose(new Vector2(260f, -380f), 0f, 0));
            typeof(HandCardContainer).GetField(
                "_player",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Player);
            typeof(HandCardContainer).GetField(
                "_cardZones",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Zones);
            typeof(HandCardContainer).GetField(
                "_commandQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Queue);
            typeof(HandCardContainer).GetField(
                "_participantPresenter",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
            typeof(HandCardContainer).GetField(
                "_cardPlayRules",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                container,
                scenario.CardPlayRules);
            typeof(HandCardContainer).GetField(
                "playLineY",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, -10000f);
            var cards = typeof(HandCardContainer).GetField(
                "_cards",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container)
                as List<HandCardVisual>;
            Assert.That(cards, Is.Not.Null);
            cards.Add(sourceVisual);
            cards.Add(legalVisual);

            container.HandleBeginDrag(sourceVisual);
            container.HandleEndDrag(sourceVisual, new PointerEventData(eventSystem: null));

            Assert.That(container.ActiveHandCardSelection, Is.Not.Null);
            Assert.That(
                legalVisual.SelectionPresentationRole,
                Is.EqualTo(HandCardSelectionPresentationRole.Candidate));
            Assert.That(
                sourceVisual.SelectionPresentationRole,
                Is.EqualTo(HandCardSelectionPresentationRole.NonCandidate));
            CardZoneLayoutData layoutBeforeCancel = scenario.Zones.Layout.CurrentValue;
            BattleTurnData turnBeforeCancel = scenario.Queue.Turn.CurrentValue;
            BattleCommandQueueData queueBeforeCancel = scenario.Queue.Queue.CurrentValue;
            int resultCountBeforeCancel = scenario.Results.Count;

            container.HandleBeginDrag(legalVisual);

            Assert.That(
                typeof(HandCardContainer).GetField(
                    "_draggingCard",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container),
                Is.Null);
            container.HandlePointerClick(
                legalVisual,
                new PointerEventData(eventSystem: null)
                {
                    button = PointerEventData.InputButton.Right,
                });

            Assert.That(container.ActiveHandCardSelection, Is.Null);
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBeforeCancel));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBeforeCancel));
            Assert.That(scenario.Queue.Queue.CurrentValue, Is.SameAs(queueBeforeCancel));
            Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBeforeCancel));
            Assert.That(
                legalVisual.SelectionPresentationRole,
                Is.EqualTo(HandCardSelectionPresentationRole.None));
            Assert.That(
                sourceVisual.SelectionPresentationRole,
                Is.EqualTo(HandCardSelectionPresentationRole.None));
        }
        finally
        {
            sourceVisual?.CancelTargetFocus();
            legalVisual?.CancelTargetFocus();
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(legalObject);
            Object.DestroyImmediate(handObject);
            Object.DestroyImmediate(presenterObject);
            foreach (GameObject presentationObject in presentationObjects)
                Object.DestroyImmediate(presentationObject);
            DOTween.KillAll(complete: false);
        }
    }

    /// <summary>验证任一冻结快照引用漂移与组件禁用都会零写清理选牌会话及其视觉角色。</summary>
    [Test]
    public void ContainerSelection_CurrentFactDriftOrDisable_CancelsWithoutAuthorityWrites()
    {
        using var scenario = new MachineGunnerStarterRuntimeTests.MachineGunnerStarterScenario(
            new[] { 3244, 3203 },
            initialHandCount: 2,
            enemyDamage: 0,
            initialEnergy: 2);
        scenario.StartBattle();
        CardInstanceId sourceCardId = scenario.Zones.Hand.Single(cardId =>
            scenario.Zones.Cards[cardId].TemplateId == 3244);
        CardInstanceId legalCardId = scenario.Zones.Hand.Single(cardId => cardId != sourceCardId);
        GameObject handPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab");
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Arts/Runtime/Card/Prefab/CardView.prefab");
        GameObject handObject = Object.Instantiate(handPrefab);
        GameObject sourceObject = Object.Instantiate(cardPrefab);
        GameObject legalObject = Object.Instantiate(cardPrefab);
        var presenterObject = new GameObject("VentHeatSelectionDriftPresenter");
        var presentationObjects = new List<GameObject>();
        HandCardVisual sourceVisual = null;
        HandCardVisual legalVisual = null;
        try
        {
            BattleParticipantPresenter presenter =
                presenterObject.AddComponent<BattleParticipantPresenter>();
            typeof(BattleParticipantPresenter).GetField(
                "_session",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(presenter, scenario.Session);
            foreach (KeyValuePair<CombatantId, CombatantData> entry in scenario.Session.Combatants.All)
            {
                var world = new GameObject($"VentHeatSelectionDriftWorld_{entry.Key.Value}");
                var hudObject = new GameObject($"VentHeatSelectionDriftHud_{entry.Key.Value}");
                presentationObjects.Add(world);
                presentationObjects.Add(hudObject);
                presenter.RegisterParticipantView(entry.Key, world);
                presenter.RegisterParticipantHud(entry.Key, hudObject.AddComponent<ParticipantHudView>());
            }

            HandCardContainer container = handObject.GetComponent<HandCardContainer>();
            sourceVisual = sourceObject.GetComponent<HandCardVisual>();
            legalVisual = legalObject.GetComponent<HandCardVisual>();
            sourceVisual.Initialize(
                Vector3.one * 0.36f,
                sourceCardId,
                sourceVisual.CardContent.gameObject.AddComponent<CanvasGroup>());
            legalVisual.Initialize(
                Vector3.one * 0.36f,
                legalCardId,
                legalVisual.CardContent.gameObject.AddComponent<CanvasGroup>());
            typeof(HandCardContainer).GetField(
                "_player",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Player);
            typeof(HandCardContainer).GetField(
                "_cardZones",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Zones);
            typeof(HandCardContainer).GetField(
                "_commandQueue",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, scenario.Queue);
            typeof(HandCardContainer).GetField(
                "_participantPresenter",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(container, presenter);
            typeof(HandCardContainer).GetField(
                "_cardPlayRules",
                BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(
                container,
                scenario.CardPlayRules);
            var cards = typeof(HandCardContainer).GetField(
                "_cards",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(container)
                as List<HandCardVisual>;
            Assert.That(cards, Is.Not.Null);
            cards.Add(sourceVisual);
            cards.Add(legalVisual);
            FieldInfo activeSelectionField = typeof(HandCardContainer).GetField(
                "_activeHandCardSelection",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(activeSelectionField, Is.Not.Null);
            var insufficientColor = new Color(0.95f, 0.2f, 0.2f, 1f);

            void AssertDriftCancels(
                string factName,
                CardZoneLayoutData frozenLayout,
                BattleTurnData frozenTurn,
                BattleCommandQueueData frozenQueue)
            {
                HandCardSelectionSession selection = HandCardSelectionSession.Begin(
                    sourceCardId,
                    scenario.Player.Id,
                    new[] { legalCardId },
                    frozenLayout,
                    frozenTurn,
                    frozenQueue);
                activeSelectionField.SetValue(container, selection);
                sourceVisual.SetInteractionPresentation(
                    HandCardInteractionMode.Disabled,
                    insufficientColor,
                    HandCardSelectionPresentationRole.NonCandidate);
                legalVisual.SetInteractionPresentation(
                    HandCardInteractionMode.Disabled,
                    insufficientColor,
                    HandCardSelectionPresentationRole.Candidate);
                CardZoneLayoutData layoutBeforeRefresh = scenario.Zones.Layout.CurrentValue;
                BattleTurnData turnBeforeRefresh = scenario.Queue.Turn.CurrentValue;
                BattleCommandQueueData queueBeforeRefresh = scenario.Queue.Queue.CurrentValue;
                int resultCountBeforeRefresh = scenario.Results.Count;

                container.RefreshHandCardSelectionFromCurrentFacts(reflow: false);

                Assert.That(
                    container.ActiveHandCardSelection,
                    Is.Null,
                    $"{factName} drift should cancel the active selection.");
                Assert.That(sourceVisual.SelectionPresentationRole,
                    Is.EqualTo(HandCardSelectionPresentationRole.None));
                Assert.That(legalVisual.SelectionPresentationRole,
                    Is.EqualTo(HandCardSelectionPresentationRole.None));
                Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layoutBeforeRefresh));
                Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turnBeforeRefresh));
                Assert.That(scenario.Queue.Queue.CurrentValue, Is.SameAs(queueBeforeRefresh));
                Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBeforeRefresh));
            }

            CardZoneLayoutData layout = scenario.Zones.Layout.CurrentValue;
            BattleTurnData turn = scenario.Queue.Turn.CurrentValue;
            BattleCommandQueueData queue = scenario.Queue.Queue.CurrentValue;
            AssertDriftCancels(
                "Layout",
                new CardZoneLayoutData(
                    layout.DrawPile,
                    layout.Hand,
                    layout.DiscardPile,
                    layout.ExhaustPile,
                    layout.PowerPile),
                turn,
                queue);
            AssertDriftCancels(
                "Turn",
                layout,
                new BattleTurnData(
                    turn.Phase,
                    turn.RoundNumber,
                    turn.Players.ToDictionary(entry => entry.Key, entry => entry.Value),
                    turn.CurrentActingEnemyId),
                queue);
            AssertDriftCancels(
                "Queue",
                layout,
                turn,
                new BattleCommandQueueData(
                    queue.CurrentAuthoritySequence,
                    queue.CurrentCommandType,
                    queue.CurrentSubmitterId,
                    queue.PendingCount,
                    queue.IsWaitingForPresentation,
                    queue.Fault));

            activeSelectionField.SetValue(
                container,
                HandCardSelectionSession.Begin(
                    sourceCardId,
                    scenario.Player.Id,
                    new[] { legalCardId },
                    layout,
                    turn,
                    queue));
            sourceVisual.SetInteractionPresentation(
                HandCardInteractionMode.Disabled,
                insufficientColor,
                HandCardSelectionPresentationRole.NonCandidate);
            legalVisual.SetInteractionPresentation(
                HandCardInteractionMode.Disabled,
                insufficientColor,
                HandCardSelectionPresentationRole.Candidate);
            int resultCountBeforeDisable = scenario.Results.Count;

            typeof(HandCardContainer).GetMethod(
                "OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(container, null);

            Assert.That(
                container.ActiveHandCardSelection,
                Is.Null,
                "Disabling the container should cancel the active selection.");
            Assert.That(sourceVisual.SelectionPresentationRole,
                Is.EqualTo(HandCardSelectionPresentationRole.None));
            Assert.That(legalVisual.SelectionPresentationRole,
                Is.EqualTo(HandCardSelectionPresentationRole.None));
            Assert.That(scenario.Zones.Layout.CurrentValue, Is.SameAs(layout));
            Assert.That(scenario.Queue.Turn.CurrentValue, Is.SameAs(turn));
            Assert.That(scenario.Queue.Queue.CurrentValue, Is.SameAs(queue));
            Assert.That(scenario.Results, Has.Count.EqualTo(resultCountBeforeDisable));
        }
        finally
        {
            sourceVisual?.CancelTargetFocus();
            legalVisual?.CancelTargetFocus();
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(legalObject);
            Object.DestroyImmediate(handObject);
            Object.DestroyImmediate(presenterObject);
            foreach (GameObject presentationObject in presentationObjects)
                Object.DestroyImmediate(presentationObject);
            DOTween.KillAll(complete: false);
        }
    }
}
