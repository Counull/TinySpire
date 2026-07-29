--
title: 场景子 LifetimeScope 代码创建验证记录
page_type: testing
lifecycle: archived
date: 2026-07-30
scope: TinySpire/Assets/Scripts/Core/SceneFlowService.cs
superseded_by: CD-008（CODE_DECISIONS.md）
status_source: ../SESSION_LOG.md
---

# 场景子 LifetimeScope 代码创建验证记录（已归档）

> 本文档描述的“`SceneFlowService` 用 `CreateChild` 动态创建/持有场景子 Scope”方案已被用户撤回、代码已还原，当前仓库不存在对应实现。原文件头的 `source: CD-008` 是错误引用（当时 `CODE_DECISIONS.md` 里从未真正存在过 CD-008）。正确结论见 `CODE_DECISIONS.md` 的 CD-008：场景级服务应改为“挂载在场景内的 `LifetimeScope`”，不由代码动态创建/销毁。本文件仅作历史记录保留，不代表当前架构。

## 已完成验证

| 检查项 | 结果 |
|---|---|
| `SceneFlowService` 注入 Root `LifetimeScope` | 通过编译 |
| 使用 `CreateChild` 创建子 Scope GameObject | 代码路径已接入 |
| LoadingScene 与目标场景复用同一创建路径 | 通过代码审查 |
| 子 Scope GameObject 移入当前场景 | 通过代码审查 |
| 切换场景时由场景销毁旧 Scope | 通过代码审查 |
| Root 服务销毁时不重复释放场景 Scope | 通过代码审查 |
| 场景文件是否被修改 | 未修改 |

## 编译结果

`dotnet build TinySpire/TinySpire.sln --no-restore`：0 错误；保留项目既有依赖版本冲突和 API 过时警告。

本轮未启动 Unity Play Mode，未验证实际场景运行时的 Scope GameObject 层级；需要后续在 Unity 中确认 `SceneLifetimeScope_<场景名>` 的创建和销毁。
