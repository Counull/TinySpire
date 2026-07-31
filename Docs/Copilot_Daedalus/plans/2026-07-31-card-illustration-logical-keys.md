---
title: 牌面短键与 Addressables 逻辑地址迁移
page_type: plan
lifecycle: completed
updated: 2026-07-31
status_source: ../SESSION_LOG.md
---

# 牌面短键与 Addressables 逻辑地址迁移

## 目标

让策划在 `battle.card.xlsx` 中只填写不带目录和扩展名的牌面短键；文件移动不再迫使配置表改写 Unity 绝对资源路径，同时继续使用 Addressables 本地 AssetBundle 加载。

## 实现

1. 将字段从 `illustration_address` 迁移为 `illustration_key`，当前值为 `card_art_strength`、`card_art_strike`、`card_art_defend`、`card_art_bash`。
2. 将动态牌面集中到 `Assets/Arts/Runtime/Card/Illustrations/`，移动时保留原 `.meta` 与 GUID；卡背、Prefab、框体和纹理目录不迁移。
3. 运行时统一由 `CardIllustrationAddress.FromKey` 生成 `card-art/{key}`，配置层不保存目录、扩展名或 Addressables 地址。
4. `AddressablesBuildTools` 递归索引专用目录，以不区分大小写的文件名短键检查重名，并在构建期检查表引用存在、大小写与文件名完全一致且资源为 `Sprite / Single / no mipmap`。
5. `TinySpire Card Art` 只收录牌表实际引用的图片，条目地址使用 `card-art/*`；继续采用本地 `PackTogether` AssetBundle。

## 影响边界

- 修改卡牌表字段、Luban 生成代码/JSON、牌面目录、Card Art 组、构建工具和手牌加载地址生成。
- 不修改图片像素、导入参数、卡背/Prefab/场景、卡牌运行时状态或战斗逻辑。
- 角色 Prefab 等其他配置仍使用已有完整 `Assets/...` 地址，本次不做通用资源键系统重构。

## 回滚

恢复 `illustration_address` 字段、四张图片原目录、Card Art 完整路径地址及原运行时读取代码，再运行 Luban 与 `TinySpire/Addressables/Build Local Content`。

## 验收

- 工作簿除 H1、H4、H5:H8 外语义和样式不变。
- Luban 生成 `Card.IllustrationKey` 与 `illustration_key` JSON。
- 四张图 GUID 保持不变，Card Art 组仅有四个 `card-art/*` 地址。
- 逻辑地址可通过运行时 Addressables API 加载 Sprite；编译、EditMode 与本地内容构建通过。
