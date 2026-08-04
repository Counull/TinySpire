---
title: M9 出牌不飞向怪物
page_type: plan
lifecycle: active
created: 2026-08-05
updated: 2026-08-05
scope: PlayCard 前奏表现与 Hand 到 DiscardPile 卡牌运动
status: validated
status_source: ../SESSION_LOG.md
source: 用户 2026-08-05 反馈：出牌后牌会移动到怪物身上，这个不要
---

# M9 出牌不飞向怪物

## 验证结果（2026-08-05）

实现已在 Unity Editor 空闲后完成定向验证。与本项直接对应的三个 EditMode 用例均通过，完整相关类回归为 26/26 通过；静态 solution build 为 0 error、12 条既有程序集版本冲突 warning。详见 `../06_testing/2026-08-05-play-card-no-target-flight.md`。

## 当前结论

用户反馈确认：出牌后的卡牌不应飞向怪物。`PlayCard` 仍保留一个命令级 Prelude，以保持既有的“Prelude 先于 Order 0”表现编排契约；但该 Prelude 不再创建卡牌运动，也不再读取目标屏幕锚点。卡牌只在其原有 `CardMoved(Hand → DiscardPile)` settlement 到达时，按冻结的 `Order` 飞向弃牌堆。

## 范围

- 删除 `PlayCardToTarget` 卡牌运动类别，以及 `BattleCardMotionCue` 对目标身份的承载。
- 将 `PlayCard` Prelude 路由为 `PlayCardTransientHold`：零时长、无位移，只负责在同一 runner 内持有并清理离手 transient。
- 保留 `BattleCommandPresentationPlan` 对 `PlayCard` Prelude 的生成、其 CardId/TargetId 冻结身份，以及后续 settlement 的原始顺序。
- 用定向 EditMode 测试证明：Prelude 不创建卡牌运动，伤害反馈后仍由 `Hand → DiscardPile` 记录创建唯一 transient。

## 明确不改

- 不修改 `BattleCommandQueue`、`Turn`、Effect、CardZones、settlement 类型或 `Order`。
- 不修改目标合法性、目标箭头、角色/怪物锚点、场景、Prefab、Addressables、DataTables 或 Candidates 资源。
- 不引入第二个 completion、动画队列、权威状态镜像或全局输入锁。

## 验收

1. `PlayCard` 计划依旧拥有一个冻结的 Prelude，且 settlement 顺序不变。
2. Prelude 只产生无位移的 `PlayCardTransientHold`，不要求目标锚点，也不移动 transient 卡牌。
3. 同一张离手卡只在 `CardMoved(Hand → DiscardPile)` 的 settlement `Order` 产生一次飞向弃牌堆的运动。
4. Card motion、Hand motion、presentation plan/runner 定向测试与 solution build 通过；不触碰无关工作区改动。

## 风险与回滚

主要风险是误删 Prelude，从而破坏 M9 的命令级顺序契约；因此只移除其可见卡牌运动消费者，并保留 plan/runner 路径。若后续 UI 需要新的出牌特效，应以现有 Prelude 为入口建立独立、冻结数据驱动的反馈，而不是重新让卡牌飞向目标。
