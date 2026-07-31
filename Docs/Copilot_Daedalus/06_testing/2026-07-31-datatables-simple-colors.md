---
title: DataTables 工作簿简易配色验收
page_type: testing
lifecycle: verified
updated: 2026-07-31
status_source: ../SESSION_LOG.md
---

# DataTables 工作簿简易配色验收

## 范围

- 工作簿：`__beans__.xlsx`、`__enums__.xlsx`、`__tables__.xlsx`、`battle.card.xlsx`、`battle.card_effect.xlsx`、`battle.deck.xlsx`、`battle.encounter.xlsx`、`battle.enemy.xlsx`、`battle.hero.xlsx`、`i18n.xlsx`。
- 配色：首行深蓝底白色粗体；Luban 类型行浅蓝、分组行浅灰、说明行浅金；内容区按列循环使用蓝、绿、金、紫、橙、青六组淡色，并以同色深浅交替区分相邻数据行。
- 明确不改：单元格值、公式、共享字符串、字段顺序、Luban 表定义、稳定 `Assets/...` 地址与生成数据语义。

## 工作簿完整性

- OpenXML 写入先在临时副本完成，所有工作簿全部校验通过后才覆盖项目文件。
- 逐工作簿提取工作表名、单元格坐标、类型、公式、值、内联字符串和共享字符串，计算忽略样式后的 SHA-256；配色前后 10/10 哈希一致。
- 每个 `.xlsx` 中的 XML / `.rels` 均可重新解析，所有单元格样式索引均落在有效 `cellXfs` 范围内。
- 抽样检查确认实际颜色为：表头 `FF365F91`、类型 `FFDCE6F1`、分组 `FFE7E6E6`、说明 `FFFFF2CC`；六组内容列浅色为 `FFF4F8FC`、`FFF3F9F4`、`FFFDF9EE`、`FFF8F4FB`、`FFFCF5F1`、`FFF0F8F8`，对应的交替深色为 `FFE7F0F8`、`FFE6F2E8`、`FFF7EFCF`、`FFEFE7F5`、`FFF5E9E1`、`FFE2F1F1`。表头字体为白色粗体。

## 配置生成

- Luban 命令成功退出，表格读取、数据校验、C# 与 JSON 生成均无错误。
- 生成前后分别统计 `Assets/Scripts/Core/Generated/Config` 与 `Assets/GameData`：文件数 `55 / 55`，内容 SHA-256 变化文件 `0`。
- `battle.card.xlsx` 既有 `illustration_address` 列及四个完整 `Assets/Arts/Runtime/Card/card_art_*.png` 地址保持不变。

## Unity 与 Addressables

- 使用用户已打开的 Unity `6000.5.5f1` Editor；未启动或结束其他 Editor。
- Unity MCP 强制刷新完成，编译与域重载回到 idle；刷新后控制台 Error 为 0。
- 回归先后暴露四张牌面的磁盘导入模式不一致，与 Single Sprite 合约测试不符；该问题不来自 Excel 内容或 Luban 输出。
- 通过 Unity MCP 将 strength、strike、defend、bash 全部统一为 `TextureImporterType.Sprite`、`SpriteImportMode.Single`、`mipmapEnabled = false`，未修改图片像素、GUID 或稳定地址；最终定向用例 1/1 通过。
- 最终全量 EditMode：35/35 通过，失败 0，跳过 0，耗时 `2.167s`；清理 Test Runner 写入结果文件的提示后 Console Error 为 0。
- 最终 `TinySpire/Addressables/Build Local Content` 报告：`buildlayout_2026.07.31.20.39.59.json`，`BuildError` 为空，构建哈希 `f347180971402fb852359628813c07b2`，耗时 `8.911s`。

## 结论

10 个配置工作簿已完成横向语义行与纵向内容列的纯显示层配色，配置内容与生成产物保持等价；Unity 配置加载链、卡牌插画 Single Sprite 合约和本地 Addressables 内容均通过最终回归。
