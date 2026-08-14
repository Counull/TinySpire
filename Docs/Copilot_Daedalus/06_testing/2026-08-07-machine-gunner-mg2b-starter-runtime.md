---
title: Marine Game 机枪兵 MG2B 初始牌运行时验收
page_type: testing
lifecycle: complete
created: 2026-08-07
updated: 2026-08-07
status: passed
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
related_requirement: ../01_requirements/2026-08-07-marine-game-card-requirement-digest.md
---

# Marine Game 机枪兵 MG2B 初始牌运行时验收

## 范围

本记录只验收机枪兵首批 5 个初始牌程序：射击、肘击、防御、装填和兴奋剂。它们通过 `HeroRuntimeProfile.MachineGunner` 创建会话私有运行时，并仍只由 `BattleCommandQueue.Submit` 写入。其余 59 张机枪兵目录卡仍为 `CatalogOnly`；本记录不宣称奖励、升级、地图、Run、角色选择或场景流程已完成。

## 已验证事实

| 检查 | 结果 | 证据 |
|---|---|---|
| 职业隔离 | 通过 | `Hero 1002` 的 `runtime_profile=MachineGunner` 才创建 `MachineGunnerBattleRuntime`；默认 Hero 1001 保持 Legacy，既有资源/回合路径未替换。 |
| 初始资源与手牌 | 通过 | 机枪兵首回合为 Energy `3/5`、Ammo `5/5`，仍复用共享补至 5 张手牌。 |
| 五张初始牌 | 通过 | 射击要求显式活敌并支付 1 Ammo；肘击自动最近敌；防御获得 5 Block；装填补满 Ammo；兴奋剂抽 2 并在额外 Ammo 足够时为射击追加一次命中。 |
| 原子失败 | 通过 | Ammo 不足时返回 `InsufficientAmmo`，不写资源、参与者或卡区。 |
| UI 读取边界 | 通过 | `HandCardContainer` 使用同一会话的职业规则，自动目标/自身目标不会被 UI 误判为必须显式选敌；未改场景、Prefab 或 Targeting 素材。 |
| 生成配置兼容 | 通过 | `program_id`、`runtime_profile` 作为生成反序列化必填字段后，所有手写测试夹具都显式写入 Legacy/None 值。 |
| 本地化作者表 | 通过 | Hero 名称行的 `smart` 已修为文本 `false`，与其余 Smart String 列一致；工作簿重导入和渲染检查通过。 |

## Unity 验收

| 检查 | 结果 | 证据 |
|---|---|---|
| 定向回归 | 通过 | 任务 `b4fe36bc267b43c09764075715c12f2c`：58/58 EditMode passed，覆盖队列展示、M8D、参与者反馈路由和手牌目标焦点等受生成字段影响的夹具。 |
| Unity 同步构建 | 通过 | 同一已连接 Editor 执行 `TinySpire/Build/Sync and Build All`；Console 记录 Local Addressables 构建成功及 `TinySpire sync and local content build completed successfully.`。 |
| 完整 EditMode | 通过 | 任务 `36884b711939459f932297342218fddc`：500/500 passed，0 failed、0 skipped。 |

## 后续边界

完整 64 张卡仍需按计划中的 MG3--MG7 串行推进。尤其 Weakness/Smoke/Burn/Oil、延迟伤害、Power、独立卡牌随机流和资源覆盖必须由职业运行时的回合/敌方时机接入；驻防与排气散热还需要权威 PendingResolution/命令协议，不能由 UI 直接改手牌。

## 范围审计

未修改默认 Hero 1001、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动/DI、地图、敌人配置、奖励/Run 或受保护 Targeting/Candidates/Hermes 美术路径；未暂存、提交或推送。
