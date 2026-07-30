---
title: 卡牌本地化与动态文本 · 验证记录
page_type: testing
lifecycle: active
date: 2026-07-30
source: ../plans/2026-07-30-card-localized-text-design.md
status_source: ../SESSION_LOG.md
---

# 卡牌本地化与动态文本 · 验证记录

## TDD / 数据生成

- Red：新增 `CardValueCalculatorTests` 后，计算模块尚不存在；同时 Unity 尚未刷新新生成的 `CardEffectBinding.cs`，旧 `.csproj` 按预期不能完成编译。
- Green：实现计算模块后，使用临时 MSBuild include 校验全部新增运行时与编辑器代码，结果 0 error。
- Luban 生成成功：新增 `cfg.battle.CardEffectBinding`，`Card` 生成 `NameI18nKey`、`DescriptionI18nKey`、`EffectBindings`；`battle_tbcard.json` 已更新。
- 纯计算覆盖：伤害为基础值加当前来源力量并下限为 0；力量增益、格挡与易伤层数保持配置值。

## 静态校验

- 初始四张卡均使用 name/description i18n key。
- Bash 的 `damage → 4004`、`vulnerable → 4005` 顺序稳定。
- `TinySpire/Localization/Validate Battle Card Text` 通过：明确要求 `en`、`zh-CN` 两张表及共享关键词 key；每语言卡牌 key、Smart String 标记、参数集合、重复参数和 effect 引用均合法。

### Excel 编辑源接入（后续验证）

- 已新增 `DataTables/Datas/i18n.xlsx`，`i18n` sheet 包含 10 个现有 key，列为 `key`、`en`、`zh-CN`、`smart`；内容与当前 Battle Cards 文本对应。
- `I18nExcelReader`、导入菜单与一致性校验已通过 `dotnet build`（0 error，12 条既有程序集引用冲突警告）。Luban 成功完成，`game-config.json` 已按生成流程恢复到 `Assets/GameData`。
- 待验收：通过 Unity 执行 `TinySpire/Localization/Import Battle Card Text from Excel`、`Validate Battle Card Text` 和 `Addressables/Build Local Content`。本次无法执行，因为已有 Unity Editor 占用了项目；未结束该进程或删除锁文件。

## Unity 与运行时

### R3 绑定与 HandCardVisual 边界（后续验证）

- 历史记录：`BattleStateTests` 与 `CardZoneStateTests` 的泛化通知断言已由 CD-019 替代。
- 运行 BattleScene 后，通过 `CardZoneState.DiscardFromHand` 与 `BattleState.ApplyDamage` 分别触发卡区重建和文本重派生。延迟销毁完成后，运行时手牌事实数为 4、`HandCardVisual` 数量也为 4，Console 无错误。
- `CardView.prefab` 上的 `HandCardVisual` 已配置 Canvas、CardContent 和四个 `Text` 引用；运行时不再通过对象名搜索文本控件。
- 因预制体是可寻址 BattleScene 的依赖，已重建 `TinySpire/Addressables/Build Local Content`，日志确认内容构建成功。
- `dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal`：0 error；保留 12 条既有 Unity 程序集引用冲突警告。

### R3 事实属性与运行时数据命名（后续验证）

- `BattleCombatantsDataTests` 断言伤害后 `Health` 发布新的权威生命值；`BattleCardZonesDataTests` 断言卡区移动发布新的完整布局和手牌值。
- `BattleSession`、卡牌格式化器、手牌 UI、测试文件与运行时源文件均已迁移为 `*Data` 命名；`State` 不再用作运行时数据尾缀。
- 定向 EditMode 18/18、全量 EditMode 25/25 通过；BattleScene 中发布卡区布局后，手牌事实数 4 与 `HandCardVisual` 数量 4 一致，Console 无错误。
- `dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` 为 0 error（12 条程序集引用冲突警告）；Addressables 本地内容重建成功。

- 已生成 `en`、`zh-CN` Locale 与 `Battle Cards` String Table，并纳入同一套 Addressables 本地内容。
- Unity EditMode 23/23 通过；`CardValueCalculatorTests` 覆盖伤害按当前来源力量派生、无来源时使用配置基础值、负值下限为 0，以及非伤害参数保持配置值。
- String Database 已启用 fallback，`zh-CN` Locale 显式以 Project Locale `en` 为后备；fallback 后仍缺失时，Editor/Development Build 显示显眼占位并报错，Release 显示稳定 key。
- BattleScene 实跑初始手牌 5 张；`zh-CN` 显示“造成 6 点伤害”“造成 8 点伤害。施加 2 层易伤”等动态文本。
- 运行时切换到 `en` 后显示 `Deal 6 damage.` 与 `Deal 8 damage. Apply 2 Vulnerable.`。
- 切换前后的 5 个 `HandCardVisual` Entity ID 完全一致，确认只原地重派生文本，没有重建卡牌实例或保存 locale/格式化文本镜像。
- 运行时 Console 0 error、0 warning。

## 未实施

- Effect 执行器、费用、目标选择、伤害/格挡/易伤结算。
- 目标侧预览、关键词富文本和 tooltip。
- 任何 `FormattedDescription`、`DisplayDamage` 或 locale 镜像状态。
