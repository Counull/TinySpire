---
title: 牌面短键与 Addressables 逻辑地址迁移验收
page_type: testing
lifecycle: verified
updated: 2026-07-31
status_source: ../SESSION_LOG.md
---

# 牌面短键与 Addressables 逻辑地址迁移验收

## 环境

- Unity `6000.5.5f1`，使用用户已打开的 TinySpire Editor；未启动或结束其他 Unity 实例。
- 工作簿使用临时安装的 ClosedXML `0.105.1` 定向修改，没有向仓库添加 NuGet 或 Unity 依赖。

## 配置与资源

- ClosedXML 临时副本比较通过：`Sheet1!H1`、`H4`、`H5:H8` 为唯一授权变化；其余单元格值、公式、样式、列宽、行高、合并区域不变。
- Luban 生成成功，`Card.IllustrationKey` 与 `Assets/GameData/battle_tbcard.json` 的四个 `illustration_key` 已更新；手写 `game-config.json` 已按 `gen.bat` 流程复制回 GameData。
- strength、strike、defend、bash 四张图移入 `Assets/Arts/Runtime/Card/Illustrations/`，四个 `.meta` GUID 与迁移前一致。
- `TinySpire Card Art` 保持 `PackTogether`，四个地址分别为 `card-art/card_art_strength`、`strike`、`defend`、`bash`，没有残留完整 Assets 路径。

## 自动化结果

- `dotnet build TinySpire.sln --no-restore`：0 error。
- 定向 EditMode：4/4 通过，覆盖短键约束、目录与导入设置、资源组逻辑地址、四张 Sprite 的 Addressables API 加载。
- 全量 EditMode：38/38 通过，失败 0，跳过 0。
- 最终 `TinySpire/Addressables/Build Local Content`：成功，Unity 报告构建耗时 `6.7s`，输出 `Library/com.unity.addressables/aa/Windows/settings.json`。

## 启动检查

- BootstrapScene 进入 Play Mode 后等待 5 秒，Console 只有 `game-config.json 已加载。`，没有 Error、`InvalidKey` 或资源地址错误；随后已退出 Play Mode。
- 牌面本身由定向测试直接调用 `Addressables.LoadAssetAsync<Sprite>` 验证四个逻辑地址，全部成功并释放句柄。

## 结论

短键配置、专用目录、逻辑地址、Luban 生成与本地 AB 构建链路均已通过验收。工作簿未进行 Excel GUI 渲染；布局完整性由 ClosedXML 语义/样式比较覆盖，仍可由用户在 Excel 中做一次肉眼确认。
