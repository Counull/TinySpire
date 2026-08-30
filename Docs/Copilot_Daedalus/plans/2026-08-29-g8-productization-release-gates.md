---
title: G8 产品化与发布门禁实施计划
page_type: plan
lifecycle: archived
date: 2026-08-29
updated: 2026-08-31
scope: G8 only
status_source: ../STATUS.md
source: ../RUN_ROADMAP.md
implementation_status: accepted-with-waiver
---

# G8 产品化与发布门禁实施计划

> **归档状态：** G8-A～E 已完成并 `verified`；G8-F 由用户于 2026-08-31 明确豁免剩余人工验证后以 `accepted-with-waiver` 收口。完整 Victory、history exactly-once、Continue disabled、最终退出日志与性能均没有被写成通过。当前状态和后续授权只查 [STATUS.md](../STATUS.md)。

## 1. Goal 与唯一边界

本轮按用户当前 Goal 实施 `RUN_ROADMAP.md` G8-A～F，并在完成全部门禁后精确 commit、push `main → origin/main`。G7 已由 `80c6376 feat(run): complete G7 single-act outcome flow` 位于本地与远端；开始时唯一既有工作树改动为用户对 `Docs/Copilot_Daedalus/DEPENDENCIES.md` 的换行调整，本轮不覆盖、不规范化、不暂存该文件。

本轮把既有单 Act 竖切收口为 **Windows Standalone x64 单平台产品基线**，不改变 `RunStateStore`、`RunFlowService`、Battle setup/result、`RunOutcome` 或 SceneFlow 的权威边界。设置、教程、历史分别拥有独立于 Run save 的持久事实；View 继续只提交意图和渲染不可变 projection。

## 2. 杀戮尖塔 2 参考方式

参考只用于产品信息层级和交互原则，不复制受版权保护的素材、文本或实现：

- 主菜单保留清晰的「继续 / 新游戏 / 设置 / 统计」单层入口，设置按语言、显示、音频和可访问性成组，变更即时生效并可跨重启恢复。
- 教程采用按上下文逐步出现、可跳过、可重置的轻量提示，不让教程系统成为 Run/Battle 的第二写入口。
- 结果与历史以一次权威终局快照为输入，统计页只读取不可变历史，不在 UI 中自行累计。
- 信息优先级、键鼠焦点、确认/返回语法和减少动态效果优先于大范围装饰重做；TinySpire 使用自有纸张视觉与原创音频，不复制 STS2 美术、音频或文案。

实施与验收记录会保存本轮实际使用的官方/第一方参考链接和“借鉴原则 → TinySpire 落点”；无法由可靠来源核实的 STS2 细节不作为规格。

本轮已核对的第一方证据：

- Mega Crit 的 [Slay the Spire 2 官方公告流](https://steamcommunity.com/app/2868840/announcements/)（检索于 2026-08-29）明确记录 first-time tutorial popup、settings startup reliability、`settings.save` 与 Run History 的独立故障/同步处理，以及音频、VFX、非标准宽高比和 controller navigation 的持续修复；TinySpire 因此把这些能力拆为可单独失败、可单独验证的 owner/adapter，而不把它们塞进 Run save 或一个万能 UI 状态机。
- Mega Crit 的 [v0.110.0 Beta Patch Notes](https://steamcommunity.com/ogg/2868840/announcements/detail/694268118771435040) 记录 keyboard-only mode、输入重映射反馈、settings 非标准宽高比滚动条和高对比 selection reticle；TinySpire 本轮只声明现有产品链可证明的“键盘菜单 + 鼠标 Battle”，并把完整 keyboard-only/gamepad Battle 语法保留为后续独立切片，避免把参考方向误报成当前支持事实。

## 3. 冻结支持矩阵

| 维度 | 本轮支持 | 本轮不支持 |
|---|---|---|
| 平台 | Windows 10/11、Standalone x64 | Android、macOS、Linux、主机、PICO、多平台同时首发 |
| 输入 | 鼠标完成 Battle 拖拽/目标选择；键盘完成菜单导航、确认和返回；二者共同构成完整 Run | 手柄可保留既有菜单 UI action 探针，但不宣称可完成 Battle；完整手柄 Battle 输入语法另立切片 |
| 语言 | `zh-CN`、`en`，默认按有效持久设置，否则使用已初始化 locale/安全默认 | 全语种、配音 |
| 分辨率 | 1280×720、1920×1080、2560×1440；额外验证 1920×1200 与 2560×1080 的 16:10/21:9 适配 | 低于 1280×720、移动刘海屏产品承诺 |
| 显示 | 窗口、无边框全屏与分辨率持久化；2026-08-29 用户明确豁免本轮性能验证 | VSync/FPS 自定义、HDR、DLSS/FSR、复杂质量档 |
| 可访问性 | 100%/125% 文字缩放、高对比、减少动态效果；设置和必经页面无截断/失焦/不可达按钮 | 屏幕阅读器、色盲全套滤镜、按键重映射、语音控制 |

若实际 Player 证据证明某组合不成立，先修复当前适配；只有需要 Scene/Prefab/ProjectSettings、完整手柄语法或新渲染管线时才回到 `needs-grill`，不得通过悄悄缩小矩阵让测试变绿。

## 4. 公共 seam 与持久所有权

### 4.1 App settings

- 新建独立 `AppSettingsService` 与 versioned `app-settings.json` adapter；不使用 `PlayerPrefs`，不进入 `RunSaveDocument`。
- 不可变设置包含 locale、master volume、display mode、resolution、text scale、high contrast、reduced motion。
- 物理平台 adapter 是唯一可调用 `LocalizationService.SetLocale`、`AudioListener.volume` 与 `Screen.SetResolution` 的边界；领域测试只通过接口观察实际应用值。
- 加载缺失文件使用默认值；坏 JSON、未知 schema、非法字段和 I/O 失败返回类型化 degraded 结果并安全回退，不把半合法字段发布为当前设置。变更先原子落盘，再发布并应用；失败保持上一稳定设置。

### 4.2 Player profile、教程与 Run 历史

- 新建独立 `PlayerProfileStateStore` 与 versioned `player-profile.json`，只保存教程进度；Run History 使用 `run-history/{RunId}.json` 的逐局不可变文件。两者只复用 internal 原子文件 helper，不共享公开 owner、codec 或失败状态，使坏教程档不会拖垮历史，单个坏历史也不会重置教程。
- 教程步骤冻结为：主菜单欢迎 → Hero 选择 → 地图路线 → Battle 基础 → 奖励 → 非战斗节点 → 终局。提示只读取当前页面/场景，确认、跳过、重置只写 Profile，不直接写 Run/Battle。
- `RunHistoryService` 只订阅耐久终局成功后发布或冷恢复的 `RunStateStore.State`。每个 `RunId` 只生成一个不可变 `RunSummary` 文件；同 RunId 同内容重复写返回 AlreadyRecorded，同 RunId 不同内容返回 Conflict 且拒绝覆盖。结果页离开前必须确认该 summary 已原子写入或可重试，绝不从按钮点击自行拼一次统计。
- `RunSummary` 冻结 RunId、完成 UTC、Hero、Outcome、root seed、最终/最大生命、路径节点数、Battle attempt 数、牌组数、金币、遗物数和药水数。统计 projection 从历史派生总局数、Victory/Defeat/Abandoned 和按 Hero 分组，不保存第二份计数。

### 4.3 Presenter/View 与输入

- `RunEntryAction` 继续只承载 RunEntry 页面与 Run 意图。设置使用独立 `IAppSettingsView → AppSettingsPresenter → AppSettingsService` seam，教程 overlay 也使用独立动作接口；Statistics 只作为 RunEntry 的只读页面导航，不允许 View 直接写任何 Store。
- 每次页面切换都设置明确首焦点；Automatic/explicit Navigation、Submit 与 Cancel 可达。Cancel 只发布现有 Back/取消意图，不绕过 Presenter 页面门禁。
- RunEntry 使用 safe viewport + 1920×1080 reference policy；Battle 只做经矩阵证明必要的 runtime layout adapter，不改 Scene/Prefab。
- 文字缩放、高对比和减少动态效果由只读应用设置投影驱动；减少动态效果只改变时长/位移等表现，不跳过领域命令、结算或场景清理。

### 4.4 表现、音频与资源门禁

- 复用现有 RunEntry 纸张背景、Battle 正式图标/箭头/牌面与既有 DOTween 表现；Settings、Tutorial、Statistics、Outcome 不再显示功能占位。
- 新增原创最小 UI 音频域，专用目录、短键转换 `ui-audio/{key}`、唯一 Addressables Group 和构建期精确同步。至少覆盖 confirm、back、error 与 outcome；运行时只经 Addressables 加载，不使用 `Resources.Load`、`AssetDatabase` 或文件系统路径。
- 每个音频条目必须是可导入 `AudioClip`、大小写精确、无目录/扩展名业务短键且无忽略大小写重名；最新 BuildLayout 与 Use Existing Build/Player 必须证明 `AssetBundleProvider` 真实加载。
- 若原创音频生成工具不可用或质量/许可信息不能记录，G8-D 保持未完成并报告阻塞，不用占位 beep 冒充正式资源。

## 5. 串行 RED → GREEN 停止点

### G8-A · 应用设置

1. RED：默认/round-trip、坏 JSON/未知 schema/非法字段、原子 commit 失败零发布、重启恢复、Run save 字节不变。
2. GREEN：不可变 settings、codec/repository、Store、Unity platform adapter、Bootstrap 初始化顺序。
3. RED：RunEntry 设置动作 payload、不可达/过期动作零写入、locale/volume/display 实际应用。
4. GREEN：真实 Settings 页面与 projection；本切片通过后再进入 G8-B。

### G8-B · 输入、分辨率与可访问性

1. RED：每个必经页首焦点、Navigate/Submit/Cancel、声明分辨率下 safe viewport、100/125% 文字布局、高对比与 reduced-motion 投影。
2. GREEN：焦点/返回控制、布局 policy、可访问性应用器和设置 UI；不改 Battle 命令 seam。
3. UnityMCP 在声明矩阵中跑 RunEntry/Battle 高风险页面；不把“手柄菜单可导航”记作“手柄完整 Run”。

### G8-C · 首轮教程

1. RED：步骤顺序、重启续接、skip/reset、重复确认零写入、Profile commit 失败保持旧进度。
2. GREEN：Profile 服务、RunEntry modal tutorial projection 与 Battle 基础提示 adapter。
3. 从新 Profile 实际走一轮，证明教程不写 Run/Battle，skip 后不再阻断，reset 后从首步恢复。

### G8-D · 表现与音频

1. RED：Settings/Tutorial/Statistics/Outcome 无占位、reduced motion 行为、UI audio key/clip/group 精确门禁。
2. GREEN：现有自有视觉收口、原创 UI 音频、Addressables loader 与页面/终局反馈；按入口→地图/节点→奖励→Boss/结果→Battle 分批目视。
3. 运行 `TinySpire/Build/Sync and Build All`，保存 BuildLayout 与 Packed/Player 真实加载证据。

### G8-E · 统计与 Run 历史

1. RED：同一 Terminal 发布/冷恢复/重复订阅只写一次；commit 失败冻结同一 summary 并重试；非终局零写入；统计只从历史派生。
2. GREEN：summary factory、history service、Profile commit 与真实 Statistics 页面。
3. 分别验证 Victory/Defeat/Abandoned，重启后历史与统计一致，清理 Run save 不清理历史。

### G8-F · 发布验证矩阵

1. Settings/Profile schema、坏数据、`.tmp` 中断和 Run terminal journal 回归。
2. Rider solution build、G8 定向 Unity tests、fresh full EditMode、Luban/Localization、`Sync and Build All`、BuildLayout。
3. Use Existing Build 在声明语言、输入、分辨率和可访问性组合下跑关键链；Windows Player build 走完整单 Act 并回主菜单，Console Error/InvalidKey/ConfigInitializationException 为 0。
4. 原冻结的 Windows Player 性能采集项由用户于 2026-08-29 明确豁免本轮验证；不再采集或追踪当前源码 raw、FPS、frame time、working set 或 GC，也不把性能作为本次 G8-F blocker。被取消并删除的采样不进入验收记录。

## 6. 预计路径与高影响停止条件

预计新增/修改：

- `TinySpire/Assets/Scripts/Settings/**`、`TinySpire/Assets/Scripts/Profile/**`、`TinySpire/Assets/Scripts/Run/History/**` 与对应 `.meta`
- `TinySpire/Assets/Scripts/Core/Bootstrap.cs`、`GameLauncher.cs`
- `TinySpire/Assets/Scripts/UI/Run/RunEntryPresentation.cs`、`RunEntryView.cs`、`RunEntryLifetimeScope.cs`
- 最小 Battle tutorial/accessibility/audio adapter；不得改 `BattleCommandQueue.Submit` 或状态所有权
- `TinySpire/Assets/Editor/Tests/*G8*Tests.cs`、必要的邻接测试
- `TinySpire/Assets/Editor/AddressablesBuildTools.cs`、对应专用音频资源目录/Addressables 配置生成物
- `DataTables/Datas/i18n.xlsx`、Localization 同步资产与本轮实际生成/构建物
- `Docs/Copilot_Daedalus/` 的 plan、decision、testing、status、roadmap、session log/index

默认不修改 Scene、Prefab、asmdef、ProjectSettings、HybridCLR settings、Luban schema、Run save schema、Run/Battle DI 结构或启动场景。Bootstrap 仅注册并按现有顺序初始化新的应用/Profile 服务，不替换启动流程。实施中确认未固定版本的 HybridCLR package 会为 Unity 6000.5 选择错误的 6000.3 IL2CPP 分支，因此采用官方 `v8.14.1` 作唯一最小例外：只修改 `Packages/manifest.json` 与 `Packages/packages-lock.json`，lock hash 为 `a0e0b502c6c1b9ce2d0983181f4555e6149ae249`，不改变 `useGlobalIl2cpp=0`、ProjectSettings、程序集或 AOT/热更新边界。该 pin 的风险是工具链版本漂移，使用 tag + lock hash 固定；回滚单位为这两个 package 文件，但回滚会重新打开 Unity 6000.5 ABI/build 阻塞。若后续实现证明仍必须进入其他上述高影响文件、完整手柄 Battle 语法或新平台 SDK，立即停止对应切片并报告影响、风险与回滚单位。

## 7. 验收、恢复与 Git 交付

1. 每个切片保留真实 RED job/id、最小 GREEN 与相邻回归；不得用 G7 的 1410 数量冒充本轮结果。
2. 保护并在验收后恢复用户原 `run-save.json`、`app-settings.json`、`player-profile.json`（若存在）、Addressables active builder；BootstrapScene 与其他 Scene 最终 `dirty=false`。
3. 更新 `STATUS.md`、`SESSION_LOG.md`、`CODE_DECISIONS.md`、`RUN_ROADMAP.md` 与新 `06_testing/2026-08-29-g8-productization-release-gates.md`，分别审计 G8-A～F。
4. `git diff --check`、工作树与生成物清单审计后，只 `git add` 精确 G8 路径；明确排除用户 `Docs/Copilot_Daedalus/DEPENDENCIES.md`。
5. commit 前展示 exact payload；本 Goal 已授权完成后的 commit/push，目标固定为当前 `main` 到 `https://github.com/Counull/TinySpire.git` 的 `origin/main`。提交与远端 push 结果分开报告，并用远端 ref 核验。

## 8. 明确不做

云同步、成就、遥测、商业化、联网/多人、多平台同时首发、全语种、全量配音、大型过场、完整手柄 Battle 输入语法、按键重映射、第二份 Run/Outcome store、Run save schema 改造、Scene/Prefab/asmdef/ProjectSettings/HybridCLR settings/DI 架构重构均不在本轮。第 6 节记录的 `v8.14.1` package pin 只修复默认工具链兼容，不扩大该排除边界。

## 9. 实施结果与当前停止点

| 切片 | 当前状态 | 本轮结果 |
|---|---|---|
| G8-A · 应用设置 | `verified` | 独立 AppSettings owner、严格 versioned JSON/原子提交/坏数据回退、真实 Settings UI 与重启恢复均已闭合；平台 Apply 失败采用磁盘/平台双补偿，补偿不完整时 sticky `RecoveryRequired` 且后续变更 fail-closed；Run save 不承载设置。 |
| G8-B · 输入/分辨率/可访问性 | `verified` | 冻结支持矩阵、首焦点/返回、safe viewport、100/125%、高对比与 reduced motion 已覆盖；紧凑地图节点采用名称/身份独立区域与 autosizing 的 overflow policy，动态地图先 untrack/detach 再延迟销毁，不把退休控件重新纳入可访问性缓存。 |
| G8-C · 首轮教程 | `verified` | Tutorial 46/46；当前源码 Player 的 fresh Profile 实际覆盖 skip→restart、reset→Welcome、Welcome→Hero→Map→Battle。Map/Battle 教程确认前后 Run save 的 hash、长度和时间戳不变，Profile 只记录教程步骤；skip/reset 不创建 Run 或 History。 |
| G8-D · 表现与音频 | `verified` | final-review 后 `Sync and Build All` 成功；fresh BuildLayout/BuildReport 证明 address-only 四 cue 由专用 `AssetBundleProvider` bundle 打包。当前源码 Development Player 隐藏无输入启动越过四 cue 初始化并到达配置后置标记，目标加载错误 0。 |
| G8-E · 统计与 Run 历史 | `verified` | final review 的首次 Load 失败冻结、pending 全摘要重试比较和 StatisticsChanged 逐观察者隔离/诊断均由 fresh History/Statistics/UI Audio **38/38** 与 full EditMode **1611/1611** 覆盖。 |
| G8-F · 发布验证矩阵 | `accepted-with-waiver` | 静态/Unity/BuildLayout、UI Audio real load、新 Profile 教程、M1～M10 设置/重启/UI 矩阵、当前源码 Defeat 产品链及当前源码 Release Player build 均已通过；真实鼠标出牌推进到首战 Round 4。用户明确豁免其余人工完整 Victory、history exactly-once、Continue disabled 与最终退出日志，均为 `waived / not run`；性能同为 `waived / not run`。 |

final Standards review 修复：

- History 在首次 `Load` 返回 unavailable 时立即冻结本次 `RunSummary`；后续 pending 重试用冻结完成时间重建并逐字段比较，终局事实漂移或同事实但不同完成时间的并发 durable summary 均返回 Conflict。
- `StatisticsChanged` 以 invocation list 逐观察者发布；单个观察者异常不遮蔽已耐久的 Record，也不阻断后续观察者，并保留最近异常或 AggregateException 诊断。
- UI Audio 构建门禁要求 importer `preloadAudioData=true`；专用 Group schema 强制 `IncludeAddressInCatalog=true`、GUID/labels=false，使运行时 catalog 只暴露四个逻辑 address。
- 对应回归测试已由已连接的唯一 Unity Editor 执行：History/Statistics/UI Audio job `4610ad8d0a274969a311acd6d251d56d` 为 **38/38 passed、0 failed、0 skipped，1.1416752s**；fresh full EditMode job `fe2d343ea283455b99a89a1b658bf8f7` 为 **1611/1611 passed、0 failed、0 skipped，159.330777s**。此前 batchmode licensing 阻断发生在测试前，只保留为环境诊断，不再构成验证缺口。Rider build `6c5046e2-6cce-49cb-b888-c3f73697e378` Completed/success/problems=[]，四个关键文件 errors 0。

final-review 后当前源码资源与 Player 证据：

- `TinySpire/Build/Sync and Build All` 成功；Console 明确记录 Addressables content built 与 sync/local content build completed。fresh `Library/com.unity.addressables/buildlayout.json` 及归档 `BuildReports/buildlayout_2026.08.29.20.28.09.json` 均为 150922 bytes、SHA-256 `838FA2FD924E855ABC49EB944317812635AFE8275CAD7DF55508A2E9DF8AB1EB`，`BuildError` 为空、catalog hash `732f207a2793f9afced440fa0ad2987f`。
- `TinySpire UI Audio` 为 PackTogether、address=true/GUID=false/labels=false；`ui-audio/hover|click|confirm|error` 四条目精确进入 `tinyspireuiaudio_assets_all_8b688eddfc5efe6f113bdee36bdda27c.bundle`，`BuildStatus=0`、Provider=`UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider`；物理 bundle SHA-256 `2F92390B7DAA5786EC1C162179C5C3AE0BA8B4CC806DCCCE055046EC05B34D77`。
- 当前源码 clean Development Player job `build-a17ae188b3` 为 `StandaloneWindows64 / succeeded`、errors=0、warnings=490、402.8325836s、2046.43 MB，输出 `TinySpire/Temp/G8DevelopmentPlayerFinal/TinySpire.exe`。EXE、`GameAssembly.dll`、`boot.config` SHA-256 分别为 `51ACC9182CC45B00A3838E7C633D056330C0B7DD4F396502EA75E111367C5F3D`、`3E369CD53EEB89B87118BCF7CAE602F3F7A95FB02C6C20714E3C16442327E0D3`、`6E8CD5EC25235A6AF99EC679C92315908FAC7E72018417F4EEC678474067E5A8`。
- 该 Player 以隐藏 `-batchmode -nographics` 且无鼠标/键盘注入启动。生产链先顺序 Addressables 加载四个 UI Audio，再加载配置；日志到达后置标记 `game-config.json 已加载。`，`InvalidKey`、UI Audio load failure、Unhandled、NullReference、FileNotFound、OperationException 与 Failed-to-load 扫描均为 0。日志 SHA-256 `11ED4986CDAE8C2AF2E76379BEF21BA10DEDD1CE41AFA62B50295C25BBF5770B`。settings/profile/两份 history hash 与数量均未变，run-save 继续不存在。
- 2026-08-31 fresh 当前源码 Release Player job `build-38ba3bf544` 为 `StandaloneWindows64 / Release / succeeded`、errors=0、warnings=489、350.8749483s、1888.26 MB，输出 `TinySpire/Temp/G8ReleasePlayerFinal3/TinySpire.exe`。EXE、`GameAssembly.dll`、`boot.config` SHA-256 分别为 `74155F5299D9F6173E902E08D9CACD511A2AF7217A69B230F68769137E1DB0A3`、`9FCB1BBF91D2E9C818FA1CF8D1583183DAC334B174AA10685B9BD465F7BE9419`、`E69AC58A65DED81DEA2677F7D5DDACAEE72054AF24725C221CC5CC4F89707124`。
- Release 内容中的 `catalog.bin`、catalog hash 文件与 `settings.json` SHA-256 分别为 `B872BFFD6D9B97D809F15C5D76B25D675C69BD414B95DB339AE0650C06832F8F`、`1EF1EC51F6E095234FC5A0C43F1B07E5723658AE66731A0D945F70589B790FCD`、`63EE54D0556991F46C9C6A182D37C295D23DACB695ECFFA65A8D30E88ABCE32E`；UI Audio bundle SHA-256 仍为 `2F92390B7DAA5786EC1C162179C5C3AE0BA8B4CC806DCCCE055046EC05B34D77`。
- 该 Release Player 以 `en / Windowed / 1920×1080 / 125% / high contrast / reduced motion` 实际启动并进入 Run `5a117682-2bf2-4187-9032-1890524a7e49`；真实鼠标提交到 `Completed #12 · PlayCard` 后推进至首战 Round 4。中间 Player.log 为 3150 bytes、SHA-256 `FB5A27D1D4350887A8C04EDCB9DD7A1B6F9BEA7EF4D244120DCDCA5CFD6F9236`，到达 `game-config.json 已加载。`，`InvalidKey`、`ConfigInitializationException`、UI Audio、Unhandled、NullReference、FileNotFound 与 OperationException 扫描均为 0。它只证明中间产品链，不等于完整 Victory 或最终退出日志。

此前自动化与构建证据（final-review 修复前）：

- 设置事务补强保留 RED `e45b9d8771b3455aa1c839b8aaa42071` / `25af4049b5c9467e89f834f8179068df`、GREEN `1c73b018cf734349a4a81b3cf89d1a9a` **18/18**；Presenter + RunEntry targeted `a0deeefbb0e24e3cae612069466ba264` 为 **33/33**。
- 地图 overflow 历史补强保留 RED `07c04ffd1ade4a07906b998bae6baa10` / `826054fc60d6439198d27862308768d5`、GREEN `975c55298ced499dae34cb5cfc289ed2` 与 focused `42fac711e7c845d5a71bfda1a9c5b702` **25/25**；动态缓存生命周期最终 RED `b415c203b7e74b64b5475e98b84e313b`、GREEN `f4fd43d62e9341269c69650927736d3c`，相邻回归 `4624253e080e4748b25259fbe6d9dcb8` 为 **22/22**。
- History 统计快照早期补强保留 RED `bb175319dbab4a30bc93aa531ae29857`、GREEN `b6daef6df30e46898e0de5e7e414be77`，相邻 History 15/15、Statistics Presenter 8/8；Rider build `977de2d6-10d1-4e29-af34-137a28d21044` success/problems 0，fresh EditMode job `b8efa7e5fc84495b8189a011db0d8d39` 为 **1605/1605**、201.1325655s。它们早于上述 History final-review 修复，不再承担当前 G8-E 验证。
- `Sync and Build All` 当时成功且 Console Error 0；BuildLayout 2026-08-29 04:24:55 SHA-256 `C53DEAB42C7D0583E4BB9FF6F82D4F33A08DD351796F0D8E1D181C7105985133`、BuildError 为空，UI Audio 四地址由 `AssetBundleProvider` 打包。该证据早于 importer/catalog schema 收紧，不再承担当前 G8-D 验证。
- HybridCLR package 固定为官方 `v8.14.1`，lock hash `a0e0b502c6c1b9ce2d0983181f4555e6149ae249`；Installer 与 `HybridCLR/Generate/All` 成功。final-review 前 clean Development Player job `build-a607c859f5` success/errors 0、warnings 489、439.5431838s、2058.75 MB。EXE、GameAssembly、`boot.config` SHA-256 分别为 `51ACC9182CC45B00A3838E7C633D056330C0B7DD4F396502EA75E111367C5F3D`、`591BACDA85E3C1D5613729A6417647C1CA887933DEEE1FC162DE1C89C6A33030`、`1AB6F153B2CD6BD7267C1CBA577F8CC1DDE5F2262BDE2D0A521C7F805C98AF25`。
- 该 final-review 前 Player 已覆盖启动、设置、新 Profile 教程 skip/restart/reset、Hero、Map、Battle、Defeat、Statistics 与返回主菜单。Battle 通过三次 End Action 进入 Defeat，结果页返回主菜单后 Run save 清除；新历史 SHA-256 为 `8461F44B86ED7F053945DD95A3278149A1E345CAB6FC143B28CFE0988AAF1CC3`，Player.log `8232CFF1532ABAEAE37D2A79F3EB559D01281AA5BF1F360EB41BF93E69FE97AE` 且目标错误 0。它继续支持未变的 G8-A/B/C 产品口径，不能替代 G8-D/E 当前源码重验或 G8-F Victory。
- 新 Profile 实际产品证据分别保留 skip 后 profile SHA-256 `856C8EDCA63FC02624969DA44C8A58EFE392D3AA8647BBBF47B5C94CF561698F`、reset 后 `A9C125D81DF6DCFE7364D9D23A08084AB466A58032641AF6D97727F81377355C`、Welcome/Hero/Map/Battle 确认后 `F5B0D1134CA73E481C5162E9EC36F593917C54BE65E5FFDFE4E020C2823E421D` / `16C655C8FD4C0206A22C3BAE1C865E33E0CD8EC4AAF8DBF2F0A7D32C06A0C3AC` / `56DD9C33799DC9AFB41FD5494AB481994B8684AC92FA11D67D5E22A9153F89C3` / `3649ED21C5AB97277B5A1C4BEE9B6A7DB1743655252CB03118B2206C72B42B7B`。Map/Battle 教程确认前后 Run save hash、长度和时间戳均不变。

10 行发布矩阵：

| ID | 显示 | 分辨率 | 语言 | 文字 | 高对比 | reduced motion | `app-settings.json` SHA-256 |
|---|---|---:|---|---:|---|---|---|
| M1 | Windowed | 1280×720 | en | 100% | off | off | `BDCA3590D30979CDA5E536C1020B7D111B2C66A89018B7335A9C9F468D32D8DA` |
| M2 | Windowed | 1920×1080 | zh-CN | 100% | off | off | `6BEC18D4E728C1E954B62A6481B007D2BFF0B8A9513DC80EDB71C9BBD0D3399A` |
| M3 | Windowed | 2560×1440 | zh-CN | 125% | off | off | `2D55B86E0685AA7928D4D6D5FDE7C9432AE06756C64D6D26EA8A260DAE5036C7` |
| M4 | Windowed | 1920×1200 | zh-CN | 125% | on | off | `1DCB31574B2A31AD3D4476F17197197CDFB3ABFBBB6C0EF0462AEE4BF925505C` |
| M5 | Windowed | 2560×1080 | zh-CN | 125% | on | on | `CB46D64FDE71429B18CC0B131CD74DBBC10081ABC47B02A68ACC17BA03C8D433` |
| M6 | Borderless | 1920×1080 | en | 125% | on | on | `F667C1C47C10EE9EDB2C007DC314AF329C94B3EDDD74A765C76A8FFF580233E4` |
| M7 | Borderless | 2560×1440 | en | 100% | on | on | `2B91AC1BABD10E7C23285A8FFA8D963D2754CD887FEAFE424E511F3E2C2EEF95` |
| M8 | Borderless | 1280×720 | zh-CN | 125% | on | on | `0D4EDEE2974375EB8AEA3AD53F4FE50012BEB1509BEEB3B114983B8E85A81509` |
| M9 | Borderless | 2560×1080 | en | 100% | off | off | `F31F41BD52F16F9882164F82CF827A1702285A8CF06FAEE6D5D123EE9C1F2822` |
| M10 | Borderless | 1920×1200 | en | 100% | off | on | `09A47C90457DF7079573FFB4CAB2480961DAD4B1C7A98E6B281056358C3364D0` |

- M1～M10 均完成设置写入、正常关闭、重启恢复与 Settings 必经控件可达性检查；M3 额外覆盖 Hero/Map 的 125% 紧凑布局，节点无重叠且 End Run 可达。M2～M10 关闭日志 SHA-256 依次为 `8232CFF1532ABAEAE37D2A79F3EB559D01281AA5BF1F360EB41BF93E69FE97AE`、`D7EE3D748DE313A598E9B54F87C98B02822010A2118E7EEEFD20C17E0123D365`（M3 地图复核另为 `93B549CE1BBD7F2CE08036D1E99FE055B4F841E571FDF0D89E596B1272479EE8`）、`BDC8197AB81B21C4F5E147C5E5A047BF2541A23AF08DE84B9D333AF51EB04238`、`38F8555EBE7FC3B50EA634491CEFDBD10EE6F191502AE62F22FC5CC6D00F5258`、`E114307EEAB02E410AA4095C7BF694DE9553E38B8720E944313740510379FBEC`、`B40343345A977C9513AE1A5C6769BC73301AE02330EAF413FC80BE80BD5D1E61`、`EC7C7ECF767C67B73B34F4031A1187A9ACCEF485E6D28B0B6CD1BE0AF3CAB6E2`、`3D7C23EEF2229F1F97D8FF8A20A90F5224978B6C264AFE2174253DBE649E80BA`、`BC1836FDAF29C4CFD153DAEF9578CDB1FFC00333B9CE6F1BD2345AAA04615B1E`；所有目标错误与 InvalidKey 扫描为 0，退出时各有 2 条既有 `JobTempAlloc` 基线。
- Borderless 行已证明请求设置、持久化、重启恢复和 UI 可达；自动化环境未取得内部 `Screen.width/height/fullScreenMode` 精确值，因此该字段保持 `unproven`，但不单独阻断本轮。用户已允许鼠标操作；Windows Computer Use 的官方单次 drag 没有 duration/path/down/up，实测攻击牌和自目标牌均未触发 Unity UGUI 的跨帧出牌链。该限制只记为 `automation-environment unproven`，不是产品失败，且不授权增加 click-click、keyboard-only Battle 或改动既有输入 seam。
- 2026-08-29 用户明确要求不做本轮性能测试；当前源码 raw、FPS、刷新率、内存和 GC 均从剩余门禁与待办删除，被取消并删除的采样不作为证据。
- 最终环境已恢复并核验 persistent baseline：settings SHA-256 `D64C6A0CB47D6F8E01C30860507A295C2A52CC8280A088DE22A4ED5B6A2AA30B`、profile `3649ED21C5AB97277B5A1C4BEE9B6A7DB1743655252CB03118B2206C72B42B7B`、Defeat history `B03825476D73E9E7A7204F109AB13FFA3773589434ABB7DB975860EC22DAEAD8`、Abandoned history `AB30288F1C776468B12B0AEE7D6D397CCD41EB24133634DBDB50A9BCD2B9B2BB`；history count=2、`run-save.json` 不存在。未完成 Run 的原文件以 SHA-256 `37CD06C14F84595BAC76D33B9F7BDB2D1000A9418891084D5B468A4669F7299B` 保存在外部临时证据目录，未进入提交。

G8-F 人工验证豁免与最终停止点：

1. 当前源码 Release Player 已由真实拖拽推进到首战 Round 4，但没有完整跑通单 Act Victory，也没有核验 Victory history exactly-once、Continue disabled 或最终退出日志；这些事实没有证据。
2. 用户于 2026-08-31 明确要求“跳过需要人工验证的部分”，并要求完成后 commit/push。上述四个字段因此从本次交付阻塞项改为 `waived / not run`；该 waiver 只改变本次验收决定，不把缺失证据改写成通过。
3. 仓库没有合法的 full-Act runtime driver；Windows Computer Use 的官方单次 drag 也不能表达正式 Battle 所需的跨帧持续按下/移动/松开。本轮没有新增 click-click/autoplay/直接命令或伪造终局档，也没有改变 Battle 输入 seam。
4. 本任务启动的 Release Player 已结束；persistent baseline 四个哈希与 history count=2、`run-save.json` 不存在均通过复核，四个构建自动噪声文件也已精确恢复到 clean。

因此本计划归档；G8 Phase 为 `completed`，G8-A～E 为 `verified`，G8-F 为 `accepted-with-waiver`。人工完整 Victory、history exactly-once、Continue disabled、最终退出日志与性能均为 `waived / not run`，不得在后续记录中简写成通过。当前进入精确 commit/push 交付。
