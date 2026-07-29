---
title: 卡牌本地化文本与动态参数设计
page_type: plan
lifecycle: proposal
date: 2026-07-30
scope: battle.Card、文本目录与卡牌 UI 展示
source: 用户补充卡牌说明需要 i18n key 和可替换文本
status_source: ../SESSION_LOG.md
---

# 卡牌本地化文本与动态参数设计

## 1. 目标

卡牌名称和说明使用稳定 i18n key；说明允许按语言重排句子并替换伤害、格挡、状态层数和关键词。格式化文字是视图派生结果，不成为卡牌实例或战斗状态。

本计划只设计文本数据与格式化 seam，不实现 Effect 执行器，也不在本轮添加 Localization 包。

## 2. 静态数据

### Card 字段

实施表格变更时，将 `battle.Card.name` 替换为：

- `name_i18n_key`
- `description_i18n_key`

示例：

| card id | name key | description key |
|---:|---|---|
| 3001 | `battle.card.strength.name` | `battle.card.strength.description` |
| 3002 | `battle.card.strike.name` | `battle.card.strike.description` |
| 3003 | `battle.card.defend.name` | `battle.card.defend.description` |
| 3004 | `battle.card.bash.name` | `battle.card.bash.description` |

key 使用稳定语义名，不包含具体语言。改显示文本不改 key；key 一旦进入存量内容，不因英文命名润色而重命名。

### 说明参数绑定

仅有 `effect_ids` 不足以给本地化作者提供可读参数名。建议把卡牌效果引用演化为有顺序的绑定：

```text
CardEffectBinding
  argument_key   // damage, block, strength, vulnerable
  effect_id
```

例如 Bash：

```text
damage     → 4004
vulnerable → 4005
```

执行器未来仍按绑定顺序读取 `effect_id`；`argument_key` 只用于说明文本和校验，不参与效果分派。具体采用 Luban 复合数组还是独立关联表，在表格实施切片中按可维护性确认；不得让本地化文本直接引用 `4004` 之类的内部 ID。

当前 CD-013 正式口径仍是 `effect_ids`。若实施时决定由绑定结构替换该字段，必须新增一条代码决策显式修订 CD-013；本 proposal 不会静默改变现有表结构事实。

## 3. 文本目录

目录至少包含卡牌 name、description 和共享关键词：

```text
battle.card.strike.name
battle.card.strike.description
battle.card.defend.name
battle.card.defend.description
battle.card.bash.name
battle.card.bash.description
battle.keyword.strength.name
battle.keyword.vulnerable.name
```

模板使用命名参数，不使用 `{0}`、`{1}`：

```text
zh-CN:
  battle.card.bash.description =
  造成 {damage} 点伤害。施加 {vulnerable} 层{keyword.vulnerable}。

en:
  battle.card.bash.description =
  Deal {damage} damage. Apply {vulnerable} {keyword.vulnerable}.
```

命名参数让不同语言自由改变语序，也让构建期校验能准确指出缺失参数。

当前项目没有 `com.unity.localization`。文本目录后端是实施前 Open Question：

- JSON/Luban 文本目录：符合现有 YooAsset 热更新链，但需要自行处理 fallback 和模板格式。
- Unity Localization/Smart Strings：现成编辑器与语法能力，但会新增包、资源类型和构建流程。

这项选择不泄漏到卡牌 UI；先保持 `LocalizationService` 为具体模块。只有出现第二个真实后端时才提取 adapter interface。

## 4. CardTextFormatter 深模块

### Interface

调用方只需要表达：

```text
Format(CardInstanceId cardId, CombatantId? sourceId)
  → CardPresentationText
      Name
      Description
```

`sourceId` 允许伤害、格挡等显示值反映玩家力量或虚弱。目标相关修正不在目标未确定时写进卡牌说明。

调用方不需要知道：

- key 如何查找和回退。
- 参数如何绑定到 Effect。
- 数值如何计算、取整和着色。
- 关键词如何本地化。
- 富文本如何转义。

### Implementation

1. 用 `CardInstanceId` 读取唯一运行时卡牌并得到 `TemplateId`。
2. 查 `battle.Card` 的 name/description key。
3. 从当前 locale 目录解析模板。
4. 从效果绑定产生参数表。
5. 调用共享纯计算模块得到当前显示值。
6. 替换值与关键词，生成安全富文本。

删除 `CardTextFormatter` 后，上述复杂度会散落到每个卡牌 View、奖励界面、牌组浏览和图鉴中，因此该模块应集中维护。

## 5. 显示值与结算值

唯一事实来源：

- 基础值：`battle.CardEffect.value`。
- 卡牌身份/升级/临时变化：对应运行时卡牌事实。
- 力量、虚弱等：`BattleState` 中的参与者事实。

派生结果：

- 卡牌说明中的 `{damage}`、`{block}`。
- 数值是增强色、削弱色还是普通色。
- 目标选定后的目标侧伤害预览。
- 最终结算值。

说明格式化与 Effect 执行器必须复用同一纯数值计算模块，区别只在上下文：

- 卡牌说明：只使用来源侧和卡牌自身已知事实。
- 目标预览：再加入目标侧状态。
- 实际结算：使用提交命令时的完整事实快照。

不得在 `CardInstanceState` 保存 `DisplayDamage`、`FormattedDescription` 或 `FinalDamage`。

## 6. 关键词与富文本

- 本地化作者使用 `{keyword.vulnerable}` 等语义 token，不直接写项目颜色代码。
- `CardTextFormatter` 解析 token 后统一应用关键词颜色、下划线或 tooltip metadata。
- 普通参数值按整数或既定数值格式输出；所有外部文本先转义，再由受控 token 生成允许的标签。
- 无障碍/纯文本输出复用相同参数，但不带颜色标签。

## 7. LocaleChanged

- `LocalizationService` 保存当前 locale 事实并发出 `LocaleChanged`。
- 卡牌 View 收到通知后按自己的 `CardInstanceId` 重新请求文本。
- View 不保留另一份可写语言状态。
- 第一版不缓存格式化文本。以后若添加缓存，必须以 locale、卡牌实例以及相关战斗事实版本为 key，并有明确失效策略。

## 8. 错误与回退

- 开发环境：缺 key、缺参数、未知参数和非法标签均记录错误，文本显示 `[missing:<key>]`。
- 发布环境：当前语言缺失时回退默认语言；默认语言仍缺失时显示 key。
- 不允许因为新语言缺一条文本而回退到某个旧缓存字符串。
- 参数求值失败时显示显眼占位并记录卡牌 ID、key 与参数名，不静默填 0。

## 9. 构建期校验

表格/文本生成后验证：

- name/description key 非空且唯一指向现有文本。
- 每个支持语言都有文本或符合明确 fallback 策略。
- 模板参数集合与 CardEffectBinding 参数集合完全一致。
- argument key 在单卡内唯一。
- Effect 引用存在，绑定顺序稳定。
- 禁止未授权富文本标签。
- 初始四张卡在至少两个 locale 下生成快照测试。

修改文本目录或表格后仍遵守 `AGENTS.md`：运行生成，更新 `Assets/GameData`，重建 YooAsset `Main` 内置包并实跑加载。

## 10. 实施切片

1. 确认文本目录后端与 fallback locale。
2. 修改 Card schema 与效果参数绑定，填写初始四张卡的 key。
3. 建立两个 locale 的最小文本目录和构建期校验。
4. 实现 `LocalizationService` 与 `CardTextFormatter`，先只计算静态基础值。
5. 让手牌 UI、牌组浏览和奖励卡统一消费格式化结果。
6. Effect 数值公式落地后，让说明与结算共享计算模块。
7. 加入运行时语言切换、关键词样式和无障碍纯文本。

## 11. 验收

- Strike、Defend、Bash、Strength 在两种语言下名称和语序正确。
- Bash 的 `{damage}` 与 `{vulnerable}` 分别取自正确效果。
- 5 张 Strike 不保存 5 份说明字符串。
- 切换语言后现有手牌原地刷新。
- 力量变化后 Strike 的显示伤害重新派生，不修改 Effect 基础值。
- 缺 key/缺参数能在构建期失败，运行时 fallback 行为明确。
- 本阶段不产生任何战斗状态写入，也不执行 Effect。
