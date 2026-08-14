---
title: Marine Game 机枪兵 MG8 功夫机甲、开火与连肘验收
page_type: testing
lifecycle: active
created: 2026-08-07
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 MG8 功夫机甲、开火与连肘验收

## 范围

本记录只验收 Hero 1002 单场私有运行时的 `KungfuMech` (3212)、`ElectroBoost` (3236) 与 `ComboElbow` (3242)。不创建奖励入口、Run、Power HUD、升级实例、场景或第二条共享写入链。

## 已证实规则

- 功夫机甲复用既有 `PowerPile` 和会话私有层数；每层让每张成功完成的非射击攻击获得 4 Block。双层能力牌只加一次 8 Block，射击攻击不触发。
- 电磁增压把 `FirePower +2` 写入玩家私有状态，可叠加；每个常规射击伤害段在 Weakness 前读取该层数，非射击不读，玩家行动结束时归零。压缩包 `battle.html` 的狙击分支没有读取 `firePower`，故狙击也不读开火；该差异由纯伤害管线回归锁定。
- 连肘为最近存活敌人 10 点攻击。仅在当前玩家回合紧邻的上一张**成功**牌是非射击攻击时，本张冻结为 0 Energy；连肘自身可延续链，技能、能力、射击或新玩家回合会断链，失败或未归宿卡不会改变链事实。
- 费用合法性和队首执行共用 `TryPreviewCost`，因此先以 1 Energy 打出肘击后，即使剩余 0 Energy 也能评估、提交和结算免费连肘；没有 UI 与运行时两份折扣实现。

## 配置与生成复核

| 项目 | 结果 |
| --- | --- |
| 作者工作簿 | 使用 `@oai/artifact-tool` 导入、值差异、重新导入和前后渲染复核；仅 Q102、Q126、Q132 的 `implementation_status` 从 `CatalogOnly` 改为 `Implemented`。 |
| 工件部署 | 部署后的 `DataTables/Datas/battle.card.xlsx` SHA-256 为 `7956CD884E0C97585C60DA3C209E84761EEB4CA88421D7D9A0EDACB5DBA53D73`，与复核工件一致。 |
| Luban | 等价 Luban 命令 validation/生成成功；生成器移除的 `TinySpire/Assets/GameData/game-config.json` 已从 `DataTables/game-config.json` 恢复。 |
| 生成目录 | `battle_tbcard.json` 已直接核对为 34 张 `Implemented` / 30 张 `CatalogOnly`；新增 ID 精确为 3212、3236、3242，目录门禁以 `MARINE_*` 外部 key 集合冻结。 |

## Unity 验收

| 项目 | 结果 |
| --- | --- |
| Editor | 仅使用唯一已连接的 Unity 6000.5.5f1 Editor；未启动、终止或驱动第二个 Editor/游戏窗口。 |
| 同步与本地内容 | `TinySpire/Build/Sync and Build All` 成功；控制台记录 Addressable content successfully built（11.372 秒）及 TinySpire sync and local content build completed successfully。 |
| 定向 EditMode | Unity MCP 任务 `760444327c1242a5b737f375eef4aaec`：**51/51 passed，0 failed，0 skipped**，总时长 0.5411895 秒。 |
| 覆盖用例 | `MachineGunnerStarterRuntimeTests` 覆盖功夫机甲、开火、免费链与 0 Energy 放行；`MachineGunnerDamagePipelineTests` 覆盖常规射击/狙击的开火差异；`BattleCardPlayRulesTests`、`MachineGunnerCatalogSnapshotMG2ATests`、`BattleCardCatalogBuildValidatorTests` 覆盖规则和 34/30 门禁。 |

## 未包含

- 升级字段仍只是配置来源：项目目前没有通用 CardInstance 升级状态，未把功夫机甲 +6、开火 +3 或连肘 13 伤害硬编码为升级行为。
- 未实现逐段命中的燃烧弹药、陈年机油、钉刺射击与十二连；未实现 BurningOil、Exhaust、延迟/下回合效果、选择协议、临时卡、自动连锁、奖励/Run 或 Power HUD。
- 未修改 Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动/DI，或受保护的 Targeting/Candidates/Hermes 美术路径；未暂存、提交或推送。
