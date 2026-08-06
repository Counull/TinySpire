---
title: STS2 v0.107.1 Ironclad 单人卡池接入
page_type: plan
lifecycle: active
created: 2026-08-06
updated: 2026-08-06
status: in-progress
status_source: ../SESSION_LOG.md
---

# STS2 v0.107.1 Ironclad 单人卡池接入

## 目标与版本边界

以用户本机 Steam public/main 安装为版本事实，尽可能把 Ironclad 单人卡池接入 TinySpire：

- 游戏版本：`v0.107.1`
- Steam build：`23811903`
- 游戏构建 commit：`59260271`
- 语言源：英文；TinySpire 提供项目自有 en / zh-CN 表述
- 提取日期：`2026-08-06`
- 范围：85 张单人卡（3 Basic、20 Common、35 Uncommon、25 Rare、2 Ancient）
- 排除：`Demonic Shield`、`Tank` 两张多人专用卡，等待 `DEP-008`；不把 v0.110.0 beta 内容混入本快照

本计划不会提交 STS2 官方卡图、完整原文数据库或二进制提取物。卡名、结构化数值与机制事实用于兼容实现；说明文本采用项目自有双语表述，未有原创牌面时使用明确的项目占位素材。

## 锁定接口与原子性

- 共享战斗写入仍只由 `BattleCommandQueue.Submit` 排序。
- Queue、Turn、BattleSession、CardZones 对外只作为只读取证面；不得增加第二份 Hand、CardZones、Combatant、Intent 或 Power 事实。
- 卡牌程序在首次权威写入前完整解析、投影和校验；失败必须是零能量、零卡区、零参与者、零随机流写入。
- 配置只表达机制原语、目标选择器、值表达式、条件、重复和触发器；不得按模板 ID、卡名或本地化文本在运行时代码分支。
- 新增 settlement 只能表达通用机制事实，不能出现卡牌专用 settlement。
- 每个切片先取得精确红灯，再做最小实现、相关回归、Luban / `TinySpire/Build/Sync and Build All`、文档同步和独立停止点。

## 串行切片

| 切片 | 目标 | 精确红灯 / 验收 seam | 受限边界 |
|---|---|---|---|
| I0 | 冻结版本、85 张目录和机制缺口矩阵 | 本机 manifest/release_info 与同 build 社区结构化数据交叉核对 | 只读；不复制官方图像/完整原文 |
| I1（已完成） | `CatalogOnly` 运行时隔离 | `Submit_CatalogOnlyCard_FailsBeforeEnergyOrCardZoneWrites` | Card schema、Rules、失败原因；不改 Queue/Turn/settlement |
| I2（已完成） | `CatalogOnly` 构建隔离 | `Validate_CatalogOnlyCardReferencedByDeck_Throws`；Implemented 卡必须具备有效程序/牌面键 | Editor validator；不改运行时权威写链 |
| I3（已完成） | 85 张单人卡全部进入 Card 表并标明实现状态 | 表计数、唯一外部 key、类型/稀有度/费用/目标/归宿/升级元数据、双语 key 覆盖；当前 Deck 只引用 Implemented | 不提交官方牌面；CatalogOnly 不可玩 |
| I4（已完成） | 成功归宿（Discard / Exhaust） | 配置为 Exhaust 的真实卡按 Effect 后移入 Exhaust | 只改 Turn 内部成功归宿；未增加 Exhaust 飞行动画 |
| I5 | 每步独立目标与深卡牌执行 module | Enemy 伤害后 Source 格挡，记录顺序正确且原子 | 需要改 Turn/settlement；Queue seam 不变 |
| I6 | 多目标、随机目标与重复命中 | Encounter 顺序、确定性随机、死亡跳过 | 不增加全局随机或第二队列 |
| I7 | 抽牌、手牌上限与卡区选择 | 抽牌上限、队首选择重验、漂移零写入 | 需要 CardZones 通用移动；选择暂停仍排除 |
| I8 | X 费、能量增减与临时费用 | 同一冻结 X 驱动所有步骤并支付一致 | 不公开新的能量写入口 |
| I9 | 升级与实例修饰 | 升级实例使用升级费用/程序且模板身份不变 | 不把实例事实回写表格 |
| I10 | Retain、Ethereal、Innate 与回合卡区时机 | 回合结束稳定顺序处理保留/消耗/弃牌 | 需要改 Turn，保持单一 Layout 发布 |
| I11 | Power、Modifier 与命令内触发器 | 后续命令内按序展开触发器并生成通用记录 | 不增加第二命令/动画队列 |
| I12 | 失血、治疗与战斗内生命变化 | 失血绕过 Block，治疗不超过战斗上限 | 永久 Max HP 等 Run authority |
| I13 | 生成、复制与随机牌 | 同种子结果一致、实例 ID 唯一、随机域隔离 | 不消费洗牌或意图随机流 |
| I14 | 全卡逐张 Queue 回归与真实 BattleScene 验收 | 每张 Implemented 卡有 Queue/事实/settlement/文本/AB/Console 证据 | 主观观感不冒充性能或规则通过 |

## 当前停止范围：I0 → I1 → I2 → I3 → I4

用户已单独确认 I4，且 I4 已按最小范围完成：`BattleTurnController` 在首次权威写入前冻结基础 `PlayDestination`，只接受 Discard / Exhaust，并在全部 Effect 之后调用既有 CardZones 移动原语。Queue、settlement 与 CardZones 公共契约均未改变。

当前独立停止点：I4 已完成，证据见 `../06_testing/2026-08-06-sts2-ironclad-i4-success-destination.md`。Tremble 已以 1 费、敌方目标、3 层易伤和 Exhaust 归宿翻为 `Implemented`；冻结 STS2 子集当前为 4 张 `Implemented`、81 张 `CatalogOnly`，加项目自有 Strength 后生产表为 5 / 81。默认 Deck 未加入 Tremble，Tremble 继续复用 `art_placeholder`；82 张缺图清单仍保留。I4 不包含 Exhaust 飞行动画。I5 会修改 Turn/settlement 的执行边界，必须重新报告风险并取得新的明确确认。

### I1 红灯（已完成）

1. 通过公开 `BattleCommandQueue.Submit` 提交一张 `CatalogOnly` 手牌。
2. 期望稳定失败 `CardNotImplemented`。
3. 断言能量、Hand/Discard/Exhaust、Combatants、Turn 与 Queue fault 均未改变。

### I2 红灯（已完成）

1. 让任一 Deck 引用 `CatalogOnly` 卡。
2. 构建期校验必须在 Luban 后、Localization/Addressables 前抛出带 Deck/Card ID 的错误。
3. `Implemented` 卡缺效果程序或合法 `illustration_key` 时同样 fail-fast；`CatalogOnly` 的占位素材规则必须显式，不允许伪造路径。

### I4 红灯（已完成）

1. 经公开 `BattleCommandQueue.Submit` 提交配置为 Exhaust 的 Tremble，期望结算严格为 Energy 3→2、Vulnerable 0→3、Hand→Exhaust；旧实现实际移动到 Discard。
2. 生成数据要求 Tremble 为 `Implemented`、绑定 `vulnerable:4006` 且 Effect 4006 为 ApplyVulnerable 3；旧 JSON 实际仍为 `CatalogOnly`。
3. 构建门禁在 4/81 数量不变时交换 Tremble 与 Anger 的可玩身份，必须报告 missing/unexpected；旧门禁只检查 3/82 数量。

## 完成定义

“录入”与“可玩”必须分开报告：

- 录入完成：85/85 单人卡都有稳定目录身份、版本来源、类型/稀有度/费用/目标/归宿/升级元数据和 en/zh-CN key，并能由 Luban/构建校验重复生成。
- 可玩完成：卡牌从 `CatalogOnly` 翻为 `Implemented`，且该卡涉及的全部机制已通过 Queue、只读事实、settlement、Addressables Packed 路径、真实 Game View 与 Console 验收。
- 多人专用卡、永久 Run 修改和需要执行中暂停选择的卡，不得用局部假状态冒充完成。
