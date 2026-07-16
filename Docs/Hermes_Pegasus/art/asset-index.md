# TinySpire Art 素材索引

> 项目素材根目录：`Docs/Hermes_Pegasus/art/`
>
> 本文是目录事实索引，不替代具体设计文档。文件新增、移动或重命名后，应同步更新本文。

## 目录职责

| 目录 | 用途 | 当前内容 |
|---|---|---:|
| `assets/agent-mythic-characters/turnaround-sheets/` | 角色身份锚点、三视图/转面表、服装变体参考 | 12 张 PNG |
| `assets/art-style/` | 风格锚点、角色概念图、场景概念、封面与 Loading Screen | 26 张 PNG |
| `assets/game-icons/` | 游戏内图标和应用图标参考 | 2 张 PNG |
| `assets/cards/` | 卡牌成品及拆分素材；包含 SVG 与 PNG | 5 个文件 |
| `art-style.md` | 艺术方向与风格规则说明 | 文档 |

## 1. 角色转面表 / 三视图

目录：`assets/agent-mythic-characters/turnaround-sheets/`

这些文件优先用于：

- 锁定角色身份、脸型、发型、体型和服装结构；
- 图像生成时作为角色一致性参考；
- 生成角色卡牌、图标、立绘前的身份锚点。

文件：

- `tinyspire-towel-turnaround-solid-offwhite-v01.png` — Towel 角色转面表
- `calliope-paper-playwright-light-heels-quill-turnaround.png` — Calliope 纸张剧作家 / 高跟鞋 / 羽毛笔版本
- `calliope-creative-dramaturge_turnaround_clean.png` — Calliope 创意剧作家，clean 版本
- `calliope-creative-dramaturge_turnaround.png` — Calliope 创意剧作家版本
- `calliope-creative-dramaturge_summer-costume_turnaround.png` — Calliope 夏季服装版本
- `calliope-creative-dramaturge_light-fresh_costume_turnaround.png` — Calliope light-fresh 服装版本
- `calliope-creative-dramaturge_alt-costume_turnaround_hands-fixed.png` — Calliope alternate costume，手部修正版
- `calliope-creative-dramaturge_alt-costume_turnaround.png` — Calliope alternate costume
- `sisyphus-tower-witness_turnaround.png` — Sisyphus Tower Witness 转面表
- `sisyphus-low-collar-registrar_turnaround.png` — Sisyphus Low Collar Registrar 转面表
- `pegasus_turnaround.png` — Pegasus 转面表
- `daedalus-artificer_turnaround.png` — Daedalus Artificer 转面表

### 使用优先级

对于 **Calliope**：

1. 先使用与当前服装/角色版本最匹配的 turnaround 文件；
2. 若只要求保持身份，不指定服装，优先使用 `calliope-creative-dramaturge_turnaround_clean.png`；
3. 若要求纸张剧作家、羽毛笔或高跟鞋，使用 `calliope-paper-playwright-light-heels-quill-turnaround.png`；
4. `alt-costume`、`summer-costume`、`light-fresh` 是服装变体，不应误当成新的角色身份。

## 2. 风格、概念与封面

目录：`assets/art-style/`

### 2.1 Calliope 封面与动作参考

子目录：`assets/art-style/covers/`

- `tinyspire-calliope-cover.png`
- `tinyspire-calliope-cover-pose-02.png`
- `tinyspire-calliope-cover-pose-03-restraint.png`
- `tinyspire-calliope-cover-pose-04-side-turn.png`
- `tinyspire-calliope-cover-paper-playwright-pencil-hand-fixed.png`
- `tinyspire-calliope-cover-with-towel-v01.png`
- `tinyspire-calliope-cover-with-towel-v02-ai.png`
- `tinyspire-calliope-cover-with-towel-v03-ai-cat-clear.png`
- `tinyspire-calliope-cover-with-towel-v04-artistic-heels.png`

用途：构图、姿势、Calliope 与 Towel 的互动、纸张剧作家风格参考。

### 2.2 角色与敌人概念

- `tinyspire-tower-executor-character-concept.png`
- `tinyspire-pontiff-monster-concept.png`
- `tinyspire-plague-doctor-executor-concept.png`
- `tinyspire-order-warden-enemy-concept.png`

### 2.3 场景与整体风格

- `tinyspire-constructivist-battle-bg-concept.png`
- `tinyspire-loading-screen-style-anchor.png`

### 2.4 Loading Screen

子目录：`assets/art-style/loading-screens/`

- `calliope_loading_screen.png`
- `pegasus_loading_screen.png`
- `sisyphus_low_collar_registrar_loading_screen.png`
- `sisyphus_tower_witness_loading_screen.png`

### 2.5 标题方案

子目录：`assets/art-style/title/`

- `tinyspire-title-transparent.png`
- `tinyspire-title-tower-no-circle-green.png`
- `tinyspire-title-stepped-spire-green.png`
- `tinyspire-title-spire-geometry-green.png`
- `tinyspire-title-ai-redesign-extracted-transparent.png`
- `tinyspire-title-ai-redesign-extracted-green.png`

## 3. 游戏图标

目录：`assets/game-icons/`

- `tinyspire-game-icon-spire-paper-constructivist-v01.png` — 游戏图标 / 尖塔纸张构成主义方案
- `tinyspire-towel-held-icon-v01.png` — Towel 被抱持状态图标

## 4. 卡牌素材

目录：`assets/cards/attack-card-001/`

### 4.1 拆分素材

子目录：`assets/cards/attack-card-001/split/`

- `illustration_composite.png`
- `card_frame_ui.svg`
- `safe_area_overlay.svg`

### 4.2 成品素材

子目录：`assets/cards/attack-card-001/final/`

- `attack_card_final.svg`
- `rarity_badges.svg`

## 5. 相关艺术方向文档

- `art-style.md` — TinySpire 艺术方向、稳定识别语法与可变实验层。

## 6. 维护规则

1. **角色身份锚点放 `agent-mythic-characters/turnaround-sheets/`**，不要只把封面图或姿势图当作三视图。
2. **风格参考放 `art-style/`**；其中 `covers/` 是封面/姿势/互动构图，`loading-screens/` 是加载画面，`title/` 是标题方案。
3. **游戏内图标放 `game-icons/`**；不要把图标参考混进角色转面表目录。
4. **卡牌的最终 UI 与拆分素材放 `cards/<card-id>/`**，不要散落在 `art-style/`。
5. 新增图片时文件名应包含：角色或用途、版本/变体、必要的修正版说明。
6. 本文只记录已存在的文件和基于文件名可确认的用途；未确认的设计决策不得写成事实。

## 更新时间

2026-07-14：首次建立索引，按当前 `art/` 目录实际文件整理。
