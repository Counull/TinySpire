---
title: STS2 战士卡牌缺图清单
page_type: communication
lifecycle: active
date: 2026-08-06
scope: 冻结快照中仍使用占位图的战士卡牌插画交付
source: TinySpire/Assets/GameData/battle_tbcard.json、DataTables/Datas/i18n.xlsx
status_source: ../SESSION_LOG.md
---

# STS2 战士卡牌缺图清单

> 本清单列出冻结快照 `sts2-v0.107.1-23811903-59260271` 中仍使用占位图的 82 张卡牌，不以可玩状态作为移出条件。I4 后 Tremble 已为 `Implemented`，但仍缺正式原创牌面，因此继续保留。当前未生成、未下载、未引用任何官方美术；缺图卡统一使用项目已有占位图 `art_placeholder`。

## 1. 范围与现状

- 生成后的 `battle_tbcard.json` 共 86 张卡，其中 82 张使用 `art_placeholder`：Tremble 已为 `Implemented`，其余 81 张为 `CatalogOnly`。
- `Strike`、`Defend`、`Bash` 已有项目内插画，分别使用 `card_art_strike`、`card_art_defend`、`card_art_bash`，不列入缺图。
- TinySpire 自有卡 `Strength` 使用 `card_art_strength`，不属于 STS2 冻结快照，也不列入缺图。
- 下表的建议短键统一由 `external_key` 转为小写并添加 `card_art_` 前缀；文件名与短键大小写精确一致，仅增加 `.png` 扩展名。
- 本清单是原创替代素材的交付清单，不是官方素材下载或复刻清单。

## 2. 缺图总表（82 张）

| # | `external_key` | English | 中文 | 建议最终 `illustration_key` | 建议文件名 | 当前占位 |
|---:|---|---|---|---|---|---|
| 1 | `ANGER` | Anger | 愤怒 | `card_art_anger` | `card_art_anger.png` | `art_placeholder` |
| 2 | `ARMAMENTS` | Armaments | 武装 | `card_art_armaments` | `card_art_armaments.png` | `art_placeholder` |
| 3 | `BLOODLETTING` | Bloodletting | 放血 | `card_art_bloodletting` | `card_art_bloodletting.png` | `art_placeholder` |
| 4 | `BLOOD_WALL` | Blood Wall | 血墙 | `card_art_blood_wall` | `card_art_blood_wall.png` | `art_placeholder` |
| 5 | `BODY_SLAM` | Body Slam | 全身撞击 | `card_art_body_slam` | `card_art_body_slam.png` | `art_placeholder` |
| 6 | `BREAKTHROUGH` | Breakthrough | 突破 | `card_art_breakthrough` | `card_art_breakthrough.png` | `art_placeholder` |
| 7 | `CINDER` | Cinder | 余烬 | `card_art_cinder` | `card_art_cinder.png` | `art_placeholder` |
| 8 | `HAVOC` | Havoc | 破灭 | `card_art_havoc` | `card_art_havoc.png` | `art_placeholder` |
| 9 | `HEADBUTT` | Headbutt | 头槌 | `card_art_headbutt` | `card_art_headbutt.png` | `art_placeholder` |
| 10 | `IRON_WAVE` | Iron Wave | 铁斩波 | `card_art_iron_wave` | `card_art_iron_wave.png` | `art_placeholder` |
| 11 | `MOLTEN_FIST` | Molten Fist | 熔融之拳 | `card_art_molten_fist` | `card_art_molten_fist.png` | `art_placeholder` |
| 12 | `PERFECTED_STRIKE` | Perfected Strike | 完美打击 | `card_art_perfected_strike` | `card_art_perfected_strike.png` | `art_placeholder` |
| 13 | `POMMEL_STRIKE` | Pommel Strike | 剑柄打击 | `card_art_pommel_strike` | `card_art_pommel_strike.png` | `art_placeholder` |
| 14 | `SETUP_STRIKE` | Setup Strike | 预备打击 | `card_art_setup_strike` | `card_art_setup_strike.png` | `art_placeholder` |
| 15 | `SHRUG_IT_OFF` | Shrug It Off | 耸肩无视 | `card_art_shrug_it_off` | `card_art_shrug_it_off.png` | `art_placeholder` |
| 16 | `SWORD_BOOMERANG` | Sword Boomerang | 飞剑回旋镖 | `card_art_sword_boomerang` | `card_art_sword_boomerang.png` | `art_placeholder` |
| 17 | `THUNDERCLAP` | Thunderclap | 闪电霹雳 | `card_art_thunderclap` | `card_art_thunderclap.png` | `art_placeholder` |
| 18 | `TREMBLE` | Tremble | 战栗 | `card_art_tremble` | `card_art_tremble.png` | `art_placeholder` |
| 19 | `TRUE_GRIT` | True Grit | 坚毅 | `card_art_true_grit` | `card_art_true_grit.png` | `art_placeholder` |
| 20 | `TWIN_STRIKE` | Twin Strike | 双重打击 | `card_art_twin_strike` | `card_art_twin_strike.png` | `art_placeholder` |
| 21 | `ASHEN_STRIKE` | Ashen Strike | 灰烬打击 | `card_art_ashen_strike` | `card_art_ashen_strike.png` | `art_placeholder` |
| 22 | `BATTLE_TRANCE` | Battle Trance | 战斗专注 | `card_art_battle_trance` | `card_art_battle_trance.png` | `art_placeholder` |
| 23 | `BLUDGEON` | Bludgeon | 重锤 | `card_art_bludgeon` | `card_art_bludgeon.png` | `art_placeholder` |
| 24 | `BULLY` | Bully | 欺凌 | `card_art_bully` | `card_art_bully.png` | `art_placeholder` |
| 25 | `BURNING_PACT` | Burning Pact | 燃烧契约 | `card_art_burning_pact` | `card_art_burning_pact.png` | `art_placeholder` |
| 26 | `COLOSSUS` | Colossus | 巨像 | `card_art_colossus` | `card_art_colossus.png` | `art_placeholder` |
| 27 | `DISMANTLE` | Dismantle | 拆卸 | `card_art_dismantle` | `card_art_dismantle.png` | `art_placeholder` |
| 28 | `DOMINATE` | Dominate | 主宰 | `card_art_dominate` | `card_art_dominate.png` | `art_placeholder` |
| 29 | `DRUM_OF_BATTLE` | Drum of Battle | 战鼓 | `card_art_drum_of_battle` | `card_art_drum_of_battle.png` | `art_placeholder` |
| 30 | `EVIL_EYE` | Evil Eye | 邪眼 | `card_art_evil_eye` | `card_art_evil_eye.png` | `art_placeholder` |
| 31 | `EXPECT_A_FIGHT` | Expect a Fight | 跃跃欲试 | `card_art_expect_a_fight` | `card_art_expect_a_fight.png` | `art_placeholder` |
| 32 | `FEEL_NO_PAIN` | Feel No Pain | 无惧疼痛 | `card_art_feel_no_pain` | `card_art_feel_no_pain.png` | `art_placeholder` |
| 33 | `FIGHT_ME` | Fight Me! | 与我一战！ | `card_art_fight_me` | `card_art_fight_me.png` | `art_placeholder` |
| 34 | `FLAME_BARRIER` | Flame Barrier | 火焰屏障 | `card_art_flame_barrier` | `card_art_flame_barrier.png` | `art_placeholder` |
| 35 | `FORGOTTEN_RITUAL` | Forgotten Ritual | 被遗忘的仪式 | `card_art_forgotten_ritual` | `card_art_forgotten_ritual.png` | `art_placeholder` |
| 36 | `HEMOKINESIS` | Hemokinesis | 御血术 | `card_art_hemokinesis` | `card_art_hemokinesis.png` | `art_placeholder` |
| 37 | `HOWL_FROM_BEYOND` | Howl from Beyond | 彼岸咆哮 | `card_art_howl_from_beyond` | `card_art_howl_from_beyond.png` | `art_placeholder` |
| 38 | `INFERNAL_BLADE` | Infernal Blade | 地狱之刃 | `card_art_infernal_blade` | `card_art_infernal_blade.png` | `art_placeholder` |
| 39 | `INFERNO` | Inferno | 狱火 | `card_art_inferno` | `card_art_inferno.png` | `art_placeholder` |
| 40 | `INFLAME` | Inflame | 燃烧 | `card_art_inflame` | `card_art_inflame.png` | `art_placeholder` |
| 41 | `JUGGLING` | Juggling | 杂耍 | `card_art_juggling` | `card_art_juggling.png` | `art_placeholder` |
| 42 | `PILLAGE` | Pillage | 劫掠 | `card_art_pillage` | `card_art_pillage.png` | `art_placeholder` |
| 43 | `RAGE` | Rage | 狂怒 | `card_art_rage` | `card_art_rage.png` | `art_placeholder` |
| 44 | `RAMPAGE` | Rampage | 暴走 | `card_art_rampage` | `card_art_rampage.png` | `art_placeholder` |
| 45 | `RUPTURE` | Rupture | 撕裂 | `card_art_rupture` | `card_art_rupture.png` | `art_placeholder` |
| 46 | `SECOND_WIND` | Second Wind | 重振精神 | `card_art_second_wind` | `card_art_second_wind.png` | `art_placeholder` |
| 47 | `SPITE` | Spite | 怨恨 | `card_art_spite` | `card_art_spite.png` | `art_placeholder` |
| 48 | `STAMPEDE` | Stampede | 惊逃 | `card_art_stampede` | `card_art_stampede.png` | `art_placeholder` |
| 49 | `STOMP` | Stomp | 踩踏 | `card_art_stomp` | `card_art_stomp.png` | `art_placeholder` |
| 50 | `STONE_ARMOR` | Stone Armor | 岩石铠甲 | `card_art_stone_armor` | `card_art_stone_armor.png` | `art_placeholder` |
| 51 | `TAUNT` | Taunt | 挑衅 | `card_art_taunt` | `card_art_taunt.png` | `art_placeholder` |
| 52 | `UNRELENTING` | Unrelenting | 无情猛攻 | `card_art_unrelenting` | `card_art_unrelenting.png` | `art_placeholder` |
| 53 | `UPPERCUT` | Uppercut | 上勾拳 | `card_art_uppercut` | `card_art_uppercut.png` | `art_placeholder` |
| 54 | `VICIOUS` | Vicious | 凶恶 | `card_art_vicious` | `card_art_vicious.png` | `art_placeholder` |
| 55 | `WHIRLWIND` | Whirlwind | 旋风斩 | `card_art_whirlwind` | `card_art_whirlwind.png` | `art_placeholder` |
| 56 | `AGGRESSION` | Aggression | 好勇斗狠 | `card_art_aggression` | `card_art_aggression.png` | `art_placeholder` |
| 57 | `BARRICADE` | Barricade | 壁垒 | `card_art_barricade` | `card_art_barricade.png` | `art_placeholder` |
| 58 | `BRAND` | Brand | 烙印 | `card_art_brand` | `card_art_brand.png` | `art_placeholder` |
| 59 | `CASCADE` | Cascade | 倾泻 | `card_art_cascade` | `card_art_cascade.png` | `art_placeholder` |
| 60 | `CONFLAGRATION` | Conflagration | 焚烧 | `card_art_conflagration` | `card_art_conflagration.png` | `art_placeholder` |
| 61 | `CRIMSON_MANTLE` | Crimson Mantle | 绯红披风 | `card_art_crimson_mantle` | `card_art_crimson_mantle.png` | `art_placeholder` |
| 62 | `CRUELTY` | Cruelty | 残酷 | `card_art_cruelty` | `card_art_cruelty.png` | `art_placeholder` |
| 63 | `DARK_EMBRACE` | Dark Embrace | 黑暗之拥 | `card_art_dark_embrace` | `card_art_dark_embrace.png` | `art_placeholder` |
| 64 | `DEMON_FORM` | Demon Form | 恶魔形态 | `card_art_demon_form` | `card_art_demon_form.png` | `art_placeholder` |
| 65 | `FEED` | Feed | 狂宴 | `card_art_feed` | `card_art_feed.png` | `art_placeholder` |
| 66 | `FIEND_FIRE` | Fiend Fire | 恶魔之焰 | `card_art_fiend_fire` | `card_art_fiend_fire.png` | `art_placeholder` |
| 67 | `HELLRAISER` | Hellraiser | 地狱狂徒 | `card_art_hellraiser` | `card_art_hellraiser.png` | `art_placeholder` |
| 68 | `IMPERVIOUS` | Impervious | 岿然不动 | `card_art_impervious` | `card_art_impervious.png` | `art_placeholder` |
| 69 | `JUGGERNAUT` | Juggernaut | 势不可当 | `card_art_juggernaut` | `card_art_juggernaut.png` | `art_placeholder` |
| 70 | `MANGLE` | Mangle | 凌虐 | `card_art_mangle` | `card_art_mangle.png` | `art_placeholder` |
| 71 | `NOT_YET` | Not Yet | 时候未到 | `card_art_not_yet` | `card_art_not_yet.png` | `art_placeholder` |
| 72 | `OFFERING` | Offering | 祭品 | `card_art_offering` | `card_art_offering.png` | `art_placeholder` |
| 73 | `ONE_TWO_PUNCH` | One-Two Punch | 连环拳 | `card_art_one_two_punch` | `card_art_one_two_punch.png` | `art_placeholder` |
| 74 | `PACTS_END` | Pact's End | 契约终结 | `card_art_pacts_end` | `card_art_pacts_end.png` | `art_placeholder` |
| 75 | `PRIMAL_FORCE` | Primal Force | 原始力量 | `card_art_primal_force` | `card_art_primal_force.png` | `art_placeholder` |
| 76 | `PYRE` | Pyre | 薪火之源 | `card_art_pyre` | `card_art_pyre.png` | `art_placeholder` |
| 77 | `STOKE` | Stoke | 添柴 | `card_art_stoke` | `card_art_stoke.png` | `art_placeholder` |
| 78 | `TEAR_ASUNDER` | Tear Asunder | 扯碎 | `card_art_tear_asunder` | `card_art_tear_asunder.png` | `art_placeholder` |
| 79 | `THRASH` | Thrash | 痛殴 | `card_art_thrash` | `card_art_thrash.png` | `art_placeholder` |
| 80 | `UNMOVABLE` | Unmovable | 坚定不移 | `card_art_unmovable` | `card_art_unmovable.png` | `art_placeholder` |
| 81 | `BREAK` | Break | 破击 | `card_art_break` | `card_art_break.png` | `art_placeholder` |
| 82 | `CORRUPTION` | Corruption | 腐化 | `card_art_corruption` | `card_art_corruption.png` | `art_placeholder` |

## 3. 美术交付与接入规范

1. 将最终原创 PNG 放入 `TinySpire/Assets/Arts/Runtime/Card/Illustrations/`；不要把官方图片、下载缓存或生成过程文件放进项目。
2. Unity 导入类型必须为 `Sprite (2D and UI)`，`Sprite Mode` 使用 `Single`，关闭 Mipmap；文件名大小写必须与上表完全一致。
3. `battle.card.xlsx` 的 `illustration_key` 只填写上表短键，不填写目录、扩展名或 `Assets/...` 路径。
4. 运行时逻辑地址固定为 `card-art/{illustration_key}`，并通过 Addressables/AssetBundle 加载；不得使用 `AssetDatabase`、`Resources.Load` 或文件系统路径绕过资源包。
5. 每批替换素材时同步修改配置表，并按项目规则运行 Luban 和 `TinySpire/Build/Sync and Build All`，随后确认 Card Illustrations Group 与实际引用集合精确一致。
6. 验收需确认本地 Addressables 内容已重建，目标地址使用 `AssetBundleProvider`，并在 `Use Existing Build`（Packed Play Mode）或 Player 中完成真实加载；Fast Mode 不能作为 AB 包加载证据。
