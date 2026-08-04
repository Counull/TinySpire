---
title: M9 目标箭头与锁定框视觉反馈验收
page_type: testing
lifecycle: active
date: 2026-08-05
status: passed
scope: 曲线分段攻击箭头、目标锁定四角框、对应 Prefab 契约与回归
plan: ../plans/2026-08-05-m9-targeting-visual-feedback.md
status_source: ../SESSION_LOG.md
---

# M9 目标箭头与锁定框视觉反馈验收

## 实现口径

- `BattleTargetingArrowView` 的调用方仍只使用 `Show`、`UpdateArrow`、`Hide`。组件内部创建独立箭头 head 与 fragment 池，并沿三次贝塞尔曲线布置多个箭身；每个箭身和终点箭头都取本地曲线切线旋转。
- `ParticipantHudView` 为合法和悬停目标分别持有四个角件。它投影 `SpriteRenderer.bounds` 的八个世界角，得到屏幕上的实际边界后再加入 16 像素可调 padding，因此角件围住怪物而不是使用固定背景板。
- 两个 Prefab 复用已有正式 Targeting 精灵。没有修改 `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/` 的源图片或 Meta，也没有碰 Candidates 资源。

## 自动化结果

1. `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`：0 error；12 条既有程序集版本冲突 warning。
2. 第一轮 Unity 定向测试：8/8 通过，覆盖三项卡牌不飞目标回归、分段箭身行为与两个 Prefab 契约。
3. 完整相关 Unity EditMode 类集：26/26 通过、0 失败、0 跳过。类集为 `BattleTargetingArrowViewTests`、`BattleTargetingPrefabContractTests`、`ParticipantHudPrefabContractTests`、`BattleCardMotionTweenFactoryTests`、`HandCardMotionTests`。

## 人工画面验收边界

本轮未驱动用户的 BattleScene Game View。自动化已验证 fragment 的多段、切线朝向、复用与隐藏，以及锁定框的四角 Prefab 契约；fragment 长度、间距、曲率、角件尺寸和 padding 均保留为局部可调参数，留给后续整体 UI 改版时在实际画面中统一调参。
