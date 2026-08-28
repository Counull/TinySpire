using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TinySpire.Run;
using TinySpire.Run.Map;
using TinySpire.UI.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class RunEntryViewTests
{
    /// <summary>奖励选择与跳过动作必须分别携带精确奖励身份及合法模板载荷。</summary>
    [Test]
    public void CardRewardActions_RequireExactRewardIdentityAndTemplatePayload()
    {
        RunCardRewardId rewardId = CreateRewardId();

        var select = new RunEntryAction(
            RunEntryActionKind.SelectCardReward,
            cardRewardId: rewardId,
            cardTemplateId: 3105);
        var skip = new RunEntryAction(
            RunEntryActionKind.SkipCardReward,
            cardRewardId: rewardId);

        Assert.That(select.CardRewardId, Is.EqualTo(rewardId));
        Assert.That(select.CardTemplateId, Is.EqualTo(3105));
        Assert.That(skip.CardRewardId, Is.EqualTo(rewardId));
        Assert.That(skip.CardTemplateId, Is.Null);
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.SelectCardReward,
                cardRewardId: rewardId);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.SkipCardReward,
                cardRewardId: rewardId,
                cardTemplateId: 3105);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.StartGame,
                cardRewardId: rewardId);
        });
    }

    /// <summary>Rest 治疗与升级动作必须分别携带精确访问身份及合法卡牌实例载荷。</summary>
    [Test]
    public void RestActions_RequireExactVisitAndCardInstancePayload()
    {
        var visitId = new RunNodeVisitId(
            new RunId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            MapNodeId.FromPosition(2, 0));
        var cardInstanceId = new RunCardInstanceId(3);

        var heal = new RunEntryAction(
            RunEntryActionKind.HealAtRest,
            nodeVisitId: visitId);
        var upgrade = new RunEntryAction(
            RunEntryActionKind.UpgradeCardAtRest,
            nodeVisitId: visitId,
            cardInstanceId: cardInstanceId);

        Assert.That(heal.NodeVisitId, Is.EqualTo(visitId));
        Assert.That(heal.CardInstanceId, Is.Null);
        Assert.That(upgrade.NodeVisitId, Is.EqualTo(visitId));
        Assert.That(upgrade.CardInstanceId, Is.EqualTo(cardInstanceId));
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.HealAtRest,
                nodeVisitId: visitId,
                cardInstanceId: cardInstanceId);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.UpgradeCardAtRest,
                nodeVisitId: visitId);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.StartGame,
                nodeVisitId: visitId);
        });
    }

    /// <summary>宝箱领取与跳过动作都只携带精确访问身份，拒绝缺失身份或夹带 Rest 实例载荷。</summary>
    [Test]
    public void ChestActions_RequireOnlyExactVisitIdentity()
    {
        var visitId = new RunNodeVisitId(
            new RunId(Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff")),
            MapNodeId.FromPosition(2, 0));

        var claim = new RunEntryAction(
            RunEntryActionKind.ClaimChest,
            nodeVisitId: visitId);
        var skip = new RunEntryAction(
            RunEntryActionKind.SkipChest,
            nodeVisitId: visitId);

        Assert.That(claim.NodeVisitId, Is.EqualTo(visitId));
        Assert.That(skip.NodeVisitId, Is.EqualTo(visitId));
        Assert.That(claim.CardInstanceId, Is.Null);
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(RunEntryActionKind.ClaimChest);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.SkipChest,
                nodeVisitId: visitId,
                cardInstanceId: new RunCardInstanceId(1));
        });
    }

    /// <summary>Shop 购买必须携带访问身份与专用库存身份，Leave 只允许携带访问身份。</summary>
    [Test]
    public void ShopActions_RequireExactVisitAndStockEntryPayload()
    {
        var visitId = new RunNodeVisitId(
            new RunId(Guid.Parse("dddddddd-eeee-ffff-aaaa-bbbbbbbbbbbb")),
            MapNodeId.FromPosition(4, 0));

        var purchase = new RunEntryAction(
            RunEntryActionKind.PurchaseShopStock,
            nodeVisitId: visitId,
            shopStockEntryId: 2);
        var leave = new RunEntryAction(
            RunEntryActionKind.LeaveShop,
            nodeVisitId: visitId);

        Assert.That(purchase.NodeVisitId, Is.EqualTo(visitId));
        Assert.That(purchase.ShopStockEntryId, Is.EqualTo(2));
        Assert.That(leave.NodeVisitId, Is.EqualTo(visitId));
        Assert.That(leave.ShopStockEntryId, Is.Null);
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.PurchaseShopStock,
                nodeVisitId: visitId);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.PurchaseShopStock,
                nodeVisitId: visitId,
                shopStockEntryId: 0);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.PurchaseShopStock,
                nodeVisitId: visitId,
                cardInstanceId: new RunCardInstanceId(1),
                shopStockEntryId: 1);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.LeaveShop,
                nodeVisitId: visitId,
                shopStockEntryId: 1);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.StartGame,
                shopStockEntryId: 1);
        });
    }

    /// <summary>Event 动作必须携带稳定访问身份与闭合 choice，且不得与其他节点 payload 混用。</summary>
    [Test]
    public void EventActions_RequireExactVisitAndDefinedChoicePayload()
    {
        var visitId = new RunNodeVisitId(
            new RunId(Guid.Parse("ffffffff-aaaa-bbbb-cccc-dddddddddddd")),
            MapNodeId.FromPosition(5, 0));

        var gain = new RunEntryAction(
            RunEntryActionKind.ChooseEvent,
            nodeVisitId: visitId,
            eventChoice: RunEventChoiceKind.GainGold);

        Assert.That(gain.NodeVisitId, Is.EqualTo(visitId));
        Assert.That(gain.EventChoice, Is.EqualTo(RunEventChoiceKind.GainGold));
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.ChooseEvent,
                eventChoice: RunEventChoiceKind.GainGold);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.ChooseEvent,
                nodeVisitId: visitId);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.ChooseEvent,
                nodeVisitId: visitId,
                eventChoice: (RunEventChoiceKind)999);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.ChooseEvent,
                nodeVisitId: visitId,
                shopStockEntryId: 1,
                eventChoice: RunEventChoiceKind.PaidHeal);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.HealAtRest,
                nodeVisitId: visitId,
                eventChoice: RunEventChoiceKind.GainGold);
        });
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new RunEntryAction(
                RunEntryActionKind.StartGame,
                eventChoice: RunEventChoiceKind.GainGold);
        });
    }

    /// <summary>主菜单继续按钮按 ViewModel 启用，并只发布一次 ContinueGame 意图。</summary>
    [Test]
    public void RenderMainMenu_EnabledContinueButton_EmitsContinueAction()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                RunEntryPage.MainMenu,
                selectedHeroTemplateId: null,
                confirmEnabled: false,
                continueEnabled: true));

            Button continueButton = view.GetButtonForTesting("ContinueGameButton");
            Assert.That(continueButton.interactable, Is.True);
            continueButton.onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.ContinueGame));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>主菜单没有可恢复检查点时继续按钮必须禁用，直接调用也不得发布动作。</summary>
    [Test]
    public void RenderMainMenu_DisabledContinueButton_DoesNotEmitAction()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                RunEntryPage.MainMenu,
                selectedHeroTemplateId: null,
                confirmEnabled: false));

            Button continueButton = view.GetButtonForTesting("ContinueGameButton");
            Assert.That(continueButton.interactable, Is.False);
            continueButton.onClick.Invoke();

            Assert.That(actions, Is.Empty);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>运行时几何入口必须只使用 TMP 文本，并创建可工作的 Input System UI 事件链。</summary>
    [Test]
    public void Build_CreatesTmpOnlyUiAndInputSystemEventModule()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();

            Assert.That(root.GetComponentInChildren<Canvas>(true), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<GraphicRaycaster>(true), Is.Not.Null);
            Assert.That(root.GetComponentInChildren<EventSystem>(true), Is.Not.Null);
            InputSystemUIInputModule inputModule =
                root.GetComponentInChildren<InputSystemUIInputModule>(true);
            Assert.That(inputModule, Is.Not.Null);
            Assert.That(inputModule.actionsAsset, Is.Not.Null);
            Assert.That(inputModule.point?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(inputModule.leftClick?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(inputModule.move?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(inputModule.submit?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(inputModule.cancel?.action?.actionMap?.asset, Is.SameAs(inputModule.actionsAsset));
            Assert.That(root.GetComponentsInChildren<TMP_Text>(true), Is.Not.Empty);
            Assert.That(root.GetComponentsInChildren<Text>(true), Is.Empty);
            Assert.That(
                root.GetComponentsInChildren<TMP_Text>(true).All(text => text.font != null),
                Is.True);
            TMP_FontAsset font = root.GetComponentsInChildren<TMP_Text>(true)[0].font;
            Assert.That(
                font.HasCharacters(
                    RunEntryView.RequiredEntryGlyphs,
                    out List<char> missingCharacters),
                Is.True,
                $"Missing RunEntry glyphs: {string.Concat(missingCharacters ?? new List<char>())}");
            Assert.That(
                root.GetComponentsInChildren<Button>(true)
                    .All(button => button.onClick.GetPersistentEventCount() == 0),
                Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>完整 ViewModel 只激活目标页，并把选择按钮归一化为单个带 Hero id 的动作。</summary>
    [Test]
    public void RenderHeroSelection_ActivatesOnlyHeroPageAndEmitsSelectedHeroAction()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                RunEntryPage.HeroSelection,
                selectedHeroTemplateId: 1002,
                confirmEnabled: true));

            foreach (RunEntryPage page in Enum.GetValues(typeof(RunEntryPage)))
            {
                Assert.That(
                    view.GetPageForTesting(page).activeSelf,
                    Is.EqualTo(page == RunEntryPage.HeroSelection),
                    page.ToString());
            }
            Assert.That(view.GetButtonForTesting("ConfirmHeroButton").interactable, Is.True);
            Assert.That(
                view.GetButtonForTesting("Hero1002Button").targetGraphic.color,
                Is.EqualTo((Color)new Color32(75, 145, 205, 255)));

            view.GetButtonForTesting("Hero1002Button").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.SelectHero));
            Assert.That(actions[0].HeroTemplateId, Is.EqualTo(1002));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>确认与存档故障页保持互斥，关键按钮每次点击只发布一个确定动作。</summary>
    [TestCase(
        RunEntryPage.AbandonConfirmation,
        "ConfirmAbandonButton",
        RunEntryActionKind.ConfirmAbandon)]
    [TestCase(
        RunEntryPage.SaveFailure,
        "RetrySaveButton",
        RunEntryActionKind.RetrySave)]
    [TestCase(
        RunEntryPage.SaveFailure,
        "SaveFailureExitButton",
        RunEntryActionKind.RequestExitAfterSaveFailure)]
    [TestCase(
        RunEntryPage.RollbackConfirmation,
        "ConfirmRollbackButton",
        RunEntryActionKind.ConfirmRollback)]
    public void RenderPersistencePage_ActivatesOnlyTargetAndEmitsActionOnce(
        RunEntryPage targetPage,
        string buttonName,
        RunEntryActionKind expectedAction)
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                targetPage,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                canRollbackFailedSave: true));

            foreach (RunEntryPage page in Enum.GetValues(typeof(RunEntryPage)))
            {
                Assert.That(
                    view.GetPageForTesting(page).activeSelf,
                    Is.EqualTo(page == targetPage),
                    page.ToString());
            }

            view.GetButtonForTesting(buttonName).onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(expectedAction));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>奖励页固定绘制三张候选与跳过，并在重复投影后只发布最新身份的一次动作。</summary>
    [Test]
    public void RenderCardReward_ReplacesProjectionWithoutStackingListeners()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;
            RunCardRewardId rewardId = CreateRewardId();
            RunCardRewardViewModel first = CreateCardRewardModel(
                rewardId,
                "Strike",
                "Defend",
                "Focus");
            RunCardRewardViewModel localized = CreateCardRewardModel(
                rewardId,
                "重击",
                "防御",
                "专注");

            view.Render(CreateModel(
                RunEntryPage.CardReward,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                cardReward: first));
            view.Render(CreateModel(
                RunEntryPage.CardReward,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                cardReward: localized));

            GameObject rewardPage = view.GetPageForTesting(RunEntryPage.CardReward);
            Assert.That(rewardPage.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(4));
            Assert.That(
                view.GetButtonForTesting("CardRewardCandidate1Button")
                    .GetComponentInChildren<TMP_Text>(true).text,
                Does.Contain("防御"));

            view.GetButtonForTesting("CardRewardCandidate1Button").onClick.Invoke();
            view.GetButtonForTesting("SkipCardRewardButton").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(2));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.SelectCardReward));
            Assert.That(actions[0].CardRewardId, Is.EqualTo(rewardId));
            Assert.That(actions[0].CardTemplateId, Is.EqualTo(3123));
            Assert.That(actions[1].Kind, Is.EqualTo(RunEntryActionKind.SkipCardReward));
            Assert.That(actions[1].CardRewardId, Is.EqualTo(rewardId));
            Assert.That(actions[1].CardTemplateId, Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>Rest 页重复投影只替换最新访问与实例动作，且没有返回或跳过后门。</summary>
    [Test]
    public void RenderRest_ReplacesProjectionWithoutStackingListeners()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;
            var runId = new RunId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
            var firstVisit = new RunNodeVisitId(runId, MapNodeId.FromPosition(2, 0));
            var latestVisit = new RunNodeVisitId(runId, MapNodeId.FromPosition(2, 1));
            var first = new RunRestViewModel(
                firstVisit,
                24,
                "Heal 24 HP",
                healEnabled: true,
                new[]
                {
                    new RunRestUpgradeCandidateViewModel(
                        new RunCardInstanceId(1), 3002, 0, "Upgrade Strike to +1", enabled: true),
                });
            var latest = new RunRestViewModel(
                latestVisit,
                24,
                "恢复 24 点生命",
                healEnabled: true,
                new[]
                {
                    new RunRestUpgradeCandidateViewModel(
                        new RunCardInstanceId(3), 3002, 0, "升级重击至 +1", enabled: true),
                    new RunRestUpgradeCandidateViewModel(
                        new RunCardInstanceId(4), 3003, 0, "升级防御至 +1", enabled: true),
                });

            view.Render(CreateModel(
                RunEntryPage.Rest,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                rest: first));
            view.Render(CreateModel(
                RunEntryPage.Rest,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                rest: latest));

            GameObject restPage = view.GetPageForTesting(RunEntryPage.Rest);
            Assert.That(restPage.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(3));
            Assert.That(
                view.GetButtonForTesting("RestUpgradeCandidate1Button")
                    .GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("升级防御至 +1"));

            view.GetButtonForTesting("RestHealButton").onClick.Invoke();
            view.GetButtonForTesting("RestUpgradeCandidate1Button").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(2));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.HealAtRest));
            Assert.That(actions[0].NodeVisitId, Is.EqualTo(latestVisit));
            Assert.That(actions[0].CardInstanceId, Is.Null);
            Assert.That(actions[1].Kind, Is.EqualTo(RunEntryActionKind.UpgradeCardAtRest));
            Assert.That(actions[1].NodeVisitId, Is.EqualTo(latestVisit));
            Assert.That(actions[1].CardInstanceId, Is.EqualTo(new RunCardInstanceId(4)));

            var disabled = new RunRestViewModel(
                latestVisit,
                24,
                "Heal 24 HP",
                healEnabled: false,
                new[]
                {
                    new RunRestUpgradeCandidateViewModel(
                        new RunCardInstanceId(3), 3002, 0, "Upgrade Strike to +1", enabled: false),
                    new RunRestUpgradeCandidateViewModel(
                        new RunCardInstanceId(4), 3003, 0, "Upgrade Defend to +1", enabled: false),
                });
            view.Render(CreateModel(
                RunEntryPage.Rest,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                rest: disabled));
            view.GetButtonForTesting("RestHealButton").onClick.Invoke();
            view.GetButtonForTesting("RestUpgradeCandidate1Button").onClick.Invoke();
            Assert.That(actions, Has.Count.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>Chest 页只有领取与跳过，重复渲染使用最新身份，满槽只禁用领取且不叠加监听。</summary>
    [Test]
    public void RenderChest_UsesLatestIdentityWithoutBackOrStackedListenersAndKeepsSkipEnabledWhenFull()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;
            var runId = new RunId(Guid.Parse("cccccccc-dddd-eeee-ffff-aaaaaaaaaaaa"));
            var firstVisit = new RunNodeVisitId(runId, MapNodeId.FromPosition(2, 0));
            var latestVisit = new RunNodeVisitId(runId, MapNodeId.FromPosition(2, 1));
            var potion = new RunHoldingItemViewModel(
                9001,
                "Healing Potion",
                "Restore 10 HP.");
            var first = new RunChestViewModel(
                firstVisit,
                potion,
                "Claim",
                "Skip",
                "Potion belt is full",
                claimEnabled: true,
                skipEnabled: true,
                isCapacityFull: false);
            var latest = new RunChestViewModel(
                latestVisit,
                potion,
                "领取",
                "跳过",
                "药水带已满",
                claimEnabled: true,
                skipEnabled: true,
                isCapacityFull: false);

            view.Render(CreateModel(
                RunEntryPage.Chest,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                chest: first));
            view.Render(CreateModel(
                RunEntryPage.Chest,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                chest: latest));

            GameObject chestPage = view.GetPageForTesting(RunEntryPage.Chest);
            Assert.That(chestPage.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(2));
            Assert.That(view.GetButtonForTesting("ChestClaimButton")
                .GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("领取"));
            Assert.That(view.GetButtonForTesting("ChestSkipButton")
                .GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("跳过"));

            view.GetButtonForTesting("ChestClaimButton").onClick.Invoke();
            view.GetButtonForTesting("ChestSkipButton").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(2));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.ClaimChest));
            Assert.That(actions[0].NodeVisitId, Is.EqualTo(latestVisit));
            Assert.That(actions[1].Kind, Is.EqualTo(RunEntryActionKind.SkipChest));
            Assert.That(actions[1].NodeVisitId, Is.EqualTo(latestVisit));

            var full = new RunChestViewModel(
                latestVisit,
                potion,
                "领取",
                "跳过",
                "药水带已满",
                claimEnabled: false,
                skipEnabled: true,
                isCapacityFull: true);
            view.Render(CreateModel(
                RunEntryPage.Chest,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                chest: full));
            view.GetButtonForTesting("ChestClaimButton").onClick.Invoke();
            view.GetButtonForTesting("ChestSkipButton").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(3));
            Assert.That(actions[2].Kind, Is.EqualTo(RunEntryActionKind.SkipChest));
            Assert.That(actions[2].NodeVisitId, Is.EqualTo(latestVisit));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>Shop 页固定三项购买与 Leave，重复渲染只使用最新身份且禁用项不会发布动作。</summary>
    [Test]
    public void RenderShop_UsesLatestTypedStockActionsWithoutBackOrStackedListeners()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;
            var runId = new RunId(Guid.Parse("eeeeeeee-ffff-aaaa-bbbb-cccccccccccc"));
            var firstVisit = new RunNodeVisitId(runId, MapNodeId.FromPosition(4, 0));
            var latestVisit = new RunNodeVisitId(runId, MapNodeId.FromPosition(4, 1));
            var first = CreateShopModel(
                firstVisit,
                "Buy",
                leaveEnabled: true,
                entryIdOffset: 10);
            var latest = new RunShopViewModel(
                latestVisit,
                new[]
                {
                    new RunShopStockEntryViewModel(
                        1, RunShopStockKind.Relic, 8001, 75,
                        "Relic One", "Relic One — Purchased", purchased: true,
                        purchaseEnabled: false),
                    new RunShopStockEntryViewModel(
                        2, RunShopStockKind.Potion, 9001, 25,
                        "Healing Potion", "购买 Healing Potion — 25 金币", purchased: false,
                        purchaseEnabled: true),
                    new RunShopStockEntryViewModel(
                        3, RunShopStockKind.Card, 3123, 50,
                        "Test Guard", "购买 Test Guard — 50 金币", purchased: false,
                        purchaseEnabled: true),
                },
                "离开",
                leaveEnabled: true);

            view.Render(CreateModel(
                RunEntryPage.Shop,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                shop: first));
            view.Render(CreateModel(
                RunEntryPage.Shop,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                shop: latest));

            GameObject shopPage = view.GetPageForTesting(RunEntryPage.Shop);
            Assert.That(shopPage.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(4));
            Assert.That(view.GetButtonForTesting("ShopStock1Button").interactable, Is.False);
            Assert.That(view.GetButtonForTesting("ShopStock1Button")
                .GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("Relic One — Purchased"));
            Assert.That(view.GetButtonForTesting("ShopStock2Button")
                .GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("购买 Healing Potion — 25 金币"));
            view.GetButtonForTesting("ShopStock1Button").onClick.Invoke();
            view.GetButtonForTesting("ShopStock2Button").onClick.Invoke();
            view.GetButtonForTesting("ShopStock3Button").onClick.Invoke();
            view.GetButtonForTesting("ShopLeaveButton").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(3));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.PurchaseShopStock));
            Assert.That(actions[0].NodeVisitId, Is.EqualTo(latestVisit));
            Assert.That(actions[0].ShopStockEntryId, Is.EqualTo(2));
            Assert.That(actions[1].ShopStockEntryId, Is.EqualTo(3));
            Assert.That(actions[2].Kind, Is.EqualTo(RunEntryActionKind.LeaveShop));
            Assert.That(actions[2].NodeVisitId, Is.EqualTo(latestVisit));

            view.Render(CreateModel(
                RunEntryPage.Shop,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                shop: CreateShopModel(latestVisit, "Locked", leaveEnabled: false)));
            view.GetButtonForTesting("ShopStock2Button").onClick.Invoke();
            view.GetButtonForTesting("ShopLeaveButton").onClick.Invoke();
            Assert.That(actions, Has.Count.EqualTo(3));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>Event 固定双按钮只发布最新 Visit 与 choice，禁用态和重复 Render 都不留下旧监听。</summary>
    [Test]
    public void RenderEvent_UsesLatestTypedChoiceActionsWithoutBackOrStackedListeners()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;
            var runId = new RunId(Guid.Parse("abababab-cdcd-efef-1212-343434343434"));
            var firstVisit = new RunNodeVisitId(runId, MapNodeId.FromPosition(5, 0));
            var latestVisit = new RunNodeVisitId(runId, MapNodeId.FromPosition(5, 1));
            var first = new RunEventViewModel(
                firstVisit,
                gainGoldAmount: 50,
                paidHealCost: 25,
                paidHealAmount: 15,
                gainGoldText: "Gain first",
                paidHealText: "Heal first",
                gainGoldEnabled: true,
                paidHealEnabled: true);
            var latest = new RunEventViewModel(
                latestVisit,
                gainGoldAmount: 50,
                paidHealCost: 25,
                paidHealAmount: 15,
                gainGoldText: "获得 50 金币",
                paidHealText: "支付 25 金币，最多恢复 15 点生命",
                gainGoldEnabled: false,
                paidHealEnabled: true);

            view.Render(CreateModel(
                RunEntryPage.Event,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                eventNode: first));
            view.Render(CreateModel(
                RunEntryPage.Event,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                eventNode: latest));

            GameObject eventPage = view.GetPageForTesting(RunEntryPage.Event);
            Assert.That(eventPage.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(2));
            Assert.That(view.GetButtonForTesting("EventGainGoldButton").interactable, Is.False);
            Assert.That(view.GetButtonForTesting("EventGainGoldButton")
                .GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("获得 50 金币"));
            Assert.That(view.GetButtonForTesting("EventPaidHealButton")
                .GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("支付 25 金币，最多恢复 15 点生命"));
            view.GetButtonForTesting("EventGainGoldButton").onClick.Invoke();
            view.GetButtonForTesting("EventPaidHealButton").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.ChooseEvent));
            Assert.That(actions[0].NodeVisitId, Is.EqualTo(latestVisit));
            Assert.That(actions[0].EventChoice, Is.EqualTo(RunEventChoiceKind.PaidHeal));

            view.Render(CreateModel(
                RunEntryPage.Event,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                eventNode: new RunEventViewModel(
                    latestVisit,
                    gainGoldAmount: 50,
                    paidHealCost: 25,
                    paidHealAmount: 15,
                    gainGoldText: "Locked gain",
                    paidHealText: "Locked heal",
                    gainGoldEnabled: false,
                    paidHealEnabled: false)));
            view.GetButtonForTesting("EventGainGoldButton").onClick.Invoke();
            view.GetButtonForTesting("EventPaidHealButton").onClick.Invoke();
            Assert.That(actions, Has.Count.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>持有物面板只用 TMP 替换文本，重复 Render 不创建按钮也不叠加既有动作监听。</summary>
    [Test]
    public void RenderHoldings_ReplacesTmpTextWithoutAddingButtonsOrListeners()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;
            RunMapViewModel map = CreateMapModel();
            var first = new RunHoldingsViewModel(
                "Gold 100",
                "Relics",
                "Potions",
                "Empty",
                Array.Empty<RunHoldingItemViewModel>(),
                Array.Empty<RunHoldingItemViewModel>());
            var localized = new RunHoldingsViewModel(
                "金币 135",
                "遗物",
                "药水",
                "无",
                new[]
                {
                    new RunHoldingItemViewModel(8002, "第二遗物", "战斗开始时获得 3 点力量。"),
                    new RunHoldingItemViewModel(8001, "第一遗物", "战斗开始时获得 1 点力量。"),
                },
                new[]
                {
                    new RunHoldingItemViewModel(9001, "治疗药水", "恢复 10 点生命。"),
                });

            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: map,
                holdings: first));
            Transform panel = root.GetComponentsInChildren<Transform>(true)
                .Single(value => value.name == "RunHoldingsPanel");
            TMP_Text gold = panel.GetComponentsInChildren<TMP_Text>(true)
                .Single(value => value.name == "RunHoldingsGoldText");
            TMP_Text relics = panel.GetComponentsInChildren<TMP_Text>(true)
                .Single(value => value.name == "RunHoldingsRelicsText");
            TMP_Text potions = panel.GetComponentsInChildren<TMP_Text>(true)
                .Single(value => value.name == "RunHoldingsPotionsText");
            RectTransform contentSurface = root.GetComponentsInChildren<RectTransform>(true)
                .Single(value => value.name == "ContentSurface");
            AssertHoldingTextFitsReferenceSidebar(gold.rectTransform, contentSurface);
            AssertHoldingTextFitsReferenceSidebar(relics.rectTransform, contentSurface);
            AssertHoldingTextFitsReferenceSidebar(potions.rectTransform, contentSurface);
            Assert.That(relics.text, Is.EqualTo("Relics\nEmpty"));
            Assert.That(potions.text, Is.EqualTo("Potions\nEmpty"));
            int buttonsAfterFirstRender = root.GetComponentsInChildren<Button>(true).Length;
            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: map,
                holdings: localized));

            Assert.That(panel.gameObject.activeSelf, Is.True);
            Assert.That(gold.text, Is.EqualTo("金币 135"));
            Assert.That(relics.text, Is.EqualTo(
                "遗物\n第二遗物 — 战斗开始时获得 3 点力量。\n第一遗物 — 战斗开始时获得 1 点力量。"));
            Assert.That(potions.text, Is.EqualTo("药水\n治疗药水 — 恢复 10 点生命。"));
            Assert.That(panel.GetComponentsInChildren<Button>(true), Is.Empty);
            Assert.That(
                panel.GetComponentsInChildren<TMP_Text>(true).All(text => !text.raycastTarget),
                Is.True);
            Assert.That(
                root.GetComponentsInChildren<Button>(true),
                Has.Length.EqualTo(buttonsAfterFirstRender));

            view.GetButtonForTesting("MapNode_L01-S00_Button").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.EnterMapNode));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>确认持有物文本四边位于 1920×1080 参考屏内，并完全处于内容面右侧。</summary>
    private static void AssertHoldingTextFitsReferenceSidebar(
        RectTransform textRect,
        RectTransform contentSurface)
    {
        const float referenceHalfWidth = 960f;
        const float referenceHalfHeight = 540f;
        float left = textRect.anchoredPosition.x + textRect.rect.xMin;
        float right = textRect.anchoredPosition.x + textRect.rect.xMax;
        float bottom = textRect.anchoredPosition.y + textRect.rect.yMin;
        float top = textRect.anchoredPosition.y + textRect.rect.yMax;
        float contentRight = contentSurface.anchoredPosition.x + contentSurface.rect.xMax;

        Assert.That(left, Is.GreaterThanOrEqualTo(-referenceHalfWidth), textRect.name);
        Assert.That(right, Is.LessThanOrEqualTo(referenceHalfWidth), textRect.name);
        Assert.That(bottom, Is.GreaterThanOrEqualTo(-referenceHalfHeight), textRect.name);
        Assert.That(top, Is.LessThanOrEqualTo(referenceHalfHeight), textRect.name);
        Assert.That(left, Is.GreaterThan(contentRight), textRect.name);
    }

    /// <summary>地图页只在 Presenter 授权时开放主动放弃，BossGate 当前节点也可提交真实 Boss 入战动作。</summary>
    [Test]
    public void RenderMap_EnablesAbandonAndBossGateActions()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;
            var map = new RunMapViewModel(
                "boss-gate-map",
                new[]
                {
                    Node(
                        "L00-S00", 0, 0, MapNodeKind.Start, 0, "START",
                        RunMapVisualAnchorKind.StartFlag,
                        RunMapNodePresentationState.Completed),
                    Node(
                        "L01-S00", 1, 0, MapNodeKind.Boss, 9001, "BOSS ALPHA",
                        RunMapVisualAnchorKind.BossAlphaCrown,
                        RunMapNodePresentationState.BossGateReached),
                },
                new[]
                {
                    new RunMapEdgeViewModel("L00-S00", "L01-S00", isCompletedPath: true),
                });

            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: map,
                canAbandonActiveRun: true));

            Button abandon = view.GetButtonForTesting("MapAbandonRunButton");
            Button boss = view.GetButtonForTesting("MapNode_L01-S00_Button");
            Assert.That(abandon.interactable, Is.True);
            Assert.That(boss.interactable, Is.True);

            abandon.onClick.Invoke();
            boss.onClick.Invoke();

            Assert.That(actions.Select(action => action.Kind), Is.EqualTo(new[]
            {
                RunEntryActionKind.RequestAbandon,
                RunEntryActionKind.EnterMapNode,
            }));
            Assert.That(actions[1].MapNodeId, Is.EqualTo(new MapNodeId("L01-S00")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>整图名称、稳定内容 ID 与视觉锚点均可见，只有可选节点提交稳定 NodeId。</summary>
    [Test]
    public void RenderMap_DrawsWholeFrozenGraphWithNamesIdsAndVisualAnchors()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            RunMapViewModel map = CreateMapModel();
            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: map));

            foreach (RunMapNodeViewModel node in map.Nodes)
            {
                Button button = view.GetButtonForTesting($"MapNode_{node.NodeId}_Button");
                Assert.That(
                    button.interactable,
                    Is.EqualTo(node.State == RunMapNodePresentationState.Selectable),
                    node.NodeId);
                Assert.That(
                    button.transform.Find($"MapNode_{node.NodeId}_ButtonLabel")
                        .GetComponent<TMP_Text>().text,
                    Is.EqualTo(node.DisplayName));
                Assert.That(
                    button.transform.Find($"MapNode_{node.NodeId}_IdentityId")
                        .GetComponent<TMP_Text>().text,
                    Is.EqualTo(node.ContentId > 0 ? $"#{node.ContentId}" : node.NodeId));
                Assert.That(
                    button.transform.Find($"MapNode_{node.NodeId}_Anchor_{node.VisualAnchorKind}"),
                    Is.Not.Null,
                    node.NodeId);
            }

            Button selected = view.GetButtonForTesting("MapNode_L01-S00_Button");
            selected.onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.EnterMapNode));
            Assert.That(actions[0].MapNodeId, Is.EqualTo(new MapNodeId("L01-S00")));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>不同 Boss 身份绘制不同程序化锚点，同一 Boss 的重复终点绘制同一种锚点。</summary>
    [Test]
    public void RenderMap_BossIdentityAnchorsAreDistinctAndRepeatable()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            RunMapViewModel map = CreateMapModel();
            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: map));

            Transform alpha = FindAnchor(view, "L03-S00", RunMapVisualAnchorKind.BossAlphaCrown);
            Transform beta = FindAnchor(view, "L03-S01", RunMapVisualAnchorKind.BossBetaHorns);
            Transform repeatedAlpha = FindAnchor(view, "L03-S02", RunMapVisualAnchorKind.BossAlphaCrown);

            Assert.That(GetShapeNames(alpha), Is.EqualTo(GetShapeNames(repeatedAlpha)));
            Assert.That(GetShapeNames(alpha), Is.Not.EqualTo(GetShapeNames(beta)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>不同首敌的遭遇节点必须建立不同程序化剪影，而不是共享通用战斗图标。</summary>
    [Test]
    public void RenderMap_EncounterPrimaryEnemySilhouettesAreDistinct()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: CreateMapModel()));

            Transform slime = FindAnchor(
                view,
                "L01-S00",
                RunMapVisualAnchorKind.EncounterSlimeSilhouette);
            Transform sentry = FindAnchor(
                view,
                "L01-S01",
                RunMapVisualAnchorKind.EncounterSentrySilhouette);

            Assert.That(GetShapeNames(slime), Is.Not.EqualTo(GetShapeNames(sentry)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>悬停可选节点时完整后半程保持高亮，另一条路线和不可达 Boss 被弱化。</summary>
    [Test]
    public void HoverSelectableNode_HighlightsDownstreamAndDimsForfeitedBoss()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            view.Render(CreateModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: CreateMapModel()));

            Button candidate = view.GetButtonForTesting("MapNode_L01-S00_Button");
            Button reachableBoss = view.GetButtonForTesting("MapNode_L03-S00_Button");
            Button forfeitedBoss = view.GetButtonForTesting("MapNode_L03-S01_Button");
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(
                candidate.gameObject,
                pointer,
                ExecuteEvents.pointerEnterHandler);

            Assert.That(reachableBoss.targetGraphic.color, Is.EqualTo(candidate.targetGraphic.color));
            Assert.That(
                forfeitedBoss.targetGraphic.color.a,
                Is.LessThan(reachableBoss.targetGraphic.color.a));

            ExecuteEvents.Execute(
                candidate.gameObject,
                pointer,
                ExecuteEvents.pointerExitHandler);
            Assert.That(
                forfeitedBoss.targetGraphic.color.a,
                Is.GreaterThan(0.7f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>失败页只提交确认离开终局，Terminal 保存失败页不会暴露旧检查点回退按钮。</summary>
    [Test]
    public void RenderFailureAndTerminalSaveFailure_RemoveRetrySemanticsAndRollback()
    {
        var root = new GameObject("RunEntryViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var actions = new List<RunEntryAction>();
            view.ActionRequested += actions.Add;

            view.Render(CreateModel(
                RunEntryPage.Failure,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: CreateMapModel()));
            view.GetButtonForTesting("LeaveTerminalRunButton").onClick.Invoke();

            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].Kind, Is.EqualTo(RunEntryActionKind.LeaveTerminalRun));

            view.Render(CreateModel(
                RunEntryPage.SaveFailure,
                selectedHeroTemplateId: 1001,
                confirmEnabled: false,
                map: CreateMapModel(),
                canRollbackFailedSave: false));
            Assert.That(
                view.GetButtonForTesting("SaveFailureExitButton").gameObject.activeSelf,
                Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>创建覆盖全部文本槽位的冻结测试投影。</summary>
    private static RunEntryViewModel CreateModel(
        RunEntryPage page,
        int? selectedHeroTemplateId,
        bool confirmEnabled,
        RunMapViewModel map = null,
        bool continueEnabled = false,
        bool canRollbackFailedSave = false,
        bool canAbandonActiveRun = false,
        RunCardRewardViewModel cardReward = null,
        RunHoldingsViewModel holdings = null,
        RunRestViewModel rest = null,
        RunChestViewModel chest = null,
        RunShopViewModel shop = null,
        RunEventViewModel eventNode = null)
    {
        var texts = new Dictionary<RunEntryTextSlot, string>();
        foreach (RunEntryTextSlot slot in Enum.GetValues(typeof(RunEntryTextSlot)))
            texts.Add(slot, slot.ToString());

        return new RunEntryViewModel(
            page,
            texts,
            selectedHeroTemplateId,
            confirmEnabled,
            map,
            continueEnabled,
            canRollbackFailedSave,
            canAbandonActiveRun,
            cardReward,
            holdings,
            rest,
            chest,
            shop,
            eventNode);
    }

    /// <summary>建立三类固定库存的 Shop ViewModel，并可统一控制动作门禁。</summary>
    private static RunShopViewModel CreateShopModel(
        RunNodeVisitId visitId,
        string prefix,
        bool leaveEnabled,
        int entryIdOffset = 0)
    {
        return new RunShopViewModel(
            visitId,
            new[]
            {
                new RunShopStockEntryViewModel(
                    entryIdOffset + 1, RunShopStockKind.Relic, 8001, 75,
                    "Relic One", $"{prefix} Relic One", purchased: false,
                    purchaseEnabled: leaveEnabled),
                new RunShopStockEntryViewModel(
                    entryIdOffset + 2, RunShopStockKind.Potion, 9001, 25,
                    "Healing Potion", $"{prefix} Healing Potion", purchased: false,
                    purchaseEnabled: leaveEnabled),
                new RunShopStockEntryViewModel(
                    entryIdOffset + 3, RunShopStockKind.Card, 3123, 50,
                    "Test Guard", $"{prefix} Test Guard", purchased: false,
                    purchaseEnabled: leaveEnabled),
            },
            "Leave",
            leaveEnabled);
    }

    /// <summary>创建按固定模板顺序排列的三张奖励候选投影。</summary>
    private static RunCardRewardViewModel CreateCardRewardModel(
        RunCardRewardId rewardId,
        string firstName,
        string secondName,
        string thirdName)
    {
        return new RunCardRewardViewModel(
            rewardId,
            new[]
            {
                new RunCardRewardCandidateViewModel(3105, firstName, "Deal 8 damage.", "1"),
                new RunCardRewardCandidateViewModel(3123, secondName, "Gain 6 block.", "1"),
                new RunCardRewardCandidateViewModel(3157, thirdName, "Draw 2 cards.", "X"),
            },
            actionsEnabled: true);
    }

    /// <summary>创建含两条分支与两个不同 Boss 的完整地图 View 投影。</summary>
    private static RunMapViewModel CreateMapModel()
    {
        RunMapNodeViewModel[] nodes =
        {
            Node("L00-S00", 0, 0, MapNodeKind.Start, 0, "START",
                RunMapVisualAnchorKind.StartFlag,
                RunMapNodePresentationState.Current),
            Node("L01-S00", 1, 0, MapNodeKind.Combat, 5001, "SLIME PATROL\nTest Slime",
                RunMapVisualAnchorKind.EncounterSlimeSilhouette,
                RunMapNodePresentationState.Selectable,
                new[] { "L01-S00", "L02-S00", "L03-S00" },
                new[] { "L01-S00>L02-S00", "L02-S00>L03-S00" }),
            Node("L01-S01", 1, 1, MapNodeKind.Combat, 5002, "SENTRY LINE\nTest Sentry",
                RunMapVisualAnchorKind.EncounterSentrySilhouette,
                RunMapNodePresentationState.Selectable,
                new[] { "L01-S01", "L02-S01", "L03-S01" },
                new[] { "L01-S01>L02-S01", "L02-S01>L03-S01" }),
            Node("L02-S00", 2, 0, MapNodeKind.Combat, 5001, "SLIME PATROL\nTest Slime",
                RunMapVisualAnchorKind.EncounterSlimeSilhouette,
                RunMapNodePresentationState.Locked),
            Node("L02-S01", 2, 1, MapNodeKind.Combat, 5002, "SENTRY LINE\nTest Sentry",
                RunMapVisualAnchorKind.EncounterSentrySilhouette,
                RunMapNodePresentationState.Locked),
            Node("L03-S00", 3, 0, MapNodeKind.Boss, 9001, "BOSS ALPHA",
                RunMapVisualAnchorKind.BossAlphaCrown,
                RunMapNodePresentationState.Locked),
            Node("L03-S01", 3, 1, MapNodeKind.Boss, 9002, "BOSS BETA",
                RunMapVisualAnchorKind.BossBetaHorns,
                RunMapNodePresentationState.Locked),
            Node("L03-S02", 3, 2, MapNodeKind.Boss, 9001, "BOSS ALPHA",
                RunMapVisualAnchorKind.BossAlphaCrown,
                RunMapNodePresentationState.Locked),
        };
        RunMapEdgeViewModel[] edges =
        {
            new RunMapEdgeViewModel("L00-S00", "L01-S00", false),
            new RunMapEdgeViewModel("L00-S00", "L01-S01", false),
            new RunMapEdgeViewModel("L01-S00", "L02-S00", false),
            new RunMapEdgeViewModel("L01-S01", "L02-S01", false),
            new RunMapEdgeViewModel("L02-S00", "L03-S00", false),
            new RunMapEdgeViewModel("L02-S01", "L03-S01", false),
            new RunMapEdgeViewModel("L02-S00", "L03-S02", false),
        };
        return new RunMapViewModel("test-map-fingerprint", nodes, edges);
    }

    /// <summary>创建一个地图节点投影并为非悬停节点补空后半程。</summary>
    private static RunMapNodeViewModel Node(
        string nodeId,
        int layer,
        int slot,
        MapNodeKind kind,
        int contentId,
        string displayName,
        RunMapVisualAnchorKind visualAnchorKind,
        RunMapNodePresentationState state,
        IReadOnlyList<string> downstreamNodeIds = null,
        IReadOnlyList<string> downstreamEdgeKeys = null)
    {
        return new RunMapNodeViewModel(
            nodeId,
            layer,
            slot,
            kind,
            contentId,
            displayName,
            visualAnchorKind,
            state,
            downstreamNodeIds ?? Array.Empty<string>(),
            downstreamEdgeKeys ?? Array.Empty<string>());
    }

    /// <summary>按稳定节点身份读取实际建立的视觉锚点根。</summary>
    private static Transform FindAnchor(
        RunEntryView view,
        string nodeId,
        RunMapVisualAnchorKind kind)
    {
        Button button = view.GetButtonForTesting($"MapNode_{nodeId}_Button");
        return button.transform.Find($"MapNode_{nodeId}_Anchor_{kind}");
    }

    /// <summary>按层级顺序读取程序化锚点的稳定形状名。</summary>
    private static string[] GetShapeNames(Transform anchor)
    {
        Assert.That(anchor, Is.Not.Null);
        return Enumerable.Range(0, anchor.childCount)
            .Select(index => anchor.GetChild(index).name)
            .ToArray();
    }

    /// <summary>建立含稳定 Run、attempt 与节点身份的测试奖励标识。</summary>
    private static RunCardRewardId CreateRewardId()
    {
        return new RunCardRewardId(new RunBattleId(
            new RunId(Guid.Parse("cdef1234-5678-90ab-cdef-1234567890ab")),
            attemptSequence: 2,
            new MapNodeId("L01-S00")));
    }
}
