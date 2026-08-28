using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TinySpire.Battle;
using TinySpire.Run.Map;
using UnityEngine;

/// <summary>聚合 G7 Act validator 读取的全部生成表，避免跨表 seam 漂成参数长列。</summary>
internal sealed class RunActContentTables
{
    /// <summary>Encounter 生成表。</summary>
    internal JObject Encounters { get; }

    /// <summary>Enemy 生成表。</summary>
    internal JObject Enemies { get; }

    /// <summary>EnemyBehaviorGroup 生成表。</summary>
    internal JObject BehaviorGroups { get; }

    /// <summary>EnemyBehavior 生成表。</summary>
    internal JObject Behaviors { get; }

    /// <summary>CardEffect 生成表。</summary>
    internal JObject Effects { get; }

    /// <summary>Relic 生成表。</summary>
    internal JObject Relics { get; }

    /// <summary>Potion 生成表。</summary>
    internal JObject Potions { get; }

    /// <summary>Hero 生成表。</summary>
    internal JObject Heroes { get; }

    /// <summary>Deck 生成表。</summary>
    internal JObject Decks { get; }

    /// <summary>Card 生成表。</summary>
    internal JObject Cards { get; }

    /// <summary>冻结同一次校验使用的跨表输入，不复制只读 JSON 树。</summary>
    internal RunActContentTables(
        JObject encounters,
        JObject enemies,
        JObject behaviorGroups,
        JObject behaviors,
        JObject effects,
        JObject relics,
        JObject potions,
        JObject heroes,
        JObject decks,
        JObject cards)
    {
        Encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
        Enemies = enemies ?? throw new ArgumentNullException(nameof(enemies));
        BehaviorGroups = behaviorGroups ?? throw new ArgumentNullException(nameof(behaviorGroups));
        Behaviors = behaviors ?? throw new ArgumentNullException(nameof(behaviors));
        Effects = effects ?? throw new ArgumentNullException(nameof(effects));
        Relics = relics ?? throw new ArgumentNullException(nameof(relics));
        Potions = potions ?? throw new ArgumentNullException(nameof(potions));
        Heroes = heroes ?? throw new ArgumentNullException(nameof(heroes));
        Decks = decks ?? throw new ArgumentNullException(nameof(decks));
        Cards = cards ?? throw new ArgumentNullException(nameof(cards));
    }
}

/// <summary>在本地内容构建前验证 G7 单 Act 的地图、Battle 配置、文本与唯一奖励引用图。</summary>
internal static class RunActContentBuildValidator
{
    private const string EncounterTablePath = "Assets/GameData/battle_tbencounter.json";
    private const string EnemyTablePath = "Assets/GameData/battle_tbenemy.json";
    private const string BehaviorGroupTablePath = "Assets/GameData/battle_tbenemybehaviorgroup.json";
    private const string BehaviorTablePath = "Assets/GameData/battle_tbenemybehavior.json";
    private const string EffectTablePath = "Assets/GameData/battle_tbcardeffect.json";
    private const string RelicTablePath = "Assets/GameData/run_tbrelic.json";
    private const string PotionTablePath = "Assets/GameData/run_tbpotion.json";
    private const string HeroTablePath = "Assets/GameData/battle_tbhero.json";
    private const string DeckTablePath = "Assets/GameData/battle_tbdeck.json";
    private const string CardTablePath = "Assets/GameData/battle_tbcard.json";

    /// <summary>读取当前生成 JSON 与 i18n 作者表，执行生产 G7 内容门禁。</summary>
    internal static void ValidateCurrentProject()
    {
        string unityProjectDirectory = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unable to determine Unity project directory.");
        string workspaceDirectory = Directory.GetParent(unityProjectDirectory)?.FullName
            ?? throw new InvalidOperationException("Unable to determine TinySpire workspace directory.");
        string i18nWorkbookPath = Path.Combine(
            workspaceDirectory,
            "DataTables",
            "Datas",
            "i18n.xlsx");
        IReadOnlyCollection<string> localizationKeys = I18nExcelReader.Read(
                i18nWorkbookPath,
                "i18n",
                new[] { "en", "zh-CN" })
            .Select(entry => entry.Key)
            .ToArray();

        var tables = new RunActContentTables(
            ReadRequiredTable(EncounterTablePath),
            ReadRequiredTable(EnemyTablePath),
            ReadRequiredTable(BehaviorGroupTablePath),
            ReadRequiredTable(BehaviorTablePath),
            ReadRequiredTable(EffectTablePath),
            ReadRequiredTable(RelicTablePath),
            ReadRequiredTable(PotionTablePath),
            ReadRequiredTable(HeroTablePath),
            ReadRequiredTable(DeckTablePath),
            ReadRequiredTable(CardTablePath));
        Validate(
            TinySpireActContentCatalog.NewRunG7V1,
            tables,
            localizationKeys,
            MachineGunnerCardProgramRegistry.PotentiallyCreatedCardTemplateIds);
    }

    /// <summary>通过一条纯校验 seam 检查指定 Act manifest 与全部生成配置输入。</summary>
    internal static void Validate(
        ActContentManifest manifest,
        RunActContentTables tables,
        IReadOnlyCollection<string> localizationKeys,
        IReadOnlyCollection<int> dynamicTemporaryCardTemplateIds)
    {
        if (manifest == null)
            throw new ArgumentNullException(nameof(manifest));
        if (tables == null)
            throw new ArgumentNullException(nameof(tables));
        if (localizationKeys == null)
            throw new ArgumentNullException(nameof(localizationKeys));
        if (dynamicTemporaryCardTemplateIds == null)
            throw new ArgumentNullException(nameof(dynamicTemporaryCardTemplateIds));

        var localizationKeySet = new HashSet<string>(localizationKeys, StringComparer.Ordinal);
        ValidateRequiredLocalizationKeys(manifest, localizationKeySet);
        ValidateSingleRealBossEncounter(manifest);
        if (manifest.NonCombatContents.Any(content => content.UsesHeroCardRewardPool))
        {
            BattleHeroRewardPoolBuildValidator.Validate(
                tables.Heroes,
                tables.Decks,
                tables.Cards,
                dynamicTemporaryCardTemplateIds);
        }

        MapDefinition generated = ActMapGenerator.Generate(manifest.Profile, mapSeed: 1u);
        MapValidationResult validation = ActMapValidator.Validate(generated, manifest.Profile);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                $"Act map is invalid: {validation.Errors[0].Message}");
        }

        ValidateEncounterReferences(
            manifest,
            tables.Encounters,
            tables.Enemies,
            tables.BehaviorGroups,
            tables.Behaviors,
            tables.Effects,
            localizationKeySet);
        ValidateNonCombatReferences(manifest, tables.Relics, tables.Potions, localizationKeySet);
        ValidateUniqueRelicReferences(manifest, tables.Relics, localizationKeySet);
    }

    /// <summary>冻结的多个 Boss 地图身份在 G7 只能指向同一个真实 Boss Encounter。</summary>
    private static void ValidateSingleRealBossEncounter(ActContentManifest manifest)
    {
        var encounterIds = new HashSet<int>(manifest.BossEncounterIds.Values);
        if (encounterIds.Count != 1)
        {
            throw new InvalidOperationException(
                $"Act {manifest.Profile.ProfileId} must reference exactly one real Boss Encounter.");
        }
    }

    /// <summary>要求普通、精英与 Boss 池中的每个 Encounter 都存在且包含敌人。</summary>
    private static void ValidateEncounterReferences(
        ActContentManifest manifest,
        JObject encounters,
        JObject enemies,
        JObject behaviorGroups,
        JObject behaviors,
        JObject effects,
        ISet<string> localizationKeys)
    {
        var encounterIds = new HashSet<int>(manifest.OrdinaryEncounterIds);
        encounterIds.UnionWith(manifest.EliteEncounterIds);
        encounterIds.UnionWith(manifest.BossEncounterIds.Values);
        var bossEncounterIds = new HashSet<int>(manifest.BossEncounterIds.Values);
        foreach (int encounterId in encounterIds)
        {
            bool isBossEncounter = bossEncounterIds.Contains(encounterId);
            JObject encounter = RequireRecord(encounters, encounterId, "battle_tbencounter");
            IReadOnlyList<int> enemyIds = ReadRequiredPositiveIntArray(
                encounter,
                "enemy_template_ids",
                $"Encounter {encounterId}");
            if (isBossEncounter && enemyIds.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Boss Encounter {encounterId} must contain exactly one Enemy.");
            }

            int phaseOneBehaviorGroupId = 0;
            IReadOnlyList<int> phaseOneBehaviorIds = Array.Empty<int>();
            foreach (int enemyId in enemyIds)
            {
                JObject enemy = RequireRecord(enemies, enemyId, "battle_tbenemy");
                string enemyNameKey = ReadRequiredTrimmedString(
                    enemy,
                    "name_i18n_key",
                    $"Enemy {enemyId}");
                RequireLocalizationKey(localizationKeys, enemyNameKey, $"Enemy {enemyId}");
                string viewPrefabKey = ReadRequiredTrimmedString(
                    enemy,
                    "view_prefab_key",
                    $"Enemy {enemyId}");
                ValidateCharacterViewKey(viewPrefabKey, enemyId);
                int behaviorGroupId = ReadRequiredPositiveInt(
                    enemy,
                    "behavior_group_id",
                    $"Enemy {enemyId}");
                JObject behaviorGroup = RequireRecord(
                    behaviorGroups,
                    behaviorGroupId,
                    "battle_tbenemybehaviorgroup");
                IReadOnlyList<int> behaviorIds = ValidateBehaviorGroup(
                    behaviorGroupId,
                    behaviorGroup,
                    behaviors,
                    effects);
                if (isBossEncounter)
                {
                    phaseOneBehaviorGroupId = behaviorGroupId;
                    phaseOneBehaviorIds = behaviorIds;
                }
            }

            ValidatePhaseTwoBehaviorGroup(
                encounterId,
                encounter,
                isBossEncounter,
                phaseOneBehaviorGroupId,
                phaseOneBehaviorIds,
                behaviorGroups,
                behaviors,
                effects);
        }
    }

    /// <summary>要求 manifest 声明的全部地图、阶段与终局文本 key 存在于作者源表。</summary>
    private static void ValidateRequiredLocalizationKeys(
        ActContentManifest manifest,
        ISet<string> localizationKeys)
    {
        foreach (string key in manifest.RequiredLocalizationKeys)
            RequireLocalizationKey(localizationKeys, key, $"Act {manifest.Profile.ProfileId}");
    }

    /// <summary>要求 Act 的每个模板唯一遗物存在，且名称与描述文本均已登记。</summary>
    private static void ValidateUniqueRelicReferences(
        ActContentManifest manifest,
        JObject relics,
        ISet<string> localizationKeys)
    {
        foreach (int relicTemplateId in manifest.UniqueRelicTemplateIds)
            ValidateRelicReference(relicTemplateId, relics, localizationKeys);
    }

    /// <summary>校验 Rest/Chest/Shop/Event anchor 的文本、物品表与奖励池使用形状。</summary>
    private static void ValidateNonCombatReferences(
        ActContentManifest manifest,
        JObject relics,
        JObject potions,
        ISet<string> localizationKeys)
    {
        var referencedRelicIds = new HashSet<int>();
        int heroRewardPoolUsers = 0;
        foreach (ActNonCombatContentReference content in manifest.NonCombatContents)
        {
            foreach (string key in content.RequiredLocalizationKeys)
                RequireLocalizationKey(localizationKeys, key, $"{content.Kind} {content.ContentId}");

            ValidateNonCombatReferenceShape(content);
            if (content.UsesHeroCardRewardPool)
                heroRewardPoolUsers++;
            foreach (int relicTemplateId in content.RelicTemplateIds)
            {
                referencedRelicIds.Add(relicTemplateId);
                ValidateRelicReference(relicTemplateId, relics, localizationKeys);
            }
            foreach (int potionTemplateId in content.PotionTemplateIds)
                ValidatePotionReference(potionTemplateId, potions, localizationKeys);
        }

        if (heroRewardPoolUsers != 1)
            throw new InvalidOperationException("G7 Act must have exactly one Shop using Hero reward pools.");
        foreach (int uniqueRelicTemplateId in manifest.UniqueRelicTemplateIds)
        {
            if (!referencedRelicIds.Contains(uniqueRelicTemplateId))
            {
                throw new InvalidOperationException(
                    $"Unique Relic {uniqueRelicTemplateId} is not referenced by any G7 non-combat reward.");
            }
        }
    }

    /// <summary>要求四种程序化非战斗节点保持 G6 已验证的最小 payload 依赖形状。</summary>
    private static void ValidateNonCombatReferenceShape(ActNonCombatContentReference content)
    {
        switch (content.Kind)
        {
            case MapNodeKind.Rest:
            case MapNodeKind.Event:
                if (content.RelicTemplateIds.Count != 0 ||
                    content.PotionTemplateIds.Count != 0 ||
                    content.UsesHeroCardRewardPool)
                {
                    throw new InvalidOperationException(
                        $"{content.Kind} {content.ContentId} cannot define item or reward-pool references.");
                }
                break;
            case MapNodeKind.Chest:
                if (content.RelicTemplateIds.Count != 0 ||
                    content.PotionTemplateIds.Count == 0 ||
                    content.UsesHeroCardRewardPool)
                {
                    throw new InvalidOperationException(
                        $"Chest {content.ContentId} must only reference a non-empty Potion pool.");
                }
                break;
            case MapNodeKind.Shop:
                if (content.RelicTemplateIds.Count == 0 ||
                    content.PotionTemplateIds.Count == 0 ||
                    !content.UsesHeroCardRewardPool)
                {
                    throw new InvalidOperationException(
                        $"Shop {content.ContentId} requires Relic, Potion and Hero reward pools.");
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(content));
        }
    }

    /// <summary>要求遗物模板存在、文本可解析且生产数值为正。</summary>
    private static void ValidateRelicReference(
        int relicTemplateId,
        JObject relics,
        ISet<string> localizationKeys)
    {
        JObject relic = RequireRecord(relics, relicTemplateId, "run_tbrelic");
        ValidateItemLocalization(relic, "Relic", relicTemplateId, localizationKeys);
        _ = ReadRequiredPositiveInt(
            relic,
            "battle_start_strength",
            $"Relic {relicTemplateId}");
    }

    /// <summary>要求药水模板存在、文本可解析且治疗数值为正。</summary>
    private static void ValidatePotionReference(
        int potionTemplateId,
        JObject potions,
        ISet<string> localizationKeys)
    {
        JObject potion = RequireRecord(potions, potionTemplateId, "run_tbpotion");
        ValidateItemLocalization(potion, "Potion", potionTemplateId, localizationKeys);
        _ = ReadRequiredPositiveInt(
            potion,
            "heal_amount",
            $"Potion {potionTemplateId}");
    }

    /// <summary>要求物品名称与描述 key 非空且已由 i18n 作者表登记。</summary>
    private static void ValidateItemLocalization(
        JObject item,
        string itemKind,
        int templateId,
        ISet<string> localizationKeys)
    {
        string nameKey = ReadRequiredTrimmedString(
            item,
            "name_i18n_key",
            $"{itemKind} {templateId}");
        string descriptionKey = ReadRequiredTrimmedString(
            item,
            "description_i18n_key",
            $"{itemKind} {templateId}");
        RequireLocalizationKey(localizationKeys, nameKey, $"{itemKind} {templateId}");
        RequireLocalizationKey(localizationKeys, descriptionKey, $"{itemKind} {templateId}");
    }

    /// <summary>要求只有真实 Boss 配置一个独立且可完整解析的第二阶段行为组。</summary>
    private static void ValidatePhaseTwoBehaviorGroup(
        int encounterId,
        JObject encounter,
        bool isBossEncounter,
        int phaseOneBehaviorGroupId,
        IReadOnlyCollection<int> phaseOneBehaviorIds,
        JObject behaviorGroups,
        JObject behaviors,
        JObject effects)
    {
        int phaseTwoBehaviorGroupId = ReadRequiredNonNegativeInt(
            encounter,
            "phase_two_behavior_group_id",
            $"Encounter {encounterId}");
        if (!isBossEncounter)
        {
            if (phaseTwoBehaviorGroupId != 0)
            {
                throw new InvalidOperationException(
                    $"Non-Boss Encounter {encounterId} cannot define a phase-two behavior group.");
            }
            return;
        }

        if (phaseTwoBehaviorGroupId == 0)
        {
            throw new InvalidOperationException(
                $"Boss Encounter {encounterId} must define a phase-two behavior group.");
        }
        if (phaseTwoBehaviorGroupId == phaseOneBehaviorGroupId)
        {
            throw new InvalidOperationException(
                $"Boss Encounter {encounterId} phase-one and phase-two groups must be different.");
        }

        JObject phaseTwoGroup = RequireRecord(
            behaviorGroups,
            phaseTwoBehaviorGroupId,
            "battle_tbenemybehaviorgroup");
        IReadOnlyList<int> phaseTwoBehaviorIds = ValidateBehaviorGroup(
            phaseTwoBehaviorGroupId,
            phaseTwoGroup,
            behaviors,
            effects);
        if (new HashSet<int>(phaseOneBehaviorIds).Overlaps(phaseTwoBehaviorIds))
        {
            throw new InvalidOperationException(
                $"Boss Encounter {encounterId} phase behavior ids must not overlap.");
        }
    }

    /// <summary>要求一个可达行为组非空，且其中每个 Behavior 记录都存在。</summary>
    private static IReadOnlyList<int> ValidateBehaviorGroup(
        int behaviorGroupId,
        JObject behaviorGroup,
        JObject behaviors,
        JObject effects)
    {
        IReadOnlyList<int> behaviorIds = ReadRequiredPositiveIntArray(
            behaviorGroup,
            "behavior_ids",
            $"BehaviorGroup {behaviorGroupId}");
        long totalWeight = 0;
        foreach (int behaviorId in behaviorIds)
        {
            JObject behavior = RequireRecord(behaviors, behaviorId, "battle_tbenemybehavior");
            totalWeight += ValidateBehavior(behaviorId, behavior, effects);
            if (totalWeight > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"BehaviorGroup {behaviorGroupId} total weight exceeds Int32.MaxValue.");
            }
        }

        return behaviorIds;
    }

    /// <summary>验证一个可达 Behavior 的权重、限制、运行时枚举与 Effect 引用。</summary>
    private static int ValidateBehavior(int behaviorId, JObject behavior, JObject effects)
    {
        int weight = ReadRequiredPositiveInt(behavior, "weight", $"Behavior {behaviorId}");
        _ = ReadRequiredNonNegativeInt(
            behavior,
            "cooldown_selections",
            $"Behavior {behaviorId}");
        _ = ReadRequiredNonNegativeInt(
            behavior,
            "max_consecutive",
            $"Behavior {behaviorId}");

        int intentType = ReadRequiredInt(behavior, "intent_type", $"Behavior {behaviorId}");
        if (!Enum.IsDefined(typeof(cfg.battle.EnemyIntentType), intentType))
            throw new InvalidOperationException($"Behavior {behaviorId} has an invalid intent type.");

        int targetRule = ReadRequiredInt(behavior, "target_rule", $"Behavior {behaviorId}");
        if (targetRule != (int)cfg.battle.TargetRule.Self &&
            targetRule != (int)cfg.battle.TargetRule.Enemy)
        {
            throw new InvalidOperationException(
                $"Behavior {behaviorId} has an unsupported target rule {targetRule}.");
        }

        int effectId = ReadRequiredPositiveInt(
            behavior,
            "effect_id",
            $"Behavior {behaviorId}");
        _ = RequireRecord(effects, effectId, "battle_tbcardeffect");
        return weight;
    }

    /// <summary>验证 Enemy 视图短键满足运行时 Addressables 转换契约。</summary>
    private static void ValidateCharacterViewKey(string viewPrefabKey, int enemyTemplateId)
    {
        try
        {
            _ = CharacterViewAddress.FromKey(viewPrefabKey);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                $"Enemy {enemyTemplateId} has an invalid view prefab key.",
                exception);
        }
    }

    /// <summary>从 ID 索引表读取对象记录，并拒绝顶层 key 与记录 ID 漂移。</summary>
    private static JObject RequireRecord(JObject table, int recordId, string tableName)
    {
        string key = recordId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        JObject record = table[key] as JObject
            ?? throw new InvalidOperationException($"{tableName} is missing record {recordId}.");
        JToken idToken = record["id"];
        if (idToken == null || idToken.Type != JTokenType.Integer || idToken.Value<int>() != recordId)
        {
            throw new InvalidOperationException(
                $"{tableName} record key {recordId} does not match its integer id.");
        }

        return record;
    }

    /// <summary>读取一个必须非空且只含不重复正整数的 JSON 数组。</summary>
    private static IReadOnlyList<int> ReadRequiredPositiveIntArray(
        JObject record,
        string fieldName,
        string recordName)
    {
        JArray array = record[fieldName] as JArray
            ?? throw new InvalidOperationException($"{recordName} has no {fieldName} array.");
        if (array.Count == 0)
            throw new InvalidOperationException($"{recordName} {fieldName} cannot be empty.");

        var values = new int[array.Count];
        var seen = new HashSet<int>();
        for (int index = 0; index < array.Count; index++)
        {
            JToken token = array[index];
            if (token.Type != JTokenType.Integer ||
                token.Value<int>() <= 0 ||
                !seen.Add(token.Value<int>()))
            {
                throw new InvalidOperationException(
                    $"{recordName} {fieldName} must contain unique positive integer ids.");
            }
            values[index] = token.Value<int>();
        }

        return Array.AsReadOnly(values);
    }

    /// <summary>读取一个必须存在且为正数的整数引用字段。</summary>
    private static int ReadRequiredPositiveInt(
        JObject record,
        string fieldName,
        string recordName)
    {
        JToken token = record[fieldName];
        if (token == null || token.Type != JTokenType.Integer || token.Value<int>() <= 0)
        {
            throw new InvalidOperationException(
                $"{recordName} must define positive integer field {fieldName}.");
        }

        return token.Value<int>();
    }

    /// <summary>读取一个必须存在且保持整数类型的配置字段。</summary>
    private static int ReadRequiredInt(
        JObject record,
        string fieldName,
        string recordName)
    {
        JToken token = record[fieldName];
        if (token == null || token.Type != JTokenType.Integer)
            throw new InvalidOperationException($"{recordName} has no integer field {fieldName}.");

        return token.Value<int>();
    }

    /// <summary>读取一个必须存在且不能为负数的整数配置字段。</summary>
    private static int ReadRequiredNonNegativeInt(
        JObject record,
        string fieldName,
        string recordName)
    {
        JToken token = record[fieldName];
        if (token == null || token.Type != JTokenType.Integer || token.Value<int>() < 0)
        {
            throw new InvalidOperationException(
                $"{recordName} must define non-negative integer field {fieldName}.");
        }

        return token.Value<int>();
    }

    /// <summary>读取一个必须存在、非空且没有首尾空白的字符串字段。</summary>
    private static string ReadRequiredTrimmedString(
        JObject record,
        string fieldName,
        string recordName)
    {
        JToken token = record[fieldName];
        if (token == null || token.Type != JTokenType.String)
            throw new InvalidOperationException($"{recordName} has no string field {fieldName}.");

        string value = token.Value<string>();
        if (string.IsNullOrWhiteSpace(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{recordName} field {fieldName} must be non-empty and trimmed.");
        }

        return value;
    }

    /// <summary>要求一个配置引用的本地化 key 已由 i18n 作者表登记。</summary>
    private static void RequireLocalizationKey(
        ISet<string> localizationKeys,
        string key,
        string owner)
    {
        if (!localizationKeys.Contains(key))
            throw new InvalidOperationException($"{owner} references missing localization key '{key}'.");
    }

    /// <summary>读取一份必须存在、可解析且非空的生成 JSON 表。</summary>
    private static JObject ReadRequiredTable(string tablePath)
    {
        if (!File.Exists(tablePath))
            throw new InvalidOperationException($"Generated table does not exist: {tablePath}");

        JObject table = JObject.Parse(File.ReadAllText(tablePath));
        if (table.Count == 0)
            throw new InvalidOperationException($"Generated table has no records: {tablePath}");

        return table;
    }
}
