---
title: G2-A Run Persistence 与继续游戏验收
page_type: testing
lifecycle: active
date: 2026-08-16
scope: G2-A only
status_source: ../SESSION_LOG.md
source: Docs/Hermes_Pegasus/design/2026-08-16-g2a-run-persistence-grill.md
---

# G2-A Run Persistence 与继续游戏验收

## 1. 结论

G2-A 已按 A1 → A2 → A3 串行完成；A1～A3 是同一 Goal 的停止点，不存在 G2-B。当前 Windows Unity 6000.5.5f1 Editor 已验证单槽 versioned JSON、稳定态 S0/S1、显式 Continue、确认放弃、失败重试/回退与 G1 失败重开兼容。最终完整 EditMode 为 947/947 passed，Luban 与 `TinySpire/Build/Sync and Build All` 成功。

本记录只证明 G2-A：没有实现 G3+、Platform Save Spike、微信/抖音 SDK、云存档、多槽、奖励、地图生成或永久死亡；没有修改 Scene、Prefab、ProjectSettings、asmdef、HybridCLR 或 Battle 终局结构。

## 2. Seam audit 与最终边界

- CD-112 和当前 Scene parent seam 已由 Bootstrap root 跨 `RunEntryScene` / `BattleScene` 保留 Run Store / Flow；CD-009 的独立 RunScope 是未落地前瞻。本片选择显式 restore/clear，没有改两个高影响 Scene。
- `RunSaveDocument` v1 只含 `schemaVersion`、Run/Hero/HP/Deck/Encounter、random root、稳定节点状态与 battle attempt 序号。ActiveBattle、Battle snapshot、BattleSession、卡区、敌人、Queue、动画和 Unity Object 无可序列化入口。
- `IRunSaveStore` 位于 Run persistence port；只有 Infrastructure Adapter 依赖 `System.IO` / Unity persistent path。Run/Map/Battle 没有 PlayerPrefs、WX/TT SDK、平台 `#if` 或存档文件 API 依赖。既有 Battle address key 校验器的 `Path.HasExtension` 与存档无关，未做无授权重构。
- 冷启动先探测并验证档案，但不 hydrate。只有玩家点击 Continue 才恢复稳定 Run；恢复前验证 Hero/Deck/Encounter ID 和 Hero 最大生命配置兼容性。

## 3. G2-A1 · Save Document

最终契约包括严格 JSON 字段、字符串枚举、`schemaVersion = 1` 与显式 `RunSaveDocumentMigrator.MigrateToCurrent`。只有无 transient facts 的 `Available` / `Completed` Run 可形成 Document；恢复保持 RunId、当前/最大生命、模板 ID、random root、节点状态和 attempt 序号。

RED → GREEN：

- 初始编译 RED 证明 Document、codec、migration、mapper 与 port 尚不存在；随后非法 numeric enum 用例继续为 RED，直到 codec 改为精确字符串枚举。
- `RunSaveDocumentTests` 最终 job `99e02513cf8d4da3be06d41156a4bf7b` 为 10/10。
- 审阅新增 Hero `MaxHealth` 配置漂移 RED：job `21d81c187a4842d09a905f2618558c54` 原先错误返回 Success；修复后在冷加载阶段返回 `InvalidDocument`，不把不兼容 Run 带入 Battle。

## 4. G2-A2 · 原子本地单槽

生产文件为 `persistentDataPath/run-save.json`，临时文件为同目录 `run-save.json.tmp`。提交使用严格 UTF-8、WriteThrough 与 `Flush(true)`；从 temp 重新读取、迁移、校验并比较后，首次档同卷 Move，已有档使用 `File.Replace`。任何异常都返回 typed failure，绝不回退到 delete+copy/move 覆盖旧正式档。

RED → GREEN：

- 原子 Adapter 初始 6 个定向用例 job `658dba6757f745de8776a9cbbe783691` 为 6/6，覆盖首次提交、旧档替换保护、坏正式档和残留 temp。
- 非法 UTF-8 bytes 最初从 Load 逸出 `DecoderFallbackException`；job `21d81c187a4842d09a905f2618558c54` 捕获该 RED。正式档读取失败与残留 temp 的组合用例 job `3bc0090c6f9f4a49b89f99ce11e7855c` 证明诊断 flag 曾丢失。
- 最终补齐真实二次 `File.Replace` 成功、首次 Move 失败保留 temp 且不造 live、Delete 失败保留 live、严格 UTF-8 typed failure、I/O 失败保留已知 temp、存在性检查不吞权限/IO 错误。审阅补丁定向 job `96604e4d2d3c400e9eac5b51516c0c1f` 为 4/4。

## 5. G2-A3 · 检查点、Continue 与 UI

- Hero 确认创建稳定 Run 并提交 S0；失败进入保存失败页，阻止后续推进，Retry 使用同一缓存 Document，不重取 entropy。
- Victory 由 child-scope `BattleResultRunBridge` exactly-once 写回，`RunStateStore` 清除战斗 transient 并形成 Completed；回到 RunEntry 地图稳定态后才提交 S1。BeginBattle、战中、Defeat 与 Restart 不调用 save port。
- 有有效档时 Start 先显示“放弃当前 Run？”；取消不删除，确认成功后才进 Hero 选择。坏 JSON、未知 schema、配置缺失/漂移与 IO 故障禁用 Continue、显示原因，并仅在用户确认后删除。有效档删除失败保留 Continue 能力。
- S1 提交失败保留当前内存结算、允许同一 S1 重试；选择退出先提示会回退上个 checkpoint，确认后清除未提交内存态并重新探测正式档。
- 当前唯一节点 Completed 后保留档案、禁用入战，并显示“节点已清除、后续内容未接入”。G1 Failed 页、snapshot 与新 seed Restart 入口保持原样。

自动化证据：

| 范围 | 结果 |
|---|---|
| RunFlow S0/S1、Continue、错误、重试、回退、删除 | job `f529af4d5e4d4df88afe807fbf8af492`：19/19 passed；后续补强删除失败 Continue 与 cold Completed S1 |
| Presenter / View 页面与意图 | job `5b6b4daffc404f7a9ca655491a564a44`：25/25 passed |
| Localization 键与精确文案 | job `87dd0c0a226e44f1bc57312080a45d1c`：43/43 passed |
| 审阅后 persistence 聚合 | job `e8e80f4de46c4541bd0d76b0017a3bae`：67/67 passed |
| G2-A 相关聚合 | job `a287a16c93f24a66b46c27e994cdc36b`：115/115 passed |
| 最终完整 EditMode | job `0004316410dc4b1e9db8d80312499dc4`：947/947 passed，0 failed，0 skipped，19.641 秒 |

## 6. 配置、Localization 与本地内容构建

`i18n.xlsx` 新增 Continue、放弃确认、档案错误、commit 重试与回退相关键，并把完成节点中文精确更新为“节点已清除、后续内容未接入”。`LocalizationBuildTools.RequiredRunEntryKeys` 与字形门禁同步更新。

- `DataTables/gen.bat`：成功。
- `TinySpire/Build/Sync and Build All`：成功。
- Local Addressables content build：14.565 秒。
- 完成行：`TinySpire sync and local content build completed successfully.`

本片没有新增素材地址域、Scene 或 Prefab；本轮没有执行 Player build，也没有把当前 Editor Play Mode 冒充目标平台 / Platform Save Spike 证据。

## 7. Unity Play Mode 手测

本轮只使用当前唯一 Unity Editor，没有启动 batch 实例、结束用户进程或清理 Library / Temp。操作通过生产 UI Button 和现有 Battle command queue 完成，没有直接写 RunState 或伪造 BattleResult。

1. 无正式档启动：主菜单 Continue 禁用。Start → Hero 1001 → Confirm 后进入地图；live 存在、temp 不存在，codec 为 Success，S0 为 `Available / 30/30 / attempt 0`。
2. 停止并重新 Play：Continue 启用；Start 先进入“放弃当前 Run？”；Cancel 后 live 不变且 Continue 仍可用。点击 Continue 恢复地图 30/30，未改写正式档时间戳。
3. 点击唯一节点进入 Battle：正式档仍是相同 S0，节点/attempt/写入时间不变，temp 不存在，证明 BeginBattle 没有写盘。
4. 通过真实卡牌/目标/结束行动命令完成胜利：bridge 回到 RunEntry 后 live 为 S1 `Completed / 12/30 / attempt 1`，temp 不存在；节点禁用且显示精确完成文案。
5. 再次停止并 Play：Continue 恢复 Completed、12/30、attempt 1，节点仍禁用且文案一致。
6. 再次从 Start 打开放弃确认并确认：进入 HeroSelection，live/temp 均不存在。

每段主链的 Console error/warning 查询均为 0。最终完整 EditMode 后 Console 的两条 error-filter 结果分别是测试自身的“battle card localization validation passed” Debug.Log 和 Test Runner 的“Saving results to TestResults.xml”，不是产品异常；最终 idle Console 再检查为 0 产品错误。

坏 JSON / UTF-8、未知 schema、缺失/漂移配置、不可写/读取/Move/Replace/Delete 失败、commit Retry 和 rollback 均由可控 fake 与真实临时目录 EditMode 用例验证；没有通过修改真实 `persistentDataPath` 权限来制造高风险手测故障。

## 8. 风险与未覆盖

- 当前生产路径为 `C:/Users/Lxxr/AppData/LocalLow/DefaultCompany/TinySpire`。未来 CompanyName / ProductName 变化会改变 `persistentDataPath`，需要显式迁移决策，不能视为本档自动搬迁。
- `File.Replace` 已在当前 Windows Editor 的真实临时目录验证二次提交；目标平台能力、沙盒路径与原子替换保证仍属于未授权的 Platform Save Spike / Player 验收。
- 若 Delete 已先删除残留 temp、随后删除正式档失败，当次 failure 结果中的 `HasPendingTemporaryFile` 可能仍保留删除前值；下一次 Refresh 会纠正。该诊断展示细节不影响正式档保留、Continue 能力或新开局阻断语义，本片不为此扩大 Adapter 状态机。
- 没有 Player build、目标移动平台、云、多槽、SDK、跨设备、战中存档、奖励、真实地图或永久死亡验证。
- schema v1 有显式 migration 入口但目前只有 v1；未来新增字段必须新增迁移与旧档 fixtures，不得在 codec 中猜默认玩法语义。
