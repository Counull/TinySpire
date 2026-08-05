---
title: M10D 交付级验证与性能基线
page_type: testing
lifecycle: completed
created: 2026-08-05
status: completed-with-non-m10-suite-failures-recorded
plan: ../plans/2026-08-05-m10-battlescene-conformance.md
status_source: ../SESSION_LOG.md
---

# M10D 交付级验证与性能基线

## 当前结论

M10D 已完成：M10A--D 定向回归、配置/Bootstrap/确定性证据、真实 Game View 默认启动、M9 已验收的真实重开/退出链路复用，以及可重复基线均已完成审计。完整 EditMode 的 451 项中有两项失败，但经独立复现、HEAD 差异和直接依赖审计确认，它们属于不依赖 M10 变更的 M9 UI/Targeting 测试/预制体契约；它们被完整记录为非 M10 全量套件异常，而不被伪报为全绿，也不阻断 M10 的相关回归收口。

本记录严格区分已执行的检查、计划中但未以本切片重复执行的工作，以及不能从当前证据推出的结论；没有把主观流畅度或未给出的预算写成性能通过。

## 先有红灯

- 新增 `TinySpire/Assets/Editor/Tests/BattleDeliveryM10DTests.cs` 后，Unity 编译准确报出 `CS0246: M10DeliveryEvidence` 和 `CS0103: M10DeliveryBaseline` 不存在。随后仅在该测试文件内补齐非持久化测量模型和夹具。
- 完整 EditMode 首次运行后出现两项失败；二者已独立复现，见“完整回归审计”。这不是由 M10D 改写 UI/Targeting 得到的红灯；它们不构成 M10 修复授权，也不被用来声称全量套件通过。

## 最小 M10D 实现

`BattleDeliveryM10DTests.cs` 只复用 M10C 的 `M10BattleReplayHarness.Replay(fps)`，由其现有的 `BattleCommandQueue.Submit` 写入和 `Queue`、`Turn`、`BattleSession`、`CardZones` 只读取证链产生测量样本。新夹具不修改生产 `Queue`、`Turn`、settlement、战斗规则、DI、场景或 Prefab，也不保存第二份权威战斗状态。

每个 30/60/120 FPS 档进行预热并记录两个各 5 次的全回合窗口，比较：

- 可复现的领域轨迹；
- `Stopwatch` 墙钟窗口时长；
- `GC.GetAllocatedBytesForCurrentThread` 的当前线程差值；
- `DOTween.TotalActiveTweens()` 是否回到采样前基线。

这是 Editor Test 的微基线，`window=EditorTest_NoGameView`；它用于以后同环境的回归比较，不能替代 Player 或真实 Game View 性能结论。

## 已执行的自动与静态检查

| 检查 | 结果 | 证据边界 |
| --- | --- | --- |
| M10D 定向 EditMode | `BattleDeliveryM10DTests` 1/1 通过 | 测量夹具和两个 5 样本窗口 |
| M10A--M10D 聚合 EditMode | 25/25 通过 | ConfigService 7、manifest 2、Bootstrap/黄金基线 12、M10C 3、M10D 1 |
| solution build | `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`：0 error、12 条既有程序集版本 warning | C# 编译；不等同运行时验收 |
| Addressables 静态地址 | `TinySpire/Assets/AddressableAssetsData/AssetGroups/TinySpire Scenes.asset:19` 为 `Assets/Scenes/BattleScene.unity` | M10 未修改 DataTables、Localization 或可寻址内容，故未运行 Luban、同步或 Local Content 重建 |
| PlayMode 测试程序集 | 未发现 `PlayMode`/`TestAssemblies` asmdef | 不把“不存在的自动程序集”伪报为 PlayMode 测试；真实路径见下文 |
| 范围审计 | M10 生产差异仅在 Core 配置/Bootstrap 和既有 Editor 构建校验文件；M10D 新增内容仅为测试和本记录 | 未改 Queue/Turn/settlement、游戏规则、场景、Prefab、DI、ProjectSettings、asmdef、HybridCLR 或受保护路径 |

## 可重复基线（非通过阈值）

环境：Unity `6000.5.5f1`、Windows 11 `10.0.26200`、AMD Ryzen 7 3700X、16,309 MB RAM、NVIDIA GeForce RTX 4070 Ti SUPER、桌面 `2560x1440@59.95`。采样工具为 `Stopwatch + GC.GetAllocatedBytesForCurrentThread + DOTween.TotalActiveTweens`，每档两个窗口各 5 样本。

| FPS | 首窗口中位数 ms | 次窗口中位数 ms | 差值 ms | 首/次窗口当前线程分配 | 轨迹与 Tween 清理 |
| --- | ---: | ---: | ---: | ---: | --- |
| 30 | 2.212 | 2.227 | +0.015 | 0 / 0 bytes | 相同 / 回到 0 |
| 60 | 2.252 | 2.243 | -0.010 | 0 / 0 bytes | 相同 / 回到 0 |
| 120 | 2.289 | 2.270 | -0.018 | 0 / 0 bytes | 相同 / 回到 0 |

没有用户给出的帧时间、GC 或设备预算；以上只报告同次双窗口差异。当前线程分配为 0 不代表整个进程零分配，也不能与未启用相同 Profiler 条件的 Player 数据直接比较。

## 真实 Game View 与 Console 证据

在唯一 Unity `TinySpire@8edf130c865b3957`（Unity 6000.5.5f1）中正常从 Bootstrap 进入 BattleScene，未注入输入、未启动第二个 Editor。Game View 为 `1600x1100`、`timeScale=1`、Very Low quality，画面显示第 1 回合、3/3 能量、5 张默认手牌、英雄 30/30、两个敌人各 20/20，以及部分中文卡牌与参与者文案；画面中的战斗流程英文标签不作为 zh-CN 黄金基线证据，完整双语口径由 M10B 自动测试覆盖。截图保存在项目相对路径 `Docs/Copilot_Daedalus/06_testing/evidence/2026-08-05-m10d/m10d-bootstrap-default.png`。

运行中 `BattleLifetimeScope` 为 1；停止后回到 BootstrapScene 且为 0。Console 只有 `game-config.json 已加载。` 产品日志，没有产品 warning/error。Profiler 仅在该唯一 Editor 中短暂启用 CPU/Memory/Rendering 区域，3 个采样帧为：

| 项目 | 三个采样值 |
| --- | --- |
| CPU frame ms | 8.791 / 8.945 / 8.718 |
| Main CPU ms | 2.027 / 2.116 / 2.097 |
| Render CPU ms | 1.470 / 1.168 / 1.175 |
| GPU ms | 0.603 / 0.624 / 0.612 |
| GC Allocated In Frame | 22,801 bytes（单个有效计数） |
| SetPass | 55 |
| Triangles / Vertices | 1,068 / 1,756 |

Profiler 会改变被测运行条件，且样本只有 3 帧；这些数字是可复查观察值，不是性能通过声明。`Total Allocated`、Batches 和 Drawing Calls 在该会话中为 invalid/unknown，未被误记为零。

## 完整回归审计（非 M10 阻断）

完整 EditMode 运行共完成 451 项，其中两项失败；M10 定向 25/25 仍通过。两项均已独立复现：

1. `BattleParticipantFeedbackRoutingTests.PlayCardPresentation_UsesPreludeThenEffectThenOriginalCardMovedOrder`：第一轮 `adapter.Tick()` 后，期望卡片屏幕中心不等于 `(800.00, 273.61)`，实际仍为 `(800.00, 273.61)`。
2. `HandCardTargetFocusTests.TargetFocus_LateUpdate_TracksMovingCardWhilePointerStaysStill`：`NullReferenceException` 于测试第 188 行读取 `_lineRect`。当前 `BattleTargetingArrowView` 源类型没有该字段，说明测试/Targeting 预制体契约需要在 M9 UI/Targeting 范围内单独核对。

两项都不属于 M10A--D 的允许生产路径。审计证据是：两项测试及 Targeting 源路径相对 `HEAD` 无差异；失败的 PlayCard 测试仅初始化 Localization、CardZones、Hand/Presenter/Adapter，并不创建或初始化 `ConfigService`；M10 改动的 Core 文件不引用 Hand、Targeting 或这两项测试。M9G 已有真实按钮连续重开、退出及 scope 清理证据；M10 未修改这些路径。因此 M10 以“相关 EditMode/真实路径/静态审计”完成，而这两项失败保留为需要单独授权的 M9 UI/Targeting 修复或测试契约校准切片。未来切片的回滚路径只能包含其自身 UI/测试改动，不应回滚 M10 的配置和回归夹具。

## 未执行或不作结论的项目

- 没有重跑 Luban、`TinySpire/Build/Sync and Build All` 或 Local Content：M10B 未改 DataTables、Localization 或可寻址内容，项目规则不要求且不应扩大操作。
- 没有重新制作 M10B 的每种故障 Game View 截图：七种 typed failure 已由 M10B 自动路径覆盖；故意损坏项目内配置来强制截图会改变当前交付物。
- 没有重新构建 Player 或新的长时压力基线；本切片没有新增构建产物，也没有用户提供设备/预算。M9G 的仓库外 Development Player 退出证据和真实按钮重开/退出证据仅作为未受 M10 改动影响的既有链路复用，并不被误写成新的 M10 Player 测量。
- 完整 EditMode 的两项范围外失败已作为套件异常记录；它们不等价于 M10 的相关回归失败，M10D 与 M10 已按本计划完成。
