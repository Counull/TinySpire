---
title: M4E 全量验证、双轴复审与文档收口
page_type: testing
lifecycle: active
date: 2026-08-01
scope: TinySpire M4A～M4D 全量回归、轮次栅栏与 pending 序号回归、Bootstrap 两轮闭环、Addressables 与 Standards / Spec 复审
source: ../plans/2026-07-31-m4-turn-scheduling-energy.md
status_source: ../SESSION_LOG.md
---

# M4E 全量验证、双轴复审与文档收口

## 验收结论

M4E **通过**。首次 Spec 复审发现“全体敌人已死亡时，旧玩家命令可在同步开始的下一轮重新合法”这一阻断缺陷；用户随后确认“玩家命令只能属于提交时的轮次”，并授权按队列内部轮次栅栏修复。修复后的复审又发现：同一 `CardInstanceId` 在下一轮重建 View 后，旧序号失败反馈可能误清更新序号的 pending。展示关联现已绑定权威序号，重建 View 会恢复映射中的最新 pending，且旧反馈只能清除匹配序号。两项修复完成后，定向与全量 EditMode、串行 solution build、Bootstrap 两轮闭环、生产跨轮及 View 重建探针、Addressables 构建和 Standards / Spec 双轴复审全部通过。

修复涉及 `BattleCommandQueue.cs`、`BattleCommandResults.cs`、`HandCardContainer.cs`、`HandCardVisual.cs` 及两份既有测试文件。公共 `Submit` / `Queue` / `Turn` seam、命令构造参数、`BattleTurnData`、场景和生产装配均未改变；UI 只收紧短生命周期 pending 的序号关联，没有新增玩法事实。本阶段没有修改配置表、生成 JSON、场景、Prefab、ProjectSettings、asmdef、DI 或启动流程，也没有暂存、提交或改写独立美术提交 `afd2cfb`。

## 自动验证

| 检查 | 结果 |
|---|---|
| 队列 TDD 回归切片 | 重复结束红灯任务 `124d94e1f8b34fdd88f368fd3ef5606f`、旧出牌红灯任务 `23e6741e4eb740a5889a13d436ee95ae` 均稳定复现旧行为为 `Expected False, But was True`；轮次栅栏后两用例任务 `1533afddde904c9297e4442d255be9a7` 为 **2/2 通过** |
| UI pending TDD 回归 | 行为红灯任务 `99d80bf405dc4c82ba3bc28d1819c312` 复现旧失败序号误清更新 pending（`Expected True, But was False`）；序号关联修复后任务 `c7ec9523be8c48a09e7b531cc38cdb6b` 为 **1/1 通过** |
| 三项缺陷复合切片 | **3/3 通过**，0 failed、0 skipped；任务 `209ac72dbcfb4306b93e99946527218f`，耗时 `0.4669904s` |
| Unity MCP M4 定向 EditMode | **30/30 通过**，0 failed、0 skipped；`BattleCommandQueueTests` + `BattleTurnControllerTests` + `BattleCommandPresentationAdapterTests`；任务 `b392398a83d248389d1c7018cc0e5dea`，耗时 `0.4082064s` |
| Unity MCP 全量 EditMode | **70/70 通过**，0 failed、0 skipped；任务 `e09e6d05c5224bdcbc20fed19ff1eaf5`，耗时 `5.3623919s` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有程序集依赖版本冲突 warning |
| 正常 Bootstrap 实跑 | 两个完整轮次后进入 `PlayerAction / Round 3`，队列空闲 |
| 生产跨轮探针 | 两条结束命令均在 Round 3 提交；第一条进入 Round 4，第二条反馈 `PlayerActionWindowExpired`，最终仍为 Round 4、能量 3、未结束、队列空闲；Console Error/Warning **0/0** |
| 生产 View 重建探针 | Round 2 的 5 张卡在结束命令后进入 Round 3；重抽重叠实例 2 张（ID 4、9）均恢复 pending（**2/2**），旧跨轮出牌全部失败后当前 5 个 View pending 为 0、队列空闲；Console Error/Warning **0/0** |

## Bootstrap 两轮与生产轮次栅栏

1. 从唯一的 `TinySpire@8edf130c865b3957` Unity 6000.5.5f1 Editor 启动 Bootstrap，自然进入 `BattleScene`；首轮为 `PlayerAction / Round 1`，队列空闲。
2. 通过生产 `BattleCommandQueue.Submit` 结束 Round 1；驱动经同一队列完成当前无行为敌人后进入 `PlayerAction / Round 2`。再次结束 Round 2 后进入 `PlayerAction / Round 3`，队列空闲。
3. 仅为诊断，通过现有 `BattleCombatantsData.ApplyDamage` 将唯一敌人生命降为 0；没有写入项目文件，也不把该探针当作 M4 的正式伤害链。
4. 在一个尚未完成的生产展示结果之后连续提交两条 `EndPlayerActionCommand`。阻塞项序号为 6，两条结束命令序号为 7、8；两条玩家命令均在 Round 3 入队，队列当时 `PendingCount=2`。
5. 序号 7 成功并同步开始 Round 4；序号 8 到达队首后展示 `Failed #8 · EndPlayerAction · PlayerActionWindowExpired`。最终仍为 `PlayerAction / Round 4 / Energy 3 / HasEndedAction false`，队列空闲，没有发生第二次跨轮写入。
6. 该实跑调用生产容器、队列、展示 adapter、HUD 反馈与运行时驱动，但不冒充物理鼠标拖拽手感验证；M4D 已单独记录实际拖拽处理函数链。

## 生产 View 重建与序号关联

1. 从新的 Bootstrap 实跑进入 Round 2，在生产展示项尚未完成时，先提交结束行动，再通过现有 `HandCardContainer.SubmitPlayCard` 路径为当轮 5 张 View 建立权威序号 pending；该诊断仅用反射调用既有私有提交入口，没有修改项目文件或新增测试专用接口。
2. 暂停 Player Loop，并把当前生产 adapter 的展示剩余时间归零一次，使阻塞项完成且结束命令成为队首。结束命令同步进入 Round 3，生产 `CardZones.Layout -> HandCardContainer.RebuildCards` 完成 View 重建，此时旧出牌命令仍在队列中。
3. Round 3 与旧手牌重叠的实例为 ID 4、9；两个新 View 的 `IsCommandPending` 均为 true，证明重建从“权威序号 → CardInstanceId”映射恢复了对应 pending，没有出现可重复提交窗口。
4. 恢复 Player Loop 后，五条旧出牌均因轮次栅栏失败。最终 Round 3 保持 `PlayerAction`，5 个当前 View 的 pending 全部清零，队列空闲，Console Error/Warning 为 0/0。

## Addressables

- 执行 `TinySpire/Addressables/Build Local Content` 成功；Editor 日志记录内容构建耗时 `16.118s`，输出 `Library/com.unity.addressables/aa/Windows/settings.json`。
- 报告 `TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.01.06.41.41.json`：`BuildError` 为空，`BuildResultHash=259e02cf2d79b5cd0bd291f571b46782`，`Duration=16.9804458s`，文件 SHA-256 为 `050FAF1CB7C431A1391BCBA475C017DE3256BA3A4B18057775DD3F12530FBEE0`；最终 settings SHA-256 为 `F5D5AD7CBB59822B5A564F5430B36B0BBC9669C96E66DFF99FD99C081A8BAD93`。
- 报告确认 `BattleScene`、`LoadingScene` 与七个 `Assets/GameData/*.json` 继续使用完整 `Assets/...` 稳定地址；最终没有遗留 ProjectSettings 改动。
- 本阶段没有修改 `DataTables/Datas/` 或表定义，因此未运行 Luban。

## Standards

- **Hard · 已修正**：`06_testing/README.md` 遗漏 M4D 验收记录。本次同时补入 M4D 与 M4E 链接，恢复测试目录路由。
- **Hard · 仅记录**：`d73eeef`、`47d9aec`、`f155bfc` 的提交摘要使用 `feat(M4):完成...`，不满足 Conventional Commits 冒号后的空格格式。现有历史未经用户授权不得改写；后续提交消息必须使用 `feat(M4): ...`。
- **Judgement call · 不扩展重构**：`BattleTurnController.TryPlayCard` 与 `TryEndPlayerAction` 重复阶段、玩家、存活、结束状态和卡区校验链。当前没有因此产生行为分叉，本阶段不以代码味道为由扩大修改范围。
- 本次修复扩展队列内部不可见信封、执行失败枚举与 UI 短生命周期 pending 的精确序号关联；没有修改公共命令 interface、`BattleTurnData`、DI、场景或 Prefab，新增测试函数与生产函数均有中文说明。
- 其余规范通过：M4 新增函数均有中文说明；能量、卡区与阶段的生产写入均收口于 `BattleCommandQueue -> BattleTurnController`，未发现 UI 或系统绕过 AC-009 / CD-027。

## Spec

### 已修复：旧玩家命令不能跨轮重新合法

规格要求玩家结束命令执行后，排在它后面的同玩家命令到队首时失败且零写入。阻断缺陷的根因是旧实现只按执行瞬间的 `Phase` 与 `HasEndedAction` 校验；当全部敌人死亡、结束命令同步开始下一轮时，旧命令会面对新一轮事实。

用户确认后的修复规则为：

1. 玩家命令只能属于提交时的 `RoundNumber`；不能在 `BattleStart` 或 `EnemyAction` 为未来轮次预排玩家命令。
2. `QueuedBattleCommand` 在内部记录提交轮次，不把轮次参数暴露给 `PlayCardCommand`、`EndPlayerActionCommand` 或 UI。
3. `PlayCardCommand` 与 `EndPlayerActionCommand` 到达队首时，若当前轮次已不同，直接返回 `PlayerActionWindowExpired`，不调用 `BattleTurnController`，因此不写能量、卡区、阶段、轮次或结束标记。
4. `StartBattleCommand` 与 `CompleteEnemyActionCommand` 不是玩家行动窗口命令，不受此栅栏影响。

两个公开 seam 回归用例分别证明：全敌死亡时同轮排队的重复结束不能结束下一轮；同一 `CardInstanceId` 在下一轮重抽后，旧出牌命令仍不能扣能量或移动卡牌。生产探针进一步确认实际展示反馈为 `PlayerActionWindowExpired`，最终事实保持在新一轮初始状态。

### 已修复：过期反馈不能解锁更新命令或重建 View

队列栅栏会让旧出牌在下一轮产生执行失败反馈，而同一 `CardInstanceId` 可能已随新一轮布局重建。`HandCardVisual` 现在只以 nullable 权威序号保存 pending 身份，`IsCommandPending` 由该身份派生；失败反馈仅在序号匹配时清除。`HandCardContainer.RebuildCards` 会从现有待定映射恢复指定实例的最新序号，因此旧意图尚未结束时重建 View 仍锁定；即使已有更新序号，旧失败也不能误清。行为测试覆盖旧序号/更新序号的清除关系，生产探针覆盖跨轮重建恢复与失败后的最终释放。

提交/执行结果分离、展示期继续入队而共享写串行、按 `CombatantId` 的多人根、失败零写入、首轮/后续轮次共用入口、Encounter 顺序、生产 UI/系统只经队列，以及未扩展 Effect/M5/M7/M9 均通过。

## 后续动作

1. 展示本次未提交审查包，等待用户明确批准后再按显式路径暂存和提交；不推送。
2. 后续新增玩家命令类型时，必须显式纳入同一玩家行动窗口判定；本次不预先抽象命令分类系统。
