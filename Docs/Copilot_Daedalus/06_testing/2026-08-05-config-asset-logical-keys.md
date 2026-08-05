---
title: 配置素材短键与真实 AssetBundle 加载
page_type: testing
lifecycle: active
date: 2026-08-05
status_source: ../SESSION_LOG.md
decision_source: ../CODE_DECISIONS.md#cd-055配置表-unity-素材统一保存短键构建期解析为-addressables-逻辑地址
---

# 配置素材短键与真实 AssetBundle 加载

## 范围与结论

- 全量审计 `DataTables/Datas/*.xlsx` 后，除已完成迁移的 `battle.Card.illustration_key` 外，仅 `battle.Hero.view_prefab_address` 与 `battle.Enemy.view_prefab_address` 仍保存 Unity 完整素材路径。
- 两个字段已改为 `view_prefab_key`，作者值分别为 `pfb_char_player` 与 `pfb_char_enemy`；生成的 Hero/Enemy C# 和 JSON 同步为 `ViewPrefabKey` / `view_prefab_key`，不再发布角色 `Assets/...prefab`。
- `BattleParticipantPresenter` 通过 `CharacterViewAddress.FromKey` 生成 `character-view/{key}`，仍使用 `Addressables.InstantiateAsync` 创建并以 `Addressables.ReleaseInstance` 释放。没有增加 `Resources.Load`、运行时 `AssetDatabase`、文件系统或直接 `AssetBundle.Load*` 旁路。
- `AddressablesBuildTools` 从 Hero/Enemy 生成 JSON 收集实际引用，扫描角色 Prefab 专用目录，拒绝空/路径/扩展名、忽略大小写的重名、大小写漂移、缺失 Prefab 和缺少 `SpriteRenderer`；`TinySpire Characters` 只保留两个实际引用并暴露逻辑地址。

## 红灯与最小实现

1. 首个测试要求 `CharacterViewAddress` 只接受文件短键并生成 `character-view/` 地址；编译以五处 `CS0103` 精确失败，因为转换函数尚不存在。加入最小转换函数后该测试 1/1 通过。
2. 随后加入生成 JSON、Addressables Group、Presenter 请求地址及真实 Addressables 实例化测试，分别观察到：生成 JSON 仍含旧字段、Group 地址仍是完整 `Assets/...`、Presenter 仍把原值直接传给 Addressables、逻辑地址运行时出现 `InvalidKey`。
3. 只迁移 Hero/Enemy 字段、转换边界、角色组同步与测试夹具后，生成内容测试 1/1、角色 Group 测试 1/1、短键/漂移/Presenter 相关测试 6/6、Fast Mode Addressables 实例化测试 1/1 均通过。两次 Test Runner 初始化任务在启动任何测试前超时，不作为产品失败或通过证据；强制 domain reload 后同一具名实例化测试通过。
4. 独立只读复核发现构建期曾接受“只有禁用 `SpriteRenderer`”的 Prefab，而运行时只发现 active Renderer。新增测试先以 `CS0117` 精确失败（缺少统一契约 seam），最小实现把构建期与运行时统一为 active-only；具名任务 `992e67d8767d48b4afef0add11767c89` 为 1/1 通过。

## 表格、生成与内容构建

- 使用项目工作簿工具导入、编辑、检查公式错误并渲染 `battle.hero.xlsx` 与 `battle.enemy.xlsx`；两份工作簿错误扫描均为 0，修改前后渲染确认既有样式未改变。
- `DataTables/gen.bat` 成功，目标 JSON 位于 `TinySpire/Assets/GameData`。
- 当前唯一 Unity Editor 执行 `TinySpire/Build/Sync and Build All`；首次 Addressables 内容成功构建为 26.357 秒。active Renderer 契约收口后又经同一入口完成最终构建（16.286 秒），输出 `Library/com.unity.addressables/aa/Windows/settings.json`，并以 `TinySpire sync and local content build completed successfully.` 收口。
- 最新 BuildLayout 的 `TinySpire Characters` 使用 `PackTogether`、`AssetBundleProvider`、`IAssetBundleResource` 与 LZ4HC；物理包 `tinyspirecharacters_assets_all_fe57eb6d2a6d15ec7fff2582c771976b.bundle` 含两个显式资产，地址为 `character-view/pfb_char_player`、`character-view/pfb_char_enemy`。

## Packed Play Mode 实包证据

- 在同一个 Unity 6000.5.5f1 Editor 内临时从 Fast Mode 切到 `Use Existing Build`，并因项目关闭 Domain Reload 而显式请求 Addressables 重新初始化；没有启动第二个 Editor，也没有修改或保存 ProjectSettings。
- 运行时 provider 列表包含 `AssetBundleProvider` 与 `BundledAssetProvider`，不含 `AssetDatabaseProvider`。两个逻辑地址各解析为唯一 `BundledAssetProvider` location，其依赖由 `AssetBundleProvider` 提供；物理 bundle 存在，`IAssetBundleResource.GetAssetBundle()` 非空，且从同一 location 实例化出的对象包含 `SpriteRenderer`。
- 正常 Bootstrap 实际进入 BattleScene，Game View 显示玩家、两名敌人、HUD 与手牌；Packed Play Mode Console 为 0 Error / 0 Warning。
- 退出 Play 后恢复 Fast Mode、原 PlayerPrefs runtime data path 与 Addressables 重新初始化状态；`AddressableAssetSettings.asset` 切换前后 SHA-256 均为 `467CB55F90DAEB4ED18C76810736F5E48561A53BEDEDBE1CB37D6D3D83BA0143`，证明没有把临时 Play Mode 配置写入项目资产。

## 证据边界

- Fast Mode 的成功只证明逻辑地址/catalog 配置可被 Editor 解析，不能证明实际经过 AB；本页的 AB 结论只来自最新 BuildLayout、物理 bundle、Packed Play Mode provider/location/resource 与真实 BattleScene 共同证据。
- 场景和 GameData 继续以完整 `Assets/...` 作为 Addressables 基础设施稳定地址；它们不属于 DataTables 的 Unity 素材业务字段。
- 本次未修改 Queue、Turn、settlement、公式、战斗规则、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、DI 或启动架构，也未触碰受保护的 Candidates/Targeting/Hermes 路径。

## 最终回归

- 当前完整工作区的 Unity 全量 EditMode 任务 `e6c01375675b4aaabdefb289f802ca8b` 为 **459/459 passed、0 failed、0 skipped**（12.670 秒），覆盖所有被迁移的 Hero/Enemy 配置夹具和 active Renderer 构建契约。该数量同时包含另行保留、未纳入本次素材短键提交的 M9/M10 测试改动，因此属于交付工作区证据，不声明为本提交独立检出的测试总数；本次素材边界自身的具名定向任务与相关回归见上文红灯/绿灯记录。
- `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 **0 error、12 条既有程序集版本冲突 warning**。
- 全量工作簿复扫确认 `DataTables/Datas/*.xlsx` 中已无 `Assets/...` 单元格值；现有素材业务字段只剩 `illustration_key` 与 `view_prefab_key`。生成 Hero/Enemy JSON 不含 `view_prefab_address` 或角色 Prefab 完整路径。
