using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TinySpire.Battle;
using TinySpire.UI.Battle;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UI;

public sealed class ParticipantHudCombatFeedbackTests
{
    private const string PrefabPath = "Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab";

    /// <summary>确认纯字符数字只在 Tween 开始后显示，清理移除 transient 且不写入 Block 事实。</summary>
    [UnityTest]
    public IEnumerator CreateCombatFeedbackTween_BlockGainedIsHiddenUntilPlaybackAndCleansWithoutMutation()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject worldView = null;
        ParticipantHudView hudView = null;
        Sequence timeline = null;
        object timelineId = null;
        BattleCommandPresentationTween lease = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 1001,
                maxHealth: 30,
                strength: 0);
            canvasObject = new GameObject(
                "ParticipantHudCombatFeedbackCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "ParticipantHudCombatFeedbackWorld",
                typeof(SpriteRenderer));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);
            Transform feedbackAnchor = hudView.transform.Find("FeedbackAnchor");
            var cue = new BattleCombatFeedbackCue(
                player.Id,
                BattleCommandPresentationStepKind.BlockGainedNumber,
                amount: 5);

            lease = hudView.CreateCombatFeedbackTween(cue);

            Assert.That(feedbackAnchor.childCount, Is.EqualTo(1));
            GameObject transient = feedbackAnchor.GetChild(0).gameObject;
            Assert.That(transient.activeSelf, Is.False);
            Assert.That(transient.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(player.CurrentBlock, Is.Zero);

            timeline = CreateManualTimeline(out timelineId)
                .Append(lease.Tween);
            timeline.Play();
            timeline.ManualUpdate(0.01f, 0.01f);

            Assert.That(transient.activeSelf, Is.True);
            Assert.That(transient.GetComponent<Text>().text, Is.EqualTo("+5"));
            Assert.That(transient.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f));
            Assert.That(player.CurrentBlock, Is.Zero);

            KillManualTimeline(timelineId);
            timelineId = null;
            timeline = null;
            lease.Cleanup();
            lease = null;

            Assert.That(feedbackAnchor.childCount, Is.Zero);
            Assert.That(player.CurrentBlock, Is.Zero);
        }
        finally
        {
            KillManualTimeline(timelineId);
            lease?.Cleanup();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>验证数字 cue 同步构建失败时立即销毁已实例化 transient，不把 orphan 留在 HUD。</summary>
    [UnityTest]
    public IEnumerator CreateCombatFeedbackTween_InvalidNumberAmountThrowsWithoutLeavingTransient()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject worldView = null;
        ParticipantHudView hudView = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(1001, 30, 0);
            canvasObject = new GameObject(
                "ParticipantHudInvalidNumberCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "ParticipantHudInvalidNumberWorld",
                typeof(SpriteRenderer));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);
            Transform feedbackAnchor = hudView.transform.Find("FeedbackAnchor");
            var invalidCue = new BattleCombatFeedbackCue(
                player.Id,
                BattleCommandPresentationStepKind.HealthLossNumber,
                amount: 0);

            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => hudView.CreateCombatFeedbackTween(invalidCue));

            Assert.That(feedbackAnchor.childCount, Is.Zero);
            Assert.That(player.CurrentHealth, Is.EqualTo(30));
        }
        finally
        {
            LocalizationSettings.SelectedLocale = previousLocale;
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>验证受击 Tween 只改变世界 View 临时姿态，清理恢复 base pose 且不写生命事实。</summary>
    [UnityTest]
    public IEnumerator CreateCombatFeedbackTween_HitShakeMovesAndRestoresWorldWithoutMutation()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject worldView = null;
        ParticipantHudView hudView = null;
        Sequence timeline = null;
        object timelineId = null;
        BattleCommandPresentationTween lease = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 1001,
                maxHealth: 30,
                strength: 0);
            canvasObject = new GameObject(
                "ParticipantHudHitShakeCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "ParticipantHudHitShakeWorld",
                typeof(SpriteRenderer));
            worldView.transform.localPosition = new Vector3(3f, 4f, 0f);
            Vector3 basePosition = worldView.transform.localPosition;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);
            var cue = new BattleCombatFeedbackCue(
                player.Id,
                BattleCommandPresentationStepKind.HitShake,
                amount: 0);

            lease = hudView.CreateCombatFeedbackTween(cue);
            bool observedMovement = false;
            lease.Tween.OnUpdate(() =>
            {
                if (worldView.transform.localPosition != basePosition)
                    observedMovement = true;
            });
            timeline = CreateManualTimeline(out timelineId)
                .Append(lease.Tween);
            timeline.Play();
            timeline.ManualUpdate(0.15f, 0.15f);

            Assert.That(observedMovement, Is.True);
            Assert.That(player.CurrentHealth, Is.EqualTo(30));

            KillManualTimeline(timelineId);
            timelineId = null;
            timeline = null;
            lease.Cleanup();
            lease = null;

            Assert.That(worldView.transform.localPosition, Is.EqualTo(basePosition));
            Assert.That(player.CurrentHealth, Is.EqualTo(30));
        }
        finally
        {
            KillManualTimeline(timelineId);
            lease?.Cleanup();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>确认力量、易伤与意图只脉冲精确现有 HUD 节点并恢复当前事实。</summary>
    [UnityTest]
    public IEnumerator CreateCombatFeedbackTween_StatusAndIntentPulseExactRootsWithoutMutation()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        BattleEnemyIntentsData enemyIntents = null;
        GameObject canvasObject = null;
        GameObject worldView = null;
        ParticipantHudView hudView = null;
        try
        {
            EnemyCombatantData enemy = combatants.AddEnemy(
                templateId: 2001,
                maxHealth: 20,
                strength: 2);
            BattleEffectStateTestDriver.Execute(
                combatants,
                enemy.Id,
                enemy.Id,
                cfg.battle.EffectType.ApplyVulnerable,
                cfg.battle.Attribute.None,
                configuredValue: 2);
            cfg.Tables tables = CreateEnemyIntentTables();
            enemyIntents = new BattleEnemyIntentsData(
                combatants,
                new[] { enemy.Id },
                tables,
                battleSeed: 3579);
            EnemyIntentLayoutData layoutBefore = enemyIntents.Layout.CurrentValue;
            Assert.That(
                layoutBefore.TryGetBehaviorId(enemy.Id, out int currentBehaviorId),
                Is.True);
            Assert.That(currentBehaviorId, Is.EqualTo(7001));
            uint randomBefore = enemyIntents.RandomState;
            canvasObject = new GameObject(
                "ParticipantHudPulseCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "ParticipantHudPulseWorld",
                typeof(SpriteRenderer));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            hudView.Bind(
                enemy,
                "battle.enemy.test_slime.name",
                worldView.transform,
                canvas,
                localization,
                tables,
                enemyIntents);
            Transform strengthRoot = hudView.transform.Find("VitalsAnchor/StatusRow/Strength");
            Transform vulnerableRoot = hudView.transform.Find("VitalsAnchor/StatusRow/Vulnerable");
            Transform intentRoot = hudView.transform.Find("NameAnchor/IntentRoot");
            Transform statusRoot = hudView.transform.Find("VitalsAnchor/StatusRow");
            Text strengthText = strengthRoot.GetComponentInChildren<Text>();
            Text vulnerableText = vulnerableRoot.GetComponentInChildren<Text>();
            Text intentText = intentRoot.Find("IntentValueText").GetComponent<Text>();

            AssertPulse(
                hudView,
                new BattleCombatFeedbackCue(
                    enemy.Id,
                    BattleCommandPresentationStepKind.StrengthIconPulse,
                    amount: 0,
                    frozenValue: 1),
                strengthRoot,
                () => Assert.That(strengthText.text, Is.EqualTo("1")),
                () => Assert.That(strengthText.text, Is.EqualTo("2")));
            AssertPulse(
                hudView,
                new BattleCombatFeedbackCue(
                    enemy.Id,
                    BattleCommandPresentationStepKind.VulnerableIconPulse,
                    amount: 0,
                    frozenValue: 1),
                vulnerableRoot,
                () => Assert.That(vulnerableText.text, Is.EqualTo("1")),
                () => Assert.That(vulnerableText.text, Is.EqualTo("2")));
            AssertPulse(
                hudView,
                new BattleCombatFeedbackCue(
                    enemy.Id,
                    BattleCommandPresentationStepKind.EnemyIntentPulse,
                    amount: 0,
                    frozenValue: 7002),
                intentRoot,
                () => Assert.That(intentText.text, Is.EqualTo("11")),
                () => Assert.That(intentText.text, Is.EqualTo("8")));

            Assert.That(strengthRoot.gameObject.activeSelf, Is.True);
            Assert.That(vulnerableRoot.gameObject.activeSelf, Is.True);
            Assert.That(intentRoot.gameObject.activeSelf, Is.True);
            Assert.That(enemy.CurrentStrength, Is.EqualTo(2));
            Assert.That(enemy.CurrentVulnerable, Is.EqualTo(2));
            Assert.That(enemyIntents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(enemyIntents.RandomState, Is.EqualTo(randomBefore));

            BattleEffectStateTestDriver.Execute(
                combatants,
                enemy.Id,
                enemy.Id,
                cfg.battle.EffectType.ModifyAttribute,
                cfg.battle.Attribute.Strength,
                configuredValue: -2);
            var statusTiming = new BattleStatusTiming(combatants);
            statusTiming.Execute(
                BattleStatusTimingPoint.EnemyActionCompleted,
                enemy.Id,
                startingOrder: 0);
            statusTiming.Execute(
                BattleStatusTimingPoint.EnemyActionCompleted,
                enemy.Id,
                startingOrder: 0);
            Assert.That(strengthRoot.gameObject.activeSelf, Is.False);
            Assert.That(vulnerableRoot.gameObject.activeSelf, Is.False);

            AssertPulse(
                hudView,
                new BattleCombatFeedbackCue(
                    enemy.Id,
                    BattleCommandPresentationStepKind.StrengthIconPulse,
                    amount: 0,
                    frozenValue: 0),
                strengthRoot,
                () =>
                {
                    Assert.That(statusRoot.gameObject.activeSelf, Is.True);
                    Assert.That(strengthRoot.gameObject.activeSelf, Is.True);
                    Assert.That(strengthText.text, Is.EqualTo("0"));
                },
                () =>
                {
                    Assert.That(statusRoot.gameObject.activeSelf, Is.False);
                    Assert.That(strengthRoot.gameObject.activeSelf, Is.False);
                });
            AssertPulse(
                hudView,
                new BattleCombatFeedbackCue(
                    enemy.Id,
                    BattleCommandPresentationStepKind.VulnerableIconPulse,
                    amount: 0,
                    frozenValue: 0),
                vulnerableRoot,
                () =>
                {
                    Assert.That(statusRoot.gameObject.activeSelf, Is.True);
                    Assert.That(vulnerableRoot.gameObject.activeSelf, Is.True);
                    Assert.That(vulnerableText.text, Is.EqualTo("0"));
                },
                () =>
                {
                    Assert.That(statusRoot.gameObject.activeSelf, Is.False);
                    Assert.That(vulnerableRoot.gameObject.activeSelf, Is.False);
                });

            Assert.That(strengthRoot.gameObject.activeSelf, Is.False);
            Assert.That(vulnerableRoot.gameObject.activeSelf, Is.False);
            Assert.That(enemy.CurrentStrength, Is.Zero);
            Assert.That(enemy.CurrentVulnerable, Is.Zero);
            Assert.That(enemyIntents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(enemyIntents.RandomState, Is.EqualTo(randomBefore));
        }
        finally
        {
            LocalizationSettings.SelectedLocale = previousLocale;
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            enemyIntents?.Dispose();
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>确认 fatal 只在死亡过渡结束后隐藏世界 View 与完整 HUD，且不删除权威参与者。</summary>
    [UnityTest]
    public IEnumerator CreateCombatFeedbackTween_FatalHidesOnlyAfterTransitionAndPreservesAuthority()
    {
        var localization = new LocalizationService();
        yield return localization.InitializeAsync().ToCoroutine();
        Locale previousLocale = LocalizationSettings.SelectedLocale;
        Assert.That(localization.SetLocale("en"), Is.True);

        var combatants = new BattleCombatantsData();
        GameObject canvasObject = null;
        GameObject worldView = null;
        ParticipantHudView hudView = null;
        Sequence timeline = null;
        object timelineId = null;
        BattleCommandPresentationTween lease = null;
        try
        {
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 1001,
                maxHealth: 30,
                strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(
                templateId: 2001,
                maxHealth: 20,
                strength: 0);
            canvasObject = new GameObject(
                "ParticipantHudFatalCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject("ParticipantHudFatalWorld");
            var spriteObject = new GameObject(
                "ParticipantHudFatalSprite",
                typeof(SpriteRenderer));
            spriteObject.transform.SetParent(worldView.transform, worldPositionStays: false);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, player.Id);
            Assert.That(player.IsAlive, Is.False);
            Assert.That(worldView.activeSelf, Is.True);
            Assert.That(hudView.gameObject.activeSelf, Is.True);
            var cue = new BattleCombatFeedbackCue(
                player.Id,
                BattleCommandPresentationStepKind.DeathTransition,
                amount: 0);

            lease = hudView.CreateCombatFeedbackTween(cue);
            timeline = CreateManualTimeline(out timelineId)
                .Append(lease.Tween);
            timeline.Play();
            timeline.ManualUpdate(0.15f, 0.15f);

            Assert.That(worldView.activeSelf, Is.True);
            Assert.That(hudView.gameObject.activeSelf, Is.True);

            timeline.Complete(withCallbacks: true);

            Assert.That(worldView.activeSelf, Is.False);
            Assert.That(hudView.gameObject.activeSelf, Is.False);
            Assert.That(combatants.All[player.Id], Is.SameAs(player));

            KillManualTimeline(timelineId);
            timelineId = null;
            timeline = null;
            lease.Cleanup();
            lease = null;

            Assert.That(worldView.activeSelf, Is.False);
            Assert.That(hudView.gameObject.activeSelf, Is.False);
            Assert.That(combatants.All[player.Id], Is.SameAs(player));

            Object.DestroyImmediate(hudView.gameObject);
            GameObject prefabAfterDeath = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            hudView = Object.Instantiate(
                prefabAfterDeath,
                canvas.transform).GetComponent<ParticipantHudView>();
            hudView.Bind(
                player,
                "battle.keyword.strength.name",
                worldView.transform,
                canvas,
                localization,
                new cfg.Tables(_ => new JArray()),
                enemyIntents: null);

            Assert.That(worldView.activeSelf, Is.False);
            Assert.That(hudView.gameObject.activeSelf, Is.False);
            Assert.That(combatants.All[player.Id], Is.SameAs(player));
        }
        finally
        {
            KillManualTimeline(timelineId);
            lease?.Cleanup();
            LocalizationSettings.SelectedLocale = previousLocale;
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            combatants.Dispose();
            localization.Dispose();
        }
    }

    /// <summary>以 runner 等价的手动父时间线验证单个 HUD 脉冲并执行 lease 清理。</summary>
    private static void AssertPulse(
        ParticipantHudView view,
        BattleCombatFeedbackCue cue,
        Transform pulseRoot,
        System.Action assertDuringPlayback = null,
        System.Action assertAfterPlayback = null)
    {
        Vector3 baseScale = pulseRoot.localScale;
        BattleCommandPresentationTween lease = view.CreateCombatFeedbackTween(cue);
        bool observedPulse = false;
        lease.Tween.OnUpdate(() =>
        {
            if (pulseRoot.localScale != baseScale)
                observedPulse = true;
        });
        Sequence timeline = CreateManualTimeline(out object timelineId)
            .Append(lease.Tween);
        try
        {
            timeline.Play();
            timeline.ManualUpdate(0.12f, 0.12f);
            Assert.That(observedPulse, Is.True, cue.Kind.ToString());
            assertDuringPlayback?.Invoke();
            timeline.Complete(withCallbacks: true);
            assertAfterPlayback?.Invoke();
        }
        finally
        {
            KillManualTimeline(timelineId);
            lease.Cleanup();
        }

        Assert.That(pulseRoot.localScale, Is.EqualTo(baseScale));
    }

    /// <summary>创建带唯一私有标识的 runner 等价手动父时间线。</summary>
    private static Sequence CreateManualTimeline(out object timelineId)
    {
        timelineId = new object();
        return DOTween.Sequence()
            .SetId(timelineId)
            .SetAutoKill(false)
            .SetUpdate(UpdateType.Manual)
            .Pause();
    }

    /// <summary>按测试时间线私有标识同步注销父 Sequence 及其嵌套 cue。</summary>
    private static void KillManualTimeline(object timelineId)
    {
        if (timelineId != null)
            DOTween.Kill(timelineId, complete: false);
    }

    /// <summary>创建含单一攻击意图与共享伤害效果的最小完整 Luban 表集合。</summary>
    private static cfg.Tables CreateEnemyIntentTables()
    {
        var data = new Dictionary<string, JArray>
        {
            ["battle_tbhero"] = new JArray(),
            ["battle_tbenemy"] = JArray.Parse(
                "[{\"id\":2001,\"name_i18n_key\":\"battle.enemy.test_slime.name\"," +
                "\"max_health\":20,\"base_strength\":0,\"view_prefab_address\":\"\"," +
                "\"behavior_group_id\":6001}]"),
            ["battle_tbdeck"] = new JArray(),
            ["battle_tbcard"] = new JArray(),
            ["battle_tbcardeffect"] = JArray.Parse(
                "[{\"id\":4002,\"effect_type\":1,\"attribute\":0,\"value\":6}," +
                "{\"id\":4003,\"effect_type\":1,\"attribute\":0,\"value\":9}]"),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1," +
                "\"effect_id\":4002,\"weight\":1,\"cooldown_selections\":0," +
                "\"max_consecutive\":0}," +
                "{\"id\":7002,\"intent_type\":0,\"target_rule\":1," +
                "\"effect_id\":4003,\"weight\":1,\"cooldown_selections\":0," +
                "\"max_consecutive\":0}]")
        };
        return new cfg.Tables(tableName => data[tableName]);
    }
}
