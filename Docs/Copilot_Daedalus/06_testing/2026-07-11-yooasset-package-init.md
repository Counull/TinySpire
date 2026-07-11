---
title: YooAssets 内置包初始化验证
page_type: testing
lifecycle: active
date: 2026-07-11
---

# YooAssets 内置包初始化验证

## 验证范围

- `YooAssetPackageService.InitializeAsync()` 创建或获取 `Main` 资源包。
- 离线模式读取 `StreamingAssets/yoo/Main/`。
- 场景加载前请求包版本并激活对应 Manifest。
- Luban JSON 按普通 AssetBundle 中的 `TextAsset` 读取。

## 结果

- 代码诊断通过：`YooAssetPackageService.cs` 未发现错误。
- 代码诊断通过：`LubanConfigService.cs` 未发现错误。
- 已从构建报告确认 GameData 是普通 Bundle，因此不使用 `LoadRawFileSync`。
- 已确认 `BattleScene` 原为依赖收集器，已改为主资源收集器；旧 Manifest 尚未包含该场景。
- 重新构建 Main 包并同步到 `StreamingAssets/yoo/Main/` 后，才能进行场景运行时验证。
- 尚未执行 Unity Play Mode 运行时验证。
