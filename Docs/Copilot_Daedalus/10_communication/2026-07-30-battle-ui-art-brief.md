---
title: BattleScene UI 美术素材需求说明
page_type: communication
lifecycle: active
date: 2026-07-30
scope: BattleScene M3A-M3E、M4-M9 的界面美术准备
source: 当前 BattleScene 已实现 HUD 与后续路线图
status_source: ../SESSION_LOG.md
---

# BattleScene UI 美术素材需求说明

> 用途：本文件可以直接交给图像生成模型或美术协作者。它只描述**尚缺少的 UI 图片**及其用途；不要据此生成文字、数值或玩法数据。

## 1. 当前已具备的内容（不需要重做）

| 类别 | 已有资源 | 说明 |
|---|---|---|
| 玩家/敌人角色 | `t_char_sisyphus_trans`、`t_char_warden_trans` 等 | BattleScene 当前已有玩家和敌人世界 Sprite。 |
| 战斗背景 | `t_bg.png` | 当前 BattleScene 背景。 |
| 卡牌框体 | `card_base.png`、`card_frame_ui.svg`、`cost_badge.png` 等 | 可继续用于手牌；当前卡图仍是占位图。 |
| 参与者 HUD 与牌堆 HUD | Unity UGUI Text / Image 组件 | 当前可显示生命、力量、抽牌/弃牌/消耗牌堆数字，但大部分还是纯色或纯文字。 |

## 2. 统一交付规则

- 输出 PNG，**透明背景**，不要拼到一张大图或带 UI 截图背景。
- 除非项目表明确要求，图片中**不要包含文字、数字、语言字符或价格**；游戏会用 i18n 和运行时事实显示文字/层数/数值。
- 图标保持正方形，主体距边缘至少留 10% 安全边距；用于九宫格拉伸的面板/条形图要留下干净的四角与边框。
- 风格目标：原创的暗黑希腊神话/地牢卡牌战斗 UI；质感可使用暗金属、旧羊皮纸、磨损青铜、火焰与灰烬。可以有 STS 式“信息清晰、层级强”的感觉，但**不要复刻任何现成游戏的具体图标、构图或商标**。
- 颜色语义保持稳定：生命=深红，格挡=冷蓝，力量=暖橙/金，能量=亮绿或蓝绿，消耗=灰白余烬，危险/减益=紫红。
- 建议导出目录：`TinySpire/Assets/Arts/Runtime/UI/Battle/`；文件名严格使用下表，便于后续接入。
- 建议导入设置：UI 图片使用 Sprite (2D and UI)；不需要 Mipmap；带半透明边缘时保留 Alpha；像素风不是当前方向，不要做硬像素锯齿。

## 3. 缺失素材总表

### P0：现在即可接入，优先生成

| 文件名 | 建议尺寸 | 用途 | 设计重点 |
|---|---:|---|---|
| `ui_battle_pile_draw.png` | 256×256 | 抽牌堆计数左侧图标 | 一叠背面朝上的卡，边缘整齐，暗蓝/青铜。 |
| `ui_battle_pile_discard.png` | 256×256 | 弃牌堆计数图标 | 一叠略凌乱、正反交错的卡，暗紫/旧纸。 |
| `ui_battle_pile_exhaust.png` | 256×256 | 消耗牌堆计数图标 | 被烧蚀的卡片与少量灰烬，灰白/暗橙；不要画数字。 |
| `ui_battle_pile_counter_panel.png` | 384×160 | 三类牌堆图标与数字的共用底板 | 小型深色面板，四角与中心可九宫格拉伸，避免占据视觉中心。 |
| `ui_battle_health_frame.png` | 512×64 | 玩家/敌人生命条外框 | 深色金属或皮革边框，中央留透明可填充区域，适合九宫格。 |
| `ui_battle_health_fill.png` | 512×64 | 生命条填充层 | 无文字的深红能量/血液质感横条；左端清晰、右端可裁切。 |
| `ui_battle_icon_strength.png` | 128×128 | 参与者 HUD 的力量状态 | 握拳、战斧印记或暖金火焰；读图优先于细节。 |

### P1：下一阶段 M4 / M3C（能量与结束回合）所需

| 文件名 | 建议尺寸 | 用途 | 设计重点 |
|---|---:|---|---|
| `ui_battle_energy_orb_frame.png` | 256×256 | 能量球的固定外框 | 青铜或魔法金属外环；中心透明，方便用运行时填充表现剩余能量。 |
| `ui_battle_energy_orb_fill.png` | 256×256 | 能量球的填充层 | 亮绿色或蓝绿色魔法火焰/液体；不带数字、不要烘焙“3”。 |
| `ui_battle_end_turn_normal.png` | 512×180 | 正常状态的结束回合按钮 | 右箭头/沙漏/回合结束意象；保留中央空白，文本由 i18n 覆盖。 |
| `ui_battle_end_turn_hover.png` | 512×180 | 鼠标悬停按钮状态 | 与 normal 同构图，只提高亮度、边缘光或能量感。 |
| `ui_battle_end_turn_disabled.png` | 512×180 | 输入锁定时按钮状态 | 与 normal 同构图，降低饱和度和亮度；不要直接画“禁用”。 |

### P2：M5～M7（敌人意图、格挡和状态）准备素材

| 文件名 | 建议尺寸 | 用途 | 设计重点 |
|---|---:|---|---|
| `ui_battle_intent_attack.png` | 192×192 | 敌人“将造成伤害”意图 | 利爪、剑痕或下劈，暖红。 |
| `ui_battle_intent_defend.png` | 192×192 | 敌人“将获得格挡”意图 | 盾牌或岩石壁垒，冷蓝。 |
| `ui_battle_intent_buff.png` | 192×192 | 敌人“将强化自身”意图 | 上升符文/火焰，金橙。 |
| `ui_battle_intent_debuff.png` | 192×192 | 敌人“将施加减益”意图 | 破碎诅咒符文或毒雾，紫红。 |
| `ui_battle_intent_special.png` | 192×192 | 敌人特殊/复杂行为意图 | 问号式神秘符文；不要直接使用问号字符。 |
| `ui_battle_icon_block.png` | 128×128 | 当前格挡状态 | 厚实盾牌，冷蓝，轮廓清晰。 |
| `ui_battle_icon_vulnerable.png` | 128×128 | 易伤状态 | 裂开的护甲/受创印记，暖红或橙红。 |
| `ui_battle_icon_weak.png` | 128×128 | 虚弱状态 | 下坠武器/黯淡肌肉印记，灰紫。 |

### P3：M7～M9 的反馈与胜败表现（可后置）

| 文件名 | 建议尺寸 | 用途 | 设计重点 |
|---|---:|---|---|
| `ui_battle_damage_number_backplate.png` | 256×128 | 伤害/治疗/格挡飘字的可选底板 | 小而简洁，避免遮挡角色；数值由代码渲染。 |
| `ui_battle_turn_banner_player.png` | 1024×256 | “玩家回合”横幅背景 | 左右可延展，中间留文字区域。 |
| `ui_battle_turn_banner_enemy.png` | 1024×256 | “敌人回合”横幅背景 | 与玩家横幅同系列但颜色更危险。 |
| `ui_battle_overlay_victory.png` | 1536×768 | 胜利覆盖层背景/装饰 | 暖金、余烬、向上动势；中央透明区域供按钮/文案放置。 |
| `ui_battle_overlay_defeat.png` | 1536×768 | 失败覆盖层背景/装饰 | 冷暗、破碎、下沉感；中央透明区域供按钮/文案放置。 |

### P4：卡牌从占位图进入可玩的初始内容

| 文件名 | 建议尺寸 | 用途 | 设计重点 |
|---|---:|---|---|
| `card_art_strike.png` | 768×512 | 打击卡插画 | 西西弗斯近战挥击或岩石冲击；留出安全边缘。 |
| `card_art_defend.png` | 768×512 | 防御卡插画 | 格挡姿态、盾形魔法或岩石壁垒。 |
| `card_art_bash.png` | 768×512 | 重击卡插画 | 强力砸击、冲击波、破甲感。 |
| `card_art_strength.png` | 768×512 | 力量卡插画 | 肌力、火焰、战斗意志；不要画卡名。 |

## 4. 各类素材在游戏中怎么用

| 玩法/界面事实 | UI 显示方式 | 对应图片 | 不应画进图片的内容 |
|---|---|---|---|
| 抽牌堆数量 | 图标 + 运行时数字 | `ui_battle_pile_draw`、`ui_battle_pile_counter_panel` | “Draw Pile”、数量。 |
| 弃牌堆数量 | 图标 + 运行时数字 | `ui_battle_pile_discard`、`ui_battle_pile_counter_panel` | “Discard Pile”、数量。 |
| 消耗牌堆数量 | 图标 + 运行时数字 | `ui_battle_pile_exhaust`、`ui_battle_pile_counter_panel` | “Exhaust Pile”、数量。 |
| 生命 | 框体 + 可裁切的填充条 + 文本 | `ui_battle_health_frame`、`ui_battle_health_fill` | 当前/最大生命。 |
| 力量、易伤等状态 | 小图标 + 层数文本 | `ui_battle_icon_strength` 等 | 状态层数、语言名称。 |
| 能量 | 能量球 + 当前数值 | `ui_battle_energy_orb_frame`、`ui_battle_energy_orb_fill` | 初始值 `3` 或剩余数值。 |
| 敌人意图 | 图标 + 即将执行的数值 | `ui_battle_intent_*` | 具体伤害、行为名称。 |
| 胜败与回合提示 | 装饰背景 + i18n 文本/按钮 | `ui_battle_turn_banner_*`、`ui_battle_overlay_*` | 任意语言文字、按钮文字。 |

## 5. 可直接发送给图像生成模型的总提示词

```text
Create a cohesive original dark Greek-mythology deckbuilder battle UI asset pack.
Visual language: worn bronze, dark leather, aged parchment, subtle magical glow,
high readability at small size, painterly 2D game UI, not pixel art. Do not copy
any existing game's exact assets, composition, logo, typography, or icon design.

Deliver each requested item as an individual PNG with a fully transparent background.
No text, no numbers, no letters, no currency, no embedded UI screenshot, no borders
outside the requested asset. Preserve 10–15% transparent padding around icon subjects.
Use deep red for health, icy blue for block, warm gold/orange for strength, teal-green
for energy, ash-gray with ember-orange for exhausted cards, and purple-red for debuffs.
For panels and bars, keep corners and edges clean for Unity 9-slice scaling.
```

## 6. P0 单图提示词

把上面的“总提示词”作为前缀，再追加以下任意一条：

| 文件名 | 追加提示词 |
|---|---|
| `ui_battle_pile_draw.png` | `A compact stack of face-down battle cards, orderly layered card backs, bronze and deep teal accents, 256x256.` |
| `ui_battle_pile_discard.png` | `A compact messy stack of discarded battle cards, a few tilted cards and worn parchment edges, muted violet and bronze accents, 256x256.` |
| `ui_battle_pile_exhaust.png` | `A single battle card partially burned to ash with a few drifting embers, ash-gray paper and restrained ember-orange glow, 256x256.` |
| `ui_battle_pile_counter_panel.png` | `A small dark bronze-and-leather UI counter panel, quiet ornamentation, clean empty center and protected corners for 9-slice scaling, 384x160.` |
| `ui_battle_health_frame.png` | `A horizontal dark bronze health-bar frame, rugged but readable, transparent empty inner channel, clean corners for 9-slice scaling, 512x64.` |
| `ui_battle_health_fill.png` | `A horizontal deep crimson magical blood-energy texture intended as a cropable health-bar fill, no frame and no text, 512x64.` |
| `ui_battle_icon_strength.png` | `A bold warm-gold combat strength icon, stylized clenched fist surrounded by a restrained flame aura, clear silhouette, 128x128.` |

## 7. 交付顺序

1. 先交 P0 的七张图：它们能立即替换当前 BattleScene 的纯文字/纯色 HUD。
2. 再交 P1 的五张图：M4 开始实现能量和结束回合时直接接入。
3. P2、P3 按敌人行为、效果器和胜败流程的实现进度再做，避免提前生成不符合最终行为语义的图。
4. P4 可以与系统开发并行；它只替换卡牌插画占位，不改变卡牌数据或效果。

## 8. 收到图片后的接入约定

- 将每张 PNG 按文件名放入建议目录，不改名、不合并。
- 图片进入仓库后由实现侧配置 Sprite 导入、UI Prefab 引用与 Addressables 本地构建。
- 本轮不要求生成角色、背景、文字贴图或完整 UI 截图；这些不是当前缺口。
