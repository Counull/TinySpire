---
title: M10 BattleScene MVP 对标、可靠性与内容扩展入口
page_type: plan
lifecycle: completed
created: 2026-08-05
updated: 2026-08-05
status: m10-complete-non-m10-suite-failures-recorded
scope: BattleScene MVP 的配置 fail-fast、数值和本地化黄金基线、确定性与生命周期回归、最终交付验证
status_source: ../SESSION_LOG.md
source: ROADMAP.md 第 5 节 M10；当前 ConfigService、GameData、LocalizationBuildTools 与 M9 完成记录
---

# M10 · BattleScene MVP 对标、可靠性与内容扩展入口

## 当前结论

本页是 M10 的唯一实施计划。M10 不扩展玩法；它把已经闭环的单场战斗变成可审计、可配置、可重复验证的 MVP 收口。实施必须严格串行执行 M10A → M10B → M10C → M10D。每个切片完成自己的自动验证、文档同步和独立停止点后，才可开始下一个切片。

M10A 已完成配置原子性与表清单 fail-fast；M10B 已完成 Bootstrap 可见失败路由与默认内容黄金基线；M10C 已完成 Submit/只读事实的确定性、帧率无关与生命周期回归；M10D 已完成交付审计与环境化性能基线。完整 EditMode 的两项 M9 UI/Targeting 套件异常已独立复现、记录且确认不依赖 M10 变更，不伪报为全绿，也不阻断 M10 的相关回归收口。本计划保留后续切片的实施边界，当前可验证状态以 `SESSION_LOG.md` 为准。

## 已观察基线

以下事实来自当前工作区，不是新的数值决定：

- `ConfigService` 以手写的八项 `TableNames` 预加载 `Assets/GameData/battle_tb*.json`；`game-config.json` 加载、解析或填充失败时会记录 warning 并创建带默认值的 `GameConfig`，这与路线图要求的 fail-fast 相冲突。
- 当前生成内容的对标基线为：初始手牌 5、每回合 3 能量；战士牌组 5×Strike、4×Defend、1×Bash；Strike 6 伤害、Defend 5 格挡、Bash 8 伤害和 2 易伤；英雄生命 30，两个默认敌人生命均为 20。
- `LocalizationBuildTools` 已能检查 en/zh-CN、卡牌参数、参与者名称和部分战斗 HUD 文案，但其“必需 key”清单仍是手写的局部集合；M10 只扩展为配置驱动的构建前覆盖检查，不另建第二套文本源。
- M9 已交付命令、结算、表现、重开和退出闭环。`BattleCommandQueue.Submit` 仍是唯一共享写入口，`Queue` 和 `Turn` 仍是只读事实；M10 不改变这些契约。

## M10 完成定义与硬边界

M10 的完成不等于增加新牌、新状态或 Run。它要求以当前默认内容证明：

1. 任何必需配置地址缺失、JSON 损坏、缺表或无效规则都会原子地进入可见的 Bootstrap 失败路径；不能留下部分 `Tables`/`GameConfig`，也不能静默使用代码默认值继续进入 BattleScene。
2. 默认战斗的牌组、数值、角色/敌人基础值、两种语言文本及其生成产物具有可回归的黄金断言，并由现有表格、生成内容和 Localization 资产共同驱动。
3. 相同启动选项和种子在 30/60/120 FPS 表现推进下拥有相同的权威战斗轨迹和终态；重复进入、重开和退出不保留旧 `BattleSession`、订阅、Tween 或 Addressables 句柄。
4. 完成交付级的 EditMode、PlayMode、Bootstrap、Addressables、真实 Game View、构建、范围审计和文档收口；性能只报告可重复测得的基线，不在未给定硬件/帧时间/分配预算时伪造性能阈值。

明确不纳入 M10：RunState 或新种子来源（DEP-007）、多人/Party（DEP-008）、命令中途选择（DEP-010）、网络（DEP-011）、Exhaust 归宿规则（DEP-012）、新状态/新 Effect/新目标、奖励/遗物/地图/主菜单、队列/回合/结算公式重写、动画系统重构，以及 Targeting 和 Candidates 源美术。

## 串行切片

### M10A · 配置原子性与表清单 fail-fast

**交付物**：`ConfigService` 只在全部必需表和 `game-config.json` 成功加载、解析并通过最小结构校验后一次性发布 `Tables` 与 `GameConfig`。缺地址、坏 JSON、根节点形状错误、缺必需字段、重复/遗漏表或生成表清单与运行时清单漂移时，返回带地址/表名/原因的可诊断启动失败，不回退至 `GameConfig` 代码默认值。

**实施状态（2026-08-05）**：已完成。`ConfigService` 通过内部文本加载 seam 建立测试 fake，先在局部构造 `Tables` 与 `GameConfig` 后一次性发布；新增 `ConfigInitializationException` 分类地址、表名与原因。`TinySpire/Build/Sync and Build All` 现会在生成/刷新后、Local Content 前比较 Luban `__tables__.xlsx`、生成 `Tables.cs`、`Assets/GameData` JSON 与运行时清单。精确红灯、相关 EditMode、真实当前清单校验与 solution build 见 `../06_testing/2026-08-05-m10a-config-fail-fast.md`；本切片没有 Bootstrap 可见失败路由。

**实现口径**：

- 保留 Addressables 读取边界；不把地址加载散落到战斗模块。
- 把配置加载结果先保存在局部变量，成功后才写入 `ConfigService` 属性，失败后保持未初始化状态。
- `TableNames` 不再是无校验的隐式真相。优先以构建期/Editor 校验比较运行时必需清单、`Assets/GameData` 生成 JSON 与 Luban 表定义；若生成器已能稳定产出 manifest，才可替换为生成 manifest，不能同时维护两份独立可漂移清单。
- 本切片只建立 typed failure 与纯加载契约；不改 Bootstrap Scene，也不假设异常日志本身就是用户可见失败界面。

**允许路径**：`TinySpire/Assets/Scripts/Core/ConfigService.cs`、`GameConfig.cs`、最小新增的 Core 配置错误类型、`TinySpire/Assets/Editor/**` 的配置构建校验、对应 Editor 测试与 `Docs/Copilot_Daedalus/06_testing/`。

**排除路径**：`DataTables/Datas/**`、`Assets/GameData/**`、Localization 资产、Scene、Prefab、`Battle/**` 规则、`UI/Battle/**`、Addressables 配置和 Candidates/Targeting 美术。

**独立验收与停止点**：先写 fake `AddressableAssetService`/可替换加载边界的红灯测试，覆盖成功、缺单表、坏数组/对象、坏 GameConfig、缺字段、加载后重试及“失败不发布半成品”。再运行构建期清单校验和相关 EditMode。M10A 通过后，配置失败尚未有 Bootstrap 可见路由；不得提前把它声称为完整启动失败体验。

**回滚单位**：仅回滚本切片的 Core 配置原子性与 Editor 校验文件；不会影响现有生成 JSON、数值或战斗契约。

### M10B · Bootstrap 可见失败路由与默认内容黄金基线

**交付物**：将 M10A 的已分类启动失败接到最小、可见且不可继续加载 BattleScene 的 Bootstrap 失败路径；同时把默认数值与两语种文本固定为配置驱动的黄金基线，并在表格/生成/Localization 变更前阻断不一致内容。

**实施状态（2026-08-05）**：已完成。`GameLauncher` 仅在配置初始化阶段捕获 `ConfigInitializationException`，把它转交给 Bootstrap 上按需创建的最小失败 View，并且不再继续 Localization 或首场景加载；未知异常保持上抛。失败 View 只显示稳定 `CFG-001`～`CFG-007`、资源地址和重启指引，不引入重试、MainMenu、Run 或第二场景流。新增 Editor 回归同时读取作者 Excel、生成 JSON 与 Unity String Table，锁定 5/3、5×Strike/4×Defend/1×Bash、6/5/8/2、30/20 和 en/zh-CN Smart String 文本；`LocalizationBuildTools` 已把运行时战斗流程 key 纳入门禁。定向 M10A+M10B EditMode 21/21、Localization 菜单校验和 solution build 均通过；唯一 Editor 的正常 Bootstrap 已进入 BattleScene。未修改任何表格、GameData、Localization 或可寻址内容，故未重跑 Luban/Local Content。失败资源不以临时损坏项目内容制造 Game View 截图，自动 typed-failure 路由证据与其边界见 `../06_testing/2026-08-05-m10b-bootstrap-golden-baseline.md`。

**实现口径**：

- `GameLauncher` 只编排启动顺序；它捕获并转交已分类的配置初始化失败，不吞掉未知异常，也不继续调用 `LoadInitialSceneAsync`。
- Bootstrap 失败展示只显示稳定的失败码/资源地址和重新启动指引；不增加 MainMenu、Run、重试写入或第二条场景流。
- 黄金测试从 `DataTables/Datas` 的权威内容、Luban 生成的 `Assets/GameData` 与 Unity Localization 表三者读取，断言当前 5/3、5×Strike/4×Defend/1×Bash、6/5/8/2、30/20 以及 en/zh-CN 的 name/description/Smart String 参数一致性。
- 扩展 `LocalizationBuildTools` 的构建前检查以覆盖当前运行时会读取的卡牌、参与者和战斗流程 key；它只校验和同步现有 Excel → Localization 单一来源，不能把翻译复制回 C# 常量。

**允许路径**：M10A 路径、`GameLauncher.cs`、Bootstrap 的最小失败 View/Prefab/Scene 接线、`DataTables/Datas/**`、`DataTables/game-config.json`、生成的 `Assets/GameData/**`、`Assets/Localization/**`、现有 Localization 编辑器工具和对应测试。

**排除路径**：BattleScene 布局、角色/怪物/Targeting/Candidates 美术、`BattleCommandQueue`/`Turn`/结算、效果公式、新卡/新敌人/新状态、Run 与主菜单。

**独立验收与停止点**：先完成 M10A 后才写 Bootstrap 失败路由。对每种配置失败证明 Bootstrap 停在失败状态且不加载 Loading/BattleScene；对正常内容证明 Bootstrap 仍可加载 BattleScene。若本切片修改表格或 Localization，必须运行 Luban、Localization 同步和 `TinySpire/Build/Sync and Build All`，并确认完整 `Assets/...` 地址、Local Content 与启动加载链路无 InvalidKey。M10B 到此停止，不进入帧率或场景循环验证。

**回滚单位**：Bootstrap 失败路由与黄金内容校验可独立回滚；表格/生成/Localization 变更始终作为同一回滚单元。

### M10C · 确定性、帧率无关与生命周期回归

**实施状态（2026-08-05）：**已完成。新增测试专用回放夹具只经由 `BattleCommandQueue.Submit` 提交命令，并在表现完成后仅读取 `Queue`、`Turn`、`BattleSession` 和 `CardZones`，覆盖 30/60/120 FPS、加速、立即完成、取消、重启及 Scope/Scene 生命周期。定向回归 3/3、相关聚合回归 53/53 与 solution build（0 error；保留既有 12 条程序集版本冲突 warning）均通过；唯一 Unity 6000.5.5f1 Editor 的正常 Bootstrap Play Mode 进入 BattleScene 后存在一个 `BattleLifetimeScope`，退出后回到 BootstrapScene 且该 Scope 为零。未修改生产 Queue/Turn/settlement、规则、Scene、Prefab、DI、DataTables、Localization、Addressables 或受保护路径。完整证据见 `../06_testing/2026-08-05-m10c-determinism-lifecycle.md`；M10D 尚未开始。

**交付物**：建立只消费公开事实的战斗回放/轨迹测试，证明同一默认启动选项与种子在 30/60/120 FPS 下拥有相同命令、`BattleSettlementRecord.Order`、牌区、参与者、Turn/Queue 终态；补足重复进入、重开、退出和取消后的 Session/订阅/Tween 清理证据。

**实现口径**：

- 回归夹具从 `BattleCommandQueue.Submit` 提交命令，只读取 `Queue`、`Turn`、`BattleSession`、`CardZones` 和既有表现完成信号；不调用 listener 伪造系统输入，不打开内部写入口，也不建立镜像状态。
- 30/60/120 FPS 只改变表现推进/帧时间安排，不能改变命令顺序、随机流或领域结果。现有 `BattleCommandPresentationRunner` 的 ManualUpdate、`SetSpeed` 与 `CompleteImmediately` 只能被验证，不能因本切片重排 cue 或加第二个动画队列。
- 生命周期检查以真实 Scope/Scene 销毁后的公开行为和对象/订阅计数为证据。若必须新增诊断，应只提供测试可读且不改变权威写入的窄观测 seam。

**允许路径**：相关 Editor/PlayMode 测试、必要的测试夹具、现有表现 runner 的测试支持、`BattleLifetimeScope` 的最小只读诊断接线，以及 M10 测试/验收文档。

**排除路径**：配置表和 Localization 内容、游戏规则数值、Queue/Turn/settlement 公共契约、Targeting/候选美术、场景布局、Prefab、DI 体系重构、Run/网络/多人。

**独立验收与停止点**：每个帧率档都必须产生相同可比较轨迹；至少覆盖正常完成、立即完成/加速、取消、重开和 Scope/Scene 销毁。先跑 EditMode 的决定性轨迹，再以一个真实 PlayMode/Bootstrap 路径确认实际系统指针与场景退出。若证据要求改写 Queue、存储第二份事实或新增全局输入锁，立即停止并报告。

**回滚单位**：新增回归 harness 与只读诊断可以独立回滚；不得混入修复未知规则 bug 的生产改动。

### M10D · 交付级验证、性能基线与 M10 收口

**实施状态（2026-08-05）：** 已完成。M10D 自身的红灯、最小测试夹具、M10 聚合自动回归、solution build、静态 Addressables 地址检查和唯一 Editor 的默认 Game View/Console/短窗口 Profiler 均已取证；M9G 已验收的真实重开/退出链路在 M10 未改动相关路径的前提下复用。完整 EditMode 451 项中两项可独立复现的 M9 UI/Targeting 失败已作为非 M10 套件异常审计，不能写成全绿，却不阻断 M10 的相关回归。完整证据、环境、基线差异和边界见 `../06_testing/2026-08-05-m10d-delivery-validation.md`。

**交付物**：汇总前 3 个切片的生产验证，记录可重复的帧率、GC/对象清理和加载基线，完成 BattleScene MVP 的自动、真实 Game View 与文档收口。

**实现口径**：

- 先复用 M9 的验证链路，再新增 M10 专属的配置失败/成功、双语言、30/60/120 FPS、重开/退出/取消和连续场景进入退出证据。
- 性能记录必须标出硬件、Unity 版本、分辨率、测试脚本、采样窗口和测量工具；在用户没有给出目标帧时间、分配或设备预算前，仅报告基线和回归差异，不为“优化”改造模块。
- 若用户在执行时给出明确性能预算，单独以已记录预算为准；任何需要 Profiler 包、Player 设置、批处理 Editor、热更、程序集或构建管线变更的工作先停止确认。

**允许路径**：M10 测试、验收文档、必要的非持久验证脚本和证据目录；只有前置切片明确授权的生产文件可在对应切片中修改。

**排除路径**：所有新的玩法和 G1+ Run 路线、性能猜测性重构、配置/资源以外的内容扩充、Candidates/Targeting 源资源、ProjectSettings/asmdef/HybridCLR。

**独立验收与停止点**：运行相关 EditMode、PlayMode、solution build、Addressables（若 M10B 改了可寻址内容）、Bootstrap 成功与失败路径、真实 Game View 的默认战斗/重开/退出，以及范围审计。任何未确认的性能预算、需要第二个 Unity Editor 或额外高影响设置，均停止在已验证证据处并报告，不能把 M10 标为完成。

**回滚单位**：只回滚 M10 最终验证脚本/文档；发现的生产问题回到其所属切片，以新的红灯和最小补丁处理。

## 验收矩阵

| 目标 | 最低证据 | 不能替代它的证据 |
|---|---|---|
| 配置 fail-fast | M10A 的精确失败单测与 M10B 的 Bootstrap 失败路径 | 只有 Console warning 或静态代码检索 |
| 表格/文本黄金基线 | DataTables、生成 JSON、Localization 三方断言；变更时 Luban + Local Content | 只读 Excel 或 C# 常量 |
| 确定性 | 相同 seed/命令的 30/60/120 轨迹与终态一致 | 单次截图或 Unity 全局 Random 未报错 |
| 生命周期 | 重开、取消、Scope/Scene 销毁后的公开事实与清理断言 | 仅人工退出 Play Mode |
| 性能 | 有环境信息的可重复基线和差异 | 未标明设备/窗口的主观“流畅” |
| 最终交付 | 相关 EditMode/PlayMode、build、Addressables、Bootstrap、真实 Game View、范围审计与文档同步 | 计划、链接校验或代码审查本身 |

## 工作区、资源与停止规则

开始实施前必须重新记录 `git rev-parse HEAD` 和 `git status --short`，不假设本页所见基线仍然有效。当前已知的用户改动包括 `Docs/Copilot_Daedalus/07_retrospective/README.md`、`Docs/Hermes_Pegasus/art/asset-index.md`、`TinySpire/Packages/packages-lock.json`、M9 结构审查草稿、Hermes Candidates、Unity Candidates Meta/目录和 `TinySpire/.codex_work/`；它们都不在 M10 范围内。

始终保护且不修改、不暂存、不回退：

- `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/**` 及其 Meta；
- `Docs/Hermes_Pegasus/art/assets/art-style/scenes/candidates/**`；
- `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates/**` 和 `Candidates.meta`；
- 上述用户改动及任何新的未关联工作区改动。

如需改动 Queue/Turn/settlement/公式、引入第二份权威事实、增加 Run 或新种子、扩展到新玩法/新内容、替换 DI/场景启动结构、修改 ProjectSettings/asmdef/HybridCLR，或不能建立足够的真实验证证据，立即停止并报告预计文件、风险、回滚方式和所需用户确认。

## 文档与提交规则

每个切片完成后，在 `06_testing/` 建立对应验收记录，更新本计划、`plans/README.md`、`ROADMAP.md`、`DEPENDENCIES.md`（仅在真实 DEP 状态改变时）和 `SESSION_LOG.md`。新的代码决策才写入 `CODE_DECISIONS.md`；计划本身不是运行时证据。

没有用户的再次明确授权，不暂存、不提交、不推送。实施交接提示词见 `2026-08-05-m10-battlescene-conformance.codex-prompt.md`。
