---
title: 卡牌区域与确定性洗牌 · 验证记录
page_type: testing
lifecycle: active
date: 2026-07-30
source: ../plans/2026-07-30-card-zones-deterministic-random.md
status_source: ../SESSION_LOG.md
---

# 卡牌区域与确定性洗牌 · 验证记录

## TDD 记录

- Red：先新增 `GameRandomTests` 与 `CardZoneStateTests`；Unity 编译按预期报告 `CardZoneState` 尚不存在。
- Green：实现 `GameRandom`、`CardZoneState` 并接入 `BattleSession`/手牌 UI 后，定向 EditMode 10/10 通过。

## 自动测试覆盖

- 同种子洗牌顺序一致。
- 随机状态恢复后，后续序列一致。
- 两个实例随机流互不推进。
- 全部卡牌先进入洗牌后的抽牌堆，重复模板仍拥有不同实例 ID。
- 抽牌、单卡弃牌、单卡消耗和整手弃牌保持区域互斥。
- 抽牌堆为空时从弃牌堆重洗，不丢牌、不复制牌。
- `BattleSession` 创建全部 10 张卡牌实例，并抽取配置指定的 5 张初始手牌。

## 最终验证

- 完整 Unity EditMode：13/13 passed，0 failed。
- `dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal`：0 error；保留 12 条既有程序集版本冲突 warning。
- Bootstrap → LoadingScene → BattleScene 正常完成。
- 运行时 `CardZoneState`：10 个实例、抽牌堆 5、手牌 5、弃牌堆 0、消耗区 0；场景存在 5 个 `HandCardVisual`。
- 种子 1 的初始手牌模板顺序为 `3002, 3004, 3002, 3003, 3002`，证明不再按卡组前五张取牌。
- Console：0 error；保留一条既有 YooAsset warning：`Operation handle is released : Assets/Scenes/LoadingScene.unity`，本轮未修改该加载流程。
- `git diff --check` 无错误，仅提示两份既有文档后续由 Git 触碰时会将 CRLF 转为 LF。

## 双轴代码审查

- Spec：通过，无 P1/P2；确定性、状态恢复、实例流隔离、四区互斥、初始洗牌抽牌与不实现效果器均符合规格。
- Standards 初审发现随机流可被外部别名推进、视图保存冗余模板 ID、旧文档存在过期口径；已分别改为 `CardZoneState` 内部创建随机流、删除 `HandCardVisual.CardTemplateId`、将旧计划标记为 superseded 并修正文档索引。
- Standards 复核：上述问题全部关闭，未引入新的 P1/P2。

## 未验证 / 未实施

- 未验证效果结算、费用、目标、回合结束自动弃牌或敌人行为，因为这些不在本切片范围内。
- 未修改 `DataTables/Datas/`、`Assets/GameData/` 或生成配置，因此不运行 Luban，也不重建 YooAsset `Main` 包。
