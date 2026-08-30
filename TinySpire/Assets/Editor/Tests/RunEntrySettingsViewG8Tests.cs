using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using TinySpire.Run.History;
using TinySpire.Run.History.Presentation;
using TinySpire.Run;
using TinySpire.Run.Map;
using TinySpire.Settings;
using TinySpire.Settings.Presentation;
using TinySpire.UI.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;

public sealed class RunEntrySettingsViewG8Tests
{
    /// <summary>动态 RunEntry 设置页必须同时公开两个独立 seam 与首发全部设置按钮。</summary>
    [Test]
    public void BuildSettingsPage_ExposesBothViewSeamsAndLaunchControls()
    {
        var root = new GameObject("RunEntrySettingsViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();

            Assert.That(view, Is.AssignableTo<IRunEntryView>());
            Assert.That(view, Is.AssignableTo<IAppSettingsView>());
            foreach (string buttonName in new[]
                     {
                         "LanguageButton",
                         "MasterVolumeDecreaseButton",
                         "MasterVolumeIncreaseButton",
                         "DisplayModeButton",
                         "ResolutionPreviousButton",
                         "ResolutionNextButton",
                         "TextScaleButton",
                         "HighContrastButton",
                         "ReducedMotionButton",
                         "SettingsBackButton",
                     })
            {
                Assert.That(view.GetButtonForTesting(buttonName), Is.Not.Null, buttonName);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>设置 Render 必须更新实际控件，所有设置按钮只发布设置动作，Back 仍发布 RunEntry 动作。</summary>
    [Test]
    public void SettingsRenderAndButtons_KeepAppAndRunActionsSeparated()
    {
        var root = new GameObject("RunEntrySettingsViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var runActions = new List<RunEntryAction>();
            var appActions = new List<AppSettingsAction>();
            view.ActionRequested += runActions.Add;
            var settingsView = (IAppSettingsView)view;
            settingsView.ActionRequested += appActions.Add;

            settingsView.Render(CreateSettingsModel(
                textScale: AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false));

            GameObject settingsPage = view.GetPageForTesting(RunEntryPage.Settings);
            settingsPage.SetActive(true);
            TMP_Text title = settingsPage
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "SettingsTitle");
            Assert.That(title.text, Is.EqualTo("Settings"));
            Assert.That(
                view.GetButtonForTesting("LanguageButton")
                    .GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo("English"));

            settingsView.Render(CreateSettingsModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false,
                status: AppSettingsViewStatus.SaveFailed,
                failureText: "Cannot save settings."));
            TMP_Text failure = settingsPage.GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "SettingsFailureText");
            Assert.That(failure.text, Is.EqualTo("Cannot save settings."));

            foreach (string buttonName in new[]
                     {
                         "LanguageButton",
                         "MasterVolumeDecreaseButton",
                         "MasterVolumeIncreaseButton",
                         "DisplayModeButton",
                         "ResolutionPreviousButton",
                         "ResolutionNextButton",
                         "TextScaleButton",
                         "HighContrastButton",
                         "ReducedMotionButton",
                     })
            {
                view.GetButtonForTesting(buttonName).onClick.Invoke();
            }

            CollectionAssert.AreEqual(
                new[]
                {
                    AppSettingsActionKind.CycleLocale,
                    AppSettingsActionKind.DecreaseMasterVolume,
                    AppSettingsActionKind.IncreaseMasterVolume,
                    AppSettingsActionKind.ToggleDisplayMode,
                    AppSettingsActionKind.PreviousResolution,
                    AppSettingsActionKind.NextResolution,
                    AppSettingsActionKind.CycleTextScale,
                    AppSettingsActionKind.ToggleHighContrast,
                    AppSettingsActionKind.ToggleReducedMotion,
                },
                appActions.ConvertAll(action => action.Kind));
            Assert.That(runActions, Is.Empty);

            view.GetButtonForTesting("SettingsBackButton").onClick.Invoke();

            Assert.That(runActions, Has.Count.EqualTo(1));
            Assert.That(runActions[0].Kind, Is.EqualTo(RunEntryActionKind.Back));
            Assert.That(appActions, Has.Count.EqualTo(9));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>125% 文字缩放不得累积，高对比与减少动态关闭后必须精确恢复设置页基线。</summary>
    [Test]
    public void AccessibilityRender_IsIdempotentAndRestoresBaselineStyles()
    {
        var root = new GameObject("RunEntrySettingsViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var settingsView = (IAppSettingsView)view;
            GameObject page = view.GetPageForTesting(RunEntryPage.Settings);
            TMP_Text title = page.GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "SettingsTitle");
            Image backdrop = page.transform.Find("SettingsAccessibilityBackdrop")
                .GetComponent<Image>();
            Button language = view.GetButtonForTesting("LanguageButton");
            TMP_Text mainTitle = view.GetPageForTesting(RunEntryPage.MainMenu)
                .GetComponentsInChildren<TMP_Text>(true)
                .Single(text => text.name == "MainTitle");
            Button startGame = view.GetButtonForTesting("StartGameButton");

            settingsView.Render(CreateSettingsModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false));
            float baseFontSize = title.fontSize;
            Color baseBackdrop = backdrop.color;
            Color baseButtonColor = language.targetGraphic.color;
            Selectable.Transition baseTransition = language.transition;
            float baseMainTitleSize = mainTitle.fontSize;
            Color baseStartButtonColor = startGame.targetGraphic.color;
            Selectable.Transition baseStartTransition = startGame.transition;

            AppSettingsViewModel accessible = CreateSettingsModel(
                AppTextScale.Percent125,
                highContrast: true,
                reducedMotion: true);
            settingsView.Render(accessible);
            float scaledOnce = title.fontSize;
            settingsView.Render(accessible);

            Assert.That(scaledOnce, Is.EqualTo(baseFontSize * 1.25f).Within(0.01f));
            Assert.That(title.fontSize, Is.EqualTo(scaledOnce).Within(0.01f));
            Assert.That(backdrop.color, Is.Not.EqualTo(baseBackdrop));
            Assert.That(language.targetGraphic.color, Is.Not.EqualTo(baseButtonColor));
            Assert.That(language.transition, Is.EqualTo(Selectable.Transition.None));
            Assert.That(mainTitle.fontSize, Is.EqualTo(baseMainTitleSize * 1.25f).Within(0.01f));
            Assert.That(startGame.targetGraphic.color, Is.Not.EqualTo(baseStartButtonColor));
            Assert.That(startGame.transition, Is.EqualTo(Selectable.Transition.None));

            settingsView.Render(CreateSettingsModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false));

            Assert.That(title.fontSize, Is.EqualTo(baseFontSize).Within(0.01f));
            Assert.That(backdrop.color, Is.EqualTo(baseBackdrop));
            Assert.That(language.targetGraphic.color, Is.EqualTo(baseButtonColor));
            Assert.That(language.transition, Is.EqualTo(baseTransition));
            Assert.That(mainTitle.fontSize, Is.EqualTo(baseMainTitleSize).Within(0.01f));
            Assert.That(startGame.targetGraphic.color, Is.EqualTo(baseStartButtonColor));
            Assert.That(startGame.transition, Is.EqualTo(baseStartTransition));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>高对比往返不得用首次建树颜色覆盖 Hero 选择与地图状态的最新运行时语义色。</summary>
    [Test]
    public void HighContrastRoundTrip_RestoresLatestHeroAndMapSemanticColors()
    {
        var root = new GameObject("RunEntrySemanticColorRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var settingsView = (IAppSettingsView)view;
            settingsView.Render(CreateSettingsModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false));

            view.Render(CreateRunEntryModel(
                RunEntryPage.HeroSelection,
                selectedHeroTemplateId: 1001));
            Button warrior = view.GetButtonForTesting("Hero1001Button");
            Button machineGunner = view.GetButtonForTesting("Hero1002Button");
            Assert.That(
                warrior.targetGraphic.color,
                Is.EqualTo((Color)new Color32(75, 145, 205, 255)));

            var lockedMap = CreateAccessibilityMap(RunMapNodePresentationState.Locked);
            view.Render(CreateRunEntryModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                map: lockedMap));
            Button selectableNode = view.GetButtonForTesting("MapNode_L01-S00_Button");
            Assert.That(
                selectableNode.targetGraphic.color,
                Is.EqualTo((Color)new Color32(55, 61, 72, 255)));

            settingsView.Render(CreateSettingsModel(
                AppTextScale.Percent125,
                highContrast: true,
                reducedMotion: true));
            var selectableMap = CreateAccessibilityMap(
                RunMapNodePresentationState.Selectable);
            view.Render(CreateRunEntryModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1002,
                map: selectableMap));
            TMP_Text selectableNodeLabel = FindText(
                view.GetPageForTesting(RunEntryPage.Map),
                "MapNode_L01-S00_ButtonLabel");
            Color highContrastButton = new Color32(12, 16, 22, 255);
            Assert.That(machineGunner.targetGraphic.color, Is.EqualTo(highContrastButton));
            Assert.That(selectableNode.targetGraphic.color, Is.EqualTo(highContrastButton));
            Assert.That(
                selectableNodeLabel.color,
                Is.EqualTo((Color)new Color32(255, 246, 140, 255)));

            settingsView.Render(CreateSettingsModel(
                AppTextScale.Percent125,
                highContrast: false,
                reducedMotion: true));

            Assert.That(
                warrior.targetGraphic.color,
                Is.EqualTo((Color)new Color32(47, 62, 86, 255)));
            Assert.That(
                machineGunner.targetGraphic.color,
                Is.EqualTo((Color)new Color32(75, 145, 205, 255)));
            Assert.That(
                selectableNode.targetGraphic.color,
                Is.EqualTo((Color)new Color32(204, 146, 62, 255)));
            Assert.That(
                selectableNodeLabel.color,
                Is.EqualTo((Color)new Color32(235, 242, 250, 255)));
            Assert.That(selectableNode.transition, Is.EqualTo(Selectable.Transition.None));
            TMP_Text mapTitle = FindText(
                view.GetPageForTesting(RunEntryPage.Map),
                "MapTitle");
            Assert.That(mapTitle.fontSize, Is.EqualTo(52.5f).Within(0.01f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>重建地图后再应用设置不得访问已经销毁的旧节点文字或按钮。</summary>
    [Test]
    public void MapRebuild_WhenAccessibilitySettingsAreApplied_DoesNotUseDestroyedControls()
    {
        var root = new GameObject("RunEntryMapAccessibilityCacheRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            view.Render(CreateRunEntryModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                map: CreateAccessibilityMap(
                    RunMapNodePresentationState.Selectable,
                    "g8-accessibility-map-first")));
            Button firstButton = view.GetButtonForTesting("MapNode_L01-S00_Button");
            TMP_Text firstLabel = FindText(
                view.GetPageForTesting(RunEntryPage.Map),
                "MapNode_L01-S00_ButtonLabel");

            view.Render(CreateRunEntryModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                map: CreateAccessibilityMap(
                    RunMapNodePresentationState.Selectable,
                    "g8-accessibility-map-second")));

            Assert.That(firstButton == null, Is.True, "first map button was not destroyed");
            Assert.That(firstLabel == null, Is.True, "first map label was not destroyed");
            Assert.DoesNotThrow(() => ((IAppSettingsView)view).Render(CreateSettingsModel(
                AppTextScale.Percent125,
                highContrast: true,
                reducedMotion: true)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>旧地图必须在销毁前先脱离 RunEntry 层级，避免同帧可访问性重扫再次缓存。</summary>
    [Test]
    public void MapRebuild_DetachesRetiringMapBeforeDestroy()
    {
        var root = new GameObject("RunEntryMapRetirementHierarchyRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            view.Render(CreateRunEntryModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                map: CreateAccessibilityMap(
                    RunMapNodePresentationState.Selectable,
                    "g8-retirement-hierarchy-map-first")));
            Transform firstMapRoot = view.GetPageForTesting(RunEntryPage.Map)
                .GetComponentsInChildren<Transform>(includeInactive: true)
                .Single(transform => transform.name == "FrozenActMap");
            firstMapRoot.gameObject.AddComponent<RunEntryMapRetirementProbe>();
            RunEntryMapRetirementProbe.ResetObservation();

            view.Render(CreateRunEntryModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                map: CreateAccessibilityMap(
                    RunMapNodePresentationState.Selectable,
                    "g8-retirement-hierarchy-map-second")));

            Assert.That(
                RunEntryMapRetirementProbe.DetachedWhileActive,
                Is.True,
                "retiring map stayed under RunEntry until destruction");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>125% 高对比设置后动态创建的紧凑地图节点必须把名称与身份文字限制在各自区域内。</summary>
    [Test]
    public void CompactMapNode_At125PercentHighContrast_DoesNotOverflowItsTextRegions()
    {
        var root = new GameObject("RunEntryCompactMapAccessibilityRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            ((IAppSettingsView)view).Render(CreateSettingsModel(
                AppTextScale.Percent125,
                highContrast: true,
                reducedMotion: false));
            var compactMap = new RunMapViewModel(
                "g8-compact-accessibility-map",
                new[]
                {
                    new RunMapNodeViewModel(
                        "L06-S00",
                        layer: 6,
                        slot: 0,
                        MapNodeKind.Combat,
                        contentId: 5001,
                        displayName: "SLIME PATROL",
                        RunMapVisualAnchorKind.EncounterSlimeSilhouette,
                        RunMapNodePresentationState.Selectable,
                        downstreamNodeIds: Array.Empty<string>(),
                        downstreamEdgeKeys: Array.Empty<string>()),
                },
                Array.Empty<RunMapEdgeViewModel>());

            view.Render(CreateRunEntryModel(
                RunEntryPage.Map,
                selectedHeroTemplateId: 1001,
                map: compactMap));
            GameObject page = view.GetPageForTesting(RunEntryPage.Map);
            TMP_Text label = FindText(page, "MapNode_L06-S00_ButtonLabel");
            TMP_Text identity = FindText(page, "MapNode_L06-S00_IdentityId");
            label.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
            identity.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);

            Assert.That(label.isTextOverflowing, Is.False, "node label overflowed");
            Assert.That(
                label.GetRenderedValues().y,
                Is.LessThanOrEqualTo(label.rectTransform.rect.height + 0.01f),
                "node label glyphs escaped their region");
            Assert.That(identity.isTextOverflowing, Is.False, "node identity overflowed");
            Assert.That(
                identity.GetRenderedValues().y,
                Is.LessThanOrEqualTo(identity.rectTransform.rect.height + 0.01f),
                "node identity glyphs escaped their region");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>RunEntry 场景 Scope 必须把同一 View 注册给两个 seam，并启动设置 Presenter。</summary>
    [Test]
    public void RunEntryScope_RegistersAllViewSeamsAndPresenters()
    {
        var root = new GameObject("RunEntrySettingsScopeRoot");
        try
        {
            RunEntryLifetimeScope scope = root.AddComponent<RunEntryLifetimeScope>();
            root.AddComponent<RunEntryView>();
            var builder = new ContainerBuilder
            {
                ApplicationOrigin = scope,
            };
            MethodInfo configure = typeof(RunEntryLifetimeScope).GetMethod(
                "Configure",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(configure, Is.Not.Null);
            configure.Invoke(scope, new object[] { builder });

            Assert.That(builder.Exists(typeof(IRunEntryView), true), Is.True);
            Assert.That(builder.Exists(typeof(IAppSettingsView), true), Is.True);
            Assert.That(builder.Exists(typeof(IRunStatisticsView), true), Is.True);
            Assert.That(builder.Exists(typeof(AppSettingsPresenter), true), Is.True);
            Assert.That(builder.Exists(typeof(RunStatisticsPresenter), true), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>动态 Statistics 页面必须公开只读 seam 与真实统计控件，不再保留 coming-soon 占位。</summary>
    [Test]
    public void BuildStatisticsPage_ExposesStatisticsViewAndRealControls()
    {
        var root = new GameObject("RunEntryStatisticsViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            GameObject page = view.GetPageForTesting(RunEntryPage.Statistics);
            string[] names = page.GetComponentsInChildren<Transform>(true)
                .Select(item => item.name)
                .ToArray();

            Assert.That(view, Is.AssignableTo<IRunStatisticsView>());
            Assert.That(names, Does.Contain("StatisticsTitle"));
            Assert.That(names, Does.Contain("StatisticsTotalRuns"));
            Assert.That(names, Does.Contain("StatisticsVictoryRate"));
            Assert.That(names, Does.Contain("StatisticsState"));
            Assert.That(names, Does.Not.Contain("StatisticsPlaceholder"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>正常 Statistics 投影必须显示全局 V/D/A、胜率，并按 HeroTemplateId 稳定排列英雄行。</summary>
    [Test]
    public void StatisticsRender_ProjectsGlobalTotalsAndStableHeroRows()
    {
        var root = new GameObject("RunEntryStatisticsViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var statistics = new RunHistoryStatistics(
                totalRuns: 3,
                victoryCount: 1,
                defeatCount: 1,
                abandonedCount: 1,
                heroes: new[]
                {
                    new RunHistoryHeroStatistics(1002, 1, 0, 0, 1),
                    new RunHistoryHeroStatistics(1001, 2, 1, 1, 0),
                });
            var rows = new[]
            {
                new StatisticsHeroRowViewModel(
                    1002, "Machine Gunner", 1, 0, 0, 1, 0d, "0%"),
                new StatisticsHeroRowViewModel(
                    1001, "Warrior", 2, 1, 1, 0, 0.5d, "50%"),
            };

            ((IRunStatisticsView)view).Render(StatisticsViewModel.Ready(
                statistics,
                rows,
                CreateStatisticsTexts(),
                "33.3%"));

            GameObject page = view.GetPageForTesting(RunEntryPage.Statistics);
            AssertText(page, "StatisticsTitle", "Statistics");
            AssertText(page, "StatisticsTotalRuns", "Runs: 3");
            AssertText(page, "StatisticsVictory", "Wins: 1");
            AssertText(page, "StatisticsDefeat", "Defeats: 1");
            AssertText(page, "StatisticsAbandoned", "Abandoned: 1");
            AssertText(page, "StatisticsVictoryRate", "Win Rate: 33.3%");
            TMP_Text warrior = FindText(page, "StatisticsHero_1001");
            TMP_Text gunner = FindText(page, "StatisticsHero_1002");
            Assert.That(warrior.text, Does.StartWith("Warrior"));
            Assert.That(gunner.text, Does.StartWith("Machine Gunner"));
            Assert.That(
                warrior.rectTransform.anchoredPosition.y,
                Is.GreaterThan(gunner.rectTransform.anchoredPosition.y));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>空历史必须明确提示；加载失败必须清空旧数值与 Hero 行而不伪装成零局。</summary>
    [Test]
    public void StatisticsRender_DistinguishesEmptyHistoryFromLoadFailure()
    {
        var root = new GameObject("RunEntryStatisticsViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var statisticsView = (IRunStatisticsView)view;
            var oneRun = new RunHistoryStatistics(
                1,
                1,
                0,
                0,
                new[] { new RunHistoryHeroStatistics(1001, 1, 1, 0, 0) });
            statisticsView.Render(StatisticsViewModel.Ready(
                oneRun,
                new[]
                {
                    new StatisticsHeroRowViewModel(
                        1001, "Warrior", 1, 1, 0, 0, 1d, "100%"),
                },
                CreateStatisticsTexts(),
                "100%"));
            GameObject page = view.GetPageForTesting(RunEntryPage.Statistics);
            Assert.That(FindText(page, "StatisticsHero_1001").gameObject.activeSelf, Is.True);

            var empty = new RunHistoryStatistics(
                0,
                0,
                0,
                0,
                Array.Empty<RunHistoryHeroStatistics>());
            statisticsView.Render(StatisticsViewModel.Ready(
                empty,
                Array.Empty<StatisticsHeroRowViewModel>(),
                CreateStatisticsTexts(),
                "0%"));

            AssertText(page, "StatisticsTotalRuns", "Runs: 0");
            AssertText(page, "StatisticsState", "No completed runs yet.");
            Assert.That(FindText(page, "StatisticsHero_1001").gameObject.activeSelf, Is.False);

            statisticsView.Render(StatisticsViewModel.Unavailable(
                CreateStatisticsTexts(),
                "History could not be loaded."));

            AssertText(page, "StatisticsTotalRuns", string.Empty);
            AssertText(page, "StatisticsVictoryRate", string.Empty);
            AssertText(page, "StatisticsState", "History could not be loaded.");
            Assert.That(FindText(page, "StatisticsHero_1001").gameObject.activeSelf, Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>设置生效后才创建的 Hero 统计行也必须先冻结基线，再应用并可恢复全局可访问性。</summary>
    [Test]
    public void DynamicStatisticsRow_InheritsCurrentAccessibilityWithoutAccumulation()
    {
        var root = new GameObject("RunEntryDynamicAccessibilityRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            ((IAppSettingsView)view).Render(CreateSettingsModel(
                AppTextScale.Percent125,
                highContrast: true,
                reducedMotion: true));
            var oneRun = new RunHistoryStatistics(
                1,
                1,
                0,
                0,
                new[] { new RunHistoryHeroStatistics(1001, 1, 1, 0, 0) });
            ((IRunStatisticsView)view).Render(StatisticsViewModel.Ready(
                oneRun,
                new[]
                {
                    new StatisticsHeroRowViewModel(
                        1001, "Warrior", 1, 1, 0, 0, 1d, "100%"),
                },
                CreateStatisticsTexts(),
                "100%"));
            TMP_Text hero = FindText(
                view.GetPageForTesting(RunEntryPage.Statistics),
                "StatisticsHero_1001");

            Assert.That(hero.fontSize, Is.EqualTo(23.75f).Within(0.01f));
            Assert.That(hero.color, Is.EqualTo((Color)new Color32(255, 246, 140, 255)));

            ((IAppSettingsView)view).Render(CreateSettingsModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: false));

            Assert.That(hero.fontSize, Is.EqualTo(19f).Within(0.01f));
            Assert.That(hero.color, Is.EqualTo((Color)new Color32(235, 242, 250, 255)));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>减少动态在首次主菜单 Render 前生效时，纸叠必须直接稳定且完全不创建入口 Tween。</summary>
    [Test]
    public void ReducedMotionBeforeFirstRunRender_SettlesPaperStackWithoutPlayingEntrance()
    {
        var root = new GameObject("RunEntryReducedMotionRoot");
        root.SetActive(false);
        var backgroundTexture = new Texture2D(16, 9, TextureFormat.RGBA32, false);
        var paperTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        Sprite background = Sprite.Create(
            backgroundTexture,
            new Rect(0f, 0f, 16f, 9f),
            new Vector2(0.5f, 0.5f));
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.ConfigureVisualAssetsForTesting(background, paperTexture);
            root.SetActive(true);
            view.BuildForTesting();
            ((IAppSettingsView)view).Render(CreateSettingsModel(
                AppTextScale.Percent100,
                highContrast: false,
                reducedMotion: true));
            EntryPaperStackView stack = view.GetPaperStackForTesting();

            view.Render(CreateRunEntryModel(RunEntryPage.MainMenu));

            Assert.That(stack.PlayCountForTesting, Is.Zero);
            Assert.That(stack.ActiveSequenceForTesting, Is.Null);
            Assert.That(view.GetButtonForTesting("StartGameButton").IsInteractable(), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(background);
            UnityEngine.Object.DestroyImmediate(backgroundTexture);
            UnityEngine.Object.DestroyImmediate(paperTexture);
        }
    }

    /// <summary>代表页面首次切换后必须把键盘焦点放到该页第一个可见可交互按钮。</summary>
    [TestCase(RunEntryPage.MainMenu, "StartGameButton")]
    [TestCase(RunEntryPage.Settings, "LanguageButton")]
    [TestCase(RunEntryPage.Statistics, "StatisticsBackButton")]
    [TestCase(RunEntryPage.HeroSelection, "Hero1001Button")]
    [TestCase(RunEntryPage.Map, "MapAbandonRunButton")]
    [TestCase(RunEntryPage.Failure, "LeaveTerminalRunButton")]
    public void PageChange_FocusesFirstActiveInteractableButton(
        RunEntryPage page,
        string expectedButtonName)
    {
        var root = new GameObject("RunEntryKeyboardViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();

            view.Render(CreateRunEntryModel(page));

            EventSystem eventSystem = root.GetComponentInChildren<EventSystem>(true);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Not.Null);
            Assert.That(eventSystem.currentSelectedGameObject.name, Is.EqualTo(expectedButtonName));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>同一页面重复 Render 必须保留玩家当前焦点，不抢回首按钮。</summary>
    [Test]
    public void SamePageRender_PreservesCurrentKeyboardFocus()
    {
        var root = new GameObject("RunEntryKeyboardViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            RunEntryViewModel model = CreateRunEntryModel(RunEntryPage.Settings);
            view.Render(model);
            EventSystem eventSystem = root.GetComponentInChildren<EventSystem>(true);
            GameObject playerSelection = view.GetButtonForTesting("DisplayModeButton").gameObject;
            eventSystem.SetSelectedGameObject(playerSelection);

            view.Render(model);

            Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(playerSelection));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>同页动态控件重建令焦点失效后，必须恢复到首个可交互按钮。</summary>
    [Test]
    public void SamePageRender_WhenSelectionIsMissing_RestoresKeyboardFocus()
    {
        var root = new GameObject("RunEntryKeyboardRepairViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            RunEntryViewModel model = CreateRunEntryModel(RunEntryPage.Settings);
            view.Render(model);
            EventSystem eventSystem = root.GetComponentInChildren<EventSystem>(true);
            eventSystem.SetSelectedGameObject(null);

            view.Render(model);

            Assert.That(eventSystem.currentSelectedGameObject, Is.Not.Null);
            Assert.That(eventSystem.currentSelectedGameObject.name, Is.EqualTo("LanguageButton"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>标准 Cancel 输入只发布既有 Back 意图，不泄漏到 AppSettings 动作 seam。</summary>
    [Test]
    public void CancelInput_EmitsOnlyRunEntryBackAction()
    {
        var root = new GameObject("RunEntryKeyboardViewRoot");
        try
        {
            var view = root.AddComponent<RunEntryView>();
            view.BuildForTesting();
            var runActions = new List<RunEntryAction>();
            var settingsActions = new List<AppSettingsAction>();
            view.ActionRequested += runActions.Add;
            ((IAppSettingsView)view).ActionRequested += settingsActions.Add;
            EventSystem eventSystem = root.GetComponentInChildren<EventSystem>(true);
            var eventData = new BaseEventData(eventSystem);

            ((ICancelHandler)view).OnCancel(eventData);

            Assert.That(runActions, Has.Count.EqualTo(1));
            Assert.That(runActions[0].Kind, Is.EqualTo(RunEntryActionKind.Back));
            Assert.That(settingsActions, Is.Empty);
            Assert.That(eventData.used, Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>建立 Statistics 测试使用的完整稳定文案集合。</summary>
    private static IReadOnlyDictionary<RunStatisticsTextSlot, string> CreateStatisticsTexts()
    {
        return new Dictionary<RunStatisticsTextSlot, string>
        {
            [RunStatisticsTextSlot.Title] = "Statistics",
            [RunStatisticsTextSlot.TotalRunsLabel] = "Runs",
            [RunStatisticsTextSlot.VictoryLabel] = "Wins",
            [RunStatisticsTextSlot.DefeatLabel] = "Defeats",
            [RunStatisticsTextSlot.AbandonedLabel] = "Abandoned",
            [RunStatisticsTextSlot.VictoryRateLabel] = "Win Rate",
            [RunStatisticsTextSlot.EmptyHistory] = "No completed runs yet.",
        };
    }

    /// <summary>在指定页面按稳定对象名读取唯一 TMP 文本。</summary>
    private static TMP_Text FindText(GameObject page, string objectName)
    {
        return page.GetComponentsInChildren<TMP_Text>(true)
            .Single(text => text.name == objectName);
    }

    /// <summary>断言指定稳定对象显示完整预期文本。</summary>
    private static void AssertText(GameObject page, string objectName, string expected)
    {
        Assert.That(FindText(page, objectName).text, Is.EqualTo(expected));
    }

    /// <summary>建立焦点测试使用的完整 RunEntry 页面投影。</summary>
    private static RunEntryViewModel CreateRunEntryModel(RunEntryPage page)
    {
        return CreateRunEntryModel(
            page,
            page == RunEntryPage.HeroSelection ? 1001 : (int?)null,
            map: null);
    }

    /// <summary>建立可指定 Hero 与地图语义状态的完整 RunEntry 页面投影。</summary>
    private static RunEntryViewModel CreateRunEntryModel(
        RunEntryPage page,
        int? selectedHeroTemplateId,
        RunMapViewModel map = null)
    {
        var texts = Enum.GetValues(typeof(RunEntryTextSlot))
            .Cast<RunEntryTextSlot>()
            .ToDictionary(slot => slot, slot => $"text:{slot}");
        return new RunEntryViewModel(
            page,
            texts,
            selectedHeroTemplateId,
            confirmEnabled: selectedHeroTemplateId.HasValue,
            map,
            continueEnabled: false,
            canRollbackFailedSave: true,
            canAbandonActiveRun: page == RunEntryPage.Map);
    }

    /// <summary>建立单一可选战斗节点，独立提供地图状态色的已知语义样本。</summary>
    private static RunMapViewModel CreateAccessibilityMap(
        RunMapNodePresentationState state,
        string fingerprint = "g8-accessibility-map")
    {
        return new RunMapViewModel(
            fingerprint,
            new[]
            {
                new RunMapNodeViewModel(
                    "L01-S00",
                    layer: 1,
                    slot: 0,
                    MapNodeKind.Combat,
                    contentId: 5001,
                    displayName: "SLIME PATROL",
                    RunMapVisualAnchorKind.EncounterSlimeSilhouette,
                    state,
                    downstreamNodeIds: Array.Empty<string>(),
                    downstreamEdgeKeys: Array.Empty<string>()),
            },
            Array.Empty<RunMapEdgeViewModel>());
    }

    /// <summary>建立含全部可见文本的一份不可变设置页面模型。</summary>
    private static AppSettingsViewModel CreateSettingsModel(
        AppTextScale textScale,
        bool highContrast,
        bool reducedMotion,
        AppSettingsViewStatus status = AppSettingsViewStatus.Ready,
        string failureText = "")
    {
        var texts = Enum.GetValues(typeof(AppSettingsTextSlot))
            .Cast<AppSettingsTextSlot>()
            .ToDictionary(slot => slot, slot => $"text:{slot}");
        texts[AppSettingsTextSlot.Title] = "Settings";
        texts[AppSettingsTextSlot.LanguageValue] = "English";
        texts[AppSettingsTextSlot.MasterVolumeValue] = "80%";
        texts[AppSettingsTextSlot.DisplayModeValue] = "Windowed";
        texts[AppSettingsTextSlot.ResolutionValue] = "1920x1080";
        texts[AppSettingsTextSlot.TextScaleValue] = $"{(int)textScale}%";
        texts[AppSettingsTextSlot.HighContrastValue] = highContrast ? "On" : "Off";
        texts[AppSettingsTextSlot.ReducedMotionValue] = reducedMotion ? "On" : "Off";

        return new AppSettingsViewModel(
            new AppSettingsSnapshot(
                AppSettingsSnapshot.EnglishLocaleCode,
                masterVolumePercent: 80,
                AppDisplayMode.Windowed,
                new AppResolution(1920, 1080),
                textScale,
                highContrast,
                reducedMotion),
            status,
            texts,
            failureText);
    }
}

/// <summary>记录动态地图在停用/销毁前是否先离开原 UI 层级。</summary>
[ExecuteAlways]
internal sealed class RunEntryMapRetirementProbe : MonoBehaviour
{
    /// <summary>本轮观察是否捕获到仍激活时的脱离事件。</summary>
    internal static bool DetachedWhileActive { get; private set; }

    /// <summary>开始新一轮确定性层级生命周期观察。</summary>
    internal static void ResetObservation()
    {
        DetachedWhileActive = false;
    }

    /// <summary>只记录明确先脱离再停用的顺序，不把销毁过程中的隐式变化冒充成功。</summary>
    private void OnTransformParentChanged()
    {
        if (gameObject.activeSelf && transform.parent == null)
            DetachedWhileActive = true;
    }
}
