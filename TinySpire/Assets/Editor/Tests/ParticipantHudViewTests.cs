using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
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

public sealed class ParticipantHudViewTests
{
    private const string PrefabPath = "Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab";

    /// <summary>验证临时头顶布局将生命与状态置于角色头顶，并令名称与生命 HUD 保持明确纵向间距。</summary>
    [Test]
    public void LateUpdate_ProjectsVitalsAboveHeadAndNameAboveVitals()
    {
        GameObject canvasObject = null;
        GameObject cameraObject = null;
        GameObject worldView = null;
        ParticipantHudView hudView = null;
        Sprite sprite = null;
        try
        {
            canvasObject = new GameObject(
                "ParticipantHudHeadAnchorTestCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            cameraObject = new GameObject("ParticipantHudHeadAnchorTestCamera", typeof(Camera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;

            worldView = new GameObject(
                "ParticipantHudHeadAnchorTestWorld",
                typeof(SpriteRenderer));
            SpriteRenderer spriteRenderer = worldView.GetComponent<SpriteRenderer>();
            sprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);
            spriteRenderer.sprite = sprite;
            worldView.transform.position = new Vector3(1f, 2f, 0f);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            hudView = Object.Instantiate(prefab, canvas.transform).GetComponent<ParticipantHudView>();
            SetPrivateField(hudView, "_spriteRenderer", spriteRenderer);
            SetPrivateField(hudView, "_canvas", canvas);

            typeof(ParticipantHudView)
                .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hudView, null);

            var serializedView = new SerializedObject(hudView);
            float headOffset = serializedView.FindProperty("_headOffset").floatValue;
            float nameAboveVitalsOffset = serializedView
                .FindProperty("_nameAboveVitalsOffset")
                .floatValue;
            Bounds bounds = spriteRenderer.bounds;
            var vitalsWorldPoint = new Vector3(
                bounds.center.x,
                bounds.max.y + headOffset,
                bounds.center.z);
            var nameWorldPoint = vitalsWorldPoint + Vector3.up * nameAboveVitalsOffset;
            RectTransform vitalsAnchor = hudView.transform.Find("VitalsAnchor") as RectTransform;
            RectTransform nameAnchor = hudView.transform.Find("NameAnchor") as RectTransform;
            RectTransform healthBar = vitalsAnchor.Find("HealthBar") as RectTransform;
            RectTransform nameText = nameAnchor.Find("NameText") as RectTransform;

            AssertAnchorMatchesWorldPoint(vitalsAnchor, canvas, vitalsWorldPoint);
            AssertAnchorMatchesWorldPoint(nameAnchor, canvas, nameWorldPoint);
            float healthTop = vitalsAnchor.anchoredPosition.y
                + healthBar.anchoredPosition.y
                + healthBar.rect.height * 0.5f;
            float nameBottom = nameAnchor.anchoredPosition.y
                + nameText.anchoredPosition.y
                - nameText.rect.height * 0.5f;
            Assert.That(nameBottom, Is.GreaterThan(healthTop));
        }
        finally
        {
            if (hudView != null)
                Object.DestroyImmediate(hudView.gameObject);
            if (worldView != null)
                Object.DestroyImmediate(worldView);
            if (cameraObject != null)
                Object.DestroyImmediate(cameraObject);
            if (canvasObject != null)
                Object.DestroyImmediate(canvasObject);
            if (sprite != null)
                Object.DestroyImmediate(sprite);
        }
    }

    /// <summary>确认公开 Bind 响应当前状态事实，新发生死亡在 M9C 反馈完成前保留生命和世界 View。</summary>
    [UnityTest]
    public IEnumerator Bind_ReprojectsStatusAndKeepsNewDeathVisibleUntilPresentation()
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
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 1001,
                maxHealth: 30,
                strength: 2);
            EnemyCombatantData enemy = combatants.AddEnemy(
                templateId: 2001,
                maxHealth: 20,
                strength: 0);
            ApplyStatusFacts(combatants, player, block: 5, vulnerable: 2);

            canvasObject = new GameObject(
                "ParticipantHudViewTestCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "ParticipantHudViewTestWorld",
                typeof(SpriteRenderer));
            cfg.Tables tables = new cfg.Tables(_ => new JArray());

            hudView = CreateBoundView(player, worldView, canvas, localization, tables);
            AssertStatus(hudView, block: "5", strength: "2", vulnerable: "2");

            var timing = new BattleStatusTiming(combatants);
            timing.Execute(BattleStatusTimingPoint.PlayerRoundStart, player.Id, startingOrder: 0);
            Assert.That(FindStatusRoot(hudView, "Block").activeSelf, Is.False);

            BattleEffectStateTestDriver.Execute(
                combatants,
                player.Id,
                player.Id,
                cfg.battle.EffectType.ModifyAttribute,
                cfg.battle.Attribute.Strength,
                configuredValue: -2);
            Assert.That(FindStatusRoot(hudView, "Strength").activeSelf, Is.False);

            timing.Execute(BattleStatusTimingPoint.PlayerActionEnded, player.Id, startingOrder: 0);
            Assert.That(FindStatusText(hudView, "Vulnerable").text, Is.EqualTo("1"));
            timing.Execute(BattleStatusTimingPoint.PlayerActionEnded, player.Id, startingOrder: 0);
            Assert.That(FindStatusRoot(hudView, "Vulnerable").activeSelf, Is.False);

            BattleEffectStateTestDriver.Execute(
                combatants,
                player.Id,
                player.Id,
                cfg.battle.EffectType.ModifyAttribute,
                cfg.battle.Attribute.Strength,
                configuredValue: 2);
            ApplyStatusFacts(combatants, player, block: 5, vulnerable: 2);
            BattleEffectStateTestDriver.ApplyDamage(
                combatants,
                enemy.Id,
                player.Id,
                configuredValue: player.CurrentHealth + player.CurrentBlock);

            AssertPendingDeathPresentation(hudView, worldView, expectedHealth: "0 / 30");
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

    /// <summary>确认首次绑定已死亡参与者时直接恢复 M9C 隐藏终态，不删除权威参与者。</summary>
    [UnityTest]
    public IEnumerator Bind_AlreadyDeadParticipantRestoresHiddenDeathEndState()
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
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 1001,
                maxHealth: 30,
                strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(
                templateId: 2001,
                maxHealth: 20,
                strength: 0);
            BattleEffectStateTestDriver.Kill(combatants, enemy.Id, player.Id);

            canvasObject = new GameObject(
                "ParticipantHudAlreadyDeadTestCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject("ParticipantHudAlreadyDeadTestWorld");
            var spriteObject = new GameObject(
                "ParticipantHudAlreadyDeadTestSprite",
                typeof(SpriteRenderer));
            spriteObject.transform.SetParent(worldView.transform, worldPositionStays: false);
            worldView.SetActive(false);
            cfg.Tables tables = new cfg.Tables(_ => new JArray());
            hudView = CreateBoundView(player, worldView, canvas, localization, tables);

            Assert.That(hudView.gameObject.activeSelf, Is.False);
            Assert.That(worldView.activeSelf, Is.False);
            Assert.That(
                hudView.transform.Find("VitalsAnchor/HealthText").GetComponent<Text>().text,
                Is.EqualTo("0 / 30"));
            Assert.That(combatants.All[player.Id], Is.SameAs(player));
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

    /// <summary>确认敌人新死亡保留反馈窗口，而已死亡重建直接恢复隐藏终态并保留权威参与者。</summary>
    [UnityTest]
    public IEnumerator Bind_EnemyNewDeathKeepsFeedbackVisibleAndDeadRebuildRestoresHiddenEndState()
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
            PlayerCombatantData player = combatants.AddPlayer(
                templateId: 1001,
                maxHealth: 30,
                strength: 0);
            EnemyCombatantData enemy = combatants.AddEnemy(
                templateId: 2001,
                maxHealth: 20,
                strength: 2);
            cfg.Tables tables = CreateEnemyIntentTables();
            enemyIntents = new BattleEnemyIntentsData(
                combatants,
                new[] { enemy.Id },
                tables,
                battleSeed: 1234);
            EnemyIntentLayoutData layoutBefore = enemyIntents.Layout.CurrentValue;
            uint randomBefore = enemyIntents.RandomState;
            Assert.That(layoutBefore.TryGetBehaviorId(enemy.Id, out int behaviorId), Is.True);

            canvasObject = new GameObject(
                "ParticipantHudEnemyTestCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "ParticipantHudEnemyTestWorld",
                typeof(SpriteRenderer));
            hudView = CreateBoundEnemyView(
                enemy,
                worldView,
                canvas,
                localization,
                tables,
                enemyIntents);

            Assert.That(
                hudView.transform.Find("NameAnchor/IntentRoot").gameObject.activeSelf,
                Is.True);
            Assert.That(
                hudView.transform.Find("NameAnchor/IntentRoot/IntentValueText")
                    .GetComponent<Text>().text,
                Is.EqualTo("8"));
            Assert.That(
                hudView.transform.Find("VitalsAnchor/HealthText").GetComponent<Text>().text,
                Is.EqualTo("20 / 20"));

            BattleEffectStateTestDriver.Kill(combatants, player.Id, enemy.Id);

            AssertPendingDeathPresentation(hudView, worldView, expectedHealth: "0 / 20");
            Assert.That(combatants.All[enemy.Id], Is.SameAs(enemy));
            Assert.That(enemyIntents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(enemyIntents.RandomState, Is.EqualTo(randomBefore));
            Assert.That(
                enemyIntents.Layout.CurrentValue.TryGetBehaviorId(
                    enemy.Id,
                    out int behaviorAfterDeath),
                Is.True);
            Assert.That(behaviorAfterDeath, Is.EqualTo(behaviorId));

            Object.DestroyImmediate(hudView.gameObject);
            hudView = CreateBoundEnemyView(
                enemy,
                worldView,
                canvas,
                localization,
                tables,
                enemyIntents);

            AssertHiddenDeathEndState(hudView, worldView, expectedHealth: "0 / 20");
            Assert.That(combatants.All[enemy.Id], Is.SameAs(enemy));
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

    /// <summary>确认 Bind 后语言切换会从当前事实重投影名称与状态，且不推进或替换敌人意图。</summary>
    [UnityTest]
    public IEnumerator Bind_LocaleChangedReprojectsNameAndStatusFromCurrentFactsWithoutAdvancingIntent()
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
            cfg.Tables tables = CreateEnemyIntentTables();
            enemyIntents = new BattleEnemyIntentsData(
                combatants,
                new[] { enemy.Id },
                tables,
                battleSeed: 2468);
            EnemyIntentLayoutData layoutBefore = enemyIntents.Layout.CurrentValue;
            uint randomBefore = enemyIntents.RandomState;

            canvasObject = new GameObject(
                "ParticipantHudLocaleTestCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            worldView = new GameObject(
                "ParticipantHudLocaleTestWorld",
                typeof(SpriteRenderer));
            hudView = CreateBoundEnemyView(
                enemy,
                worldView,
                canvas,
                localization,
                tables,
                enemyIntents);
            Text nameText = hudView.transform.Find("NameAnchor/NameText").GetComponent<Text>();
            Text strengthText = FindStatusText(hudView, "Strength");

            Assert.That(nameText.text, Is.EqualTo("Warden"));
            Assert.That(strengthText.text, Is.EqualTo("2"));
            nameText.text = "stale";
            strengthText.text = "stale";

            Assert.That(localization.SetLocale("zh-CN"), Is.True);

            Assert.That(nameText.text, Is.EqualTo("典狱长"));
            Assert.That(strengthText.text, Is.EqualTo("2"));
            Assert.That(
                hudView.transform.Find("NameAnchor/IntentRoot").gameObject.activeSelf,
                Is.True);
            Assert.That(
                hudView.transform.Find("NameAnchor/IntentRoot/IntentValueText")
                    .GetComponent<Text>().text,
                Is.EqualTo("8"));
            Assert.That(enemyIntents.Layout.CurrentValue, Is.SameAs(layoutBefore));
            Assert.That(enemyIntents.RandomState, Is.EqualTo(randomBefore));
            Assert.That(enemy.CurrentStrength, Is.EqualTo(2));
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

    /// <summary>通过公共 Effect seam 建立当前 Block 与 Vulnerable 事实。</summary>
    private static void ApplyStatusFacts(
        BattleCombatantsData combatants,
        CombatantData target,
        int block,
        int vulnerable)
    {
        BattleEffectStateTestDriver.Execute(
            combatants,
            target.Id,
            target.Id,
            cfg.battle.EffectType.GainBlock,
            cfg.battle.Attribute.None,
            block);
        BattleEffectStateTestDriver.Execute(
            combatants,
            target.Id,
            target.Id,
            cfg.battle.EffectType.ApplyVulnerable,
            cfg.battle.Attribute.None,
            vulnerable);
    }

    /// <summary>从正式 Prefab 建立一个只绑定当前玩家事实的 HUD View。</summary>
    private static ParticipantHudView CreateBoundView(
        PlayerCombatantData player,
        GameObject worldView,
        Canvas canvas,
        LocalizationService localization,
        cfg.Tables tables)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = Object.Instantiate(prefab, canvas.transform);
        ParticipantHudView view = instance.GetComponent<ParticipantHudView>();
        view.Bind(
            player,
            "battle.keyword.strength.name",
            worldView.transform,
            canvas,
            localization,
            tables,
            enemyIntents: null);
        return view;
    }

    /// <summary>从正式 Prefab 建立一个绑定真实当前意图与状态事实的敌人 HUD View。</summary>
    private static ParticipantHudView CreateBoundEnemyView(
        EnemyCombatantData enemy,
        GameObject worldView,
        Canvas canvas,
        LocalizationService localization,
        cfg.Tables tables,
        BattleEnemyIntentsData enemyIntents)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject instance = Object.Instantiate(prefab, canvas.transform);
        ParticipantHudView view = instance.GetComponent<ParticipantHudView>();
        view.Bind(
            enemy,
            "battle.enemy.test_slime.name",
            worldView.transform,
            canvas,
            localization,
            tables,
            enemyIntents);
        return view;
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
                "[{\"id\":4002,\"effect_type\":1,\"attribute\":0,\"value\":6}]"),
            ["battle_tbencounter"] = new JArray(),
            ["battle_tbenemybehaviorgroup"] = JArray.Parse(
                "[{\"id\":6001,\"behavior_ids\":[7001]}]"),
            ["battle_tbenemybehavior"] = JArray.Parse(
                "[{\"id\":7001,\"intent_type\":0,\"target_rule\":1," +
                "\"effect_id\":4002,\"weight\":1,\"cooldown_selections\":0," +
                "\"max_consecutive\":0}]")
        };
        return new cfg.Tables(tableName => data[tableName]);
    }

    /// <summary>验证三个状态槽均显示传入的当前层数。</summary>
    private static void AssertStatus(
        ParticipantHudView view,
        string block,
        string strength,
        string vulnerable)
    {
        Assert.That(
            view.transform.Find("VitalsAnchor/StatusRow").gameObject.activeSelf,
            Is.True);
        Assert.That(FindStatusRoot(view, "Block").activeSelf, Is.True);
        Assert.That(FindStatusRoot(view, "Strength").activeSelf, Is.True);
        Assert.That(FindStatusRoot(view, "Vulnerable").activeSelf, Is.True);
        Assert.That(FindStatusText(view, "Block").text, Is.EqualTo(block));
        Assert.That(FindStatusText(view, "Strength").text, Is.EqualTo(strength));
        Assert.That(FindStatusText(view, "Vulnerable").text, Is.EqualTo(vulnerable));
    }

    /// <summary>验证死亡状态行和意图均隐藏，但生命 HUD 与世界 View 仍保持活动。</summary>
    private static void AssertPendingDeathPresentation(
        ParticipantHudView view,
        GameObject worldView,
        string expectedHealth)
    {
        Assert.That(view.gameObject.activeSelf, Is.True);
        Assert.That(
            view.transform.Find("VitalsAnchor/StatusRow").gameObject.activeSelf,
            Is.False);
        Assert.That(FindStatusRoot(view, "Block").activeSelf, Is.False);
        Assert.That(FindStatusRoot(view, "Strength").activeSelf, Is.False);
        Assert.That(FindStatusRoot(view, "Vulnerable").activeSelf, Is.False);
        Assert.That(view.transform.Find("NameAnchor/IntentRoot").gameObject.activeSelf, Is.False);
        Assert.That(view.transform.Find("VitalsAnchor").gameObject.activeSelf, Is.True);
        Assert.That(view.transform.Find("VitalsAnchor/HealthBar").gameObject.activeSelf, Is.True);
        Assert.That(
            view.transform.Find("VitalsAnchor/HealthText").GetComponent<Text>().text,
            Is.EqualTo(expectedHealth));
        Assert.That(worldView.activeSelf, Is.True);
    }

    /// <summary>验证死亡重建直接恢复隐藏终态，同时保留可读取的最终生命文本。</summary>
    private static void AssertHiddenDeathEndState(
        ParticipantHudView view,
        GameObject worldView,
        string expectedHealth)
    {
        Assert.That(view.gameObject.activeSelf, Is.False);
        Assert.That(worldView.activeSelf, Is.False);
        Assert.That(
            view.transform.Find("VitalsAnchor/HealthText").GetComponent<Text>().text,
            Is.EqualTo(expectedHealth));
    }

    /// <summary>取得指定状态项的非交互根节点。</summary>
    private static GameObject FindStatusRoot(ParticipantHudView view, string statusName)
    {
        return view.transform.Find($"VitalsAnchor/StatusRow/{statusName}").gameObject;
    }

    /// <summary>取得指定状态项的当前层数文本。</summary>
    private static Text FindStatusText(ParticipantHudView view, string statusName)
    {
        return view.transform
            .Find($"VitalsAnchor/StatusRow/{statusName}/{statusName}Text")
            .GetComponent<Text>();
    }

    /// <summary>为布局测试设置 View 的运行时私有依赖，不绕过生产绑定路径写入任何战斗事实。</summary>
    private static void SetPrivateField(
        ParticipantHudView view,
        string fieldName,
        object value)
    {
        FieldInfo field = typeof(ParticipantHudView).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(view, value);
    }

    /// <summary>验证运行时锚点与给定世界点投影一致，避免只比较局部坐标而遗漏 Canvas 相机转换。</summary>
    private static void AssertAnchorMatchesWorldPoint(
        RectTransform anchor,
        Canvas canvas,
        Vector3 worldPoint)
    {
        Assert.That(anchor, Is.Not.Null);
        Vector3 screenPoint = canvas.worldCamera.WorldToScreenPoint(worldPoint);
        bool projected = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform,
            screenPoint,
            canvas.worldCamera,
            out Vector2 expected);
        Assert.That(projected, Is.True);
        Assert.That(anchor.anchoredPosition.x, Is.EqualTo(expected.x).Within(0.001f));
        Assert.That(anchor.anchoredPosition.y, Is.EqualTo(expected.y).Within(0.001f));
    }
}
