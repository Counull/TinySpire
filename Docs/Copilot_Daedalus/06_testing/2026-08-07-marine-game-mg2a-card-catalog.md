---
title: Marine Game 机枪兵 MG2A 卡牌目录验收
page_type: testing
lifecycle: complete
created: 2026-08-07
updated: 2026-08-07
status: passed
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
related_requirement: ../01_requirements/2026-08-07-marine-game-card-requirement-digest.md
---

# Marine Game 机枪兵 MG2A 卡牌目录验收

## 范围

本页只记录 MG2A 的 64 张 `CatalogOnly` 目录录入与静态门禁。它不验证也不声明机枪兵卡牌可抽、可打、可升级或已接入 Deck；地图、敌人、奖励、Run、场景和 UI 继续在范围外。

## 已完成的静态验证

| 检查 | 结果 | 证据 |
|---|---|---|
| 作者表结构与来源追溯 | 通过 | `battle.card.xlsx` 为 ID `3201`--`3264` 的 64 行；`i18n.xlsx` 为 192 条 key。重导入检查确认唯一 `MARINE_*` key、64 张空绑定/占位、128 条 Smart 描述；zh-CN 基础/升级文本逐条等于 `cards.json.desc` / `known_upgrades.change`。 |
| 工作簿质量 | 通过 | 重新导入、公式错误扫描为 0，渲染检查覆盖新增卡表行与全部新增 i18n 行；保持既有表格样式。 |
| Luban | 通过 | 在 `DataTables/` 目录执行 `gen.bat`，日志完成 `validation end`、JSON 生成和 `game-config.json` 回拷。 |
| 生成卡表快照 | 通过 | `Assets/GameData/battle_tbcard.json` 手工读取：64 张、`3201`--`3264`、64 `CatalogOnly`、64 空 `effect_bindings`、64 `art_placeholder`、63 有升级/1 无升级。 |
| C# 编译 | 通过 | `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`：0 error；12 条既有程序集版本冲突 warning。 |

## Unity 验收

| 必需检查 | 结果 | 证据 |
|---|---|---|
| Unity MCP 连通性 | 通过 | 单一 `TinySpire` 6000.5.5f1 Editor，非 Play、idle，Refresh 后 Console 无产品编译错误。 |
| `TinySpire/Build/Sync and Build All` | 通过 | Console 记录 `TinySpire sync and local content build completed successfully.`；Local Addressables 构建约 8.341 秒。 |
| Localization 导入与验证 | 通过 | 同步构建完成导入；显式 `TinySpire/Localization/Validate Battle Card Text` 输出 `validation passed`。 |
| Local Addressables 内容重建及 InvalidKey 检查 | 通过 | Console 记录 `Library/com.unity.addressables/aa/Windows/settings.json`；没有 InvalidKey/产品错误。 |
| `MachineGunnerCatalogSnapshotMG2ATests`、目录与快照回归 | 通过 | 任务 `0098fec0e0204ade8ef8d22a6245709e`：21/21 passed（机枪兵 4、目录门禁 10、Ironclad 7）。 |
| 配置/本地化/黄金基线相关回归 | 通过 | 任务 `11741e4d623947c4b7335b1de013a048`：19/19 passed。 |

最初 MCP 没有可连接实例时，本切片没有启动/结束任何 Unity 进程或用批处理绕过；待用户已有 Editor 恢复连接后，才在同一实例中完成以上验证。Console error 过滤中的“localization validation passed”和 Test Runner 保存 `TestResults.xml` 是工具分类为 Exception 的成功日志，不是产品失败。

## 后续边界

MG2A 已完成；下一停点是 MG2B 的 Card Program 预构建/原子提交。该步必须保持 `BattleCommandQueue.Submit` 为唯一共享写入入口，并在引入任何机枪兵实际卡牌、Ammo 支付、随机、目标选择或状态之前证明失败零写入。

## 范围审计

没有修改 Hero/Deck、默认 BattleSession 装配、出牌执行、Ammo、状态、Power、目标选择、抽弃牌、随机流、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动/DI 或任何受保护美术路径；没有暂存、提交或推送。
