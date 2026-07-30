---
title: YooAsset 到 Addressables 迁移
page_type: plan
lifecycle: active
date: 2026-07-30
scope: 启动、场景、GameData、Localization 与本地内容构建
source: 用户确认弃用 YooAsset 并迁移到 Addressables
status_source: ../SESSION_LOG.md
---

# YooAsset 到 Addressables 迁移

## 目标

移除 YooAsset 包和运行时依赖，用 Unity Addressables 2.9.1 承接启动初始化、场景切换、Luban JSON 与 Unity Localization 资源。第一阶段维持本地内容构建，不引入远程 catalog、更新服务器或热更新策略。

## 稳定地址

- `Assets/Scenes/LoadingScene.unity`
- `Assets/Scenes/BattleScene.unity`
- `Assets/GameData/<file>.json`

地址保留完整 `Assets/...` 路径，避免迁移同时改变现有配置定位语义。

## 模块边界

- `AddressableAssetService` 初始化 Addressables，并向上层返回已复制的文本；上层不持有 Addressables handle。
- `ConfigService` 预加载已知 Luban 表和 `game-config.json`，再构造 `cfg.Tables`。
- `SceneFlowService` 在内部加载场景并选择 `ReleaseSceneWhenSceneUnloaded`，不向调用方泄漏场景 handle。
- `AddressablesBuildTools` 维护本地场景组、GameData 组、稳定地址和本地内容构建入口。
- Unity Localization 自动维护自身 Addressables 组，和场景、GameData 使用同一个 Addressables Settings。

## 删除

- `YooAssetPackageService`
- `com.tuyoogame.yooasset` 包与只为其存在的 OpenUPM registry
- YooAsset 的扫描/收集设置资产
- 旧的生成目录 `Assets/StreamingAssets/yoo` 与仓库根 `Bundles`

历史验证文档保留为迁移前记录，不回写为当前流程。

## 构建流程

1. 表格变化后运行 Luban，输出到 `TinySpire/Assets/GameData`。
2. Localization 变化后执行 `TinySpire/Localization/Configure Battle Card Text`。
3. 执行 `TinySpire/Addressables/Build Local Content`。
4. 验证 Bootstrap → LoadingScene → BattleScene、GameData 加载与本地化表加载。

## 验收

- manifest/lock、运行时代码和当前构建流程不再依赖 YooAsset。
- Bootstrap 只在 Player 场景列表中保留启动场景；LoadingScene/BattleScene 由 Addressables 加载。
- 6 张 Luban 表及 `game-config.json` 可按稳定地址加载。
- 本地 Addressables 内容构建成功，启动 Console 无 InvalidKey/资源地址无效/handle 重复释放错误。
- 不新增远程更新、CDN、HybridCLR 边界调整或第二套资源包。
