using System.Reflection;
using NUnit.Framework;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleTurnHudM9FPrefabContractTests
{
    private const string PrefabPath = "Assets/Prefabs/UI/Battle/BattleTurnHud.prefab";

    /// <summary>确认 M9F 三类流程反馈节点、全屏指针策略、共享横幅与终局按钮均已正式序列化。</summary>
    [Test]
    public void Prefab_ContainsSerializedStartTurnAndTerminalFlowFeedback()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        BattleTurnHudView view = prefab.GetComponent<BattleTurnHudView>();
        Assert.That(view, Is.Not.Null);

        Transform startOverlay = prefab.transform.Find("BattleStartOverlay");
        Transform turnBanner = prefab.transform.Find("TurnBanner");
        Transform outcomePanel = prefab.transform.Find("BattleOutcomePanel");
        Assert.That(startOverlay, Is.Not.Null);
        Assert.That(turnBanner, Is.Not.Null);
        Assert.That(outcomePanel, Is.Not.Null);
        Assert.That(startOverlay.gameObject.activeSelf, Is.False);
        Assert.That(turnBanner.gameObject.activeSelf, Is.False);
        Assert.That(outcomePanel.gameObject.activeSelf, Is.False);
        AssertFullScreenStretch(startOverlay.GetComponent<RectTransform>());
        AssertFullScreenStretch(outcomePanel.GetComponent<RectTransform>());

        CanvasGroup startGroup = GetPrivateField<CanvasGroup>(view, "_battleStartOverlay");
        Text startText = GetPrivateField<Text>(view, "_battleStartText");
        CanvasGroup turnGroup = GetPrivateField<CanvasGroup>(view, "_turnBannerGroup");
        Image sharedBannerImage = GetPrivateField<Image>(view, "_playerTurnBanner");
        Text turnText = GetPrivateField<Text>(view, "_turnBannerText");
        CanvasGroup outcomeGroup = GetPrivateField<CanvasGroup>(view, "_battleOutcomePanel");
        Text outcomeText = GetPrivateField<Text>(view, "_battleOutcomeText");
        Button restartButton = GetPrivateField<Button>(view, "_restartButton");
        Text restartText = GetPrivateField<Text>(view, "_restartButtonText");
        Button exitButton = GetPrivateField<Button>(view, "_exitButton");
        Text exitText = GetPrivateField<Text>(view, "_exitButtonText");

        Assert.That(startGroup.transform, Is.SameAs(startOverlay));
        Assert.That(startText.transform.IsChildOf(startOverlay), Is.True);
        Assert.That(startOverlay.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(turnGroup.transform, Is.SameAs(turnBanner));
        Assert.That(sharedBannerImage.transform, Is.SameAs(turnBanner));
        Assert.That(turnText.transform.IsChildOf(turnBanner), Is.True);
        Assert.That(sharedBannerImage.raycastTarget, Is.False);
        Assert.That(turnText.raycastTarget, Is.False);
        Assert.That(outcomeGroup.transform, Is.SameAs(outcomePanel));
        Assert.That(outcomeText.transform.IsChildOf(outcomePanel), Is.True);
        Assert.That(outcomePanel.GetComponent<Image>().raycastTarget, Is.True);
        AssertTerminalButton(outcomePanel, restartButton, restartText, "RestartButton");
        AssertTerminalButton(outcomePanel, exitButton, exitText, "ExitButton");

        Color playerTint = GetPrivateField<Color>(view, "_playerTurnBannerColor");
        Color enemyTint = GetPrivateField<Color>(view, "_enemyTurnBannerColor");
        Assert.That(enemyTint, Is.Not.EqualTo(playerTint));
    }

    /// <summary>确认面板使用四边拉伸且不携带屏幕尺寸偏移。</summary>
    private static void AssertFullScreenStretch(RectTransform rect)
    {
        Assert.That(rect, Is.Not.Null);
        Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
        Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
        Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
        Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
    }

    /// <summary>确认终局按钮自身完整接收指针，而标签不会截断真实按钮命中。</summary>
    private static void AssertTerminalButton(
        Transform outcomePanel,
        Button button,
        Text label,
        string expectedName)
    {
        Assert.That(button, Is.Not.Null);
        Assert.That(button.name, Is.EqualTo(expectedName));
        Assert.That(button.transform.IsChildOf(outcomePanel), Is.True);
        Assert.That(button.GetComponent<Image>(), Is.Not.Null);
        Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
        Assert.That(label, Is.Not.Null);
        Assert.That(label.transform.IsChildOf(button.transform), Is.True);
        Assert.That(label.raycastTarget, Is.False);
    }

    /// <summary>读取 Prefab 组件上的计划内私有序列化引用。</summary>
    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Missing field {fieldName}.");
        T value = (T)field.GetValue(target);
        Assert.That(value, Is.Not.Null, $"Prefab field {fieldName} is not assigned.");
        return value;
    }
}
