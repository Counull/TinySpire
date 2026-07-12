---
created: 2026-07-06
updated: 2026-07-12
---

# Daedalus · 代码决策记录

> 代码级决策，补充 `Hermes_Pegasus/design/decisions.md`（玩法级决策）。

## CD-001：LoadingScene 采用最短展示时间

**问题**：资源准备很快时，LoadingScene 会一闪而过，无法形成稳定过渡。

**选择**：LoadingScene 完成切入后开始真实时间计时，目标场景加载前保证累计展示至少 1 秒；内容准备耗时计入展示时间。

**理由**：快加载时补足视觉过渡，慢加载时不叠加无意义的固定等待；真实时间延迟避免受到游戏暂停或 `Time.timeScale` 的影响。

**影响**：`TinySpire/Assets/Scripts/Core/SceneFlowService.cs` 的所有带 LoadingScene 的场景切换。

## CD-002：BattleScene 基础选牌 UI 复用 UGUI CardView

**问题**：BattleScene 需要先展示并选择卡牌，但当前切片尚未实现卡牌数据、ViewModel 和出牌链路。

**选择**：在现有 UGUI Canvas 下直接实例化 `CardView.prefab`，由场景级 `Toggle` + `ToggleGroup` 提供单选表现；选择结果暂时只存在于 UGUI 控件状态，不建立运行时业务模型。

**理由**：`CardView` 已经是 UGUI 预制体，复用它可以用最小场景改动先验证布局和交互；同时避免把 UI 构造任务扩成数据与战斗逻辑实现。现有 Canvas 继续使用 Screen Space - Camera，并把平面距离设为 1，使 UI 位于背景 Sprite 之前。

**影响**：`TinySpire/Assets/Scenes/BattleScene.unity`。后续接入动态手牌时，需要由运行时层生成卡牌并把 UI 选择同步到明确的 ViewModel/命令入口。

## 决策模板

```markdown
## CD-XXX：决策标题

**问题**：一句话描述

**选项**
- A: ...
- B: ...

**选择**：X

**理由**：为什么选 X。

**影响**：哪些模块/文件受影响。
```
