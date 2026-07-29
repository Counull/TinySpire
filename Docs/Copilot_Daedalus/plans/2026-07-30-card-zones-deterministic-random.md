---
title: 卡牌区域与确定性洗牌
page_type: plan
lifecycle: active
date: 2026-07-30
scope: TinySpire/Assets/Scripts/Core/、TinySpire/Assets/Scripts/Battle/ 与 TinySpire/Assets/Scripts/UI/Battle/Hand/
source: 用户确认采用 Unity.Mathematics.Random 实施 M2 随机与牌区切片
status_source: ../SESSION_LOG.md
---

# 卡牌区域与确定性洗牌

## 目标

用一个权威状态统一持有整场战斗中的卡牌实例、抽牌堆、手牌、弃牌堆和消耗区；规则随机不使用 Unity 全局随机状态，而是使用可保存、可恢复、彼此独立的实例随机流。

## 实施边界

- `TinySpire.Core.GameRandom` 薄封装 `Unity.Mathematics.Random`，只暴露当前 `uint State`、`NextInt` 和 Fisher–Yates `Shuffle`。
- `CardZoneState` 创建卡组中的全部 `CardInstanceState`，并以四个互斥的有序 `CardInstanceId` 列表表达区域归属。
- `BattleSession` 用 `BattleSetupOptions.RandomSeed` 创建本场战斗的洗牌随机流，先洗牌，再按 `GameConfig.InitialHandCount` 抽取初始手牌。
- 手牌 UI 只读取 `CardZoneState.Hand` 与 `Cards`，拖过出牌线的现有占位行为改为把指定实例移入弃牌堆。

## 唯一事实

- `Cards` 是 `CardInstanceId → CardInstanceState` 的唯一实例定义映射。
- `DrawPile`、`Hand`、`DiscardPile`、`ExhaustPile` 是区域顺序与归属事实；卡牌实例不再保存镜像 `Zone` 字段。
- `GameRandom.State` 是该随机流下一次输出位置的唯一事实；不同时保存调用次数或下一随机值缓存。
- 区域计数直接由只读列表的 `Count` 派生。
- 一次洗牌产生的牌序是已经发生的战斗事实，不在每次读取时从初始种子重新计算。

## 行为

1. 按初始卡组顺序分配稳定且唯一的战斗内实例 ID。
2. 用专属 `GameRandom` 对抽牌堆执行 Fisher–Yates 洗牌。
3. 抽牌从抽牌堆尾部移动到手牌尾部。
4. 抽牌堆为空时，将弃牌堆全部移回抽牌堆并再次洗牌。
5. 抽牌堆与弃牌堆同时为空时少抽，不创建虚拟牌。
6. 单卡弃牌、单卡消耗与整手弃牌只通过 `CardZoneState` 写入口移动。

## 明确排除

- 不实现卡牌效果器、伤害、格挡、易伤或力量结算。
- 不实现目标合法性、费用扣除与回合调度；现有拖拽越线仍是占位提交入口。
- 不实现地图、奖励和敌人行为随机流；它们后续各自持有独立 `GameRandom`。
- 不实现 Run 存档与种子派生；当前 BattleScene 由 Inspector 提供正整数种子，登记为 `DEP-007`。
- 不修改表格、Luban 生成数据或 YooAsset 包。

## 验收

- 同一随机种子产生相同洗牌/初始手牌顺序。
- 两个随机实例互不推进。
- 保存并恢复 `GameRandom.State` 后，后续序列可重复。
- 重复模板卡拥有不同实例 ID。
- 任意时刻每张卡恰好位于一个区域。
- 弃牌重洗不丢牌、不复制牌。
- Bootstrap 进入 BattleScene 后显示 5 张由洗牌结果抽出的卡，控制台无本次改动引入的错误。

验证记录见 `../06_testing/2026-07-30-card-zones-deterministic-random.md`。
