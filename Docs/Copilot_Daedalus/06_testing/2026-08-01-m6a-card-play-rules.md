---
title: M6A 目标契约与纯合法性 module
page_type: testing
lifecycle: active
date: 2026-08-01
plan: ../plans/2026-08-01-m6-card-play-legality-target-selection.md
status_source: ../SESSION_LOG.md
---

# M6A 目标契约与纯合法性 module

## 验收范围

- `PlayCardCommand` 增加可空单目标 `TargetId`，不携带费用、目标规则、生命或 UI 预判结果。
- 增加目标、派生战斗结束与未知规则的稳定执行失败原因。
- 建立具体纯 C# `BattleCardPlayRules` 与不可变评估结果，不增加抽象 adapter、写入口或可变合法性镜像。
- 从当前 Turn、参与者、玩家卡区、静态 Tables 与 Encounter 顺序派生 Self/Enemy、费用与合法目标。
- 保持 M6A 边界：不接队列执行，不修改场景、Prefab、配置、卡区写入或 Effect。

## TDD 证据

1. 首个 Self tracer 在生产类型尚不存在时经 Unity 编译得到预期红灯：缺少 `BattleCardPlayRules`、`BattleCardPlayEvaluation`，且 `PlayCardCommand` 尚无三参数构造器。
2. 加入最小目标契约与 Self 规则后，任务 `bf8f6c038e6141629b0b56ce57a32e78` 为 **1/1 通过**。
3. Enemy 稳定候选用例随后以 `Expected True, But was False` 得到行为红灯，任务 `a6ab098ad24b4e27b6fe520a94a7c0db`；只加入 Enemy 派生与 Encounter 顺序候选后，Self/Enemy 为 **2/2 通过**。
4. 最终任务 `0a13a89d7b3f417d83c5ec8ea5162b80` 为 **8/8 通过**，0 failed、0 skipped，耗时 `0.4266496s`。

覆盖行为包括：

- Self 仅接受 Actor；Enemy 精确接受 Encounter 顺序中的存活敌人。
- Enemy 合法候选过滤死亡者，不依赖参与者字典枚举；重复评估候选顺序一致。
- 空目标、未知正数目标、死亡目标、错误阵营、未知规则、费用不足与双方不再各有存活者均返回稳定原因。
- 空目标仍保留目标规则、费用与合法候选预览；未知规则不按 Self/Enemy 猜测。
- 命令允许空目标，并拒绝非空的结构无效目标标识。
- 重复预览前后 Turn 与卡区快照保持同一对象，生命值及洗牌/敌人意图随机状态保持原值。

## 回归与静态编译

| 检查 | 结果 |
|---|---|
| M6A 前 `BattleCommandQueueTests` + `BattleTurnControllerTests` 基线 | **26/26 通过**；任务 `f87c0c3003f642ae8e203c6ef91e266d` |
| Unity 脚本刷新与 Meta 生成 | 通过；两个新 `.cs.meta` 由现有唯一 Editor 生成，最终 Console Error 0 |
| `BattleCardPlayRulesTests` | **8/8 通过**，0 failed、0 skipped |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning |

## 停止点结论

- `BattleCommandQueue.Submit`、只读 `Queue` / `Turn` 与轮次栅栏未改变。
- `BattleCommandQueue.cs`、`BattleTurnController.cs`、场景、Prefab、配置、卡区写入与 Effect 均未修改。
- 本切片只建立命令目标契约和无写入纯规则；权威队首重校验严格留给 M6B。
- 未运行 Addressables、Bootstrap 或 Game View；它们不属于 M6A 停止点，将按计划在 M6C/M6D 完成。
