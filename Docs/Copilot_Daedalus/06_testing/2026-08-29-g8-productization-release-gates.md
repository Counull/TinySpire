---
title: G8 产品化与发布门禁验收记录
page_type: testing
lifecycle: active
date: 2026-08-29
updated: 2026-08-31
scope: G8 only
status_source: ../STATUS.md
source: ../plans/2026-08-29-g8-productization-release-gates.md
implementation_status: accepted-with-waiver
---

# G8 产品化与发布门禁验收记录

## 1. 当前结论

G8-A～E 均为 `verified`。最终 Standards review 的 4 个 P2 已修复并由当前源码 Unity 定向 **38/38**、fresh full EditMode **1611/1611** 覆盖；fresh `Sync and Build All`、BuildLayout/BuildReport 与四地址 `AssetBundleProvider` bundle 均通过。Rider build `6c5046e2-6cce-49cb-b888-c3f73697e378` Completed/success/problems=[]。当前源码 Release Player `build-38ba3bf544` 构建成功、errors 0，并由真实鼠标出牌推进到首战 Round 4，中间目标错误扫描 0。用户于 2026-08-31 明确豁免其余需要人工操作的完整 Victory、history exactly-once、Continue disabled 与最终退出日志；G8-F 因此为 `accepted-with-waiver`，这些字段没有被写成通过。Player 已结束，persistent baseline 与四个构建噪声文件均已恢复。性能为 `waived / not run`；G8 Phase 按用户验收决定收口为 `completed`。

| 切片 | 状态 | 终审证据 |
|---|---|---|
| G8-A · 应用设置 | `verified` | 设置补偿/fail-closed RED → GREEN，AppSettings **18/18**；Presenter + RunEntry **33/33**；最终完整 EditMode 覆盖当前源码。 |
| G8-B · 输入、分辨率与可访问性 | `verified` | 地图 overflow 与动态缓存生命周期均保留 RED → GREEN；最终邻接 **22/22**；完整 EditMode 覆盖 untrack/detach 后当前源码。 |
| G8-C · 首轮教程 | `verified` | Tutorial 定向 **46/46**；当前源码 fresh Profile 实际覆盖 skip→restart、reset→Welcome、Welcome→Hero→Map→Battle，Map/Battle 教程确认前后 Run save hash/长度/时间戳不变。 |
| G8-D · 表现与音频 | `verified` | importer preload/address-only catalog 测试、fresh `Sync and Build All`、BuildLayout/BuildReport、物理 bundle 与当前源码 Player real load 全部通过。 |
| G8-E · 统计与 Run 历史 | `verified` | 4 个 final-review History/observer 契约由当前源码定向 **38/38** 与 full EditMode **1611/1611** 覆盖。 |
| G8-F · 发布验证矩阵 | `accepted-with-waiver` | M1～M10、当前 Defeat 产品链、fresh 当前源码 Release build 与真实鼠标 Submit seam 均有证据；完整 Victory、history exactly-once、Continue disabled、最终退出日志和性能由用户明确豁免，均为 `waived / not run`。环境恢复已完成。 |

修复后双轴只读复审均为 no findings：Standards 确认原 4 个 P2 全部关闭且无剩余 P0～P3；独立 Spec 确认 G8-A～F 当前实现没有规格偏离、漏 AC、越界或破坏 Run/Battle owner。该静态结论现已由下述 fresh Unity、资源构建和 Player 证据补齐。

### History 统计快照事件终审补强

- RED job `bb175319dbab4a30bc93aa531ae29857` 为 **0/1**，证明 `RunHistoryService` 仍只有无载荷 `HistoryChanged`，违反 AC-P001。
- GREEN job `b6daef6df30e46898e0de5e7e414be77` 为 **1/1**；owner 改为发布完整 `RunHistoryStatisticsLoadResult`，Presenter 缓存快照，locale 变化不再重读 repository。
- 相邻回归 `07f7ac2d6b044d86b4d40de2a5876482` 为 History **15/15**，`563eabdd796f4e88a23e8072a230df32` 为 Statistics Presenter **8/8**；它们早于下述 final-review History 修复，只保留为当时证据。
- full EditMode job `b8efa7e5fc84495b8189a011db0d8d39` 为 **1605/1605 passed、0 failed、0 skipped，201.1325655s**；同样早于最终 4 个 P2 修复，不得写成当前源码已通过。

## 2. RED → GREEN 与自动化证据

### 设置事务补偿与 fail-closed

- Unity RED job `e45b9d8771b3455aa1c839b8aaa42071` 首先复现平台 Apply 异常逃逸；RED `25af4049b5c9467e89f834f8179068df` 继续证明补偿写回与平台补偿失败时尚未进入稳定恢复门禁。
- 最小 GREEN job `1c73b018cf734349a4a81b3cf89d1a9a`：AppSettings **18/18 passed**。平台 Apply 失败后，repository 与 platform 独立补偿；完整恢复返回 typed `ApplyFailed`，任一补偿失败返回 `RecoveryFailed` 并设置 sticky `RequiresRecovery`，后续 `TryChange` 在任何磁盘/平台副作用前返回 `RecoveryRequired`。
- Presenter 对完整补偿和恢复故障分别投影 `ApplyFailed` / `RecoveryRequired`，重建后仍显示恢复门禁；Presenter + RunEntry targeted job `a0deeefbb0e24e3cae612069466ba264`：**33/33 passed**。该 job 早于最终地图 patch，当前源码最终覆盖以本节后述 1605/1605 为准。

### 地图 125% 文字缩放 + 高对比回归

- Unity RED job `07c04ffd1ade4a07906b998bae6baa10` 与 `826054fc60d6439198d27862308768d5` 真实复现 Run 地图节点标签在 125% 文字缩放与高对比组合下的重叠问题。
- 最小布局修复后的 GREEN job 为 `975c55298ced499dae34cb5cfc289ed2`。
- 最终地图聚焦集合 job `42fac711e7c845d5a71bfda1a9c5b702`：**25/25 passed**。后续 Player 目视同样确认该组合下标签无重叠，静态几何测试没有被单独冒充产品证据。

### 动态地图可访问性缓存生命周期

- RED job `cc45c8ce55aa43f7a385c1785ac81caf` 以 `MissingReferenceException` 复现重建地图后设置重绘仍访问已销毁旧控件；首轮 GREEN `f3deb959e3eb4aa6a4989e93eb00b5d4` 证明显式 untrack 能关闭直接失效引用。
- 终审发现 PlayMode 的 `Destroy` 延迟到帧末，同次 Render 的 `GetComponentsInChildren(includeInactive: true)` 仍可能重新缓存已停用但仍挂在 RunEntry 下的退休子树。有效 RED `b415c203b7e74b64b5475e98b84e313b` 为 **1/1 failed**（Expected True but False）。
- 最终实现先 untrack，并在旧地图仍激活时 `SetParent(null)` 脱离 RunEntry，再停用/销毁。GREEN `f4fd43d62e9341269c69650927736d3c` 为 **1/1 passed**；相邻回归 `4624253e080e4748b25259fbe6d9dcb8` 为 **22/22 passed**。用户取消 Play 造成的 0/1 orphaned job 不计入 RED、GREEN 或回归证据。

### 分域早期定向集合

- AppSettings job `901ce9566e4247778b80d492dbeb038c`：**25/25 passed**；该数量早于上述事务补强，只保留为早期定向证据。
- Tutorial / Profile job `b96a994584ba4a878cb35676e89b2968`：**46/46 passed**。
- Run History / Statistics job `f94013406fde464fb0be6865d7efab0f`：**14/14 passed**。
- Bootstrap wiring job `130a106efb74433bb909ea8e97b53383`：**2/2 passed**。

### Rider 与 final-review 后当前 Unity 验证

- 当前 Rider MCP solution build session `6c5046e2-6cce-49cb-b888-c3f73697e378`：`Completed / buildIsSuccess=true / problems=[]`；四个关键文件 Rider errors 0。
- 已连接的唯一 Unity 6000.5.5f1 Editor 运行 History/Statistics/UI Audio job `4610ad8d0a274969a311acd6d251d56d`：**38/38 passed、0 failed、0 skipped，1.1416752s**。
- 同一 Editor 的 fresh full EditMode job `fe2d343ea283455b99a89a1b658bf8f7`：**1611/1611 passed、0 failed、0 skipped，159.330777s**。旧 1605/1605 只保留为 final-review 前证据；新增 6 个用例后的 1611/1611 才承担当前源码完整回归。
- 此前 batchmode 在测试前发生的 headless licensing 阻断没有执行测试，只保留为环境诊断；它不再构成 G8-E 缺口。

### final Standards review 的 4 个 P2 修复

- History 首次 `Load` 返回 unavailable 时立即冻结本次 `RunSummary`，确保恢复后不会用新时钟生成另一个 completion time。
- pending 重试使用冻结完成时间重建完整摘要并逐字段比较；终局事实漂移、或 durable history 与 pending 仅完成时间不同，均判 Conflict。
- `StatisticsChanged` 逐观察者隔离异常；已耐久 Record 不被观察者故障遮蔽，后续观察者仍收到完整快照，并由 `LastStatisticsNotificationException` 保留单异常或聚合诊断。
- UI Audio importer 必须 `preloadAudioData=true`；专用 Group schema 只允许 address 进入 catalog，GUID 与 labels 均关闭。

## 3. Sync and Build All、BuildLayout 与 UI Audio

- final-review 后执行 `TinySpire/Build/Sync and Build All`；Unity Console 明确记录 Addressable content successfully built（37.775s）与 `TinySpire sync and local content build completed successfully.`，Error 为 **0**。
- fresh `TinySpire/Library/com.unity.addressables/buildlayout.json` 与归档 `BuildReports/buildlayout_2026.08.29.20.28.09.json` 均为 150922 bytes、SHA-256 `838FA2FD924E855ABC49EB944317812635AFE8275CAD7DF55508A2E9DF8AB1EB`；`BuildError` 为空，catalog hash `732f207a2793f9afced440fa0ad2987f`。
- `TinySpire UI Audio` 为 PackTogether，schema 强制 address=true、GUID=false、labels=false；Group 精确包含 `ui-audio/hover`、`ui-audio/click`、`ui-audio/confirm`、`ui-audio/error` 四地址，对应 `click.wav`、`confirm.wav`、`error.wav`、`hover.wav`，全部 importer `preloadAudioData=true`。
- 四个条目位于 `tinyspireuiaudio_assets_all_8b688eddfc5efe6f113bdee36bdda27c.bundle`，`BuildStatus=0`、AssetCount=4，Provider 为 `UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider`。物理 bundle SHA-256 为 `2F92390B7DAA5786EC1C162179C5C3AE0BA8B4CC806DCCCE055046EC05B34D77`。
- 第 4 节当前源码 Player 生产启动链进一步证明该物理内容可被真实加载；BuildLayout 没有被单独冒充运行时证据。

## 4. Windows Player 构建与产品链证据

### stock IL2CPP 历史诊断构建

- UnityMCP build job `build-0eaf79b22f`：`StandaloneWindows64 / succeeded`，**errors=0、warnings=489**，输出 `TinySpire/Temp/G8Player/TinySpire.exe`。
- 489 条警告按本次 `BuildReport` 完整守恒分类：Sentis package shader variant **485**、HybridCLR 未配置 hot-update modules **1**、TextMeshPro / IL2CPP 大方法拆分提示 **3**、项目 `Assets/` 警告 **0**。MCP 直接读取 Unity `BuildReport.summary.totalWarnings`；这些计数不是 489 个独立 G8 缺陷，也没有混入旧 Console 区间。
- `TinySpire.exe` SHA-256 为 `74155F5299D9F6173E902E08D9CACD511A2AF7217A69B230F68769137E1DB0A3`；`GameAssembly.dll` SHA-256 为 `FD2C06CC9D7928E7E826F19DE43A4647E506779D387653F37788527FFF9A49EA`。
- 该构建只用于早期 stock IL2CPP 诊断与产品探针；临时切换完成后 `HybridCLRSettings.useGlobalIl2cpp` 已恢复为原值 `0`。它不再承担当前默认管线或最终当前源码 Player 证据。

### HybridCLR v8.14.1 与 final-review 前 Development Player

- 根因是先前未固定版本的 HybridCLR package 为 Unity `6000.5.5f1` 选择了 6000.3 的本地 IL2CPP 分支。官方 `v8.14.1` 包含上游修复 commit `a93ca3dc27a2cbb7756b32c187534c18bfbbaf06`；仓库将 `com.code-philosophy.hybridclr` 固定为该 tag，`packages-lock.json` hash 为 `a0e0b502c6c1b9ce2d0983181f4555e6149ae249`。
- HybridCLR Installer 与 `HybridCLR/Generate/All` 成功。该最小兼容修复只改 `Packages/manifest.json`、`Packages/packages-lock.json`；`HybridCLRSettings.useGlobalIl2cpp=0`，没有修改 ProjectSettings、程序集或 AOT/热更新架构。
- final-review 前 UnityMCP clean Development Player job `build-a607c859f5`：`StandaloneWindows64 / succeeded`，**errors=0、warnings=489、439.5431838s、2058.75 MB**，输出 `TinySpire/Temp/G8DevelopmentPlayerCurrent/TinySpire.exe`。
- `TinySpire.exe` SHA-256 为 `51ACC9182CC45B00A3838E7C633D056330C0B7DD4F396502EA75E111367C5F3D`；`GameAssembly.dll` 为 `591BACDA85E3C1D5613729A6417647C1CA887933DEEE1FC162DE1C89C6A33030`；`TinySpire_Data/boot.config` 为 `1AB6F153B2CD6BD7267C1CBA577F8CC1DDE5F2262BDE2D0A521C7F805C98AF25`，profiler connection 为 `Listen`，build GUID 为 `9b82acb274a64159b9ca329f6abb14a5`。
- 该构建证明默认 HybridCLR Unity 6000.5 ABI/build 阻塞继续闭合；同一二进制取得下述教程、M1～M10、Defeat 与 Statistics 产品证据，但早于 final-review History/UI Audio 修复。它继续支持 G8-A/B/C 与 G8-F 的既有产品观察，不能替代 G8-D/E 当前源码重验。

### final-review 后当前源码 Development Player 与 UI Audio real load

- UnityMCP clean Development Player job `build-a17ae188b3`：`StandaloneWindows64 / succeeded`，**errors=0、warnings=490、402.8325836s、2046.43 MB**，输出 `TinySpire/Temp/G8DevelopmentPlayerFinal/TinySpire.exe`。`BuildReport.summary.totalWarnings` 的 490 作为完整计数保留；Console 可见的 4 类摘要不用于臆测其逐条分类。
- `TinySpire.exe` SHA-256 为 `51ACC9182CC45B00A3838E7C633D056330C0B7DD4F396502EA75E111367C5F3D`；`GameAssembly.dll` 为 `3E369CD53EEB89B87118BCF7CAE602F3F7A95FB02C6C20714E3C16442327E0D3`；`TinySpire_Data/boot.config` 为 `6E8CD5EC25235A6AF99EC679C92315908FAC7E72018417F4EEC678474067E5A8`。
- 该 Player 用隐藏 `-batchmode -nographics` 方式启动，未合成鼠标或键盘输入。生产 `GameLauncher` 在加载配置前等待 `UiAudioService.InitializeAsync`，后者顺序 Addressables 加载 Hover、Click、Confirm、Error；日志到达后置标记 `game-config.json 已加载。`，因此与第 3 节 BuildLayout/物理 bundle 共同构成四 cue real-load 证据。
- 验证日志 `TinySpire/Temp/G8DevelopmentPlayerFinal/headless-20260829-204528/Player.log` 为 3865 bytes、SHA-256 `11ED4986CDAE8C2AF2E76379BEF21BA10DEDD1CE41AFA62B50295C25BBF5770B`；`InvalidKey`、UI Audio load failure、Unhandled、NullReferenceException、FileNotFoundException、OperationException、Failed to load 与 LoadAssetAsync failed 扫描均为 **0**。`Application.persistentDataPath is an empty path` 只出现在 Addressables catalog cache warning，未阻断本地 bundle 或配置加载。
- 只结束了本轮创建并核对路径为该 EXE 的 PID 8936。启动前后 settings、profile、两份 history 的 SHA-256 与 history count 均一致，run-save count 继续为 0；无需恢复任何用户持久数据。

### 当前源码 Release Player、中间 full-Act 检查点与人工豁免

- UnityMCP build job `build-38ba3bf544`：`StandaloneWindows64 / Release / succeeded`，**errors=0、warnings=489、350.8749483s、1888.26 MB**，输出 `TinySpire/Temp/G8ReleasePlayerFinal3/TinySpire.exe`。
- `TinySpire.exe`、`GameAssembly.dll`、`TinySpire_Data/boot.config` SHA-256 分别为 `74155F5299D9F6173E902E08D9CACD511A2AF7217A69B230F68769137E1DB0A3`、`9FCB1BBF91D2E9C818FA1CF8D1583183DAC334B174AA10685B9BD465F7BE9419`、`E69AC58A65DED81DEA2677F7D5DDACAEE72054AF24725C221CC5CC4F89707124`。`catalog.bin`、catalog hash 文件与 `settings.json` SHA-256 分别为 `B872BFFD6D9B97D809F15C5D76B25D675C69BD414B95DB339AE0650C06832F8F`、`1EF1EC51F6E095234FC5A0C43F1B07E5723658AE66731A0D945F70589B790FCD`、`63EE54D0556991F46C9C6A182D37C295D23DACB695ECFFA65A8D30E88ABCE32E`；UI Audio bundle SHA-256 为 `2F92390B7DAA5786EC1C162179C5C3AE0BA8B4CC806DCCCE055046EC05B34D77`。
- 实际设置为 `en / Windowed / 1920×1080 / 125% / high contrast / reduced motion`。Player 已进入 Run `5a117682-2bf2-4187-9032-1890524a7e49` 的首战第 2 回合；当前 Warrior 为 24/30 HP、3 energy，两个敌人均为 20/20。该状态仅证明 Release 产品链已从启动进入正式 Battle，不是 Victory 证据。
- 外部临时中间快照中的 Player.log 为 3150 bytes、SHA-256 `FB5A27D1D4350887A8C04EDCB9DD7A1B6F9BEA7EF4D244120DCDCA5CFD6F9236`；到达 `game-config.json 已加载。`，`InvalidKey`、`ConfigInitializationException`、UI Audio、Unhandled、NullReference、FileNotFound 与 OperationException 扫描均为 **0**。同一时点 `run-save.json` SHA-256 为 `37CD06C14F84595BAC76D33B9F7BDB2D1000A9418891084D5B468A4669F7299B`。它是中间日志，不是最终关闭日志。
- 用户已允许鼠标产品验收。Windows Computer Use 的官方 drag 只有单次 from/to，没有 duration、path、mouse-down/up 或跨调用保持按下；对攻击牌与自目标牌的实际尝试均未触发 Unity UGUI 的 `OnBeginDrag → OnDrag → OnEndDrag → Submit` 链。该项是自动化环境限制，不是产品失败，也不授权新增替代 Battle seam。
- 用户随后在同一 Release Player 手动完成真实拖拽出牌；画面显示 `Completed #10 · PlayCard` 并推进到 Round 3。观察时 Warrior 17/30 HP、2 energy，两只 Warden 分别为 17/20 与 14/20，证明鼠标拖拽本身已进入权威 PlayCard 提交与下一回合产品链。该证据只关闭“产品输入是否可用”的疑问；尚未闭合完整 Act Victory。
- 用户继续手动提交到 `Completed #12 · PlayCard`；代理随后点击 End Action，Player 安全进入 Round 4。此时 Warrior 11/30 HP、3 energy，两只 Warden 分别为 11/20 与 8/20，双方敌人意图均为 Attack 6，手牌为 2 Strike + 3 Defend。用户明确之后不再交互；代理用官方单次 drag 再试中间 Defend 后能量、手牌与命令序号均未变化，因此停在回合开始，没有结束会导致 Defeat 的回合。
- Round 4 中间点 Player.log 仍为 3150 bytes、SHA-256 `FB5A27D1D4350887A8C04EDCB9DD7A1B6F9BEA7EF4D244120DCDCA5CFD6F9236`，目标错误扫描继续为 0；稳定地图检查点 `run-save.json` 仍为 1537 bytes、SHA-256 `37CD06C14F84595BAC76D33B9F7BDB2D1000A9418891084D5B468A4669F7299B`。这些中间字段没有被写成最终退出或 Victory 证据。
- 用户于 2026-08-31 明确要求跳过需要人工操作的验证。完整 Victory → 结果 → 主菜单、Victory history exactly-once、Continue disabled 与最终退出日志因此均记为 **`waived / not run`**；这项验收决定不改变上述证据边界。测试 Player 随后结束，未完成 run-save 的同哈希副本保存在外部临时证据目录后，从 persistent data 删除。

### final-review 前 Player 的教程、Defeat、History 与 Statistics

- fresh Profile 启动先显示 Welcome；选择 Skip 后 `player-profile.json` SHA-256 为 `856C8EDCA63FC02624969DA44C8A58EFE392D3AA8647BBBF47B5C94CF561698F`，`tutorialSkipped=true` 且 completed ids 为空，未创建 Run 或 History。正常关闭、重启后 Welcome 不再出现，Hero Selection 没有 overlay。
- Reset 后 Welcome 恢复，Profile SHA-256 为 `A9C125D81DF6DCFE7364D9D23A08084AB466A58032641AF6D97727F81377355C`。依次确认 Welcome、Hero、Map、Battle 后，Profile SHA-256 分别为 `F5B0D1134CA73E481C5162E9EC36F593917C54BE65E5FFDFE4E020C2823E421D`、`16C655C8FD4C0206A22C3BAE1C865E33E0CD8EC4AAF8DBF2F0A7D32C06A0C3AC`、`56DD9C33799DC9AFB41FD5494AB481994B8684AC92FA11D67D5E22A9153F89C3`、`3649ED21C5AB97277B5A1C4BEE9B6A7DB1743655252CB03118B2206C72B42B7B`；最终只包含 Welcome/Hero/Map/Battle ids。Map 与 Battle 教程确认前后 Run save 的 SHA-256、长度与最后写入时间均不变，证明教程只写 Profile。
- 该 Player 的 Battle 通过三次 End Action 进入 Defeat，结果页显示 HP 0/30；生成历史 `7cd34451-b1e1-4954-819c-6bc3f351bfe1.json`，SHA-256 `8461F44B86ED7F053945DD95A3278149A1E345CAB6FC143B28CFE0988AAF1CC3`。返回主菜单后 Run save 已清除，History 保留；Statistics 实见 Total 3、Victory 0、Defeat 2、Abandoned 1，Hero 1001 为 total 2 / defeat 2，Hero 1002 为 total 1 / abandoned 1。
- 该产品链关闭后的 Player.log SHA-256 为 `8232CFF1532ABAEAE37D2A79F3EB559D01281AA5BF1F360EB41BF93E69FE97AE`，Error、InvalidKey、ConfigInitializationException、NullReference 与 Unhandled 扫描为 0；退出阶段 2 条 `JobTempAlloc` 继续按第 6 节既有基线处理。

### M1～M10 设置、重启与 UI 矩阵

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

- 每行均完成设置写入、正常关闭、重启恢复和 Settings 必经控件可达性检查；M3 额外检查 Hero/Map 的 125% 布局，地图节点无重叠且 End Run 可达。Borderless 行证明请求设置、持久化、重启恢复与 UI 可达；自动化环境未取得内部 `Screen.width/height/fullScreenMode` 精确值，因此该字段保持 `unproven`，但不单独阻断本轮。
- M2～M10 关闭日志 SHA-256 依次为 `8232CFF1532ABAEAE37D2A79F3EB559D01281AA5BF1F360EB41BF93E69FE97AE`、`D7EE3D748DE313A598E9B54F87C98B02822010A2118E7EEEFD20C17E0123D365`（M3 地图复核另为 `93B549CE1BBD7F2CE08036D1E99FE055B4F841E571FDF0D89E596B1272479EE8`）、`BDC8197AB81B21C4F5E147C5E5A047BF2541A23AF08DE84B9D333AF51EB04238`、`38F8555EBE7FC3B50EA634491CEFDBD10EE6F191502AE62F22FC5CC6D00F5258`、`E114307EEAB02E410AA4095C7BF694DE9553E38B8720E944313740510379FBEC`、`B40343345A977C9513AE1A5C6769BC73301AE02330EAF413FC80BE80BD5D1E61`、`EC7C7ECF767C67B73B34F4031A1187A9ACCEF485E6D28B0B6CD1BE0AF3CAB6E2`、`3D7C23EEF2229F1F97D8FF8A20A90F5224978B6C264AFE2174253DBE649E80BA`、`BC1836FDAF29C4CFD153DAEF9578CDB1FFC00333B9CE6F1BD2345AAA04615B1E`。所有目标错误与 InvalidKey 扫描为 0；每次退出均有 2 条既有 `JobTempAlloc` 基线。
- Computer Use 的 Tab/Down/Return/Escape 注入没有触发菜单，Sky 的原子 drag 也不能表达 Unity 所需的跨帧 Battle 拖拽；二者只记为 `automation-environment unproven`，不是产品失败，也不授权修改已冻结的 Battle 输入 seam。

### 前一版 Player 产品链与性能历史诊断（不再构成当前门禁）

- `build-77f17d0b8f` Player 曾依次覆盖主菜单、设置、统计、选英雄、地图与首场 Battle；关闭后 Player.log SHA-256 为 `9A1DB362FE8719E42073D801D18B9CDE42CADE2903F15C6ABD3257C12826EFA8`，Error、InvalidKey、ConfigInitializationException、NullReference/NullRef 与 Unhandled 扫描均为 **0**。该二进制早于 History 统计快照修复，不能代表当前源码。
- 对应历史 raw 为 `TinySpire/Temp/G8DevelopmentPlayerCurrent/G8StableFinal.raw`，SHA-256 `87350EDFEE9B49B3AB5B52488D1CCDCEBC73D65845F8446AC36B7A7F7E977811`；CPU 与 Memory 均为 **1200/1200 frames**，采样 20.216s。它只保留为前一版诊断，不代表当前源码，也不要求当前源码重新采样。
- 平均帧时间为 **16.846760864 ms**，即 **59.358 FPS**。计划门槛是平均 `>= 60 FPS`，因此该项严格失败，不能通过小数舍入改写为 60 FPS。p95 为 **17.318455 ms**、p99 为 **20.695103 ms**，均低于 33.3 ms 的 p95 预算。
- Unity Editor 对显示设备的只读枚举曾用于解释该历史数据；用户于 2026-08-29 已明确豁免本轮性能验证，因此刷新率和 FPS 不再进入当前 G8-F 判定。
- working set、private memory 与 GC Alloc 均只属于前一版二进制诊断，当前源码不再安排性能重验。
- 该旧产品链只到达首战；当前源码的新 Player 证据已由第 4 节取代，但仍未贯通完整单 Act Victory。

### 早期启动、设置、地图、Battle 与历史诊断

- stock Player 启动与配置应用成功；125% 文字缩放 + 高对比地图终审目视无标签重叠。这些观察早于最终地图 detach patch，只证明当时产品链，不冒充 `build-77f17d0b8f` 的最终 full-Act 证据。
- Player 在终审 Battle 场景中保持 `Responding`；连续 **60** 个 working-set 样本平均 **566.799 MiB**、峰值 **569.949 MiB**，低于计划的 1 GiB working-set 上限。该采样只证明内存与进程响应性，不提供 FPS、p95 frame time 或 GC Alloc。
- Player persistent-data `Player.log` SHA-256 为 `B773ADA54FC01DE73E1AAFE0487C5D7A2AB51DC7E402C14F41506E92C64192E0`；终审目标错误扫描为 0。
- Defeat history 文件 SHA-256 为 `B03825476D73E9E7A7204F109AB13FFA3773589434ABB7DB975860EC22DAEAD8`；Abandoned history 文件 SHA-256 为 `AB30288F1C776468B12B0AEE7D6D397CCD41EB24133634DBDB50A9BCD2B9B2BB`。它们证明历史记录与 Run save 分离，并为两个 Player outcome 提供不可变文件证据；本轮没有把它们表述为 Player Victory 证据。

## 5. G8-F 人工验证豁免与非阻塞诊断

### 未取得、但由用户明确豁免的字段

1. 当前源码 Release Player 已到首战 Round 4，但没有取得“完整单 Act Victory → 结果 → 主菜单”的产品链证据，也没有核验 Victory history exactly-once、Continue disabled 或最终退出日志。

中间 Battle 检查点、Defeat、旧 Player、自动化测试、伪造终局档或 G7 Packed Play 均没有被拿来替代这些证据。用户明确将这些人工字段从本次交付阻塞项改为 **`waived / not run`**，因此 G8-F 状态为 `accepted-with-waiver`，不是 `verified`。G8-D/E 已由第 2～4 节 current-source 证据闭合；性能同为 `waived / not run`。

### 不单独阻断的退出日志

Player 退出阶段出现的 `JobTempAlloc` 报告属于 G8 前已存在的包 / 引擎基线，未伴随运行中崩溃、配置失败或本轮目标错误。它应继续作为工具链基线跟踪，但不单独构成 G8 新回归或发布阻断理由。

## 6. 范围与环境恢复

- 本轮没有修改 Scene、Prefab 或 asmdef。
- 没有保留 ProjectSettings 或 HybridCLR settings 改动；用于 stock IL2CPP 诊断的临时开关已恢复，`HybridCLRSettings.useGlobalIl2cpp=0`。唯一 package-level 例外是 `Packages/manifest.json` 与 `Packages/packages-lock.json` 的官方 `v8.14.1` pin。
- Development Player 验收后曾精确恢复 `DefaultVolumeProfile.asset`、`UniversalRenderPipelineGlobalSettings.asset`、`ProjectSettings.asset` 与 `UnityConnectSettings.asset`。fresh Release build 再次自动序列化这四个高影响文件；测试 Player 结束后四项均已精确恢复，`git status --short -- <四路径>` 无输出，不进入交付 payload。
- 2026-08-31 交付前预审基线为 132 个 G8 候选路径（24 tracked 实质 diff + 108 untracked）、PerformanceTestRun residual=0；106 个新增 Assets 路径双向 `.meta` 配对缺失/孤儿均为 0。`DEPENDENCIES.md` 与 8 个 Luban generated EOL-only 文件明确排除；文档闭环后重新运行 scoped `git diff --check`、LLM knowledge workflow 与精确 payload 审计。
- 没有把完整手柄 Battle、按键重映射、多平台、云同步、成就、遥测、商业化或多 Act 扩入 G8。
- Release full-Act 启动前 baseline 已复制并冻结；最终复核确认 `app-settings.json` SHA-256 `D64C6A0CB47D6F8E01C30860507A295C2A52CC8280A088DE22A4ED5B6A2AA30B`，`player-profile.json` SHA-256 `3649ED21C5AB97277B5A1C4BEE9B6A7DB1743655252CB03118B2206C72B42B7B`，Defeat/Abandoned history SHA-256 分别为 `B03825476D73E9E7A7204F109AB13FFA3773589434ABB7DB975860EC22DAEAD8`、`AB30288F1C776468B12B0AEE7D6D397CCD41EB24133634DBDB50A9BCD2B9B2BB`。History JSON count=2、Victory history 不存在、`run-save.json` 不存在；测试 Player 进程也已结束。
- 本页只记录已取得的验证事实和 blocker；G8 当前状态以 [STATUS.md](../STATUS.md) 为唯一可变状态源。

## 7. 明确豁免、未执行的字段

- 完整单 Act：Elite/Boss → Victory 结果页 → 主菜单、Continue 不可用、Victory history exactly-once 与最终 Player.log/目标错误扫描均为 `waived / not run`。
- 当前自动化输入：真实用户拖拽已证明产品输入正常；Windows Computer Use 单次 drag 未触发正式 Battle 的跨帧 UGUI 拖拽。本轮没有新增 click-click/autoplay/直接命令或伪造终局档绕过产品 owner。
- 性能：`waived / not run`。用户于 2026-08-29 明确豁免本轮性能验证，不再采集或追踪当前源码 raw、FPS、刷新率、内存或 GC。
- 恢复：已完成。第 6 节 persistent baseline 与四个构建自动噪声文件均已精确恢复。
- 本页 `implementation_status` 为 `accepted-with-waiver`，不是 `verified`；G8 Phase `completed` 表示用户接受当前实现与证据边界，不表示上述人工字段已经通过。
