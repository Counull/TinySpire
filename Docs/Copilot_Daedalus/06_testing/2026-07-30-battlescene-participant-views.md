---
title: BattleScene M3A 参与者配置与 Prefab 工厂
page_type: testing
lifecycle: active
date: 2026-07-30
source: ../plans/2026-07-30-battlescene-participant-views.md
status_source: ../SESSION_LOG.md
---

# BattleScene M3A-1/4 参与者视图与生命 HUD 验证记录

## M3A-3/4 待人工验收

- 在现有 Unity Editor 执行 `TinySpire/Build/Sync and Build All`，使 BattleScene 与 HUD Prefab 的 Addressables 本地内容进入 catalog。
- 从 Bootstrap 进入 BattleScene：应出现一名玩家、一名敌人；每个角色上方为本地化名称、下方为红色生命条和 `当前 / 上限`。
- 初始力量为零时，HUD 不显示力量；用调试入口修改任一参与者生命时，只对应 HUD 数值和生命条刷新。
- 切换 `en` 与 `zh-CN`：名称和非零力量标签应刷新；Console 不得出现 `InvalidKey`、资源地址无效、Prefab 引用缺失或 VContainer 解析异常。
- 退出 BattleScene 后确认 Hierarchy 中没有残留 Addressables 角色实例或 `CombatantHud_*` 对象。

## HUD 前景与名称修正待回归

- Canvas 的 `Plane Distance` 应为 `1`；进入 BattleScene 后，HUD 必须显示在背景之前且保持原有世界投影位置。
- 英文 locale 下 Hero/Enemy HUD 名称应分别为 `Sisyphus`、`Warden`；`zh-CN` 下应分别为 `西西弗斯`、`典狱长`。

## 本轮已执行

- `DataTables/gen.bat` 已在 `DataTables/` 目录运行成功，生成代码与 `Assets/GameData` 已刷新。
- `dotnet build TinySpire/Assembly-CSharp.csproj --no-restore`：0 error，6 条既有程序集版本冲突警告。
- `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore`：0 error，12 条既有程序集版本冲突警告。
- 场景 Canvas、两张 Localization String Table 与 `i18n.xlsx` 的两条参与者名称均已核对一致；`git diff --check` 通过。

## 已执行

- Luban 生成完成：`battle_tbhero.json`、`battle_tbenemy.json` 与对应 C# bean 均包含 `name_i18n_key`、`view_prefab_address`；JSON 位于 `TinySpire/Assets/GameData/`。
- `dotnet build TinySpire/Assembly-CSharp.csproj --no-restore`：0 error，6 条既有程序集版本冲突警告；该项目已包含 `BattleParticipantPresenter` 与 `EnemyCombatantLayout`。
- `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore`：0 error，12 条既有程序集版本冲突警告；新增布局测试可编译。
- `git diff --check` 通过。
- 新增 EditMode 用例覆盖 Encounter 敌人顺序、两/三敌人从右到左等距布局、零或四名敌人以及非正间距的配置错误。
- BattleScene 实跑曾稳定复现 VContainer 尝试解析 `BattleSession` 的私有运行时聚合构造函数，并因未注册 `BattleCombatantsData` 失败。`BattleLifetimeScope` 已改为显式 `BattleSession` 工厂；修复后静态构建为 0 error，仍待现有 Unity Editor 实跑回归。

## 待在现有 Unity Editor 验收

- 运行 `TinySpire/Build/Sync and Build All`：重新生成 Luban 数据、从 `i18n.xlsx` 导入 Hero/Enemy 名称、校验本地化，并重建本地 Addressables 内容。
- 确认 `TinySpire Characters` 本地 Addressables 组含两个角色 Prefab，且 catalog 使用表中完整 `Assets/...` 地址。
- 在 Editor 刷新后运行新增 EditMode 用例。`BattleParticipantPresenter` 的场景挂载、锚点配置及 BattleScene 实跑属于 M3A-4，未在本切片执行。

## 环境限制

验证时已有 Unity Editor 占用项目，且 `BattleScene.unity` 带有用户未提交改动。遵循项目规则，本轮未启动第二个 Unity 实例、未删除锁文件、未改写该场景，也没有用批处理方式绕过 Editor。
