---
title: G3 确定性尖塔式 Act 地图验收记录
page_type: testing
lifecycle: active
date: 2026-08-24
scope: G3 only
status_source: ../SESSION_LOG.md
source: Docs/Hermes_Pegasus/design/decisions.md#决策-012g3-地图采用尖塔式分层路线
implementation_status: verified
---

# G3 确定性尖塔式 Act 地图验收记录

## 1. 当前结论

G3 已完成并标记为 `verified`。最终真实证据为：

- seam audit 完成；MapDefinition / Store / Flow / View / save port 的所有权与生命周期无需高影响重构。
- 静态 `dotnet build`：生产 **0 errors / 6 warnings**；Editor **0 errors / 12 warnings**。
- Mono 定向 runner：map+store **25/25**、save **21/21**、atomic **19/19**、flow **22/22**、presenter **15/15**。
- 首次完整 Unity 的两项 RED 分别暴露 i18n workbook 四格漂移与测试构造参数未同步，修复后最终交互式完整 EditMode job `8e910a98b14f4fe4b4901ba78bf060dc` 为 **993 passed / 0 failed / 0 skipped，44.1991795s**。
- `Sync and Build All`、Local Addressables、Packed Play 胜利→BossGate 与失败终局两条生产链、两次进程级冷启动及各检查点 Console Error=0 均已完成。

## 2. Seam audit 与最终边界

- `MapDefinition` 是由固定 `ActMapProfile + map seed + generator version` 创建的不可变整图事实，冻结节点、边、Encounter、Boss 和 fingerprint。
- `RunStateStore` 是 Run/Map 可变事实唯一写入所有者；`RunFlowService` 只编排；RunEntry View 只投影/提交 `NodeId` 命令，hover 不写状态。
- Bootstrap root / child Scene Scope、Battle setup source、child-scope Result bridge 和原子单槽 save port 原位复用；当前不改 Scene、Prefab、asmdef、ProjectSettings、HybridCLR 或 DI 架构。
- 普通移动、未来 WingBoots 模式、完整 downstream 和可达 Boss 都由纯规则计算；UI 和节点对象不硬编码可选性。
- `MapDefinition` 防御性复制构造输入；validator 结构门覆盖 profile/version、稳定 ID/位置/内容、重复/缺失/非相邻边及 Boss 出边。存档另有 path drift fail-fast 证据。
- 只读 identity catalog 仅做展示投影：5001 为 `SLIME PATROL` + 首敌本地化名 + Slime silhouette；5002 `SENTRY LINE` 仅用于判别测试；9001/9002/9003 为 `BOSS ALPHA/BETA/GAMMA` + Crown/Horns/Eye 三种锚点。名称/锚点不进入 MapDefinition 或存档。
- Boss 只做到明牌终点与 `BossGateReached`；不做真实 Boss Battle、奖励或 RunOutcome。

## 3. RED → GREEN 自动化矩阵

| 范围 | 必须证明 | 当前证据 |
|---|---|---|
| Generator / Profile / Validator / Reachability / Store | 同 recipe 同图、固定 Layer/Slot、冻结身份、defensive-copy、结构门、普通/WingBoots、downstream、非法选择零写入、BossGate/Defeat | Mono map+store **25/25 PASS** |
| Save schema / restore | recipe-only round-trip；无整图/UI/derived/attempt 字段；profile/version/fingerprint/path/config 漂移失败；v1 fail-fast | Mono save **21/21 PASS**；含新增 path drift |
| Atomic terminal recovery | recovery artifact 先耐久校验；同文档重试复用、不同 intent 拒绝、损坏 fail-closed；Delete live→intent→tmp | Mono atomic **19/19 PASS** |
| RunStateStore / Flow | 非法/过期结果拒绝、胜利回图、BossGate、失败终局、Continue/删除编排 | Mono flow **22/22 PASS** |
| Presenter | 全图名称/ID 明牌；只允许规则候选；hover 高亮完整后继/可达 Boss 并弱化放弃路线 | Mono presenter **15/15 PASS** |
| View | 稳定节点锚点/身份剪影投影、hover 弱化与清除 | Unity `RunEntryViewTests` **13/13 PASS** |
| 静态编译 | 产品与 Editor 程序集无 C# error | 生产 **0 errors / 6 warnings**；Editor **0 errors / 12 warnings** |
| 完整 EditMode | 全项目 0 failed / 0 skipped | 最终交互式 job `8e910a98b14f4fe4b4901ba78bf060dc`：**993/993 PASS，44.1991795s** |

首次完整 Unity RED 的两项来源已经保留：i18n workbook 四个单元格漂移，以及测试未跟随生产构造函数新增 identity catalog 参数。二者已分别修正 workbook 与测试构造参数，并由最终 993/993 完整套件复验为 GREEN。Headless `-nographics` 诊断另得 989/993：CardArt、Character Addressables 各一次 180 秒超时及两项 HUD 图形/几何失败均为 headless 不兼容；交互式 Unity 首轮 992/993 后，聚焦 CardArt 定向 job `5112cb216356432bbe615c0d49f7aa3c` 1/1 通过，最终交互式全量 993/993 是权威结果。

## 4. Save recipe、fingerprint 与旧档门禁

当前 schema v2 在既有 Run/Hero/HP/Deck/random root 事实之外，地图与进度只允许保存：map seed、generator version、profile/config ID、规范化整图 SHA-256 fingerprint、实际 path、稳定 progress phase、可选 committed node 与 terminal reason。

已由 save 21/21 证明不会保存：MapDefinition、节点/边副本、可选节点、可达 Boss、hover、颜色、布局、动画、`BattleAttemptSequence`、BattleSession、ActiveBattle、卡区、敌人状态或 Queue。恢复后的 attempt 只由已完成 Combat path 与 phase 推导。

恢复顺序是：精确 profile/version → 以 map seed 重建 → validator → fingerprint 精确比对 → path/phase 校验与恢复。save 21/21 新增 path drift，和 generator/profile/fingerprint/config 漂移一起 fail-fast。schema v1 缺 profile/version/map seed/fingerprint/path/Boss 身份，无法无歧义迁移；结果是 typed UnsupportedSchema，而不是默认 profile、补边或重新随机。

上述 mapper/codec/restore 已取得 Mono save 21/21、最终完整 Unity 993/993、Packed Play 与进程级冷启动的联合证据。

## 5. 失败终局验收合同

普通战斗失败的目标顺序是：当前 attempt 的 Defeat Result → Store `Terminal(Defeat)`，失败 Combat 节点不标完成、不追加实际路径 → 同一 terminal document 经原子 save port 提交 → Continue 禁用 → 失败页。Adapter 先耐久写入并回读校验 `terminal-intent recovery artifact`，再发布 temp/live；相同 document 重试复用恢复物，不同 validated intent 拒绝，损坏或非终局恢复物 fail-closed。提交失败不得 rollback 为可继续的旧 checkpoint，也不得恢复同节点 Restart。

玩家确认离开后才按 live → intent → temp 删除；live 删除失败时保留恢复物，避免旧 Continue 复活。若在确认前退出、崩溃或重启，冷启动直接恢复失败页而不是提供 Continue。Mono atomic 19/19 与 flow 22/22 已覆盖 Adapter/编排合同；生产 UI 已实际证明失败页进程级冷启动与确认后删除。

## 6. 构建与内容验证

| 检查 | 当前状态 | 最终必须记录 |
|---|---|---|
| 生产静态 `dotnet build` | **PASS · 0 errors / 6 warnings** | 已记录 |
| Editor 静态 `dotnet build` | **PASS · 0 errors / 12 warnings** | Unity 编译与最终 Console 均无产品 error |
| 完整 Unity EditMode | **PASS · 993/993，44.1991795s** | job `8e910a98b14f4fe4b4901ba78bf060dc` |
| `TinySpire/Build/Sync and Build All` | **PASS** | `G3-SyncAndBuildAll-final.log`；Addressables 子构建 22.798s |
| Addressables / Packed Play | **PASS** | 临时 `Use Existing Build` 实走双链；验收后恢复 Fast Mode |

首次全量测试暴露并已修复 i18n workbook 四个单元格漂移，因此本轮实际执行了 `Sync and Build All`。完成日志含 `TinySpire sync and local content build completed successfully`；Addressables 子构建耗时 22.798 秒。最新 `Library/com.unity.addressables/buildlayout.json` 的 `BuildError` 为空、Duration 为 23.6814477 秒、SettingsHash 为 `c75573cdbbb63d0d710c623991a6e057`，12 个 bundle 的 provider 均为 `UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider`。

生产 `persistentDataPath` 原有 2026-08-20 schema v1 `run-save.json` 为 302 bytes、SHA-256 `419058435D82A48EA08DBF3121F6127417EAC700D302388BFFFA4586DFEE54B9`。用户授权后做字节级暂移；所有验收档与 recovery artifact 已删除，原档按相同长度与 hash 恢复，临时备份名不再存在。该旧档仍按 G3 合同 typed fail-fast，没有补字段迁移或静默重掷。

## 7. 实际运行验收步骤

### 7.1 胜利路线与 Boss 门（PASS）

1. Packed Play 新建 Hero 1001 Run，地图一次显示完整拓扑、普通 Encounter 与 Boss 身份。recipe 为 profile `tinyspire.act1.g3.v1`、generator 1、map seed 288837076、fingerprint `e753b35f2678a63ab457b4e432f6025daccb546550e5591b79593a8c5cde453d`。
2. 通过实际节点按钮、卡牌拖放与 EndAction handler 完成两场普通战斗；第一次回图路径为 `L00-S00,L01-S00`、HP 17/30，第二次为 `L00-S00,L01-S00,L02-S00`、HP 15/30，fingerprint 始终不变。没有直接改 Store 或伪造 Result。
3. 当前 Boss 终点 `L03-S00/BETA` 与 `L03-S01/GAMMA` 可达；点击 `L03-S00` 后停在 RunEntry、phase=`BossGateReached`，没有 Battle、奖励或 Run 胜利。正式档路径为 `L00-S00,L01-S00,L02-S00,L03-S00`，committed/terminal 均为空。
4. 结束进程并启动新 Unity 后，经主菜单 Continue 恢复同一 BossGate phase/path/HP/fingerprint；正式档 SHA-256 `AAC1386228647A70EB5C3EB8A50DA8B7FF745F56B93AAD93731D11738C761600` 未变。随后经生产 Abandon 确认删除该验收档。

### 7.2 失败终局与冷启动（PASS）

1. 新 Run 选择 `L01-S00`，只使用实际 EndAction 按钮让敌方回合把玩家 HP `30→18→12→0`；自动返回失败页，页面只有“退出”，没有 Restart。
2. Store 为 `Terminal(Defeat)`：当前位置仍 `L00-S00`，committed 为 `L01-S00`，实际 path 仍只有 Start，失败节点未完成，HP 0/30，Continue=false，fingerprint 保持 `e19232cc237719713a5485b9cc30d7474cd8b37b4548d10771bf0d6fa92c51cd`。
3. live 与 terminal-intent 均为 532 bytes 且 SHA-256 同为 `2385F5B301ADA38AED5CFB3B65406EA382FAD9A6E6F09127D5D2D3901A081708`，tmp 不存在。结束进程并启动新 Unity 后直接恢复相同失败页/状态/hash，没有主菜单 Continue。
4. 点击生产失败页“退出”后，Run 为空、persistence=`NotFound`、Continue 禁用，live/intent/tmp 全部不存在。

所有步骤必须通过生产 UI、Flow、Store 和现有 Battle command/result seam 完成，不得直接改 RunState、伪造 BattleResult 或编辑正式 save JSON 冒充主链。

## 8. Console 证据

新 Run 地图显示、两次 Battle 往返、BossGate、BossGate 冷启动恢复、失败终局、失败页冷启动、确认删除以及最终 EditMode 后 idle 的产品 Console Error 均为 **0**；没有 `InvalidKey`、Addressables 初始化失败或 Player content build 错误。

MCP 截图取证工具曾向 `Editor-prev.log` 写入 5 条 `PlayerLoop called recursively`；调用栈指向 `com.coplaydev.unity-mcp` 的 `ScreenshotUtility.cs`。停止截图、清空 Console 后不再出现，随后上述所有产品检查点均为 0。该工具侧噪声保留在记录中，不计为产品通过证据，也未被隐去。

## 9. 范围、风险与工作区保护

- 不包含真实 Boss 战/Boss 阶段、奖励、Run 胜利、遗物实际效果/库存/次数/UI、精英、商店、事件、休息、宝箱、多人/FishNet、云/多槽、战中存档或目标平台 Player build。
- 用户已明确授权结束旧 PID 20384 与字节级暂移/恢复旧档；验收过程保持单一主 Editor，没有删除锁或清理 Library/Temp。当前主 Editor 留在 Edit Mode。
- LocalLow 测试档已删除，用户 schema v1 G2 存档已按原 hash 恢复；Addressables Play Mode 从临时 Packed index 1 恢复 Fast index 0。
- 若发现旧档可以无歧义迁移的新事实、需要新的玩法选择，或必须扩大到真实 Boss/奖励/遗物/高影响 Scene/DI，必须停止并交回用户裁决。
- 本轮不 commit、不 push、不 broad stage；不修改 `Docs/Hermes_Pegasus/design/decisions.md`，保留 package 文件与所有无关用户 WIP。
