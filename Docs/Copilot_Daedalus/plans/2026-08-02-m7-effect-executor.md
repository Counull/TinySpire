---
title: TinySpire BattleScene M7 · Effect 执行器
page_type: plan
lifecycle: archived
created: 2026-08-02
updated: 2026-08-02
scope: BattleScene M7A-M7E
status_source: ../SESSION_LOG.md
source: ../ROADMAP.md M7；../07_retrospective/2026-08-01-m5-architecture-roast.md；当前 M6 实现与最终验收
---

# TinySpire BattleScene M7 · Effect 执行器

## 当前结论

本页是 M7 实施期间的**唯一实施计划**，现已归档。M7 使用现有 Luban `Card.EffectBindings`、`CardEffect`、Self/Enemy 目标与 M4～M6 权威命令顺序，建立了一个纯 C# Effect 执行 module：完整预校验后，按绑定顺序计算并写入权威参与者事实，同时为每次权威变化产生不可变结算记录。

M7 已按 **M7A → M7B → M7C → M7D → M7E** 串行完成，每个切片均先达到测试、文档和独立停止点再继续。M7 没有实现敌人真实行动、状态时机调度、表现动画、胜负或奖励。

已兑现的代码决策见 CD-039～CD-042；各切片与最终自动验证、Bootstrap、真实 Game View、范围审计及双轴复审证据见 `../06_testing/`，最终收口见 `../06_testing/2026-08-02-m7e-full-validation-review.md`。

## 推荐 Goal 文案

> 完成 TinySpire BattleScene M7 · Effect 执行器。严格以 `Docs/Copilot_Daedalus/plans/2026-08-02-m7-effect-executor.md` 为唯一实施计划，遵守根 `AGENTS.md`、`Docs/Copilot_Daedalus/ARCHITECTURE_CONVENTIONS.md` 以及计划中的范围、公式、失败原子性、停止点与验证要求，按 M7A → M7B → M7C → M7D → M7E 串行执行，每个切片达到独立停止点并完成文档同步后再继续。复用 M4～M6 的 `BattleCommandQueue.Submit`、只读 `Queue`/`Turn`、权威序号、展示屏障、轮次栅栏、`BattleCardPlayRules` 与显式 Self/Enemy 目标；所有 Effect 在首次写入前完整预校验，成功后严格按 `effect_bindings` 顺序写入唯一权威事实并产生不可变结算记录，失败命令必须零写入且结算记录为空。落实 Strike、Defend、Strength、Bash、格挡吸收、易伤 1.5 倍向下取整、致死与多效果顺序，并把出牌事务重塑为“校验与预构建 → 支付能量 → 执行效果 → 当前卡牌进入弃牌堆”。当前配置没有消耗牌归宿字段，禁止硬编码模板 ID 或提前改表实现 Exhaust。阶段内抽牌、弃手牌与重洗继续由现有命令调用栈触发，但必须把明确卡区变化追加到该命令的结算记录；不得在 M7 新增系统命令或重写队列调度。M7 不实现 M8 的敌人 Effect、状态衰减/格挡清理时机、阶段屏障、队列事件化或 pending 协作者，不实现 M3E/M9 的格挡/状态 HUD、伤害数字、抖动、死亡/胜负/奖励/最终动画或 LXX-6 美术接线，也不修改 DataTables、生成配置、Localization、Addressables 内容、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、Run/网络生命周期或 DI 架构。开始前重新读取规则并执行 `git status --short`，记录起始 HEAD 作为双轴复审固定点，保护全部已有改动，特别是 `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/` 未跟踪美术。新增 Meta 优先经当前 Unity MCP 生成；不得启动第二个 Editor、结束用户 Unity 进程、删除锁文件或清理 Library/Temp。最终完成定向与全量 EditMode、串行 solution build、Bootstrap 生产实跑、真实 Game View 物理验收、文档同步和 Standards/Spec 双轴复审；本计划不预计 Luban 或 Addressables 重建，若实际需要修改可寻址内容或配置表必须停止并请求扩大范围。未经明确确认不 commit、不 push；遇到范围扩大、公式与上游设计冲突、工具阻塞或无法形成真实证据时停止并准确报告。

## 无人值守执行规则

1. Goal 启动后重新读取根 `AGENTS.md`、本计划、`../ARCHITECTURE_CONVENTIONS.md`、`../CODE_DECISIONS.md` 中 M4～M6 相关决策、M6 最终验收与 `../07_retrospective/2026-08-01-m5-architecture-roast.md` 的 M7 到期意见。
2. 执行 `git status --short` 和 `git rev-parse HEAD`，记录固定复审点与全部已有改动。当前已知的 `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/` 及目录 Meta 属 M9 美术交付，M7 不修改、不暂存、不删除。
3. 严格按 M7A → M7B → M7C → M7D → M7E 执行；一个切片未达到停止点时，不开始后续切片。
4. 遵守测试先行：先由公开 interface 或新 module interface 的失败用例证明缺口，再做最小实现；不测试私有方法，不为测试把 `BattleTurnController` 公开化。
5. 所有新增函数至少有中文注释；测试名称和断言描述经 interface 可观察的规则、结果、记录顺序和零写入保证。
6. 不引入只有一个 adapter 的假 seam，不创建通用 Effect DSL、反射分发、脚本语言、事件总线或可替换存储 interface。M7 使用具体纯 C# module，并允许其内部拥有不外泄的计算 seam。
7. 测试失败只修复当前切片引入的问题。不得借机清偿复盘中已明确路由到 M8、M3E、M9、M10/G1 的债务。
8. Unity/Meta 验证优先复用当前 Editor 与 Unity MCP；不得启动第二个 Editor、强制结束进程、删除项目锁或清理 `Library`、`Temp`。
9. 本计划不修改 DataTables、生成配置、Localization 或可寻址内容，因此不要求 Luban 与 Addressables 重建。若实现发现必须修改这些内容，视为范围扩大并立即停止请求确认；获准后才按根规则执行生成和本地内容构建。
10. 不因用户暂时离线而 commit、push、清理、还原或覆盖文件。M7E 先展示 review package，等待用户决定是否提交。

## 目标

- 让当前四张卡的正式 `effect_bindings` 进入真实权威结算：Strength、Strike、Defend、Bash。
- 在运行时 Effect 管线内使用强类型 `BattleEffectId`（最终名称可按现有命名统一），只在 Luban 表适配位置与裸 `int` 转换，不把 Effect ID 以 `int` 穿过多层。
- 建立一个小 interface、深实现的具体 Effect 执行 module：调用方提供来源、当前显式目标与有序绑定；module 隐藏表查找、预校验、公式、顺序执行、死亡后跳过和结算记录生成。
- 建立共享纯公式 module。展示层继续获得无目标投影值，真实结算额外读取目标状态；卡牌文本、敌人意图预览与执行不得各写一套基础公式。
- 在 `CombatantData` 的唯一事实内增加 Block 与 Vulnerable；保持 Health、Strength、Block、Vulnerable 都由纯 C# 状态层唯一持有并以只读事实公开。
- 重塑现有 `ApplyDamage` 占位：格挡吸收、生命扣减、致死与记录生成在同一 Effect 写入路径内完成，不保留“外面先扣格挡、里面再扣血”的双层直通。
- `BattleCommandExecutionResult` 携带按发生顺序冻结的结算记录；失败结果记录为空，表现层只能读取记录，不能借记录再次写状态。
- 出牌首次写入前完成卡牌、目标、全部 Effect 绑定和操作参数预校验；任何预校验失败保持能量、卡区、参与者、回合和记录全部不变。
- 成功出牌顺序固定为：队首合法性重校验与 Effect 计划预构建 → 扣能量 → 逐 Effect 写入并记录 → 当前卡牌进入弃牌堆并记录 → 一次发布最新玩家回合快照。
- 阶段进入所触发的抽牌、弃手牌和重洗不新增另一条命令路径；这些变化由原状态 module 返回明确操作结果，并追加到当前队首命令的结算记录，避免 M9 从最终卡区差值猜动画。

## M7 公式与状态口径

以下是本计划的 M7 MVP 验收公式；若上游玩法事实在实施前给出不同数值，必须先停止、更新计划并由用户确认，不能在代码中静默改口径。

### DealDamage

1. 基础攻击值：`max(0, CardEffect.Value + source.Strength)`。
2. 当前目标 `Vulnerable > 0` 时，攻击值乘 `3 / 2`，使用非负整数运算向下取整；未易伤时保持基础攻击值。
3. M7 没有 Weak/Dexterity/遗物/暴击/随机波动，不实现或假装预留对应乘区。
4. 目标 Block 先吸收 `min(Block, attack)`；剩余值才减少 Health，Health 最低为 0。
5. Damage 记录至少能区分：公式后攻击值、格挡吸收量、实际生命损失、变化前后 Block/Health、是否致死。

### GainBlock

- 增加 `max(0, CardEffect.Value)` 点 Block；M7 没有 Dexterity。
- Block 是权威标量事实，不是 HUD 数字。Block 的回合清理时机属于 M8，M7 不在自动阶段中擅自清零。

### ModifyAttribute(Strength)

- M7 只支持 `Attribute.Strength`，按 `CardEffect.Value` 对当前 Strength 做加法。
- 未知 Attribute 在预校验阶段明确失败，不能退化为无操作。

### ApplyVulnerable

- 增加 `max(0, CardEffect.Value)` 层/回合计数；已有值累加。
- `Vulnerable > 0` 即参与 DealDamage 公式。
- Vulnerable 的回合开始/结束衰减属于 M8；M7 只建立权威事实、累加语义和公式读取，不在同步自动阶段中提前接入衰减。

### 多效果与致死

- `Card.EffectBindings` 的数组顺序就是唯一执行顺序，不按 EffectType 重排。
- 首个 Effect 致死后，同一张卡后续针对该死亡目标的操作不回滚已发生效果，也不把整张牌改成失败；它产生明确的 `TargetNotAlive`/Skipped 结算记录并继续完成卡牌归堆。
- 在首次 Effect 写入前就失效的目标仍由 M6 规则拒绝，整条命令零写入且没有结算记录。

## 结算记录契约

M7A 必须先锁定最小不可变记录，再允许 M7B 写状态。具体类型可按现有 C# 版本选择 sealed class/readonly struct，但 observable interface 必须满足：

- `BattleCommandExecutionResult` 暴露只读、冻结、非 null 的记录列表；调用方不能修改列表或记录。
- 每条记录拥有命令内稳定顺序，并可关联 `BattleEffectId?`、来源 `CombatantId?`、目标 `CombatantId?`。
- 记录类型至少覆盖：`EnergySpent`、`DamageApplied`、`BlockGained`、`AttributeModified`、`StatusApplied`、`CardMoved`、`CardsReshuffled`、`OperationSkipped`。
- 每种记录只携带表现与审计真正需要的最小 before/after/applied 信息；不得建立与 `CombatantData`、`BattleCardZonesData` 平行的可变镜像。
- `CardMoved` 明确保存 `CardInstanceId`、来源区与目标区；多张弃手牌按权威手牌顺序记录。
- `CardsReshuffled` 明确保存进入新抽牌堆后的稳定卡牌顺序或足以重放的不可变结果，不让 M9 再调用随机数推测顺序。
- 失败命令记录列表必须为空。合法命令中被前序 Effect 致死而跳过的后续操作属于成功命令内的 `OperationSkipped`，不是命令失败。
- 结算记录是一次执行结果，不保存为第二个可变全局日志；表现 adapter 消费当前结果，状态仍以各聚合的只读事实为准。

## 深 module 与 seam

### 外部保持不变

- `BattleCommandQueue.Submit` 仍是共享战斗写入的唯一外部 seam。
- `Queue` 与 `Turn` 继续只读公开；`BattleTurnController` 继续保持 `internal`。
- UI 继续只组成并提交 `PlayCardCommand`，不解析 Effect、不扣能量、不改参与者或卡区。
- `BattleCardPlayRules` 继续负责 UI 预览和队首合法性；Effect 执行器不复制阶段、能量、手牌或目标规则链。

### 新 module

- 公式 module：纯计算，无 Unity、R3 写入、Tables 查找或场景对象；以一个上下文对象/值集合表达 source、target 和规则参数，避免持续拍宽多参数接口。
- Effect 执行 module：具体纯 C# module，不先抽 `IEffectExecutor`；其 interface 同时供生产调用方和测试使用，隐藏绑定解析、强类型 ID、预校验、操作生成、顺序写入与记录。
- 卡区 module：保留 `BattleCardZonesData` 作为抽牌/重洗/移动的深 module；M7 只让写入方法返回明确不可变操作结果供当前命令收集，不能由队列比较前后布局猜测。

### 禁止的形状

- 不在 `BattleCommandQueue.Execute` 里按 EffectType 堆叠新的长 if/else 写状态。
- 不给每种 Effect 建一个只有一条实现的 public interface/adapter。
- 不把 Tables、R3、Unity View 或 Localization 传入公式 module。
- 不让 `BattleEffectValueCalculator.Calculate(effect, source)` 直接增加 target 等多个可选参数；它应退为共享公式 module 的无目标展示投影。
- 不保留旧 `ApplyDamage` 直通入口与新 executor 两条并行生产写链。

## 明确排除

- 不实现 M8 的敌人行为 Effect；`CompleteEnemyActionCommand` 仍只完成既有意图/顺序交接。
- 不实现 M8 的 Vulnerable 衰减、Block 清理、状态开始/结束触发、阶段停留屏障、死亡中止敌人链或完整战斗循环。
- 不重构 M8 才处理的队列错误态、轮询驱动、固定 0.35 秒表现、Queued 发布协议、pending 唯一 owner 或订阅回调重入规则。
- 不实现 M3E/M9 的 Block/Vulnerable HUD、状态图标、伤害/格挡数字、受击抖动、死亡过渡、胜负、奖励、重开、最终飞牌/弃牌动画。
- 不接入 LXX-6 箭身、箭头或高亮 PNG；这些未跟踪资源继续由 M9 确认导入/切片/缩放/Prefab 契约。
- 不新增 `AllEnemy`、`RandomEnemy`、多目标集合、链式目标、目标重选、运行中输入 token 或通用目标 DSL；M7 只消费 M6 已验证的单个 Self/Enemy 目标。
- 不修改 Card/Effect Luban 工作簿、枚举或生成 JSON；当前正式配置已经包含四类 Effect 与 Strength/Strike/Defend/Bash 绑定。
- 当前 `Card` 没有出牌归宿/Exhaust 字段；M7 所有现有卡成功后进入弃牌堆。禁止按模板 ID、效果类型或卡名硬编码消耗牌，`DEP-012` 保持 open。
- 不修改 Localization、Addressables 组/地址/内容、Scene、Prefab、ProjectSettings、Physics、asmdef、HybridCLR、Run/网络生命周期、第二玩家生产接线或 DI 架构。
- 不处理复盘中路由到 G1/M10 的 Session 装配、Config fail-fast、keyword 常量和构建前 i18n 校验。

## 现有基础与接入位置

| 现有事实或 module | 当前能力 | M7 处理方式 |
|---|---|---|
| `BattleCommandQueue` | 唯一提交 seam、权威序号、队首串行执行、展示屏障 | 保持外部 interface；执行结果增加不可变结算记录 |
| `BattleTurnController.TryPlayCard` | M6 规则通过后直接弃牌、扣能量、发布 Turn | 重塑为预构建后一次成功事务，不复制 M6 合法性 |
| `BattleCardPlayRules` | 从当前权威事实派生 Self/Enemy 合法性和目标 | 继续作为写入前规则；Effect module 只消费已通过的来源/目标 |
| `BattleCombatantsData` / `CombatantData` | 唯一参与者映射；Health、Strength；旧 `ApplyDamage` 占位 | 增加 Block/Vulnerable 唯一事实，重塑伤害写入口 |
| `BattleCardZonesData` | Draw、重洗、弃牌、Exhaust 与原子布局快照 | 返回明确卡区操作结果；不增加全局变化日志 |
| `BattleEffectValueCalculator` | 卡牌文本和敌人意图共用“基础值 + Strength”展示 | 委托给新公式 module 的无目标投影；不自行执行 Effect |
| `Card.EffectBindings` / `TbCardEffect` | 正式有序绑定和四类 Effect 配置 | 在适配点转为强类型 ID并完整预校验 |
| `BattleCommandPresentationAdapter` | 读取当前执行结果并维持 M4 展示屏障 | 只传递/读取记录；M7 不实现动画编排 |

## M7A · 结算记录与公式契约

状态：**已完成（2026-08-02）**。

### 实施

1. 新增运行时 `BattleEffectId` 值类型与 Effect/结算所需的最小枚举和值对象；只在 Luban 适配位置接收裸 `int`。
2. 定义不可变 `BattleSettlementRecord` 体系与只读列表，把列表加入 `BattleCommandExecutionResult`。
3. 先让全部既有命令在没有 M7 写入时返回空记录，保持 M4～M6 行为与 presentation interface 可编译。
4. 定义纯公式 module 的输入/输出 interface，锁定本计划公式、整数取整和无目标展示投影。
5. 让 `BattleEffectValueCalculator` 委托给新公式 module，但保持现有 CardText/EnemyIntent 展示结果不变。
6. 以测试证明记录列表不可变、失败结果为空、公式和现有展示投影一致。

### 停止点

- 尚未写入 Block/Vulnerable，也未执行任何正式 Effect；M6 成功出牌行为暂不改变。
- `BattleCommandExecutionResult` 的新记录 interface 已由 queue/presentation 测试消费，既有调用方全部迁移。
- DealDamage、Vulnerable 取整、零/负基础值、无目标展示值的纯测试通过。
- `BattleEffectValueCalculatorTests`、相关 CardText/EnemyIntent 测试和队列/presentation 回归通过。
- 串行 solution build 0 error，`git diff --check` 通过；新增 Meta 已由当前 Unity 生成。
- 新增 `06_testing/2026-08-02-m7a-settlement-formula-contract.md` 并更新状态源后，才进入 M7B。

## M7B · 参与者权威状态与伤害操作

状态：**已完成（2026-08-02）**。

### 实施

1. 在 `CombatantData` 内新增 Block、Vulnerable 私有 R3 持有者、只读公开事实和同步读取值；构造初值为 0，Dispose 生命周期与 Health/Strength 一致。
2. 建立由 Effect 执行 module 独占的内部状态写入口：GainBlock、ModifyStrength、ApplyVulnerable、ApplyDamage。
3. 删除或重塑旧 `BattleCombatantsData.ApplyDamage → CombatantData.ApplyDamage` 双层直通，确保没有第二条生产伤害写链。
4. Damage 操作一次计算并写入 Block/Health，返回足够生成 Damage 记录的不可变结果。
5. 所有派生列表继续从唯一参与者映射即时计算，不新增存活/死亡/状态镜像。

### 停止点

- 纯状态测试覆盖：初值、Block 累加、Strength 正负修改、Vulnerable 累加、格挡全吸收、格挡溢出、生命最低 0、重复攻击死亡目标的明确结果。
- 公式测试覆盖：Strength、易伤 1.5 倍向下取整、Block 吸收和致死字段。
- 状态写入只能经新 Effect 路径调用；旧 `ApplyDamage` 不再形成平行生产 seam。
- 尚未读取 Card.EffectBindings，也未接入出牌事务、UI、敌人行动或状态衰减。
- 定向 EditMode、现有参与者/HUD 回归、串行 build 与 `git diff --check` 通过。
- 新增 `06_testing/2026-08-02-m7b-combatant-effect-operations.md` 并更新状态源后，才进入 M7C。

## M7C · 有序 Effect 执行 module

状态：**已完成（2026-08-02）**。

### 实施

1. 新增具体 `BattleEffectExecutor`（名称可按现有命名统一），构造时接收 Tables 与参与者唯一事实入口，不创建 public `I*` adapter。
2. 调用 interface 接收来源 ID、稳定目标集合（当前只有已验证的单个目标）和 `CardEffectBinding[]`。
3. 预构建阶段按数组顺序完成：Binding 非空、EffectId 结构合法、表项存在、EffectType 支持、Attribute 合法、数值可解释、来源/目标存在且初始存活。
4. 任一预构建失败返回明确 `BattleCommandExecutionFailureReason`，不扣能量、不改卡区/参与者/回合且记录为空。
5. 预构建成功后按原顺序执行 `ModifyAttribute(Strength)`、`DealDamage`、`GainBlock`、`ApplyVulnerable`，每个操作读取前序操作后的最新事实并追加一条记录。
6. 前序致死后，后续针对同一死亡目标的操作追加 skipped 记录；不回滚、不抛异常、不建立第二次目标选择。

### 停止点

- 纯 executor 测试覆盖 Strength、Strike、Defend、Bash、缺失 Effect、未知类型、未知 Attribute、无效来源/目标和致死后跳过。
- Bash 记录顺序严格为绑定数组顺序；重复执行相同输入只因当前权威事实变化而产生预期差异，不使用随机数。
- 所有预构建失败用例都断言 Health/Strength/Block/Vulnerable 对象和值保持原样，记录为空。
- 卡牌文本与敌人意图的展示基础值继续与执行公式共享 module，未加入目标专用状态镜像。
- 尚未接 `TryPlayCard`，生产出牌仍保持 M6 行为。
- 定向 EditMode、相关全回归、串行 build 与 `git diff --check` 通过。
- 新增 `06_testing/2026-08-02-m7c-ordered-effect-executor.md` 并更新状态源后，才进入 M7D。

## M7D · 出牌事务与卡区结算记录接入

状态：**已完成（2026-08-02）**。

### 实施

1. `BattleTurnController.TryPlayCard` 继续先调用 M6 同一 `BattleCardPlayRules`；规则失败直接返回且记录为空。
2. 规则成功后解析当前 Card/目标，并调用 Effect executor 完成全量预构建；预构建失败仍零写入。
3. 预构建成功后按固定事务顺序：记录并扣除费用 → 执行 Effect 并追加记录 → 指定卡牌进入弃牌堆并追加 CardMoved → 发布一次当前阶段玩家快照。
4. 当前正式 Card 没有 Exhaust 归宿字段，四张卡全部弃牌；不调用 `ExhaustFromHand` 模拟不存在的规则。
5. 深化 `BattleCardZonesData` 写入结果，使 Draw、DiscardHand、DiscardFromHand、Reshuffle 明确返回移动卡 ID/顺序；现有抽牌与重洗算法、随机域和一次布局发布保证保持不变。
6. StartBattle、EndPlayerAction 与最终敌人完成触发的阶段抽牌/弃手牌/重洗，把卡区操作结果追加到当前 `BattleCommandExecutionResult`；不新增系统命令、不改阶段状态机和展示屏障。
7. `BattleCommandQueue.Execute` 只协调命令结果，不按 EffectType 写状态；表现 adapter 收到完整记录后仍按原 completion 回调推进。

### 停止点

- Strength：0 费、Self、力量 +3、卡牌弃置、能量不变，记录顺序正确。
- Strike：1 费、Enemy、按来源 Strength 与目标 Vulnerable 计算，先吸收 Block 再扣 Health，卡牌最后弃置。
- Defend：1 费、Self、Block +5，卡牌最后弃置。
- Bash：2 费、Enemy、先伤害 8 再施加 Vulnerable 2，绑定与记录顺序一致；致死时后续 Vulnerable 明确 skipped。
- 费用不足、目标排队后死亡、卡牌离手、模板/Effect 缺失和跨轮旧命令全部零写入且记录为空；M6 pending 恢复继续按权威序号工作。
- StartBattle 抽牌、EndPlayerAction 弃手牌及发生时的重洗都有明确记录；未从布局差值反推。
- 现有 M2 随机确定性、M4 队列/轮次、M5 意图和 M6 合法性/目标回归全部通过。
- 定向 EditMode、串行 build 与 `git diff --check` 通过；Bootstrap 自动实跑无 Error/InvalidKey/VContainer。
- 新增 `06_testing/2026-08-02-m7d-card-effect-transaction.md` 并更新状态源后，才进入 M7E。

## M7E · 全量验证、真实 Game View、复审与文档收口

状态：**已完成并通过最终验收（2026-08-02）**。最终定向/相关回归/队列/全量 EditMode、串行 build、Bootstrap、真实 Game View、范围审计及 Standards / Spec 复审证据见 `../06_testing/2026-08-02-m7e-full-validation-review.md`。

### 自动验证

1. 运行 M7 结算契约、公式、参与者状态、Effect executor、卡区和命令事务定向 EditMode。
2. 运行 M2～M6 相关回归及全量 EditMode，0 failed、0 skipped。
3. 串行执行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`，记录 error 与既有 warning。
4. 执行 `git diff --check`；按 Goal 启动 HEAD 审查全部 M7 tracked/untracked diff，同时排除并保护 M9 美术。
5. 本计划无 DataTables、生成配置、Localization、Prefab/Scene 或 Addressables 内容变更，因此记录“无需 Luban/Addressables 重建”的范围证据；若实际 diff 出现这些路径，停止并按范围扩大处理。

### Bootstrap 与真实 Game View

1. 从 Bootstrap 生产链进入 BattleScene，确认手牌、HUD、目标与现有 Addressables 加载正常，Console 无 Error、InvalidKey、VContainer 或 Effect 配置错误。
2. 使用真实物理拖拽验证至少一张 Self 卡和一张 Enemy 卡：Strength 后玩家力量 HUD +3；Strike/Bash 后目标生命按自动测试锁定的结果变化；卡牌只在执行成功后离手。
3. 若生产洗牌无法在有限轮次组成 Bash→Strike，可使用不保存资产的运行期夹具提供所需手牌/生命；必须仍经真实 UI、`BattleCommandQueue.Submit` 和生产 Effect module，不得直接调用写入口冒充。
4. Defend 的 Block 与 Bash 的 Vulnerable 当前没有 M3E/M9 HUD；只以自动测试和运行时只读事实验收，不新增临时 UI，也不声称 Game View 已直接看到不可见状态。
5. 验证费用不足卡仍保持 M6 口径：红色且可视觉拖动，但无瞄准、Submit、Effect、卡区/能量/参与者写入，释放回弹。
6. 验证目标致死后生命为 0、后续不再成为 M6 合法目标；死亡动画、胜利面板与战斗终止仍明确缺失并属于 M8/M9。

### 文档与复审

1. 新增 `../06_testing/2026-08-02-m7e-full-validation-review.md`，回填全部任务 ID、计数、构建、Bootstrap、Game View 与无法可视化项。
2. 实现后更新本计划各切片状态、`../SESSION_LOG.md`、`../CODE_DECISIONS.md`、`../DEPENDENCIES.md`、`../ROADMAP.md`、`README.md` 与 `../06_testing/README.md`。
3. 对 Goal 启动 HEAD 之后的完整 M7 diff 做 Standards / Spec 双轴复审；只修复 M7 finding，不提前处理 M8/M9/G1/M10。
4. 复核 `../07_retrospective/2026-08-01-m5-architecture-roast.md`：只采纳已到期 M7 项，记录其余建议仍按原里程碑延期。

### 最终停止点

- M7A～M7D 的独立验收页、M7E 全量证据和双轴复审全部完成。
- Strike、Defend、Strength、Bash 的权威事实、公式、记录顺序、失败原子性和卡牌归堆均经公开 seam 证明。
- Bootstrap 与真实 Game View 证明生产 UI 进入同一 Effect 链；不可见 Block/Vulnerable 未被伪装成视觉验收。
- 没有修改配置表、生成内容、Localization、Addressables 内容、Scene/Prefab、高影响设置或 M9 美术。
- `DEP-009` 只在 M7 共享 Effect seam 实际完成部分回填，敌人真实执行仍保持 open 至 M8；`DEP-012/013` 保持 open。
- 计划、状态源、决策源、依赖账本和验收页不存在互相矛盾的 M7 状态。
- 最终先展示 review package；未经用户明确确认，不 commit、不 push。

## TDD 测试 seam

M7 的测试面按层次固定为：

```text
BattleEffectFormula.Calculate(context)
  -> 纯公式、取整、无目标展示投影

BattleEffectExecutor.Execute(request)
  -> 预校验、顺序状态写入、不可变结算记录

BattleCommandQueue.Submit(PlayCardCommand)
  -> BattleCommandExecutionResult.Settlements
  -> Combatants / CardZones / Turn 只读事实
```

测试不得直接调用 `BattleTurnController` 私有/内部步骤验证出牌事务；队列和 presentation 收到的执行结果才是生产调用方的 observable interface。纯公式与 Effect module 直接通过各自 interface 测试，不给内部操作类型增加 public 可见性。

## 预期文件范围

计划允许按实际命名创建以下纯 C# 文件及对应 Meta：

- `TinySpire/Assets/Scripts/Battle/Effects/`：Effect ID、公式、执行 module、结算记录和值对象。
- `TinySpire/Assets/Editor/Tests/`：M7 公式、状态、executor、结算与事务测试。

预计最小修改：

- `TinySpire/Assets/Scripts/Battle/CombatantData.cs`
- `TinySpire/Assets/Scripts/Battle/BattleCombatantsData.cs`
- `TinySpire/Assets/Scripts/Battle/BattleCardZonesData.cs`
- `TinySpire/Assets/Scripts/Battle/BattleEffectValueCalculator.cs`
- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandResults.cs`
- `TinySpire/Assets/Scripts/Battle/Commands/BattleCommandQueue.cs`
- `TinySpire/Assets/Scripts/Battle/Turn/BattleTurnController.cs`
- `TinySpire/Assets/Editor/Tests/BattleCombatantsDataTests.cs`
- `TinySpire/Assets/Editor/Tests/BattleCardZonesDataTests.cs`
- `TinySpire/Assets/Editor/Tests/BattleEffectValueCalculatorTests.cs`
- `TinySpire/Assets/Editor/Tests/BattleCommandQueueTests.cs`
- `TinySpire/Assets/Editor/Tests/BattleCommandPresentationAdapterTests.cs`
- `Docs/Copilot_Daedalus/plans/2026-08-02-m7-effect-executor.md`
- `Docs/Copilot_Daedalus/plans/README.md`
- `Docs/Copilot_Daedalus/SESSION_LOG.md`
- `Docs/Copilot_Daedalus/CODE_DECISIONS.md`（实现实际锁定后）
- `Docs/Copilot_Daedalus/DEPENDENCIES.md`
- `Docs/Copilot_Daedalus/ROADMAP.md`
- `Docs/Copilot_Daedalus/06_testing/`（各切片实施后新增）

以下路径不在预期范围：

- `DataTables/Datas/`、`TinySpire/Assets/GameData/`、生成配置代码与 Localization。
- `TinySpire/Assets/AddressableAssetsData/` 及本地构建产物。
- `TinySpire/Assets/Scenes/`、全部生产 Prefab 与 ProjectSettings。
- `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/` 及目录 Meta。
- asmdef、HybridCLR、Run/网络、启动流程与 DI 架构。

如果实现必须超出排除范围，先停止并说明目标文件、原因、风险和回滚单位，等待用户确认。

## 依赖项口径

- `DEP-004`：M7 提供 Effect/结算与 CardMoved 事实，但最终销毁前/弃牌过渡仍由 M9 表现消费，因此 M7 不直接 resolved。
- `DEP-009`：M7 完成共享 Effect/目标操作 seam；敌人意图真正调用该 seam 仍由 M8，依赖保持 open 并在 M7 完成时补部分解决记录。
- `DEP-010`：M7 只消费 Submit 前已选定的 Self/Enemy，不实现命令中途局部输入。
- `DEP-012`：Card 缺少 Discard/Exhaust 权威归宿字段，M7 当前卡全部弃牌，不硬编码。
- `DEP-013`：Block 清理、Vulnerable 衰减和状态触发时机等待 M8。

## 风险与回滚

| 风险 | 控制方式 | 回滚单位 |
|---|---|---|
| 预校验完成前扣能量或写状态 | 全部 Binding/目标/操作先构建成功，失败断言所有事实对象和值不变 | M7C 预构建与 M7D 事务接线 |
| 展示与结算各写一套公式 | 无目标展示与目标结算共用纯公式 module | M7A 公式 module |
| 旧 ApplyDamage 与新 executor 双写 | 删除或重塑旧直通，测试只经新 module/queue | M7B 状态写入口 |
| 结算记录成为第二份可变状态 | 记录只属于一次执行结果，冻结后只读，不保存全局日志 | M7A 记录类型 |
| 阶段抽牌仍没有记录 | 卡区 module 返回明确操作结果，由当前命令收集 | M7D 卡区结果接线 |
| 为 Exhaust 擅自改表或硬编码卡 ID | 当前卡一律 Discard，登记 DEP-012；需要新字段即停 | M7D 归堆步骤 |
| M7 顺手重写队列/状态机 | 保持 Submit、屏障和阶段流程；M8 债务明确排除 | M7D Queue/Turn diff |
| Block/Vulnerable 为了可见性提前加 HUD | 自动测试与只读事实验收，不创建临时 UI | M7E Game View |
| 致死后 Bash 第二效果造成半失败 | 明确 skipped 记录，命令仍成功归堆 | M7C 顺序执行 |
| 全量快照复制被静默优化 | 保持既有正确取舍，只有 profiler 证据和新决策才能加缓存 | M7B～M7D 状态 module |
| M9 美术被 Unity 自动导入后误纳入 M7 | 启动/结束按精确路径审计，禁止暂存或接线 | 全 Goal |

每个切片都是独立回滚单位。禁止广泛还原、`git reset --hard`、`git clean` 或覆盖用户已有改动。

## 完成定义

M7 完成意味着：现有 Strength、Strike、Defend、Bash 经 M4～M6 同一权威命令与目标链，在首次写入前完整预校验，随后按正式绑定顺序应用 Strength、Damage、Block 与 Vulnerable，产生不可变结算记录，并在效果完成后把卡牌放入弃牌堆；失败命令零写入、记录为空。展示值与执行值共享公式 module，阶段抽牌/弃牌/重洗也产生明确记录，生产 Bootstrap 和真实 Game View 已进入该链，自动测试、构建、文档和双轴复审全部完成。

M7 完成**不等于完整战斗闭环**：敌人真实 Effect、Block/Vulnerable 时机、死亡中止、队列/阶段屏障属于 M8；Block/状态 HUD、数字、抖动、死亡/胜负与最终动画属于 M3E/M9；Exhaust 卡牌归宿等待 `DEP-012`。未经用户明确确认，不创建 commit，不 push。
