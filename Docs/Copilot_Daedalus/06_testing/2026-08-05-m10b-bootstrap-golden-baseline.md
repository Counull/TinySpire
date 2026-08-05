---
title: M10B Bootstrap 可见失败路由与默认内容黄金基线
page_type: testing
lifecycle: active
created: 2026-08-05
status_source: ../SESSION_LOG.md
plan: ../plans/2026-08-05-m10-battlescene-conformance.md
---

# M10B · Bootstrap 可见失败路由与默认内容黄金基线

## 范围与结论

M10B 已在 M10A 停止点之后完成，并停止于本切片。`GameLauncher` 现在只编排启动：它只捕获 `ConfigInitializationException`，将其交给 Bootstrap 的最小失败展示，随后返回，不会初始化 Localization 或调用 `SceneFlowService.LoadInitialSceneAsync`。未知异常不被吞掉。

`BootstrapFailureView` 在 Bootstrap 对象上按运行时需要创建；失败时显示稳定 `CFG-001`～`CFG-007` 代码、失败资源地址与“修复配置后重启应用”的指引。它不提供重试、MainMenu、Run、第二条场景流或任何配置/战斗写入口。正常内容仍由当前唯一 Unity Editor 从 `BootstrapScene` 进入 `BattleScene`。

默认内容没有改值：黄金测试同时读取 `DataTables/Datas` 作者表、`TinySpire/Assets/GameData` 生成 JSON、`TinySpire/Assets/Localization` String Table 与 `i18n.xlsx`，锁定 5/3、5×Strike/4×Defend/1×Bash、6/5/8/2、30/20，以及 Strike/Defend/Bash 的 en/zh-CN 名称、描述和 Smart String 标记。`LocalizationBuildTools` 的构建前必需键清单已加入 7 个运行时战斗流程键，并保持 Excel → Localization 单一内容来源。

## 精确红灯

1. 新增 `GameLauncherM10BTests` 后，串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 精确失败：`CS0117: GameLauncher 未包含 RunStartupAsync`。这证明既有启动编排尚不能把 typed 配置失败转交为可观察停止状态。
2. 新增 `BattleGoldenBaselineM10BTests.LocalizationBuildGate_RequiresAllRuntimeBattleFlowKeys` 后，Unity EditMode 任务 `6e7fc222f4c94adc9bad8a534c1de2aa` 失败：`LocalizationBuildTools must declare required battle-flow keys. Expected: not null; But was: null`。同一测试类的作者表/生成 JSON/Localization 三方黄金断言可执行。

## 最小实现

- 新增 `IBootstrapFailurePresenter` 与 `BootstrapFailureView`；View 只保存并绘制失败文本，不保存第二份配置或战斗事实。
- `Bootstrap` 在现有 GameObject 上按需创建 View，并仅以接口实例注册给既有容器；没有新 Scene、Prefab 或 DI 结构替换。
- `GameLauncher.RunStartupAsync` 是内部的启动编排 seam：资源初始化后只在配置阶段捕获 typed failure；成功路径仍是配置 → Localization → 首场景，其他异常照常抛出。
- `LocalizationBuildTools` 将 `battle.ui.battle.start`、玩家/敌人回合、胜利/失败、重开/退出七个键纳入 Excel 覆盖和 en/zh-CN String Table 校验。

## 自动与 Editor 证据

| 检查 | 结果 | 说明 |
|---|---:|---|
| `GameLauncherM10BTests` | 10/10 passed | 七类 typed failure 均展示失败且不继续；未知异常继续抛出；成功顺序仍至首场景；View 文本包含稳定码、地址和重启指引。任务 `8745f9568e2848348027ef033cdb74bf`。 |
| `BattleGoldenBaselineM10BTests` | 2/2 passed | 三方黄金基线与运行时战斗流程 key 门禁。任务 `ed5b21bc6ea5443e9d9fa86d243b1eb3`。 |
| M10A + M10B 聚合 EditMode | 21/21 passed | `ConfigServiceTests`、`ConfigTableManifestValidatorTests`、上述两个 M10B 类。任务 `7190d4bdca904d5f89104b17c21716d3`。 |
| `TinySpire/Localization/Validate Battle Card Text` | passed | 当前 Unity Editor Console 输出 `TinySpire battle card localization validation passed.`；含新增运行时流程键。 |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | 0 error | 保留既有 12 条 Unity/R3/UniTask 程序集版本冲突 warning；不将它们记作本切片代码 warning。 |

## Bootstrap / Game View 证据边界

当前唯一 Unity 6000.5.5f1 Editor 在 Play Mode 中从 `Assets/Scenes/BootstrapScene.unity` 实际切换到 `Assets/Scenes/BattleScene.unity`；Console 记录 `game-config.json 已加载。`，随后已退出 Play Mode。没有启动第二个 Editor，也没有驱动用户的活动 Game View。

配置失败路径的“停止且不加载场景”由七类 typed-failure 自动路由测试和失败 View 断言证明。没有为制造真实运行时坏配置而改写、移动或临时篡改 `DataTables`、`Assets/GameData`、Localization、Addressables 或 Scene，因此这不是外部 Game View 的损坏配置截图；不得将其描述为已完成真实损坏资源演练。

## Addressables / 内容生成边界

本切片没有修改 `DataTables/Datas/**`、`TinySpire/Assets/GameData/**`、`TinySpire/Assets/Localization/**` 或可寻址内容。因此没有运行 Luban、Localization Import、`TinySpire/Build/Sync and Build All` 或 Local Content 重建；这不是跳过应有生成，而是避免对未改内容制造无关产物。完整 `Assets/...` 稳定地址由现有加载链保持，正常 Bootstrap Console 未见 `InvalidKey`。

## 范围审计与停止点

修改仅限 Core Bootstrap failure routing、现有 Localization Editor 校验、M10B Editor 测试和本记录/索引/状态文档。没有修改表格、生成 JSON、Localization 资产、Addressables 配置、Scene/Prefab、BattleScene 布局、Queue、Turn、settlement、战斗公式、Targeting/Candidates 或受保护美术路径；未暂存、提交或推送。DEP 状态不变。

M10B 在此停止。M10C 仍须以 `BattleCommandQueue.Submit` 和既有只读 Queue/Turn/BattleSession/CardZones 事实另起红灯，不能把本页的启动/内容证据当作确定性、帧率或生命周期验收。
