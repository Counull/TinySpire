---
created: 2026-07-06
updated: 2026-07-30
---

## 2026-07-30 · 卡牌区域与确定性洗牌实施

> - 新增 `GameRandom`，以实例方式封装项目已存在的 `Unity.Mathematics.Random`；规则随机可读取/恢复 `uint State`，并通过 Fisher–Yates 洗牌，不使用 `UnityEngine.Random` 全局状态。
> - `HandState` 升级为 `CardZoneState`：全部卡牌实例由 `Cards` 字典定义，抽牌堆、手牌、弃牌堆、消耗区分别保存互斥的有序 `CardInstanceId`；不保存 `Zone` 镜像或缓存计数。
> - `BattleSession` 现在创建完整 10 张初始卡组，按战斗种子洗牌并抽取 `GameConfig.InitialHandCount` 张；`DEP-006` 已解决。当前种子仍来自 BattleScene Inspector，未来改由 Run 生命周期提供，登记为 `DEP-007`。
> - 手牌拖过出牌线的既有占位行为现在把指定实例移动到弃牌堆；未实现效果器、目标合法性、费用、回合调度、地图/奖励/敌人随机。
> - TDD 定向 EditMode 10/10、完整 EditMode 13/13 通过；dotnet build 0 error；Bootstrap 实跑为 10 个实例、抽牌堆 5、手牌 5、弃牌/消耗 0，Console 0 error，保留既有 LoadingScene handle warning。
> - 双轴代码审查最终通过；审查修正了随机流外部别名、视图冗余模板 ID 与旧文档过期状态，复核无新增 P1/P2。
> - 本轮未修改表格、生成 JSON 或 YooAsset 包，因此不运行 Luban/AB 重建。

---

## 2026-07-30 · 卡牌 i18n key 与动态说明设计补充

> - 路线图新增 M2A：卡牌名称/说明使用 i18n key，说明模板使用 `{damage}`、`{block}`、`{vulnerable}` 等命名参数，并允许不同语言调整语序。
> - 规划 `CardTextFormatter` 深模块：UI 只提交卡牌实例和可选来源参与者，模块内部解析 key、效果参数、关键词和动态数值；格式化文本不进入 `CardInstanceState`。
> - 说明显示值、目标预览值和实际结算值必须复用同一纯数值计算模块，只是上下文不同；三者均由配置和运行时事实派生，不成为第二份状态。
> - 当前未安装 Unity Localization，文本目录后端与 fallback locale 保持为实施前 Open Question。本轮只修改设计文档，未改表格、代码、生成数据、AB 包或效果器。
> - 详细设计见 `plans/2026-07-30-card-localized-text-design.md`。

---

## 2026-07-30 · 战斗配置接入运行时 + BattleScene MVP 路线图

> - 新增 `BattleSession`，由 `BattleLifetimeScope` 从英雄 1001、遭遇 5001、初始卡组和 `GameConfig.InitialHandCount` 创建玩家、敌人与手牌；`CombatantState` 接入模板基础力量。
> - `HandState` 改用唯一 `CardInstanceId` + `TemplateId`，解决初始卡组内重复 Strike 无法独立表示的问题。手牌 UI 读取同一运行时状态，并从 `battle.Card` 显示卡名和费用。
> - 未实现效果器、目标、费用扣除、牌堆、回合流程或敌人行为。正式牌堆前暂取卡组前 5 张，登记为 `DEP-006`。
> - 重写 `ROADMAP.md`：按 M0～M10 规划牌堆、主 HUD、回合、敌人意图/随机行为、出牌命令、效果器、完整循环和反馈；并以 G1～G8 承接主菜单、Run、存档、地图、奖励、遗物/药水、商店/事件和完整产品收尾。每阶段明确唯一事实、派生数据与验收标准。
> - 验证：EditMode 6/6 通过；dotnet build 0 error；Bootstrap → BattleScene 实跑生成 5 张独立 Strike，标题/费用绑定正确。本次无 error，保留一条既有 LoadingScene handle warning。

---

## 2026-07-30 · STS 战士初始卡组配置

> - `battle.Deck` 1001 设为 5×Strike、4×Defend、1×Bash；初始手牌 `game-config.json` 已是 5，保持不变。
> - `battle.Card` 由单个 `effect_id` 改为 `effect_ids` 列表，使 Bash 可表达“8 伤害 + 2 易伤”；新增敌方目标、伤害、格挡、易伤和空属性枚举项，仅作为静态配置。
> - Luban 生成成功，YooAsset `Main` 内置包已重建；Bootstrap 场景实跑控制台 0 error。运行时效果结算不在本轮范围内。

---

## 2026-07-30 · 战斗表 YooAsset 生成路径修正

> - Luban JSON 输出从 `TinySpire/Assets/StreamingAssets/GameData` 改为 `TinySpire/Assets/GameData`，与 `ConfigService` 的资源路径加载约定对齐。
> - 生成后重建 YooAsset `Main` 内置包；仅刷新 Unity 不会把新 JSON 写入离线清单。Bootstrap 场景实跑确认 `battle_tbhero` 加载不再报错。

---

# Daedalus · 会话日志

> 记录每次编程会话的关键产出、决策和待办。

---
## 2026-07-30 · 战斗静态配置表实施

- 在 `DataTables/Datas/__tables__.xlsx` 登记 6 张手工 schema 战斗表，并在 `__enums__.xlsx` 定义 `TargetRule.Self`、`EffectType.ModifyAttribute`、`Attribute.Strength`。
- 新增 `battle.hero.xlsx`、`battle.enemy.xlsx`、`battle.deck.xlsx`、`battle.card.xlsx`、`battle.card_effect.xlsx`、`battle.encounter.xlsx`；填入一套闭合最小样例：Test Warrior（30 HP）→ deck 1001 → Strength 卡牌（Self，+3 Strength），Test Slime（20 HP）和单敌人 encounter 5001。
- 模板表只保存稳定 ID 与设计数值；`CombatantId`、当前生命、存活、手牌/抽牌/弃牌堆、卡牌实例、临时费用、升级、敌人意图和控制者不进入配置。表间关系暂以 ID 表达，未实现 `ref` 校验或运行时导航。
- Luban 生成 `cfg.battle` 的 6 个记录类型、6 个表管理器、3 个枚举及 6 个 JSON；`#demo.item.xlsx` 按既有删除意图保持缺失，旧 demo 生成产物已随重新生成移除。战斗数据文件故意不使用 `#` 前缀，避免自动导入与手工 schema 重复。
- 验证：UnityMCP 资源刷新无编译错误；`dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` 为 0 错误（13 条既有程序集版本冲突警告）。详情见 `plans/2026-07-30-battle-static-config-tables.md`、`06_testing/2026-07-30-battle-static-config-tables.md` 与 CD-011。

---
## 2026-07-30 · BattleState 运行时参与者模型实施

- 新增纯 C# `TinySpire.Battle` 运行时模型：`CombatantId`、共同基类 `CombatantState`、`PlayerCombatantState`、`EnemyCombatantState` 与聚合根 `BattleState`。
- `BattleState` 是唯一持有 `CombatantId → CombatantState` 映射的事实源，并以只读字典 `Combatants` 暴露；按用户反馈删除了预置的玩家/敌人/存活派生视图和与 `TryGetCombatant` 重复的 `ResolveSelf`，未来只在真实目标规则出现时从字典值按需派生。未并存 `List` 作为索引或镜像；本次将原始 `List` 正式替换为该字典，`TryGetCombatant` 直接委托 `TryGetValue`。
- 初版共同可变事实仅为生命；`ApplyDamage` 修改目标参与者自身的当前生命，`IsAlive` 由该生命值派生，当前不预置存活视图。未接入 `HandState`、卡牌实例、Effect、敌人意图、能量、UI、场景锚点或 `BattleLifetimeScope`。
- 新增 EditMode `BattleStateTests`；字典调整后 UnityMCP 重新运行 3 项测试全部通过。`dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` 为 0 错误（13 条既有程序集版本冲突警告）。测试结束后 Console 出现 YooAsset `AssetBundleCollectorWindow.RefreshWindow` 的既有编辑器包空引用（Undo 回调，未触及本次代码），已在验证记录注明。
- 决策见 `CODE_DECISIONS.md` CD-010；计划与验证记录分别见 `plans/2026-07-30-battle-runtime-state.md`、`06_testing/2026-07-30-battle-runtime-state.md`。

---
## 2026-07-30 · BattleScene 拖拽出牌（最小判定）验收完成

- 用户已在 Game View 完成并确认鼠标手势验收：拖拽保持抓取偏移且持续跟随；越过 `playLineY` 松手后卡牌销毁、其余手牌补位并显示透明度反馈；线内松手会回弹并恢复反馈。
- `拖拽打出最小判定 + 手牌数据归属权收回`（ROADMAP Phase 1）由“已实施，待人工手势验收”更新为“已完成并验证”。
- 已更新 `ROADMAP.md` 与 `06_testing/2026-07-30-battlescene-drag-to-play-minimal.md`；未修改代码、预制体、场景或 DEP-001～DEP-004 的未解决状态。

---
## 2026-07-30 · BattleScene LifetimeScope 实施

- 新增 `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`：定义 sealed 的 VContainer 场景 Scope，`Configure` 保持为空，仅保留 `TODO(DEP-005)` 占位标记。
- 通过 Unity MCP/编辑器在 `BattleScene.unity` 根级创建并保存 `BattleLifetimeScope` GameObject，`parentReference` 设置为 `Bootstrap`。
- 未修改 `SceneFlowService.cs`、`Bootstrap.cs`，未实现回合调度器、抽牌堆、弃牌堆或相关抽象。
- 验证：`dotnet build TinySpire/TinySpire.sln --no-restore` 为 0 错误；Unity Play Mode 为 0 错误、0 警告，未出现 parent reference 警告。
- 验证记录：`06_testing/2026-07-30-battle-lifetime-scope.md`。

---
## 2026-07-30 · 战斗场景 LifetimeScope 结构 grilling（纯讨论，无代码改动）

- 用户提出未来战斗场景需要独立的回合调度循环、抽牌堆、弃牌堆，讨论是否需要专属 `LifetimeScope`、是否绑定场景生命周期。结论：需要，且应作为场景内挂载的 GameObject（不由代码动态创建/销毁），因为 YooAsset `LoadSceneMode.Single` 切场景时会自动销毁旧场景 GameObject，VContainer 的 `parentReference` 按类型全局查找父 Scope（`LifetimeScope.FindParent`），与 `SceneFlowService` 完全解耦，不需要改动 `SceneFlowService.cs`。
- 进一步讨论"未来加入地图"后的结构（Game → 存档 → 三张地图 → 具体事件），结论：不需要给每一层单开 DI Scope，只需要 3 层——Bootstrap（Root）→ RunScope（存档，跨场景持久，需要新的 `RunFlowService` 手动创建/销毁）→ 事件层场景 Scope（战斗/地图/商店，沿用场景挂载方案）；"地图"本身只是 `RunState` 里的字段，不需要单独 Scope。
- 确认了 `06_testing/2026-07-30-scene-child-scope.md` 描述的"`SceneFlowService.CreateChild` 动态创建子 Scope"方案已被用户撤回、代码已还原，该文件头部的 `source: CD-008` 是错误引用（当时 CD-008 从未真正存在）；已将该文件归档至 `99_archive/2026-07-30-scene-child-scope.md` 并更新其前言说明。
- 新增 `CD-008`（场景级服务用挂载在场景内的 `LifetimeScope`，不由代码动态创建/销毁）与 `CD-009`（存档层 `RunScope` 需要显式 `RunFlowService` 管理生命周期，前瞻记录、未实现）；`ARCHITECTURE_CONVENTIONS.md` 新增 Locked 的 `AC-006` 并在 Open 部分登记 `RunScope` 仍是前瞻性质。
- 按 CD-008/AC-006 产出 Codex 实施 Prompt（直接在对话中给出，未另存为文件）：仅创建 `BattleLifetimeScope`（挂在 `BattleScene.unity`，`parentReference` 指向 `Bootstrap`），`Configure` 暂空并标记 `DEP-005`；明确排除回合调度器/抽牌堆/弃牌堆的实际实现，不改动 `SceneFlowService.cs`。新增依赖项 `DEP-005` 登记到 `DEPENDENCIES.md`。
- 本轮未写任何 C# 代码，未创建 `RunFlowService`/`RunLifetimeScope`/`RunState`，仅做文档维护。

---

## 2026-07-30 · 最小状态机 Core 实施

- 新增纯 C# `TinySpire.Core.StateMachine`：状态包含 `Enter`、`Tick(TimeSpan)`、`Handle(event)`、`Exit`，状态通过返回值请求切换。
- 状态机不持有事件队列、不依赖 Unity/UniTask、不查找游戏运行时数据；Update/Tick 驱动和事件排队由外部负责。
- 支持状态跨多帧保持、同步事件分发、同一次 `Tick` 中后续状态使用零时间继续 Tick，以及不可重启的 `Stop()`。
- 本轮明确不实现 Context、嵌套状态、并行状态、异步调度和任何游戏领域接入，避免在缺少真实用例时扩展 Core。
- 验证记录：`06_testing/2026-07-30-state-machine-core.md`。

---

## 2026-07-30 · BattleScene 拖拽出牌（最小判定）实施

- 在 `TinySpire/Assets/Scripts/UI/Battle/Hand/HandState.cs` 新增纯 C# `HandState`：以占位 ID 初始化手牌列表，只暴露只读快照、`PlayCard(int)` 和 `event Action` 变化通知；不接入 R3、真实卡牌数据、费用或 BattleState。
- `HandCardContainer` 现在只以 Inspector `initialHandCount` 初始化 `HandState`；运行期张数从 `HandState.CardIds.Count` 得出。它订阅变化后销毁已打出卡的视觉对象并按状态快照重排其余卡牌。松手时以可调 `playLineY`（默认 240）判定；越线调用 `HandState.PlayCard`，未越线仍回弹。
- 拖拽坐标使用每帧 `PointerEventData.delta / Canvas.scaleFactor` 累加到当前锚点，不再把屏幕点换算到独立根 Canvas 的零尺寸 RectTransform；因此按下不跳中心，后续移动保持抓取偏移并持续跟随鼠标。
- `HandCardVisual` 使用 `CardContent` 上运行时添加的 `CanvasGroup` 做越线透明度反馈，并独立维护、终止其反馈 Tween；未修改 `CardView.prefab`。
- 按依赖台账添加 `TODO(DEP-001)` 至 `TODO(DEP-004)`：目标 ID 填充、费用、反馈样式、销毁前动作。没有实现目标、费用、效果、抽牌或弃牌逻辑。
- 验证：纯 `HandState` 检查通过；`dotnet build TinySpire/TinySpire.sln --no-restore` 通过（0 错误；9 条既有程序集版本冲突警告）；UnityMCP Play Mode Console 为 0 错误、0 警告。MCP 无指针事件注入，最终鼠标拖拽手势需人工确认。
- 验证记录：`06_testing/2026-07-30-battlescene-drag-to-play-minimal.md`。

### 后续动作

- 已完成：用户在当前 Game View 中人工确认移动不跳中心、越线销毁补位、线内回弹和透明度反馈。

---

## 2026-07-29 · 拖拽出牌（最小判定）grilling + 计划产出

- 确认杀戮尖塔式手牌 UI 已由 Codex 实施完成（见上一条会话日志与 `06_testing/2026-07-29-battlescene-hand-ui-sts-style.md`），但拖拽当前不能判定出牌。
- 用 `grilling` 技能逐项确认了最小可行的出牌判定（可调 Y 轴出牌线），并在过程中发现 `handCount` 应该从 UI 组件里收回归属权，因此新增了最小 `HandState` 纯 C# 聚合类的设计。
- 确认本轮不做目标选择、不做费用检查、打出后立即 `Destroy`（无过渡动画），拖过出牌线只加最简占位视觉反馈。
- 按用户建议引入了一套“依赖项 ID”机制（DEP-001~DEP-004），写进计划文档，并要求未来实现时在代码里用 `TODO(DEP-xxx)` 标记对应位置。
- 产出实现计划：`plans/2026-07-29-battlescene-drag-to-play-minimal.md`（proposal，未实施代码）。
- 新增代码决策 CD-005（HandState 收回数据归属权）与 CD-006（拖拽出牌判定机制）。
- 本轮未写任何 C# 代码，未 commit。配套 Codex Prompt 直接在对话中给出，未另存为文件。

### 下次会话

- 若 Codex 产出代码，需核对：HandState 是否真正持有数据且 UI 无自行自减、四个 TODO(DEP-xxx) 是否都写到了代码里。
- 验收通过后补充一条 `06_testing/` 验证记录。

---

## 2026-07-29 · BattleScene 手牌 UI 实施

- 在 `TinySpire/Assets/Scripts/UI/Battle/Hand/` 新增纯扇形布局计算、手牌容器、单卡视觉动画与 UGUI 事件转发脚本；手牌数量保持为 Inspector 的临时 `int` 占位字段，并在字段处标明未来仅替换为 Luban 数据来源。
- 通过 UnityMCP 创建 `TinySpire/Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab`，并在 `BattleScene.unity` 引用它。预制体保存手牌容器和 Inspector 配置；运行时仅复用（未修改）`CardView.prefab` 创建数量可变的独立根 Canvas 卡牌及其交互组件。实现扇形/溢出间距压缩、悬停抬起、独立 Canvas 层级提升、拖拽跟手、拖起后的补位与松手回弹，不含任何出牌判定或数据接入。
- 每张卡的 `HandCardVisual` 独立保存 Tween，并在新动画前终止旧 Tween；悬停采用 `Ease.OutBack` / 0.15 秒，补位与回弹采用 `Ease.OutCubic` / 0.22 秒。
- 静态验证：`dotnet build TinySpire/TinySpire.sln --no-restore` 通过（0 错误；9 条既有第三方程序集版本冲突警告）。场景文本检查未发现 `Toggle` 或 `ToggleGroup`。
- UnityMCP 已连到 `TinySpire@8edf130c865b3957`；Game View 验证了 3 / 5 / 10 张扇形布局与 10 张间距压缩，最后一次 Play Mode Console 为 0 错误、0 警告。未重启、未结束任何用户 Unity 进程。
- 修正扇形旋转方向：布局旋转改为 `-t × maxFanAngle`，使左右卡牌的轴线朝手牌下方汇聚；纯布局测试先复现左 `-15°` / 右 `15°` 的错误方向，再验证为左 `15°` / 右 `-15°`。UnityMCP 干净重启 Play Mode 后，Game View 视觉确认扇轴朝下，Console 为 0 错误、0 警告。
- 验证记录：`06_testing/2026-07-29-battlescene-hand-ui-sts-style.md`。

### 后续动作

- 需在 Game View 手动确认 hover 与拖拽交互手感；本次 UnityMCP 无指针事件注入能力，未伪造该两项结果。

---

## 2026-07-29 · 手牌 UI 杀戮尖塔化 grilling + 计划产出

- 用 `grilling` 技能逐项确认了手牌 UI 从 CD-002 的静态 Toggle 单选，升级为杀戮尖塔式悬停抬起 + 扇形排布 + 拖拽跟手视觉（本轮不做出牌判定）。
- 确认用户已将 DOTween/DOTweenPro 导入 `TinySpire/Assets/Plugins/Demigiant/`；确定悬停/重排补位的时长与缓动曲线参数。
- 手牌数量来源经用户确认后改为：本轮不引入接口抽象，直接用 Inspector 可调 `int` 字段，注释标记为未来 Luban 数据驱动的临时占位。
- 产出实现计划：`plans/2026-07-29-battlescene-hand-ui-sts-style.md`（proposal，未实施代码）。
- 新增代码决策 CD-003（DOTween 引入）与 CD-004（交互模型替换 CD-002），未删除旧记录。
- 本轮未写任何 C# 代码，未改动 `BattleScene.unity`，未 commit。用户计划将计划 + 配套 Prompt 交给外部 Codex 实施。

### 下次会话

- 若 Codex 产出代码，需核对实现是否符合本计划的 10 条决策，尤其是“不做出牌判定”的边界是否被越界。
- 验收通过后补充一条 `06_testing/` 验证记录。

---

## 2026-07-12 · BattleScene 基础手牌 UI

- 在 `TinySpire/Assets/Scenes/BattleScene.unity` 的现有 Canvas 下新增 `BattleCardUI`：包含底部手牌托盘、5 个 `CardView` 实例和单选高亮。
- 卡牌选择使用 UGUI `Toggle` + `ToggleGroup`；本轮只构造表现与可点击状态，没有新增运行时代码，也未接入卡牌数据、ViewModel 或出牌逻辑。
- 将现有 Screen Space - Camera Canvas 的 `planeDistance` 从 100 调整为 1，避免 UI 平面落在背景 Sprite 后方而被完全遮挡。
- Unity Game View 目视验证通过；EventSystem 点击第二张卡后，第一张取消选中、第二张进入选中状态；Console 0 错误、0 警告。
- 实现计划：`Docs/Copilot_Daedalus/plans/2026-07-12-battlescene-card-ui.md`；验证记录：`Docs/Copilot_Daedalus/06_testing/2026-07-12-battlescene-card-ui.md`。

---

## 2026-07-12 · LoadingScene 最短展示时间

- `SceneFlowService` 在 LoadingScene 完成切入后开始计时，保证目标场景切换前至少展示 1 秒。
- 内容准备耗时计入这 1 秒；仅补足剩余时间，不给慢加载额外增加固定等待。
- 补足延迟不受 `Time.timeScale` 影响。
- `dotnet build TinySpire.sln --no-restore` 通过（0 错误、3 个既有程序集版本冲突警告）；Unity Editor 当前存在运行实例，未启动额外实例进行 Play Mode 验证。
- 验证记录：`Docs/Copilot_Daedalus/06_testing/2026-07-12-loading-scene-minimum-duration.md`。

---

## 2026-07-06 · 初始化

- 创建 `Copilot_Daedalus/` 工作区，确立与 Pegasus 的协作约定
- 项目处于 planning 阶段，尚未开始编码

### 当前状态

- Unity 项目路径：`../TinySpire/`（相对于 `Docs/`）
- 现有代码：仅 `Assets/Scripts/Launcher.cs`
- BattleScene MVP 待实现（见 `Hermes_Pegasus/STATUS.md` P0 列表）

### 下次会话

- 阅读最新 `AGENT_HANDOFF.md` + `STATUS.md`
- 根据 P0 优先级制定 BattleScene 实现计划

---

## 2026-07-08 · 协作体系与文档库初始化

### 设计讨论（proposal，未落事实源）

- 起点讨论：从纯 C# 内核倒着往外长（计算 → 状态 → 时序 → UI），先不铺框架
- character 数据：确认 `模板 / 运行时` 两层；运行时持模板引用 + 只存会变字段
- `maxHp / currentHp` 同类两字段，约束 `current ≤ max`；max 变化时 current 是否同步 = **Open Question**
- 数据管线选型：**Luban + JSON 输出**（承重基础设施，提前定合理）；Theseus 去接入
- Open Question：max 变化时 current 同步规则；游戏 asmdef 布局（暂定"一个游戏 asmdef + 一个 Test asmdef"）

### 协作体系（对齐 AI_COLLABORATION_RULES.md）

- 四角色确认：Theseus（拍板）/ Pegasus（设计·数值）/ Calliope（创意·文本，Gemini）/ Daedalus（实现）
- Gemini 正式名从讨论中的 Urania 定为 **Calliope / 卡利俄佩**

### 文档库产出

- 新建 `AGENT_PROMPT.md` — 调用 Daedalus 的 Prompt 模板（6 节）
- 拆分身份/导航：新建 `AGENT_PROFILE.md`（身份），`README.md` 重写为 llm-workflow `index` 路由页
- 新建 `AGENTS.md` — 文档库入口 + llm-workflow 角色本地化映射
- 按 llm-workflow bootstrap 初始化本库：index-first ✅、status source = 本文件 ✅
- **完整实例布局初始化**（每个 AI 各维护一份 llm-workflow）：新建 8 个角色目录
  `00_inbox` `01_requirements` `04_research` `06_testing` `07_retrospective` `08_tools` `10_communication` `99_archive`，各带 keeper README；
  已有文件就地充当角色：`README`=index、`SESSION_LOG`=dev-log、`plans/`=design、`CODE_DECISIONS`=decision（事实源不移动）；`09_meetings` 不适用未建

### 记录的文档冲突（待 Theseus 裁决，未覆盖）

1. `.github/instructions/TinySpire.instructions.md` 仍是两人叙事（Pegasus+Daedalus），与四人体系不一致
2. 主库 `dev` 分支与 `Pegasus_Docs` worktree 存在同名文件双份，本次改动落在**主库 dev**

### 下次会话

- 待 Theseus 确认上述 proposal / Open Question 后，制定 BattleScene 首个实现计划
- Luban 接入完成后，落地 `CharacterTemplate` 表 → 生成 C# 类的目录/程序集归属
