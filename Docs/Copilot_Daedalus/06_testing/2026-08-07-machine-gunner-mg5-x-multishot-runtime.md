---
title: Marine Game 机枪兵 MG5 X 费与多段射击运行时验收
page_type: testing
lifecycle: active
created: 2026-08-07
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 MG5 X 费与多段射击运行时验收

## 范围

本记录验收 Hero 1002 的会话私有 `MachineGunnerBattleRuntime` 新增的首批资源与多段程序；默认 Hero 1001、共享 `BattleCommandQueue.Submit` 写入入口、场景和 UI 入口均不变。

本切片将下列 11 张卡从 `CatalogOnly` 翻为 `Implemented`，连同既有 5 张初始牌形成精确的 16 张可执行集合：`TumbleReload` (3214)、`HoldLine` (3220)、`Spray` (3224)、`BayonetParry` (3225)、`WildRampage` (3226)、`QuickElbow` (3227)、`HeavyElbow` (3230)、`HurricaneElbow` (3232)、`PrecisionShot` (3233)、`SixHits` (3256) 和 `QuickManeuver` (3258)。

## 已验收行为

- `CardCostKind.X` 在命令执行前冻结为玩家当前 Energy；X=0 合法，仍完成卡牌归宿但不虚构格挡、弹药或随机命中。
- 固定、最多消耗和消耗全部弹药均由职业程序在首次共享写入前原子校验；`Stim` 的额外弹药和额外射击次数属于同一次支付快照。
- 随机多段攻击每一段都从当前投影的存活敌人中取目标，死亡目标不会继续被选中；职业随机流以显式状态副本预演，只有整张卡成功移动后才回写。零段随机卡不会推进该流。
- 程序仅通过生成的 `MachineGunnerProgramId` 解释，不按卡牌模板 ID、名称或文案分支。`BattleCardPlayRules` 只投影目标输入和最小资源合法性，实际资源、伤害、抽牌和卡区结算仍由同一职业运行时在 Queue 链路内完成。
- 目录构建门禁由“前 5 个连续 ID”改为精确外部 key 集合；其余 48 张机枪兵目录卡仍必须为 `CatalogOnly`。

## 配置与构建验证

| 项目 | 结果 |
| --- | --- |
| `battle.card.xlsx` | 用工作簿导入、值差异检查、重新导入和渲染复核；仅 11 个 `implementation_status` 单元格由 `CatalogOnly` 改为 `Implemented`。 |
| Luban | 直接等价命令成功完成 validation 与 `battle_tbcard.json` 生成。 |
| 生成 JSON | 机枪兵快照共 64 张，其中 16 张 `Implemented`、48 张 `CatalogOnly`；已实现 ID 与本页范围完全一致。 |
| Unity 同步构建 | 已连接的单一 Unity 6000.5.5f1 Editor 执行 `TinySpire/Build/Sync and Build All`；控制台记录本地 Addressables 内容构建成功，耗时 13.262 秒。 |

## Unity 定向回归

| 项目 | 结果 |
| --- | --- |
| EditMode 任务 | `e7a502caaa4c4d738cb9a9a96ae6c6d7` |
| 汇总 | **15/15 passed，0 failed，0 skipped**，0.1218447 秒 |
| 覆盖 | 5 张初始牌、11 张本切片卡、X=0、随机流提交、全弹药/上限弹药支付、Stim、默认职业隔离、目录快照和越界 CatalogOnly 门禁。 |
| 控制台说明 | 刷新编译后产品错误查询为 0。测试结束后 Unity Test Framework 记录两条“Saving results to TestResults.xml” Exception 输出；任务本身通过，日志未含堆栈或产品代码失败。 |

## 未包含

- 尚未开启使用 Weakness、Smoke、Burn、Oil、Invisible 或 Vulnerable 的即时状态卡；它们需要下一切片的私有程序操作和逐卡验收。
- 尚未实现延迟伤害、下回合资源修正、结束行动、复杂 Power 触发、手牌选择、自动连锁出牌、临时机枪卡、Exhaust 路由或全息诱饵受击实体。
- `Overload` 与 `LimitOverload` 的来源语义允许临时超过 Energy 上限，当前共享 `PlayerTurnData` 会裁剪到上限；未经专门的共享资源模型决策，本切片不改变该规则。
- 未修改 Hero/Deck 配置、奖励/Run、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护的 Targeting/Candidates/Hermes 美术路径；未暂存、提交或推送。
