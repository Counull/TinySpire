---
title: LoadingScene 最短展示时间验证
page_type: testing
lifecycle: active
date: 2026-07-12
scope: SceneFlowService 场景过渡
source: TinySpire/Assets/Scripts/Core/SceneFlowService.cs
status_source: Docs/Copilot_Daedalus/SESSION_LOG.md
---

# LoadingScene 最短展示时间验证

## 验收口径

- LoadingScene 完成切入后，至少展示 1 秒再切换到目标场景。
- 内容准备不足 1 秒时，只补足剩余时间。
- 内容准备超过 1 秒时，不再额外等待。
- `Time.timeScale == 0` 时，保底计时仍能完成。

## 验证结果

- 静态代码检查通过：计时起点位于 LoadingScene 加载完成之后。
- 静态代码检查通过：剩余时间按 `1 秒 - 已用时间` 计算，并仅在大于零时延迟。
- 静态代码检查通过：`UniTask.Delay(..., ignoreTimeScale: true)` 不依赖缩放时间。
- 编译检查通过：`dotnet build TinySpire.sln --no-restore` 返回 0 错误、3 个既有程序集版本冲突警告。
- 未执行 Unity Play Mode 运行时验证：检查时已有 Unity Editor 实例运行，未启动额外实例。

## 待人工验收

1. 从 BootstrapScene 启动，观察 LoadingScene 不少于 1 秒。
2. 模拟内容准备超过 1 秒，确认不会在准备完成后再固定等待 1 秒。
3. 在切换前令 `Time.timeScale = 0`，确认仍可进入目标场景。
