---
title: G2-A Run Persistence 与继续游戏实施计划
page_type: plan
lifecycle: active
date: 2026-08-16
scope: G2-A only
source: Docs/Hermes_Pegasus/design/2026-08-16-g2a-run-persistence-grill.md
status_source: ../SESSION_LOG.md
implementation_status: verified
---

# G2-A Run Persistence 与继续游戏

## 1. 范围与 seam audit 结论

G2-A1～A3 是同一个 Goal 的串行停止点，不存在 G2-B。本计划只实现单槽本地 Run 存档、地图稳定态检查点、继续游戏和对应失败 UI；不实现 G3+、Platform Save Spike、平台 SDK、云存档、多槽、奖励、地图生成或永久死亡。

当前有效结构以 G1-A 的正式实现为基线：`Bootstrap` 的长寿命 root Scope 持有 `RunStateStore` 与 `RunFlowService`，`RunEntryLifetimeScope` 只持有 View/Presenter，`BattleLifetimeScope` 随 BattleScene 卸载。`ARCHITECTURE_CONVENTIONS.md` 明确将 CD-009 的独立 `RunScope` 标为前瞻、未落地约定；因此本片不改 Scene/Prefab 或 parentReference，而由 Store 的显式 restore/clear 表达 active Run 生命周期。

测试 seam 已由 Grill 和当前公共边界确认：

- Save Document：稳定 `RunState` ↔ `RunSaveDocument` 映射、schema migration、结构/配置校验。
- Save port：`IRunSaveStore.Load/Commit/Delete` 的类型化结果；内存 fake 证明编排不依赖文件系统。
- 原子 Adapter：真实临时目录和可替换文件系统边界，证明校验后替换以及失败保留旧档。
- RunFlow：Hero 确认后的 S0、胜利稳定态 S1、Continue、放弃、commit 重试与回退提示。
- RunEntry：View 只提交意图并渲染 Continue/确认/错误投影；G1 失败重开仍走原 seam。

## 2. G2-A1 · Save Document 契约

按 RED → GREEN 的竖切顺序交付：

1. v1 DTO 只含 `schemaVersion`、Run/Hero/HP/Deck/Encounter、随机根、稳定节点状态与 attempt 序号；没有 BattleSession、ActiveBattle、snapshot、卡区、敌人、队列、动画或 Unity Object 字段。
2. 显式 codec/migration 入口先读取 `schemaVersion`，只接受当前 v1；坏 JSON、缺失版本、未知版本和字段/数值非法返回类型化失败，不猜默认值。
3. mapper 只允许 `Available` / `Completed` 且没有 transient battle facts 的地图稳定态；恢复时通过当前 Luban 表验证 Hero/Deck/Encounter ID。
4. `IRunSaveStore` 暴露 load/commit/delete 结果；内存 fake 用于 RunFlow 测试，不让领域层依赖 `System.IO`、PlayerPrefs、平台 SDK 或平台 `#if`。

停止点：S0/S1 round-trip、排除战斗中间态、坏 JSON/版本/配置 ID 和 fake store 定向测试全部通过。

## 3. G2-A2 · 原子本地单槽

1. `AtomicJsonRunSaveStore` 位于 Infrastructure 边界，生产路径由 composition root 注入 `Application.persistentDataPath`。
2. 在正式文件同目录创建临时文件，完整写入并 durable flush；重新读取、迁移、验证且与输入等价后，首次提交用同卷 move，已有档用原子 replace。
3. 任一步失败都返回类型化错误并保留旧正式档；不得降级为 delete+copy/move 覆盖。坏正式档与残留临时文件不静默删除。
4. Bootstrap 只把 Adapter 注册为 `IRunSaveStore`；Run/Map/Battle 不引用 Infrastructure 或文件 API。

停止点：真实临时目录 round-trip、替换失败旧档不变、坏正式档、残留 temp、delete 失败的定向测试全部通过。

## 4. G2-A3 · 检查点与继续游戏

1. 启动配置就绪后探测单槽但不自动 hydrate；只有玩家点击 Continue 才恢复最近成功的稳定态。
2. Hero 确认先创建内存 Run，再提交 S0；失败时停在保存失败页，重试同一 checkpoint，不重新取 entropy。
3. BeginBattle、失败页与重开全程不调用 save port；战斗中断后冷启动仍读取上一稳定档。
4. Victory 经唯一 `BattleResult` bridge 完整结算，形成 Completed 稳定态并回到 RunEntry 后提交 S1；失败保留 G1 snapshot/重开，不转永久死亡。
5. 有任何有效或不可用存档记录时，Start Game 先显示“放弃当前 Run？”；确认删除成功后才进入 Hero 选择，取消零写入。
6. 坏 JSON、未知 schema、缺失配置 ID 或 IO 失败禁用 Continue 并显示原因；只有玩家确认才删除。
7. commit 失败保留内存结算，阻止后续动作，提供重试；退出前二次提示将回退上一成功检查点。
8. 当前 Completed 节点保留存档并显示“节点已清除、后续内容未接入”。

停止点：RunFlow/Presenter/View/Localization 定向测试、完整 EditMode、唯一 Unity Editor 的冷启动 S0/S1/坏档/commit 失败与 G1 胜败手测通过，Console Error 为 0。

## 5. 高影响与回滚

- 不修改 Scene、Prefab、ProjectSettings、asmdef、HybridCLR 或 Battle 命令/结算结构。
- i18n 只增加本片运行时键并更新完成节点文案；修改后必须运行 Luban/Localization 同步与 `TinySpire/Build/Sync and Build All`。
- 回滚按 A1、A2、A3 新增文件和对应窄接线分别撤销；不得 reset/clean，不触碰现有 Hermes 美术与 GameData WIP。

## 6. 实施结果

G2-A1 → A2 → A3 已按本计划串行实施并验证，未建立 G2-B。最终完整 EditMode job `0004316410dc4b1e9db8d80312499dc4` 为 947/947；Luban、Localization / Local Addressables 同步构建与唯一 Editor 的稳定态主链通过。故障注入与真实临时目录自动化覆盖坏 JSON / UTF-8、未知 schema、配置缺失/漂移、write/move/replace/delete/read 失败、残留 temp、同一 checkpoint 重试和回退；这些故障未通过修改生产目录权限来做 Play Mode 注入。完整结果、手测步骤、Console 与剩余风险见 `../06_testing/2026-08-16-g2a-run-persistence.md`。
