---
title: STS2 v0.107.1 Ironclad 卡池快照与机制缺口
page_type: research
lifecycle: active
created: 2026-08-06
updated: 2026-08-06
status: verified-snapshot
status_source: ../SESSION_LOG.md
plan: ../plans/2026-08-06-sts2-v01071-ironclad-card-pool.md
---

# STS2 v0.107.1 Ironclad 卡池快照与机制缺口

## 结论

TinySpire 当前不能靠追加 Excel 行忠实运行整个 Ironclad 卡池。现有配置和执行器只覆盖固定费用、Self/单体 Enemy、Damage/Block/Strength/Vulnerable 与普通弃牌；空效果列表反而会被当成成功出牌。因此必须先建立 `CatalogOnly` 的运行时与构建期隔离，再录入全目录，并按机制逐批翻转为 `Implemented`。

本快照固定为 85 张单人卡。Spire Codex 同 build 的稳定接口还返回 2 张 `multiplayer_only`：`Demonic Shield`、`Tank`；它们依赖 `DEP-008`，不计入本轮 85 张，也不能用单玩家事实假实现。

## 版本与来源

| 项目 | 已核验值 |
|---|---|
| Steam channel | public/main；本机配置无 BetaKey |
| 游戏版本 | `v0.107.1` |
| Steam build | `23811903` |
| 游戏构建 commit | `59260271` |
| 内容更新时间 | 2026-07-09 21:12:26 +08:00 |
| 语言源 | English |
| 快照日期 | 2026-08-06 |

本机安装目录没有独立明文卡表；官方随附 XML 文档只证明运行时存在 `ModelDb.AllCards`、`CardPoolModel.AllCards` 等枚举线索，不含具体卡牌数据。本页目录使用 [Spire Codex 公共 API](https://spire-codex.com/developers) 的 stable 数据；其 changelog 将 build `23811903` 明确对应到 STS2 `v0.107.1`，与本机 manifest 交叉一致。该接口是社区项目，不称为 Mega Crit 官方卡表。

STS2 仍处于 Early Access，卡牌会继续新增和调整，见 [Mega Crit FAQ](https://www.megacrit.com/faq/) 与 [Early Access 发布说明](https://www.megacrit.com/news/2026-03-05-early-access-launch/)。后续同步必须新建版本差异，不得静默把 beta 或新 stable 数值覆盖本快照。

## 内容使用边界

- 仓库不提交官方卡图、游戏二进制、解包结果或完整英文规则文本镜像。
- 表中可以保存兼容所需的卡名、结构化数值和机制事实；en/zh-CN 说明使用项目自有表述。
- 牌面只接受 TinySpire 自有素材或明确占位素材，并继续走 `illustration_key → card-art/{key}` 的 Addressables/AssetBundle 链路。
- 对外文档应明确 TinySpire 与 Mega Crit 无隶属或官方关系。Mega Crit 的 [Content Policy](https://www.megacrit.com/content-policy/) 允许符合条件的 Mod，但没有明确授权独立公开仓库镜像完整数据库或官方图像。

## 85 张单人目录

| 稀有度 | 数量 | 卡牌 |
|---|---:|---|
| Basic | 3 | Bash、Defend、Strike |
| Common | 20 | Anger、Armaments、Bloodletting、Blood Wall、Body Slam、Breakthrough、Cinder、Havoc、Headbutt、Iron Wave、Molten Fist、Perfected Strike、Pommel Strike、Setup Strike、Shrug It Off、Sword Boomerang、Thunderclap、Tremble、True Grit、Twin Strike |
| Uncommon | 35 | Ashen Strike、Battle Trance、Bludgeon、Bully、Burning Pact、Colossus、Dismantle、Dominate、Drum of Battle、Evil Eye、Expect a Fight、Feel No Pain、Fight Me!、Flame Barrier、Forgotten Ritual、Hemokinesis、Howl from Beyond、Infernal Blade、Inferno、Inflame、Juggling、Pillage、Rage、Rampage、Rupture、Second Wind、Spite、Stampede、Stomp、Stone Armor、Taunt、Unrelenting、Uppercut、Vicious、Whirlwind |
| Rare | 25 | Aggression、Barricade、Brand、Cascade、Conflagration、Crimson Mantle、Cruelty、Dark Embrace、Demon Form、Feed、Fiend Fire、Hellraiser、Impervious、Juggernaut、Mangle、Not Yet、Offering、One-Two Punch、Pact's End、Primal Force、Pyre、Stoke、Tear Asunder、Thrash、Unmovable |
| Ancient | 2 | Break、Corruption |

类型计数为 Attack 37、Skill 29、Power 19。稳定外部 ID 使用 API 的大写 snake case；Defend / Strike 分别为 `DEFEND_IRONCLAD` / `STRIKE_IRONCLAD`，不得只按显示名称对账。

## 机制缺口矩阵

下列分组会重叠，计数不能相加。它们用于安排实现依赖，不是第二份运行时状态。

| 机制组 | 数量 | 代表卡 / 说明 |
|---|---:|---|
| 现有四效果与普通弃牌可忠实运行 | 5 | Bash、Defend、Strike、Twin Strike、Bludgeon；Twin Strike 仍需双记录回归 |
| Effect 独立 Source/Selected/All 目标 | 7 | Breakthrough、Iron Wave、Setup Strike、Dominate、Fight Me!、Hemokinesis、Taunt |
| 全体敌人 | 8 | Breakthrough、Thunderclap、Howl from Beyond、Inferno、Stomp、Whirlwind、Conflagration、Pact's End |
| 随机敌人 | 4 | Sword Boomerang、Stampede、Hellraiser、Juggernaut |
| 多段或动态次数 | 10 | Twin Strike、Sword Boomerang、Dismantle、Fight Me!、Spite、Whirlwind、Conflagration、Fiend Fire、Tear Asunder、Thrash |
| 自动出牌或重放 | 6 | Havoc、Howl from Beyond、Stampede、Hellraiser、Cascade、One-Two Punch |
| 抽牌及抽牌事件 | 10 | Pommel Strike、Shrug It Off、Battle Trance、Burning Pact、Drum of Battle、Pillage、Vicious、Dark Embrace、Hellraiser、Offering |
| 能量、X 费或费用覆写 | 12 | Bloodletting、Drum of Battle、Expect a Fight、Forgotten Ritual、Infernal Blade、Stomp、Unrelenting、Whirlwind、Cascade、Offering、Pyre、Corruption |
| 失血、治疗、Fatal、Max HP 或失血历史 | 13 | Bloodletting、Blood Wall、Breakthrough、Hemokinesis、Inferno、Rupture、Spite、Brand、Crimson Mantle、Feed、Not Yet、Offering、Tear Asunder |
| Exhaust 操作、触发或 Exhaust Pile 读取 | 26 | 需要卡牌归宿、卡区选择、计数与触发器；当前仅已有 Exhaust 区移动能力 |
| Power 生命周期或持续触发 | 19 | 从 Feel No Pain 到 Corruption；不能为每张 Power 增一份字段或专用 handler |
| 手牌/弃牌选择 UI | 5 | Armaments、Headbutt、True Grit+、Burning Pact、Brand |
| 随机选择 | 10 | Cinder、Sword Boomerang、True Grit、Infernal Blade、Stampede、Aggression、Hellraiser、Juggernaut、Stoke、Thrash |
| 复制、生成或变形 | 5 | Anger、Infernal Blade、Juggling、Primal Force、Stoke |
| 升级、实例或临时费用变异 | 9 | Armaments、Infernal Blade、Rampage、Stomp、Unrelenting、Aggression、Primal Force、Thrash、Corruption |
| 条件与动态数值 | 23 | Block、Vulnerable、牌名/类型、手牌/消耗区数量、回合/战斗历史等事实表达式 |
| 格挡生命周期或反击 | 5 | Colossus、Flame Barrier、Barricade、Juggernaut、Unmovable |
| 新状态或公式特例 | 11 | Weak、Plating、临时 Strength、Vulnerable 倍率、Heal/MaxHP/Fatal 等 |

## 实现含义

1. 先以 `CatalogOnly` 阻止假成功和误入 Deck。
2. 所有卡进入同一 `battle.Card` 目录，但“已录入”不等于“可玩”。
3. 每实现一个通用机制，至少选择一张真实快照卡经 Queue、事实、settlement、文本与 Game View 验收后翻转状态。
4. I4 起需要修改 Turn/settlement 边界；依据既有用户停止要求，I3 完成后必须先报告并确认。

