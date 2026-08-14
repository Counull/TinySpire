---
title: TinySpire · BattleScene MVP 路线图（冻结归档）
owner: Daedalus
page_type: roadmap
lifecycle: archived
created: 2026-07-29
updated: 2026-08-14
status_source: SESSION_LOG.md
note: 本文件固定保存已完成的 BattleScene MVP 路线；Run 阶段由 RUN_ROADMAP.md 承接。
---

# TinySpire · BattleScene MVP 路线图（冻结归档）

> **已归档。** BattleScene MVP 已于 2026-08-14 形成 Git 检查点 `e07e39a`，标签为 `milestone-battlescene-mvp-2026-08-14`。本文固定保存 M0～M10 的目标、依赖顺序和验收口径，不再承载 Run 阶段规划；当前状态只查 `SESSION_LOG.md`，后续路线见 [RUN_ROADMAP.md](RUN_ROADMAP.md)。

## 1. MVP 终点

这里的“对标 STS”不是一次性复制完整游戏，而是先交付一场规则、操作感和信息反馈都闭合的单场战斗：

1. 从配置创建一名玩家和一个遭遇中的全部敌人。
2. 初始卡组进入抽牌堆，按确定性随机种子洗牌，开局抽 5 张。
3. 玩家回合开始时获得 3 点能量；可按当前语言查看卡名、费用、目标与带动态数值的效果说明。
4. 拖拽攻击牌指向敌人，技能牌按规则打出；合法性、费用和目标在提交命令时统一验证。
5. 卡牌按 `effect_bindings` 顺序结算伤害、格挡、力量和易伤；结算结果驱动生命、格挡、状态与反馈 UI。
6. 玩家结束回合后，手牌按规则进入弃牌堆，格挡和能量按回合规则清理。
7. 敌人展示下一步意图，按行为规则行动；随机行为在同一战斗种子下可复现。
8. 抽牌堆为空时把弃牌堆洗回抽牌堆；死亡参与者不再行动。
9. 全部敌人死亡进入胜利；玩家死亡进入失败；两者均停止继续接受出牌。
10. 一场战斗可从 Bootstrap 正常进入、完成、退出或重新开始，控制台无错误。

地图、遗物、药水、商店、事件、存档、卡牌奖励和完整角色内容不纳入第一段 BattleScene MVP；它们现由独立的 [Run MVP 路线图](RUN_ROADMAP.md) 承接。先稳定单场战斗，再让跨场景玩法依赖它。

## 2. 不可破坏的数据原则

### 2.1 静态模板与运行时事实分离

- Luban 表只保存英雄、敌人、遭遇、卡组、卡牌和效果的静态模板。
- `BattleCombatantsData` 保存参与者运行时事实；配置表不保存当前生命、格挡、死亡或状态层数。
- 卡牌模板 ID 可以重复；每张进入战斗的卡必须拥有唯一 `CardInstanceId`。
- UI 只显示运行时状态与静态模板的组合结果，不成为生命、能量、牌堆或意图的写入者。

### 2.2 次生数据按需派生

以下内容不得成为第二份可变状态：

- `IsAlive` 从当前生命派生。
- 玩家列表、敌人列表、存活目标从 `BattleCombatantsData.All` 派生。
- 手牌数、抽牌堆数、弃牌堆数从各权威有序集合的数量派生。
- `CanPlayCard` 从当前阶段、能量、卡牌费用、目标规则和目标状态派生。
- 伤害预览从攻击基础值、力量、易伤、虚弱等事实计算；不保存一个需要同步的“最终伤害”字段。
- UI 高亮、按钮可用性、胜负提示均从当前状态派生。
- 已本地化的卡名、卡牌说明和说明中的动态数值从 i18n key、卡牌模板、卡牌实例与战斗数据派生，不保存在 `CardInstanceData`。

必须保存为事实的内容包括：

- 当前生命、当前格挡、状态层数。
- 卡牌实例身份，以及它当前所在区域内的顺序。
- 当前回合阶段、回合号、当前能量。
- 本轮已选定的敌人意图，因为它既要展示，也要在敌人阶段执行。
- 随机种子与随机流位置，保证行为可复现；不把 Unity 全局随机状态当作战斗事实。

## 3. 目标运行时结构

这是一份边界图，不要求一次完成所有类。

| 模块 | 唯一事实 | 只读/派生出口 | 写入口 |
|---|---|---|---|
| `BattleSession` | 本场战斗拥有的各运行时聚合引用 | 战斗状态、卡牌区域、回合状态 | 仅负责从配置创建和装配 |
| `BattleCombatantsData` | `CombatantId → CombatantData` | 阵营、存活目标、胜负 | 伤害、治疗、格挡、状态变更 |
| `BattleCardZonesData` | 卡牌实例定义；`CardZoneLayoutData` 中抽牌/手牌/弃牌/消耗区的有序且互斥集合 | 各区数量、某卡所在区域 | `Draw`、`DiscardFromHand`、`ExhaustFromHand`、`DiscardHand`；空抽牌堆时内部重洗 |
| `BattleCommandQueue` / `BattleTurnData` | 阶段、回合号、每玩家能量与结束状态、当前行动敌人、权威命令顺序 | 是否可出牌、是否可结束行动、当前行动者 | `BattleCommandQueue.Submit` |
| `BattleEnemyIntentsData` | `CombatantId → BehaviorId` 当前意图、最小选择历史、敌人行为专属随机流 | 意图类型、目标、图标与预测数值均从模板和当前参与者事实派生 | 合法敌人完成命令协调 `CompleteAndSelectNext` |
| `GameRandom` | 单个规则随机流的 `uint State` | 可复现的抽样与 Fisher–Yates 洗牌 | 由拥有该随机域的聚合推进；不同随机域不共享实例 |
| `CardTextFormatter` | 不持有战斗事实 | 当前语言的卡名、说明、关键词与动态参数 | 纯格式化，无状态写入口 |

`BattleCardZonesData` 不同时保存 `CardInstanceData.Zone` 与四份区域列表。卡牌模板/实例定义是一类事实，区域顺序是另一类事实；四个区域集合互斥，移动卡牌必须通过单一原子入口完成，区域计数直接由集合派生。

## 4. 阶段总览

| 阶段 | 交付物 | 依赖 |
|---|---|---|
| M0 | 手牌扇形、悬停、拖拽、越线判定 | 无 |
| M1 | Luban 数据接入 `BattleCombatantsData`/`BattleCardZonesData` | M0 |
| M2 | 卡牌区域、洗牌、抽牌、弃牌、重洗 | M1 |
| M2A | 卡牌 i18n key、说明模板与可替换参数 | M1；可与 M2 并行 |
| M3 | BattleScene 主 HUD 与玩家/敌人运行时视图（M3A-M3E） | M1、M2A；可与 M2 分切片推进 |
| M4 | 回合调度与 3 能量规则 | M2、M3 |
| M5 | 敌人生成、意图与确定性行为选择 | M3、M4 |
| M6 | 出牌命令、合法性与目标选择 | M3～M5 |
| M7 | Effect 执行器 | M6 的命令与目标边界 |
| M8 | 敌人行动、状态时机与完整战斗循环 | M7 |
| M9 | STS 式反馈、胜负与重开 | M8 |
| M10 | 数值对标、回归、性能与内容扩展入口 | 完整闭环 |

依赖顺序把 M2 放在 M7 之前：没有权威牌堆与回合移动规则时，效果器无法组成真实的“抽牌—出牌—弃牌—重洗”循环。

## 5. 详细阶段

### M0～M1 · 前置切片

当前实施状态与完成历史只查 `SESSION_LOG.md`，具体设计/验收查：

- `plans/2026-07-30-battle-config-runtime-integration.md`
- `06_testing/2026-07-30-battle-config-runtime-integration.md`
- `06_testing/2026-07-30-battlescene-drag-to-play-minimal.md`

`DEP-006` 已由 M2 运行时数据切片解决：初始卡组的全部卡牌先实例化到 `BattleCardZonesData`，经过确定性洗牌后再抽取初始手牌。当前实施状态与剩余 UI/回合接入仍以 `SESSION_LOG.md` 为准。

### M2 · 卡牌区域与确定性洗牌

目标是一次性解决抽牌堆、手牌、弃牌堆和消耗区的数据归属权。

状态设计：

- 创建全部 `CardInstanceData`，实例身份在整场战斗中不变化。
- 抽牌堆、手牌、弃牌堆、消耗区各保存有序 `CardInstanceId`，四者互斥。
- 不在卡牌实例上再保存一个 `Zone` 镜像字段。
- `DrawPileCount` 等计数全部由集合派生。
- 所有区域移动通过一个聚合入口执行，移动失败不产生半完成状态。

行为：

1. 按初始卡组创建 10 张卡牌实例。
2. 使用战斗专属 `GameRandom` 实例执行 Fisher–Yates 洗牌；地图、奖励和敌人行为不得共享该实例。
3. 抽牌：从抽牌堆约定的一端移动到手牌。
4. 抽牌堆为空且弃牌堆非空：把弃牌堆全部移入抽牌堆并洗牌，再继续抽。
5. 抽牌堆和弃牌堆都为空：少抽，不制造虚拟牌。
6. 回合结束：仍在手中的牌按顺序进入弃牌堆。
7. 打出的普通牌在结算完成后进入弃牌堆；消耗牌进入消耗区。

测试重点：

- 重复模板卡仍保持不同实例身份。
- 任意时刻一张实例只能存在于一个区域。
- 多次重洗不丢牌、不复制牌。
- 同一随机种子产生同一洗牌顺序。
- `GameRandom.State` 可保存/恢复；不同实例随机流互不推进。

验收场景：进入 BattleScene 后初始手牌来自洗牌结果；连续结束回合能看到抽牌堆减少、弃牌堆增加并最终重洗。

### M2A · 卡牌本地化文本与动态参数

实现口径：使用 Unity Localization 1.5.12 + Smart Strings，并统一进入 Addressables 本地内容构建；完成状态只查 `SESSION_LOG.md`。

这一阶段只负责“如何正确显示卡牌文字”，不执行卡牌效果。

静态配置目标：

- `battle.Card.name` 迁移为 `name_i18n_key`。
- 新增 `description_i18n_key`。
- 卡牌效果引用需要提供稳定的命名参数绑定，例如 `damage → effect 4002`、`vulnerable → effect 4005`；本地化文本只看到 `{damage}`、`{vulnerable}`，不看到效果 ID。
- 文本目录与战斗表分离。卡牌表只保存 key，不在同一行复制中文、英文或最终格式化文本。

初始 key 示例：

```text
battle.card.strike.name
battle.card.strike.description
battle.card.defend.name
battle.card.defend.description
battle.card.bash.name
battle.card.bash.description
battle.keyword.vulnerable.name
```

不同语言可以自由调整语序：

```text
zh-CN: 造成 {damage} 点伤害。施加 {vulnerable} 层{keywordVulnerable}。
en:    Deal {damage} damage. Apply {vulnerable} {keywordVulnerable}.
```

`CardTextFormatter` 的外部接口只接收卡牌实例和可选来源参与者，返回 `Name`、`Description` 等展示结果。模块内部完成：

1. 按 `TemplateId` 找卡牌的 name/description key。
2. 从当前语言目录解析文本模板。
3. 按命名参数绑定读取对应 Effect 模板。
4. 用与未来效果结算共享的纯数值公式计算当前展示值。
5. 替换命名参数和关键词，并由模块统一控制富文本转义与着色。

动态数值规则：

- Strike 的 `{damage}` 可从基础伤害与玩家力量、虚弱等来源事实派生。
- Defend 的 `{block}` 可从基础格挡和影响格挡的来源事实派生。
- Bash 的 `{damage}` 与 `{vulnerable}` 分别绑定两个效果。
- 目标特有的易伤等修正不写回卡牌说明；目标尚未确定时只显示来源侧可确定的数值，目标命中后的预览由目标 UI 单独派生。
- “基础值”“当前展示值”“最终结算值”不能三份并存为状态。基础值来自配置，展示值和结算值调用同一计算模块按上下文即时得出。

语言切换：

- 当前语言变化时发出一次 `LocaleChanged` 通知，现有卡牌 View 重新请求格式化结果。
- 第一版不缓存格式化结果；未来若性能证明需要缓存，key 必须包含 locale 与相关事实版本，并记录失效策略。
- 缺失 key：开发环境显示显眼占位并报错；发布环境回退到默认语言，仍缺失则显示 key，不能静默显示旧语言文本。

校验：

- 构建前检查 key 唯一、各支持语言都存在、模板参数与绑定完全一致。
- 拒绝重复参数、未知参数、未使用绑定、效果引用缺失和不允许的富文本标签。
- 文本目录后端已确认为 Unity Localization；`Battle Cards` String Table 提供 `en`、`zh-CN`，卡牌说明使用 Smart Strings。Localization 资源与场景、GameData 一起由 Addressables 构建，不再维护 YooAsset 或另一套资源包。
- 不自行实现复数/性别语法引擎；需要复杂语法时选择已有的 Smart String/ICU 能力。

完整设计见 `plans/2026-07-30-card-localized-text-design.md`。

### M3 · BattleScene 主 HUD 与参与者视图

当前将 M3 拆为按事实依赖推进的切片：M3A 参与者世界视图与生命 HUD；M3B 抽牌/弃牌计数；M3C 能量与结束回合；M3D 敌人意图；M3E 格挡、状态、死亡、回合提示与胜败覆盖层。M3A～M3D 已分别随 M1/M2、M4 与 M5 落地；M7 已提供格挡、状态与结算事实，M8 已完成状态时机、死亡中止与规则层终局，M9 已完成 M3E 的常驻 HUD、死亡/胜负覆盖层与最终表现，最终验收见 `06_testing/2026-08-02-m9g-full-validation-review.md`。参与者设计见 `plans/2026-07-30-battlescene-participant-views.md`，敌人意图设计见 `plans/2026-08-01-m5-enemy-intents-deterministic-behavior.md`。

主界面至少包含：

- 顶部或上方战场区：敌人视图、生命、格挡、状态、意图。
- 玩家区：玩家形象/锚点、生命、格挡、状态。
- 底部操作区：手牌、当前能量、抽牌堆数、弃牌堆数、结束回合按钮。
- 全局覆盖层：战斗开始、玩家回合、敌人回合、胜利、失败与输入锁定。

绑定规则：

- View 只持有 `CombatantId` 或 `CardInstanceId`，不复制生命、费用和意图。
- 名称、基础美术和卡牌文本来自静态模板；当前生命、格挡和状态来自运行时。
- 卡牌 View 不直接拼接说明文本，统一消费 `CardTextFormatter` 的结果；语言切换与状态变化均重新派生。
- 敌人 View 由遭遇生成结果创建，不能依赖场景中手摆固定数量的敌人。
- `LivingEnemies` 每次从 `BattleCombatantsData.All.Values` 派生，不保存镜像列表。
- 生命、力量、格挡与易伤等标量事实由状态聚合以只读 R3 属性公开；UI 不再维护可写镜像，派生展示按这些事实即时计算。

生成与布局边界：

- `CombatantViewFactory`（或同职责的场景服务）接收 `CombatantId`，从 `BattleCombatantsData` 获取运行时类型和模板 ID，再选择玩家/敌人 View prefab；工厂返回 View，不持有第二份参与者集合。
- `PlayerCombatantView`、`EnemyCombatantView` 只保存自身 `CombatantId`。刷新时重新按 ID 查询生命、格挡和状态；静态名称与美术按 `TemplateId` 查询。
- `BattleScene` 提供一个玩家锚点和一个敌人布局区域。MVP 明确支持 1～3 名敌人：按遭遇中的稳定顺序从右向左/等距分布，不能使用 `Dictionary.Values` 顺序决定位置或行动顺序。
- 敌人数量超过布局容量时显式报配置错误，不允许重叠后静默继续。后续扩大容量时只替换布局策略。
- View 在 `BattleSession` 就绪后一次创建，参与者死亡时播放死亡表现后隐藏/销毁 View；运行时 `CombatantState` 仍保留到战斗结束，供日志与胜负派生使用。
- 场景卸载统一销毁 View 和订阅；工厂不创建跨场景对象。
- 第一版可用单一占位玩家/敌人 prefab；把“模板 → 美术 prefab location”写入表格前需单独确认资源寻址字段，不在 UI 代码里硬编码一串模板 ID switch。

事件契约：

- 聚合变化通知只表示“事实可能变化”，不携带另一份生命/格挡副本。
- View 收到通知后按自己的 `CombatantId` 重查当前事实。
- 伤害数字、受击抖动等一次性反馈不从最终生命差值猜测；M7 已产出明确结算记录，M9 已按冻结结果和原 Order 消费并播放表现。

验收：遭遇表从 1 名敌人改为 2 名时，不改场景层级即可生成两个独立 View；位置稳定，各自生命显示对应运行时状态，死亡一个敌人不会让另一个 View 绑定错位。

### M4 · 回合调度与能量

M4 采用“多人根基、当前单玩家接线”。所有玩家共享行动阶段，能量和结束状态按 `CombatantId` 归属；玩家可同时提交命令，不存在全局 `CurrentEnergy`、固定轮转的 `CurrentPlayer` 或“一张牌后切人”。

阶段状态：

```text
NotStarted
  → BattleStart
  → PlayerRoundStart
  → PlayerAction（玩家并发提交，权威队列串行结算）
  → PlayerRoundEnd（全体玩家结束后）
  → EnemyRoundStart
  → EnemyAction（按 Encounter 顺序逐个敌人）
  → EnemyRoundEnd
  → RoundEnd
  → PlayerRoundStart
```

权威事实：

- `BattleTurnPhase`
- `RoundNumber`
- `CombatantId → PlayerTurnData`：每名玩家的当前能量与结束行动标记
- `CurrentActingEnemyId`：仅敌人行动阶段存在
- 权威命令序号、当前执行命令与已确认待执行数量

规则：

- 玩家输入和系统/敌人阶段推进经同一个 `BattleCommandQueue.Submit` interface 建立权威命令顺序；Effect 以及阶段内抽牌、弃手、重洗是当前命令内部的有序操作并进入同一执行结果，不单独伪造系统命令。UI 不直接调用阶段或卡区写入口。
- 命令提交不等待其他玩家输入或当前效果展示；已确认命令按权威序号一次执行和展示一条，共享状态不并行修改。
- 提交接受不等于执行成功；最终合法性以命令到达队首时的权威状态为准。
- 玩家命令只属于提交时的轮次；跨轮后即使新一轮事实重新满足条件也返回 `PlayerActionWindowExpired`，且不写能量、卡区或阶段。
- `BattleTurnController` 只在队首命令执行期间写阶段、能量和行动结束状态。
- 战斗开始只执行一次初始化；首次与后续 `PlayerRoundStart` 都把每名玩家能量重置为 3，并抽到目标手牌数。
- `PlayerAction` 才允许玩家命令在执行期通过阶段校验；玩家之间不设固定行动顺序。
- 单名玩家结束行动后只锁定该玩家并弃掉其剩余手牌；全部玩家结束后才进入敌人阶段。
- 敌人按稳定的行动顺序逐个执行；不依赖字典遍历顺序，行动顺序必须成为明确事实。
- 每个状态的进入和退出只承担本阶段职责，避免一个“大 Update”同时修改所有系统。

当前 BattleScene 只接入一名玩家，不实现联网、输入仲裁或多玩家 UI。完整分步与测试 seam 见 `plans/2026-07-31-m4-turn-scheduling-energy.md`。

测试重点是并发提交不阻塞、权威序号与执行/展示顺序一致、执行期重新校验、玩家命令不能跨提交轮次、多人结束门槛、每玩家能量隔离、死亡敌人跳过，以及一帧内连续转换不会重复进入。

### M5 · 敌人生成、意图与随机行为

M5 复用 Encounter 既有敌人生成和 M4 权威顺序，只增加行为模板、当前意图、独立确定性选择与 M3D HUD；完整计划和验收分别见 `plans/2026-08-01-m5-enemy-intents-deterministic-behavior.md` 与 `06_testing/2026-08-01-m5d-full-validation-review.md`。

- 敌人模板引用一个行为组。
- 行为组以稳定顺序引用行为模板；行为模板包含意图类型、目标规则、既有 `CardEffect` 引用、正整数权重、冷却选择次数和最大连续次数。
- 当前意图只保存已经选定的 `BehaviorId`；意图类型、目标、图标和预测数值从静态模板与当前参与者事实派生。
- `BattleEnemyIntentsData` 拥有每名敌人的最小选择历史和敌人行为专属 `GameRandom`；洗牌、地图或奖励不得共享该实例。
- HUD、命令队列与未来 Effect 执行读取同一个当前意图，读取、订阅、语言变化、力量变化或 View 重建不得重新随机。

选择流程：

1. 初始选择严格按 Encounter 敌人顺序执行；候选严格按行为组显式顺序收集。
2. 按最大连续次数与冷却历史过滤；第一版没有通用条件 DSL 或可选前置条件抽象。
3. 单候选不消费随机；多候选使用专属随机流执行一次稳定整数加权选择。
4. 一次发布完整、不可变的 `CombatantId → BehaviorId` 快照。
5. 合法完成当前敌人行动时，先原子选择并发布该敌人的下一意图，再保证推进既有 Encounter 顺序。
6. 错误、重复、死亡跳过或无候选不得部分推进；配置错误显式失败，不随机回退或静默跳过。

第一版默认 Encounter 包含一个固定攻击敌人与一个攻击/防御加权随机敌人。意图 HUD 使用正式五类图标，并与卡牌文本共用 `BattleEffectValueCalculator` 解释当前可计算数值。M7 建立玩家与敌人共用的 Effect/公式/结算 seam，M8 已完成敌人真实执行、状态时机、死亡中止与终局，`DEP-009` 已 resolved。

### M6 · 出牌命令、合法性与目标选择

M4 已经把旧“越线即删除”替换为权威 `PlayCardCommand`，并完成阶段、手牌、费用、能量、提交轮次和队首执行期校验。M6 不重做该基础，而是在同一命令和 `BattleCommandQueue.Submit` seam 上补充显式目标、派生预览、目标失效重校验与当前单玩家 UI；完整实施边界见 `plans/2026-08-01-m6-card-play-legality-target-selection.md`。

```text
PlayCardCommand
  ActorId
  CardId
  TargetId?
```

队首在 M4 既有顺序后统一补充验证：

- 当前是否为 `PlayerAction`。
- 卡牌实例是否仍在手牌。
- 能量是否足够。
- 当前双方是否仍各有存活参与者；只派生可否出牌，不提前实现胜负流程。
- 目标规则是否满足。
- 目标是否存在且存活。

M6 的独立停止点只验证合法后扣能量并归堆；该临时闭环现已由 M7 的“完整预构建 → 支付能量 → 按绑定顺序执行 Effect → 当前卡牌归堆”事务替代。失败仍不改变任何权威状态，UI 回弹并展示原因；`CanPlayCard`、目标高亮与费用颜色继续只是派生预览，最终以命令到达队首时的事实为准。

目标交互建议：

- `Self`：越线松手，由 UI 自动提交玩家自身 `CombatantId`。
- `Enemy`：越线后显示功能性目标箭头和合法敌人高亮，松手命中存活敌人后提交。
- 第一版命中复用 `BattleParticipantPresenter` 的唯一 View 映射，把世界角色 `SpriteRenderer.bounds` 投影为屏幕矩形；不增加 Collider、Physics2D Raycaster 或第二套参与者注册表。
- 未来的 AllEnemy/RandomEnemy 不复用单目标命中结果，由目标解析规则派生目标集合。

M6A～M6D 已串行完成：显式 Self/Enemy 目标、UI/队首共享规则、目标失效零写入、功能性箭头/高亮/命中、费用不足视觉拖动，以及定向/全量 EditMode、Addressables、Bootstrap、真实 Game View 与双轴复审均有证据；详见 `06_testing/2026-08-02-m6d-full-validation-review.md`。`DEP-001` resolved，`DEP-002` 已由 M4D 解决。目标选择发生在 Submit 前，不解决命令执行中途等待局部输入的 `DEP-010`。

### M7 · Effect 执行器（已完成）

M7A～M7E 已按唯一实施计划 `plans/2026-08-02-m7-effect-executor.md` 串行完成；最终自动验证、Bootstrap、真实 Game View、范围审计与双轴复审见 `06_testing/2026-08-02-m7e-full-validation-review.md`。当前动态状态仍只查 `SESSION_LOG.md`。

效果器是纯计算/状态写入边界，不负责动画、拖拽或查找场景对象。

已落实的事务顺序：

1. `PlayCardCommand` 通过 M6 同一规则完成队首重校验。
2. 在首次写入前按卡牌模板顺序完整预校验 `effect_bindings`。
3. 复用 M6 Self/Enemy 目标得到稳定的单个显式 `CombatantId`。
4. 每个效果转换为明确的运行时操作。
5. 操作依次写入权威状态，并产生只读结算记录供表现层消费。
6. 当前正式卡牌完成后进入弃牌堆；配置尚无 Exhaust 归宿字段，见 `DEP-012`。

MVP 效果类型：

- `DealDamage`
- `GainBlock`
- `ModifyAttribute(Strength)`
- `ApplyVulnerable`

必须先定义时机和公式：

- M7 伤害 = `max(0, 卡牌基础伤害 + 力量)`；目标易伤时乘 `3/2` 并向下取整，M7 不实现虚弱。
- 格挡先吸收伤害，剩余才扣生命。
- 格挡与易伤是权威事实；M8 已把清理/衰减接入命令调度并产生有序结算，UI 仍不得保存第二份数字。
- 多效果卡严格按表中 `effect_bindings` 顺序执行。

测试以纯 C# 为主：Strength、Strike、Defend、Bash、致死、格挡溢出、易伤倍率、无效目标、失败零写入、多效果顺序和阶段卡区记录。表现层只读取结算记录；数字、抖动和状态图标仍属于 M3E/M9。

### M8 · 敌人行动与完整循环

唯一实施计划：[`plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md`](plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md)，现已归档。M8A～M8E 已按串行停止点完成；最终自动验证、Bootstrap、真实 Game View、范围审计与双轴复审见 [`06_testing/2026-08-02-m8e-full-validation-review.md`](06_testing/2026-08-02-m8e-full-validation-review.md)，动态状态仍只见 `SESSION_LOG.md`。

敌人行为已走与卡牌相同的 ordered Effect、显式目标和共享公式边界，没有复制第二套伤害写链。

已完成：

- 行动前目标仍有效。
- 敌人死亡后跳过行动。
- 玩家中途死亡立即终止剩余敌人行动。
- 多敌人的稳定行动顺序。
- 敌人行动反馈结束后再进入下一敌人或下一回合。
- 状态的回合开始/回合结束触发与衰减。
- 整个调度器等待表现层的必要动画屏障，但权威状态不由动画回调决定。
- 已把 Hand/Turn HUD 各自维护的 `Submit → pending → PublishQueued → feedback` 协议收敛为统一 coordinator；权威序号只由 Queue 对账，View 不再承担调度身份。该改造已与 Queue 生命周期、按结算形成的表现屏障和唯一 `Queued` 协议裁决一并完成。

验收已通过：当前双敌 Encounter 可用初始卡组完成多回合胜利或失败；抽牌、弃牌、能量、格挡、状态、意图、敌人行动、死亡、反馈屏障与规则层终局全部闭环。最终视觉反馈、胜负面板与重开已由 M9 承接。

### M9 · STS 式反馈、胜负与重开

归档计划：[`plans/2026-08-02-m9-sts-feedback-outcome-restart.md`](plans/2026-08-02-m9-sts-feedback-outcome-restart.md)。M9A～M9G 已串行完成；ordered settlement 表现、M3E HUD、目标聚焦、卡区运动、阶段横幅、终局面板、同种子重开与退出应用均已接入。M9G 自动/生产重跑、仓库外 Player 退出、范围审计与 Standards / Spec 零 finding 见 `06_testing/2026-08-02-m9g-full-validation-review.md`；M3E/M9 完成，动态状态只见 `SESSION_LOG.md`。

表现层至少包含：

- 卡牌不可用灰化、费用不足提示和合法目标高亮。
- 出牌轨迹、卡牌飞向目标/弃牌堆。
- 伤害数字、格挡数字、受击抖动、死亡过渡。
- 状态图标与层数、敌人意图图标和预测伤害。
- 抽牌/弃牌/重洗动画。
- 玩家回合/敌人回合横幅。
- 胜利/失败面板、重新开始和退出入口。

反馈只消费已产生的结算记录；不能在动画脚本里再次扣血或修改牌堆。动画中断、加速或跳过后，状态仍必须正确。

### M10 · 对标、回归与扩展入口

唯一实施计划为 `plans/2026-08-05-m10-battlescene-conformance.md`，按以下切片串行执行：

| 切片 | 独立交付物 | 依赖 | 不提前实施 |
|---|---|---|---|
| M10A | 配置原子性、表清单漂移校验与 typed fail-fast（已完成；证据见 `06_testing/2026-08-05-m10a-config-fail-fast.md`） | 已完成 M0～M9 | Bootstrap UI、表格/本地化内容、规则 |
| M10B | Bootstrap 可见失败路由、数值/两语文本黄金基线与构建前内容校验（已完成；证据见 `06_testing/2026-08-05-m10b-bootstrap-golden-baseline.md`） | M10A | 新玩法、Run、主菜单 |
| M10C | 30/60/120 FPS 确定性轨迹与 Session/订阅/Tween 生命周期回归（已完成；证据见 `06_testing/2026-08-05-m10c-determinism-lifecycle.md`） | M10B 的稳定启动基线 | Queue/Turn/结算契约重写、第二事实 |
| M10D | 已完成交付级验证与可重复性能基线；完整 EditMode 的两项非 M10 UI/Targeting 套件异常已独立复现并记录（证据见 `06_testing/2026-08-05-m10d-delivery-validation.md`） | M10A～M10C | 未确认预算下的猜测性优化、G1+ 内容或未经授权的 UI/Targeting 修复 |

最终统一核对：

- 初始手牌 5、每回合 3 能量、回合抽牌与弃牌规则。
- 战士初始卡组 5×Strike、4×Defend、1×Bash。
- Strike/Defend/Bash 数值与项目目标版本一致。
- 至少验证默认语言与第二语言；四张初始卡的 name/description key、参数替换、语序和缺失 key 回退全部通过。
- 玩家和敌人的生命、力量及行为数值来自表格，不残留 Inspector/代码常量。
- 固定种子的战斗可重放；随机行为不依赖帧率或 Unity 全局随机状态。
- 30/60/120 FPS 下拖拽和动画不改变规则结果。
- 场景重复进入退出不残留旧 `BattleSession`、订阅或 Tween。
- EditMode 覆盖状态与计算；PlayMode 覆盖 DI、场景生成、UI 绑定与完整单战斗冒烟。
- 配置地址缺失、JSON 损坏或规则表预加载失败必须显式进入启动失败路径，`ConfigService` 不再以代码默认值静默继续；手工 `TableNames` 清单同时改为生成或构建期校验，并覆盖 Bootstrap 失败路径测试。

## 6. 实施顺序与切片大小

每个阶段继续拆成可独立验收的 tracer slice：

1. 先写纯状态与规则测试。
2. 再接 `BattleLifetimeScope`。
3. 再接一个最薄 UI。
4. 最后跑 Bootstrap → BattleScene 实际加载。

禁止把 M2～M8 合并成一次“大重构”。推荐一次只交付一个可观察结果，例如“抽牌堆能重洗”“遭遇能生成两个敌人”“结束回合会弃牌并进入敌人阶段”。

## 7. 阶段交接

BattleScene MVP 的完成定义为：M0～M10 全部验收，可独立完成一场具备完整规则、操作与反馈闭环的战斗。2026-08-14 检查点记录为完整 Unity EditMode **807/807 passed**。

当前 UI、视觉反馈与动画是可用的功能基线，不是最终品质标准；这些表现债保留在后续切片和产品收尾阶段处理，不阻塞游戏本体进入 Run 阶段。

地图、Run 生命周期、存档、奖励、遗物、药水、商店、事件、休息点、宝箱、Act 与 Boss 的阶段骨架已迁移到 [RUN_ROADMAP.md](RUN_ROADMAP.md)。该新路线图本身不授权实现；G1 以及其后每个切片、必要时每个子切片，都必须先完成针对该范围的 Grill 与明确授权。
