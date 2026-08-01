---
title: M5 敌人意图与确定性行为选择
page_type: plan
lifecycle: archived
date: 2026-08-01
updated: 2026-08-01
scope: TinySpire 敌人行为配置、运行时意图、确定性选择、M4 队列接线与 M3D HUD
source: 用户确认将 M5 拆为 M5A～M5D，并以一个总 Goal 无人值守串行执行
status_source: ../SESSION_LOG.md
depends_on: 2026-07-31-m4-turn-scheduling-energy.md（M4 已完成）
---

# M5 敌人意图与确定性行为选择

## 当前结论

M5 使用**一份总计划和一个总 Goal**执行，Goal 内按 M5A～M5D 串行推进。每个切片都必须先满足自己的停止点验收，才能继续下一切片；无人值守不代表可以跳过失败、扩大范围或绕过 Unity、工作簿和 Git 安全规则。

当前项目已经从 `Encounter` 创建敌人运行时数据，并由 `BattleParticipantPresenter` 按 Encounter 顺序实例化敌人 View；M5 不再创建第二套“敌人生成系统”。M5 只在现有生成链上补充行为组引用、运行时当前意图、确定性选择及意图 HUD。

M4 已经建立 `BattleCommandQueue`、稳定的 `CurrentActingEnemyId` 和 `CompleteEnemyActionCommand`。M5 替换当前“无行为敌人直接完成”的内容，但不替换命令队列和回合阶段根，也不提前实现 M7/M8 的真实 Effect 与敌人行动结算。

## 推荐 Goal 文案

> 完成 TinySpire BattleScene M5：在复用现有 Encounter 敌人生成、M4 权威命令队列和敌人行动顺序的前提下，按 M5A～M5D 串行加入最小敌人行为配置、独立确定性随机流、每名敌人的权威当前意图、行动完成后的下一意图选择，以及 M3D 敌人意图 HUD。第一版包含一个固定行为敌人和一个加权随机敌人；不实现真实 Effect、伤害、格挡、状态、胜败、行为树或通用条件 DSL。每个切片完成独立验收和文档同步后再继续；最终完成 Luban、Addressables、定向与全量 EditMode、串行构建、Bootstrap 实跑和双轴复审。保护已有工作区改动，不提交、不推送；遇到工具不可用、Unity 占用冲突、测试失败无法安全修复或需要扩大范围时停止并报告。

## 无人值守执行规则

1. Goal 启动后先重新读取根 `AGENTS.md`，执行 `git status --short`，记录并保护所有已有改动。
2. 严格按 M5A → M5B → M5C → M5D 执行；每步完成对应停止点验收后才进入下一步。
3. 测试失败时只修复本切片引入的问题，不借机重构 M4、DI、程序集、启动流程或其他系统。
4. 工作簿编辑能力、Unity MCP、现有 Unity Editor 或 Addressables 构建若被阻塞，不得安装替代包、启动第二个 Editor、结束用户进程或删除锁文件。
5. 无候选行为、配置引用错误和确定性不一致必须显式失败；不得增加随机回退或静默跳过规则。
6. 不因用户暂时离线而 commit、push、清理、还原或覆盖任何现有改动。
7. 若 M5A～M5C 均完成但人工 Game View 视觉确认缺失，保留 Goal 未完成并准确报告待验收项。

## 目标

- 敌人静态模板引用一个行为组；行为组以稳定顺序引用行为模板。
- 第一版配置一个固定行为敌人和一个加权随机敌人。
- 每名敌人在玩家首轮开始前拥有一个已经选定的当前意图。
- 当前意图只保存选中的 `BehaviorId`；行为类型、目标、效果与展示数值从静态模板及当前战斗事实派生。
- HUD、命令队列和未来 Effect 执行入口读取同一个当前意图，不在读取或展示时重新随机。
- 合法完成敌人行动后恰好选择一次下一意图；错误、重复或过期完成不推进历史、意图或随机流。
- 敌人行为使用独立 `GameRandom`，不与洗牌、地图或奖励共享实例。
- M3D HUD 显示敌人意图图标和当前可计算数值，不持有玩法事实镜像。

## 明确排除

- 不重新实现 Encounter 敌人创建、敌人 View 工厂或 Encounter 顺序。
- 不实现真实 Effect、伤害、格挡、状态、Buff、Debuff、死亡动画、胜败或奖励。
- 不实现卡牌目标选择、敌人多目标选择或多人生产装配。
- 不实现行为树、状态树、脚本语言、反射条件、通用条件 DSL 或可热更新 AI 代码。
- 不为没有当前样例的“可选前置条件”提前建立抽象；出现真实条件规则后再扩展。
- 不新增 `IEnemyAI`、`ICandidateCollector`、`IWeightPicker` 等只有一个实现的假想 seam。
- 不在 M5 重命名整套 `battle.CardEffect` Luban 表；敌人行为先通过 `effect_id` 复用同一效果数值事实。
- 不修改 asmdef、ProjectSettings、HybridCLR、网络协议、Run 生命周期或启动流程。
- 不解决 `DEP-007` 的 Run 根种子来源；当前仍从 `BattleSetupOptions.RandomSeed` 派生本场各随机域。
- 不把 `DEP-009` 提前标记为完全解决；真实敌人 Effect 执行仍等待 M7/M8。

## 现有基础与接入位置

- `TinySpire/Assets/Scripts/Battle/BattleSession.cs`
  - 已按 `Encounter.EnemyTemplateIds` 创建敌人并保存 `EnemyCombatantIdsInEncounterOrder`。
  - M5 在该会话中增加敌人意图聚合的创建、公开和释放。
- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandQueue.cs`
  - 保持唯一战斗写入 interface。
  - M5 只让当前敌人的合法完成命令协调意图推进，不增加 UI 直通写入口。
- `TinySpire/Assets/Scripts/Battle/Turn/BattleTurnController.cs`
  - 已负责 Encounter 顺序、死亡跳过和 `CurrentActingEnemyId`。
  - 不把候选收集、权重或随机算法放进该阶段模块。
- `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`
  - 已注册 Session、命令队列和逐帧驱动。
  - M5 复用现有注册结构，不重写 DI 架构。
- `TinySpire/Assets/Scripts/UI/Battle/ParticipantHudView.cs`
  - 已从参与者事实派生名称、生命和力量。
  - M3D 在同一静态 HUD Prefab 中为敌人增加只读意图投影。

## 静态配置模型

```text
Enemy
  └─ behavior_group_id
       └─ EnemyBehaviorGroup.behavior_ids（稳定顺序）
            └─ EnemyBehavior
                 ├─ intent_type
                 ├─ target_rule
                 ├─ effect_id
                 ├─ weight
                 ├─ cooldown_selections
                 └─ max_consecutive
```

计划新增或修改：

- `battle.enemy.xlsx`
  - 新增 `behavior_group_id`。
- `battle.enemy_behavior_group.xlsx`
  - `id`。
  - `behavior_ids`：有序行为模板 ID 列表；选择顺序不依赖生成表字典枚举。
- `battle.enemy_behavior.xlsx`
  - `id`。
  - `intent_type`。
  - `target_rule`。
  - `effect_id`：引用现有 `battle.CardEffect`，不复制效果数值。
  - `weight`：正整数权重。
  - `cooldown_selections`：执行该行为后，需要跳过的后续选择次数。
  - `max_consecutive`：`0` 表示不限，正数表示最大连续执行次数。
- `__tables__.xlsx`
  - 登记行为组与行为表。
- `__enums__.xlsx`
  - 新增 `EnemyIntentType`：`Attack`、`Defend`、`Buff`、`Debuff`、`Special`。
- `battle.encounter.xlsx`
  - 默认最小验收 Encounter 包含固定行为敌人与加权随机敌人。

静态表只描述可复用模板，不保存 `CombatantId`、当前意图、连续次数、冷却历史或随机流状态。

## 运行时事实与深模块

新增一个战斗内聚合，建议命名为 `BattleEnemyIntentsData`。它是 in-process 深模块，不增加 interface 类型或 adapter 层。

它拥有：

- `CombatantId → BehaviorId` 的当前意图映射。
- 每名敌人为连续次数和冷却判断所需的最小历史。
- 一条敌人行为专属 `GameRandom` 及其当前 `uint State`。
- 一份完整、不可变、只读响应式意图快照。

它不拥有：

- 敌人 Encounter 顺序；顺序继续由 `BattleSession.EnemyCombatantIdsInEncounterOrder` 持有。
- 生命、力量、存活、回合阶段或当前行动敌人的镜像。
- 图标、翻译文本、预测数值或 UI 可见性。
- Effect 执行结果、伤害记录或动画状态。

建议 interface 只覆盖：

```text
Layout -> ReadOnlyReactiveProperty<EnemyIntentLayoutData>
RandomState -> uint
CompleteAndSelectNext(CombatantId enemyId)
```

候选收集、限制过滤、整数权重选择、历史推进和快照发布全部留在实现内部。生产调用方与测试通过同一个 interface 观察行为，不为测试暴露内部过滤步骤。

## 选择规则

1. 初始意图按 Encounter 顺序为每名敌人选择；不得枚举参与者字典决定随机消费顺序。
2. 从行为组的有序 `behavior_ids` 收集模板并验证引用。
3. 按该敌人的已执行历史过滤连续次数和冷却限制。
4. 候选只有一个时直接选择，不推进随机流。
5. 多个候选以整数权重累计区间，并调用敌人行为专属 `GameRandom.NextInt(totalWeight)` 一次。
6. 当前意图发布后，读取、订阅和 UI 刷新不推进随机流。
7. 敌人合法完成当前行动时，先以当前行为更新历史，再选择并原子发布下一意图。
8. 错误敌人、错误阶段、重复完成或过期完成不得调用意图推进入口。
9. 无合法候选属于配置契约错误：显式失败并停止该命令链，不修改意图、历史或随机状态。
10. 规则随机不得使用 `UnityEngine.Random`、帧时间、Dictionary 枚举顺序或平台相关 `GetHashCode`。

敌人行为随机种子必须从当前战斗种子以稳定、非零的命名域盐派生。现有洗牌仍使用原有种子和实例，保证 M5 不改变既有初始手牌序列。

## 分步实施

### M5A · 行为配置与确定性选择核心

状态：**已完成并通过独立验收（2026-08-01）**。六份 Luban 工作簿已按 spreadsheet 工作流编辑并完成渲染/公式复核；Luban 已成功生成新增 C# 与 `Assets/GameData` JSON。`BattleEnemyIntentsData` 使用独立命名域随机流、Encounter 稳定顺序、不可变完整快照与原子失败回滚；Unity MCP 定向 EditMode 18/18 通过，Console Error 为 0。详细证据见 `../06_testing/2026-08-01-m5a-enemy-behavior-selection.md`。本切片未修改命令队列、场景、Prefab 或 HUD；M5B 尚未实施。

范围：

1. 按 spreadsheet 工作流修改 Luban 工作簿；不得使用未批准的替代库。
2. 新增行为表、行为组表、意图枚举和两个最小敌人样例。
3. 运行 Luban，更新生成 C# 与 `TinySpire/Assets/GameData/` JSON。
4. 更新 `ConfigService` 必需表名和相应配置加载测试。
5. 实现 `BattleEnemyIntentsData`、不可变意图快照、最小历史和专属随机流。
6. 使用现有 `effect_id` 作为行为数值唯一来源；不实现 Effect。
7. 新增纯 C# EditMode 行为测试。
8. 所有新增函数至少包含中文注释。

停止点验收：

- Luban 成功生成新表 C# 与 JSON。
- Enemy、BehaviorGroup、Behavior、Effect 引用全部有效。
- 固定行为始终选择同一行为且不推进随机流。
- 同种子、同配置产生相同加权序列。
- 洗牌与敌人行为随机实例互不推进。
- 连续次数、冷却、权重边界与无候选策略通过测试。
- 失败不会部分修改当前意图、历史或随机状态。
- 本切片不修改命令队列、场景、Prefab 或 HUD。

### M5B · BattleSession、M4 队列与生产接线

状态：**已完成并通过独立验收（2026-08-01）**。`BattleSession` 创建、公开并释放同一敌人意图聚合；M4 队列按“纯校验 → 下一意图原子选择 → 保证推进 Encounter 顺序”执行合法完成，公共 `Submit` / `Queue` / `Turn` 不变，驱动仍每帧最多提交一名当前敌人。Unity MCP 相关 EditMode 47/47 通过；本地 Addressables 构建成功。Bootstrap 生产路径从 Round 1 完成两轮并进入 Round 3，敌人生命未变化，Console Error/Warning 为 0。详细证据见 `../06_testing/2026-08-01-m5b-session-command-queue-wiring.md`。本切片未修改场景或 Prefab；M5C 尚未实施。

范围：

1. `BattleSession` 在创建敌人后创建并持有 `BattleEnemyIntentsData`，销毁时统一释放。
2. `BattleCommandQueue` 接收同一意图聚合；公共 `Submit`、`Queue`、`Turn` interface 保持不变。
3. `CompleteEnemyActionCommand` 到达队首后，先沿用 M4 的阶段、敌人身份和当前行动者校验。
4. 校验成功后完成当前意图并选择下一意图，再推进 Encounter 顺序。
5. 错误或重复命令不得改变意图、历史、随机状态、阶段或当前行动敌人。
6. `BattleCommandRuntimeDriver` 继续在队列空闲时每帧最多提交一名当前敌人，不直接执行随机选择。
7. 更新当前“无行为敌人”注释，明确剩余缺口属于 M7/M8 Effect 执行。
8. 增加队列、轮次、Session、驱动和确定性集成测试。

停止点验收：

- 战斗创建后所有 Encounter 敌人都有初始意图。
- 当前意图在完成前保持稳定，重复读取不推进随机。
- 合法完成恰好推进一次该敌人的意图与 Encounter 顺序。
- 完成第一名敌人不会修改第二名敌人的当前意图。
- 错误敌人、错误阶段和重复完成全部失败且无事实写入。
- 死亡敌人继续由 M4 跳过，不为其补选新意图。
- Bootstrap 可运行至少两个完整轮次，当前仍不产生真实敌人伤害。
- 本切片不修改场景和 Prefab。

### M5C · M3D 敌人意图 HUD

状态：**已完成并通过独立验收（2026-08-01）**。`ParticipantHudView.prefab` 已使用五类正式意图 Sprite 增加静态意图子树；玩家隐藏、敌人从同一权威 `BehaviorId` 与当前战斗事实派生图标和数值。HUD、共享效果值、Prefab 合约、Session 与队列定向 EditMode **39/39** 通过，Bootstrap 生产 Encounter 首轮及下一意图与 HUD 一致；Game View 已用不保存资产的运行期布局夹具目视确认 1～3 名敌人无 HUD 重叠，干净复跑 Console Error/Warning 为 0。验收见 `../06_testing/2026-08-01-m5c-enemy-intent-hud.md`。

使用现有正式资源：

- `TinySpire/Assets/Arts/Runtime/UI/Battle/ui_battle_intent_attack.png`
- `TinySpire/Assets/Arts/Runtime/UI/Battle/ui_battle_intent_defend.png`
- `TinySpire/Assets/Arts/Runtime/UI/Battle/ui_battle_intent_buff.png`
- `TinySpire/Assets/Arts/Runtime/UI/Battle/ui_battle_intent_debuff.png`
- `TinySpire/Assets/Arts/Runtime/UI/Battle/ui_battle_intent_special.png`

所有带 `_ref_` 的参考图不得进入生产 UI。

范围：

1. 使用 Unity MCP 在 `TinySpire/Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab` 静态增加敌人意图根节点、图标和数值文本。
2. 玩家 HUD 隐藏意图节点；敌人 HUD 绑定同一意图快照。
3. 图标从 `EnemyIntentType` 派生；数值从行为的 `effect_id` 和当前 `CombatantData` 派生。
4. 将现有 `CardValueCalculator` 最小深化为共享的 `BattleEffectValueCalculator`，卡牌文本和敌人意图共用同一计算入口。
5. 力量变化时重新派生攻击预测值；不保存预测值镜像。
6. 死亡敌人隐藏意图，不在本切片实现死亡动画。
7. 优先只修改 HUD Prefab；没有必要不得修改 `BattleScene.unity`。
8. 增加纯展示测试与实际 Game View 验收。

停止点验收：

- 固定敌人显示与其 Behavior 一致的意图图标和数值。
- 随机敌人的显示与当前 `BehaviorId` 一致。
- 敌人完成行动后 HUD 显示下一意图。
- 玩家 HUD 不显示意图。
- 力量、意图或 View 重建只重派生显示，不推进随机。
- 1～3 名敌人的意图不与名称、生命和力量 HUD 重叠。
- 运行期 Console 不出现缺失引用、`InvalidKey` 或 VContainer 错误。

### M5D · 全量验证、复审与文档收口

状态：**已完成并通过全量验收与 Standards / Spec 双轴复审（2026-08-01）**。最终 Luban 与 Addressables 成功；M5 定向 EditMode **73/73**、全量 EditMode **98/98**、串行 `dotnet build -m:1` 0 error；两次同种子 Bootstrap 得到相同的 Encounter 行动与意图发布序列，Console Error/Warning 为 0。首轮双轴复审各自只指出同一项“最终状态源尚未回填”的 P2，已通过本状态、`SESSION_LOG.md` 与验收页收口修复；没有代码、规格或 scope finding。验证证据见 `../06_testing/2026-08-01-m5d-full-validation-review.md`。

自动验证：

1. M5 配置、选择核心、Session、队列、回合和 HUD 定向 EditMode。
2. 全量 EditMode。
3. 串行运行：

   ```text
   dotnet build TinySpire/TinySpire.sln --no-restore -m:1
   ```

4. 因修改 `DataTables/Datas/`，确认已运行 Luban，目标 JSON 位于 `TinySpire/Assets/GameData/`。
5. 因生成 JSON、Prefab 和可寻址内容变化，执行 `TinySpire/Addressables/Build Local Content`。
6. 检查构建报告 `BuildError` 为空，并确认完整稳定地址仍有效。

Bootstrap 实跑至少记录：

- 初始两个敌人的 `BehaviorId` 和 `EnemyIntentType`。
- 第一轮 EnemyAction 的 Encounter 顺序。
- 每名敌人完成后产生的下一意图。
- 第二轮开始时 HUD 展示与运行时事实一致。
- 退出重进并使用相同种子时得到相同序列。
- Console Error、Warning、`InvalidKey` 与 VContainer 错误数量。

文档收口：

- 更新本计划各切片状态。
- 更新 `../SESSION_LOG.md`。
- 新口径写入 `../CODE_DECISIONS.md`，不改写历史决策。
- 更新 `../DEPENDENCIES.md`；`DEP-009` 保持 open，并明确剩余工作是 M7/M8 的真实 Effect 执行。
- 为 M5A～M5D 在 `../06_testing/` 写入对应验收记录。
- M5 全部完成后更新 `../ROADMAP.md`。
- 完成 Standards / Spec 双轴复审，并修复本 Goal 范围内发现的问题。

人工验收：

- Game View 确认图标语义、数值可读性和 1～3 敌人布局。
- 自动化可以证明绑定和事实一致性，但不能代替最终视觉接受。

## TDD 测试 seam

测试只通过生产使用的 module interface 与 `BattleCommandQueue.Submit` 验证行为，不直接断言候选过滤器、随机区间或历史容器的私有结构。

首要用例：

1. 固定敌人的唯一候选不推进随机流。
2. 同种子得到相同加权行为序列。
3. 改变洗牌调用次数不改变行为序列。
4. 改变行为选择次数不改变洗牌随机状态。
5. 最大连续次数过滤后不会继续选择同一行为。
6. 冷却期结束前行为不可选，结束后重新成为候选。
7. 无候选时显式失败且所有事实保持原值。
8. 当前意图重复读取不重新随机。
9. 错误和重复完成命令不推进意图或回合。
10. 多敌人按 Encounter 顺序行动，单名敌人完成只推进自身历史。
11. 死亡敌人被跳过且不消费行为随机。
12. UI 重建、语言变化和力量变化不改变 BehaviorId。

## 预期文件范围

M5A：

- `DataTables/Datas/__tables__.xlsx`
- `DataTables/Datas/__enums__.xlsx`
- `DataTables/Datas/battle.enemy.xlsx`
- `DataTables/Datas/battle.enemy_behavior_group.xlsx`
- `DataTables/Datas/battle.enemy_behavior.xlsx`
- `DataTables/Datas/battle.encounter.xlsx`
- `TinySpire/Assets/Scripts/Core/ConfigService.cs`
- `TinySpire/Assets/Scripts/Core/Generated/Config/`
- `TinySpire/Assets/GameData/`
- `TinySpire/Assets/Scripts/Battle/` 下新增敌人意图运行时文件
- `TinySpire/Assets/Editor/Tests/` 下对应测试

M5B：

- `TinySpire/Assets/Scripts/Battle/BattleSession.cs`
- `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`
- `TinySpire/Assets/Scripts/Battle/Commands/`
- `TinySpire/Assets/Scripts/Battle/Turn/`
- 对应 EditMode 测试

M5C：

- `TinySpire/Assets/Scripts/UI/Battle/ParticipantHudView.cs`
- `TinySpire/Assets/Scripts/UI/Battle/ParticipantHudPresentation.cs` 或一个专用纯展示文件
- `TinySpire/Assets/Scripts/Battle/CardValueCalculator.cs` 的最小共享命名调整
- `TinySpire/Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab`
- 对应展示测试

文档：

- `Docs/Copilot_Daedalus/ROADMAP.md`
- `Docs/Copilot_Daedalus/SESSION_LOG.md`
- `Docs/Copilot_Daedalus/CODE_DECISIONS.md`
- `Docs/Copilot_Daedalus/DEPENDENCIES.md`
- `Docs/Copilot_Daedalus/plans/2026-08-01-m5-enemy-intents-deterministic-behavior.md`
- `Docs/Copilot_Daedalus/06_testing/`

如果实际实现需要超出以上范围修改场景、程序集、DI 架构或启动流程，必须停止并等待用户确认。

## 风险与回滚

| 风险 | 控制方式 | 回滚单位 |
|---|---|---|
| 表引用错误导致运行时加载失败 | M5A 完成 Luban、引用测试与 Addressables 构建后再接生产 | M5A 表格、生成代码和 JSON |
| 行为随机与洗牌耦合 | 独立 `GameRandom` 和双向不推进测试 | M5A 意图聚合与种子派生 |
| 字典枚举导致跨平台序列变化 | 只按 BehaviorGroup 和 Encounter 显式顺序消费随机 | M5A 选择实现 |
| 无候选时静默破坏规则 | 配置契约错误显式失败，禁止回退 | M5A 选择实现 |
| 错误或重复命令多选一次意图 | M4 阶段校验成功后才调用唯一推进入口 | M5B 队列接线 |
| UI 保存第二份意图或预测值 | UI 只订阅 BehaviorId 和参与者事实并即时派生 | M5C HUD |
| M5 顺手实现 Effect | 生命、格挡和状态在本 Goal 保持不变，`DEP-009` 继续 open | M5B/M5D 复审 |
| Prefab 修改影响现有 HUD | 只新增敌人意图子树，回归玩家和敌人原有 HUD | M5C Prefab |

每个切片都是独立回滚单位。禁止使用广泛还原、`git reset --hard`、`git clean` 或覆盖用户未提交改动。

## 完成定义

M5 完成不等于敌人已经造成真实伤害。完成标准是：一个固定行为敌人和一个加权随机敌人可以从配置生成；每名敌人拥有唯一、稳定、可复现的当前意图；M4 队列只在合法完成后推进该意图；M3D HUD 从同一事实显示图标和数值；随机域互相独立；配置错误显式失败；Luban、Addressables、测试、Bootstrap 实跑、文档和双轴复审全部完成。

最终交付先展示 review package。未经用户明确确认，不创建 commit，不 push。
