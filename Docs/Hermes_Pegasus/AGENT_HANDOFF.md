# TinySpire · Local Agent Handoff

> 给本地 coding/design agent 读取的项目上下文入口。先读本文件，再按需读同目录其他文档。

## 项目定位

- 项目代号：TinySpire
- 引擎：Unity 6.5
- 类型：Slay the Spire-like / roguelike deckbuilder demo
- 当前目标：先做 BattleScene 垂直切片：**卡牌 → 效果 → 状态 → UI → 反馈**。
- 不优先做：完整地图、商店、遗物、完整构筑循环、联网完整实现。

## 已有 wiki 文档

同目录：

- `index.md` — 项目总览
- `design/project-definition.md` — 当前项目与游戏定义
- `design/decision-locks.md` — 决策锁定表：哪些是地基、哪些只是暂定/延后/开放
- `design/baseline.md` — 基础设定
- `design/decisions.md` — 玩法决策记录
- `architecture.md` — 程序架构
- `art-style.md` — 美术风格与 AI 资源管线

## 程序架构基线

当前架构方向：

```text
计算层（纯 C#）     → 伤害多少、buff 多久、谁死了
状态层（R3）       → 计算结果存在哪、谁关心
时序层（UniTask）  → 播什么动画、等多久、下一步是什么
```

技术目标：

- VContainer
- R3
- MVVM：CardView ↔ CardViewModel
- UniTask
- NUnit 测逻辑核

最小跑通目标：

> 选一张“力量+3”的牌 → 打出去 → buff 挂上 → 攻击力自动变 → UI 自动亮。

## 美术方向

当前 demo 视觉主题：

> **构成主义奇幻 / 东欧-沙俄-瘟疫医生-档案塔**

注意：这只是 **demo / 第一章主题**，不要锁死整个独立游戏世界观。

核心边界：

- 可用：构成主义版式、红黑米白、几何块、鸟嘴面具、欧洲/沙俄仪式感、档案塔、怪物像教皇/仪式生物。
- 避免：真实政治符号、镰刀锤子、旗帜、可读文字、直接苏联士兵、真实宗教符号十字架。

## 当前概念图资产

已保存到：

```text
assets/art-style/
```

文件：

- `tinyspire-constructivist-battle-bg-concept.png` — 构成主义奇幻战斗背景
- `tinyspire-tower-executor-character-concept.png` — 初版玩家角色概念
- `tinyspire-order-warden-enemy-concept.png` — 初版怪物概念

另外，本轮聊天生成过两张新方向图，尚未归档到 wiki assets：

- 玩家：鸟嘴面具 / 欧洲-沙俄风格档案执行员
- 怪物：教皇式 / 欧洲宗教-沙俄仪式感怪物

如需归档，请从 Hermes cache 中复制最新图片到 `assets/art-style/`，并更新 `art-style.md`。

## 表现策略

当前建议：

> **纸片角色 + Tween 位移/抖动 + 构成主义 VFX**

不急着上 Spine。

优先做：

- 静态 PNG 角色/敌人
- DOTween 假动画：idle 浮动、attack 前冲、hit 抖动闪白、death 淡出/解体
- VFX 粒子：红黑斜线、纸片碎片、盖章、墨点、几何盾

不优先做：

- Spine 完整骨骼动画
- Q 版化
- 复杂 mesh deform / IK / 换装

## UI / 分辨率策略

- 横屏优先
- 基准：1920x1080 / 16:9
- UI 使用 Canvas Scaler + Anchor
- 战斗核心内容放在 16:9 安全区
- 超宽屏显示更多背景，窄屏不裁关键内容
- 不做竖屏响应式

## 给 Agent 的工作原则

1. 不要扩大范围。只做当前切片需要的东西。
2. 先跑通状态链路，再追最终美术质量。
3. AI 图只当概念/占位/风格锚点，Unity UI 负责文字和数值。
4. 不要让 AI 生成卡牌文字。
5. 任何新决策写入 `design/decisions.md` 或 `art-style.md`。
6. 如果要改架构，先说明会影响哪些层：计算 / 状态 / 时序 / UI。
