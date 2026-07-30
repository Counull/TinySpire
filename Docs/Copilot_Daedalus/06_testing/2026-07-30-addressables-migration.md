---
title: Addressables 迁移 · 验证记录
page_type: testing
lifecycle: active
date: 2026-07-30
source: ../plans/2026-07-30-addressables-migration.md
status_source: ../SESSION_LOG.md
---

# Addressables 迁移 · 验证记录

## 已验证

- `GameStartupOptionsTests`：迁移早期通过 3/3，确认场景名转换为完整 `.unity` Addressables 地址。
- Addressables Settings 已建立本地 `TinySpire Scenes`、`TinySpire GameData` 组；场景和 7 个 GameData JSON 使用完整 `Assets/...` 地址。
- Unity 已生成新增脚本与 Addressables/Localization 资产的 `.meta`。
- `dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal`：0 error、12 个既有程序集版本冲突 warning。
- Unity EditMode：23/23 通过，其中 `GameStartupOptionsTests` 3/3 通过。
- 源码、manifest/lock 和当前构建规则不再引用 YooAsset；`PackageManagerSettings` 中旧 OpenUPM registry 已移除。
- `Addressables.InitializeAsync` 显式使用 `autoReleaseHandle: false`，等待 Task 后由服务释放一次，避免默认自动释放与手动释放重叠。
- `TinySpire/Addressables/Build Local Content` 成功，输出 `Library/com.unity.addressables/aa/Windows/settings.json`；构建耗时约 5.4 秒。
- 构建工具显式绑定默认 Remote Catalog profile 引用，同时保持 `BuildRemoteCatalog = false`，构建报告阶段不再出现空 profile ID 错误。
- Bootstrap → LoadingScene → BattleScene 实跑成功；`game-config.json` 与 7 个 GameData JSON 正常加载，Console 无 InvalidKey、资源地址无效、handle 重复释放或其他 error/warning。

## 未实施

- 远程 catalog、CDN、资源更新检查。
- 第二套资源包。
- HybridCLR/AOT 边界调整。
