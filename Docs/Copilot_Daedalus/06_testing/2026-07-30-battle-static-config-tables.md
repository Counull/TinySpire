---
title: 战斗静态配置表验证记录
page_type: testing
lifecycle: active
date: 2026-07-30
scope: DataTables/Datas/、Luban 生成产物与 TinySpire 编译
source: Luban 生成日志、UnityMCP 刷新与 dotnet 编译
status_source: ../SESSION_LOG.md
---

# 战斗静态配置表验证记录

| 检查 | 方法 | 结果 |
|---|---|---|
| 工作簿结构 | 隐藏 Excel 实例读取 `__tables__.xlsx`、`__enums__.xlsx` 与 6 张 `battle.*.xlsx` | 6 张表、3 个枚举、字段类型与样例值完整。 |
| 模板 ID 链 | 检查数据行与生成 JSON | 1001 英雄→卡组 1001→卡牌 3001→效果 4001；遭遇 5001→敌人 2001。 |
| Luban | `dotnet Tools/Luban/Luban.dll -t all -d json2 -c cs-newtonsoft-json ...` | 成功生成 `cfg.battle` 的 6 个记录类型、6 个表管理器、3 个枚举和 6 个 JSON 文件。 |
| 已删除 demo | Luban 重新生成 | `#demo.item.xlsx`、`demo_tbitem.json` 与 demo 生成代码均不存在；`Tables` 不再暴露 `Tbitem`。 |
| YooAsset 路径回归 | 生成后运行 Bootstrap 场景 | `battle_tbhero.json` 位于 `Assets/GameData`；重建 `Main` 内置包后，`ConfigService` 以 `Assets/GameData/battle_tbhero.json` 成功加载，控制台 0 error。 |
| STS 战士初始卡组 | Luban 生成的 `battle_tbdeck.json` 与 `battle_tbcard*.json` | deck 1001 为 5×Strike、4×Defend、1×Bash；Bash 关联 8 伤害与 2 易伤两个效果；`initialHandCount` 为 5。 |
| STS 配置运行验证 | 重建 `Main` 内置包后启动 Bootstrap 场景 | 控制台 0 error，配置服务成功加载更新后的战斗表。 |
| Unity 编译 | UnityMCP 全量资源刷新 | 无编译错误；仅有既有 `FindFirstObjectByType` 废弃警告与 MCP 包 WebSocket 警告。 |
| .NET 编译 | `dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` | 0 错误；13 条既有程序集版本冲突警告。 |

## 结论

静态配置已可由 `ConfigService.Tables` 加载为 `TbHero`、`TbEnemy`、`TbDeck`、`TbCard`、`TbCardEffect` 与 `TbEncounter`。本记录不宣称已完成运行时实例化、表间自动引用解析或卡牌效果执行。
