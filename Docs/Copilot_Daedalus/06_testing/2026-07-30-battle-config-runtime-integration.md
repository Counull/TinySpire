---
title: 战斗配置接入运行时 · 验证记录
page_type: testing
lifecycle: active
date: 2026-07-30
source: ../plans/2026-07-30-battle-config-runtime-integration.md
status_source: ../SESSION_LOG.md
---

# 战斗配置接入运行时 · 验证记录

> 后续演进：本文当时未验证的洗牌、抽牌、弃牌和重洗已在 `2026-07-30-card-zones-deterministic-random.md` 单独实施并验证；本文保留原切片的验收结果。

## 自动测试

- 当时的 Unity EditMode：`BattleStateTests`、`HandStateTests`、`BattleSessionTests` 共 6 项，6 passed / 0 failed；后续测试类与状态类型已随 `CardZoneState` 切片演进。
- 覆盖参与者唯一 ID、按 ID 查询、生命写入、重复模板卡实例身份、按实例出牌，以及英雄/遭遇/初始手牌的配置实例化。
- `dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal`：0 error；保留 12 条既有程序集版本冲突 warning。

## Play Mode 冒烟

- 从 `BootstrapScene` 启动并通过 YooAsset 进入 `BattleScene`。
- 运行时发现 5 个 `HandCardVisual`。
- 5 张卡均为不同实例 ID，模板均为 3002，标题均为 `Strike`，费用均为 `1`。
- 本次接入无 error。
- 仍出现既有 YooAsset warning：`Operation handle is released : Assets/Scenes/LoadingScene.unity`，堆栈位于 `SceneFlowService.cs:79`；本轮未修改该流程。

## 未验证/未实现

- 未验证效果结算，因为本轮明确不实现效果器。
- 本记录形成时尚未验证洗牌、抽牌、弃牌和重洗；`DEP-006` 当时为 open，现已由后续卡牌区域切片解决。
- 未修改 `DataTables/Datas/` 或 `Assets/GameData/`，因此本轮不需要重新运行 Luban 或重建 YooAsset `Main` 包。
