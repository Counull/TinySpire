---
title: TinySpire · G2-A Run Persistence 与继续游戏 · Grill 记录
status: proposed-for-plan
scope: G2-A only
created: 2026-08-16
roadmap: Docs/Copilot_Daedalus/RUN_ROADMAP.md
---

# G2-A · Run Persistence 与继续游戏

> 本文记录本轮 Grill 已确认的玩家规则、状态边界与独立验证门禁；它不是代码实施授权。
> G2-A 建立可恢复的单个 Run，不实现真实多节点地图、奖励、永久死亡、云存档或小游戏 SDK 接入。

## 1. 可观察目标

```text
Hero 确认 → 初始地图稳定态（S0） → 节点 → 节点完整结算 → 地图稳定态（S1）
                  ↓                                         ↓
                保存 S0                                  保存 S1

主菜单 → 继续游戏 → 恢复最近一份地图稳定态
```

玩家可在冷启动后恢复同一个有效 Run：角色、HP、稳定模板 ID、当前地图/节点事实和随机状态不重置。

当前 G1-A 的唯一节点胜利后，地图显示“节点已清除、后续内容未接入”。该 Run 仍保留，等待 G3 增加后续节点后继续接续；不能因为当前切片没有下一步而删档。

## 2. 存档检查点

### 2.1 只保存地图稳定态

- 节点完整结算、所有属于该节点的最后一次玩家选择完成、RunState 回到地图稳定态后，保存一份完整 Run Snapshot。
- Hero 确认并到达初始地图稳定态时保存首份 `S0`；这是首局没有“上一个节点结算”的唯一初始化检查点。
- 点击节点、进入战斗、战斗进行中、失败页、手牌/牌区/敌人/行动队列/动画中间态均不持久化。
- 节点尚未结算时中断，继续游戏从最近保存的地图稳定态进入；玩家重新选择该节点并从其开头开始。

### 2.2 战斗不产生存档副作用

```text
S0：地图稳定态，已保存
→ 点击节点 B：不改 S0，不写盘
→ B 的 BattleScene 临时运行
→ B 未结算就离开：S0 原封不动
→ B 胜利并回到地图：产生并保存 S1
```

- 战斗中“返回主菜单”允许存在；它不写、不删、不覆盖存档。
- BattleScene 的 BattleSession、BattleState、卡区、队列与表现应随场景生命周期销毁。
- Run 的地图稳定事实跨 Map / Battle 存活；只有 BattleResult 结算成功时才能把结果一次写回 Run。
- 当前 G1-A 的 `RunBattleSnapshot` 是进程内失败 SL 的既有实现细节，不是 G2-A 的持久化对象。计划阶段必须审计并调整现有 `InBattle` / `ActiveBattle` 写入，使“进入节点”不污染可持久化的地图稳定 Run 事实。

## 3. 主菜单与 Run 生命周期

- 系统只持有一个 active Run 槽。
- 存在有效 Run 时，主菜单显示“继续游戏”。
- 点“开始游戏”时，先明确确认“放弃当前 Run？”。
  - 确认：删除该 active save，销毁当前 Run 生命周期，才进入 Hero 选择并创建新 Run。
  - 取消：原 Run 原样保留。
- 胜利不删除 active save；当前 G1-A 单节点地图以“节点已清除、后续内容未接入”如实展示。
- 当前 G1-A 的失败 SL 规则不在本片改为永久死亡或最终结算。

## 4. 持久化模块与平台边界

```text
IRunSaveStore
├─ AtomicJsonRunSaveStore       # G2-A 真实交付
├─ WxRunSaveStore               # 独立 Platform Save Spike
├─ TtRunSaveStore               # 独立 Platform Save Spike
└─ CloudRunSaveStore            # 有后端后才引入
```

- `IRunSaveStore` 是游戏自有的公共持久化接口，承诺完整 Run Document 的 load / commit / delete 成功或失败；不得泄漏 `System.IO`、PlayerPrefs、WX SDK、TTSDK 或云 SDK 到 Run、Map、Battle 领域层。
- G2-A 的真实 Adapter 为 `Application.persistentDataPath` 下的单槽版本化 JSON：临时文件完整写入并校验后，再原子替换正式文件。
- 由 VContainer composition root 选择当前 Adapter；平台 `#if` 只允许出现在装配/Adapter 边界。
- 微信、抖音与 Android 的实际 SDK/容器/真机验证不混入 G2-A，单列为 **Platform Save Spike** Goal。该 Spike 才验证 WX/TT Adapter、真机退出/恢复、卸载/缓存边界与 Android 真实构建。

## 5. Save Document 的事实边界

- `RunSaveDocument` 带 `schemaVersion`，通过 DTO 显式序列化；不得直接序列化 Unity 对象、Prefab、Addressables handle 或领域对象图。
- 保存可变 Run 事实和稳定模板 ID：如 HP、当前节点/地图、随机状态、Hero/Deck/Encounter 等稳定 ID。
- 静态 Hero、Deck、Encounter 配置仍由 Luban / Addressables 的正式配置解析；读档时稳定 ID 缺失即明确失败，不复制整份静态配置进存档。
- 当前 G1-A 尚未有奖励、升级后的实例牌组；G2-A 只保存现存的 `DeckTemplateId` 等真实事实。G4 出现实例卡牌后，才通过 schema migration 增加实例牌组字段。

## 6. 版本与故障规则

- 每份存档有 `schemaVersion` 与明确迁移入口。
- 只有能被证明无歧义的旧版存档才自动迁移；不能迁移、JSON 损坏、配置 ID 缺失或平台读写失败时，不猜测默认值、不静默降级。
- 损坏或不支持的存档：禁用“继续游戏”、说明原因；仅在玩家确认后才删除。原始存档与可写入时的本地错误上下文保留作诊断物。
- 节点结算后的 commit 失败：保留内存中的结算结果，禁止进入下一节点，显示“保存失败，重试”。若玩家仍退出，必须明确提示会回退到上一份成功保存的地图检查点。
- 按 fail-fast 原则，读档、反序列化、schema 校验和平台写入首次失败必须显式报错；当前无后端，诊断物暂只在本地保留。

## 7. 明确排除项

- 真实多节点/分支地图与地图生成（G3）。
- 奖励、卡牌获得/升级、遗物、药水、金币、商店、事件、篝火、Act、Boss（G4+）。
- 战斗中途保存、手牌/敌人/队列/动画恢复。
- 云存档、跨设备同步、账号体系、远程崩溃上报。
- 微信/抖音 SDK、开发者工具、容器或真机存档接入；它们属于独立 Platform Save Spike。
- 反作弊、加密或把本地存档视作可信权威状态。

## 8. 计划前技术核对问题

实施计划阶段必须审计，而非凭本页假定：

1. 当前 Bootstrap、RunStateStore、RunFlowService、BattleLifetimeScope 的真实 VContainer 生命周期，及如何建立/销毁 RunScope 而不让 BattleScene 生命周期吞掉 Run。
2. 当前 `BeginBattle`、`ActiveBattle`、`RunBattleSnapshot`、`BattleResult` 的写入/读取链；将战斗输入变为瞬时 context 后，BattleResult 仍如何保持唯一、可验证的回写关联。
3. 当前主菜单/地图/失败页 Presenter 的可最小化 UI seam：继续、放弃确认、返回主菜单、读写失败和“后续内容未接入”的投影位置。
4. 当前 Newtonsoft.Json / 运行时可用 API 与测试程序集，及原子临时文件替换在 Editor/Standalone 的可验证实现。
5. EditMode / PlayMode 验收路径：S0/S1 round-trip、冷启动恢复、节点中断回 S0、胜利保留、主动放弃删除、损坏/不支持版本拒绝、commit 失败阻断与既有 G1 胜败回归。

## 9. 下一门禁

1. 对上述 seam 做技术审计。
2. 基于本 Grill 输出窄实施计划与验收矩阵；不自动实施。
3. 用户审阅并明确批准计划。
4. 才向 Coding Agent 下发一个只覆盖 G2-A 的实现 Goal。
