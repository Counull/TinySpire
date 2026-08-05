---
title: M10C 确定性、帧率无关与生命周期回归
page_type: testing
lifecycle: active
date: 2026-08-05
status: passed
scope: 默认 BattleScene 1001/5001/seed5 的 Submit 回放、30/60/120 FPS、加速、立即完成、取消、重启与 Scope/Scene 生命周期
plan: ../plans/2026-08-05-m10-battlescene-conformance.md
status_source: ../SESSION_LOG.md
---

# M10C 确定性、帧率无关与生命周期回归

## 结论

M10C 已完成并在 M10D 前停止。新增的测试专用夹具使用默认 BattleScene 的 1001/5001/seed5 输入，经由既有 `BattleCommandQueue.Submit` 提交命令；每个表现完成点只读取 `Queue`、`Turn`、`BattleSession`、`CardZones`、既有结算记录与既有生命周期事件。它证明 30/60/120 FPS、8 倍加速和立即完成得到相同的可比较权威轨迹，并覆盖取消、重启与 Scope/Scene 生命周期边界。

这不是性能通过结论，也未将测试替代真实 Game View 交互。M10D 才负责交付级 Game View、Addressables 条件路径和可重复性能基线。

## 红灯到最小实现

| 顺序 | 精确红灯 | 最小实现 |
| --- | --- | --- |
| C1 | `M10BattleReplayTrace` 和 `M10BattleReplayHarness` 不存在：`CS0246`、`CS0103`。 | 新增 `BattleConformanceM10CTests.cs` 中的测试专用回放 harness；预先注册既有协调器后，直接调用生产 `Submit`，由既有 presentation adapter 用指定 unscaled delta 推进。 |
| C2 | `ReplayAccelerated`、`ReplayImmediately` 不存在：`CS0117`。 | 仅调用既有 adapter 的 `SetPresentationSpeed` 与 `CompleteImmediately`，不重排 cue、不增加动画队列。 |
| C3 | `M10BattleLifecycleEvidence` 与 `VerifyCancellationAndRestart` 不存在：`CS0246`、`CS0117`。 | 在测试中 dispose 旧 adapter，断言旧 Queue 的执行/表现屏障不晚到推进、生命周期观察者无晚到发布、DOTween 活动数回到基线，并用两个新回放比对重启轨迹。 |

实现过程中曾因缺少 R3 扩展导入出现一次临时 `CS1660`；只补回 using 后恢复编译，未改变测试或生产 API 语义。

## 取证边界

- 生产代码、`BattleCommandQueue`、`BattleTurnController`、结算和公式均未修改；没有新增权威写入口。
- `M10TracingPresentation` 只将已执行命令的结果冻结为测试轨迹文本，随后委托既有 `BattleCommandPresentationAdapter` 完成真实表现；它不是第二份 Hand、CardZones、Combatant 或 Intent 事实。
- 生命周期测试的观察者只记录 dispose 后是否有晚到生命周期事件；正常 `using` 释放该订阅。DOTween 只以活动数量回到测试开始基线为清理证据，不把全局 tween 状态作为新的业务事实。
- 本切片没有修改 DataTables、Localization 或可寻址内容，因此没有运行 Luban、`TinySpire/Build/Sync and Build All` 或 Local Content；这不是对内容变更时生成验收的替代。

## 自动回归与构建

| 检查 | 结果 | 说明 |
| --- | --- | --- |
| Unity EditMode C1 | `3d8e55f47eb04600a548996f885d80d9`：**1/1 passed** | 30/60/120 FPS 的默认回放产生相同权威轨迹。 |
| Unity EditMode C1+C2 | `8eef040130d048b28a37a3d12ca84c7c`：**2/2 passed** | 增加 8 倍加速与立即完成，轨迹仍一致。 |
| Unity EditMode C1+C2+C3 | `4a6cef7ad5f64abd8403b1429a3e044f`：**3/3 passed** | 覆盖取消、重启和 tween/晚到事件边界。 |
| 相关聚合 | `ee9720d3161a473d950940fe80edc1f1`：**53/53 passed** | `BattleConformanceM10CTests`、presentation runner/adapter、M8D Queue、BattleSession。 |
| solution build | `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`：**0 error** | 保留既有 12 条程序集版本冲突 warning。 |

source domain refresh 后，MCP 中的两个较早任务 `61d5…` 和 `d194…` 未返回最终结果；仅以 scoped `clear_stuck` 清除了 MCP 任务记录，未终止或重启 Unity 进程。它们不构成通过证据，表中四项具名结果才是本切片结论。

## 真实 Unity 观察

- 仅使用当前唯一 Unity 6000.5.5f1 Editor。正常 Play Mode 从 BootstrapScene 进入 BattleScene，`BattleLifetimeScope` 为 1，Console 记录 `game-config.json 已加载。`，未见产品 Error/Warning。
- 正常停止 Play Mode 后回到 BootstrapScene，`BattleLifetimeScope` 为 0，Console 仍无产品 Error/Warning。这是实际场景退出后的 Scope 销毁观察。
- 未驱动 Game View 指针、按钮或 Restart；未记录设备、分辨率、帧时间或分配采样。因此不能将本观察升级为真实交互或性能通过。

## 范围、回滚与后续

本切片只新增 `TinySpire/Assets/Editor/Tests/BattleConformanceM10CTests.cs` 及其 Unity meta 和本页/索引/状态同步文档。未触碰 Queue/Turn/settlement 公共契约、战斗规则、Scene、Prefab、DI、ProjectSettings、asmdef、HybridCLR、Candidates、Targeting 或用户保护的无关改动。回滚单位是这一个测试文件、其 meta 与本切片文档。

M10C 的独立停止点到此为止。M10D 必须从交付与性能的独立红灯开始；在没有用户给出的设备、帧时间或分配预算前，只能报告环境和回归差异，不能作猜测性性能重构或声称主观流畅即通过。
