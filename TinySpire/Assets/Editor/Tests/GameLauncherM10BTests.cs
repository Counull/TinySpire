using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public sealed class GameLauncherM10BTests
{
    /// <summary>确认配置初始化发生 typed failure 时，启动编排展示失败而不继续本地化或场景加载。</summary>
    [TestCase(ConfigInitializationFailureReason.AssetLoadFailed)]
    [TestCase(ConfigInitializationFailureReason.InvalidJson)]
    [TestCase(ConfigInitializationFailureReason.InvalidGameConfigShape)]
    [TestCase(ConfigInitializationFailureReason.MissingRequiredGameConfigField)]
    [TestCase(ConfigInitializationFailureReason.UnsupportedTableShape)]
    [TestCase(ConfigInitializationFailureReason.InvalidTableRowShape)]
    [TestCase(ConfigInitializationFailureReason.TableConstructionFailed)]
    public void ConfigurationFailure_PresentsTypedFailureAndDoesNotAdvanceStartup(
        ConfigInitializationFailureReason reason)
    {
        var steps = new List<string>();
        ConfigInitializationException expectedFailure = new ConfigInitializationException(
            "Assets/GameData/game-config.json",
            null,
            reason);
        ConfigInitializationException observedFailure = null;

        GameLauncher.RunStartupAsync(
                () => RecordAsync(steps, "assets"),
                () => RecordAsync(steps, "audio"),
                () => UniTask.FromException(expectedFailure),
                () => RecordAsync(steps, "localization"),
                () => steps.Add("settings"),
                () => steps.Add("profile"),
                () => steps.Add("tutorial-view"),
                () => steps.Add("tutorial-presenter"),
                () => steps.Add("history"),
                () => RecordAsync(steps, "scene"),
                failure =>
                {
                    observedFailure = failure;
                    steps.Add("failure");
                })
            .GetAwaiter()
            .GetResult();

        CollectionAssert.AreEqual(new[] { "assets", "audio", "failure" }, steps);
        Assert.That(observedFailure, Is.SameAs(expectedFailure));
    }

    /// <summary>确认未知启动异常不被配置失败路由吞掉，也不伪装成可恢复的 Bootstrap 提示。</summary>
    [Test]
    public void UnexpectedFailure_PropagatesWithoutPresentingConfigurationFailure()
    {
        var steps = new List<string>();
        bool presentedFailure = false;

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() =>
            GameLauncher.RunStartupAsync(
                    () => RecordAsync(steps, "assets"),
                    () => RecordAsync(steps, "audio"),
                    () => UniTask.FromException(new InvalidOperationException("unexpected")),
                    () => RecordAsync(steps, "localization"),
                    () => steps.Add("settings"),
                    () => steps.Add("profile"),
                    () => steps.Add("tutorial-view"),
                    () => steps.Add("tutorial-presenter"),
                    () => steps.Add("history"),
                    () => RecordAsync(steps, "scene"),
                    _ => presentedFailure = true)
                .GetAwaiter()
                .GetResult());

        Assert.That(thrown.Message, Is.EqualTo("unexpected"));
        CollectionAssert.AreEqual(new[] { "assets", "audio" }, steps);
        Assert.That(presentedFailure, Is.False);
    }

    /// <summary>确认正常内容保留原有的资源、配置、本地化和首场景顺序。</summary>
    [Test]
    public void SuccessfulStartup_ContinuesToInitialScene()
    {
        var steps = new List<string>();

        GameLauncher.RunStartupAsync(
                () => RecordAsync(steps, "assets"),
                () => RecordAsync(steps, "audio"),
                () => RecordAsync(steps, "configuration"),
                () => RecordAsync(steps, "localization"),
                () => steps.Add("settings"),
                () => steps.Add("profile"),
                () => steps.Add("tutorial-view"),
                () => steps.Add("tutorial-presenter"),
                () => steps.Add("history"),
                () => RecordAsync(steps, "scene"),
                _ => steps.Add("failure"))
            .GetAwaiter()
            .GetResult();

        CollectionAssert.AreEqual(
            new[]
            {
                "assets", "audio", "configuration", "localization", "settings", "profile",
                "tutorial-view", "tutorial-presenter", "history", "scene",
            },
            steps);
    }

    /// <summary>确认应用设置在本地化就绪后、首场景加载前同步应用。</summary>
    [Test]
    public void SuccessfulStartup_AppliesSettingsBeforeInitialScene()
    {
        var steps = new List<string>();

        GameLauncher.RunStartupAsync(
                () => RecordAsync(steps, "assets"),
                () => RecordAsync(steps, "audio"),
                () => RecordAsync(steps, "configuration"),
                () => RecordAsync(steps, "localization"),
                () => steps.Add("settings"),
                () => steps.Add("profile"),
                () => steps.Add("tutorial-view"),
                () => steps.Add("tutorial-presenter"),
                () => steps.Add("history"),
                () => RecordAsync(steps, "scene"),
                _ => steps.Add("failure"))
            .GetAwaiter()
            .GetResult();

        CollectionAssert.AreEqual(
            new[]
            {
                "assets", "audio", "configuration", "localization", "settings", "profile",
                "tutorial-view", "tutorial-presenter", "history", "scene",
            },
            steps);
    }

    /// <summary>确认独立 Player Profile 在设置应用后、首场景加载前同步恢复。</summary>
    [Test]
    public void SuccessfulStartup_InitializesPlayerProfileBeforeInitialScene()
    {
        var steps = new List<string>();

        GameLauncher.RunStartupAsync(
                () => RecordAsync(steps, "assets"),
                () => RecordAsync(steps, "audio"),
                () => RecordAsync(steps, "configuration"),
                () => RecordAsync(steps, "localization"),
                () => steps.Add("settings"),
                () => steps.Add("profile"),
                () => steps.Add("tutorial-view"),
                () => steps.Add("tutorial-presenter"),
                () => steps.Add("history"),
                () => RecordAsync(steps, "scene"),
                _ => steps.Add("failure"))
            .GetAwaiter()
            .GetResult();

        CollectionAssert.AreEqual(
            new[]
            {
                "assets", "audio", "configuration", "localization", "settings", "profile",
                "tutorial-view", "tutorial-presenter", "history", "scene",
            },
            steps);
    }

    /// <summary>确认全局教程 View 与 Presenter 在 Profile 就绪后、历史和场景之前依次初始化。</summary>
    [Test]
    public void SuccessfulStartup_InitializesTutorialOverlayAfterProfileBeforeHistoryAndScene()
    {
        var steps = new List<string>();

        GameLauncher.RunStartupAsync(
                () => RecordAsync(steps, "assets"),
                () => RecordAsync(steps, "audio"),
                () => RecordAsync(steps, "configuration"),
                () => RecordAsync(steps, "localization"),
                () => steps.Add("settings"),
                () => steps.Add("profile"),
                () => steps.Add("tutorial-view"),
                () => steps.Add("tutorial-presenter"),
                () => steps.Add("history"),
                () => RecordAsync(steps, "scene"),
                _ => steps.Add("failure"))
            .GetAwaiter()
            .GetResult();

        CollectionAssert.AreEqual(
            new[]
            {
                "assets", "audio", "configuration", "localization", "settings", "profile",
                "tutorial-view", "tutorial-presenter", "history", "scene",
            },
            steps);
    }

    /// <summary>确认 Run 历史观察者在首场景可能恢复终局前完成订阅。</summary>
    [Test]
    public void SuccessfulStartup_InitializesRunHistoryBeforeInitialScene()
    {
        var steps = new List<string>();

        GameLauncher.RunStartupAsync(
                () => RecordAsync(steps, "assets"),
                () => RecordAsync(steps, "audio"),
                () => RecordAsync(steps, "configuration"),
                () => RecordAsync(steps, "localization"),
                () => steps.Add("settings"),
                () => steps.Add("profile"),
                () => steps.Add("tutorial-view"),
                () => steps.Add("tutorial-presenter"),
                () => steps.Add("history"),
                () => RecordAsync(steps, "scene"),
                _ => steps.Add("failure"))
            .GetAwaiter()
            .GetResult();

        CollectionAssert.AreEqual(
            new[]
            {
                "assets", "audio", "configuration", "localization", "settings", "profile",
                "tutorial-view", "tutorial-presenter", "history", "scene",
            },
            steps);
    }

    /// <summary>确认最小失败视图保留稳定失败码、资源地址和重新启动指引。</summary>
    [Test]
    public void BootstrapFailureView_StoresStableFailureDetailsForDisplay()
    {
        var viewObject = new GameObject("M10B Bootstrap Failure View Test");
        try
        {
            BootstrapFailureView view = viewObject.AddComponent<BootstrapFailureView>();
            view.ShowConfigurationFailure(new ConfigInitializationException(
                "Assets/GameData/game-config.json",
                null,
                ConfigInitializationFailureReason.InvalidJson));

            FieldInfo visibleField = typeof(BootstrapFailureView).GetField(
                "_isVisible",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo messageField = typeof(BootstrapFailureView).GetField(
                "_message",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(visibleField, Is.Not.Null);
            Assert.That(messageField, Is.Not.Null);
            Assert.That((bool)visibleField.GetValue(view), Is.True);
            string message = (string)messageField.GetValue(view);
            StringAssert.Contains("CFG-002", message);
            StringAssert.Contains("Assets/GameData/game-config.json", message);
            StringAssert.Contains("restart the application", message);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(viewObject);
        }
    }

    /// <summary>为启动编排测试记录一个已完成的异步步骤。</summary>
    private static UniTask RecordAsync(ICollection<string> steps, string step)
    {
        steps.Add(step);
        return UniTask.CompletedTask;
    }
}
