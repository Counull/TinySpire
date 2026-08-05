---
title: M10A 配置原子性与表清单 fail-fast
page_type: testing
lifecycle: active
date: 2026-08-05
status: passed
scope: ConfigService 的配置原子发布、typed failure、重试与 Luban/生成物/运行时表清单漂移校验
plan: ../plans/2026-08-05-m10-battlescene-conformance.md
status_source: ../SESSION_LOG.md
source: ConfigService、AddressableAssetService、TinySpireBuildTools、DataTables/Datas/__tables__.xlsx 与当前 Unity Editor
---

# M10A 配置原子性与表清单 fail-fast

## 当前结论

M10A 已通过。`ConfigService` 只在全部必需表与 `game-config.json` 成功读取、解析并通过最小结构校验后，才一次性发布 `Tables` 与 `GameConfig`。失败不会保留半成品，也不会静默用 `GameConfig` 的代码默认值继续。运行时手写表清单已由既有 `TinySpire/Build/Sync and Build All` 的构建期四源比较约束。

本页只记录 M10A 的 Core/Editor 自动证据；它不等同于 Bootstrap 的可见失败体验。M10B 尚未开始。

## 精确红灯与最小修复

| 顺序 | 红灯或可观察失败 | 最小修复 | 绿灯证据 |
|---|---|---|---|
| 1 | 新的 fake-loader 契约测试首次导入时报 `CS0246`：缺少 `IConfigTextLoader`。 | `AddressableAssetService` 实现内部窄 seam；`ConfigService` 增加 internal 加载入口。 | 任务 `695bf04117214032af17c180cfb4e76c`：缺失 game-config 的原子失败测试 **1/1 passed**。 |
| 2 | 表清单契约测试首次导入时报缺少验证器；随后真实校验发现错误读取 `__tables__.xlsx` 的 G 列 mode，Luban 定义集合为空。 | 新增 Editor 验证器，改读 H 列 group；在生成/刷新后、Local Content 前接入同步构建。 | 当前项目四源比较返回 `CONFIG_TABLE_MANIFEST_OK`。 |
| 3 | 重复运行时表名测试 `1ab22470426a442a949e869f6dd9b0c3` 失败：预期 drift exception，但没有异常。 | 清单标准化改为先拒绝重复，再比较差异。 | 任务 `babe50c8d6d54fce8e82179caf7da942`：验证器 **2/2 passed**。 |
| 4 | 坏表行测试首次导入时报 `CS0117`：没有 `InvalidTableRowShape` 分类。 | 在 JSON 根解析后、`Tables` 构造前验证每行必须是对象。 | 任务 `51bbb0900a0542cd8a8631ddd4ff3710`：`ConfigServiceTests` **7/7 passed**。 |

## 自动验证

| 检查 | 结果 | 覆盖边界 |
|---|---|---|
| `ConfigServiceTests` | **7/7 passed，0 failed，0 skipped**；任务 `51bbb0900a0542cd8a8631ddd4ff3710` | game-config 缺失、单表缺失、非对象表行、坏 JSON、错误根节点、缺字段、失败后重试成功；失败时 `Tables`/`GameConfig` 均不发布。 |
| `ConfigTableManifestValidatorTests` | **2/2 passed，0 failed，0 skipped**；任务 `babe50c8d6d54fce8e82179caf7da942` | 生成 JSON 缺表、运行时 `TableNames` 重复表名。 |
| 当前项目清单校验 | `CONFIG_TABLE_MANIFEST_OK` | Luban `__tables__.xlsx`、生成 `Tables.cs`、`Assets/GameData/battle_tb*.json`、运行时八项清单一致。 |
| Unity 编译/Console | C# 编译 Error **0**；无 M10A 配置失败 | 当前唯一 Unity 6000.5.5f1 Editor 已完成脚本导入。最终 Console 保留 Unity PerformanceTesting 的 IPrebuild/IPostBuild setup warning 与 TestResults 写入消息，未将它们冒充为 M10A 问题或零 warning；本记录不把未运行的 Bootstrap/Game View 当作证据。 |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings** | 12 条均为既有 Unity/R3/UniTask 依赖程序集版本冲突 warning。 |

## 范围与未做事项

- 修改仅在 `TinySpire/Assets/Scripts/Core/`、`TinySpire/Assets/Editor/` 的 M10A 配置/测试路径，以及本记录所列 Daedalus 文档。
- 未修改 `DataTables/Datas/`、`Assets/GameData/`、Localization、Addressables 配置、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、战斗规则或 `BattleCommandQueue`/`Turn`/settlement 契约；因此未运行 Luban、Localization 同步或 Local Content。
- 未验证 Bootstrap 成功/失败路径、Addressables 运行时加载、真实 Game View、默认数值/双语黄金基线、30/60/120 FPS 或生命周期；这些分别仍属于 M10B～M10D。
- Candidates、Targeting 与用户既有未关联工作区改动保持排除；没有暂存、提交或推送。

## M10A 停止点

M10A 的纯加载契约、表清单构建期校验、定向 EditMode 与 solution build 已完成。下一步只能进入 M10B 的最小 Bootstrap 失败路由和配置驱动黄金基线；若该工作需要修改 M10A 排除项之外的启动/DI、场景或战斗边界，必须先停止并请求确认。
