---
title: M9 出牌不飞向怪物验收
page_type: testing
lifecycle: active
date: 2026-08-05
status: passed
scope: PlayCard transient 持有、Hand 到 DiscardPile 运动、异常清理与既有表现顺序
plan: ../plans/2026-08-05-play-card-no-target-flight.md
status_source: ../SESSION_LOG.md
---

# M9 出牌不飞向怪物验收

## 当前结论

实现已删除 `PlayCardToTarget` 与卡牌运动 cue 的 `TargetId`。`PlayCardTransientHold` 是零时长、无位移的 transient 生命周期 lease：它不读取角色/怪物屏幕锚点，只保证离手卡在后续 cue 构造失败、正常完成或取消时仍能由同一 runner 幂等释放。真正的卡牌运动只保留 `CardMoved(Hand → DiscardPile)`，并继续发生在冻结 settlement 的原始 `Order`。

## 已完成验证

- `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`：**0 error，12 条既有程序集版本冲突 warning**。
- 静态检索确认 production 与 Editor tests 中不再出现 `PlayCardToTarget`；卡牌运动 cue 不再承载 `TargetId`。

## 待完成的 Unity 验证

检查时既有 Unity Editor 正在 Play Mode 转换中（`BattleScene`），因此没有启动第二个 Editor、没有驱动用户的 Game View，也没有运行可能改变当前播放状态的 Test Runner。待 Editor 空闲后执行：

1. `BattleCardMotionTweenFactoryTests.Play_PlayCardPreludeHoldsTransientWithoutTargetFlightAndCardMovedAtItsOwnOrder`：Prelude 先于 Order 0，但只产生 `PlayCardTransientHold`；`HandToDiscard` 仍位于自身 Order。
2. `HandCardMotionTests.PlayCardPreludeHoldsTransient_CardMovesOnlyToDiscardThenCleansExactlyOnce`：半程位置只在手牌与弃牌堆之间，不出现怪物目标轨迹；收口时 transient 只销毁一次。
3. `HandCardMotionTests.PlayCardPreludeHold_LaterCueBuildThrows_ReleasesDetachedTransient`：后续 cue 构造 fault 时，无位移 hold 仍回收 transient。
4. 相关 card-motion/hand/presentation EditMode 回归与全量 EditMode。

## 当前验证结果（2026-08-05）

没有启动第二个 Editor，也没有驱动用户的 Game View。Unity Editor 空闲后执行了与卡牌运动、目标箭头和锁定框共同相关的完整定向类集，结果为 26/26 通过、0 失败、0 跳过。其中本项直接覆盖：

1. `BattleCardMotionTweenFactoryTests.Play_PlayCardPreludeHoldsTransientWithoutTargetFlightAndCardMovedAtItsOwnOrder`：Prelude 先于 Order 0，但只产生 `PlayCardTransientHold`，`HandToDiscard` 仍位于自身 Order。
2. `HandCardMotionTests.PlayCardPreludeHoldsTransient_CardMovesOnlyToDiscardThenCleansExactlyOnce`：半程位置只在手牌与弃牌堆之间，不出现怪物目标轨迹；收口时 transient 只销毁一次。
3. `HandCardMotionTests.PlayCardPreludeHold_LaterCueBuildThrows_ReleasesDetachedTransient`：后续 cue 构造 fault 时，无位移 hold 仍回收 transient。

同一轮 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error、12 条既有程序集版本冲突 warning。

## 范围结论

未修改 Queue、Turn、settlement、CardZones、Effect、目标规则、目标箭头、Scene、Prefab、DataTables、Addressables 或 Candidates；也没有添加第二个 completion、动画队列或权威状态镜像。
