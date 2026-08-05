---
created: 2026-07-06
updated: 2026-08-05
---

## 2026-08-05 DOTween Pro 仓库净化、NOTICE 补全与远端 LFS 阻塞（本地已完成，远端未更新）

- 从索引移除 `DOTweenPro/`、`DOTweenPro Examples/`、对应目录 Meta 与 `readme_DOTweenPro.txt`/Meta 共 46 个跟踪项，并在 `TinySpire/.gitignore` 添加六条精确规则；免费 `DOTween/` 与 `DemiLib/` 继续跟踪 307 项。新增 CD-056，并修订 CD-003，明确 Pro 只允许持证开发者本地安装，不是 Clone、构建或运行前置条件。
- `THIRD-PARTY-NOTICES.md` 已从错误的“全部 MIT”概括补为真实分类：免费 DOTween 自定义许可、Luban 及其 MIT/BSD/Apache/MPL 内含依赖、UPM/NuGet、Unity 专用许可、模板、子模块和本地专有工具；`Tools/Luban/NOTICE.md` 已指向根依赖清单。
- 本地 Pro 临时移出后，唯一 Unity 6000.5.5f1 Editor 全量重编译成功，完整 EditMode `5b817700afff40f1a4928b2e78f01a25` 为 459/459，通过独立正常 Bootstrap 进入 BattleScene、唯一 `BattleLifetimeScope` 与 Console 0 Error / 0 Warning。恢复后 50 个物理文件与备份 SHA-256 全部一致，Editor 再次编译回到 idle、Console 0 Error / 0 Warning。
- 清理提交旧 SHA `bec2f892c8f38f995046e8f11f088e0921b5c2e2` 已生成本地新历史 `3c831013046e9f5fb30097701533b66c80abeb0e`；独立镜像重放得到相同 HEAD，tip tree 与过滤前同为 `f8003ddb36b14f79fc5c2e68ddfbd0f937043887`。镜像 81 个提交中目标路径可达对象/路径提交均为 0、只有 `main`、无 Tag，`git fsck` 通过；本地完整 bundle 与逐文件备份保存在已忽略的 `.codex_work`。
- 使用旧远端 SHA 的精确 `--force-with-lease` 推送被 GitHub `GH008` 拒绝，远端 `main` 仍为 `3e7b8e5100015686a3c12260155e9b7076456a26`。缺失对象精确为本地领先两个提交中的一张 M10D 证据图、三张 Hermes scene candidates 与一张 Battle Candidates 图，均非 Pro。遵守用户“不修改 LFS”边界，没有执行 `git lfs push --all` 或上传任何对象。继续远端净化需要用户明确允许只上传这五个 LFS 对象，或另行授权改写/排除相关本地提交；完整证据见 `06_testing/2026-08-05-dotween-pro-repository-sanitization.md`。

---

## 2026-08-05 配置素材短键、构建期漂移校验与真实 AB 加载（已完成）

- 全量审计 `DataTables/Datas/*.xlsx` 后确认：已迁移的 `battle.Card.illustration_key` 之外，只有 Hero/Enemy 的 `view_prefab_address` 仍保存完整角色 Prefab 路径。两个作者表已改为 `view_prefab_key`，值为 `pfb_char_player` / `pfb_char_enemy`；Luban 已成功重生成对应 C# 与 `Assets/GameData` JSON，工作簿公式/错误扫描和最终全表 `Assets/...` 复扫均为 0。
- 精确红灯依次固定缺少 `CharacterViewAddress` 的五处 `CS0103`、生成 JSON 旧字段、角色 Group 完整路径地址、Presenter 直接转发短键和逻辑地址 `InvalidKey`。独立复核又以 `CS0117` 固定了构建期接受 inactive-only Renderer、运行时拒绝的契约漂移。最小实现后，Presenter 统一把短键转换为 `character-view/{key}` 并继续使用 `Addressables.InstantiateAsync` / `ReleaseInstance`；构建工具从 Hero/Enemy 生成表解析实际引用，拒绝短键重名、大小写漂移、缺失 Prefab 与缺少 active `SpriteRenderer`，并让 `TinySpire Characters` 与实际引用精确同步。
- 当前唯一 Unity 6000.5.5f1 Editor 的 `TinySpire/Build/Sync and Build All` 成功。最新 BuildLayout 证明两个逻辑地址由 `AssetBundleProvider` 打入同一 PackTogether 物理 bundle；同一 Editor 临时切到 `Use Existing Build` 后，运行时只出现 `AssetBundleProvider` / `BundledAssetProvider`、物理 `IAssetBundleResource` 非空，正常 Bootstrap 实际进入包含玩家和两名敌人的 BattleScene，Console 0 Error / 0 Warning。退出后已恢复 Fast Mode，AddressableAssetSettings 哈希前后一致，未保存 ProjectSettings。
- 当前完整工作区的全量 EditMode 任务 `e6c01375675b4aaabdefb289f802ca8b` 为 **459/459 passed、0 failed、0 skipped**；solution build 为 **0 error、12 条既有 warning**。该数量包含另行保留、未纳入本次素材短键提交的 M9/M10 测试改动，只作为当前交付工作区证据；本次素材边界自身的具名定向任务、相关回归和证据限制见 `06_testing/2026-08-05-config-asset-logical-keys.md`。当前规则见 CD-055、`CONTEXT.md` 与根 `AGENTS.md`。
- 旧完整路径并非直接磁盘加载：它曾被 Editor 构建工具同时设为 Addressables catalog 地址；Packed/Player 经 `BundledAssetProvider` → `AssetBundleProvider`，Fast Mode 经 `AssetDatabaseProvider`。当前修正的是配置与工程路径耦合，不是更换资源系统。Scene/GameData 基础设施地址继续使用完整 `Assets/...`；Queue/Turn/settlement、公式、战斗规则、Scene/Prefab、ProjectSettings、asmdef、HybridCLR、DI/启动和受保护 Candidates/Targeting/Hermes 路径均未改。本次提交范围继续排除 M9/M10、Candidates/Targeting/Hermes、`packages-lock.json` 与其他无关改动；未推送。

---

## 2026-08-05 M10D 交付级验证与性能基线（已完成，非 M10 套件异常已记录）

- 新增仅测试使用的 `Assets/Editor/Tests/BattleDeliveryM10DTests.cs`。先由 Unity 编译的 `CS0246`/`CS0103` 固定缺少 `M10DeliveryEvidence` 与 `M10DeliveryBaseline` 的精确红灯，再以最小非持久化夹具复用 M10C 的 `M10BattleReplayHarness.Replay(fps)`；夹具不改生产 Queue/Turn/settlement、规则、DI、Scene、Prefab 或第二份权威状态。M10D 定向 EditMode 1/1 与 M10A--M10D 聚合 25/25 均通过；`dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error、保留 12 条既有 warning。
- 唯一 Unity 6000.5.5f1 Editor 的正常 Bootstrap 已进入默认 BattleScene：Game View 显示 3/3 能量、5 张默认手牌、英雄 30/30、两个敌人各 20/20，以及部分中文卡牌与参与者文案；画面中的战斗流程英文标签不作为 zh-CN 黄金基线证据，完整双语口径由 M10B 自动测试覆盖。运行中 `BattleLifetimeScope` 为 1，停止后 BootstrapScene 中为 0，Console 无产品 warning/error。记录了 30/60/120 FPS 的各两个 5 样本 Editor 微基线，以及启用 Profiler 后的 3 帧 Game View 观察值；用户未给出帧时间、GC 或设备预算，故这些仅是环境化基线与差异，不是性能通过。M10 未改 DataTables、Localization 或可寻址内容，故未运行 Luban、Sync and Build All 或 Local Content；静态组地址仍是 `Assets/Scenes/BattleScene.unity`。
- 交付审计的完整 EditMode 共完成 451 项，其中两项失败且已独立复现：`BattleParticipantFeedbackRoutingTests.PlayCardPresentation_UsesPreludeThenEffectThenOriginalCardMovedOrder` 的第一轮 Tick 卡片中心未移动；`HandCardTargetFocusTests.TargetFocus_LateUpdate_TracksMovingCardWhilePointerStaysStill` 在读取已不存在的 `_lineRect` 测试契约时抛出 NullReferenceException。两项测试及 Targeting 源路径相对 `HEAD` 无差异；前者仅走 Localization/CardZones/Hand/Presenter/Adapter，未创建或初始化 `ConfigService`，后者无 M10 Core 依赖，M10 Core 文件亦不引用 Hand/Targeting。因此它们是如实保留的非 M10 UI/Targeting 套件异常，不伪报全绿，却不阻断 M10 的相关回归收口。M10D 已完成；完整证据、性能环境、实际/计划区分与后续单独授权边界见 `06_testing/2026-08-05-m10d-delivery-validation.md`。

---

## 2026-08-05 M10C 确定性、帧率无关与生命周期回归（已完成）

- 先以精确红灯固定三个缺口：缺少 `M10BattleReplayTrace`/`M10BattleReplayHarness`（`CS0246`/`CS0103`）、缺少加速/立即完成入口（`CS0117`）、缺少取消和重启生命周期证据（`CS0246`/`CS0117`）。最小实现仅新增测试文件 `Assets/Editor/Tests/BattleConformanceM10CTests.cs`：它经 `BattleCommandQueue.Submit` 提交既有命令，在表现完成时仅读取 `Queue`、`Turn`、`BattleSession`、`CardZones` 和既有结算记录；测试用 tracing presentation 只冻结结果文本并委托既有 adapter，不保存第二份权威状态，也不改变生产写入口或契约。
- Unity 定向任务依次为 `3d8e55f47eb04600a548996f885d80d9` **1/1 passed**、`8eef040130d048b28a37a3d12ca84c7c` **2/2 passed**、`4a6cef7ad5f64abd8403b1429a3e044f` **3/3 passed**；相关聚合任务 `ee9720d3161a473d950940fe80edc1f1` 为 **53/53 passed**。`dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 **0 error**，保留既有 12 条程序集版本冲突 warning。此前 domain refresh 后两个 MCP 测试任务仅清除了卡住的任务记录，未以其为通过依据；以上具名绿色任务才是结论证据。
- 当前唯一 Unity 6000.5.5f1 Editor 的真实正常 Bootstrap Play Mode 已从 BootstrapScene 进入 BattleScene：存在一个 `BattleLifetimeScope`，Console 只有 `game-config.json 已加载。`，无产品 Error/Warning；停止后回到 BootstrapScene，`BattleLifetimeScope` 数量为零。此切片未驱动真实 Game View 指针或 Restart 按钮，也未声称性能通过；这些交付级验证仅留给 M10D。
- 没有修改 `BattleCommandQueue`、`BattleTurnController`、结算、公式、`BattleLifetimeScope`、Scene、Prefab、DI、DataTables、生成 GameData、Localization 或 Addressables；故未运行 Luban、Sync and Build All 或 Local Content。未触碰 Candidates/Targeting 和其他受保护路径，未暂存、提交或推送。M10C 的独立停止点已完成；M10D 必须从新的交付/性能红灯开始。完整证据见 `06_testing/2026-08-05-m10c-determinism-lifecycle.md`。

## 2026-08-05 M10B Bootstrap 可见失败路由与默认内容黄金基线（已完成）

- `GameLauncher` 现在只编排启动：它只捕获 `ConfigInitializationException` 并交给 `IBootstrapFailurePresenter`，随后停止，不继续初始化 Localization 或加载首场景；未知异常保持上抛。`Bootstrap` 在现有对象上按需创建 `BootstrapFailureView`，失败只显示稳定 `CFG-001`～`CFG-007`、资源地址和修复后重启指引，不增加重试、MainMenu、Run、第二场景流或新的权威写入口。
- 新增精确红灯先暴露了缺失的 `GameLauncher.RunStartupAsync`（solution build `CS0117`）和 `LocalizationBuildTools` 缺少运行时战斗流程必需键（任务 `6e7fc222f4c94adc9bad8a534c1de2aa`）。最小实现后，`GameLauncherM10BTests` **10/10 passed**，覆盖七类 typed failure 停止、未知异常上抛、成功场景序列与失败 View 诊断文本；`BattleGoldenBaselineM10BTests` **2/2 passed**，从 DataTables 作者表、生成 GameData、i18n.xlsx 和 Unity String Table 锁定 5/3、5×Strike/4×Defend/1×Bash、6/5/8/2、30/20 和 en/zh-CN Smart String 基线。
- M10A+M10B 聚合 EditMode 任务 `7190d4bdca904d5f89104b17c21716d3` 为 **21/21 passed**；`TinySpire/Localization/Validate Battle Card Text` 已通过；`dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 **0 error**，保留既有 12 条程序集版本冲突 warning。当前唯一 Unity 6000.5.5f1 Editor 的正常 Play Mode 从 BootstrapScene 实际进入 BattleScene，Console 记录 `game-config.json 已加载。`，随后已退出。
- 未修改 DataTables、生成 JSON、Localization 或可寻址内容，因此没有运行 Luban、Localization Import、Sync and Build All 或 Local Content；这避免产生无关内容输出，不代表跳过变更后的生成验收。也没有通过篡改资源制造真实 Game View 失败截图；七类自动 typed-failure 路由与失败 View 断言是失败路径证据，其边界已记录于 `06_testing/2026-08-05-m10b-bootstrap-golden-baseline.md`。
- M10B 停止点完成；M10C 才可开始，并必须只通过 `BattleCommandQueue.Submit` 与既有只读 Queue/Turn/BattleSession/CardZones 建立确定性、帧率和生命周期红灯。未修改 Queue/Turn/settlement/公式、Scene/Prefab、战斗规则、Targeting/Candidates 或受保护路径；未暂存、提交或推送；DEP 状态不变。

## 2026-08-05 M10A 配置原子性与表清单 fail-fast（已完成）

- `ConfigService` 现在仅在八张必需表与 `game-config.json` 全部成功加载、解析并通过最小结构校验后，才一次性发布 `Tables` 和 `GameConfig`。加载失败、坏 JSON/根节点、坏表行、缺必需 game-config 字段均抛出携带稳定地址、可选表名与失败原因的 `ConfigInitializationException`；不再记录 warning 后用 `GameConfig` 默认值继续。
- 通过内部 `IConfigTextLoader` 窄 seam 建立 fake loader。M10A 的精确红灯先暴露了缺失 seam、未校验的表清单验证器和重复表名被集合去重掩盖；最小修复后，`ConfigServiceTests` 为 **7/7 passed**，`ConfigTableManifestValidatorTests` 为 **2/2 passed**。真实项目的 Luban `__tables__.xlsx`、生成 `Tables.cs`、`Assets/GameData` JSON 与运行时清单比较为 `CONFIG_TABLE_MANIFEST_OK`。
- `TinySpire/Build/Sync and Build All` 在 Luban 生成和同步 AssetDatabase 后、Localization/Local Content 前调用 `ConfigTableManifestValidator`，会阻断遗漏、额外或重复表名。未修改 `DataTables/Datas/`、`Assets/GameData/`、Localization 或可寻址内容，故本切片未运行 Luban 或 Local Content。
- 验证：当前唯一 Unity 6000.5.5f1 Editor 的定向 EditMode 回归通过，`dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 **0 error、12 条既有程序集版本冲突 warning**。完整证据见 `06_testing/2026-08-05-m10a-config-fail-fast.md`。
- M10A 停止点完成；M10B 才可接入 Bootstrap 可见失败路径和默认内容黄金基线。没有修改 Bootstrap、Scene、Prefab、战斗规则、Queue/Turn/settlement、DataTables、生成 JSON、Localization、Addressables 配置、Candidates 或 Targeting；未暂存、提交或推送。DEP 状态不变。

## 2026-08-05 M10 BattleScene MVP 对标计划与下一会话交接（计划就绪，未实施）

- 新增唯一 M10 计划 `plans/2026-08-05-m10-battlescene-conformance.md`，把路线图的“数值对标、回归、性能与内容扩展入口”拆为 M10A 配置原子 fail-fast、M10B Bootstrap 失败路由与黄金内容、M10C 确定性/帧率/生命周期回归、M10D 交付级验证与性能基线四个串行停止点。
- 计划基于当前代码观察到的 `ConfigService` 风险：八项手写 `TableNames` 未做漂移校验，`game-config.json` 失败时会回退 `GameConfig` 默认值；M10A 将先以精确测试收口，不提前改 BattleScene 或内容。
- 当前默认内容只作为待验证黄金基线：5 手、3 能量、5×Strike/4×Defend/1×Bash、6/5/8/2、英雄 30、默认敌人 20，以及 en/zh-CN 文本。若产品目标值不同，M10B 必须在修改表格前记录新的明确来源；不能把计划当作已通过的运行时证据。
- 配套 `plans/2026-08-05-m10-battlescene-conformance.codex-prompt.md` 提供可复制 `/goal` 与实施提示词。当前无 DEP 状态变化；DEP-007/008/010/011/012 继续是 M10 排除项。
- 本轮只修改 M10 计划、计划索引、路线图和状态日志；未修改 C#、测试、表格、生成 JSON、Localization、Addressables、Scene、Prefab 或受保护艺术资源，未运行 Unity、Luban、Addressables、测试或构建，未暂存、未提交、未推送。

## 2026-08-05 M9 出牌、目标箭头与锁定框反馈（已实施，定向验收通过）

> 本条是今日较早“收集反馈中 / Unity 回归待执行”两条记录的当前状态来源。

- 用户授权直接实施三项反馈：`PlayCard` Prelude 不再把牌飞向怪物；攻击箭头改为独立 head 加多段 fragment，fragment 与 head 按曲线切线朝向；怪物锁定框改为四个角围住实际怪物边界。
- `BattleTargetingArrowView` 的外部 `Show / UpdateArrow / Hide` seam 未扩大；内部用曲线采样和 fragment 池实现分段箭身。`ParticipantHudView` 对合法与悬停状态均使用四角件，按投影后的 `SpriteRenderer.bounds` 加 16 像素可调留白定位。
- `PlayCardTransientHold` 继续只管理 transient 生命周期；唯一可见卡牌位移仍是结算时的 `CardMoved(Hand -> DiscardPile)`。
- 验证：`dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error、12 条既有程序集版本冲突 warning；相关 Unity EditMode 类集为 26/26 通过、0 失败、0 跳过。详细结果见 `06_testing/2026-08-05-play-card-no-target-flight.md` 与 `06_testing/2026-08-05-m9-targeting-visual-feedback.md`。
- 未改 Targeting 源图片/Meta、Candidates 资源、DataTables、Addressables 配置、Scene、`BattleCommandQueue`、`Turn` 或结算契约；未暂存、提交或推送本次新增改动。

## 2026-08-05 · M9 目标箭头与锁定框视觉反馈（收集反馈中，未实施）

- 新增 `plans/2026-08-05-m9-targeting-visual-feedback.md` 作为连续反馈的唯一记录：攻击箭头拆分为独立箭头与多段箭身 fragment，fragment 和箭头均按路径局部切线朝向；怪物锁定框改为放在怪物视觉后方的四个角件包围，不使用完整矩形底图。
- 用户说明仍会继续反馈修改项，因此本轮只写入需求草案和索引；未修改 C#、Prefab、Scene、正式 Targeting 美术/Meta、Candidates 或任何运行时行为，未运行 Unity 验证、未暂存、未 commit/push。

## 2026-08-05 · M9 出牌不飞向怪物（实现完成，Unity 回归待 Editor 空闲）

- 用户反馈“出牌后牌会移动到怪物身上，这个不要”已记录为 `plans/2026-08-05-play-card-no-target-flight.md`。实现移除 `PlayCardToTarget` 与卡牌运动 cue 的 `TargetId`；`PlayCard` Prelude 现在只创建零时长、无位移的 `PlayCardTransientHold`，不再读取角色/怪物屏幕锚点。卡牌仍只在冻结的 `CardMoved(Hand → DiscardPile)` 自身 `Order` 飞向弃牌堆。
- 保留 M9 单一 runner、Prelude 先于 Order 0、一次 completion 与 transient 异常/取消清理。无位移 hold 和后续弃牌 cue 共享幂等 release，故后续 cue 同步构造失败仍不会遗留离手卡；未修改 Queue、Turn、settlement、CardZones、Effect、目标规则/箭头、Scene、Prefab、DataTables、Addressables 或 Candidates。
- 串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 已为 **0 error、12 条既有程序集版本冲突 warning**。检查时既有 Unity Editor 正在 `BattleScene` Play Mode 转换中，未启动第二个 Editor、未驱动用户 Game View 或运行 Test Runner；定向与全量 EditMode 验证待 Editor 空闲后执行，详见 `06_testing/2026-08-05-play-card-no-target-flight.md`。

## 2026-08-05 · M9 验收后 Hand motion 双 BUG 与临时生命 HUD 修复（已完成）

- `BUG-MOTION-001` 与 `BUG-MOTION-002` 已按 `06_testing/2026-08-05-m9-post-validation-bug-triage.md` 修复。`HandCardContainer` 继续只从当前 `CardZones.Layout` 收敛 View 与 base pose，但未被 `Draw→Hand` cue 展示过的 View 会保持隐藏；`HandCardVisual` 只在该冻结 cue 进入 runner 时显示并开始 incoming motion。普通 Layout 不再拥有可见入场运动，现有 Queue、Turn、settlement、一次 completion、取消/销毁边界保持不变。
- 两条真实 `CardZones.Layout` + container + visual + runner 的精确测试先在任务 `a63b7dfd32a74427ac0bc28f5b925bcb` 得到 **0/2 passed，2/2 failed**，修复后任务 `c7e59c0df1424678a38ba5ecebad0b25` 为 **2/2 passed**。相关 Hand/card-motion/adapter/runner 回归 `d925456056364adf9c6f10fa87cd3c2f` 为 **46/46 passed**，全量 EditMode `d40a8c5543194fa79db5ac18d5e561cb` 为 **425/425 passed**；串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error、12 条既有版本冲突 warning。全量测试后的 Console 仅有 PerformanceTesting 的 IPrebuild/IPostBuild 提示与 TestResults 写入记录，不把它们冒充本次代码问题或 0 warning。
- 用户明确后续 Battle UI 将整体重做后，`BUG-UI-001` 采用临时、可替换的头顶投影：`ParticipantHudView` 把生命/状态锚点投影到角色精灵 bounds 顶部外侧，名称再向上错开；`ParticipantHudView.prefab` 只新增名称与生命 HUD 的可调垂直间距。现有 Canvas、Scene、排序和参与者事实均未改变，后续 UI 重做可整体替换这段布局逻辑。
- 已由既有 Unity Editor 导入本次改动且 Console Error 为 0。定向 `LateUpdate_ProjectsVitalsAboveHeadAndNameAboveVitals` 为 **1/1 passed**；全量 EditMode 任务 `d50762b82f0147df82921b0e6c388c00` 为 **426/426 passed**。修改后的 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error、12 条既有版本冲突 warning。
- 在同一个 Editor 的真实 BattleScene 复测 `M9D final` 五种尺寸：`1600×700`、`1600×900`、`1600×1000`、`1600×1100`、`1600×1400` 各有 3 条实际 `HealthBar` 与 5 张 `CardContent`，每种均为 **0 个矩形相交对**。测试后已恢复 Game View 的 `1600×1100` 预设并退出本次启动的 Play Mode；没有保存 Scene 或修改 Canvas/排序。
- 已在既有 Editor 的 `1600×1100`（`16:11`）`Round 1 / PlayerAction` 静态 Game View 帧观察一次：玩家生命徽章与两名敌人上方数值/意图元素均可见，未复现 `BUG-UI-001`。该临时截图已清理，不能替代连续帧、五种宽高比或最小遮挡复现。
- `BUG-UI-001` 的五种宽高比测量仍是修复前证据：每种尺寸有 5～8 个生命条与手牌 `CardContent` 相交对。生命 HUD 位于 `BattleScene` 的 `ScreenSpaceCamera` Canvas（order `0`），手牌位于 `ScreenSpaceOverlay` Canvas（order `0`～`4`），故相交时手牌会在 HUD 之上渲染；本次不以调整 Canvas 覆盖该根因，而以移动临时 HUD 位置避开当前覆盖区域。
- 先前未保存的 Overlay/order `200` 实验仅保留为诊断证据，未写入任何资产，也不构成本次修复方案。Queue、Turn、settlement、公式、目标、终局、DI、DataTables、Localization、Addressables 配置、Candidates 或受保护的 Hermes 美术路径均不在本 BUG 的修改范围；未暂存、未 commit、未 push。

## 2026-08-05 · M9 验收后 BUG 分诊与结构审查关联（已记录，未修复）

- 新增 `06_testing/2026-08-05-m9-post-validation-bug-triage.md`，固定三个不冲突的编号：`BUG-UI-001`（生命 HUD 遮挡）、`BUG-MOTION-001`（初始手牌提前出现并重复发牌）、`BUG-MOTION-002`（受击反馈未结束时下一轮手牌抢跑）。三项均保持 reported / 未修复；用户实机报告、代码已观察、高置信推断和已验证证据分层记录，没有把静态诊断冒充 Agent 独立复现。
- 两个卡牌运动问题保留独立验收，但共同诊断为权威 Hand 唯一、可见 Hand motion 却由普通 Layout/base-pose Tween 与 M9 正式 cue 双重拥有；当前证据不支持修改 Queue 或增加栅栏。精确红灯、最小 concrete UI 范围、禁止全局输入锁/第二队列/事实镜像及契约扩张停止点均已写明。
- 用户确认此前 Claude 类似审查是 `07_retrospective/2026-08-01-m5-architecture-roast.md`：它是 M6 阶段产出的 M0～M5 历史架构审查。新分诊把其 §2.4/§2.7/§2.8 风险预警、M6D 的谨慎分流，与现有 `plans/2026-08-05-m9-code-structure-review.md` 的 A2/C1 建议并列关联；两份原文均未被改写，完整 Container 拆分仍是独立架构工作。
- 支持性 EditMode 任务 `4abbc7e83d2b4a58882570f0e94554b9` 为 **3/3 passed**，只证明 runner 顺序、敌人结算和重洗/抽牌事实，不覆盖真实 Layout subscription 与反馈 cue 的并行可见时间线。当前只改文档索引与日志，未改代码、Prefab、Scene、配置或资源，未运行修复后回归，也未 commit/push；`packages-lock.json`、`.codex_work` 与 Hermes/Candidates 用户改动持续排除。

## 2026-08-05 · M9G 全量验证与双轴收口（已完成）

- Standards 首轮发现 PlayCard Prelude 已脱离手牌后若后续 cue factory 同步抛错，transient lease 可能没有被 runner 接管；新增精确红灯任务 `1c2d50ad7851429c8703704859b79771` 后，让 Prelude 与 Hand→Discard 共享幂等释放边界。单项 **1/1**、相关 **24/24**、M9 定向 **160/160**、M2～M8 回归 **262/262**、全量 EditMode **423/423** 均通过；串行 solution build 0 error、12 条既有依赖 warning。
- 最终 Local Content 成功，`catalog.hash=0f333c04c6f20921aab45e7c6bf9e827`，BattleScene 保持完整稳定地址。唯一 Unity Editor 从 Bootstrap 进入 BattleScene 后停止回到 Bootstrap，Console Error/Warning **0/0**。真实系统指针、连续帧与只读事实覆盖五种宽高比、出牌/多轮卡区、胜利、失败、两次同 `1001:5001:5` 重开、旧终局输入、立即完成与场景销毁取消。
- 用户授权后仅临时关闭当前 Editor 内存中的 HybridCLR 并恢复原值，以 Unity 内置 IL2CPP 构建仓库外 Development Player；任务 `build-5c4c9005fe` 0 error。PID `45720` 经真实 End Action 进入失败，再以同一可见 Exit 按钮自然结束：`ExitCode=0`、PID 消失、`ForceKillUsed=false`。首次 Windows Firewall 提示只点击“取消”，未授予策略；Player log 无加载/未处理异常，但有两行同一 Development JobTempAlloc 警告。
- Standards 另一项首轮 finding 是缺少长期 M9 决策，已新增 CD-049；末轮 Standards 与 Spec 均为 **0 Hard / 0 Judgement**。最终验收见 `06_testing/2026-08-02-m9g-full-validation-review.md`；M9G、M3E 与 ROADMAP M9 已完成，唯一计划、Goal 与启动 Prompt 已归档。
- Player 构建产生的四个 ProjectSettings/Settings 序列化噪声及两份 PerformanceTest 文件已按精确目标恢复，最终无相关 diff。`packages-lock.json` 与 Hermes/Candidates 用户改动持续排除，未暂存、未 commit、未 push。

## 2026-08-04 · M9F 阶段横幅、胜负面板、重开与退出（已完成）

- 新增 concrete `BattleFlowFeedbackTweenFactory` 并深化既有 adapter/Turn HUD：StartBattle 覆盖层作为唯一 Prelude 严格先于 settlement；玩家/敌人横幅只在 phase 真正变化时播放。BattleEnded 末端只临时调用同程序集 internal `BattleTerminalRules` 并立即映射文案，不公开/注册规则、不保存 outcome，也没有第二 completion、事件总线或动画队列。
- 胜负面板只在数字、抖动、死亡与隐藏反馈全部结束后稳定显示；终局战斗输入、Restart/Exit 和 StartBattle 指针锁均为局部表现状态。连续重开两次均经 Loading 创建新 Session/Queue/HUD，authority/HP/Intent RNG/CardZones 重置且 Inspector `1001:5001:5` 不变，无旧订阅/Tween/HUD 残留；Editor Exit 经实际 InputSystemUIInputModule/EventSystem 按钮链命中一次，Editor no-op 未冒充 OS 退出。
- `DataTables/Datas/i18n.xlsx` 只新增 Battle Start、Player/Enemy Turn、Victory/Defeat、Restart/Exit 七个正式 en/zh-CN key；Luban、Localization 同步与 `TinySpire/Build/Sync and Build All` 完成，生成范围只含对应 Localization/Addressables。聚焦 **7/7**、M9F 定向与相关回归 **111/111**、Localization **7/7** 均通过；串行 solution build 0 error、12 条既有依赖 warning。
- 用户确认本次验收 Player 可不使用热更新。Editor 内存临时 `HybridCLRSettings.enable=false` 后使用 Unity 内置 IL2CPP 构建仓库外 Development Player，构建任务 `build-79a93a95b7` 为 0 error，磁盘 HybridCLRSettings SHA 保持 `22BD4714FC1BC8B093457FFFE2818D99AB733BF45374BCE1E81CBE8DC86F1FE8`，内存/环境已恢复；ignored stripped AOT cache 被包 preprocess 失效，未无快照猜测恢复或清理。
- 外部 Player PID `43692` 在 `1600×900` 下经 Windows `SendInput` 三次实际 End Action 进入失败；同一可见 Exit 按钮的 Move/Down/Up 均返回 1。原生进程句柄确认 `WaitForSingleObject=0`、`GetExitCodeProcess=true`、`ExitCode=0`，PID 消失且无强杀路径。Player log 无 InvalidKey/VContainer/Addressables/未处理异常并正常 shutdown，但保留一条 Development JobTempAlloc 警告，未伪称零 warning。
- M9F 未修改 Queue、Turn、settlement、公式、目标/状态/终局规则、BattleScene、BattleLifetimeScope、GameData 战斗 JSON、ProjectSettings、asmdef、HybridCLR 磁盘设置或启动/DI/Run/MainMenu；`packages-lock.json` 与 Hermes/Candidates 用户改动持续排除，未暂存、未 commit、未 push。验收见 `06_testing/2026-08-02-m9f-turn-terminal-restart-exit.md`；M9F 停止点完成，下一步只进入 M9G。

## 2026-08-03 · M9E 出牌、弃牌、抽牌与重洗运动（已完成）

- 新增 concrete `BattleCardMotionTweenFactory`，并让既有 adapter 把 PlayCard Prelude、`CardMoved` 与 `CardsReshuffled` 交给 M9A 同一 runner；Prelude 后 settlement 仍严格按 Order，未新增 completion、表现屏障、事件总线或动画队列，也未按卡名、模板 ID、EffectType 复制规则。
- Hand 在权威 Layout 发布后先把离手卡移出可交互集合并关闭 raycast/pending/targeting，再复用为非交互 transient；Draw→Hand 只移动当前权威 Hand View，pile HUD 只显示一个非交互纯字符 `↻`。完成、立即完成、取消、owner/Scene 销毁均清除租约/Tween/ghost 并以最新 Layout/base pose 收口，无迟到 completion。
- 用户授权的 InputSystem/EventSystem 跨帧输入链真实覆盖 Strike/Defend/Bash；默认牌组缺少 Strength，因此用仅存在于 Play Mode 的现有模板 3001 夹具加载正式 Addressable 牌面后完成第四张卡：Strength `0→3`、Energy `3→3`、Hand `5→4`、Discard `5→6`，释放帧 Queue waiting、transient 1，最终 idle/fault none/transient 0。夹具随 Session 销毁，未改配置或文件。
- 真实 End Action、多牌 ghost、EnemyAction 无旧交互手牌、下一轮抽牌与重洗顺序均通过；ghost/`↻` 射线不命中且不能提交。incoming A 的真实 BeginDrag 只让目标 cue token `0→1` 并恢复最新 base pose，另一合法 B 在 A incoming 时仍可完整拖拽且 token `0→0`，权威 Hand/Energy/Queue 不变；证据 harness 的临时 `timeScale=0` 已恢复为 1。
- 最新 M9E 聚焦 **88/88 passed**（任务 `cf327d4aeb0e4ff0b9614bc3d00aa236`），CardZones、Effect Queue、Hand/transition、Pile HUD 与 M7/M8 stage-record/Queue 相关回归 **166/166 passed**（任务 `01ae9015550d4e2b90be7bd991f14124`）；串行 solution build 0 error、12 条既有依赖 warning。最终 Local Content 构建成功、耗时 8.88s；干净 Bootstrap 为 PlayerAction/Queue idle/fault none、Hand=Views `[1,10,7,6,2]`、transient 0，Console Error/Warning 0/0。
- M9E 未修改 Queue、Turn、settlement、公式、目标/终局规则、Scene、Prefab、DI、DataTables、Localization、GameData、ProjectSettings、asmdef 或 HybridCLR；`packages-lock.json` 与 Hermes/Candidates 用户改动持续排除，未暂存、未 commit、未 push。验收见 `06_testing/2026-08-02-m9e-card-zone-motion.md`；`DEP-004` 已 resolved，停止点完成，下一步只进入 M9F。

## 2026-08-03 · M9D 不可用样式、目标聚焦与正式目标素材（已完成）

- Hand 从现有规则、阶段、能量、pending/fault 与 readiness 即时派生 Disabled/VisualOnly/Playable；Enemy 首次越线后进入序列化 focus anchor，归零、缩放/呼吸，箭头起点逐帧跟随。四张既有 Runtime/Targeting 正式 Sprite 已接入箭身、箭头和左右 Legal/Hovered 高亮，文件本身无 diff，未使用 Candidates。
- 16:7 首轮 tight bounds 与左敌世界 Sprite 约重叠 1.9 px，未判通过；只把 `BattleHandUI.prefab` anchor 从 `(0,-40)` 改为 `(-8,-40)`。最终 1600×700/900/1000/1100/1400 均取得三帧连续事实，箭头起点/卡中心与终点/指针事实 delta 均为 0，聚焦卡在屏内且与参与者 tight mesh / 活动 HUD Graphics 无交叠。
- 用户授权的 InputSystem/EventSystem 跨帧注入完整经过当前 `InputSystemUIInputModule`、EventSystem raycast 与 BeginDrag/Drag/EndDrag，不是 OS 物理鼠标，也没有直接调用 listener/Container。Self、左右 Enemy、空白/玩家/死亡目标、VisualOnly、BattleEnded Disabled 与真实 End Action 均通过；表现屏障期间另一张合法卡仍可 raycast，未引入全局输入锁。
- 队首普通失败/fault 清理由自动测试覆盖；对象/Scene 销毁实跑确认旧 card、arrow、高亮与 focus transition/breath 全部清理，随后可重建新 BattleScene。截图只作画面佐证，时序以连续 frame 与 Combatants/Energy/CardZones/Turn/Queue 只读快照为准；M9F outcome 面板尚未出现。
- 最终 M9D 合并回归 **98/98 passed**（任务 `5de9234f03b24c629ea650747a6cf21b`），Canvas 缩放测试修正后单项 **1/1 passed**（任务 `dc4fc8ed05434fd0890bf21ca5fe076f`）；串行 solution build 0 error、12 条既有依赖 warning。Prefab 最终修订后重建 Local Content，catalog 时间 `2026-08-03 10:19:50 +08:00`；Bootstrap 生产链与 Console Error/Warning 0/0。
- M9D 未修改 Queue、Turn、settlement、公式、目标/终局规则、Scene、DI、DataTables、Localization、GameData、ProjectSettings、asmdef 或 HybridCLR；`packages-lock.json` 与 Hermes/Candidates 用户改动持续排除，未暂存、未 commit、未 push。验收见 `06_testing/2026-08-02-m9d-card-focus-targeting-feedback.md`；`DEP-003` 已 resolved，停止点完成，下一步只进入 M9E。

## 2026-08-03 · M9C 结算反馈、受击与死亡过渡（已完成）

- 新增 concrete `BattleCombatFeedbackTweenFactory`、纯字符 `BattleFloatingNumberView` 与 Participant HUD `FeedbackAnchor`；冻结 Damage/Block/Attribute/Status/Intent 步骤按 M9A 顺序精确路由，完成 Block/Health/BlockGained 数字、Strength/Vulnerable/Intent 脉冲、实际生命损失抖动和 fatal 死亡过渡。用户确认不接伤害底板，未使用 Candidates。
- fatal 完成前保留 0 HP world View/完整 HUD，完成后只隐藏对应表现对象；重新绑定死亡参与者直接恢复终态，权威 Combatant/Encounter/Intent/Turn/outcome 不变。M9C 不消费 CardMoved、横幅或 BattleOutcome，胜负面板仍未出现。
- `BattleParticipantPresenter` 从当前 Session 与唯一 world View/HUD 映射即时派生 readiness；映射未齐时仅 Turn HUD 与 Hand 系统指针入口关闭，直接 Queue seam、排序与 completion 契约不变。失败、部分加载、对象/Scene 销毁和迟到完成均幂等清理，无事件总线、第二 completion 或事实镜像。
- 复审补齐 DOTween 实际所有权：HandCardVisual 默认 AutoKill tween 以 `Complete(false)` 同帧回收；命令父 Sequence 使用播放级私有 ID，在自然结束、立即完成、构建异常与 Dispose 精确 Kill。红灯分别证明同帧 Hand 与 runner 各残留 1 个 Tween；最终 Runner 12/12，统一测试后 `active=0 / playing=0`。
- 最终 M9C 聚焦 **96/96 passed**（任务 `1edb43696c294fd6aef3cddb7d9cd886`），M9A～M9C 与相关回归 **239/239 passed**（任务 `aea498d7fb544681ba3c5a810ca85656`），M8B Queue fault/lifecycle **11/11 passed**（任务 `2ae8e6a13a3d4094ba9ee552a9ca65c2`）；串行 solution build 0 error、12 条既有依赖 warning。
- 最终 Local Content 已重建；Bootstrap 进入 BattleScene 后 `ready=True / views=3 / huds=3 / endAction=True / PlayerAction / Queue idle / fault=False / Tween=0`，Console Error/Warning 0/0，退出 Play 后恢复 BootstrapScene。用户授权的 InputSystem/EventSystem 跨帧注入及连续事实证明飘字、抖动、fatal 和 readiness 时序；未冒充 OS 物理鼠标。
- M9C 未修改 Queue、Turn、settlement、公式、目标、终局、Scene、DI、DataTables、Localization 或生成战斗内容；受保护 Hermes/Candidates 持续排除，未 commit、未 push。验收见 `06_testing/2026-08-02-m9c-settlement-combat-feedback-death.md`；停止点完成，下一步只进入 M9D。

## 2026-08-02 · M9B 参与者状态、Block 与既有意图 HUD（已完成）

- `ParticipantHudView` 现从当前 Combatant 的 Health、Block、Strength、Vulnerable 与存活事实即时派生状态行；零值逐槽隐藏、全零/死亡整行隐藏。敌人意图继续由当前 BehaviorId、静态 Effect 与共享公式派生，没有 HUD / Intent / Combatant 镜像或随机推进。
- 既有 `ParticipantHudView.prefab` 通过当前唯一 Unity MCP 静态接入正式 Block、Strength、Vulnerable 图标与层数；状态行默认隐藏、非交互，生命 HUD 保持独立。没有 Weak / Poison 节点或生产分支，也未接入 Candidates。
- 公开 production `Bind` 测试证明玩家状态增减/清零/衰减、敌人死亡与死亡重建、Bind 后 locale 重投影，以及 0 HP Health HUD / 世界 View 保留；同一权威参与者、Intent Layout、BehaviorId 与 RNG 均未被删除或替换。三敌不同状态事实保持隔离。
- 最终 Participant / Prefab / View / Intent HUD 为 **17/17 passed**（任务 `67f758d8702b40289fad2d27004dbb68`）；Combatants、StatusTiming、Effect、Intent、M8D terminal/enemy loop 与 targeting 相关回归为 **130/130 passed**（任务 `3df95152b461404c9ee8c5a450c7540c`），均为 0 failed、0 skipped。串行 solution build 0 error、12 条既有依赖 warning。
- 最终 Prefab 修订后重建 Local Content，catalog 时间为 `2026-08-02 19:15:12`。Bootstrap 生产链进入 BattleScene，初始零状态行隐藏、两名敌人意图可见、玩家与敌人生命 HUD 正常；证据为 `TinySpire/Temp/CodexEvidence/m9b_final_initial_status.png`，Console Error / Warning 为 0/0。
- M9B 未修改 Queue、Turn、settlement、Combatant/Intent/Effect/公式/状态时机、目标、终局、Scene、DI、配置、Localization 或生成战斗内容；死亡世界 View 的最终隐藏严格留给 M9C。受保护用户改动持续排除，未 commit、未 push。验收见 `06_testing/2026-08-02-m9b-combatant-status-hud.md`；M9B 停止点完成，下一步只进入 M9C。

## 2026-08-02 · M9A 有序表现时间线、一次 completion 与取消（已完成）

- 新增不可变 `BattleCommandPresentationPlan` 与 concrete `BattleCommandPresentationRunner`，并深化既有 `BattleCommandPresentationAdapter`；Queue-facing `IBattleCommandPresentation.Present(result, completion)`、Queue / Turn / settlement、continuation、屏障与 fault seam 均保持不变，没有新增第二 completion、事件总线或动画命令队列。
- 当前 14 类 concrete settlement 均被显式映射为零到多个稳定步骤；StartBattle、Strike、Bash、唯一 Hand→Discard、首个可见 Effect target、BattleEnded 尾序、三层只读集合与后置记录不重排均有自动证据。每条命令至多一个互斥 Prelude，随后严格保留 settlement `Order`。
- 零可见结果同步直通；正常、加速、立即完成与 completion 重入均精确完成一次。runner 显式拥有父 Sequence 与幂等 cue lease，证明自然结束、立即完成、构建异常和 owner 销毁的清理与无迟到 completion；表现期间仍允许既有合法命令提交并由 Queue 排序。
- 最终 Plan / Runner / Adapter、settlement contract 与 M8B / M8D 聚焦为 **83/83 passed**（任务 `f3703ba76c4e4d8d9472f27215a32d81`）；完整 Queue / M8B / M8D / Effect Queue 回归为 **57/57 passed**（任务 `c64fce57df5c4d55812e2a7c3efce75e`），均为 0 failed、0 skipped。串行 solution build 为 0 error、12 条既有依赖 warning；`git diff --check` 通过，最终 Console Error / Warning 为 0/0。
- M9A 未修改 Prefab、可寻址依赖、DataTables 或 Localization，因此 Local Content、Luban 与同步工具不适用且未运行；未提前声明 Bootstrap、真实 Game View 或物理动画时序通过。受保护用户改动持续排除，未 commit、未 push。验收见 `06_testing/2026-08-02-m9a-ordered-presentation-timeline.md`；M9A 停止点完成，下一步只进入 M9B。

## 2026-08-02 · M9 总计划、Goal 与 Prompt 边界（待实施）

- M8 已按用户授权仅暂存显式 M8 路径并本地提交为 `6545640963e3f184bcd7915706e87bea4a142afa`（`feat(Battle): 完成 M8 敌人行动与战斗循环`），未 push；Hermes/Candidates 用户美术未纳入提交、未修改或回退。
- 新增 `plans/2026-08-02-m9-sts-feedback-outcome-restart.md` 作为 M9 唯一实施计划，按 M9A 有序表现时间线 → M9B Block/状态 HUD → M9C 数字/抖动/死亡 → M9D 聚焦/目标素材 → M9E 卡区运动 → M9F 战斗开始/回合横幅/终局/重开/退出 → M9G 全量验证与双轴复审串行推进；每个切片必须形成独立验收页并完成文档停止点后再继续。
- 计划保持 Queue/Turn/settlement/公式/目标/终局不变，只深化既有 `IBattleCommandPresentation` 与 concrete adapter；每命令最多一个互斥 StartBattle/PlayCard `CommandPrelude`，之后 settlement 步骤严格按 Order。常驻 HUD 读取当前事实，一次性反馈读取冻结结果，transient card View 不成为假手牌，场景销毁不得留下迟到 Tween/completion；入场卡被合法拖拽时只快进目标卡的 cue。表现期间仍允许既有合法命令提交并由 Queue 排序。
- M9 默认产品口径已锁定：重开同一 BattleScene 与 Inspector seed；退出应用而非新增 MainMenu，并以 Editor 接线加仓库外 Development Player 进程退出形成证据；胜负仅由同程序集表现 adapter 临时调用 internal `BattleTerminalRules` 派生，不公开 seam 或保存 outcome；战斗开始及玩家/敌人回合共使用七个正式 Unity Localization key；缺失 enemy banner/胜负装饰先用现有横幅 tint 与功能性 UGUI；Runtime/Targeting 正式素材只在 M9D 接入，当前 Hermes/Candidates 继续排除。
- 配套新增独立 `plans/2026-08-02-m9-sts-feedback-outcome-restart.goal.md` 与 `.codex-prompt.md`，供新任务分别复制 `/goal` 和开工指令。本轮只写规划/交接文档并同步 ROADMAP、DEPENDENCIES、计划索引和状态源，没有实施 M9 代码、测试、Prefab、Localization 或 Addressables，也未 commit/push 这些 M9 文档。

## 2026-08-02 · M8E 全量验证、双轴复审与 M8 收口（已完成）

- M8A～M8E 已按唯一计划串行完成：Queue 唯一拥有 Queued、非重入 drain、continuation、表现屏障与 fault；玩家/敌人状态时机、ordered enemy Effect、下一意图、多敌稳定顺序、死亡跳过/中止和 `BattleEnded` 已接入同一权威循环。
- 最终 M8 定向 **84/84**（任务 `3a5af905f4b1434ea4397c2f78a4555a`）、M2～M7 相关回归 **200/200**（任务 `6bc09fcecf4f48e89b93d6fba205dbf4`）、审查修正聚焦 **86/86**（任务 `4d51ecf7ceba4a9ebcb69e2d0cca3879`）与最终全量 EditMode **285/285**（任务 `63967ec19cf64333921c72ea27293f67`）均为 0 failed、0 skipped；串行 solution build 0 error、12 条既有依赖 warning。
- Bootstrap 真实 Game View 已完成四轮物理胜利、Encounter 启动时跳过已死亡敌人、玩家死亡中止剩余敌人、一次性表现屏障暂停/恢复、状态时机、最后敌人死亡立即终局及终局后稳定失败；排队后 source 才死亡的 source-only skip 由专用自动测试证明。Standards 修正后又经生产 End listener 短 sanity 进入 Round 2，最终 Console Error/Warning 为 0/0。
- Standards 首轮 **1 Hard / 2 Judgement** 已关闭：旧意图写入口与 enemy target/terminal helper 收窄为 internal，联合事务只保留一个 guard；最终复核 **0 Hard / 0 Judgement**。Spec 首轮唯一 Hard 是 M8E 文档尚未收口；生产规格与 scope finding 为 0，最终文档同步后复核 **0 Hard / 0 Judgement**。
- M8 未修改配置、生成内容、Localization、Addressables、Scene/Prefab、ProjectSettings、asmdef、HybridCLR、启动或 DI 架构，也未实现 M3E/M9 表现与其他明确排除能力；无需 Luban/Addressables 重建。M8E 收口当时尚未 commit；随后已按用户授权以显式路径本地提交为 `6545640963e3f184bcd7915706e87bea4a142afa`，未 push，用户 Hermes/Candidates 美术持续排除并保护。
- `DEP-009` 与 `DEP-013` 已 resolved；其余 M9、多人、网络、Exhaust 与 Run 依赖保持原状态。计划已归档，最终证据见 `06_testing/2026-08-02-m8e-full-validation-review.md`，下一阶段为 M9。

## 2026-08-02 · M8D 生产状态时机、死亡与完整战斗循环（已完成）

- 生产 Queue 已接入 M8C 联合敌人事务；玩家 RoundStart 为 Block → Energy → Draw，EndPlayerAction 为 Discard → Vulnerable，敌人为 Block → ordered Effect → Vulnerable → Intent。Queue 只合并连续 settlement、派生 terminal 与排 frozen continuation，没有在命令分支复制 Behavior/Effect/公式。
- 双敌严格按 Encounter 顺序；死亡 source 只产出 source-only skip。当前敌人致死玩家后仍完成本次 Intent commit，再进入 `BattleEnded` 且不排剩余敌人；玩家击杀最后敌人时同一出牌命令直接终局。普通失败零写入/空 settlement，direct fault 为 `partial=false`，提交后未预期异常才为 `partial=true`。
- M8D 定向 **11/11**（任务 `c043935ab8f64ff2b95ea6631e77044c`）、M8D 加旧阶段重洗聚焦 **12/12**（任务 `d96ef64e291a4171ae77f06e83400c24`）、最终全量 EditMode **285/285**（任务 `b07b41b753a24865b50b73fb652be332`）均为 0 failed、0 skipped；串行 solution build 0 error、12 条既有 warning，`git diff --check` 通过。
- Bootstrap 真实系统指针完成四轮胜利：玩家残余 Block 在下一 RoundStart 清零，敌人旧 5 Block 在自身 attack 前清零，Bash 的 Vulnerable `2 → 1`，易伤 Strike 为 9 点，最后敌人死亡立即 `BattleEnded`。终局后物理 End 点击不推进，Console Error/Warning 为 0/0。
- 独立致死路线让玩家以 5 HP 进入 Round 4；首敌 attack 致死后，剩余敌人保持 `20 HP / Behavior 7003`，Intent RNG `853394020` 前后不变。一次性只读屏障探针还锁定 `current=3 / CompleteEnemyAction / pending=1 / waiting=true`：首敌伤害已提交，次敌事实/意图未执行；恢复后才进入下一轮。
- M8D 未修改配置/生成内容、Localization、Addressables、Scene/Prefab、ProjectSettings、asmdef、HybridCLR、启动或 DI 架构，也未实现 M3E/M9 表现或其他排除能力。决策见 CD-047，验收见 `06_testing/2026-08-02-m8d-status-death-battle-loop.md`；下一步严格进入 M8E。

## 2026-08-02 · M8C 敌人 Effect、状态投影与下一意图联合事务（已完成）

- Effect 核心已改为 source + 显式 target + ordered `BattleEffectId`；Card 只在 `BattleTurnController` 边缘把合法 `CardEffectBinding` 保序适配为 ID，null/零/负绑定经公开 Queue 在能量、状态、卡区与 Turn 首写前稳定失败。敌人事务没有伪造 Card binding，也没有复制 M7 公式或状态写链。
- `BattleEnemyIntentsData` 已落地三段式 completion plan：Prepare 使用复制 history 与恢复到同一权威 state 的本地 RNG，固定单候选不推进随机；Validate 只允许一次，Commit 只允许一次并按 history → random → Layout 发布下一意图记录。
- internal `BattleEnemyActionExecutor` 以同一初始 source/target/Turn/Intent 快照联合预构建 Block 清理投影、Effect、Effect 后 Vulnerable、下一 Intent/random 与 continuation；唯一校验后按 Block → Effect → Vulnerable → Intent 无普通失败提交。Self defend 从 Block=0 得到最终 5，attack 复用 Strength/Vulnerable/Block/致死公式。
- 死亡 source 在读取 Behavior、目标、Effect 或 Intent 前 source-only skip；活 source 的零玩家 terminal、多玩家 fault，以及缺配置、未知枚举、无下一意图、序号容量和 prepared 漂移均为首次写入前空结算零写入。阶段、无效敌人和非当前行动者保持普通失败。
- M8C 最终定向 **25/25**（任务 `93fb4cb0fd384ea6a4acec931616ae27`）、Effect/Intent/Card/Queue 相关 **200/200**（任务 `9ee5346a6ecd4ea08712d01af8a9aa5b`）均为 0 failed、0 skipped；串行 solution build 0 error、12 条既有 warning，`git diff --check`、接口与排除路径审计通过。
- 本切片只交付纯 module/fixture，没有把 executor 注册进 Queue/LifetimeScope 或生产循环；不要求 Bootstrap/真实 Game View，生产敌人仍保持 M5 占位。未改配置/生成内容、Localization、Addressables、Scene/Prefab、ProjectSettings、asmdef、HybridCLR、启动或 DI 架构。决策见 CD-046，验收见 `06_testing/2026-08-02-m8c-enemy-effect-transaction.md`；下一步严格进入 M8D。

## 2026-08-02 · M8B 统一提交、Queue 生命周期与阶段屏障（已完成）

- 生产 `BattleCommandQueue` 现唯一持有调度 core、权威序号、Queued、非重入 drain、continuation FIFO、一次性 system token、按非空结算形成的单次表现屏障与冻结 fault。统一 coordinator 只在 Submit 前为同一命令引用预注册 opaque handle 并转发生命周期；Hand/Turn 不再保存序号或手工发布 Queued，只按精确 handle 清理当前 Failed/Completed/Faulted。
- continuation 在 Execute 返回后、Present 前入队，既有 accepted、continuation 与表现期间新提交的顺序已由公开 seam 锁定。每条命令只聚合一次前后 Turn 的 `BattlePhaseChanged`；普通失败为空结算且零 presentation，零结算 system continuation 直通；同步 completion 使用缓存/arm 边界，表现抛错仍会冻结当前 fault，旧 completion 无效。
- Runtime driver 已移除 `ITickable` 轮询，只在启动时预注册并提交唯一 Start。`NoLegalNextIntent` 通过 typed fault 稳定分类为首次写入前零写入；该桥接在 M8C 将由 intent 三段式联合事务取代。生产敌人此时仍只推进 M5 占位意图，不执行 Effect、Block/Vulnerable 时机、死亡或终局。
- M8B 定向 **11/11**（任务 `e58b73dbf30146af9c3c872452b480f8`）、相关回归 **86/86**（任务 `9ff3cfac1fd04c8985225a8fab372f8d`）、最终全量 EditMode **240/240**（任务 `4641e50e1b1b4f089997571a76d23a8f`）均为 0 failed、0 skipped；串行 solution build 0 error、12 条既有依赖 warning，`git diff --check` 与排除路径审计通过。
- Bootstrap 真实输入完成：物理结束行动 Round 1 → 2、Round 2 物理双击只产生一次 End 并进入 Round 3、真实拖放 Self 卡使能量 `3 → 2`、手牌 `5 → 4`、弃牌 `0 → 1`；生产状态最终为 `Completed #8 · PlayCard`，运行期 Console Error/Warning 为 0/0，随后正常退出 Play Mode。证据见 `TinySpire/Temp/CodexEvidence/m8b_*.png`。
- 本切片只对 `BattleLifetimeScope` 做 coordinator 注册和 runtime polling 移除；未修改配置/生成内容、Localization、Addressables、Scene/Prefab、ProjectSettings、asmdef、HybridCLR、启动/Run/网络或 DI 架构。并发的 Hermes/Candidates 美术始终排除。决策见 CD-045，验收见 `06_testing/2026-08-02-m8b-command-lifecycle-presentation-barrier.md`；下一步严格进入只交付纯 module/fixture 的 M8C。

## 2026-08-02 · M8A 命令、状态与终局契约（已完成）

- M8 Goal 实际起始 HEAD 为 `937b6fe50ec890cb3e71048da13a67c9d6815067`，开始时 `git status --short` 为空。实施期间新增的 `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates*` 为用户并发未跟踪美术，已逐次从 diff、测试与范围结论中排除并保持未触碰。
- M8A 建立 opaque handle/coordinator、internal scheduling core、生命周期/fault、六类新 settlement、中立 `BattleEnded`、敌人 Self/唯一存活玩家目标、死亡 source-only skip、四个状态时点与派生 terminal。外部伪造 system command、错配 handle 与 fault 后提交均无序号拒绝；continuation 由 Queue token 授权一次，非空 settlement 自动建立精确 completion 屏障，提交后表现异常进入明确的可能部分写入 fault。
- 敌人联合初始快照真实冻结 source/target 标量、完整 Turn、Intent Layout/history/random、恰好一个 ordered EffectId 与 continuation；只允许一次 validate/commit，commit 不复验中间写入。状态投影复用现有标量快照，Self defend 契约从清理后的 Block=0 开始，Effect 后 Vulnerable 减 1；没有伪造 CardEffectBinding、复制公式或新增 outcome/目标镜像。
- 最终 M8A 定向 EditMode **58/58** 通过（任务 `d0ba59205b67451c97a895f99afb6a28`），M4～M7 契约回归 **145/145** 通过（任务 `940eaf0766564474b95e04800ab257cd`），均为 0 failed、0 skipped；串行 solution build 0 error、12 条既有依赖 warning，最终 Unity Console Error 0，`git diff --check` 与新增 authored C# 尾随空白检查通过。
- 两轴只读复审最终均为 **0 Hard**。Spec 保留 1 条明确 judgement：M8A 的玩家 Block → Energy → Draw、Discard → Vulnerable 只是纯 settlement 顺序口径，尚未接生产；M8D 必须用公开 Queue 的真实结算顺序测试替代。Editor friend access 只服务 M8 internal contract tests，M8E 强制复审并尽可能删除，决策见 CD-043～044。
- M8A 没有迁移现有 Queue、View 手工 Queued/sequence、pending、runtime polling 或自动阶段，生产敌人仍不造成伤害；未改配置/生成内容、Localization、Addressables、Scene/Prefab、ProjectSettings、asmdef、HybridCLR、启动/Run/网络、DI 或 `BattleLifetimeScope`，无需 Luban/Addressables 重建。验收见 `06_testing/2026-08-02-m8a-command-status-terminal-contract.md`；下一步严格进入 M8B。

## 2026-08-02 · M8 总计划与 Goal 边界（待实施）

- 新增 `plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md` 作为 M8 唯一实施计划，按 M8A 契约 → M8B 提交/Queue 生命周期与屏障 → M8C 敌人 Effect 联合事务 module → M8D 生产接线/状态/死亡/完整循环 → M8E 全量验证与双轴复审串行推进；每个切片必须形成独立验收页并完成文档停止点后再继续。
- 计划锁定当前单玩家 MVP：敌人 Self 指向自身、Enemy 只允许唯一存活玩家；玩家 Block 在下一 PlayerRoundStart 清除，敌人 Block 在自身行动前清除，玩家 Vulnerable 在弃手后减 1，敌人 Vulnerable 在 Effect 后减 1；死亡敌人不执行状态时机/Effect/意图推进，玩家死亡中止剩余敌人，终局从存活事实派生。
- M8 将保持 `BattleCommandQueue.Submit`、只读 `Queue`/`Turn` 与 M7 共享 Effect；coordinator 在 Submit 前预注册 token/handle，Queue 唯一拥有 Queued、非重入 drain、`Execute` 后/`Present` 前的 continuation 排序、一次性 system token、显式 fault 与按结果表现屏障。普通失败继续零写入且结算为空；多玩家目标、敌人多 Effect、配置/可寻址内容或其他排除路径需要扩大范围时必须停止确认。
- 本轮只新增/同步计划、计划索引、ROADMAP、依赖来源与状态源，没有修改 C#、测试、配置、生成内容或 Unity 资产，也没有运行 M8 EditMode、build、Bootstrap、Game View、Luban 或 Addressables。规划前的干净代码/资源基线为 `c46950ff4026f383487b1b2c15755b60ae2b2c3d`；正式 M8 Goal 必须现场记录实际起始 HEAD 与全部 tracked/untracked 工作区，本计划本身属于受保护基线。
- 计划候选经压缩后为 112 行，并完成只读复核：Standards **0 Hard / 0 Judgement**、Spec **0 Hard / 0 Judgement**、深 module/原子性/continuation 接口审计剩余 finding **0**；本地 Markdown 链接、真实代码路径和 `git diff --check` 均通过。
- M7 权威结算 review package 已本地提交为 `7b9463e`；用户确认的能量 HUD 位置调整已隔离提交为 `c46950f`。提交期间只按用户明确授权移除了精确的零字节 `.git/index.lock`，未结束 Git/Unity 进程；当前未 push。

## 2026-08-02 · M7E 全量验证、真实 Game View、双轴复审与 M7 收口（已完成）

- M7A～M7E 已按唯一计划串行完成：`BattleCardPlayRules` 队首重校验 → 全量 Effect 预构建/快照校验 → 支付能量 → 按 `effect_bindings` 原序写入 → 当前卡牌进入 DiscardPile → 发布 Turn。失败命令保持能量、卡区、参与者、回合零写入且结算记录为空；阶段抽牌、弃手与重洗仍属于既有命令调用栈。
- 最终 M7 定向 **60/60**（任务 `4670704375fa4beb98b6206fce56c521`）、M2～M6 相关回归 **139/139**（任务 `873fd4ba9e844cf3a44b0b34529e691c`）、最终既有队列 **25/25**（任务 `713fd756cd5c46299f3e9bf212fbf8e2`）、最终全量 EditMode **180/180**（任务 `1ed0fbab97e74fe68c912b082129fda9`）均通过；串行 solution build 0 error、12 条既有依赖 warning，`git diff --check` 与未跟踪 C#/Markdown 空白审计通过。
- 唯一 Unity Editor 从 Bootstrap 生产链进入 BattleScene，干净实跑 Console 的 Error、InvalidKey、VContainer、Effect 四类筛选均为 0。真实系统指针依次证明 Bash `20 → 12` 且 Vulnerable `0 → 2`、易伤 Strike `12 → 3`、Defend Block `0 → 5`、致死 `3 → 0`、死亡目标与费用不足释放零写入回弹；无遮挡运行期 Strength 夹具仍经真实 UI/Submit/生产 executor，使 Strength `0 → 3`、能量不变、卡牌归弃牌堆，并在 Game View 直接显示“力量 +3”。
- Standards 首轮的文档状态硬 finding 已在本次收口修正，public executor 误报经生产所有权复核后撤销；两条判断性重复按 AC-002 保留显式小分支。最终收口复核为 **Standards 0 finding / Spec 0 finding**，两轴均无 Hard 或 Judgement finding。固定点为 `e76a654846fa735c92f51ad293dfa823e6724b44`，用户独立 Targeting 提交与 `BattleTurnHud.prefab` 调整均排除并保护。
- M7 未修改配置、生成内容、Localization、Addressables 内容、Scene/Prefab、高影响设置、Run/网络或 DI，无需 Luban/Addressables 重建。决策维持 CD-039～042；DEP-004/009 只回填 M7 已完成部分并保持 open，DEP-012/013 保持 open。最终证据见 `06_testing/2026-08-02-m7e-full-validation-review.md`；下一阶段为 M8。

## 2026-08-02 · M7D 出牌事务与卡区结算记录（已完成）

- `BattleTurnController.TryPlayCard` 现在继续先用 M6 同一 `BattleCardPlayRules` 重校验，再由 M7C executor 完成全量预构建与快照校验；成功后固定按 Energy → `effect_bindings` 原序 Effect → 当前卡牌进入 DiscardPile → 一次 Turn 发布执行。队列只透传内部操作结果，不解析 EffectType，Submit、权威序号、轮次栅栏与 presentation 屏障保持不变。
- `BattleCardZonesData` 的 Draw、DiscardHand、DiscardFromHand/ExhaustFromHand 现在返回冻结的 `BattleCardZoneOperationResult`。记录明确包含残余抽牌、弃牌按原序移回抽牌堆、重洗后完整顺序、继续抽牌与弃手；StartBattle、EndPlayerAction 和最终敌人完成只在既有状态机调用栈中把这些记录并入当前命令，没有新增系统命令或全局变化日志。
- 公开队列测试证明 Strength、Strike、Defend、Bash 的能量、公式、格挡吸收、易伤、绑定顺序、致死 skipped 与最后归堆；费用不足、卡牌离手、目标排队后死亡、模板/Effect 缺失和跨轮旧出牌均为空记录且不新增写入。最终 M7D 定向及 M2～M6 回归 **139/139** 通过（任务 `873fd4ba9e844cf3a44b0b34529e691c`），旧队列回归 **25/25** 通过。
- 串行 solution build 为 0 error、12 条既有依赖 warning，`git diff --check` 通过。BootstrapScene 生产链进入 BattleScene，Console Error、InvalidKey、VContainer、Effect 过滤均为 0。未修改配置、可寻址内容、Scene/Prefab、高影响设置或 M9 美术，无需 Luban/Addressables 重建。
- 决策见 CD-042，验收见 `06_testing/2026-08-02-m7d-card-effect-transaction.md`。本切片只形成自动 Bootstrap 证据，没有冒充真实鼠标验收；M7D 独立停止点完成，下一步严格进入 M7E。

## 2026-08-02 · M7C 有序 Effect 执行 module（已完成）

- 新增 concrete `BattleEffectExecutor` 与冻结 request/result；公共 `Execute` 接收来源、单个显式目标和有序绑定，内部 `Prepare` 在首次写入前校验全部 Binding、Effect 表项、类型、属性、数值范围及初始参与者事实，并用四项标量顺序模拟完整操作链。任一失败均返回明确原因与空记录，Health/Strength/Block/Vulnerable 的只读对象和值保持不变。
- 预构建成功后只经 M7B internal 状态操作写入：Strength、Strike、Defend、Bash 均按 `effect_bindings` 原顺序产生不可变记录；重复 Bash 会读取最新易伤而把第二次 8 点基础伤害结算为 12。前序致死后的后续已验证操作产生 `OperationSkipped(TargetNotAlive)`，但后续缺失/非法配置仍会在首次写入前令整链失败。
- TDD 最终 executor **15/15** 通过（任务 `090eb2a78ff6455fa7b22ab638b39d55`）；M7B 测试夹具全部迁到公共 executor，临时 `InternalsVisibleTo` 与 Meta 已删除，最终相关回归 **95/95** 通过（任务 `aa249726f6c9464396471ee74f864a40`）。串行 build 0 error、12 条既有 warning，`git diff --check` 通过，Unity Console Error 0。
- M7C 尚未接 `TryPlayCard`，因此生产 M6 出牌仍不执行 Effect；未修改配置、可寻址内容、Scene/Prefab、高影响设置或 M9 美术，无需 Luban/Addressables 重建。决策见 CD-041，验收见 `06_testing/2026-08-02-m7c-ordered-effect-executor.md`；下一步严格进入 M7D。

## 2026-08-02 · M7B 参与者权威状态与伤害操作（已完成）

- `CombatantData` 现在唯一持有 Health、Strength、Block、Vulnerable 四项 R3 事实，Block/Vulnerable 初值为 0，并提供对应只读事实、同步读取与完整 Dispose 生命周期；未新增存活、状态或派生列表镜像。
- 新增 internal concrete `BattleCombatantEffectOperations`，集中 GainBlock、ModifyStrength、ApplyVulnerable 与 ApplyDamage。Damage 只调用一次 M7A 共享公式，再由一个内部写入口在同一同步调用内写入 Block/Health，并返回完整不可变 damage outcome；重复攻击死亡目标明确返回 `TargetNotAlive` 且不再写入。
- 删除旧 `BattleCombatantsData.ApplyDamage → CombatantData.ApplyDamage(int)` 双层直通，13 个既有测试调用全部迁到新 Effect 状态路径。最终状态/公式核心 **24/24** 通过（任务 `8cc24387d2664e5cba1b17d27ad29973`），连同规则、队列、Session、目标和敌人意图/HUD 的定向回归 **72/72** 通过（任务 `de864c324234402b86e4d9b2e2c79220`）；串行 build 0 error、12 条既有 warning，`git diff --check` 通过。
- M7B 没有读取 Card.EffectBindings、创建正式 executor 或接入出牌事务；临时 Editor friend 只用于当前切片直接验证 internal 状态操作，M7C 公共 executor seam 落地后必须迁移测试并删除。未修改配置、可寻址内容、Scene/Prefab、高影响设置或 M9 美术，无需 Luban/Addressables 重建。决策见 CD-040，验收见 `06_testing/2026-08-02-m7b-combatant-effect-operations.md`；下一步严格进入 M7C。

## 2026-08-02 · M7A 结算记录与公式契约（已完成）

- 新增强类型 `BattleEffectId`、最小 Effect/结算枚举、不可变 `BattleSettlementRecord` 体系和冻结列表；`BattleCommandExecutionResult` 与 production presentation adapter 均携带同一列表。既有成功命令和尚未进入 M7 写链的失败命令都返回非 null 空记录，未建立全局结算日志。
- 新增纯 `BattleEffectFormula.Calculate(context)`：伤害先取 `max(0, configured + Strength)`，目标易伤时按 `* 3 / 2` 向下取整；GainBlock/ApplyVulnerable 钳制非负，ModifyAttribute 保留有符号值。`BattleEffectValueCalculator` 保持公开签名并只做 Luban/来源事实到无目标公式投影的适配，卡牌文本与敌人意图继续共享结果。
- TDD 依次确认缺失结算契约、八种记录类型、缺失公式 module、易伤/非负行为和旧显示分叉的红灯。最终 M7A 定向 EditMode **83/83** 通过（任务 `c62162836bd5451487ac273793d461a3`），0 failed、0 skipped；串行 solution build 0 error，保留 12 条既有依赖 warning；`git diff --check` 通过，新增 Meta 均由当前唯一 Unity Editor 生成。
- M7A 没有修改参与者权威状态或出牌事务，没有执行正式 Effect；`Submit`、只读 `Queue` / `Turn`、序号、展示屏障与轮次栅栏保持不变。未修改 DataTables、生成配置、Localization、Addressables、Scene/Prefab、高影响设置、Run/网络、DI 或 M9 美术，无需 Luban/Addressables 重建。决策见 CD-039，验收见 `06_testing/2026-08-02-m7a-settlement-formula-contract.md`；下一步严格进入 M7B。

## 2026-08-02 · M7 Effect 执行器总计划与 Goal 边界（待实施）

- 新增 `plans/2026-08-02-m7-effect-executor.md` 作为 M7 唯一实施计划，按 M7A 结算/公式契约、M7B 参与者状态操作、M7C 有序 Effect executor、M7D 出牌事务与卡区记录、M7E 全量验证/真实 Game View/双轴复审串行推进。每个切片有独立停止点，计划内附可直接复制到新对话的总 Goal 文案。
- M7 计划锁定当前 MVP 公式：伤害先取 `max(0, base + Strength)`，目标 Vulnerable 大于 0 时乘 `3/2` 并向下取整，Block 先吸收、剩余才扣 Health；GainBlock 不含 Dexterity，ModifyAttribute 只支持 Strength，ApplyVulnerable 累加。Block 清理、Vulnerable 衰减和状态触发时机仍由 M8，登记 `DEP-013`。
- 结算记录先于状态写入接口落地：`BattleCommandExecutionResult` 将携带不可变有序记录，失败命令记录为空；阶段内抽牌、弃手牌和重洗继续发生在现有命令调用栈，但由卡区 module 返回明确操作结果并并入该命令记录，不在 M7 新增系统命令或重写队列调度。
- Effect 管线采用具体纯 C# 深 module，不为单一实现新增 `I*` adapter；运行时 Effect ID 在新管线内强类型化，公式 module 同时支撑无目标展示投影与目标结算。全部 Binding/目标/操作在首次写入前预构建，错误必须保持能量、卡区、参与者、回合和记录零变化。
- 当前 Card 配置没有 Discard/Exhaust 归宿字段，M7 四张现有卡一律在效果完成后进入弃牌堆，不按模板 ID、卡名或 EffectType 硬编码；新增 `DEP-012` 等待未来首张消耗牌的正式数据来源。`DEP-004` 仍等待 M9 消费结算记录播放过渡，`DEP-009` 仍需 M7 seam 与 M8 敌人接线共同完成。
- 明确排除 M8 敌人 Effect/状态时机/队列与 pending 重构，M3E/M9 HUD、数字、抖动、死亡/胜负/最终动画和 LXX-6 美术接线，以及 G1/M10 复盘债务。本计划不修改 DataTables、生成配置、Localization、Addressables 内容、Scene/Prefab、ProjectSettings、asmdef、HybridCLR、Run/网络或 DI。
- 本次只创建和同步计划文档、计划索引、ROADMAP、DEPENDENCIES 与状态源；没有修改 C#、测试、配置、资源或 Unity 资产，没有运行 Unity、Luban、Addressables、测试或构建，也未 commit、未 push。工作区既有 `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/` 及目录 Meta 保持未跟踪且未触碰。下一步由用户在新对话启动总 Goal。

## 2026-08-02 · M6D 全量验证、双轴复审与 M6 收口（已完成）

- M6A～M6D 已按唯一计划串行完成：`PlayCardCommand` 显式携带 Self/Enemy 目标；UI 预览与队首执行共用同一 `BattleCardPlayRules`；目标排队后死亡会以 `TargetNotAlive` 零写入失败；生产 UI 只通过既有 `BattleCommandQueue.Submit` 提交，并提供功能性费用颜色、箭头、高亮、屏幕命中和回弹。
- 最终 Unity MCP M6 定向 EditMode **53/53**、全量 EditMode **122/122** 通过，均为 0 failed、0 skipped；串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error，保留 12 条既有 Unity/R3/UniTask 依赖 warning；`git diff --check` 在最终文档收口后通过。
- `TinySpire/Addressables/Build Local Content` 成功；报告 `buildlayout_2026.08.02.01.52.06.json` 的 `BuildError` 为空、`BuildResultHash=2f21014862b879079e277deb7b7d1cbb`、耗时 `18.4565022s`。从 Bootstrap 生产链进入 BattleScene 后，运行时手牌 5、参与者 HUD 3、目标箭头已接线，Console 无 Error、InvalidKey 或 VContainer 错误。
- 真实 Game View 使用累计最终证据收口：M6C 已物理确认 Self、左右 Enemy、无效释放、结束行动/下一轮和 16:7/16:9/16:10/16:11/16:14；最终 transition 修订后，用户又确认费用不足红色卡可拿起跟手，但不进入出牌反馈、瞄准、resolver 或 Submit，释放回弹且权威事实不变。因此不要求重复整套已通过动作。
- Standards / Spec 以 M5 commit `bbfb650ce9643c470fa59345cba91be26b82420a` 为固定基线，并行读取 tracked diff 与全部未跟踪 M6 文件。Spec 首轮为 0 finding；Standards 唯一硬 finding 是 M6C 页残留过期状态，已修正。Container 职责与三处短线性目标扫描是判断性气味：前者真正收敛需跨 Hand/TurnHUD/Presentation/Queue，已排入 M8；后者保留各自局部边界，不新增浅通用 helper。最终文档回填后，原两个审查者复核为 **Standards 0 finding / Spec 0 finding**。
- 已完整复核 M5 回顾并谨慎采纳：统一规则链、`TargetId` 承诺、纯拖拽 transition 与 Presenter 唯一 View 映射已落实；结算/事务留 M7，队列/提交/pending/阶段屏障留 M8，HUD/Prefab/最终反馈留 M3E/M9，配置 fail-fast 与构建前校验留 M10，Session 唯一玩家/卡区装配出口留 G1。没有提前实施 M7～M9，也没有修改 `BattleScene.unity`、`CardView.prefab`、角色 Prefab、ProjectSettings、Physics、asmdef、HybridCLR、Luban、Localization、Run/网络/启动流程。
- `DEP-001` 保持 resolved，`DEP-003/004/009/010/011` 保持原状态；M6 计划已归档到计划索引历史区。Linear LXX-6 后续回复确认四张同名 PNG 已按尺寸、RGBA、透明中心及文件一致性完成美术验收，Issue 状态为 Done，并再次明确只供后续 M9 接入。Unity 随后为这些工作区文件生成未跟踪 Meta，但它们未接入 M6 Prefab、未纳入本次 Addressables/验收，也不进入 M6 提交。详细证据见 `06_testing/2026-08-02-m6d-full-validation-review.md`。本次 M6 提交由用户在验收完成后另行授权，仍不 push，并继续保护范围外改动。

## 2026-08-02 · M6C 人工审阅回填与费用不足拖动修订（已完成）

- 真实 Game View 人工审阅已回填：Self、Enemy 功能性箭头/高亮、无效释放、16:7/16:9/16:10/16:11/16:14、结束行动/下一轮清理及 Console 检查均有物理结果。最终 Enemy 聚焦与结束行动弃牌过渡属于 M9，已写入 `10_communication/2026-08-02-battle-card-motion-feedback-brief.md`；Linear LXX-6 已按用户澄清收窄为只请求箭身、箭头及合法/悬停高亮四张透明 PNG，不交付交互代码。尝试委派给 Linear AI 时曾被“workspace 未启用 coding sessions”阻止；随后该 Issue 收到正式资源交付回复并标记 Done，仍未创建重复工单或新项目。目标伤害/格挡/状态继续由 ROADMAP M7 与 `DEP-009` 承接，未提前实现。
- 当前只采纳“费用不足卡仍可拖动”的窄修改：新增 UI 纯函数区分视觉拿起与规则许可；精确 `InsufficientEnergy` 保持红色并允许跟手，但出牌反馈、Enemy 瞄准、释放 resolver、最终评估与 `BattleCommandQueue.Submit` 仍要求原始规则许可。因此越线释放只回弹，不创建目标、pending、权威序号或任何卡区/能量/回合写入；其他失败仍锁定输入。决策见 CD-038。
- TDD 先用新可供性用例得到旧实现红灯，再接入 `HandCardContainer`；独立审计随后发现“拖动中另一命令扣费”会错误取消当前卡牌，补充三态 `Disabled / VisualOnly / Playable` seam 的编译红灯后修复。二次复核继续按真实写入顺序发现 CardZones 在能量 Turn 前发布，旧 `RebuildCards` 仍会抢先取消拖拽；为避免只测成员 helper，新的 `CardZones → Turn → 被拖牌自身离手` tracer test 先以 11 个缺失 transition interface 编译错误失败，再以 `HandCardDragTransitionPolicy` 一次输出保留/取消、排除重排、下一阶段、清反馈/目标表现和重建 Enemy 瞄准，并由容器两个事实回调直接消费。最终 Unity MCP M6 定向 EditMode **53/53** 通过（0 failed、0 skipped，任务 `6de86cddde1d4cd7ac38cbf72431bb91`），串行 solution build 0 error、保留 12 条既有依赖 warning，`git diff --check` 通过。
- M2/M4 的结束行动规则保持不变：剩余手牌权威移入弃牌堆，M6 不保留可交互旧 View 或手牌镜像；未来可见过渡只由 M9 文档承接。用户已在真实 Game View 确认费用不足红色卡可拿起跟手、无出牌反馈/箭头/高亮、越线释放回弹且权威事实不变，复测后的 Console 无错误；`DEP-001` resolved，M6C 独立停止点完成，下一步串行进入 M6D。未 commit、未 push。

## 2026-08-01 · M6C Self / Enemy 目标选择 UI（当时完成自动验证，现已完成物理验收）

- `HandCardInteraction` 现在把完整 `PointerEventData` 交给 `HandCardContainer`；容器从 M6A 同一 `BattleCardPlayRules` 即时派生交互、费用颜色和合法目标。Self 越线显式提交玩家自身，Enemy 首次越线后冻结卡牌并进入箭头瞄准，释放时只把 Presenter 命中的精确存活敌人 ID 交给既有 `BattleCommandQueue.Submit`。生产与测试调用均已显式提供目标，未增加第二条写链或目标结果流。
- `BattleParticipantPresenter` 按 Encounter 稳定顺序把现有 `SpriteRenderer.bounds` 投影为屏幕矩形，重叠命中选择矩形中心最近者、同距保留先遇到者；`ParticipantHudView` 仅显示默认隐藏且不接收 Raycast 的合法/悬停高亮。`BattleHandUI.prefab` 接入功能性箭头 Prefab；未修改 `BattleScene.unity`、`CardView.prefab`、角色 Prefab、ProjectSettings、Physics、配置表或 Localization。
- Bootstrap 自动诊断先后暴露两个生产接线问题：`BattleHandUI` 根节点缩放为 0 使箭头继承零缩放；箭头嵌套在有缩放/深度的 Screen Space Camera Canvas 下又使屏幕端点转换失败。现已把手牌根缩放恢复为 1，并由容器在运行时把序列化箭头提升为独立 `ScreenSpaceOverlay`、统一持有和销毁；新增 Prefab 与 Overlay 回归测试锁定该契约。
- Unity MCP 最终 M6C 定向 EditMode **51/51** 通过，任务 `3b8af941470b4933a86f2c098d95098d`；串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error，保留 12 条既有依赖 warning；`git diff --check` 通过。Addressables 本地内容构建 `BuildError` 为空、`BuildResultHash=92b5408c9884e0ed9922ed56f9c10ffa`；Bootstrap 自动进入 BattleScene，手牌 5、HUD 3，未见 Error、InvalidKey 或 VContainer 错误。
- 自动运行期探针确认箭头成为独立 Overlay、可见且不接收 Raycast，左右敌人屏幕命中与合法/悬停高亮可生成；这些只用于当时的接线定位，**没有冒充真实 Game View 物理鼠标验收**。该条记录中的待验项后来已由用户在多个分辨率完成，`DEP-001` 已 resolved，M6C/M6D 均已完成；最终证据见 `06_testing/2026-08-02-m6d-full-validation-review.md`，实现决策见 CD-037。

## 2026-08-01 · M6B 队首目标重校验与权威写链（已验证）

- `BattleTurnController.TryPlayCard` 现在在首次权威写入前调用 M6A 的同一 `BattleCardPlayRules`；全部当前事实通过后才沿用既有“指定实例离手进入弃牌堆 → 扣除该玩家能量 → 发布 Turn”写链。`BattleCommandQueue.Submit`、只读 `Queue` / `Turn`、提交轮次栅栏与 public interface 均未改变。
- 队列测试工厂可为测试卡显式配置 `TargetRule`；现有命令与 presentation 测试已全部显式传 Self/Enemy 目标。因 `BattleAlreadyEnded` 的既定优先级，相关旧出牌夹具逐用例加入 Encounter 中的存活敌人，没有在工厂内隐式伪造战斗事实；生产 `HandCardContainer` 的显式目标迁移仍严格留给 M6C。
- TDD 首先证明旧控制器会让“目标排队后死亡”错误成功；接线后该场景稳定返回 `TargetNotAlive`。失败时 Turn 与卡区快照、目标 `Health` 只读对象和当前值保持不变，表现完成后队列正常回到空闲；合法 Enemy 出牌只扣 1 点能量、只移动指定实例一次，表现回调前后目标生命均不变化，因此没有提前执行 M7 Effect。
- Unity MCP 最终相关 EditMode **60/60** 通过，覆盖纯规则、队列、presentation、回合控制与 M5 敌人意图回归；Console Error 为 0。串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error，保留 12 条既有 Unity/R3/UniTask 依赖版本冲突 warning。
- M6B 未修改场景、Prefab、Luban、Localization、Addressables、生产 UI、Effect、伤害、格挡、状态或胜负；未运行 Addressables、Bootstrap 或 Game View，它们按唯一计划留给 M6C/M6D。验收见 `06_testing/2026-08-01-m6b-queue-head-target-revalidation.md`，决策见 CD-036；下一步严格进入 M6C。

## 2026-08-01 · M6A 目标契约与纯合法性 module（已验证）

- `PlayCardCommand` 增加可空 `CombatantId? TargetId`，非空的零/负结构标识在构造时拒绝；M6A 暂保留默认空目标供既有调用方编译，默认值将在 M6C 完成全部显式迁移后移除。
- 新增具体纯 C# `BattleCardPlayRules` 与不可变 `BattleCardPlayEvaluation`。规则只读取当前 `BattleTurnData`、`BattleCombatantsData`、玩家卡区、静态 `Tables` 和 `EnemyCombatantIdsInEncounterOrder`，即时派生 Self/Enemy、费用可支付性、战斗可继续性与稳定合法目标快照，不保存 `CanPlayCard`、存活列表或目标历史镜像。
- 新增 `BattleAlreadyEnded`、`TargetRequired`、`TargetNotFound`、`TargetNotAlive`、`TargetRuleMismatch` 与 `UnsupportedTargetRule`。Self 只接受 Actor；Enemy 只接受 Encounter 顺序中的存活敌人；重复预览不改变 Turn、卡区、生命或洗牌/敌人意图随机流。
- TDD 先得到缺失规则类型/三参数命令构造器的编译红灯，再得到 Enemy 规则行为红灯；最终 Unity MCP `BattleCardPlayRulesTests` **8/8** 通过，M6A 前相关队列/回合基线 **26/26** 通过，Console Error 为 0。串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error，保留 12 条既有依赖版本冲突 warning。
- M6A 未修改 `BattleCommandQueue`、`BattleTurnController`、场景、Prefab、Luban、Localization、Addressables、卡区写链或 Effect；`Submit` 与只读 `Queue` / `Turn` seam 保持不变。验收见 `06_testing/2026-08-01-m6a-card-play-rules.md`，决策见 CD-035；下一步严格进入 M6B 队首目标重校验。

## 2026-08-01 · M6 总计划与 Goal 执行边界（待实施）

- 新增 `plans/2026-08-01-m6-card-play-legality-target-selection.md`，按“一份总计划 + 一个总 Goal”拆为 M6A 目标契约与纯合法性、M6B 队首目标重校验、M6C Self/Enemy 目标 UI、M6D 全量验证与收口。每个切片具有独立停止点，新会话可直接复制计划内 Goal 文案串行执行。
- M6 基线按当前实现修正：M4 已完成 `PlayCardCommand`、阶段/手牌/费用/能量执行期校验、权威队列和 UI pending 恢复；M6 不重做这些内容，只增加显式目标、派生合法性、队首失效保护与目标交互。成功仍只扣能量并进入弃牌堆，不实施 M7 Effect、M8 敌人真实行动或 M9 胜负/最终反馈。
- 第一版目标命中方案使用 `BattleParticipantPresenter` 现有 `CombatantId → world view/HUD` 映射，把世界角色 `SpriteRenderer.bounds` 投影为屏幕矩形；不增加 Collider、Physics2D Raycaster、角色 Prefab 身份脚本或第二套 View 注册表。`DEP-001` 当前仍 open，只有 M6C 实现并通过真实 Game View 验证后才能 resolved。
- 本次只创建计划并对齐 `ROADMAP.md`、`DEPENDENCIES.md` 与计划索引；未修改任何 C#、场景、Prefab、配置、生成文件或测试，未运行 Unity、Addressables、EditMode 或构建，也未 commit、未 push。下一步由用户在新会话启动总 Goal。

## 2026-08-01 · M5D 全量验证、双轴复审与 M5 收口（已完成）

- M5A～M5D 已串行完成：Enemy 静态模板引用有序行为组；`BattleEnemyIntentsData` 以独立确定性随机流和不可变完整快照持有每名敌人的权威当前 `BehaviorId`；M4 合法敌人完成命令先原子选择下一意图，再保证推进 Encounter 顺序；M3D HUD 从同一意图、静态 Effect 与当前参与者事实派生正式图标和值。
- 最终 Luban 等价命令成功生成 C# 与 `Assets/GameData` JSON；生成器清理的手写 `game-config.json` 已从既有源逐字恢复，双方 SHA-256 为 `048CDC9E8DB80F80BE9E43D409ED1A91A011E0118CBAB18EC207509B3C904CF8`。最终 Addressables 报告 `buildlayout_2026.08.01.09.39.35.json` 的 `BuildError` 为空、`BuildResultHash=d030cfdcfd7d76e4ca432b66eae62cea`、耗时 `8.6252568s`，M5 JSON、game-config、BattleScene 与 HUD Prefab 的完整稳定地址均存在。
- Unity MCP 最终 M5 定向 EditMode **73/73**、全量 EditMode **98/98** 通过，均为 0 failed、0 skipped；串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error，保留 12 条既有依赖版本冲突 warning。
- 两次从 Bootstrap 使用同一 Inspector 种子 `1` 实跑得到完全相同序列：初始 ID `2 → 3`、`7001 Attack / 7003 Defend`；ID 2 完成后发布 `7001/7003`，ID 3 完成后发布 `7001/7002`；第二轮双方 HUD 均为正式攻击图标和值 6，玩家意图隐藏。两次 Console Error/Warning 为 `0/0`，没有 `InvalidKey` 或 VContainer 错误。
- M5C 已在实际 Game View 用不保存资产的运行期夹具目视确认 1～3 敌人图标语义、数值可读性和 HUD 不重叠。计划内 attack/defend/buff/debuff/special 五类正式图标全部存在且导入合约正确，没有缺失美术资源。
- Standards / Spec 首轮并行复审各自只指出同一项 P2：最终验证已写入验收页，但 `SESSION_LOG.md` 与计划状态尚未收口。现已通过本条状态源、计划归档和 M5D 验收页回填修复；两轴均未发现代码实现、规格偏差、scope creep 或明确代码气味。M5 未实现真实 Effect、伤害、格挡、状态、死亡动画、胜败、行为树或 DSL；`DEP-009` 保持 open，剩余工作为 M7/M8 的真实敌人 Effect 执行。
- 本次保护了启动前唯一既有未跟踪计划文件，并在其上同步状态；未修改 `BattleScene.unity`、ProjectSettings、asmdef、HybridCLR、Run 生命周期或启动流程，未 commit、未 push。详细证据见 `06_testing/2026-08-01-m5d-full-validation-review.md`，决策见 CD-032～CD-034。

## 2026-08-01 · M5C 敌人意图 HUD（已验证）

- `ParticipantHudView.prefab` 以静态 `IntentRoot / IntentIcon / IntentValueText` 子树接入五类正式意图 Sprite；所有 `_ref_` 参考图均未进入生产 Prefab。玩家 HUD 固定隐藏意图，存活敌人从同一 `EnemyIntentLayoutData` 的 `BehaviorId`、Luban 行为/Effect 模板和当前参与者事实即时派生图标与数值，死亡时隐藏。
- 原 `CardValueCalculator` 以保留 Meta GUID 的方式最小深化为 `BattleEffectValueCalculator`，卡牌文本与敌人 HUD 共用同一效果值计算入口；力量、生命、Locale、意图快照或 View 重建只触发展示重派生，不保存预测值，也不调用行为选择。`BattleParticipantPresenter` 只把现有 Session、Tables 与世界 View 交给 HUD，没有新增事实镜像或 DI 层。
- Unity MCP 定向 EditMode **39/39** 通过，覆盖共享效果值、HUD 纯投影、Prefab 正式资源/层级合约、权威意图核心、Session 和命令队列。`TinySpire/Addressables/Build Local Content` 成功；报告 `buildlayout_2026.08.01.09.25.30.json` 的 `BuildError` 为空、`BuildResultHash=d030cfdcfd7d76e4ca432b66eae62cea`、耗时 `15.5289761s`。
- Bootstrap 生产实跑首轮显示 Enemy 2001 `Behavior 7001 / Attack / 6`、Enemy 2002 `Behavior 7003 / Defend / 5`，玩家意图隐藏；第一轮完成后进入 Round 2，Enemy 2002 变为 `Behavior 7002 / Attack / 6`，HUD 与事实同步。重复读取前后敌人随机状态均为 `2144564843`。
- Game View 用现有正式 View/HUD 构造了不保存场景或 Prefab 的运行期 1/2/3 敌人视觉夹具，确认意图不与名称、生命和力量 HUD 重叠。MCP 截图实现自身曾写入 5 条 `PlayerLoop recursive` 错误；销毁夹具、退出并从 Bootstrap 干净复跑后 Console Error/Warning 为 `0/0`，未出现 `InvalidKey` 或 VContainer 错误。
- 本切片只修改 `ParticipantHudView.prefab`，未修改 `BattleScene.unity`、asmdef、ProjectSettings、启动流程或 DI 架构；未执行真实 Effect、伤害、格挡、状态、死亡动画或胜败。五类计划内正式意图图标均已存在，没有缺失美术资源。决策见 CD-034，验收见 `06_testing/2026-08-01-m5c-enemy-intent-hud.md`。

## 2026-08-01 · M5B Session、权威命令队列与生产接线（已验证）

- `BattleSession` 现在在按 Encounter 顺序创建敌人后建立并公开唯一 `BattleEnemyIntentsData`，构造失败会释放已经创建的意图、卡区与参与者，正常销毁时由 Session 先释放意图再释放其依赖事实。`BattleLifetimeScope` 把该同一实例交给命令队列，没有第二份聚合或额外 DI 层。
- `CompleteEnemyActionCommand` 到达队首后，`BattleTurnController` 先只读校验阶段、敌人身份与当前行动者；通过后由队列调用 `EnemyIntents.CompleteAndSelectNext`，成功才调用不可失败的 Encounter 顺序推进。无候选异常会让队列停在当前命令，意图、随机与回合均不变；错误阶段、错误敌人和重复完成继续返回 M4 原失败原因且零写入。
- `BattleCommandRuntimeDriver` 仍只在队列空闲时每帧提交一条当前敌人完成命令，不直接读取候选或调用随机。进入敌人轮前已死亡的敌人继续由 M4 Encounter 顺序跳过，不为其补选意图；第一版仍不执行伤害或其他 Effect。
- Unity MCP 相关 EditMode **47/47** 通过，覆盖 M5A 核心、Session、M5B 意图/队列集成与完整 `BattleCommandQueueTests` 回归。特别验证意图发布先于 Turn 推进、第一名完成不改变第二名、错误/重复完成零写入、死亡跳过、无候选停止命令链，以及生产驱动两轮每帧最多完成一名敌人。
- `TinySpire/Addressables/Build Local Content` 成功；报告 `buildlayout_2026.08.01.09.07.56.json` 的 `BuildError` 为空、`BuildResultHash=d8794bc54bf6fa0df3cc1595bc89c6ef`、耗时 `13.7309617s`，两个新增行为 JSON 与 BattleScene 均保留完整稳定地址。
- Bootstrap 实跑初始为 `PlayerAction / Round 1`，Enemy 2001 为 Behavior 7001 / Attack、Enemy 2002 为 Behavior 7003 / Defend；生产命令链完成两轮后进入 `PlayerAction / Round 3`，两名敌人生命均保持 20，Console Error/Warning 为 0。M5B 未修改场景、Prefab 或 HUD；M5C 尚未开始。决策见 CD-033，验收见 `06_testing/2026-08-01-m5b-session-command-queue-wiring.md`。

## 2026-08-01 · M5A 敌人行为配置与确定性选择核心（已验证）

- 新增 `EnemyBehaviorGroup` / `EnemyBehavior` Luban 表与 `EnemyIntentType` 枚举；Enemy 通过 `behavior_group_id` 引用行为组，默认 Encounter 5001 现在按顺序包含固定行为 Enemy 2001 与加权随机 Enemy 2002。行为只引用既有 `CardEffect` 数值，不执行 Effect。
- 新增 `BattleEnemyIntentsData`：按 Encounter 顺序为每名敌人选择初始意图，以不可变完整 R3 快照持有唯一的 `CombatantId -> BehaviorId` 事实；每名敌人只保存冷却与最大连续次数所需的已完成历史。敌人行为随机从战斗种子以稳定盐派生独立 `GameRandom`，单候选不消费随机，多候选只调用一次整数权重选择。
- 行动完成后的候选过滤、历史更新、随机选择与意图发布先在副本上完成；无候选或配置错误会恢复随机状态且不发布新快照。错误引用、非正权重、负冷却/连续上限、重复行为和权重溢出均显式失败，没有随机回退。
- 六份工作簿已通过 `@oai/artifact-tool` 编辑、渲染与公式错误扫描；Luban 等价命令成功生成新增配置 C# 与 `Assets/GameData` JSON，`ConfigService` 已预加载两张新表。Unity MCP 定向 `BattleEnemyIntentsDataTests + BattleSessionTests` **18/18** 通过，脚本编译与 Console Error 为 0。
- 本切片未修改 `BattleSession` 生产持有关系、M4 命令队列、回合推进、场景、Prefab 或 HUD，也未实现真实伤害、格挡、状态、胜败、行为树或条件 DSL。M5A 已满足独立停止点；M5B 尚未开始。决策见 CD-032，验收见 `06_testing/2026-08-01-m5a-enemy-behavior-selection.md`。

## 2026-08-01 · M4E 全量验证、轮次栅栏修复与文档收口（已完成）

- 首次 Spec 复审与生产探针发现：全体敌人已死亡时，上一轮排在结束命令后的玩家命令会在同步开始的新一轮重新合法。用户确认“玩家命令只能属于提交时的轮次”，并授权采用队列内部轮次栅栏；该问题属于当前 M4 已承诺行为，没有登记为未来依赖。
- `BattleCommandQueue` 的内部排队信封现在记录提交 `RoundNumber`。`PlayCardCommand` 与 `EndPlayerActionCommand` 到达队首时若已经跨轮，返回 `PlayerActionWindowExpired`，不调用 `BattleTurnController`；公共命令构造参数、`Submit` / `Queue` / `Turn` seam、`BattleTurnData` 与 DI 均未改变。两条 TDD 用例分别覆盖全敌死亡后的重复结束，以及同一 `CardInstanceId` 下一轮重抽后的旧出牌，均先复现旧行为再通过修复。
- 修复后的 Spec 复审又发现同 ID View 的展示关联风险：旧序号失败可能误清更新序号 pending。`HandCardVisual` 现以 nullable 权威序号作为 pending 唯一事实，失败只清除匹配序号；`HandCardContainer` 在 View 重建时从既有映射恢复最新待定序号。该行为同样完成红灯到绿灯闭环。
- 最终 Unity MCP 三项缺陷复合切片 **3/3**、M4 队列/回合/展示定向 EditMode **30/30**、全量 EditMode **70/70** 通过，均为 0 failed、0 skipped；串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error，保留 12 条既有依赖版本冲突 warning。
- Bootstrap 正常路径完成两个轮次并进入 `PlayerAction / Round 3`。生产跨轮探针将两条结束命令同时排在 Round 3：第一条进入 Round 4，第二条反馈 `Failed #8 · EndPlayerAction · PlayerActionWindowExpired`；最终仍为 `PlayerAction / Round 4 / Energy 3 / HasEndedAction false`、队列空闲，运行期 Console Error/Warning 为 0。
- 生产 View 重建探针在 Round 3 观察到与旧手牌重叠的 ID 4、9 两个新 View 均恢复 pending；旧跨轮出牌全部失败后当前 5 个 View pending 为 0、队列空闲，Console Error/Warning 为 0/0。
- 最终 `TinySpire/Addressables/Build Local Content` 成功；报告 `buildlayout_2026.08.01.06.41.41.json` 的 `BuildError` 为空、`BuildResultHash=259e02cf2d79b5cd0bd291f571b46782`、耗时 `16.9804458s`，场景与七个 GameData JSON 保持完整稳定地址。本阶段未修改 `DataTables/Datas/`、生成 JSON、场景、Prefab、ProjectSettings、asmdef、DI 或启动流程，因此未运行 Luban。
- Standards / Spec 双轴复审通过本次修复范围；既有三个 M4 提交摘要缺少冒号后空格的问题只记录、不改写历史，`BattleTurnController` 重复校验链也不借此扩展重构。M4E 与 M4 已完成，计划转入历史归档；详细证据见 `06_testing/2026-08-01-m4e-full-validation-review.md`，实现决策见 CD-031。

## 2026-08-01 · M4D 当前单玩家命令 UI 接线（已验证并接生产）

- `HandCardContainer` 不再直接弃牌或推进阶段：拖牌越过出牌线只提交 `PlayCardCommand`，并用权威序号关联该卡的短生命周期 pending 视觉。当前结果展示期间其他合法卡仍可继续提交；成功后由权威卡区布局移除 View，执行期失败则清除 pending、恢复交互，且能量和卡区保持不变。
- 新增 `BattleCommandPresentationAdapter`，生产中分别发布“已排队 / 执行失败 / 执行完成”反馈并在非缩放时间内保留最短展示窗口；新增 `BattleTurnHudView` 与静态 `BattleTurnHud.prefab`，从 `BattleCommandQueue.Turn` 和展示反馈派生第几轮、阶段、当前玩家能量、状态文字及按钮可用性。结束按钮只提交 `EndPlayerActionCommand`；成功进入敌人阶段后手牌输入立即锁定，系统敌人完成命令后下一轮恢复 3 能量、5 张新手牌和输入。
- Unity MCP 定向 EditMode **30/30** 通过（0 failed、0 skipped，`0.3453076s`）；两套相关程序集串行静态编译均为 0 error，保留 6/12 条既有依赖版本冲突 warning。Bootstrap 实跑首轮读取到 `PlayerAction / Round 1 / Energy 3 / Hand 5`，快速连续提交两张费用 1 的牌后按权威序号依次变为 1 能量、3 手牌；能量不足的费用 2 卡执行失败后仍为 1 能量、3 手牌并恢复交互。实际拖拽处理链由运行时回调执行，物理鼠标手感不冒充自动化结论。
- 从场景结束按钮实际 `onClick` 提交后，阶段进入 `EnemyAction`、剩余手牌统一弃置且旧 View 立即不可交互；系统完成无行为敌人后进入第 2 轮，能量恢复 3、手牌恢复 5、按钮和新手牌重新可用。运行期间 Console Error/Warning 均为 0，未出现 `InvalidKey` 或 VContainer 错误；随后正常退出 Play Mode。
- 场景和 Prefab 接线完成后，`TinySpire/Addressables/Build Local Content` 成功；报告 `buildlayout_2026.08.01.04.27.53.json` 的 `BuildError` 为空、哈希为 `259e02cf2d79b5cd0bd291f571b46782`、耗时 `19.984942s`。本切片未修改 Excel/Luban 表、生成 JSON、asmdef、DI 架构或启动流程，因此未运行 Luban；未实现 M4E 全量收口、真实敌人行为、Effect、伤害、状态、胜败或奖励。
- `DEP-002` 已解决；`DEP-001`、`DEP-004` 继续保持 open，M4E 尚未开始。决策见 CD-030，计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，验收见 `06_testing/2026-08-01-m4d-single-player-command-ui.md`。

## 2026-08-01 · M4C 队列化结束行动与敌人顺序交接（已验证并接生产）

- `EndPlayerActionCommand` 现在在队首执行时校验阶段、玩家身份与存活、重复结束及玩家卡区；成功后只弃置该玩家剩余手牌并设置其结束标记。仍有存活玩家未结束时继续保持 `PlayerAction`，全体完成后才进入敌人阶段；重复结束和排在结束命令后的旧出牌均明确失败且不重复写入事实。
- 敌人阶段只读取 `BattleSession.EnemyCombatantIdsInEncounterOrder`：死亡或缺失敌人会跳过，每次只发布一个 `CurrentActingEnemyId`，错误或重复的 `CompleteEnemyActionCommand` 不会越过当前敌人。当前无行为敌人由生产逐帧入口在后续帧经同一 `BattleCommandQueue.Submit` 完成，每帧最多一名，没有场景直通阶段写入。
- `BattleSession` 现在只创建参与者、运行时卡牌实例和洗牌后的未发牌抽牌堆；`StartBattleCommand -> PlayerRoundStart` 成为首轮与后续轮次重置每玩家能量、结束标记并抽到目标手牌数的唯一入口。`BattleLifetimeScope` 已注册队列、即时表现 adapter 与 `BattleCommandRuntimeDriver`；当前生产 Session 仍只映射唯一玩家卡区，`DEP-008` 保持 open。
- Unity MCP 定向 EditMode **27/27** 通过；`Assembly-CSharp` 与 `Assembly-CSharp-Editor` 静态编译均为 0 error（保留 6/12 条既有依赖版本冲突 warning），脚本刷新后 Console Error 为 0。Bootstrap 实跑进入 BattleScene 后从生产容器读取到 `PlayerAction / Round 1 / Energy 3 / Hand 5 / queueIdle=true`，加载日志正常且 Error 为 0，随后正常退出 Play Mode。
- 本切片未修改 Excel/Luban 表、手写 JSON、Addressables 内容、场景、Prefab、asmdef、HybridCLR 或现有 UI，因此未运行 Luban 或重建 Addressables。拖牌提交、能量/回合显示和结束按钮仍属于 M4D；真实敌人行为与 Effect 仍分别属于 M5/M7。M4C 已满足独立停止点，M4D～M4E 尚未开始。决策见 CD-029，计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，验收见 `06_testing/2026-08-01-m4c-end-action-enemy-handoff.md`。

## 2026-08-01 · M4B 队列化出牌、能量与执行期校验（已验证，未接生产）

- `GameConfig` 新增 `EnergyPerRound`，代码默认值、`DataTables/game-config.json` 与 `Assets/GameData/game-config.json` 均为 3；两份 JSON 内容一致。该配置是手写运行时规则，不是 Luban Excel 表，因此本切片未改工作簿或生成代码。
- `BattleCommandQueue` 现在把权威参与者、`CombatantId -> BattleCardZonesData`、Luban `Tables` 与每轮能量交给内部 `BattleTurnController`。`PlayCardCommand` 到达队首后依次校验阶段、玩家身份与存活、结束行动标记、玩家卡区、手牌实例、静态 `Card.Cost` 和当前能量；成功才把指定实例移入弃牌堆并扣该玩家能量，失败只返回明确执行原因且不发布新事实。
- 公共 `Submit` / `Queue` / `Turn` seam 的相关 EditMode **18/18** 通过：新增覆盖费用 1+2 顺序归零、首张牌展示期间另一玩家继续提交、旧能量重校验防透支、排队期间卡牌离手、敌人冒充玩家、死亡玩家、缺少玩家卡区和缺少静态模板；M4A 原有顺序与重复回调行为继续通过。
- `Assembly-CSharp` 与 `Assembly-CSharp-Editor` 静态编译均为 0 error（保留 6/12 条既有依赖版本冲突 warning），Unity 刷新后 Console Error 为 0。`TinySpire/Addressables/Build Local Content` 已完成；报告 `buildlayout_2026.08.01.01.41.10.json` 的 `BuildError` 为空、哈希为 `4877b4655f41f300d0ffc1bb4c37fb25`、耗时 `52.562s`，并确认 `Assets/GameData/game-config.json` 仍以完整稳定地址进入 `TinySpire GameData`。Bootstrap 短时运行打印“game-config.json 已加载。”，Error 与 `InvalidKey` 均为 0，随后正常退出 Play Mode；模式切换期间仅有 MCP 传输 warning。
- 未修改 `BattleSession` 初始抽牌、`BattleLifetimeScope`、场景、Prefab、asmdef 或 UI，也未实现真实 Effect、结束玩家行动或敌人交接；未运行 Luban、全量 EditMode 或完整 BattleScene 功能实跑。M4B 已满足独立停止点，M4C～M4E 尚未开始。决策见 CD-028，计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，验收见 `06_testing/2026-08-01-m4b-queued-card-play-energy.md`。

## 2026-08-01 · M4A 权威命令队列与回合事实骨架（已验证，未接生产）

- 新增纯 C# `BattleCommandQueue` 调度根、四类首批命令、提交/执行结果与只读 `Queue`/`Turn` R3 事实；本地权威序号从 1 单调递增，当前命令执行和等待表现期间都可继续提交，只有绑定当前序号的表现完成回调可以推进下一条。
- `BattleTurnController` 保持队列内部，通过既有 `StateMachine<TEvent>` 组合 `NotStarted -> BattleStart -> PlayerRoundStart -> PlayerAction`；回合事实按 `CombatantId -> PlayerTurnData` 保存，M4A 能量骨架为 0，没有 `CurrentPlayer` 或全局 `CurrentEnergy`。
- TDD 通过公共 `Submit` / `Queue` / `Turn` seam 完成 9 个 EditMode 用例：覆盖未开始拒绝、执行期与等待期提交、稳定序号、FIFO 交接、重复表现回调、重复开始执行期失败、后续里程碑命令不改共享事实及双玩家独立映射。Unity MCP 定向测试 9/9；`Assembly-CSharp` 与 `Assembly-CSharp-Editor` 静态编译均为 0 error（保留 6/12 条既有依赖版本冲突 warning）。
- Unity MCP 已为新增目录、脚本和测试生成全部 Meta；Console Error 为 0。未修改 `BattleSession` 的现有初始抽牌，未接 `BattleLifetimeScope`、场景或 UI，也未实现能量扣除、出牌结算、结束玩家行动或敌人行为；未运行全量 EditMode、PlayMode、Luban 或 Addressables 构建。
- M4A 已满足独立停止点；M4 总计划仍为 active，M4B～M4E 尚未开始。实现计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，验收记录见 `06_testing/2026-08-01-m4a-authoritative-command-queue.md`。

## 2026-07-31 · M4 多人命令队列口径修订（计划完成，尚未实施）

- 用户修订旧的“交错出牌/一张牌后切人”口径：所有未结束玩家可同时提交命令，提交不因其他玩家输入或当前效果展示而阻塞；权威调度层建立唯一顺序，再逐条执行共享状态修改和效果展示。全部玩家的结束命令均执行后才进入敌人阶段。
- 外部查证区分了原版《杀戮尖塔》的行动队列与《杀戮尖塔 2》的多人模型。当前结论锁定逻辑上的统一权威顺序，不声称未来必须使用单一物理 FIFO；研究记录见 `04_research/2026-07-31-slay-the-spire-action-queue.md`。
- M4 外部 seam 改为 `BattleCommandQueue.Submit` 与只读 `Queue`/`Turn` 事实；`BattleTurnController` 退为队列内部阶段模块。提交接受与执行成功分离，最终合法性在队首执行时重新校验。
- M4A 改为权威命令队列与调度骨架；M4B 为队列化出牌、能量与执行期校验；M4C 为队列化结束行动、敌人交接和生产接线；M4D 接当前单玩家 UI；M4E 全量验证。TDD seam 同步改为命令提交及队列公开事实。
- `DEP-008`/`DEP-009` 保持；新增 `DEP-010` 记录命令中途局部输入续接，`DEP-011` 记录未来网络权威确认与重放。本轮只修改文档，未修改代码、场景、配置或资源，未运行 Unity 测试。
- 完整计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`，代码决策见 CD-027，架构约定见 AC-009。

## 2026-07-31 · 牌面短键与 Addressables 逻辑地址迁移（已验证）

- `battle.card.xlsx` 已由 `illustration_address` 迁移为 `illustration_key`，策划只填写不带目录和扩展名的文件短名；ClosedXML 临时副本比较确认除 H1、H4、H5:H8 外，值、公式、样式与版式均未变化。Luban 已生成 `Card.IllustrationKey` 和对应 JSON。
- 四张动态牌面已集中到 `Assets/Arts/Runtime/Card/Illustrations/`，原 `.meta` GUID 全部保留。运行时统一把短键转换为 `card-art/{key}`；构建工具按文件短名建立不区分大小写的索引，阻止重名、缺失引用和非 `Sprite / Single / no mipmap` 资源。
- `TinySpire Card Art` 继续使用本地 `PackTogether` AssetBundle，但四个条目地址已改为 `card-art/*`。最终 `TinySpire/Addressables/Build Local Content` 成功（6.7 秒）；定向 EditMode 4/4、全量 EditMode 38/38、静态编译 0 error，四个逻辑地址均可通过 Addressables API 加载 Sprite。
- BootstrapScene 短时启动未出现 Error、`InvalidKey` 或资源地址错误。未修改图片像素、卡背、Prefab、场景、战斗逻辑及其他资源配置字段。实现见 `plans/2026-07-31-card-illustration-logical-keys.md`，验收见 `06_testing/2026-07-31-card-illustration-logical-keys.md`，决策见 CD-026。

## 2026-07-31 · DataTables 工作簿简易配色（已验证）

- `DataTables/Datas/` 下 10 个 `.xlsx` 已统一使用低饱和配色：首行深蓝底白色粗体，Luban 类型行浅蓝、分组行浅灰、说明行浅金；内容区按列循环使用蓝、绿、金、紫、橙、青六组淡色，并用同色深浅交替区分相邻数据行。没有新增、删除或改写任何单元格内容、公式、字段、表定义或共享字符串。
- OpenXML 写入在临时副本中完成，逐工作簿对比配色前后语义 SHA-256，10/10 一致；所有 XML、关系文件与样式索引均可解析。Luban 生成成功，生成目录中的 55 个 C# / JSON 文件前后内容哈希变化为 0。
- Unity MCP 回归先后暴露四张牌面的磁盘导入模式不一致；已通过当前 Editor 将 strength、strike、defend、bash 全部统一为 `Sprite / Single / no mipmap`，未改牌面地址或图片像素。最终定向 EditMode 1/1、全量 EditMode 35/35 通过，清理 Test Runner 结果写入提示后 Console Error 为 0。
- 最终 `TinySpire/Addressables/Build Local Content` 成功，报告 `buildlayout_2026.07.31.20.39.59.json` 的 `BuildError` 为空，构建哈希为 `f347180971402fb852359628813c07b2`，耗时 `8.911s`。本次没有新增代码决策；详细记录见 `06_testing/2026-07-31-datatables-simple-colors.md`。

## 2026-07-31 · 战斗 UI 首批美术与牌面配置链路接入（已验证）

- 按 `10_communication/2026-07-30-battle-ui-art-brief.md` 接入当前已有运行时事实能够承载的 P0/P4 素材：BattleScene 三个牌堆计数改用共用九宫格面板及抽牌/弃牌/消耗图标；`ParticipantHudView` 改用生命框、横向填充与力量图标。P1-P3 所对应的能量、回合、敌人意图、状态与结算覆盖层没有提前创建占位状态。
- `DataTables/Datas/battle.card.xlsx` 新增 `illustration_address`，四个模板使用完整 `Assets/Arts/Runtime/Card/card_art_*.png` 稳定地址；Luban 已重新生成 `Card.IllustrationAddress` 与 `Assets/GameData/battle_tbcard.json`。
- 四张牌面已统一导入为 `Sprite / Single / no mipmap`。`AddressablesBuildTools` 从生成卡牌表收集并校验地址，使专用 `TinySpire Card Art` 本地组与表中地址完全同步；`HandCardContainer` 按牌组唯一模板预加载并在销毁时释放句柄，`HandCardVisual` 让横图等比 cover 插图区后交给现有 Stencil Mask 裁切。
- Unity 6000.5.5f1 当前 Editor 内完成编译、定向测试 1/1、全量 EditMode 35/35、最终 Addressables 本地构建（19.026 秒）与 Bootstrap→BattleScene 实跑。初始 5 张手牌均加载到对应牌面，显示尺寸为 `862.5×575`、遮罩为 `682×575`，比例无拉伸；Console 错误、`InvalidKey` 与牌面加载失败均为 0。
- 实现边界和回滚见 `plans/2026-07-31-battle-ui-art-integration.md`，验收细节见 `06_testing/2026-07-31-battle-ui-art-integration.md`，资源事实与生命周期决策见 CD-025。

## 2026-07-30 · CardView 旋转插图灰边修复（待 Unity 人工验收）

- 用户报告手牌扇形布局中，只有旋转卡的插图区出现灰色边缘。代码与 Prefab 静态检查确认：`HandCardVisual` 旋转 `CardContent`，而 `IllustrationMask` 使用轴对齐的 `RectMask2D`，导致其子节点 `Illustration` 被错误裁剪并露出下层 `CardBase`。
- `CardView.prefab` 已将 `IllustrationMask` 的裁剪组件替换为 `Mask`，保留既有 `Image`、尺寸、卡图资源与层级，并关闭 `Show Mask Graphic`，使模板裁剪区域随卡片旋转。
- 新增 CD-024、实施方案与验收记录；未修改 C#、手牌布局参数、场景、数据表、资源地址或 Addressables 配置。
- 当前检测到用户正在使用 Unity，未启动第二个 Editor 或批处理实例；请在该 Editor 执行 `TinySpire/Addressables/Build Local Content`，并在 BattleScene 检查左右倾斜卡、悬停归零和拖拽时是否还会露出灰边。

## 2026-07-30 · M3B 抽牌堆/弃牌堆计数 HUD 实施（待 Unity 人工验收）

> - 新增 `BattleCardPileHudView`：它仅订阅 `BattleSession.CardZones.Layout` 与 `LocalizationService.LocaleChanged`，从已发布布局的 `DrawPile.Count`、`DiscardPile.Count`、`ExhaustPile.Count` 即时派生三个底部计数文本；没有新增计数、卡区列表或卡牌归属的镜像状态。场景 `BattleCardPileHud` 已置于主 Canvas 底部左右两侧，并由 `BattleLifetimeScope` 注入。
> - 新增 `battle.card_pile.draw.name` / `battle.card_pile.discard.name` / `battle.card_pile.exhaust.name` 三个 Excel i18n key（en：`Draw Pile` / `Discard Pile` / `Exhaust Pile`；zh-CN：`抽牌堆` / `弃牌堆` / `消耗牌堆`）。本地化校验器把它们纳入必需运行时 key，防止表格遗漏后静默运行。相应 String Table 已与 Excel 编辑源同步。
> - `DataTables/gen.bat` 已成功执行；两套程序集串行静态编译均为 0 error（保留 6/12 条既有版本冲突 warning）；工作表、场景 GUID 与 `git diff --check` 均已检查。新增 `BattleCardPileHudPresentationTests` 已编译，Unity EditMode 与实际场景验收尚未执行。
> - 未实施 M3C～M3E：它们分别依赖 M4 回合/能量、M5 意图及 M7～M9 的效果与结算事实，不能先以 UI 占位状态替代。需在当前 Unity Editor 执行 `TinySpire/Build/Sync and Build All` 后从 Bootstrap 人工验收 M3B，详见 `06_testing/2026-07-30-battlescene-card-pile-hud.md`。

## 2026-07-30 · M3A-1/2 参与者配置与 Prefab 工厂实施（待 Unity 验收）

> - `battle.Hero`、`battle.Enemy` 已新增 `name_i18n_key` 与 `view_prefab_address`；Test Warrior 与 Test Slime 分别指向现有玩家、敌人 Prefab，名称写入 `i18n.xlsx`。Luban 已生成对应 C# 与 `Assets/GameData` JSON。
> - 本地化导入/校验现在覆盖 Hero、Enemy 名称；Addressables 配置工具会把两个角色 Prefab 放入 `TinySpire Characters` 本地组，地址仍是表中的完整 `Assets/...` 路径。
> - 已实现 `BattleParticipantPresenter` 与 `EnemyCombatantLayout`：一名玩家、1–3 名敌人按 Encounter 顺序自右向左等距实例化；场景销毁时以 `ReleaseInstance` 释放。`BattleSession` 显式保留遇敌顺序，未依赖字典遍历顺序。
> - 本轮实跑曾暴露 VContainer 选择参数最多的非公开 `BattleSession` 构造函数，导致尝试解析本不应注册的 `BattleCombatantsData`。`BattleLifetimeScope` 已改为显式工厂，仅解析 `ConfigService` 与 `BattleSetupOptions` 后调用正确的公共构造函数。
> - 定向用例覆盖遇敌顺序、两/三敌布局、容量与间距错误；既有程序集 `dotnet build` 为 0 error（6 条既有程序集引用冲突警告）。运行中的 Unity 尚未将新文件刷新进生成的 `.csproj`，且当前存在用户的 `BattleScene.unity` 改动，因此未启动第二个 Editor、未修改场景、未运行 Unity EditMode 或 Addressables 构建。待在现有 Editor 执行 `TinySpire/Build/Sync and Build All` 后完成 M3A-1 内容验收；场景挂载与实跑属于尚未开始的 M3A-4。

## 2026-07-30 · M3A-3/4 HUD 与 BattleScene 接线实施（待 Unity 人工验收）

> - 新增 `ParticipantHudView` Prefab/组件：它只保存参与者事实、世界 Sprite、Canvas 与本地化服务的引用，不复制生命、力量或语言状态；Health/Strength/Locale 变化分别驱动展示重派生。名称投影在角色头顶，生命条与 `当前 / 上限` 投影在脚下，力量为零时隐藏。
> - `BattleParticipantPresenter` 现在同时创建并销毁角色 Addressables 实例和对应 HUD；HUD 构建失败会立即释放已生成角色，Presenter 销毁时也会显式释放两类 View。场景中的 `BattleLifetimeScope` 已挂载 Presenter，并接入既有 Player/Enemy Anchor、主 Canvas 与 HUD Prefab；Scope 注册该场景组件以完成 VContainer 注入。
> - 本轮未改动战斗表、翻译正文或角色 Addressables 配置。因现有 Unity Editor 正在被用户使用，尚未启动第二个 Editor 或重建 Addressables 本地内容；需在该 Editor 执行 `TinySpire/Build/Sync and Build All` 并从 Bootstrap 实跑 BattleScene，确认 HUD 和 Console 后再完成验收。

## 2026-07-30 · M3A HUD 前景渲染与素材名修正（待重建本地内容）

> - 人工实跑确认 HUD 的世界投影位置正确，但现有 Screen Space - Camera Canvas 的 `Plane Distance = 100` 位于世界背景之后。BattleScene 已将其改为 `1`，使该 Canvas 位于相机近端、背景之前；未改变角色或背景的 Sorting Layer。
> - Hero 1001 与 Enemy 2001 的名称语义取自实际 Sprite 的关键词：英文为 `Sisyphus`、`Warden`，中文为 `西西弗斯`、`典狱长`。稳定 i18n key 不变，Excel 编辑源、Unity String Table 与运行时读取链保持一致；之后仍需由现有 Unity Editor 重导入本地化并重建 Addressables。
> - `DataTables/gen.bat` 已成功生成；两套程序集静态构建均为 0 error（分别保留 6/12 条既有版本冲突警告），`git diff --check` 通过。尚未由 Unity 菜单重建 Addressables 本地内容，也尚未进行修正后的人工实跑。

## 2026-07-30 · M3 BattleScene 主 HUD 与参与者视图 grilling 完成

> - 已确认 M3 按运行时事实拆为 M3A-M3E；当前只规划 M3A 的参与者世界视图与生命 HUD。M3B 牌堆计数可复用已完成的 M2 卡区事实，M3C 能量/结束回合等待 M4，M3D 意图等待 M5，M3E 格挡/状态/死亡/覆盖层等待 M7-M9。
> - M3A 的静态模板将新增 `name_i18n_key` 与 `view_prefab_address`。名称进入现有 `i18n.xlsx` 和 Unity Localization；角色 Prefab 作为 Addressables 资源从表中指定的完整 `Assets/...` 地址加载。
> - 已确定 `BattleParticipantPresenter` 负责 BattleScene 内的实例与 HUD 生命周期：按 `CombatantId` 绑定，世界 Sprite 与 UGUI HUD 分层；单玩家、1-3 敌人按 Encounter 顺序自右向左布局。地址/加载/Prefab 合约错误直接抛出，不做占位或回退。
> - M3A 只显示名称、生命和非零力量；生命为零时仅刷新数值，尚不实现死亡、格挡、状态、意图、能量、回合、胜败或 Effect。完整设计见 `plans/2026-07-30-battlescene-participant-views.md`，决策见 CD-023。
> - 本轮仅完成设计与文档沉淀；未修改表格、Addressables、场景或运行时代码，未产生新的测试结果。

## 2026-07-30 · i18n Excel 编辑源接入与一键构建验收

> - 新增 `DataTables/Datas/i18n.xlsx`（`i18n` sheet，`key`、`en`、`zh-CN`、`smart`）作为翻译正文的编辑源；初始内容与既有 Strength、Strike、Defend、Bash 及共享关键词一致。
> - 新增 `I18nExcelReader` 和 `TinySpire/Localization/Import Battle Card Text from Excel`。导入后校验 Excel 覆盖运行时所需 key，并确认 String Table 的正文/Smart 标记与 Excel 一致；运行时仍只通过 Unity Localization 读取。
> - 新增 `TinySpire/Build/Sync and Build All`：依次执行 Luban 生成、Unity 资源刷新、Excel 导入与校验、Addressables 本地构建。已由用户在 Unity Editor 内执行并确认通过；决策见 CD-022。
> - `dotnet build` 为 0 error（12 条既有程序集引用冲突警告）。一键入口已完成 Luban、Excel 导入、本地化校验与 Addressables 本地内容构建，M2A 的 Excel 内容管线验收完成。决策见 CD-021。

## 2026-07-30 · 本地化文本唯一来源收敛

> - 删除 `LocalizationBuildTools` 中硬编码的 `LocalizedEntry[]`、配置/补全菜单及其写表辅助函数。`Battle Cards` Unity Localization 表资源现在是翻译正文的唯一来源。
> - 保留 `TinySpire/Localization/Validate Battle Card Text`，它只校验 locale、key、Smart String、参数和效果引用，不创建或覆盖翻译。
> - 新增/修改本地化内容的流程：直接编辑 String Table → 执行校验 → 重建 Addressables 本地内容。未修改任何翻译资源、Luban 表或运行时效果逻辑；决策见 CD-020。

## 2026-07-30 · 运行时数据命名与 R3 事实绑定修正

> - 运行时类型与文件统一改用 `Data` 尾缀：`CombatantData`、`PlayerCombatantData`、`EnemyCombatantData`、`BattleCombatantsData`、`CardInstanceData`、`CardZoneLayoutData`、`BattleCardZonesData`；`State` 留给未来状态机/状态模式。
> - 删除泛化 `Changed`/`Subject<Unit>`。生命、力量以只读 R3 属性公开；四卡区以不可变的完整 `CardZoneLayoutData` 原子发布。手牌 UI 订阅手牌布局、玩家力量与 Locale 的实际值，卡区移动不会向观察者暴露中间状态。
> - 验证：定向 EditMode 18/18、全量 EditMode 25/25 通过；BattleScene 实跑后 `BattleCardZonesData.Layout` 发布的手牌数为 4，`HandCardVisual` 也为 4，Console 无错误；`dotnet build` 为 0 error（12 条程序集引用冲突警告）；Addressables 本地内容已重建。决策见 CD-019；术语见 `CONTEXT.md`。

## 2026-07-30 · R3 通知绑定与 HandCardVisual 展示边界

> - 历史记录：当时曾将 `BattleState`、`CardZoneState` 与 `LocalizationService` 迁移为 `Subject<Unit>` / `Observable<Unit>`。该做法已由 CD-019 替代；`HandCardVisual` 的展示引用归属结论仍有效。
> - `CardView.prefab` 根节点现在序列化配置 `HandCardVisual` 的 Canvas、CardContent、标题、费用、类型和说明引用。容器不再按对象名扫描 `Text`，而是在 `HandCardVisual.Bind` 中写入展示值；语言和战斗事实变化仍只触发即时重派生，不保存文本镜像。
> - 验证：BattleState/CardZoneState EditMode 9/9、全量 EditMode 25/25 通过；`dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` 为 0 error（12 条既有程序集引用冲突警告）。运行时触发卡区变动与战斗伤害后，手牌事实数与延迟销毁后的 View 数均为 4，Console 无错误；`TinySpire/Addressables/Build Local Content` 成功完成。
> - 决策见 `CODE_DECISIONS.md` CD-018；M2A 仍不包含 Effect 执行、费用、目标选择、敌人行为或回合流程。

## 2026-07-30 · Addressables 迁移与 M2A 完成

> - 已移除 YooAsset 运行时/包/收集设置，建立本地 Addressables 场景与 GameData 组；启动、配置和场景加载改走 Addressables，完整 `Assets/...` 地址保持稳定。
> - `battle.Card` 已迁移为 name/description i18n key 与有序 `CardEffectBinding`；Luban 生成成功。
> - 已实现 Unity Localization 薄服务、Smart String 资源配置/校验工具、`CardTextFormatter` 与 `CardValueCalculator`；手牌 UI 在语言或战斗事实通知后即时重派生文本，不保存格式化字符串或显示伤害状态。
> - Luban、Localization 配置/校验和 Addressables 本地内容构建均已完成；校验器要求 `en`、`zh-CN`、共享关键词 key 与 `zh-CN → en` fallback，String Database 启用 fallback；`dotnet build` 0 error，Unity EditMode 23/23 通过。
> - Bootstrap → LoadingScene → BattleScene 实跑成功；GameData 正常加载，中文/英文动态卡牌说明正确，切换语言前后 5 个手牌 View 身份不变，Console 0 error、0 warning。
> - 本阶段仍未实现 Effect 执行器、费用、目标选择、伤害/格挡/易伤结算、远程 catalog 或第二套资源包。

---

## 2026-07-30 · 卡牌区域与确定性洗牌实施

> - 新增 `GameRandom`，以实例方式封装项目已存在的 `Unity.Mathematics.Random`；规则随机可读取/恢复 `uint State`，并通过 Fisher–Yates 洗牌，不使用 `UnityEngine.Random` 全局状态。
> - `HandState` 升级为 `CardZoneState`：全部卡牌实例由 `Cards` 字典定义，抽牌堆、手牌、弃牌堆、消耗区分别保存互斥的有序 `CardInstanceId`；不保存 `Zone` 镜像或缓存计数。
> - `BattleSession` 现在创建完整 10 张初始卡组，按战斗种子洗牌并抽取 `GameConfig.InitialHandCount` 张；`DEP-006` 已解决。当前种子仍来自 BattleScene Inspector，未来改由 Run 生命周期提供，登记为 `DEP-007`。
> - 手牌拖过出牌线的既有占位行为现在把指定实例移动到弃牌堆；未实现效果器、目标合法性、费用、回合调度、地图/奖励/敌人随机。
> - TDD 定向 EditMode 10/10、完整 EditMode 13/13 通过；dotnet build 0 error；Bootstrap 实跑为 10 个实例、抽牌堆 5、手牌 5、弃牌/消耗 0，Console 0 error，保留既有 LoadingScene handle warning。
> - 双轴代码审查最终通过；审查修正了随机流外部别名、视图冗余模板 ID 与旧文档过期状态，复核无新增 P1/P2。
> - 本轮未修改表格、生成 JSON 或 YooAsset 包，因此不运行 Luban/AB 重建。

---

## 2026-07-30 · 卡牌 i18n key 与动态说明设计补充

> - 路线图新增 M2A：卡牌名称/说明使用 i18n key，说明模板使用 `{damage}`、`{block}`、`{vulnerable}` 等命名参数，并允许不同语言调整语序。
> - 规划 `CardTextFormatter` 深模块：UI 只提交卡牌实例和可选来源参与者，模块内部解析 key、效果参数、关键词和动态数值；格式化文本不进入 `CardInstanceState`。
> - 说明显示值、目标预览值和实际结算值必须复用同一纯数值计算模块，只是上下文不同；三者均由配置和运行时事实派生，不成为第二份状态。
> - 当前未安装 Unity Localization，文本目录后端与 fallback locale 保持为实施前 Open Question。本轮只修改设计文档，未改表格、代码、生成数据、AB 包或效果器。
> - 详细设计见 `plans/2026-07-30-card-localized-text-design.md`。

---

## 2026-07-30 · 战斗配置接入运行时 + BattleScene MVP 路线图

> - 新增 `BattleSession`，由 `BattleLifetimeScope` 从英雄 1001、遭遇 5001、初始卡组和 `GameConfig.InitialHandCount` 创建玩家、敌人与手牌；`CombatantState` 接入模板基础力量。
> - `HandState` 改用唯一 `CardInstanceId` + `TemplateId`，解决初始卡组内重复 Strike 无法独立表示的问题。手牌 UI 读取同一运行时状态，并从 `battle.Card` 显示卡名和费用。
> - 未实现效果器、目标、费用扣除、牌堆、回合流程或敌人行为。正式牌堆前暂取卡组前 5 张，登记为 `DEP-006`。
> - 重写 `ROADMAP.md`：按 M0～M10 规划牌堆、主 HUD、回合、敌人意图/随机行为、出牌命令、效果器、完整循环和反馈；并以 G1～G8 承接主菜单、Run、存档、地图、奖励、遗物/药水、商店/事件和完整产品收尾。每阶段明确唯一事实、派生数据与验收标准。
> - 验证：EditMode 6/6 通过；dotnet build 0 error；Bootstrap → BattleScene 实跑生成 5 张独立 Strike，标题/费用绑定正确。本次无 error，保留一条既有 LoadingScene handle warning。

---

## 2026-07-30 · STS 战士初始卡组配置

> - `battle.Deck` 1001 设为 5×Strike、4×Defend、1×Bash；初始手牌 `game-config.json` 已是 5，保持不变。
> - `battle.Card` 由单个 `effect_id` 改为 `effect_ids` 列表，使 Bash 可表达“8 伤害 + 2 易伤”；新增敌方目标、伤害、格挡、易伤和空属性枚举项，仅作为静态配置。
> - Luban 生成成功，YooAsset `Main` 内置包已重建；Bootstrap 场景实跑控制台 0 error。运行时效果结算不在本轮范围内。

---

## 2026-07-30 · 战斗表 YooAsset 生成路径修正

> - Luban JSON 输出从 `TinySpire/Assets/StreamingAssets/GameData` 改为 `TinySpire/Assets/GameData`，与 `ConfigService` 的资源路径加载约定对齐。
> - 生成后重建 YooAsset `Main` 内置包；仅刷新 Unity 不会把新 JSON 写入离线清单。Bootstrap 场景实跑确认 `battle_tbhero` 加载不再报错。

---

# Daedalus · 会话日志

> 记录每次编程会话的关键产出、决策和待办。

---
## 2026-07-30 · 战斗静态配置表实施

- 在 `DataTables/Datas/__tables__.xlsx` 登记 6 张手工 schema 战斗表，并在 `__enums__.xlsx` 定义 `TargetRule.Self`、`EffectType.ModifyAttribute`、`Attribute.Strength`。
- 新增 `battle.hero.xlsx`、`battle.enemy.xlsx`、`battle.deck.xlsx`、`battle.card.xlsx`、`battle.card_effect.xlsx`、`battle.encounter.xlsx`；填入一套闭合最小样例：Test Warrior（30 HP）→ deck 1001 → Strength 卡牌（Self，+3 Strength），Test Slime（20 HP）和单敌人 encounter 5001。
- 模板表只保存稳定 ID 与设计数值；`CombatantId`、当前生命、存活、手牌/抽牌/弃牌堆、卡牌实例、临时费用、升级、敌人意图和控制者不进入配置。表间关系暂以 ID 表达，未实现 `ref` 校验或运行时导航。
- Luban 生成 `cfg.battle` 的 6 个记录类型、6 个表管理器、3 个枚举及 6 个 JSON；`#demo.item.xlsx` 按既有删除意图保持缺失，旧 demo 生成产物已随重新生成移除。战斗数据文件故意不使用 `#` 前缀，避免自动导入与手工 schema 重复。
- 验证：UnityMCP 资源刷新无编译错误；`dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` 为 0 错误（13 条既有程序集版本冲突警告）。详情见 `plans/2026-07-30-battle-static-config-tables.md`、`06_testing/2026-07-30-battle-static-config-tables.md` 与 CD-011。

---
## 2026-07-30 · BattleState 运行时参与者模型实施

- 新增纯 C# `TinySpire.Battle` 运行时模型：`CombatantId`、共同基类 `CombatantState`、`PlayerCombatantState`、`EnemyCombatantState` 与聚合根 `BattleState`。
- `BattleState` 是唯一持有 `CombatantId → CombatantState` 映射的事实源，并以只读字典 `Combatants` 暴露；按用户反馈删除了预置的玩家/敌人/存活派生视图和与 `TryGetCombatant` 重复的 `ResolveSelf`，未来只在真实目标规则出现时从字典值按需派生。未并存 `List` 作为索引或镜像；本次将原始 `List` 正式替换为该字典，`TryGetCombatant` 直接委托 `TryGetValue`。
- 初版共同可变事实仅为生命；`ApplyDamage` 修改目标参与者自身的当前生命，`IsAlive` 由该生命值派生，当前不预置存活视图。未接入 `HandState`、卡牌实例、Effect、敌人意图、能量、UI、场景锚点或 `BattleLifetimeScope`。
- 新增 EditMode `BattleStateTests`；字典调整后 UnityMCP 重新运行 3 项测试全部通过。`dotnet build TinySpire/TinySpire.sln --no-restore --verbosity:minimal` 为 0 错误（13 条既有程序集版本冲突警告）。测试结束后 Console 出现 YooAsset `AssetBundleCollectorWindow.RefreshWindow` 的既有编辑器包空引用（Undo 回调，未触及本次代码），已在验证记录注明。
- 决策见 `CODE_DECISIONS.md` CD-010；计划与验证记录分别见 `plans/2026-07-30-battle-runtime-state.md`、`06_testing/2026-07-30-battle-runtime-state.md`。

---
## 2026-07-30 · BattleScene 拖拽出牌（最小判定）验收完成

- 用户已在 Game View 完成并确认鼠标手势验收：拖拽保持抓取偏移且持续跟随；越过 `playLineY` 松手后卡牌销毁、其余手牌补位并显示透明度反馈；线内松手会回弹并恢复反馈。
- `拖拽打出最小判定 + 手牌数据归属权收回`（ROADMAP Phase 1）由“已实施，待人工手势验收”更新为“已完成并验证”。
- 已更新 `ROADMAP.md` 与 `06_testing/2026-07-30-battlescene-drag-to-play-minimal.md`；未修改代码、预制体、场景或 DEP-001～DEP-004 的未解决状态。

---
## 2026-07-30 · BattleScene LifetimeScope 实施

- 新增 `TinySpire/Assets/Scripts/Battle/BattleLifetimeScope.cs`：定义 sealed 的 VContainer 场景 Scope，`Configure` 保持为空，仅保留 `TODO(DEP-005)` 占位标记。
- 通过 Unity MCP/编辑器在 `BattleScene.unity` 根级创建并保存 `BattleLifetimeScope` GameObject，`parentReference` 设置为 `Bootstrap`。
- 未修改 `SceneFlowService.cs`、`Bootstrap.cs`，未实现回合调度器、抽牌堆、弃牌堆或相关抽象。
- 验证：`dotnet build TinySpire/TinySpire.sln --no-restore` 为 0 错误；Unity Play Mode 为 0 错误、0 警告，未出现 parent reference 警告。
- 验证记录：`06_testing/2026-07-30-battle-lifetime-scope.md`。

---
## 2026-07-30 · 战斗场景 LifetimeScope 结构 grilling（纯讨论，无代码改动）

- 用户提出未来战斗场景需要独立的回合调度循环、抽牌堆、弃牌堆，讨论是否需要专属 `LifetimeScope`、是否绑定场景生命周期。结论：需要，且应作为场景内挂载的 GameObject（不由代码动态创建/销毁），因为 YooAsset `LoadSceneMode.Single` 切场景时会自动销毁旧场景 GameObject，VContainer 的 `parentReference` 按类型全局查找父 Scope（`LifetimeScope.FindParent`），与 `SceneFlowService` 完全解耦，不需要改动 `SceneFlowService.cs`。
- 进一步讨论"未来加入地图"后的结构（Game → 存档 → 三张地图 → 具体事件），结论：不需要给每一层单开 DI Scope，只需要 3 层——Bootstrap（Root）→ RunScope（存档，跨场景持久，需要新的 `RunFlowService` 手动创建/销毁）→ 事件层场景 Scope（战斗/地图/商店，沿用场景挂载方案）；"地图"本身只是 `RunState` 里的字段，不需要单独 Scope。
- 确认了 `06_testing/2026-07-30-scene-child-scope.md` 描述的"`SceneFlowService.CreateChild` 动态创建子 Scope"方案已被用户撤回、代码已还原，该文件头部的 `source: CD-008` 是错误引用（当时 CD-008 从未真正存在）；已将该文件归档至 `99_archive/2026-07-30-scene-child-scope.md` 并更新其前言说明。
- 新增 `CD-008`（场景级服务用挂载在场景内的 `LifetimeScope`，不由代码动态创建/销毁）与 `CD-009`（存档层 `RunScope` 需要显式 `RunFlowService` 管理生命周期，前瞻记录、未实现）；`ARCHITECTURE_CONVENTIONS.md` 新增 Locked 的 `AC-006` 并在 Open 部分登记 `RunScope` 仍是前瞻性质。
- 按 CD-008/AC-006 产出 Codex 实施 Prompt（直接在对话中给出，未另存为文件）：仅创建 `BattleLifetimeScope`（挂在 `BattleScene.unity`，`parentReference` 指向 `Bootstrap`），`Configure` 暂空并标记 `DEP-005`；明确排除回合调度器/抽牌堆/弃牌堆的实际实现，不改动 `SceneFlowService.cs`。新增依赖项 `DEP-005` 登记到 `DEPENDENCIES.md`。
- 本轮未写任何 C# 代码，未创建 `RunFlowService`/`RunLifetimeScope`/`RunState`，仅做文档维护。

---

## 2026-07-30 · 最小状态机 Core 实施

- 新增纯 C# `TinySpire.Core.StateMachine`：状态包含 `Enter`、`Tick(TimeSpan)`、`Handle(event)`、`Exit`，状态通过返回值请求切换。
- 状态机不持有事件队列、不依赖 Unity/UniTask、不查找游戏运行时数据；Update/Tick 驱动和事件排队由外部负责。
- 支持状态跨多帧保持、同步事件分发、同一次 `Tick` 中后续状态使用零时间继续 Tick，以及不可重启的 `Stop()`。
- 本轮明确不实现 Context、嵌套状态、并行状态、异步调度和任何游戏领域接入，避免在缺少真实用例时扩展 Core。
- 验证记录：`06_testing/2026-07-30-state-machine-core.md`。

---

## 2026-07-30 · BattleScene 拖拽出牌（最小判定）实施

- 在 `TinySpire/Assets/Scripts/UI/Battle/Hand/HandState.cs` 新增纯 C# `HandState`：以占位 ID 初始化手牌列表，只暴露只读快照、`PlayCard(int)` 和 `event Action` 变化通知；不接入 R3、真实卡牌数据、费用或 BattleState。
- `HandCardContainer` 现在只以 Inspector `initialHandCount` 初始化 `HandState`；运行期张数从 `HandState.CardIds.Count` 得出。它订阅变化后销毁已打出卡的视觉对象并按状态快照重排其余卡牌。松手时以可调 `playLineY`（默认 240）判定；越线调用 `HandState.PlayCard`，未越线仍回弹。
- 拖拽坐标使用每帧 `PointerEventData.delta / Canvas.scaleFactor` 累加到当前锚点，不再把屏幕点换算到独立根 Canvas 的零尺寸 RectTransform；因此按下不跳中心，后续移动保持抓取偏移并持续跟随鼠标。
- `HandCardVisual` 使用 `CardContent` 上运行时添加的 `CanvasGroup` 做越线透明度反馈，并独立维护、终止其反馈 Tween；未修改 `CardView.prefab`。
- 按依赖台账添加 `TODO(DEP-001)` 至 `TODO(DEP-004)`：目标 ID 填充、费用、反馈样式、销毁前动作。没有实现目标、费用、效果、抽牌或弃牌逻辑。
- 验证：纯 `HandState` 检查通过；`dotnet build TinySpire/TinySpire.sln --no-restore` 通过（0 错误；9 条既有程序集版本冲突警告）；UnityMCP Play Mode Console 为 0 错误、0 警告。MCP 无指针事件注入，最终鼠标拖拽手势需人工确认。
- 验证记录：`06_testing/2026-07-30-battlescene-drag-to-play-minimal.md`。

### 后续动作

- 已完成：用户在当前 Game View 中人工确认移动不跳中心、越线销毁补位、线内回弹和透明度反馈。

---

## 2026-07-29 · 拖拽出牌（最小判定）grilling + 计划产出

- 确认杀戮尖塔式手牌 UI 已由 Codex 实施完成（见上一条会话日志与 `06_testing/2026-07-29-battlescene-hand-ui-sts-style.md`），但拖拽当前不能判定出牌。
- 用 `grilling` 技能逐项确认了最小可行的出牌判定（可调 Y 轴出牌线），并在过程中发现 `handCount` 应该从 UI 组件里收回归属权，因此新增了最小 `HandState` 纯 C# 聚合类的设计。
- 确认本轮不做目标选择、不做费用检查、打出后立即 `Destroy`（无过渡动画），拖过出牌线只加最简占位视觉反馈。
- 按用户建议引入了一套“依赖项 ID”机制（DEP-001~DEP-004），写进计划文档，并要求未来实现时在代码里用 `TODO(DEP-xxx)` 标记对应位置。
- 产出实现计划：`plans/2026-07-29-battlescene-drag-to-play-minimal.md`（proposal，未实施代码）。
- 新增代码决策 CD-005（HandState 收回数据归属权）与 CD-006（拖拽出牌判定机制）。
- 本轮未写任何 C# 代码，未 commit。配套 Codex Prompt 直接在对话中给出，未另存为文件。

### 下次会话

- 若 Codex 产出代码，需核对：HandState 是否真正持有数据且 UI 无自行自减、四个 TODO(DEP-xxx) 是否都写到了代码里。
- 验收通过后补充一条 `06_testing/` 验证记录。

---

## 2026-07-29 · BattleScene 手牌 UI 实施

- 在 `TinySpire/Assets/Scripts/UI/Battle/Hand/` 新增纯扇形布局计算、手牌容器、单卡视觉动画与 UGUI 事件转发脚本；手牌数量保持为 Inspector 的临时 `int` 占位字段，并在字段处标明未来仅替换为 Luban 数据来源。
- 通过 UnityMCP 创建 `TinySpire/Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab`，并在 `BattleScene.unity` 引用它。预制体保存手牌容器和 Inspector 配置；运行时仅复用（未修改）`CardView.prefab` 创建数量可变的独立根 Canvas 卡牌及其交互组件。实现扇形/溢出间距压缩、悬停抬起、独立 Canvas 层级提升、拖拽跟手、拖起后的补位与松手回弹，不含任何出牌判定或数据接入。
- 每张卡的 `HandCardVisual` 独立保存 Tween，并在新动画前终止旧 Tween；悬停采用 `Ease.OutBack` / 0.15 秒，补位与回弹采用 `Ease.OutCubic` / 0.22 秒。
- 静态验证：`dotnet build TinySpire/TinySpire.sln --no-restore` 通过（0 错误；9 条既有第三方程序集版本冲突警告）。场景文本检查未发现 `Toggle` 或 `ToggleGroup`。
- UnityMCP 已连到 `TinySpire@8edf130c865b3957`；Game View 验证了 3 / 5 / 10 张扇形布局与 10 张间距压缩，最后一次 Play Mode Console 为 0 错误、0 警告。未重启、未结束任何用户 Unity 进程。
- 修正扇形旋转方向：布局旋转改为 `-t × maxFanAngle`，使左右卡牌的轴线朝手牌下方汇聚；纯布局测试先复现左 `-15°` / 右 `15°` 的错误方向，再验证为左 `15°` / 右 `-15°`。UnityMCP 干净重启 Play Mode 后，Game View 视觉确认扇轴朝下，Console 为 0 错误、0 警告。
- 验证记录：`06_testing/2026-07-29-battlescene-hand-ui-sts-style.md`。

### 后续动作

- 需在 Game View 手动确认 hover 与拖拽交互手感；本次 UnityMCP 无指针事件注入能力，未伪造该两项结果。

---

## 2026-07-29 · 手牌 UI 杀戮尖塔化 grilling + 计划产出

- 用 `grilling` 技能逐项确认了手牌 UI 从 CD-002 的静态 Toggle 单选，升级为杀戮尖塔式悬停抬起 + 扇形排布 + 拖拽跟手视觉（本轮不做出牌判定）。
- 确认用户已将 DOTween/DOTweenPro 导入 `TinySpire/Assets/Plugins/Demigiant/`；确定悬停/重排补位的时长与缓动曲线参数。
- 手牌数量来源经用户确认后改为：本轮不引入接口抽象，直接用 Inspector 可调 `int` 字段，注释标记为未来 Luban 数据驱动的临时占位。
- 产出实现计划：`plans/2026-07-29-battlescene-hand-ui-sts-style.md`（proposal，未实施代码）。
- 新增代码决策 CD-003（DOTween 引入）与 CD-004（交互模型替换 CD-002），未删除旧记录。
- 本轮未写任何 C# 代码，未改动 `BattleScene.unity`，未 commit。用户计划将计划 + 配套 Prompt 交给外部 Codex 实施。

### 下次会话

- 若 Codex 产出代码，需核对实现是否符合本计划的 10 条决策，尤其是“不做出牌判定”的边界是否被越界。
- 验收通过后补充一条 `06_testing/` 验证记录。

---

## 2026-07-12 · BattleScene 基础手牌 UI

- 在 `TinySpire/Assets/Scenes/BattleScene.unity` 的现有 Canvas 下新增 `BattleCardUI`：包含底部手牌托盘、5 个 `CardView` 实例和单选高亮。
- 卡牌选择使用 UGUI `Toggle` + `ToggleGroup`；本轮只构造表现与可点击状态，没有新增运行时代码，也未接入卡牌数据、ViewModel 或出牌逻辑。
- 将现有 Screen Space - Camera Canvas 的 `planeDistance` 从 100 调整为 1，避免 UI 平面落在背景 Sprite 后方而被完全遮挡。
- Unity Game View 目视验证通过；EventSystem 点击第二张卡后，第一张取消选中、第二张进入选中状态；Console 0 错误、0 警告。
- 实现计划：`Docs/Copilot_Daedalus/plans/2026-07-12-battlescene-card-ui.md`；验证记录：`Docs/Copilot_Daedalus/06_testing/2026-07-12-battlescene-card-ui.md`。

---

## 2026-07-12 · LoadingScene 最短展示时间

- `SceneFlowService` 在 LoadingScene 完成切入后开始计时，保证目标场景切换前至少展示 1 秒。
- 内容准备耗时计入这 1 秒；仅补足剩余时间，不给慢加载额外增加固定等待。
- 补足延迟不受 `Time.timeScale` 影响。
- `dotnet build TinySpire.sln --no-restore` 通过（0 错误、3 个既有程序集版本冲突警告）；Unity Editor 当前存在运行实例，未启动额外实例进行 Play Mode 验证。
- 验证记录：`Docs/Copilot_Daedalus/06_testing/2026-07-12-loading-scene-minimum-duration.md`。

---

## 2026-07-06 · 初始化

- 创建 `Copilot_Daedalus/` 工作区，确立与 Pegasus 的协作约定
- 项目处于 planning 阶段，尚未开始编码

### 当前状态

- Unity 项目路径：`../TinySpire/`（相对于 `Docs/`）
- 现有代码：仅 `Assets/Scripts/Launcher.cs`
- BattleScene MVP 待实现（见 `Hermes_Pegasus/STATUS.md` P0 列表）

### 下次会话

- 阅读最新 `AGENT_HANDOFF.md` + `STATUS.md`
- 根据 P0 优先级制定 BattleScene 实现计划

---

## 2026-07-08 · 协作体系与文档库初始化

### 设计讨论（proposal，未落事实源）

- 起点讨论：从纯 C# 内核倒着往外长（计算 → 状态 → 时序 → UI），先不铺框架
- character 数据：确认 `模板 / 运行时` 两层；运行时持模板引用 + 只存会变字段
- `maxHp / currentHp` 同类两字段，约束 `current ≤ max`；max 变化时 current 是否同步 = **Open Question**
- 数据管线选型：**Luban + JSON 输出**（承重基础设施，提前定合理）；Theseus 去接入
- Open Question：max 变化时 current 同步规则；游戏 asmdef 布局（暂定"一个游戏 asmdef + 一个 Test asmdef"）

### 协作体系（对齐 AI_COLLABORATION_RULES.md）

- 四角色确认：Theseus（拍板）/ Pegasus（设计·数值）/ Calliope（创意·文本，Gemini）/ Daedalus（实现）
- Gemini 正式名从讨论中的 Urania 定为 **Calliope / 卡利俄佩**

### 文档库产出

- 新建 `AGENT_PROMPT.md` — 调用 Daedalus 的 Prompt 模板（6 节）
- 拆分身份/导航：新建 `AGENT_PROFILE.md`（身份），`README.md` 重写为 llm-workflow `index` 路由页
- 新建 `AGENTS.md` — 文档库入口 + llm-workflow 角色本地化映射
- 按 llm-workflow bootstrap 初始化本库：index-first ✅、status source = 本文件 ✅
- **完整实例布局初始化**（每个 AI 各维护一份 llm-workflow）：新建 8 个角色目录
  `00_inbox` `01_requirements` `04_research` `06_testing` `07_retrospective` `08_tools` `10_communication` `99_archive`，各带 keeper README；
  已有文件就地充当角色：`README`=index、`SESSION_LOG`=dev-log、`plans/`=design、`CODE_DECISIONS`=decision（事实源不移动）；`09_meetings` 不适用未建

### 记录的文档冲突（待 Theseus 裁决，未覆盖）

1. `.github/instructions/TinySpire.instructions.md` 仍是两人叙事（Pegasus+Daedalus），与四人体系不一致
2. 主库 `dev` 分支与 `Pegasus_Docs` worktree 存在同名文件双份，本次改动落在**主库 dev**

### 下次会话

- 待 Theseus 确认上述 proposal / Open Question 后，制定 BattleScene 首个实现计划
- Luban 接入完成后，落地 `CharacterTemplate` 表 → 生成 C# 类的目录/程序集归属
