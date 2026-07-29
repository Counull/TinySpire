---
title: TinySpire · BattleScene MVP 路线图（Daedalus 侧提案）
owner: Daedalus
page_type: roadmap
lifecycle: proposal
created: 2026-07-29
updated: 2026-07-29
status_source: SESSION_LOG.md
note: 本文件是实现侧的阶段化路线图提案，细化 ../Hermes_Pegasus/STATUS.md 的 P0 清单；不替代 STATUS.md 的项目状态权威。若要成为项目级正式状态源，需要 Pegasus/Theseus 确认后同步回 STATUS.md，Daedalus 不会静默改写 STATUS.md。
---

# TinySpire · BattleScene MVP 路线图

> 目标只有一个（来自 [`architecture.md`](../Hermes_Pegasus/architecture.md) 的"最小跑通目标"）：
> **选一张"力量+3"的牌 → 打出去 → buff 挂上 → 攻击力自动变 → UI 自动亮。**
>
> 下面把这条链路拆成阶段，每个阶段列：目标、依赖、涉及文件、当前状态。阶段顺序是当前建议，不是锁定决策——如果发现顺序不合理，随时可以在开始某阶段前重新讨论。

## 阶段总览

| 阶段 | 内容 | 状态 |
|---|---|---|
| Phase 0 | 手牌 UI 表现层（扇形/悬停/拖拽视觉） | ✅ 已实施 |
| Phase 1 | 拖拽打出最小判定 + 手牌数据归属权收回 | � 已实施，待人工手势验收 |
| Phase 2 | 卡牌数据最小结构（"力量+3"测试卡） | ⬜ 未开始 |
| Phase 3 | Effect 执行链（Card → Effect → 属性变化） | ⬜ 未开始 |
| Phase 4 | 属性状态 → UI 绑定 | ⬜ 未开始 |
| Phase 5 | 最小反馈（数字跳动/图标） | ⬜ 未开始 |
| Phase 6 | 怪物/玩家锚点 + 目标系统 + 攻击类卡牌 | ⬜ 未开始（依赖 DEP-001） |
| Phase 7 | 费用/能量系统 | ⬜ 未开始（依赖 DEP-002） |
| Phase 8 | 弃牌堆动画 + 美术样式收尾 | ⬜ 未开始（依赖 DEP-003、DEP-004） |

## Phase 0 · 手牌 UI 表现层 ✅

- **目标**：扇形排布、悬停抬起、拖拽跟手视觉，不含任何判定/数据。
- **涉及文件**：`TinySpire/Assets/Scripts/UI/Battle/Hand/`（`HandCardLayout`/`HandCardContainer`/`HandCardVisual`/`HandCardInteraction`）。
- **状态**：已由 Codex 实施并通过 Play Mode 验收，见 `06_testing/2026-07-29-battlescene-hand-ui-sts-style.md`。
- **相关决策**：CD-003（DOTween 引入）、CD-004（交互模型替换）。

## Phase 1 · 拖拽打出最小判定 + HandState �

- **目标**：拖过可调出牌线判定为"打出"；手牌数据归属权从 UI 收回到 `HandState` 纯 C# 聚合类。
- **依赖**：Phase 0。
- **涉及文件**：同 Phase 0 目录，新增 `HandState`。
- **状态**：已由 Codex 实施，`HandState` 行为、出牌线判定、视觉重建、越线反馈、4 处 `TODO(DEP-xxx)` 均通过静态验证；拖拽/回弹的最终鼠标手势仍需人工 Game View 验收（UnityMCP 无法注入指针事件）。见 `06_testing/2026-07-29-battlescene-drag-to-play-minimal.md`。
- **产生的依赖项**：DEP-001（目标检测方式）、DEP-002（费用检查）、DEP-003（视觉样式）、DEP-004（销毁过渡动作），见 [`DEPENDENCIES.md`](DEPENDENCIES.md)。
- **架构约定**：遵守 `ARCHITECTURE_CONVENTIONS.md` 的 AC-001（最小状态聚合）。

## Phase 2 · 卡牌数据最小结构

- **目标**：定义"力量+3"测试卡的最小数据形态（模板 + 实例两层，遵守 `decision-locks.md` L-006）；本轮仍可用占位数据，不必等 Luban 真正接入。
- **依赖**：Phase 1 的 `HandState` 完成，作为承载卡牌实例的容器。
- **涉及文件**：预计新增 `TinySpire/Assets/Scripts/Core/` 或新的卡牌数据目录（具体位置留待该阶段开始时确认）。
- **状态**：未开始，尚无计划文档。
- **需要确认的点**：卡牌数据用 ScriptableObject、JSON 还是纯 C# mock（`STATUS.md` Open Question 之一，尚未回答）。

## Phase 3 · Effect 执行链

- **目标**：跑通 `Card → Effect → 属性变化` 的最小链路，先只支持"改变一个数值属性"这一种效果类型（对应"力量+3"）。
- **依赖**：Phase 2。
- **涉及文件**：预计新增计算层的 Effect 执行类（不依赖 Unity API，可 NUnit 测试）。
- **状态**：未开始。

## Phase 4 · 属性状态 → UI 绑定

- **目标**：把"力量"这个属性值绑定到一个 UI 文本上，验证"打出后数字自动变"。
- **依赖**：Phase 3。
- **状态**：未开始。
- **架构约定**：绑定方式遵守 AC-001/AC-004——UI 只读该属性的聚合类快照，不自己持有权威数值。

## Phase 5 · 最小反馈

- **目标**：数字跳动或简单图标提示，验证"效果生效有可感知反馈"，不需要完整 VFX。
- **依赖**：Phase 4。
- **状态**：未开始。

> **到 Phase 5 为止，就是 `architecture.md` 定义的"最小跑通目标"闭环。之后的阶段是在闭环基础上扩展，不是闭环的必要条件。**

## Phase 6 · 怪物/玩家锚点 + 目标系统 + 攻击类卡牌

- **目标**：定义怪物/玩家在场景中的锚点方案（UGUI 还是 World Space Sprite），实现目标检测（解决 DEP-001），支持第一张攻击类卡牌。
- **依赖**：Phase 5 完成的闭环验证了自增益卡链路后，再扩展到需要目标的卡牌。
- **状态**：未开始。
- **需要确认的点**：`STATUS.md` P0 里的"实现一个敌人静态 PNG + hit shake"应归入本阶段。

## Phase 7 · 费用/能量系统

- **目标**：定义能量池数据结构，接入出牌费用检查（解决 DEP-002）。
- **依赖**：Phase 2（卡牌数据结构需要能承载费用字段）。
- **状态**：未开始。

## Phase 8 · 弃牌堆动画 + 美术样式收尾

- **目标**：解决 DEP-003（拖过线视觉样式）、DEP-004（按卡牌效果类型区分的销毁前动作），接入真正的弃牌堆。
- **依赖**：Phase 6（需要知道卡牌效果类型分类）、美术资源到位。
- **状态**：未开始。

## 与 `Hermes_Pegasus/STATUS.md` 的关系

`STATUS.md` 的 P0 清单是项目级权威状态源，本文件是它在实现侧的细化展开。两者对应关系：

| STATUS.md P0 项 | 对应本路线图阶段 |
|---|---|
| 定义 BattleScene 屏幕布局安全区 | Phase 0（手牌安全区已定），战场整体安全区待 Phase 6 |
| 定义卡牌数据最小结构 | Phase 2 |
| 定义 Effect 执行链 | Phase 3 |
| 定义 UI 绑定方式 | Phase 4 |
| 实现一张"力量+3"测试牌 | Phase 2~5 |
| 实现一个敌人静态 PNG + hit shake | Phase 6 |
| 实现基础 VFX | Phase 5、Phase 8 |

如果这份细化展开被认可为项目级事实，需要 Pegasus/Theseus 确认后手动同步回 `STATUS.md`；本文件本身不会被 Daedalus 拿去覆盖 `STATUS.md`。
