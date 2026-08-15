---
title: TinySpire · G1-A 基础入口到可重开首战 · 窄实施计划
status: proposed-for-approval
scope: G1-A only
created: 2026-08-15
roadmap: Docs/Copilot_Daedalus/RUN_ROADMAP.md
grill: Docs/Hermes_Pegasus/design/2026-08-15-g1a-entry-first-battle-grill.md
art_checklist: Docs/Hermes_Pegasus/art/entry-ui-asset-checklist.md
---

# G1-A · 基础入口到可重开首战 · 窄实施计划

> 本计划是给 Daedalus/Codex 的实现包。它依据已确认的 Grill 记录编写；不扩大为 G1 全阶段、更不提前实现 G2～G8。
>
> 实施授权、提交与 push 仍由 Theseus 单独确认。本文件本身不改变项目事实源。

## 0. 一屏结论

**目标：**让玩家从基础入口开始一个单角色 Run，经过角色选择与一个临时节点进入既有 Battle；胜利后带着 Battle 结果回到已完成节点的地图；失败后可从进战前快照重开同一关、但使用新的本战随机输入。

**最小结构：**在现有 Bootstrap 的长寿命父 Scope 内新增一个 Run 状态所有者与流程协调器。`RunStateStore` 是跨场景业务事实唯一所有者；入口 UI、临时地图、失败页均为同一 `RunEntryScene` 内的可替换页面状态；Battle 只从现有 `IBattleSetupOptionsSource` 获取输入，并仅通过已存在的、表现完成后发布一次的 `BattleResult` 原子写回 Run。

**关键 seam（已审计）：**

```text
Bootstrap parent Scope
  └─ RunStateStore + RunFlowService
       ├─ RunEntryScene：主菜单 / 角色选择 / 地图 / 失败 / 占位页面
       └─ BattleScene：IBattleSetupOptionsSource → BattleLifetimeScope
                         BattleCommandQueue.Result → RunFlowService
```

现有 `BattleResult` 已带冻结后的玩家生命、终局序号和结果类型；`BattleCommandQueue` 仅在终局表现完成后发布一次结果。因此它是本片唯一的 Battle → Run 回写入口。

## 1. 已冻结的玩家可见行为

1. 启动进入主菜单；`开始游戏` 可用。
2. `设置` 是有返回按钮的布局页；`图鉴`、`统计` 是各自可返回的“开发中”页。它们不保存任何设置、不写入 Run。
3. 新游戏先到角色选择页。Hero 1001（战士）与 1002（机枪兵）均可选；一局只选一人。
4. 确认角色后，创建一个新 Run 并显示临时地图。地图只有一个可点击的战斗节点。
5. 进入 Battle 时，Battle 由 Run 的输入快照而非 UI/场景临时变量创建。
6. 胜利：把冻结的玩家生命写回 Run，节点标记完成，返回地图。没有奖励、下一节点、离开 Run、结算页。
7. 失败：转入失败页。`重开本关` 恢复**进战前** Run 快照，并重新派生新的本战随机输入后再进 Battle；它不是同种子重放。
8. 本片不提供存档/继续游戏、主动退出、永久死亡、真实地图、奖励、组队运行时或正式入口美术。

完整事实见 Grill：`Docs/Hermes_Pegasus/design/2026-08-15-g1a-entry-first-battle-grill.md`。

## 2. 实施约束

- **业务事实唯一所有者：**`RunStateStore`。它持有 RunId、所选 Hero、当前生命/上限、起始牌组的模板事实、当前节点状态、进战前快照、Run 随机根事实与本战派生序号。不要把这些字段复制到 MonoBehaviour、View、静态全局或场景间 DTO。
- **服务只编排：**`RunFlowService` 接收 UI 命令、调用 Store 的原子状态迁移、请求场景切换；它不保存第二份业务状态。UI 只订阅只读状态并提交命令。
- **Battle 输入：**复用 `BattleSession.cs` 中既有 `IBattleSetupOptionsSource` / `BattleSetupOptions` seam。必要时以最小向后兼容方式扩展 `BattleSetupOptions`，让当前生命、Hero/Deck 模板和本战 seed 来自 Run 输入，而不是 BattleScene 自己重建。
- **Battle 输出：**仅订阅/桥接 `BattleCommandQueue.Result` 的 `BattleResult`。不要轮询 Battle UI、不要从 View 读取胜负、不要让 Battle UI 写 Run。
- **失败恢复：**保存进战前 immutable Run snapshot；失败后先从该 snapshot 恢复，再让 RunFlow 递增/派生新的本战随机输入。不得复用失败 Battle 的临时生命、手牌、弃牌堆或 seed。
- **场景：**优先只新增一个 `RunEntryScene`，内部切换面板；不要为菜单、角色选择、地图和失败页各拆一个 Scene。Run root 必须跨 `RunEntryScene` / `BattleScene` 存活，旧 Battle Scope 必须随 BattleScene 卸载。
- **UI / 美术：**本片只做功能性 Unity UI/SVG 几何占位。不得批量生成图、不得锁定入口构图、不得把生成图中文字当 UI。复用已有 Hero 名称 i18n；新增菜单文字走现有本地化源与生成流程，禁止手改生成 C#。
- **Battle 终局 UI：**既有“当前场景重开 / 退出应用”动作不能再承担 G1 流程语义。Run 模式下终局结果应交给 RunFlow：胜利转地图、失败转失败页；保持非 Run 的现有 Battle 启动/测试路径可工作。
- **无关范围：**不要顺便拆 `HandCardContainer`、DI 工厂、表现层、Battle 命令队列或做大型架构债重构；发现这些旧债只记录为风险。

## 3. 影响路径与任务顺序

### Task 1 · 先建立 Run 的纯状态与迁移测试

**创建（建议命名，若现有命名规范有更自然位置可等价调整）：**

- `TinySpire/Assets/Scripts/Run/RunState.cs`
- `TinySpire/Assets/Scripts/Run/RunStateStore.cs`
- `TinySpire/Assets/Scripts/Run/RunBattleSnapshot.cs`
- `TinySpire/Assets/Editor/Tests/RunStateStoreTests.cs`

**先写失败测试，再实现最小不可变迁移：**

- 创建新 Run 会冻结一名合法 Hero、初始生命/牌组模板、唯一 RunId、初始未完成节点与随机根事实。
- 仅当节点可进入时才能形成进战前 snapshot；重复进入/已完成节点被拒绝。
- Victory 仅接受与当前 Battle 输入匹配的结果，并原子写入玩家结算生命、标记节点完成、清除 active battle snapshot。
- Defeat 不污染进战前 snapshot；恢复操作回到该 snapshot，并使下一次本战随机输入不同于前一次。
- 无 Run、Hero 不匹配、无 snapshot、重复结算等非法迁移应以明确结果/异常拒绝，而不是悄悄修正。

**停止点：**纯 C# EditMode 测试先绿；这一任务不得接触 Scene/UI。

### Task 2 · 把 Run 输入接到既有 Battle setup seam

**修改：**

- `TinySpire/Assets/Scripts/Battle/BattleSession.cs`
- 必要的 Battle 初始化/测试装配文件（以现有 `BattleLifetimeScope` 为准）
- `TinySpire/Assets/Editor/Tests/BattleSessionTests.cs`
- 新增或扩展 `Run` 与 `Battle` 的 seam 测试

**要求：**

- `IBattleSetupOptionsSource` 仍是 BattleScene 读取上游输入的唯一入口。
- 为 Run 的单玩家输入提供当前生命、Hero、起始牌组模板和本战 seed；旧的非 Run 测试/启动路径保留原有默认行为。
- Battle 创建必须确实以 Run 输入的初始生命与 deck 模板装配，而不只是在 RunState 里记录它们。
- 不改变 `BattleResult` 的“稳定终局、表现结束后一次发布”的含义。

**停止点：**新增的 Run 输入测试与既有 BattleSession 测试同时通过。

### Task 3 · 建立跨场景 RunFlow 与 BattleResult bridge

**创建：**

- `TinySpire/Assets/Scripts/Run/RunFlowService.cs`
- `TinySpire/Assets/Scripts/Run/RunSceneNames.cs`（或等价单一场景常量归属）
- `TinySpire/Assets/Scripts/Run/BattleResultRunBridge.cs`
- `TinySpire/Assets/Editor/Tests/RunFlowServiceTests.cs`

**修改：**

- `TinySpire/Assets/Scripts/Core/Bootstrap.cs`
- `TinySpire/Assets/Scripts/Core/SceneFlowService.cs`
- `TinySpire/Assets/Scripts/Core/GameLauncher.cs`
- `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`
- `TinySpire/Assets/Editor/Tests/GameLauncherM10BTests.cs`
- `TinySpire/Assets/Editor/Tests/GameStartupOptionsTests.cs`

**要求：**

- 在 Bootstrap 的长寿命 Scope 注册 `RunStateStore`、只读接口和 `RunFlowService`；BattleScene 的 Scope 能解析 Run 的 `IBattleSetupOptionsSource`，但不能反向持有/修改 Store。
- `BattleResultRunBridge` 在 BattleScope 内只订阅一次已发布的 `BattleResult`，转交给 RunFlow；避免重复订阅、卸载后回调或并发二次场景加载。
- Victory 路由到 `RunEntryScene` 的地图状态；Defeat 路由到同一场景的失败状态。由 RunFlow 在场景过渡边界处理，不能通过 Button 回调间接判断。
- BattleScene 的旧 outcome/restart/exit UI 在 Run 模式下不再触发“同场重开/退出应用”；保留其独立 Battle 启动路径的兼容行为，或将其明确隔离为 legacy/debug 路径。

**停止点：**以 fake SceneFlow / fake battle result 的 EditMode 测试验证：新 Run 入战、胜利回图并完成节点、失败到失败页、重开恢复 snapshot 且 seed 改变、旧 Battle bridge 释放后不再写回。

### Task 4 · 建立一个功能性 RunEntryScene 与页面控制器

**创建：**

- `TinySpire/Assets/Scenes/RunEntryScene.unity`
- `TinySpire/Assets/Scripts/UI/Run/RunEntryView.cs`
- `TinySpire/Assets/Scripts/UI/Run/RunEntryPresenter.cs`（或现有 UI 层已有等价 presenter 结构）
- 必要的 UI prefab / `.meta` / Addressables scene 条目
- 入口文字的本地化源项，并通过既有生成流程更新生成物

**修改：**

- `TinySpire/Assets/Scenes/BootstrapScene.unity`（初始场景从 `BattleScene` 变为 `RunEntryScene`）
- Addressables / 场景构建登记的既有来源与其对应测试

**页面状态：**

- `MainMenu`：开始游戏、设置、图鉴、统计。
- `HeroSelection`：1001/1002 均可选；单角色确认；视觉上可留空槽，但无多人规则。
- `Map`：一个节点；未完成时可点击，完成后清晰显示已完成且不可重复入战。
- `Failure`：只提供“重开本关”。
- `Settings`、`CompendiumComingSoon`、`StatisticsComingSoon`：只有返回。

**要求：**

- View 不能保存或修改 Run 业务事实；其按钮只调用 presenter/flow 的命令。
- 页面刷新从只读 Run state/flow state 派生；场景重新进入时能正确显示地图或失败页。
- 所有新文字由 TMP + i18n 渲染。不要制作正式美术，不要引入批量图片资源。
- 必须验证 Bootstrap → RunEntryScene 的生产路径，以及 RunEntryScene → BattleScene → RunEntryScene 的返回路径。

### Task 5 · 端到端验证、文档闭环与停手

**验证层级：**

1. 运行新增/修改的 Run、Battle setup、bootstrap EditMode 测试。
2. 运行项目现有的完整 Unity EditMode 命令；报告真实 passed/failed 数，不能沿用旧基线冒充本轮结果。
3. 从 Unity Editor 实测：
   - 开始游戏 → 两名 Hero 分别可选 → 单节点入战；
   - 胜利回地图、节点完成；
   - 失败到失败页 → 重开 → 入战随机输入不同；
   - Console Error 0；旧 BattleScene 不留下 Run result bridge/Scope 回调。
4. 重新构建必要的 Addressables Local Content，并验证新 Scene 可被生产场景流加载。

**文档：**

- 只在真实验证后更新 `Docs/Copilot_Daedalus/SESSION_LOG.md` 与 `RUN_ROADMAP.md` 的动态状态；不得把“代码写完”提前写成 `verified`。
- 若实现发现 Grill 与现有代码无法同时成立，停止并记录冲突，不自行改写 Grill 或事实源。

**停手条件：**G1-A 验收完成即停止。后续奖励、多节点、地图退出、继续游戏/存档、正式入口概念与美术，都回到各自 Grill。

## 4. 验收清单

- [ ] 启动进入功能性主菜单，不再直入 BattleScene。
- [ ] 两位现有 Hero 均可在角色页选择；每 Run 仅一名。
- [ ] 角色确认后才存在 Run，并进入可点击的单节点地图。
- [ ] 进入 Battle 的 hero / health / deck / seed 都来自 Run 输入。
- [ ] Battle 终局通过单一 `BattleResult` bridge 回写，不由 UI 判断/写状态。
- [ ] 胜利回地图并将唯一节点标记完成。
- [ ] 失败进入失败页；重开恢复进战前 snapshot，且新本战 seed 不同。
- [ ] 设置、图鉴、统计页面都可往返，但无额外功能或持久化。
- [ ] UI 不拥有 Run 业务状态；BattleScope 卸载后无残留回调。
- [ ] 无正式入口美术、无生成图片批量、无 G2+ 功能偷渡。
- [ ] 新增/修改测试、全量 EditMode、Addressables build 和 Unity 手测均有真实证据。

## 5. 已知风险与回滚点

| 风险 | 处理 |
|---|---|
| 既有 Battle 终局面板将 restart/exit 绑定为局部流程 | 只在 Run 模式由 RunFlow 接管；legacy/debug 启动路径明确隔离。不要删除既有路径却没有兼容测试。 |
| Run root 生命周期与 BootstrapScope 不一致 | 先用 EditMode seam 测试验证 scope/scene 停止点，再接 UI。不要用 static singleton 兜底。 |
| Battle 不能吃自定义初始生命/牌组 | 在 `BattleSetupOptions` seam 做最小、向后兼容的输入扩展，并用 BattleSession 测试证明实际生效。 |
| 入口构图未定 | 只用可替换的功能性布局；不硬编码正式美术坐标或生成资源。 |
| Unity Scene/Addressables 接线难以纯测 | 先补最小构建/启动合约测试，最后用 Unity Editor 手测，不把手测省略为“应该能跑”。 |

## 6. 明确不做

存档、继续游戏、真实设置、真实地图、多节点、奖励、商店/事件/篝火/Boss、永久死亡、主动放弃、多人组队运行时、联网、正式入口概念或美术定稿，均不属于 G1-A。
