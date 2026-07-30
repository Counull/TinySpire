---
title: 卡牌本地化文本与动态参数设计
page_type: plan
lifecycle: active
date: 2026-07-30
scope: battle.Card、Unity Localization、CardTextFormatter 与手牌 UI
source: 用户确认迁移到 Addressables 后通过 Unity Localization 实施 M2A
status_source: ../SESSION_LOG.md
---

# 卡牌本地化文本与动态参数设计

## 当前结论

M2A 使用 Unity Localization 1.5.12 的 String Table 与 Smart Strings。卡牌名称、说明和共享关键词使用稳定 i18n key；格式化文本与说明数值是当前配置和战斗事实的即时派生结果，不进入 `CardInstanceData` 或 `BattleCombatantsData`。

本切片不实现 Effect 执行器、费用结算、目标选择、状态施加或目标侧伤害预览。

## 静态数据

`battle.Card` 使用：

- `name_i18n_key`
- `description_i18n_key`
- `effect_bindings: CardEffectBinding[]`

`CardEffectBinding` 包含：

```text
argument_key   // damage, block, strength, vulnerable
effect_id
```

它同时保留复合效果顺序，并向 Smart String 暴露稳定的命名参数。`argument_key` 不参与未来效果分派，本地化文本也不直接引用效果 ID。

| card id | name key | description key |
|---:|---|---|
| 3001 | `battle.card.strength.name` | `battle.card.strength.description` |
| 3002 | `battle.card.strike.name` | `battle.card.strike.description` |
| 3003 | `battle.card.defend.name` | `battle.card.defend.description` |
| 3004 | `battle.card.bash.name` | `battle.card.bash.description` |

## Unity Localization 目录

- String Table Collection：`Battle Cards`
- locale：`en`、`zh-CN`
- project locale / fallback source：`en`
- 卡牌说明条目启用 Smart String
- 两张语言表启用 preload
- Localization 生成的资源由 Addressables 构建，不建立第二套资源包

共享关键词 key：

```text
battle.keyword.strength.name
battle.keyword.vulnerable.name
```

Smart String 示例：

```text
en:
  Deal {damage} damage. Apply {vulnerable} {keywordVulnerable}.

zh-CN:
  造成 {damage} 点伤害。施加 {vulnerable} 层{keywordVulnerable}。
```

效果参数名与表格绑定严格一致。`keywordStrength`、`keywordVulnerable` 是格式化器额外提供的共享语义参数，不属于效果绑定。

## 运行时边界

### LocalizationService

- 直接封装 Unity Localization，不预先抽取多后端 interface。
- 启动时等待 `LocalizationSettings.InitializationOperation`。
- 当前语言只读取/写入 `LocalizationSettings.SelectedLocale`，服务不保存镜像 locale 字段。
- `LocaleChanged` 以只读 R3 `Observable<Locale>` 原样转发 Unity Localization 的 `SelectedLocaleChanged` 值；服务不持有 locale 镜像。
- 从预加载的 `Battle Cards` 表同步读取名称与 Smart String。

### CardTextFormatter

```text
Format(CardInstanceData card, CombatantData source)
  → CardPresentationText
      Name
      Description
```

格式化步骤：

1. 从卡牌实例的 `TemplateId` 取得静态 `battle.Card`。
2. 从 `effect_bindings` 读取命名参数及对应 `battle.CardEffect`。
3. 通过 `CardValueCalculator` 从效果基础值和当前来源参与者事实派生显示值。
4. 读取共享关键词本地化文本。
5. 交给 Unity Localization Smart String 生成最终文本。

调用方不持有本地化 key、格式化参数或说明缓存。

## 数值派生

唯一事实：

- 基础值：`battle.CardEffect.value`
- 卡牌身份：`CardInstanceData.TemplateId`
- 当前来源修正：`CombatantData`，本切片只使用 `Strength`

当前公式：

- `DealDamage`：`max(0, effect.value + source.Strength)`
- `GainBlock`、`ModifyAttribute`、`ApplyVulnerable`：直接使用 `effect.value`

当前尚无 Dexterity、Weak 或目标 Vulnerable 事实，因此不假装计算它们。以后效果执行与预览必须复用并扩展同一纯计算模块，不能另写一套说明公式。

不得保存：

- `DisplayDamage`
- `FormattedDescription`
- `FinalDamage`
- 当前 locale 镜像

手牌 UI 通过 R3 订阅玩家 `Strength` 和当前 Locale，并用 `AddTo(this)` 绑定视图生命周期；订阅值变化后重新调用格式化器，原地刷新已有卡牌。

## 构建期校验

Unity Localization 表资源由编辑器直接维护，是翻译文本的唯一来源。`LocalizationBuildTools` 只校验，不创建、补全或覆盖条目；因此新增卡牌、语言或关键词后，必须先在 String Table 中完成翻译，再运行校验。

`LocalizationBuildTools` 检查：

- 每张卡 name/description key 在每个 locale 中存在且非空。
- description 是 Smart String。
- 单卡 `argument_key` 唯一。
- 模板中的效果参数集合与 `effect_bindings` 完全一致。
- 每个绑定的 effect ID 存在。

修改卡牌表或本地化资源后：

1. 运行 Luban，更新生成 C# 与 `Assets/GameData` JSON。
2. 在 Unity Localization 的 `Battle Cards` 表中添加或修改每种语言的文本，并为说明条目启用 Smart String。
3. 执行 `TinySpire/Localization/Validate Battle Card Text`。
4. 执行 `TinySpire/Addressables/Build Local Content`。
5. 运行 EditMode、启动加载和双语言手牌验收。

## 验收

- Strength、Strike、Defend、Bash 在 `en` 与 `zh-CN` 下名称和语序正确。
- Bash 的 `damage`、`vulnerable` 分别来自效果 4004、4005。
- Strike 的显示伤害由基础 6 与当前来源力量即时派生，效果表基础值保持 6。
- 5 张 Strike 不保存 5 份说明字符串。
- 切换语言后已有手牌原地刷新。
- 缺 key、缺效果、重复/缺失参数在编辑器校验阶段失败。
- 本阶段不执行任何 Effect，不写入额外战斗状态。

## 后续边界

- 目标选择后再加入目标侧预览上下文。
- Effect 执行器落地时复用 `CardValueCalculator` 或其演化后的共享规则模块。
- 关键词富文本、tooltip、无障碍纯文本与复数规则另开切片，不在本轮提前实现。
