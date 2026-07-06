---
title: TinySpire · 美术风格与 AI 资源管线
created: 2026-07-06
updated: 2026-07-06
type: constraint
tags: [pattern, workflow]
sources: [20260706_153457, 20260706_174737]
confidence: medium
attribution: |
  来源分层：
  - 用户自述：【用户】TinySpire 是 Unity 杀戮尖塔-like/card-based roguelike demo；用户要求讨论美术风格，用 ComfyUI + Z-Image 生成美术资源，并固定成永久文档。
  - 用户自述：【用户】后续讨论中确认 demo 表现路线倾向“纸片角色 + Tween 位移/抖动 + 构成主义 VFX”，不急于上 Spine/Q 版。
  - 已有 wiki：03_projects/card-game/* 记录了项目原型、玩法决策和程序架构。
  - 模型框架化：【合成:Pegasus】将现有玩法/工程约束转译为美术风格约束与资源生产管线。
---

# TinySpire · 美术风格与 AI 资源管线

> 本页是 TinySpire 美术方向与 AI 资源生成的长期锚点。后续新会话讨论 TinySpire 美术/Comfy/Z-Image 资源时，先读本页。

## 项目上下文

- 项目代号：**TinySpire**
- 类型：**杀戮尖塔 like · Unity · 卡牌肉鸽 demo**
- 原始长期设想：多人联机 PVE，参考《横跨方尖碑》
- 当前 demo 优先级：先做 **BattleScene / 卡牌→效果→状态→UI** 垂直切片，不先做完整爬塔、商店、遗物、地图。
- 程序目标：VContainer / R3 / MVVM / UniTask，计算层→状态层→时序层分离。

## 美术必须服务的东西

TinySpire 的美术不是先追“漂亮大世界观”，而是先服务卡牌战斗 demo：

1. **卡牌可读性**：卡面插画不能抢走标题、费用、描述、状态图标。
2. **批量一致性**：几十张卡看起来必须像同一个游戏，不是 AI 拼贴合集。
3. **低实现负担**：Unity UI 负责文字、边框、数值、按钮；AI 图不负责写字。
4. **垂直切片优先**：第一批资源只覆盖战斗场景、敌人、卡牌插画、状态图标占位。

## 当前资源管线策略

### 模型分工

- **GPT Image**：用于风格探索、生成 1-2 张风格标杆图。
- **ComfyUI + Z-Image**：用于本地批量试错、固定 seed/workflow/参数，形成可重复资源管线。
- **SDXL 系**：作为保底生产模型；如果 Z-Image 显存/工作流/一致性踩坑，则退回 SDXL。

### 生成原则

- 不让 AI 生成文字。卡牌标题、描述、费用、数值全部由 Unity UI 渲染。
- 卡牌插画优先生成“无字画面”，再嵌入统一卡框。
- 背景图可以更完整；卡牌图必须更克制。
- 图标暂不作为 AI 生成重点，优先用简单几何/占位图，后续再统一。

## 初始资源清单（MVP）

第一批只需要：

1. 战斗背景 1 张
2. 玩家/角色占位 1 个
3. 敌人 1 个
4. 卡牌插画 3 张：攻击 / 防御 / 功能或 Buff
5. 状态图标占位：力量 / 护甲 / 易伤或破甲
6. 卡框/UI 框：先 Unity 绘制，不追 AI

## 美术风格决策

用户明确偏好：**苏式风格 / 构成主义 / 呼捷马思（Vkhutemas）**。但该方向不能粗暴做成“苏联题材换皮”，否则会变窄、变符号化，也不天然适配卡牌肉鸽。

当前判断：

> 构成主义更适合作为 TinySpire 的“视觉语法”，不适合作为完整题材。

也就是说：

- UI / 卡框 / 版式：可以吃构成主义，强调斜线、几何块、强对比、编号、章印感。
- 战斗场景 / 怪物 / 卡牌插画：不直接做苏联宣传画，而是保留奇幻/小剧场主体。
- 色彩：可用红、黑、米白、灰蓝作为界面主色，但插画要控制，避免全图变政治海报。
- 题材：避免“镰刀锤子/红军/苏联国徽”这种窄符号；抽象成“工业、秩序、塔、实验、集体行动”。

下一步核心问题：

> TinySpire 是把构成主义用在 UI 框架上，还是连角色/敌人/世界也一起被构成主义化？

这个决策会影响：

- 色彩：红黑米白几何系统 vs 更自由的奇幻插画色
- 卡牌插画：普通奇幻主体 + 构成主义卡框 vs 角色本身也几何/海报化
- UI：纸片/章印/编号/斜切构图 vs 标准奇幻羊皮纸/金属框
- 敌人：怪物生物化 vs 象征化、机械化、剧场化

## 2026-07-06 追加决策：纸片角色 + Tween + 构成主义 VFX

后续讨论修正了“为了 Spine 而半 Q 化”的方向：TinySpire demo 不必为了动画简化成 Q 版。更合适的是 **构成主义纸片剪影角色**，用极少 pose / 整张图位移 / 抖动 / 闪白表现角色反馈。

当前表现公式：

> **纸片角色 + Tween 位移/抖动 + 构成主义 VFX**

角色可以少动，VFX 要形成语言：

- 攻击：红黑斜线切割 / 几何碎片爆裂
- 防御：米白或蓝灰几何盾展开
- Buff：圆章盖印 / 编号感矩形块
- Debuff：黑墨点污染 / 印刷网点扩散
- 死亡：纸片或几何块解体淡出

第一版优先使用 Unity **Particle System + Sprite 粒子 + DOTween**，暂不把 VFX Graph 作为第一阶段必选项。

最小 VFX prefab 套件：

1. `VFX_CardPlay`
2. `VFX_AttackSlash`
3. `VFX_BlockGain`
4. `VFX_BuffStamp`
5. `VFX_DebuffInk`

## Prompt 固定模板（草案）

### 卡牌插画模板

```text
fantasy roguelike card illustration, [SUBJECT], centered composition, readable silhouette, no text, no letters, no UI, game asset, consistent style, [STYLE_TAGS], [COLOR_TAGS]
```

### 战斗背景模板

```text
fantasy roguelike battle background, small arena, layered depth, no characters, no text, readable foreground, card battler game background, [STYLE_TAGS], [COLOR_TAGS]
```

### 敌人模板

```text
fantasy roguelike enemy creature, full body, isolated, readable silhouette, front-facing three-quarter view, no text, no UI, game asset, [STYLE_TAGS], [COLOR_TAGS]
```

## 质量判断标准

每张生成图先按这三项判断：

1. **统一性**：是否像同一个游戏？
2. **可读性**：缩小到卡牌尺寸还能看懂主体吗？
3. **可生产性**：这个风格能不能批量生成 30 张而不崩？

速度参考：

- 1024x1024 单张 ≤ 15 秒：适合批量试风格
- 15-40 秒：可用，但别无限试
- > 60 秒：不适合作为 TinySpire 主生产管线

## 当前概念图资产

已保存第一轮概念图：

- `assets/art-style/tinyspire-constructivist-battle-bg-concept.png` — 构成主义奇幻战斗背景
- `assets/art-style/tinyspire-tower-executor-character-concept.png` — 玩家角色：塔内执行员 / 失控档案员
- `assets/art-style/tinyspire-order-warden-enemy-concept.png` — 敌人：秩序守卫 / Order Warden

这些图适合作为 **一个场景 / 一个 demo 垂直切片** 的美术锚点。

## 重要边界：Demo 风格 vs 独立游戏风格

当前“构成主义实验塔”方向很适合 TinySpire 的第一个 demo，因为它：

- 视觉识别度强；
- 和卡牌 UI / 版式天然贴合；
- 一个战斗场景能快速形成统一气质；
- 很适合用 AI 固定 prompt 批量探索。

但如果 TinySpire 未来扩展成完整独立游戏，这个方向可能**过窄**：

- 世界观容易被锁死在“塔 / 实验 / 档案 / 秩序机器”；
- 怪物类型容易重复；
- 场景扩展空间不如传统奇幻/多区域冒险；
- 构成主义强版式长期使用可能造成审美疲劳；
- 过度小众会提高玩家进入门槛。

因此当前策略：

> 把它定义为 **Demo 视觉主题 / 第一章主题**，不要定义为整个游戏宇宙的唯一方向。

更稳的长期扩展方式：

- TinySpire 的整体世界可以是“许多风格化塔层/档案/梦境/规则空间”；
- 构成主义实验塔只是第一层或第一章；
- 后续可以扩展其他视觉语法，例如：炼金手稿塔、荒诞剧场塔、废墟童话塔、星图机械塔；
- 卡牌 UI 保持统一系统，场景和敌人可分主题变化。

## 相关

- [[architecture]] — 程序架构
- [[baseline]] — 基础设定
- [[decisions]] — 玩法决策
- [[notes]] — 松散随笔
