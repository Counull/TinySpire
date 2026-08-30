---
title: Daedalus · 会话变更日志
page_type: changelog
lifecycle: active
created: 2026-07-06
updated: 2026-08-31
status_source: STATUS.md
---

> 当前可变状态只查 [STATUS.md](STATUS.md)。本页保留按日期排列的审计与恢复线索；其中旧状态快照不构成当前事实。

## 2026-08-31 G8 人工验证豁免、环境恢复与交付收口

- 用户明确要求跳过需要人工操作的验证，并再次明确要求完成后 commit、push。G8-A～E 保持 `verified`；G8-F 从 `validation-blocked` 改为 `accepted-with-waiver`，G8 Phase 收口为 `completed`。该验收决定只移除当前交付 blocker，不把缺失证据改写成通过。
- 当前源码 Release Player `build-38ba3bf544` 的有效证据截至首战 Round 4：真实鼠标已提交到 `Completed #12 · PlayCard`，中间 Player.log 3150 bytes、SHA-256 `FB5A27D1D4350887A8C04EDCB9DD7A1B6F9BEA7EF4D244120DCDCA5CFD6F9236`，目标错误扫描 0。完整 Victory → 结果 → 主菜单、Victory history exactly-once、Continue disabled 与最终退出日志均没有执行，统一记录为 `waived / not run`；性能同为 `waived / not run`。
- 仓库只读复核确认没有合法的 Release full-Act runtime driver；Windows Computer Use 的官方单次 drag 也不能表达 Battle 所需的跨帧按下/移动/松开。本轮没有新增 click-click、autoplay、直接命令或伪造终局档，没有改变 Battle input/command owner。
- 本任务启动且路径精确核对的 Release Player PID 14816 已结束。未完成 `run-save.json` 的同哈希副本保存在外部临时证据目录；persistent data 中该文件已移除。最终 baseline 复核为：settings `D64C6A0CB47D6F8E01C30860507A295C2A52CC8280A088DE22A4ED5B6A2AA30B`、profile `3649ED21C5AB97277B5A1C4BEE9B6A7DB1743655252CB03118B2206C72B42B7B`、Defeat `B03825476D73E9E7A7204F109AB13FFA3773589434ABB7DB975860EC22DAEAD8`、Abandoned `AB30288F1C776468B12B0AEE7D6D397CCD41EB24133634DBDB50A9BCD2B9B2BB`，History count=2、`run-save.json` 不存在。
- fresh Release build 自动改写的 `DefaultVolumeProfile.asset`、`UniversalRenderPipelineGlobalSettings.asset`、`ProjectSettings.asset` 与 `UnityConnectSettings.asset` 已逐项恢复到 HEAD，四路径 status clean。`DEPENDENCIES.md` 与 8 个 Luban EOL-only generated 文件继续作为保护路径排除；进入精确 132 路径候选审计、commit 与 push。

## 2026-08-31 G8 当前源码 Release Player 中间验收（较早 checkpoint；已由上方 waiver 收口取代）

- fresh UnityMCP Release job `build-38ba3bf544` 为 `StandaloneWindows64 / Release / succeeded`、errors 0、warnings 489、350.8749483s、1888.26 MB，输出 `TinySpire/Temp/G8ReleasePlayerFinal3/TinySpire.exe`。EXE/GameAssembly/boot.config SHA-256 分别为 `74155F5299D9F6173E902E08D9CACD511A2AF7217A69B230F68769137E1DB0A3`、`9FCB1BBF91D2E9C818FA1CF8D1583183DAC334B174AA10685B9BD465F7BE9419`、`E69AC58A65DED81DEA2677F7D5DDACAEE72054AF24725C221CC5CC4F89707124`。
- Release 内容中的 catalog.bin、catalog hash 文件、settings.json 与 UI Audio bundle SHA-256 分别为 `B872BFFD6D9B97D809F15C5D76B25D675C69BD414B95DB339AE0650C06832F8F`、`1EF1EC51F6E095234FC5A0C43F1B07E5723658AE66731A0D945F70589B790FCD`、`63EE54D0556991F46C9C6A182D37C295D23DACB695ECFFA65A8D30E88ABCE32E`、`2F92390B7DAA5786EC1C162179C5C3AE0BA8B4CC806DCCCE055046EC05B34D77`。
- Player 以 `en / Windowed / 1920×1080 / 125% / high contrast / reduced motion` 启动，进入 Run `5a117682-2bf2-4187-9032-1890524a7e49` 首战第 2 回合。中间 Player.log 为 3150 bytes、SHA-256 `FB5A27D1D4350887A8C04EDCB9DD7A1B6F9BEA7EF4D244120DCDCA5CFD6F9236`，已到 `game-config.json 已加载。`，目标错误扫描 0；同一时点 run-save SHA-256 `37CD06C14F84595BAC76D33B9F7BDB2D1000A9418891084D5B468A4669F7299B`。Player 与 Run 仍在运行，中间快照不冒充 Victory 或最终日志。
- 用户已允许鼠标。Windows Computer Use 的官方 drag 仅支持一次性 from/to，没有 duration/path/down/up；实测攻击牌与自目标牌均未触发 Unity UGUI 跨帧出牌链。这是自动化输入限制，不是产品失败；未增加 click-click、autoplay、直接命令或伪造终局档。继续需要用户手动拖牌，或另行授权产品输入 seam 变更。
- 用户随后在同一 Release Player 手动完成真实拖拽出牌；画面显示 `Completed #10 · PlayCard` 并推进到首战 Round 3。观察时 Warrior 17/30 HP、2 energy，两只 Warden 分别为 17/20 与 14/20，证明产品鼠标拖拽可达权威 PlayCard Submit；仍不能代替完整 Act Victory。
- 用户继续手动提交到 `Completed #12 · PlayCard`，随后明确之后不再交互。代理完成 End Action 并进入 Round 4：Warrior 11/30 HP、3 energy，两只 Warden 为 11/20 与 8/20，双方本回合均 Attack 6，手牌为 2 Strike + 3 Defend。官方单次 drag 再试中间 Defend 后能量/手牌/命令序号均未变化，因此停在安全的回合开始，没有结束会导致 Defeat 的回合。
- Round 4 中间点 Player.log 仍为 3150 bytes、SHA-256 `FB5A27D1D4350887A8C04EDCB9DD7A1B6F9BEA7EF4D244120DCDCA5CFD6F9236`，目标错误扫描 0；稳定地图检查点 run-save 仍为 1537 bytes、SHA-256 `37CD06C14F84595BAC76D33B9F7BDB2D1000A9418891084D5B468A4669F7299B`。Player 与当前战斗继续运行，不冒充最终日志或 Victory。
- G8-A～E 保持 `verified`；G8-F / G8 Phase 继续 `blocked / validation-blocked` / `active`。尚缺完整 Victory → 结果 → 主菜单、history exactly-once、Continue disabled、最终目标错误 0、persistent baseline 与四个构建噪声文件恢复。性能为 `waived / not run`；没有暂存、commit 或 push。

## 2026-08-29 G8-D/E final-review 后 fresh 验证闭合（G8-A～E verified）

- 唯一已连接 Unity 6000.5.5f1 Editor 完成 final-review 后回归：History/Statistics/UI Audio job `4610ad8d0a274969a311acd6d251d56d` 为 **38/38 passed**；fresh full EditMode `fe2d343ea283455b99a89a1b658bf8f7` 为 **1611/1611 passed**。Rider build `6c5046e2-6cce-49cb-b888-c3f73697e378` 继续为 success/problems 0；双轴 Standards/Spec 复审均 no findings。
- `TinySpire/Build/Sync and Build All` fresh 成功；BuildLayout/归档 BuildReport SHA-256 均为 `838FA2FD924E855ABC49EB944317812635AFE8275CAD7DF55508A2E9DF8AB1EB`，四个 address-only UI Audio 精确进入 `AssetBundleProvider` bundle，物理 bundle SHA-256 `2F92390B7DAA5786EC1C162179C5C3AE0BA8B4CC806DCCCE055046EC05B34D77`。
- 当前源码 Development Player job `build-a17ae188b3` success/errors 0；EXE/GameAssembly/boot.config SHA-256 分别为 `51ACC9182CC45B00A3838E7C633D056330C0B7DD4F396502EA75E111367C5F3D`、`3E369CD53EEB89B87118BCF7CAE602F3F7A95FB02C6C20714E3C16442327E0D3`、`6E8CD5EC25235A6AF99EC679C92315908FAC7E72018417F4EEC678474067E5A8`。
- Player 以隐藏 `-batchmode -nographics` 启动，未注入鼠标或键盘；生产链越过四 cue Addressables 初始化后到达 `game-config.json 已加载。`。日志 SHA-256 `11ED4986CDAE8C2AF2E76379BEF21BA10DEDD1CE41AFA62B50295C25BBF5770B`，InvalidKey、配置初始化、UI Audio load、Unhandled/NullReference/Exception/Error 扫描 0。只结束了路径核对无误的本轮 PID 8936。
- 启动前后 settings/profile/Defeat history/Abandoned history hash 和 history count 均未改变，run-save 继续不存在。资源/Player build 自动写入的四个高影响设置文件已精确恢复，8 个 Luban EOL-only 文件继续保持用户原 CRLF 且 normalized diff clean。
- 交付预审得到 132 个精确 G8 候选路径（24 tracked 实质 diff + 108 untracked）；106 个新增 Assets 路径双向 `.meta` 配对 0 缺失/孤儿，staged=0。`DEPENDENCIES.md` 与 8 个 Luban generated EOL-only 文件均 normalized diff clean 并继续排除；4 个构建自动噪声文件 clean。排除这 9 项后的 scoped `git diff --check` 与 LLM knowledge workflow 均通过。
- 续查 Windows `computer-use` 能力后确认其 UI 输入层使用 `SendInput`；真实 Battle drag 会接管系统指针，且技能安全规则禁止以自制 PowerShell UI Automation 旁路替代。按用户“不影响鼠标”的当前边界，没有初始化目标窗口、激活 Player、发送按键/点击/拖拽或运行任何 Windows UI 自动化。
- 因此 G8-D/E 恢复 `verified`，G8-A～E 均已闭合。G8-F 仍 `blocked / validation-blocked`，唯一 blocker 是当前源码完整单 Act Victory → 结果 → 主菜单；仓库没有合法无鼠标 full-Act driver，正式 Battle 仍需跨帧鼠标拖拽。按用户当前边界不注入输入、不改产品 seam；性能验证继续豁免，commit/push 完成条件尚未满足。

## 2026-08-29 G8 final Standards review 修复与验证回退（较早 checkpoint；已由上方闭合）

- 最终 Standards review 的 4 个 P2 已在工作树完成最小修复并补回归测试：History 首次 Load 失败立即冻结；pending 重试以冻结完成时间重建并逐字段比较完整摘要，覆盖终局事实漂移和同事实/不同完成时间的 durable 冲突；`StatisticsChanged` 逐观察者隔离，已耐久 Record 不被异常遮蔽且保留单异常/聚合诊断；UI Audio 强制 importer `preloadAudioData=true`，专用 catalog 仅暴露 address，关闭 GUID/labels。
- Rider build session `6c5046e2-6cce-49cb-b888-c3f73697e378` 为 Completed/success/problems=[]，四个关键文件 Rider errors 0；`git diff --check` 通过。此前 `b8efa7e5fc84495b8189a011db0d8d39` 的 1605/1605、BuildLayout 和 Player 均早于这些最终修复，不能冒充当前源码 G8-D/E 证据。
- 修复后双轴只读复审已收口：Standards 对原 4 个 P2 逐项确认关闭且无剩余 P0～P3；独立 Spec 对 G8-A～F 当前 tracked/untracked 实现确认无规格偏离、漏 AC、越界或 Run/Battle owner 破坏。两次复审均未运行 Unity/GUI，也未修改文件。
- 尝试以 batchmode 运行新增测试时，Unity 在进入测试与加载用例前被 headless licensing 阻断；没有测试开始执行，因此既不是红灯/失败，也不能记作通过。G8-E 回到 `validating`，需 fresh History/Statistics 定向与 full EditMode。
- UI Audio importer 与 catalog schema 已改变旧资源构建输入，G8-D 回到 `validating`；需 fresh `Sync and Build All`、BuildLayout，并由 Packed/Player 证明 address-only catalog 下四个 cue 仍经 `AssetBundleProvider` real load。
- G8-A/B/C 保持 `verified`。G8-F 继续 `blocked / validation-blocked`，唯一 blocker 仍是当前源码完整单 Act Victory；D/E 的验证缺口不改写成 G8-F 输入或性能失败。用户明确豁免的性能验证不重开，persistent baseline 仍已恢复；自动化键盘/跨帧 drag 未证明不是产品失败。
- 因此 G8 Phase 保持 `active`，尚不满足完成后 commit/push 条件。下一串行停止点是：G8-E fresh Unity → G8-D fresh 资源构建/real load → G8-F 完整 Victory。

## 2026-08-29 G8 当前源码产品链与 10 行矩阵（较早 checkpoint；已由上方记录收紧）

- G8-C 的产品证据已闭合并从 `validating` 提升为 `verified`。当前源码 Player `build-a607c859f5` 以 fresh Profile 实际覆盖 Welcome、skip、正常关闭/重启、reset、Hero、Map 与 Battle；Map/Battle 教程确认前后 Run save 的 SHA-256、长度与最后写入时间均不变，Profile 只记录教程步骤。Tutorial 自动化仍为 46/46。
- 同一当前源码 Player 已覆盖启动、设置、Hero、Map、Battle、Defeat、Statistics 与返回主菜单。三次 End Action 后形成 Defeat，结果页返回主菜单后 Run save 清除；新历史 `7cd34451-b1e1-4954-819c-6bc3f351bfe1.json` SHA-256 `8461F44B86ED7F053945DD95A3278149A1E345CAB6FC143B28CFE0988AAF1CC3`，Statistics 实见 Total 3 / Victory 0 / Defeat 2 / Abandoned 1。产品链 Player.log SHA-256 `8232CFF1532ABAEAE37D2A79F3EB559D01281AA5BF1F360EB41BF93E69FE97AE`，目标错误扫描为 0；退出阶段 2 条 `JobTempAlloc` 继续归入既有引擎/包基线。
- M1～M10 已全部完成设置写入、正常关闭、重启恢复与 UI 可达性检查，覆盖 Windowed/Borderless、1280×720 / 1920×1080 / 2560×1440 / 1920×1200 / 2560×1080、`zh-CN/en`、100/125% 文字、高对比与 reduced motion 的冻结组合；每行 settings hash、关闭日志 hash 与配置见 `06_testing/2026-08-29-g8-productization-release-gates.md`。M3 额外覆盖 125% Hero/Map，节点无重叠且 End Run 可达。
- Borderless 行已证明请求设置、持久化、重启恢复与 UI 可达；自动化环境未取得内部 `Screen.width/height/fullScreenMode` 精确值。Computer Use 的键盘注入没有触发菜单，Sky 原子 drag 也不能表达 Unity 所需的跨帧 Battle 拖拽；这些只记为 `automation-environment unproven`，不是产品失败，也不授权新增 click-click/keyboard-only Battle 或修改既有输入 seam。
- 最终 Rider build `977de2d6-10d1-4e29-af34-137a28d21044` success/problems 0；fresh full EditMode `b8efa7e5fc84495b8189a011db0d8d39` 为 **1605/1605 passed、0 failed、0 skipped，201.1325655s**。
- 用户于 2026-08-29 明确豁免本轮性能验证。当前源码 raw、FPS、刷新率、内存与 GC 已从本轮剩余门禁和待办删除；被取消并删除的采样不进入验收证据。
- persistent baseline 已恢复并核验：settings SHA-256 `D64C6A0CB47D6F8E01C30860507A295C2A52CC8280A088DE22A4ED5B6A2AA30B`、profile `3649ED21C5AB97277B5A1C4BEE9B6A7DB1743655252CB03118B2206C72B42B7B`、Defeat history `B03825476D73E9E7A7204F109AB13FFA3773589434ABB7DB975860EC22DAEAD8`、Abandoned history `AB30288F1C776468B12B0AEE7D6D397CCD41EB24133634DBDB50A9BCD2B9B2BB`；history JSON count=2，`run-save.json`、验证新增 history 与验证 profile sibling 均不存在。
- 因此 G8-A～E 均为 `verified`；G8-F 与 G8 Phase 继续为 `blocked / validation-blocked`、`active`。唯一剩余 blocker 是当前源码 Player 的完整单 Act Victory → 结果 → 主菜单证据；Defeat、旧 Player、G7 Packed Play 或自动化测试均不能代替。完成后 commit/push 条件仍未满足。

## 2026-08-29 G8 产品化与发布门禁（较早 validation checkpoint；已由上方记录取代）

- G8 当前 Phase 为 `active`，不是 `completed`。G8-A/B/D/E 已完成并 `verified`，G8-C 保持 `validating`：应用设置以独立 `AppSettingsService` / versioned `app-settings.json` 持有语言、音量、窗口、分辨率与可访问性；教程以独立 `PlayerProfileStateStore` / `player-profile.json` 只保存教程进度；Run 历史以 `run-history/{RunId}.json` 保存逐局不可变 `RunSummary`，Statistics 只从历史派生，不建立第二份计数或 Run/Outcome store。
- G8-C 的全局 TutorialGuideOverlay 只读消费 AppSettings 的初始化 `Current` 与后续 `Changed`，即时应用 100/125% 文字、高对比和 reduced-motion；Presenter/overlay 解除时取消订阅。教程上下文只读取当前 RunEntry/Battle 页面事实，确认、skip、reset 只写 Profile，不直接写 Run、Battle 或 History。
- Run 终局采用 history-before-delete barrier：只有不可变 summary 已提交或确认 AlreadyRecorded 后，结果页才允许清理 Run save/journal；Conflict 或 commit failure 保持可重试且不删除活动证据。真实 Defeat 与 Abandoned 历史文件 SHA-256 分别为 `B03825476D73E9E7A7204F109AB13FFA3773589434ABB7DB975860EC22DAEAD8`、`AB30288F1C776468B12B0AEE7D6D397CCD41EB24133634DBDB50A9BCD2B9B2BB`，统计继续从同一历史 projection 派生。
- G8-B 冻结 Windows x64、`zh-CN/en`、鼠标 Battle + 键盘菜单、五种声明分辨率、100/125% 文字、高对比与减少动态效果。地图紧凑节点在 125%+高对比下通过“名称/身份独立区域 + autosizing”保持 glyph 不越界；补强保留 RED `07c04ffd1ade4a07906b998bae6baa10` / `826054fc60d6439198d27862308768d5`、GREEN `975c55298ced499dae34cb5cfc289ed2`，focused job `42fac711e7c845d5a71bfda1a9c5b702` 为 **25/25**。未把菜单手柄探针写成完整手柄 Run 支持。
- 设置事务终审补强先以 RED `e45b9d8771b3455aa1c839b8aaa42071` 暴露平台 Apply 异常逃逸，再以 RED `25af4049b5c9467e89f834f8179068df` 暴露两个补偿/fail-closed 缺口；GREEN job `1c73b018cf734349a4a81b3cf89d1a9a` 为 **18/18**。候选已提交但平台应用失败时，服务独立补偿磁盘与平台；两项均恢复才返回 typed `ApplyFailed`，任一补偿失败则返回 `RecoveryFailed` 并进入 sticky `RecoveryRequired`，后续变更在触碰磁盘或平台前 fail-closed。Presenter 将 Apply/Recovery 状态投影为类型化失败，重建后仍保留 RecoveryRequired；Presenter + RunEntry targeted job `a0deeefbb0e24e3cae612069466ba264` 为 **33/33**，其后最终源码由完整 1604/1604 覆盖。
- 地图动态可访问性缓存先以 RED `cc45c8ce55aa43f7a385c1785ac81caf` 复现已销毁控件的 `MissingReferenceException`，GREEN `f3deb959e3eb4aa6a4989e93eb00b5d4` 关闭首个缺口；终审继续以有效 RED `b415c203b7e74b64b5475e98b84e313b`（1/1 failed）证明延迟 Destroy 前旧地图仍会留在 RunEntry 层级。最终 GREEN `f4fd43d62e9341269c69650927736d3c` 为 1/1，邻接回归 `4624253e080e4748b25259fbe6d9dcb8` 为 **22/22**：旧地图先从可访问性基线移除并在仍激活时脱离 RunEntry 层级，再停用/销毁，避免同帧 `includeInactive=true` 重扫重新缓存退休控件。用户取消导致的 orphaned Play job 不计入证据。
- 设置补强后的 Rider build session `3d52faea-edbc-4763-afd1-31cf57627b9f` 为 success/problems 0；最终地图 patch 后的 fresh Unity EditMode job `5ac459cd8d5447718e62d40087290746` 为 **1604/1604 passed、0 failed、0 skipped，35.9688173s**，并由下述 post-detach Player build 再次完成当前源码编译。
- `TinySpire/Build/Sync and Build All` 成功，最终 Console Error 0。2026-08-29 04:24:55 的最终 BuildLayout SHA-256 为 `C53DEAB42C7D0583E4BB9FF6F82D4F33A08DD351796F0D8E1D181C7105985133`、BuildError 为空；四个 `ui-audio/*` 逻辑地址均由 `AssetBundleProvider` 进入专用物理 bundle，bundle SHA-256 为 `9A2441C5C87227BB8DF40F631972B8395327DEBBC15C78CCDE0E9792C9615865`。UI Audio 继续使用短键、专用组与 Addressables loader，没有 `Resources.Load`、运行时 `AssetDatabase` 或业务素材路径旁路。
- HybridCLR 已按官方 Unity 6000.5 兼容修复把 package 固定为 `v8.14.1`，lock hash 为 `a0e0b502c6c1b9ce2d0983181f4555e6149ae249`；上游修复 commit 为 `a93ca3dc27a2cbb7756b32c187534c18bfbbaf06`。Installer 与 `HybridCLR/Generate/All` 成功，且未修改 `HybridCLRSettings`、ProjectSettings 或 AOT/热更新架构；先前 `build-0eaf79b22f` stock IL2CPP 只保留为历史诊断。
- History 统计快照修复后的当前 clean Development Player job `build-a607c859f5` 为 success、errors 0、warnings 489、439.5431838s、2058.75 MB，输出 `TinySpire/Temp/G8DevelopmentPlayerCurrent/TinySpire.exe`。EXE SHA-256 `51ACC9182CC45B00A3838E7C633D056330C0B7DD4F396502EA75E111367C5F3D`，GameAssembly `591BACDA85E3C1D5613729A6417647C1CA887933DEEE1FC162DE1C89C6A33030`，`boot.config` `1AB6F153B2CD6BD7267C1CBA577F8CC1DDE5F2262BDE2D0A521C7F805C98AF25`，profiler connection 为 `Listen`。前一版 Player 日志已冻结为 `9A1DB362FE8719E42073D801D18B9CDE42CADE2903F15C6ABD3257C12826EFA8` 且目标错误 0；本段当时仍计划重验，后续已由上方当前源码证据与性能豁免取代。
- 在该较早 checkpoint，当前源码 Player `build-a607c859f5` 尚未启动；主菜单、设置、统计、选英雄、地图、首战与目标日志错误为 0 的观察来自前一版 `build-77f17d0b8f`。这一状态已由上方当前源码产品链取代。
- 前一版 Profiler raw 为 `TinySpire/Temp/G8DevelopmentPlayerCurrent/G8StableFinal.raw`（Temp 证据，不提交），SHA-256 `87350EDFEE9B49B3AB5B52488D1CCDCEBC73D65845F8446AC36B7A7F7E977811`；CPU+Memory 1200/1200 帧、20.216s。平均帧时间 16.846760864 ms = **59.358 FPS**，p95 17.318455 ms、p99 20.695103 ms；working set 370.6–370.7 MiB、private memory 974 MiB，GC Alloc 平均 453.25 B/frame、最大 609 B（frame 925）。这些只保留为较早诊断；后续用户已明确豁免本轮性能验证，不再要求当前源码重采。
- Unity Editor 当时只读枚举当前显示模式为 `2560×1440 @ 59951/1000 = 59.951 Hz`，且 1920×1080 也只有同一模式；该记录只解释较早诊断。后续用户已明确选择本轮不做性能测试，刷新率与 FPS 不再是当前 blocker。
- Standards 终审发现 History 无载荷 invalidation event 违反 AC-P001；TDD RED `bb175319dbab4a30bc93aa531ae29857` 为 0/1，GREEN `b6daef6df30e46898e0de5e7e414be77` 为 1/1。`RunHistoryService` 现在发布完整 `RunHistoryStatisticsLoadResult`，Statistics Presenter 缓存该快照，locale 变化只重建本地化模型；相邻 History 15/15、Presenter 8/8 与 Rider build `977de2d6-10d1-4e29-af34-137a28d21044` success/problems 0。
- 该终审修复后的 fresh full EditMode job `b8efa7e5fc84495b8189a011db0d8d39` 为 **1605/1605 passed、0 failed、0 skipped，201.1325655s**，覆盖当前源码。
- Spec 终审在该 checkpoint 确认冻结发布矩阵和 G8-C 产品证据尚未闭合，因此当时把 G8-C 降为 `validating` 并扩回完整矩阵；这些缺口已由上方当前源码教程与 M1～M10 证据闭合。
- 既有 stock Player 产品链覆盖启动/config、地图 125%+高对比、Battle、Abandoned 与历史，目标日志错误为 0；Battle 60 次 working-set 平均 566.799 MiB、峰值 569.949 MiB。它们只作为早期产品/内存诊断，不替代最终当前源码二进制的完整 Act 或性能证据。
- 该 checkpoint 的状态是 G8-C `validating`、G8-F `blocked / validation-blocked`；上方记录已把 G8-C 推进为 `verified`，并以用户明确豁免取代性能 blocker。Scene、Prefab、asmdef、ProjectSettings、HybridCLR settings、DI 架构与 BattleCommandQueue 所有权均未因 G8 改动；完成后 commit/push 条件仍未满足。

## 2026-08-29 G8 产品化与发布门禁（implementing checkpoint；已由上方结果记录取代）

- 用户明确授权实现 `RUN_ROADMAP.md` G8、优先参考杀戮尖塔2的产品设计，并允许完成后 commit/push。本轮冻结为 Windows Standalone x64、`zh-CN/en`、键鼠完整 Run、五种分辨率/宽高比、100/125% 文字缩放、高对比和减少动态效果；手柄 Battle 输入语法、多平台、云同步、成就/遥测/商业化继续排除。
- 开始基线为 `main`/`origin/main` HEAD `5c415b03b00d74d32e634f26b0d8d15a7fd3b2d2`，G7 已由 `80c6376` 交付。唯一既有改动是用户 `Docs/Copilot_Daedalus/DEPENDENCIES.md` 的换行差异，本轮明确保护并排除。
- UnityMCP 只有一个 `TinySpire@8edf130c865b3957`，用户结束 Play Mode 后确认 BootstrapScene、Edit Mode、idle、无编译/刷新、`ready_for_tools=true`；RiderMCP 以项目相对根 `TinySpire/` 连通，初始 Error problems=0。
- 只读 seam audit 确认当前只有 `LocalizationService` 具备应用设置能力；Settings 与 Statistics 仍是占位，没有 App/Profile store、教程、RunSummary/history 或音频资源。RunEntry 已有唯一 Action→Presenter→View seam 与 Input System UI map；Battle 完整操作仍依赖鼠标拖拽，因此本轮不把菜单手柄导航冒充为手柄完整 Run。
- 已建立 [G8 窄计划](plans/2026-08-29-g8-productization-release-gates.md)，按 G8-A settings → G8-B 输入/分辨率/可访问性 → G8-C 教程 → G8-D 表现/音频 → G8-E history/统计 → G8-F Player 矩阵串行 RED→GREEN。当前尚未产生代码、Unity 测试、生成、BuildLayout、Player、commit 或 push 完成证据。

## 2026-08-28 G7 单 Act、精英、Boss 与 Run 终局（completed / verified）

- G7-A～E 已按窄计划完成：`tinyspire.act1.g7.v1` 复用 mixed generator v2，路线冻结为 `Combat→Rest→Chest→Shop→Event→Combat→Elite→Boss`；ActContentManifest 统一解析普通/Elite/Boss 内容，Elite 5101 与唯一真实 Boss Encounter 5201 均继续走既有 Battle setup/result bridge，未建立第二地图或结果通道。
- `battle.encounter` 新增 nullable `int? phase_two_behavior_group_id`，无阶段为 null/0，Boss 5201 的二阶段组为 6202。Phase I 首个已明示行动完整提交时，由 Battle-owned prepared completion 恰好一次切到 Phase II；Packed 实际 Boss 意图为 `5→8→8`，Run/save/UI 不拥有 phase，也没有通用 Boss DSL。
- `RunStateStore` 已以 schema v6 闭合唯一 `RunOutcome(Victory/Defeat/Abandoned)`；Combat/Elite 胜利保留 G4 reward，Boss 胜利直达无普通奖励的 Victory，任意战败进入 Defeat，稳定地图主动放弃先耐久形成 Abandoned。Store 自身按 G7 profile 与 Boss identity 独立解析 manifest，legacy/未知 profile 不能绕过 Flow 启动真实 Boss；三类终局均 save-before-publish、冷启动不可 Continue、确认后清理回主菜单，普通战斗失败没有恢复同节点 retry。
- 首次 G7 定向聚合为 425/428，暴露三个旧 fixture/兼容断言仍停在 pre-G7 口径；没有放宽生产 validator，只修复 Boss 候选、v4 未来字段与 schema 版本期望，阶段性回归为 428/428。终审随后发现 Atomic 普通奖励/Defeat 后继尚未证明 committed node 是 live recipe 当前路径的直达 Combat/Elite，以及 Store public Boss 入口需独立 profile/manifest 门禁。Unity RED job `e81cc15a7483467291b7b9d72094fc1f` 为 **510 total / 505 passed / 5 failed**（四项 Atomic 旧 fixture/新来源门禁、一项 Boss round-trip catalog profile）；保持生产门禁并修正 fixture 后，GREEN job `60ec69d046b5442cb593a8bef123c0f1` 为 **510/510 passed、0 failed、0 skipped，8.0937884s**。
- Rider 最终 build session `e750f929-d9bf-4cfd-bbf6-d715c237be51` 为 `Completed / success / problems 0`；完整 Unity EditMode job `9758c02e718540aa97e5e26f832794e3` 为 **1410/1410 passed、0 failed、0 skipped，23.0963649s**。生产形状测试同时覆盖 Elite Victory→RewardPending 与 Boss Defeat→Terminal/no reward，Atomic 对 RewardPending 和 terminal 都重建并核对 live recipe、fingerprint、路径与直达 Combat/Elite 前驱。
- Spreadsheet artifact 流程维护五份 workbook，Luban 与 `TinySpire/Build/Sync and Build All` 成功。最新 BuildLayout `buildlayout_2026.08.28.02.59.50.json` SHA-256 为 `BAF93C72F09D968197A9B54DF56803E8EE16160FF8E67EDD00B0BBAEE424B015`，BuildError 为空；四份 G7 GameData JSON、`pfb_char_enemy.prefab`、RunEntryScene 与 BattleScene 均位于 `AssetBundleProvider / BuildStatus=0` 的现存物理 bundle。
- UnityMCP Packed Play 真实 UGUI 分别走通：完整 Act→Boss 的 Victory、MapReady 确认 Abandoned、首战连续三次 EndAction 令 HP `30→18→6→0` 的 Defeat。三条分支都形成 schema v6 terminal、展示对应结果、确认返回 MainMenu 且不可 Continue；Console Error、InvalidKey 与 ConfigInitializationException 均为 0。
- 验收后用户原 302-byte `run-save.json` 已按 SHA-256 `419058435D82A48EA08DBF3121F6127417EAC700D302388BFFFA4586DFEE54B9` 恢复，Addressables builder 1→0，Settings hash 比较不变，BootstrapScene `dirty=false`。Scene、Prefab、asmdef、ProjectSettings、HybridCLR、DI 与 G8 均未改；用户 `.gitignore`、`AGENTS.md` WIP 继续排除。用户已授权精确 commit/push，但本节形成时尚未执行，最终回复必须分别报告本地 commit 与远端 push。完整证据见 `06_testing/2026-08-28-g7-single-act-elite-boss-outcome.md`，决定见 CD-121。

## 2026-08-28 G7 单 Act、精英、Boss 与 Run 终局（implementing checkpoint；已由上方完成记录取代）

- 用户明确授权实现 `RUN_ROADMAP.md` G7、控制当前 Unity Editor，并允许在确认完成后 commit/push；当前授权不延伸到 G8。开始时工作区已有用户 `.gitignore` 与 `AGENTS.md` 修改，本轮保持不覆盖、不清理、不纳入 G7 暂存。
- Unity MCP 已确认只有一个 `TinySpire@8edf130c865b3957` Editor，Unity 6000.5.5f1、BootstrapScene、Edit Mode、idle、未编译；Rider MCP 以正确 solution root `E:/Project/TinySpire` 连通且初始 Error problems=0。
- seam audit 冻结：继续使用唯一 MapDefinition、BossGate、Battle setup/result bridge 和 RunStateStore；新增 G7 profile/Act manifest、Elite 节点、单一真实 Boss Encounter、Battle-owned 一次性 phase，以及 Store-owned `RunOutcome(Victory/Defeat/Abandoned)`，不创建第二地图、第二 result bridge 或 Outcome store。
- Boss phase 最小口径为：Phase I 已明示的首个敌人行动完整执行后，在同一 prepared completion 中恰好一次切到 Phase II 并冻结下一意图；不使用血量/回合 DSL，不把 phase 写入 Run/save/UI。实现正按地图/内容、Battle phase、Run outcome/persistence 三个独立 RED→GREEN 切片推进。
- 当前计划见 `plans/2026-08-28-g7-single-act-elite-boss-outcome.md`。尚未形成 Unity 测试、Luban、Addressables、Packed、commit 或 push 完成证据；这些不能由旧 G5/G6 结果替代。

## 2026-08-27 G5/G6 持有物与非战斗节点（completed / verified）

- 用户授权从共同 Run 持有物/非战斗一次性结算边界开始，串行交付 G5→G6，并随后明确取消逐片 Grill。S0、G5-B～D、G6-A～E 均已按窄计划实现：schema v5、唯一 `RunHoldings`、类型化 `PendingNodeVisit`、BattleStart 遗物、BattleResult 药水消费、首次奖励 attached loot、mixed map v2，以及 Rest/Chest/Shop/Event 的 save-before-publish 结算。
- 最终范围审查发现 schema v5 只校验 attached loot 模板存在，第二场 RewardPending 冷档可伪造合法 Potion 9001 并重复获利。新增测试先得到 `Expected InvalidDocument / But was Success` 的真实 RED，再让恢复从已完成 Combat 路径与 holdings 重建权威 attached loot；篡改的后续奖励与删除首战应有 loot 均 fail-closed。修复后 G5 当前源码离线聚合为 **155 passed、0 failed、4 个真实 GameObject 用例明确 skipped**；G6 最终非 Unity 聚合为 **312/312 passed、0 failed、0 skipped**。
- 生产与 Editor 静态 build 分别为 0 errors / 6 warnings 与 0 errors / 12 warnings；Luban `gen.bat` 与 `TinySpire/Build/Sync and Build All` 均成功，新增 12 个 `.meta` GUID 唯一。Rider MCP project problems 为 0。独立 G6-D/G6-E 审查未发现其他 P1/P2，Shop 审查指出的三个直接证据缺口已补测试后复审通过。
- 先前两次 Unity 批处理因 license/entitlement 返回 code 198 的历史阻塞已解除。完整 Unity EditMode 最终于 2026-08-27 21:43:42 得到 **1348/1348 passed、0 failed、0 skipped**，耗时 74.8235407s；首次全量运行暴露 G4 production acceptance 假定奖励后直接进入下一战的旧路径假设，补齐穿越 G6 必经非战斗节点的验收接线后重跑全绿。
- 最新 BuildLayout `BuildError` 为空、12/12 bundles `BuildStatus=0`；`run_tbrelic.json` 与 `run_tbpotion.json` 位于 `AssetBundleProvider` bundle，物理 bundle SHA-256 为 `BB0FB1DB789F8851538E07C4A6ECF50014C4987CF93792F54D7FDE2925FA9610`。
- UnityMCP 在 Packed Play active builder index 1 下从 Bootstrap 进入 RunEntry，以 Hero 1001 新建 schema v5 Run；实际 profile 为 `tinyspire.act1.g6.v1`，路线为 `Combat→Rest→Chest→Shop→Event→Combat→BossGate`。Console Error、InvalidKey 与 ConfigInitializationException 均为 0；验收后已恢复 active builder index 0，并按 SHA-256 `419058435D82A48EA08DBF3121F6127417EAC700D302388BFFFA4586DFEE54B9` 恢复用户原 `run-save.json`。
- 本轮严格没有进入 G7、真实 Boss/Boss 阶段、精英或 RunOutcome，也没有实现云存档、多槽或架构重构；Scene、Prefab、asmdef、ProjectSettings、HybridCLR、DI 与启动流程均未纳入功能改动。完整证据见 `06_testing/2026-08-27-g5-g6-run-holdings-noncombat-nodes.md`，决策见 CD-119、CD-120。

## 2026-08-26 G4 RunDeck、奖励闭环与多级升级（verified）

- G4-A～D 已全部完成并关闭：ordered RunDeck 与稳定实例 ID、schema v4/legacy canonicalize、双 Hero 冻结奖励池、选择/跳过 exactly-once 事务，以及四张生产卡的有限/无限升级投影均落地；没有进入 G5。
- 最终停止点为 120/120、30/30、35/35、258/258；生产 GameData 双 Hero 验收 job `614adafdcec0456088074214dbc85f98` 为 1/1；完整 Unity EditMode job `7cad4b02d38248f298227ea06804c949` 为 **1093/1093 passed、0 failed、0 skipped**；Rider build session `07b40384-6749-4cfa-ac8c-b5f8bd4f9cee` 成功且 errors-only 检查为 0。
- `battle.hero.xlsx`、`battle.card.xlsx`、`battle.card_upgrade_level.xlsx`、`__tables__.xlsx`、`__enums__.xlsx` 与 `i18n.xlsx` 已通过 Spreadsheets 流程维护并回读；Luban 与 `TinySpire/Build/Sync and Build All` 成功。最新 BuildLayout 无 BuildError，G4 GameData 和 RunEntry/Battle/Loading 场景均由 `AssetBundleProvider` 打包。
- Packed Play 使用真实 UGUI、SceneFlow、BattleCommandQueue、BattleSession、卡区与 Effect 链完成两名 Hero 的产品闭环：1001 选择 3116 后新增 origin 实例 11 并在下一战实际抽到；1002 冷启动后候选 3218/3213/3274 顺序不变，Skip 前后 RunDeck 完全相同，下一战投影一致。最终产品检查点 Console Error、InvalidKey 与配置失败均为 0。
- Packed 验收后已恢复 Fast Mode，并把用户原 `run-save.json` 按 SHA-256 `419058435D82A48EA08DBF3121F6127417EAC700D302388BFFFA4586DFEE54B9` 精确恢复；临时副本已删除。用户原有 `TinySpire/ProjectSettings/ProjectSettings.asset` 与 `TinySpire/.codex/` WIP 均保留。
- 本轮没有修改 Scene、Prefab、asmdef、ProjectSettings、HybridCLR 或 DI 架构，未实现任何 G5 内容，也未执行 `git add`、`commit`、`push`、`reset` 或 `clean`。完整原始证据见 `06_testing/2026-08-25-g4-run-deck-rewards-upgrades.md`。

## 2026-08-26 G4-B～D 冻结奖励、选择/跳过与多级升级（implemented；final validation pending）

- 用户授权 Agent 自行裁量阻塞后，普通奖励稀有度配表冻结为 Common/Uncommon/Rare `60/37/3`，明确只做无状态独立抽取、不继承旧草稿保底。Spreadsheets 插件直接维护生产 `battle.hero.xlsx`、`battle.card.xlsx`、新增升级表、表/枚举定义与 i18n；Luban 已成功生成 schema 与 GameData，最终仍必须由 `Sync and Build All` 重建 Local Addressables。
- Hero 1001/1002 分别配置 12/76 张互不共享的 Implemented 非 Basic 奖励候选。独立 Reward seed、三张不同模板、schema v4 冻结 Pending、读档/冷启动不重抽、选择/跳过、save-before-publish、reward intent 恢复、失败 exact retry、伪造/过期/重复零写入与下一战 Run origin 实例链均已接入现有 Store/Flow/RunEntry seam。
- 升级生产轨道锁定 3002 Strike 有限 L1 伤害 9、3123 Bludgeon 无限伤害 +10/level、3201 Shoot 无限程序伤害 +3/level、3207 OutputAdjust 有限 L1 费用 0。共享只读投影进入文本、费用、归宿、通用 Effect 与 MachineGunner program；G4 只提供实例级领域升级，不新增玩家按钮，首个主动入口仍留给 G6。
- Rider 最近完整静态 build `29060419-60fa-4d8a-add3-2fca3431720b` 为 success、0 problems；两处正式 GameData 测试加载器已兼容对象索引根与升级表数组根，errors-only lint 为 0，临时 csproj Include 已字节级恢复。合同审查未发现 G5 越界、高影响文件写入或第二份 Run 状态。
- 持久化终审发现旧 v2/v3 legacy fallback 若只在内存展开，首战 reward intent 崩溃窗口会让磁盘 legacy live 与 canonical intent 被误判冲突。已先补 Continue canonicalize-before-publish 与失败 exact retry 的预期 RED 用例；必须在 Unity Session 中取得真实 RED，再做最小修复与 GREEN，之后才能继续最终门。
- 当前既有 Unity Editor 尚未连接 UnityMCP，因此没有把静态证据冒充 Unity 验收。待连接同一 Editor 后，只执行 G4-B～D 定向、`Sync and Build All`、完整 EditMode、Local Addressables/Packed 双 Hero 产品链及 Console 0；全部通过后立即停在 G4。用户原有 ProjectSettings 与 `.codex/` WIP 保留，未执行 add/commit/push/reset/clean。

## 2026-08-26 G4-A RunDeck 实例、schema v3 与 Battle origin 投影（G4-A verified；G4 整体未完成）

- 按已冻结 G4 窄计划完成首个串行停止点：新增稳定 `RunCardInstanceId`、不可变 `RunCard` 与 ordered `RunDeck`；新 Run 只从 Hero initial deck 展开一次，同模板副本保持独立身份，Store/Flow/View 不再以第二份模板列表拥有牌组事实。
- 当前写入 schema 提升到 v3 并 canonical 保存 ordered RunCards。v2 仅经严格正整数 `deckTemplateId` 迁移到显式 legacy fallback，恢复时展开一次并在后续稳定提交改写 canonical；v1 继续 fail-fast。Atomic adapter 的 durable equality 已纳入实例 ID、模板、等级和顺序。
- Battle setup 只读接收 RunCard 投影，局部 CardInstanceId 与跨战 Run origin 明确分离；`CardInstanceData` 保存 nullable origin 和 opaque UpgradeLevel，临时卡保持 null/0，抽弃牌堆、手牌和其他 Battle 状态不回写 RunDeck。
- TDD 中保留了 schema 版本、v2 migration、Battle origin 与默认实例 ID 等真实 RED；终审又以 job `2b353e13f9224f10b76bd44db5b980f1` 证明 legacy Deck 缺失 Card 会错误恢复成功，最小修复后 job `2470f01dddc9430bb0a209deef2fd3f1` 1/1。补齐 canonical 冷启动→Battle setup 与 atomic 三类可解析漂移覆盖后，最终八类定向聚合 job `c210e4b045aa454780e22a38d02e9445` 为 **120/120 passed、0 failed、0 skipped，4.6678839s**。Rider 相关文件分析、Unity 刷新编译与清空测试工具日志后的 Console 均为 0 errors。
- Spreadsheets 插件已用 `battle.hero.xlsx` 完成只读导入/渲染，并在一次性副本编辑、导出和回读；生产 workbook Git 状态与 SHA-256 均未变化。后续若 G4 表格修改获准，可使用该插件并继续遵守 Luban 与 Sync/Build 门。
- G4-B 尚未开始。Common/Uncommon/Rare 精确权重没有权威值；非权威 inbox 的 `60/37/3` 不采纳。等待用户冻结权重前不创建奖励池、Reward RNG 或 Pending，也不越过 B 进入 C/D。完整滚动证据见 `06_testing/2026-08-25-g4-run-deck-rewards-upgrades.md`，决策见 CD-117。

## 2026-08-24 LLM workflow 第二轮文档维护（verified）

- 授权边界已统一：目录 ownership 只表示获当前任务写入授权后的落点，不构成常驻编辑权；旧状态、Roadmap、计划、约定或 owner 字段都不能授予新工作，commit/push 仍需当前任务明确指令。
- Daedalus Agent 接口压缩为单一路由入口、角色职责和宿主调用 adapter；失效链接、重复验收副本、计划 lifecycle 与 `DEP-012` 历史状态已按现有来源修正，未删除底层证据。
- 公共 `llm-workflow` 补齐 testing route、上下文压缩恢复、检索回退和模板分组一致性，并以 `4491bb4` 推送 `origin/main`；TinySpire 私有语义仍只留在项目实例。授权接口、历史迁移与公共 workflow 三路独立复核均为 PASS。
- 离线验证新增全目录相对链接、授权语义、归档唯一性与计划 lifecycle 检查。完整结果见 `06_testing/2026-08-24-llm-project-knowledge-workflow-v2.md`；本轮不涉及 Unity、Luban、Addressables 或运行时代码。
- ByteRover 仅定向刷新 Status 与 Architecture 路由：查询能返回正确授权边界和精确原始路径；BRV 解析错误如实保留，人工复核后的 4 个文件以本地提交 `82117ba` 保存。无关 `testing_index` 改动未暂存，且没有 BRV 云同步。

## 2026-08-24 LLM 项目知识工作流 V2 试点（已验证文档结构）

- 新增 `STATUS.md` 作为唯一当前可变状态源；默认集收敛为 `README → STATUS → 至多一份相关页`，不再加载完整 changelog 或整本决策集。
- `SESSION_LOG.md` 改为按需 changelog；G3 计划原路径标 `archived`，G1/G2 被取代口径补 supersede 提示；活跃 Roadmap 将 G1～G3 压成摘要，plans/testing 重复索引从 80/196 行压到 28/28 行，底层证据未删除。
- 上位协作事实源、Daedalus 入口/Profile/Prompt、架构/术语/依赖与冻结 Battle Roadmap 均改指 `STATUS.md`；`DEP-007` 依据 CD-112/113/116 标 resolved，Pegasus 旧 Status 明确为设计/美术同步历史快照。
- 离线脚本、相对链接、UTF-8/BOM 与 `git diff --check` 通过；具体数字见 `06_testing/2026-08-24-llm-project-knowledge-workflow-v2.md`。这些结构检查不替代 Unity 或内容链路验收。
- 公共 `llm-workflow` 同时完成供应商无关的 Interface 精简、GPT-family 输出适配与 Optional Retrieval Adapter；独立只读审计通过后以 `310edca` 推送 `origin/main`，父项目只更新 submodule 指针，不把 TinySpire 语义写回公共仓库。

## 2026-08-24 ByteRover 项目检索适配（verified local）

- MCP/CLI 链路与初始空树 RED 已验证；用户明确批准后，5 份小型路由骨架成功 curate 为 4 个知识主题，任务 `f8289d91-6021-4b95-b17f-fc082c7d3666` 为 4/4 confirmed、0 failed。
- 带来源回查正确返回 `STATUS.md` 唯一状态源、G3 completed/verified、无已授权 active slice 与 G4-A 仅为 candidate，并给出 `STATUS.md`、`RUN_ROADMAP.md`、`README.md` 三个精确仓库相对路径；原文逐项核对一致。
- 生成知识由 BRV 自有 VC 先以 `70c49b4` 保存，验收索引刷新再以 `2eb1b37` 保存，最终 context-tree clean；`.brv/` 继续由父仓库忽略。Account/Space 与 BRV remote 未配置，因此本轮只证明本地检索闭环，不声称云同步。完整证据见 `06_testing/2026-08-24-byterover-project-context.md`。

## 2026-08-24 G3 确定性尖塔式 Act 地图（verified）

- G3 已按 Hermes 决策 012～016 完成并关闭：冻结明牌分层 DAG、唯一 Store/Flow、recipe-only schema v2、`BossGateReached` 与原子 `Terminal(Defeat)` 已落地；CD-116 明确取代 G1/G2 的 snapshot、schema v1 当前写入口和失败重开口径。
- 最终交互式完整 EditMode 为 **993/993 passed**；`Sync and Build All`、Local Addressables、12 个 `AssetBundleProvider` bundle、Packed Play 胜利到 Boss 门与失败终局两条进程级冷启动链均通过，产品 Console Error=0。测试档、旧档恢复与工具侧截图错误均已在验收页如实记录。
- G3 完成不授权 G4；G4-A 仍须独立 Grill、计划与明确授权。详细方案与证据只查 `plans/2026-08-24-g3-deterministic-act-map.md`、`06_testing/2026-08-24-g3-deterministic-act-map.md` 和 CD-116。

## 2026-08-24 G3 implementing checkpoint（superseded / compressed）

- 本中间快照曾记录 991/993、构建/手测 pending、Editor 与旧档处理等待；这些状态已被上方 `verified` 记录全部取代。详细 RED、修复和最终验收仍完整保留在 G3 验收页，本节不再复制过时状态。

## 2026-08-17 BattleCommandQueue 提交接口深化（implemented-and-verified）

- 本轮是独立架构维护，不改变 Run 当前 phase、slice 或 G3+ 授权状态。删除了生产调用者必须知道的 `PreRegister → 建立 pending → Submit → 拒绝回滚` 隐藏协议：`BattleCommandQueue.Submit` 现在内部签发 handle，Queue 公开只读 `Lifecycle`，其 `Queued` 事件携带原始不可变命令供 Hand/HUD 建立精确 pending。
- `BattleCommandRuntimeDriver`、`BattleTurnHudView`、`HandCardContainer` 不再注入或调用 concrete coordinator；结构性拒绝不发布 lifecycle，因此 UI 不再需要手工 pending 回滚。coordinator 的注册、匹配、取消、对账与 lifecycle 源降为 internal implementation；Queue 的权威序号、迭代 drain、FIFO continuation、system token、表现屏障、completion 与 fault 语义均未改变。
- 测试侧删除共享 `SubmitRegistered` 扩展和 Queue→coordinator registry；普通测试只调用生产 `Submit`，只有 internal scheduling 合同测试继续直测预注册算法。RED 先锁定 lifecycle 缺少原始 Command，迁移中又由重复预注册失败暴露旧 helper；最终 M8B 11/11、相关聚合 116/116、完整 EditMode job `09c7b62ffe5c4bcfa8d239b99e30f51a` 为 **953/953 passed**，Unity Console 编译 error=0，`git diff --check` 通过。
- 本轮没有修改 `BattleCardPlayEvaluation`、Run、存档、DataTables/GameData、Localization、Scene/Prefab、ProjectSettings、asmdef 或包依赖；没有运行 PlayMode、Player build、Addressables 或人工 BattleScene smoke。本轮按用户授权作为本地提交收口，不 push；既有 RunEntry visual 与其他用户 WIP 保持原状态。决定见 CD-115，方案与验证见 `plans/2026-08-17-battle-command-submission-interface-deepening.md`、`06_testing/2026-08-17-battle-command-submission-interface-deepening.md`。

## 2026-08-17 RunEntryScene 主入口视觉切片（verified，待用户视觉审阅）

- 先完成 Scene/Canvas/菜单/加载/DOTween seam audit：RunEntry 仍由现有 `RunEntryView` 动态组装单一 Overlay Canvas，CanvasScaler 保持 1920×1080、match 0.5；新增视觉只在 View 的生产资产 seam 创建，不修改 Presenter、RunStateStore、RunFlow、Battle、存档或导航规则。
- 已把确认的 `ENTRY-BG-002` 字节级复制到 Runtime UI 美术目录；从外部 v06 ivory 临时源裁取并中和为一张 1024² 共享细颗粒纹理，三张完整纸只以 tint 区分。三纸不接收 raycast，菜单与标题不在旋转根下；V06 基线米白边约 38.88%、总纸叠约 44.97%，没有放大为 60% 左侧大面。
- 五个既有 Button/TMP/Action 全部保留，视觉改为 459×99、27 px 对称切角、透明纸面内芯与细边线。红/黑/米白依次在 0/.12/.24s 入场，米白 1.00s 停稳，内容 1.10s 开始淡入；私有 DOTween Sequence 只播放一次并独立清理，无粒子、循环、视差、镜头或弹跳。
- Unity 6000.5.5f1 定向 EditMode job `c42339825cab4bc684c7df34b549e45e` 为 22/22；`Build Local Content` 20.569 秒成功。BuildLayout 证明背景/纸纹均为 RunEntryScene 的 `DataFromOtherAsset`，与场景同 bundle、Group=`TinySpire Scenes`、Provider=`AssetBundleProvider`，没有新 AddressableName。
- Packed Play 从 Bootstrap 实际进入 RunEntryScene，1920×1080 最终内容 alpha/交互、三纸共享不挡点击、17.52° 构图均通过；Settings → Back 往返成功且 `EntrancePlayCount=1`、无 active Sequence，Console error=0。截图与完整 importer/响应式/风险记录见 `06_testing/2026-08-17-run-entry-visual-slice.md`；决定见 CD-114。
- 21:9/窄窗已有几何自动化：超宽保留左侧基准构图区并裁背景上下，窄窗缩放菜单、右缘对齐背景以保护主塔；本轮只保留 16:9 实拍。未运行 Player build，也未实现 G3、地图、FishNet/多人、战斗、存档、GameData 或菜单业务重写。未 commit、未 push、未 stage/unstage；既有 Hermes staged/WIP 与三个 GameData 修改保持原状态。

## 2026-08-16 G2-A Run Persistence 与继续游戏（verified，待 Theseus 审查）

- 先完成 seam audit：现行 CD-112 与生产 Scene parent seam 以 Bootstrap root 跨场景持有 `RunStateStore` / `RunFlowService`；CD-009 的独立 RunScope 仍是未落地前瞻。本片以 Store 的显式 restore/clear 表达 active Run 生命周期，不修改 Scene、Prefab、parentReference、asmdef 或 Battle 结算结构。领域 `Run/Map/Battle` 不接触存档 IO；既有 Battle 地址校验器使用 `System.IO.Path` 不属于存档依赖，未顺手重构。
- G2-A1 新增 `RunSaveDocument` v1、严格 codec、显式 migration 入口、稳定态 mapper、配置兼容校验与 `IRunSaveStore` port。文档只含 Run/Hero/HP/Deck/Encounter、随机根、节点状态与 attempt 序号；`InBattle` / `Failed`、ActiveBattle、snapshot、BattleSession、卡区、敌人、队列、动画与 Unity Object 均不能进入存档。恢复除检查 Hero/Deck/Encounter ID 外，还要求存档最大生命与当前 Hero 配置一致，配置漂移会在 Continue 前类型化失败。
- G2-A2 在 Infrastructure 新增 `persistentDataPath/run-save.json` 单槽 Adapter：同目录唯一 temp、严格 UTF-8、durable flush、重新解析/迁移/校验、首次同卷 Move、已有档 `File.Replace`；任何失败都不使用 delete+copy 覆盖旧档。坏 JSON、非法 UTF-8、未知 schema、读取/写入/替换/删除失败与残留 temp 都返回可诊断结果；Run/Map/Battle 无 `System.IO`、PlayerPrefs、WX/TT SDK 或平台 `#if` 存档依赖。
- G2-A3 在 Hero 确认后提交 S0；胜利只在唯一 BattleResult bridge 完整结算、回到地图稳定态后提交 S1。BeginBattle、战斗过程、失败与 G1 重开不写/删/覆盖存档；Continue 只在用户点击后恢复最近成功稳定态。有效档新开局、坏档删除均需确认；commit 失败阻止推进并缓存同一 document 供重试，退出前提示并回退上一成功 checkpoint。当前唯一节点胜利后保留 S1 并显示“节点已清除、后续内容未接入”；G1 失败页/重开语义保持不变。
- TDD 证据：A1 10/10、A2 6/6、A3 Flow 19/19、Presenter/View 25/25 和 Localization 43/43 分阶段转绿；审阅补强的非法 UTF-8、Hero 最大生命漂移、删除失败后仍可 Continue、I/O 失败保留 temp 诊断、真实二次 Replace / Move / Delete 与冷启动 Completed S1 均先红后绿。最终相关聚合 job `a287a16c93f24a66b46c27e994cdc36b` 为 115/115，审阅补丁定向 job `96604e4d2d3c400e9eac5b51516c0c1f` 为 4/4，最终完整 EditMode job `0004316410dc4b1e9db8d80312499dc4` 为 **947/947 passed**、0 failed、0 skipped、19.641 秒。
- `DataTables/gen.bat` 成功；`TinySpire/Build/Sync and Build All` 成功，Local Addressables 构建 14.565 秒。唯一 Unity 6000.5.5f1 Editor 实走：无档 Continue 禁用 → Hero 1001 提交 S0 → 重启后确认框取消不删并 Continue → 入战期间正式档字节/时间戳与 S0 不变 → 真实 Queue 胜利后提交 S1（12/30、attempt 1）→ 再次重启恢复 Completed 与精确完成文案 → 经 UI 确认放弃并删除。各段运行时 Console error/warning 查询为 0；最终 EditMode 后仅有测试自身本地化通过日志与 Test Runner 保存结果日志，无产品异常。
- 本片没有实现或修改 G3+、Platform Save Spike、微信/抖音 SDK、云存档、多槽、奖励、地图生成、永久死亡、Player build 或平台存档迁移。当前路径仍受 `DefaultCompany/TinySpire` 的 `persistentDataPath` 身份影响；`File.Replace` 的目标平台能力仍须在未来明确授权的 Platform Save Spike / Player 验收中验证。完整证据见 `06_testing/2026-08-16-g2a-run-persistence.md`，决定见 CD-113。未 commit、未 push、未建分支。

## 2026-08-16 G1 阶段关闭 → G2 可执行路线图交接（documentation-only）

- 用户明确裁决 G1-A 的 verified 结果构成 G1 阶段完成依据；当前不再从“G1 剩余范围或 G2”二选一，而是进入 G2。`fa14889` 已位于 `main` 与 `origin/main`；既有 G1-A 测试、构建和手测证据不在本轮重写或重复冒充新验证。
- `RUN_ROADMAP.md` 已从“阶段名 + 候选问题”重写为可执行路线图：G2～G8 各自列明玩家结果、候选子切片、主要交付物、通过标准、阶段完成门槛、依赖和明确排除项；所有候选切片仍须分别 Grill、计划、授权和验收。
- 当前工作区已有 Hermes/Pegasus 的 `design/2026-08-16-g2a-run-persistence-grill.md`，状态为 `proposed-for-plan`。Roadmap 保留 G2-A 名称，并把 Save Document、本地原子单槽、检查点/继续游戏拆成 A1～A3 串行停止点；未把它们误登记为三个已授权 Goal。
- 本轮没有修改运行时代码、配置表、Scene/Prefab 或 Unity 资产，也没有运行 Unity、测试或 Addressables 构建。没有授权 G2 技术审计、实施计划或实现。
- 本轮遵守所有权边界，只修改 `Docs/Copilot_Daedalus/`；根协作状态文档与 `Docs/Hermes_Pegasus/STATUS.md` 中仍有 BattleScene/G1 旧状态，需由对应 owner 后续同步，不在本轮顺手改写。

## 2026-08-16 G1-A 基础入口 → 首战最小 Run 生命周期（verified，待 Theseus 审查）

- 已按冻结的 G1-A Grill 与实施计划完成一个进程内最小竖切：Bootstrap 默认进入功能性 `RunEntryScene`；主菜单、设置、图鉴、统计、双 Hero 单选、临时单节点地图、失败页都在同一 Scene 内以 TMP + i18n 切换。Hero 1001/1002 都可创建单人 Run；没有实现多人队伍、存档、奖励、多节点或主动退出 Run。
- `RunStateStore` 是跨场景 Run 事实的唯一写入所有者，冻结进战 snapshot 并签发 attempt 身份；`RunFlowService` 只编排。Battle 继续只经 `IBattleSetupOptionsSource` / `BattleSetupOptions` 取得 Hero、当前生命、牌组、Encounter 与 seed，`BattleSession` 已证明实际消费这些值。Battle → Run 只由 child Scope 的 `BattleResultRunBridge` 消费稳定、屏障后 exactly-once `BattleResult`；旧 Scope Dispose 后解除订阅。Run 模式隐藏旧 HUD Restart/Exit，legacy Battle 入口仍有 Inspector fallback。
- 胜利通过真实 Queue 命令打完：1001 以 seed `1143371176`、30/30、Deck 1001 入战，结算 17/30；`BattleResult` 后回 `RunEntryScene`，节点 `Completed`、生命 17/30。失败也通过真实回合/敌人行动完成：1002 attempt 1 seed `768055331` 失败后恢复 snapshot 70/70；重开 attempt 2 seed `261103211`，新 Session 为 70/70、Hand 5 / Draw 7 / Discard 0 / Exhaust 0 / Power 0，未继承失败战临时状态。
- 当前唯一 Unity 6000.5.5f1 Editor 在 Packed Play Mode / Use Existing Build 中完成 `BootstrapScene → RunEntryScene → BattleScene → RunEntryScene` 两条胜败链；设置/占位页返回、两名 Hero 选择、节点、失败重开均通过，运行时 Console Error 查询为 0。`TinySpire/Build/Sync and Build All` 成功，Addressables 15.018 秒，最新 BuildLayout `BuildError` 为空且 RunEntryScene 由 `AssetBundleProvider` 打入独立 bundle。
- 最终完整 EditMode job `55272b6354df42b6a0f351975ab58e71` 为 **873/873 passed**、0 failed、0 skipped、24.2229056 秒；中间 RED 包含 seed 具体碰撞 0/1、RunEntry Scope 0/1、idle RunFlow legacy 1/3、生产 DI 的真实 VContainer 异常，以及禁用 Domain Reload 下重复分配默认 UI Actions 导致的 870/873，均已逐项转绿；完整套件后 View 再跑仍为 3/3。详细命令、job、构建与手测证据见 `06_testing/2026-08-16-g1a-entry-first-battle-run-lifecycle.md`；代码决定见 CD-112。
- 当前工作区停在 `main`、HEAD `65fd7846eaf414b89987c33785879844d4c2e023`，未 commit、未 push，等待 Theseus 审查。已知非阻塞风险：CJK TMP 字体使用当前 OS 字体动态创建，当前 Windows 验收通过但仓库未携带跨平台字体资产；Scene builder 原子重试与 scene group stale 清理仍为 Editor 工具 P2。G1-A verified 不自动授权 G1 后续或 G2。

## 2026-08-14 BattleScene → Run 交接最小 seam 修复（自动门禁与 Editor 原生串行验收完成，G1 仍未开始）

- 所有者在只读审计完成后另行授权 B2R-101/201 与 B2R-102/202 的最小边界加固；这不是 P0/P1/blocker 修复，也不改变审计的 `SAFE_TO_START_G1_GRILL` 结论。G1 仍为 `needs-grill`。
- W1 新增 Queue-owned 公开不可变 `BattleResult`，支持 Victory / Defeat，并冻结 `Kind / AuthoritySequence / RoundNumber` 与按 `CombatantId` 稳定排序的不可变 `Players`；每个玩家结算快照含 `CombatantId / TemplateId / Health / MaxHealth / IsAlive`。Queue 在终局 settlement 与 continuation 完全冻结后才创建同一结果对象，表现层直接消费它映射既有文案，公开只读 `Result` 在表现屏障完成后 exactly-once 发布；连续 Battle Scope 不残留旧结果。`Abandoned`、牌组 / 奖励与 `BattleResult → RunState` 原子写回仍留给 G1。
- W2 新增父 Scope `IBattleSetupOptionsSource` 注入边界；每个 Battle child Scope 只求值一次并冻结唯一 `BattleSetupOptions`，无来源时继续使用 Inspector `1001 / 5001 / 5`。只注入 hero / encounter / seed；生命与牌组仍从模板创建，Run 根种子/恢复、初始 HP / 牌组输入仍属 DEP-007 / G1。
- 结算快照补强先得到 Editor compile RED：6 个缺失 `Players` / 快照类型错误；随后两个精确 GREEN `cdbee956af0449ceb154b459d9115ab6` 与 `9b2cc0f1a9c94bd3883bac240cfeba79` 均为 1/1。QueueM8D `439414c167ee4058ae6ce48bfd6e137b` 14/14、相关 9 个 fixture `445e2407b7494d7291c9d192b12ba0fe` 127/127、完整 EditMode `7057ee5000a24d739b347076ee766c6e` **811/811**（18.8400845 秒）均通过；Runtime / Editor 静态 build 分别为 0 error / 6 条既有 warning 与 0 error / 12 条既有 warning。
- 唯一 Unity Editor 已串行通过 BootstrapScene → BattleScene、真实 Queue 胜利（屏障后 Result 与 Players 匹配）、HUD Restart、旧 Scope 消失与新 Result 为空、真实 Queue 失败、HUD Exit 临时 probe 连点两次仅调用一次且按钮锁、2 秒无晚到 Result、`activeTweens=0`，以及 Stop 后回到 BootstrapScene 且 `BattleLifetimeScope=0`；Console error=0。Editor 只证明 HUD Exit listener / guard，没有证明 Player OS 进程实际退出。B2R-203 继续作为 owner open；未修改 `Docs/Hermes_Pegasus/**`。决定见 CD-111，计划与证据见 `plans/2026-08-14-battlescene-to-run-seam-corrections.md`、`06_testing/2026-08-14-battlescene-to-run-seam-corrections.md`。

## 2026-08-14 BattleScene → Run 交接审计已执行（SAFE_TO_START_G1_GRILL）

- 外部只读审计已执行并归档：结论 SAFE_TO_START_G1_GRILL，无 ExistingDefect / PreG1Blocker / P0 / P1。基线核验通过：tag `milestone-battlescene-mvp-2026-08-14` 解引用 `e07e39a`，快照 `18d9023` 可解析且为其后代，两者之间 `TinySpire/**` 无已提交变化；审计范围内源码/测试/Scene/Prefab 无未提交改动。
- 两轴独立评审共呈报 5 条：4 × G1DesignInput·P2（Battle 终局结果 seam 与输入注入 seam，两轴各自独立收敛到同一结论）+ 1 × DocumentationDrift·P3（Pegasus STATUS / project-definition / decision-locks D-003 漂移，待所有者裁决，不阻塞 Grill）。
- 已核验不成立：唯一写 seam（`BattleCommandQueue.Submit`）无绕过、UI 无权威写入、确定性随机无跨域耦合、场景生命周期与订阅销毁合规、完成定义与 807/807 记录一致。
- G1 仍为 `needs-grill`；不存在开始 G1 首片 Grill 前必须纠正的真实缺陷，也没有发生任何修复授权。审计记录：`06_testing/2026-08-14-battlescene-to-run-audit.md`。

## 2026-08-14 BattleScene → Run 外部只读交接审计包（待执行）

- 已在 `10_communication/` 建立 BattleScene → Run 阶段交接审计任务书与 DeepSeek Harness 薄 Prompt。审计固定产品代码 tag `milestone-battlescene-mvp-2026-08-14`（`e07e39a`），并以 `18d9023` 的 Roadmap 换轨文档作为 Run 交接语义；两提交之间 `TinySpire/**` 无已提交变化。
- 本轮只准备外发材料，没有运行外部审计、Unity、build 或测试，也没有授权修改代码。G1 仍为 `needs-grill`；审计只判断是否存在开始 Grill 前必须纠正的真实缺陷或生命周期阻塞。
- DeepSeek Harness 输出首先作为 `source-only` 审查材料返回，不自动成为事实或修复授权。确认的 blocker 才另建窄修复计划；G1 设计问题回到对应切片 Grill，UI / 动画品质债继续留在独立轨道。
- 入口：`10_communication/2026-08-14-battlescene-to-run-audit-brief.md` 与 `10_communication/2026-08-14-battlescene-to-run-audit.deepseek-harness-prompt.md`。

## 2026-08-14 BattleScene MVP 检查点与 Run 路线图交接（G1 未开始）

- BattleScene MVP 与当前基础卡牌运行时已形成 Git 检查点：commit `e07e39a`，tag `milestone-battlescene-mvp-2026-08-14`，分支 `codex/battlescene-mvp-checkpoint` 已推送到 GitHub。该检查点保留完整 EditMode **807/807 passed** 的最新权威记录。
- `ROADMAP.md` 原地冻结为 M0～M10 的固定归档，不移动路径，以保留历史计划和验收页的引用；独立 `RUN_ROADMAP.md` 开始承担 G1～G8 的阶段骨架、依赖和完成定义。根 `README.md` 已同步公开阶段转换。
- G1 当前状态为 `needs-grill`：尚未 Grill、尚未拆成可执行切片、尚未形成 G1 实施计划，也没有任何运行时代码、Scene、Prefab、表格或构建授权。未来每个切片以及出现独立玩法/生命周期选择的子切片都分别 Grill 和授权。
- 战斗 UI、视觉反馈与动画被认定为功能可用但非最终品质的产品债；除非阻塞某个新玩法切片的可读性或可操作性，否则不阻断 Run 本体推进。大范围表现改造后续单独切片。
- Roadmap 换轨已经提交并推送到 `main`，临时换轨分支已删除。下一动作先执行上述只读交接审计；审计结果经本仓裁决后，才决定直接开始 G1 首片 Grill，或先建立一个独立的最小修复切片。
- 决定见 `CODE_DECISIONS.md` CD-110；Run 阶段入口见 `RUN_ROADMAP.md`。

## 2026-08-14 共享 settlement-derived trigger、Ironclad Juggernaut 与机枪兵 Unstoppable（已完成 Unity 原生验证）

- 新增共享 `BattleSettlementTriggerEngine`：Power 在父出牌事务内以 Prepare / Validate / Commit 注册持久触发；后续已提交 settlement 按 settlement 顺序、再按注册顺序冻结 trigger intent。Queue 只在父命令表现屏障完成后，以内部 system-token `ResolveSettlementTriggers` continuation 执行子事务；外部仍只能调用 `BattleCommandQueue.Submit`。
- `Juggernaut`（3169）基础态为 2 Energy / Rare Power / Self / PowerPile，Effect 4019 的目标语法为 raw type 10 / `Attribute.None` / value 6。每条目标为持有者且实际增加量为正的 `BattleBlockGainedSettlement` 冻结一次随机存活敌人 6 点子事务；伤害不读 Strength / Vulnerable，仍经目标 Block / HP / 致死写链。
- `Unstoppable / 势不可挡`（3250）基础态为 1 Energy / Rare Power / Self / PowerPile / Program 50。持有者造成致死，或把目标正 Block 从正值降到 0 时，从静态表顺序冻结的 `Implemented` / Attack / 非 Shoot / 可自动解析目标候选中随机创建一张临时卡，免费出牌并强制 Exhaust。当前注册 ID 在自己派生链中被抑制，避免同一 Power 自递归。
- 本切片不新增 HUD / Prefab / Scene；Juggernaut 升级伤害与 Unstoppable 升级追加 debuff 触发仍只是 metadata，升级实例未实现。正式生成后全项目 168 张为 **98/70**、Ironclad **15/70**、Marine **82/0**（V1 **64/0**、V2 **18/0**）、Effect **19**；Effect 强枚举已替代开发期 raw 10 引用。
- 正式 xlsx SHA-256：Enums `D899B0C39E01A5829A8FDC0BA4EB0F4A36609E4BF177EF92D17BC2976E6BF194`、Effect `3224852248155DC34A0ADE73A2C7693E8F4AB8DFD5D041406E3448581EE15A9D`、Card `C22E2380915C4D847CB073228785EC20453C9170832C12E3977DDFE8B831A253`、i18n `E6329D49F669DB3FA4223CF5EE7CCCBAF5DA5F9B3102A8C8DDB1D7F009987617`。生成物 SHA-256：Card JSON `DDDC4CE73D93A3C40939EE096C2E1CA6CCDE82D187D8FEDAF9533CA39FEA0FDD`、Effect JSON `67A5865E17F803CCE614B617B207C407B42F20959F615B9FCD04C5B62FBD9868`、`EffectType.cs` `BA13A3CF7D0584C44A4C2AB74C3F3B5C4B4FEF5AF79F82CD8985923F1ED526FA`。
- Luban 通过。首次 `Sync and Build All` 因 i18n 缺少 `{triggerDamage}` 被 validator 正确拒绝；只修复该占位符后重跑成功，Localization en/zh 更新于 05:25:02，Addressables 15.175 秒，BuildLayout SHA-256 `429C1CD806275B7095205307B67DAE71F39678C19E53E3C39B574193ACDAA769`。Runtime 静态编译 0 error / 6 warning，Editor 0 error / 12 warning；定向任务 `054b6bcd5d734f729a2f1f95c4e7a80d` 7/7（0.6658563 秒），完整 EditMode `d156b8e2537546ef9e83da0ef5dadd2a` 807/807（19.3037496 秒）。未单独重跑另一个 aggregate job，full 807/807 已包含本切片相关聚合范围。决定见 `CODE_DECISIONS.md` CD-109，完整证据见 `06_testing/2026-08-14-shared-settlement-trigger-juggernaut-unstoppable-runtime.md`。

## 2026-08-14 共享触发出牌、Ironclad Havoc 与机枪兵 Opportunistic Strike（已完成 Unity 原生验证）

- 新增 Queue-owned system-token continuation：触发卡不会递归直调 Turn，而是在当前命令提交后由 Queue 以内部令牌串行提交，沿用唯一命令、表现屏障与 fault 路径。`Havoc`（3108）抽出抽牌堆顶卡、免费打出并强制 Exhaust；`Opportunistic Strike`（3243）只在上一张成功牌为 Attack / Shoot 时，从当前手牌随机选一张 Attack 免费打出。
- 3108 / 3243 基础态翻为 `Implemented`：全项目 168 张为 96/72，Ironclad 14/71，Marine 81/1（V1 63/1、V2 18/0），Effect 18。Havoc 升级费用 0 与 Opportunistic 升级改为选择攻击手牌仍仅为 metadata。
- 正式工作簿 SHA-256：Enums `EA91547F88FBB05C74A8DFDBFA5864A36F72FC309F5B51421564AF3CEF8EB7CF`、Effect `2639CE5F87BAA6774D32C199CB4A31A82A0DE47EF4CD9E2B8E3BA419F74EE73D`、Card `D5ECD06EE838ED239E0BBB60D8449396F82EEF14662639B551DF8A9A51200DE1`、i18n `602005AA8DD5749BCB3BC9E5ACA917401B5C85A859CF487A137C7309F897477B`；生成 Card / Effect JSON 分别为 `E5152D0C…` / `F925AC30…`。04:03:07 BuildLayout 证明 GameData 由 `AssetBundleProvider` 进入物理 bundle。
- 初次 full 暴露强枚举与“非展示 Effect 不应要求本地化”门禁不一致；修正后 Localization cleanup `be23e45bedbe430f87173f0e3e913a0c` 1/1、定向 `a35d7b7a38f64ad5936132655e7f5318` 8/8、完整 EditMode `dd5ba1f2b6004e0a85a3aee6de4256e4` 802/802（19.3797688 秒），静态 Editor build 0 error / 12 条既有 warning。未 commit、未 push。
- 决定见 `CODE_DECISIONS.md` CD-108，完整证据见 `06_testing/2026-08-14-shared-triggered-play-havoc-opportunistic-strike-runtime.md`。

## 2026-08-14 共享 Block 保留、Ironclad Barricade 与机枪兵 Garrison（已完成 Unity 原生验证）

- 新增共享 `BattleBlockRetention`，永久与计时两类授权都使用一次性 Prepare / Validate / Commit：`Barricade`（3157）通过 Effect 4017 建立永久 Block 保留；`Garrison`（3246）获得 12 Block 与 2 层计时保留。玩家回合开始先按开始时层数决定是否跳过 Block 清除，再令计时层数 `2→1→0`；降为 0 的当次仍保留，下一次玩家回合开始才清 Block。
- Garrison 基础态要求从来源以外的当前手牌精确选择 2 个不同实例；UI 会话在收齐 2 张前不提交，来源/右键取消。所选牌只跳过当前一次 `EndPlayerAction` 的弃牌，下一次行动结束恢复普通弃牌规则，不把“保留手牌”混入共享 Block 模块。Barricade 进入 PowerPile，Garrison 进入 DiscardPile。
- 3157 / 3246 已由 `CatalogOnly` 翻为 `Implemented`：全项目 168 张为 94/74，Ironclad 13/72，Marine 80/2（V1 62/2、V2 18/0），Effect 17。Barricade 升级 2 Energy、Garrison 升级 15 Block / 选择 3 张仍只是作者表与本地化 metadata；升级实例运行时未实现。
- 正式工作簿 SHA-256 / bytes：Enums `8fc42a27fced4998a3a72940bed75e32f09f3e8e0d5aff8e26369fabaa38e4b5` / 11084、Effect `c55948630183518cebf28e7516d782cceb388b02592410b6a3e71ebbd8e2eabb` / 4638、Card `2874c0df732f7aced41641437910beaca5bffdf3424c4d10895876f7a2f3e3c3` / 23210、i18n `7812301c2acdcadbf62e1cefc2c26ad56ad44879aab09e5afecc070d0e58a699` / 29098。
- `Sync and Build All` 因两次域重载分阶段完成生成、Localization Import / Validate 与 Local Addressables；02:59:03 的 BuildLayout 证明 Card / Effect JSON 位于 `AssetBundleProvider` 的物理 GameData bundle。生成前 3/3 任务前缀 `006e…`、最终定向 `17e031…` 300/300、完整 EditMode `b4d970…` 798/798 均通过；静态 build 0 error / 12 条既有 warning。未 commit、未 push。
- 共享契约与边界见 `CODE_DECISIONS.md` CD-107；完整证据见 `06_testing/2026-08-14-shared-block-retention-barricade-garrison-runtime.md`。

## 2026-08-14 共享来源动态伤害、Ironclad Body Slam 与机枪兵 Secondhand Smoke / Poison（已完成 Unity 原生验证）

- 公共 Effect 计划新增“来源当前 Block”数值来源：Prepare 冻结 source Block 作为普通攻击的 base magnitude，随后仍走 Strength、目标 Vulnerable、目标 Block / HP 与致死规则；来源 Block 不被消耗。`Body Slam`（3105）基础态据此造成动态伤害；正式基础与升级文本均为 EN `Deal {damage} damage, equal to your Block.`、ZH `造成 {damage} 点伤害，数值等同于你当前的格挡。`。`{damage}` 是 Localization validator 要求的绑定占位符，运行时显示来源当前 Block；升级行为仍只是 metadata。
- 通用 Poison 由参与者权威事实持有，并以 Apply / Tick 的 Prepare、Validate、Commit 契约提交。正层 Poison 在行动开始绕过 Block 失去 `min(Poison, Health)` 点生命，并把层数减 1；致死同样减层，零层不写事实或 settlement。敌人非致死后继续行为与意图推进，致死则产生 source-not-alive skip、不推进意图并保留 Encounter continuation；玩家非致死后继续 Block / 资源 / 抽牌，致死则跳过这些重置并在状态机调用栈退出后结束战斗。
- `Secondhand Smoke / 二手烟`（3270）Program 70 冻结来源当前 Smoke 并向显式目标施加同值 Poison，不消耗或改写来源 Smoke；Smoke 为 0 时仍成功支付与归宿，但不伪造 Poison settlement。普通来源牌归宿在 Poison 首写前冻结并于末尾一次提交，因此同步 Poison observer 改动卡区时仍以冻结布局收口；会 Draw / Replace 等改写卡区的程序继续使用各自深事务，不被普通归宿计划覆盖。升级“来源与目标烟雾总和”仍仅是 metadata，升级实例未实现。
- 状态种类计数统一为 Strength、Vulnerable、通用 Poison 与 17 种机枪兵私有状态，最大 20 种；同种 Poison 不论层数只计一次。3277 按来源、3278 / 3279 按目标冻结该集合。Poison tick 表现只派生 `HealthLossNumber`，致死再追加 `DeathTransition`，不会伪造 Attack hit-shake 或 Block absorbed；本切片未获 Prefab 修改授权，因此没有常驻 Poison 图标、层数 HUD 或脉冲。
- 已完成的运行时证据包括：公式与 3277 Poison 计数任务前缀 `419c…` 2/2、原子归宿与修复任务前缀 `b5f…` 8/8、行为聚合任务前缀 `79a…` 289/289。生成前完整 EditMode 任务前缀 `fd6…` 共 791 项，其中 7 项仅因 3105 / 3270 尚保持 `CatalogOnly` 的预期目录红灯；它不是发布完成证据。
- 正式工作簿 SHA-256 / bytes 为 Enums `48aa59ec32cba63429678f34d2f88d8010d0ba2842865e021d3578b93ce2ef5e` / 10982、Effect `cac78b6069764a037275b3261125e379de9a8f75a358f34c9d430ac98dff6d14` / 4603、Card `01c1613de65ee7e9b6fb49a774fecb4e31c53535c2186cb0b5e9bbac03358be0` / 23197、i18n `0bb37d8ba79bff9c3d8853b95af7c436373893385c0c62055e5400be2fbd8d0b` / 29057。Luban、Localization Import / Validate 与 `TinySpire/Build/Sync and Build All` 均成功；Addressables 为 50.667 秒。生成后全项目为 92/76、Ironclad 12/73、Marine 79/3（V1 61/3、V2 18/0），Effect 16。
- 生成 `EffectType.cs` 为 1083 bytes / `d901715fe9802566b137215d9b8d655d68c3bef60bd41c972885dce4c846b9b9`，Card JSON 为 123848 bytes / `5b84dcf7a0d1757fae7a901b5e8bab990b702c0a69cb1a9df8897811241984aa`，Effect JSON 为 1541 bytes / `7b435ce2cde44571c988f93dc1ef6c00668d2220519c52749062734eb919cabb`。Localization EN asset 为 93547 bytes / `2d717822eb9a32ef6374908d4c32c3508233b5f74e23d6df0c67638af5d2e32a`，ZH-CN asset 为 111579 bytes / `204bac90744eff72501a8f7b8b70de8fbcf0712ba3dcf2700a61cd8320c74b90`。BuildLayout `TinySpire/Library/com.unity.addressables/buildlayout.json` 为 134621 bytes / `74a51e87ebc1e938caca6eacd7e0f6cd8a7ccbd8f23ff4c4217f670ef79aff3`，证明两份 JSON 均由 `AssetBundleProvider` 进入 12201 bytes 的物理 bundle `tinyspiregamedata_assets_all_2779cc5206157ad3345f769bdba15759.bundle`（SHA-256 `5711d9ce71d7da896535340d2c843ff6faffcb12b38384f12375336814f33eea`）。
- 最终定向任务 `88e36d2a5cbb47b7b4a67207dad00856` 为 9/9 passed（1.1633036 秒），完整 EditMode `9ca3d43a79d24b25a917fad7b6166584` 为 793/793 passed（20.0509052 秒）；两者是本切片最终权威门禁。移除生产、Queue 测试与 I3 的裸数值 7 后，精确任务 `40af8c25ba4442ffbe9e98451890f01c` 为 1/1 passed，静态 Editor build 为 0 error / 12 条既有 warning。最终资产屏障为 `refresh_triggered=true / compile=false / idle`，Console 再清理后 0 error，唯一 Editor、tests idle / null。
- 已知 P2：玩家回合开始的 Poison 与其后整轮 reset 尚不是一个跨模块联合事务；敌人未来若开放 Regeneration，还需让其行动结束治疗计划读取 Poison 后投影生命。升级实例、默认 Deck、奖励、Run、多人、Scene / Prefab 与常驻 Poison HUD 不在本切片范围。决定见 `CODE_DECISIONS.md` CD-106，当前证据见 `06_testing/2026-08-14-shared-source-magnitude-poison-body-slam-secondhand-smoke-runtime.md`。

## 2026-08-13 共享重复伤害、Ironclad Sword Boomerang 与机枪兵幻彩射击（已完成 Unity 原生验证）

- 新增 concrete `BattlePreparedRepeatedDamagePlan` 与 `BattleRepeatedDamageExecutor`：Prepare 在首次写入前冻结来源、Encounter 全体敌人标量、每段目标/主伤 outcome/后效终态投影、卡牌目标随机流 before/after 与 settlement 数；Validate 拒绝 owner、标量、Encounter、RNG、顺序或一次性生命周期漂移；Commit 不再选目标或重算。当前只开放 `FixedEnemy` 与 `RandomLivingEnemyPerHit` 两个真实策略。
- 通用 `BattleRepeatedDamageEffectAdapter` 只接受普通 `DealDamage / Attribute.None` grammar，并复用既有伤害 outcome 与内部写入口。机枪兵 `MachineGunnerRepeatedDamageHitSequence` 仅在职业侧冻结 Stim、IncendiaryAmmo、PortableHelper 与 17 种私有状态。共享 planner 只拥有目标、投影、随机和计划生命周期，没有卡牌 ID、名称、Program 79、Ammo 或职业后效分支。
- 通用卡牌目标随机流的唯一可变 `GameRandom` 已归 `BattleTurnController` 所有；`BattleSession` 只保存由战斗种子复制的不可变 `CardTargetRandomSeed`，Queue 只读暴露当前状态。成功 Commit 才推进冻结 after；规则、费用、配置或快照失败保持 RNG 零推进。
- `Sword Boomerang`（3116）基础态已翻为 `Implemented`：1 Energy、Common Attack、RandomEnemy、DiscardPile，三条绑定 `damage` / `damageRepeat1` / `damageRepeat2` 均引用 4015=`DealDamage / None / 3`。三击逐段从投影存活敌人重选，击杀目标从后续候选移除；没有存活敌人时停止尾段且不再取随机数。升级第 4 击仍只是元数据。
- `Prismatic Shot / 幻彩射击`（3279）基础态已翻为 `Implemented`：0 Energy、Rare Attack、显式 Enemy、Program 79、基础 Ammo 1。目标起始状态种类 `S` 由 Strength 非零、Vulnerable 正层和 17 种职业私有状态正层各计一次；逻辑段为 `[6, 9 × S]`。Stim 为每个逻辑段追加紧邻同值复制，并把整卡 Ammo 冻结为 `1 + logicalCount`；每段严格执行 `main Damage → IncendiaryAmmo Burn → PortableHelper`，固定目标死亡即停止且不重定向。升级首段 9、重复段 9 仍仅为元数据。
- 初次广义行为聚合 `14131e7fa23c4f14a3a08e2cad0da556` 完成 250 项但有 16 项失败；最小化后确认既有机枪兵复合卡区 Prepare 漏算稍后前置的 Energy/Ammo settlement，触发“settlement 顺序不连续”。修复在 `ExecutePlayerCard` 的本地 `settlements` 初始化时先放 `EnergySpent`，实际 Ammo 大于 0 时再放 `AmmoSpent`，所有深卡区计划随后自然以 `settlements.Count` 冻结顺序；首次权威写仍位于全部 Validate 之后。代表回归 `6ee679521f4c45d9a69b9984110c51bb` 5/5、最终行为聚合 `4ea4eff81b3c4ce786e318d0902c1ed4` 243/243 均通过。
- 正式工作簿 SHA-256 为 Enums `DC35FC55DF7A4223347F81054C09DF88DDEA3B6EB88DA36DE41499562DD7618E`、Card `EA90C1A34FBDD9C54EBE2832C6CCC796DC4752A6B90C15F6A42BDB8C03A2CDF1`、Effect `35BF163D09E6F8AA6478C134D90A5FBAC304CC3135357D8237909DBC87ECAE64`、i18n `B80CD6EDCD0EAE2F52812B1CFF5DDAD96C1AB0507CD05E012C919DB05122215F`。Luban、Localization Import/Validate 与 `Sync and Build All` 成功；Addressables 13.962 秒，最新 BuildLayout `TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.21.31.49.json` 证明 Card/Effect JSON 由 `AssetBundleProvider` 写入 12182 bytes 物理 GameData bundle。
- 全项目 168 张现为 **90/78**，Ironclad 为 **11/74**，Marine 为 **78/4**（V1 61/3、V2 17/1），Effect 为 **15**。Runtime 静态编译 0 error / 6 条既有 warning，Editor 0 error / 12 条既有 warning；双卡定向 `6932f72f288a477ca5869c21e3ac3996` 11/11、正式门禁 `908e5fb8b93e437d89533bb1b727231a` 53/53、完整 EditMode `3e0a091d891e4f918668b99cb4a20157` **776/776 passed**（77.7525946 秒），最终 Console 已清空。决策与证据见 `CODE_DECISIONS.md` CD-105 和 `06_testing/2026-08-13-shared-repeated-damage-sword-boomerang-prismatic-shot-runtime.md`。

## 2026-08-13 共享 Heal、Ironclad Not Yet 与机枪兵战地手术（已完成 Unity 原生验证）

- 公共 `EffectType.Heal = 6` 已接入普通 Effect 事务。`BattleHealthRestorationOutcomeResolver` 在首次写入前冻结请求量、目标前后生命、生命上限和实际恢复量；普通 Effect 与职业 Regeneration 共用 internal `BattleCombatantEffectOperations.ApplyPreparedHealthRestoration`，没有第二个 Health 写入口。`BattleHealthRestoredSettlement` 保留实际为 0 的治疗记录，表现只对正实际量显示 `+N`。
- `Not Yet`（3171）基础态已从 `CatalogOnly` 翻为 `Implemented`：2 Energy、Rare Skill、Self、Hand→ExhaustPile，以 `heal:4014` 请求恢复 10。缺 7 HP 时实际恢复 7；满生命仍支付、记录 requested 10 / actual 0 并 Exhaust。Heal 后缺失后续 Effect 会在 Energy、Health、卡区、随机流、Turn 和 settlement 首写前整体失败。升级恢复 13 仍只是目录与本地化元数据。
- `Field Surgery / 战地手术`（3231）基础态已翻为 `Implemented`：1 Energy、Rare Skill、Self、Hand→ExhaustPile，Program 31 出牌时只获得 Regeneration 5 与 Shackle 1，不立即治疗。Regeneration 作为追加的第 17 个职业私有状态保留旧枚举值，并进入 3277 / 3278 的状态种类计数；Shackle 的 Attack 拒绝规则不变，当前其余 16 种私有状态可进入成功计数。
- 玩家回合末来源顺序已锁定为 `Shackle 清零 → LoseStrength 清零 → Heal → Regeneration -1 → Bomb → Burn`。顺序红灯 `c8d4aa4ddf7347dc8d6515f297c9ed90` 精确证明旧实现把 Heal 放在状态清理之前；修复后 `ed358fb765e74283848a28d40c9ae3ce` 为 1/1 passed。Field Surgery 出牌红灯/绿灯为 `32984ebfa0e14118a9f16f5ea3606c0a` / `5084062eb6b041e8a64512cfcde701dc`；Not Yet Heal 红灯/绿灯为 `4b1ef61209f749fe87138f6e9a767175` / `94d085dec3c74d2287831846b0baddba`。
- 治疗表现除了 plan / factory 回归，还以 `BattleFloatingNumberViewTests.CreateTween_HealthRestoredNumber_UsesPositiveTextAndHealingGreen` 精确锁定视图输出；任务 `4d5e4253e93840bd849571512f5f0a43` 为 1/1 passed，文本为 `+7`，颜色为 RGBA `(105,235,185,255)`。补强后的最终聚合与完整任务已重新执行并收口。
- 正式工作簿 SHA-256 为 Enums `dc35fc55df7a4223347f81054c09df88ddea3b6eb88da36de41499562dd7618e`、Effect `34eef4012c2b858e43fb0f7cb7c2417e1a3caa34d5afa3dcb46dfbd61c465af0`、Card `7c57c0a024d445d990ee275e7474a5460f7055504b1169f0b74dfd525d3665f3`、i18n `bd37b5660cbd5b1ceff8c07a58410c4f49e124acbdc3b97d893d4754b8551f5e`。Luban 于 19:31:59 成功；Card / Effect JSON SHA-256 为 `A47F249F2007ED80707354C263B3154313C96DD3C41FF58F743FB9494A7A1752` / `32036F53048206871D39C22A56CCDF74B3FA01976078AA49047E79DA4308986B`。全项目 168 张为 88/80，Ironclad 10/75，Marine 77/5（V1 61/3、V2 16/2），Effect 14 项。
- Localization Import / Validate 与 `Sync and Build All` 成功，Addressables 子构建 11.968 秒；最新 BuildLayout 为 `TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.19.37.10.json`，真实 GameData bundle 为 `TinySpire/Library/com.unity.addressables/aa/Windows/StandaloneWindows64/tinyspiregamedata_assets_all_77a1973868c636fe147c61465e862169.bundle`。Runtime 静态编译 0 error / 6 warning，Editor 0 error / 12 warning；来源顺序修正后的最终精确行为 `b511f5ddcd2041a9b264c0f982c4b600` 为 9/9（0.3818676 秒）、正式目录 `c3e5c7dbcb534cd18a85b635761fb8d7` 为 50/50、含治疗视图与真实 AB 的最终聚合 `818f8283386b4d86aa625c6d95284245` 为 243/243（10.3679275 秒）、完整 EditMode `c6a86ba528804a13b1c84fe38c28b48b` 为 **766/766 passed**（18.1031754 秒）。升级 Not Yet 13 / Field Surgery 6、AnyAlly / 多玩家、默认 Deck、奖励、Run、Scene / Prefab 与其他目录卡均未实现；决策与证据见 `CODE_DECISIONS.md` CD-104 和 `06_testing/2026-08-13-shared-heal-not-yet-field-surgery-runtime.md`。

## 2026-08-13 STS2 Ironclad Burning Pact 与通用选择消耗抽牌事务（已完成 Unity 原生验证）

- `Burning Pact`（3125）基础态已从 `CatalogOnly` 翻为 `Implemented`：1 Energy、Skill、Self、Hand→DiscardPile，`Program.None` 通过有序绑定 `exhaustCards:4012,cards:4013` 表达“选择并消耗另一张手牌，然后抽 2 张”。若来源之外没有候选牌，则不创建选择请求、不产生 Exhaust，但仍支付 1 Energy、抽 2 张并将来源牌最后弃置；升级“抽 3 张”仍只是作者表与本地化元数据。
- 公共 `EffectType.ExhaustSelectedHandCard = 5` 只承担选择 Effect；通用语法固定为首项 `ExhaustSelectedHandCard / Attribute.None / Value 1`，其后恰好一个合法 `DrawCards`，3125 的 4013 冻结为 `Attribute.None / Value 2`。规则层与执行层共同拒绝缺失、重复、乱序、夹入战斗 Effect 或非法 Attribute/Value 的绑定，且没有卡牌 ID、显示名或 Ironclad 分支。`BattleSingleOtherHandCardSelectionRules` 同时服务 Burning Pact 与 V2V Vent Heat，UI 继续复用 `SelectedCardIds`、`BattleHandCardSelectionRequest` 与 `HandCardSelectionSession`。
- `BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture` 在首次写入前冻结 owner、起始 Layout、洗牌 RNG 前后状态、最终 Layout、连续 settlements 与一次性提交。权威逻辑顺序为 `EnergySpent → optional selected HandToExhaust → optional reshuffle → DrawPileToHand（至多 2）→ source HandToDiscard`，最终只发布一次 Layout。抽牌投影期间来源牌仍占 Hand：命令开始为 Hand 10 时先消耗另一张、只能抽 1 张，来源最后弃置后 Hand 为 9；跨 owner、Layout/RNG 漂移或重复提交均拒绝且零写入。
- TDD 精确红灯 `91544da77057452bba4004fda382a130` 唯一暴露 `UnsupportedEffectType`。基础事务与 CardZones、唯一来源/Hand10、规则与漂移、非法选择、非法语法、真实选择会话协议和表现链绿灯分别为 `1e10bdc85d3a4970a540378e8e9aa773`、`f161c8b0ffd549bd906dfb7da7715de2`、`a8246f19fdb24f8283430abf81022307`、`814665044dac4aea917a624ea969757f`、`08954580668f4d7588792cbb2898059e`、`1325defc95f644d69fd4f43cf50f289b` 与 `34146393c7f9466ba7f0faf285127094`。
- 正式工作簿 SHA-256 为 Enums `D0984D35BE585D04C9C1E56B62B5C8AEFBB0F9760A38DBACF9477B3A685D0EC3`、Card `C3025BA774D84E24CAD679DEE057AA79F25A41F81AC83798E6263DDE8FAA22DB`、Effect `0B002B0C97820E7BF3F5DEFB54084F53CF94F1F224E77E15A8E8BCB62CC30173`、i18n `A05411C781FE20D3CFA99F0FD4AAD08F68E34F0A80571E425A5C2772E50B4C37`；候选/正式 artifact 对 7217 个单元格规范化样式与渲染一致，Effect C 列仅按内容由 15.625 调整为 21.13。Luban 于 2026-08-13 17:32:12 成功；Card/Effect JSON SHA-256 分别为 `23BCA0295418E949AC3CA752C26F2C23A56FBD569EEA88C784C65B8EC914BAF6` / `D06C67D9AF1B22733340706607AE2D95DD3E7E78FD12AC7D4AEFA79AB077D008`。全项目 168 张为 **86 `Implemented` / 82 `CatalogOnly`**，Ironclad 85 张为 **9/76**，Marine 82 张保持 **76/6**，Effect 共 13 项。
- Localization 首轮因 Unity `AssetDatabase` 尚未看见新生成资源而失败，属于 stale 导入状态诊断，不是产品红灯；强制刷新后 Import 7.401 秒、Validate 6.161 秒、`Sync and Build All` 22.054 秒与 Addressables 13.551 秒均成功。BuildLayout `Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.17.40.23.json`（134622 bytes）证明 `tinyspiregamedata_assets_all_aa609eaff8569429297e832a2721d5a6.bundle`（12085 bytes）由 `AssetBundleProvider` 打入 Card/Effect JSON。Runtime 静态编译 0 error / 6 warning，Editor 0 error / 12 warning；并行静态构建曾出现输出文件争用 `CS2012`，串行重跑全绿。正式行为 `c1c48a5d4738462aa8a150d6e614f577` 为 9/9，目录 `5310c9f189044261922c4cdc2823ef31` 为 22/22，含真实 AB 的聚合 `54a914c2c66647879fe274dcc384b86d` 为 172/172，完整 EditMode `c708030e61834d7dbe3196c6d378f30f` 为 **754/754 passed**，Console 最终 0 error。决策与完整证据见 `CODE_DECISIONS.md` CD-103 和 `06_testing/2026-08-13-sts2-ironclad-burning-pact-runtime.md`。

## 2026-08-13 Marine Game 机枪兵 V2V 排气散热与共享手牌单选协议（已完成 Unity 原生验证）

- V2V 已为 `VentHeat` (3244) 注册可执行基础态：0 Energy、Skill、Self、Hand→DiscardPile。若手中存在另一张可选牌，命令必须精确携带一个其他手牌实例；被选牌先 Hand→ExhaustPile，随后只按上限记录实际 `Energy +1`，最后来源牌 Hand→DiscardPile。若来源是唯一手牌，则直接弃置且不获得能量；能量已满仍会消耗所选牌，但不会伪造 `EnergyGained`。
- 权威结算顺序冻结为 `EnergySpent(0) → selected HandToExhaust → EnergyGained(仅实际增加时) → source HandToDiscard`。`BattlePreparedHandCardSelectionResolution` 在首次写入前联合冻结两张牌的归宿并只发布一次 `CardZones.Layout`；选择非法、重复、跨 owner，或 Layout / Turn / Queue 快照漂移时保持 Turn、资源、卡区与 settlement 零写入。已提交的非法命令仍由 Queue 发布 typed failure lifecycle；`BattleCommandQueue.Submit` 继续是唯一共享写入入口。
- 新增的共享选择 seam 由 `PlayCardCommand.SelectedCardIds`、`BattleHandCardSelectionRequest`、`BattlePreparedHandCardSelectionResolution` 和 UI 局部 `HandCardSelectionSession` 组成。Hand UI 冻结 Layout / Turn / Queue：候选牌左键确认，来源牌左键或任意卡右键取消；选择中禁止拖拽、区分候选与非候选视觉，并在事实漂移、禁用或销毁时取消。双 transient 不创建伪 prelude，按 selected→Exhaust、source→Discard 的既定步骤依次清理。
- TDD 历史保留了 Program 44 未支持、命令选择参数、卡区深事务、规则请求、纯选择会话、容器交互、视觉、生命周期与双 transient 的逐片红绿。历史中未保留完整任务号的记录只以明确前缀记载；关键最终绿灯为确认提交 `f1e74a5829d746f08fa456b737b04caf`、交互转发 `d6a01d920498443ab32518797b770fe0`、视觉 `e46df1b0bda846b09b67dabca1433770`、容器取消 `a92b6214657848d98776cae71fe8183f`、生命周期 `2902d4a46c9b4a1fbf3b54f914d8ec42` 与双 transient `78bcd00d468c4b19952bb935ba708a77`；完整红绿与 fixture/oracle 修正见测试页。
- 正式作者表只把 Q134（3244）从 `CatalogOnly` 翻为 `Implemented`，`battle.card.xlsx` SHA-256 为 `B3BA678FBC0C021F49C3F9FEDE4190099960EE109FFC302D96C77F29D54F4A6D`；i18n 只修改 B/C404-405 四个文本单元格，SHA-256 为 `8833E99F546B2C1195C4F0317A1B9208535ED083743F1ABF183874EFFFD23D77`。Luban 于 2026-08-13 14:55:40 成功；生成 JSON SHA-256 为 `5988DA20801C8BF724EF0E471466A0A746A5E732DE3450BD7680F00A735F2615`，全项目 168 张为 85/83，Marine 82 为 **76 张 `Implemented` / 6 张 `CatalogOnly`**，V1 为 60/4、V2 为 16/2。
- Localization Import / Validate 日志通过；`Sync and Build All` 成功，本地 Addressables 为 15.85 秒，BuildLayout 为 `Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.14.59.24.json`（134615 bytes）。Runtime 静态编译 0 error / 6 warning，Editor 0 error / 12 warning。行为任务 `86186ac62acd476188b1c67c75443582` 为 15/15，目录任务 `6f75f8955c944aae8290934cefe4dc45` 为 38/38，含真实 AB 的正式聚合 `55d24ae6959f48fbbfc96238b9c1ce16` 为 **306/306 passed**，完整 EditMode `0bf8b7bf3ffc40c986a55917993894f4` 为 **744/744 passed**。
- 本切片只提供可复用的普通手牌单选 seam，未来可供 Ironclad `Burning Pact` 适配；本轮没有实现战士卡。3244 升级能量 +2 仍只是元数据；默认 Deck、奖励、Run、多人、Scene、Prefab 与其他剩余目录卡也未实现。决策与验证见 `CODE_DECISIONS.md` CD-102 和 `06_testing/2026-08-13-machine-gunner-v2v-vent-heat-runtime.md`。

## 2026-08-13 Marine Game 机枪兵 V2U 不解释12连两波换弹射击（已完成 Unity 原生验证）

- V2U 已为 `TwelveHits` (3257) 注册可执行基础态：3 Energy、Rare、Attack、自动锁定命令开始时最近敌人、Hand→DiscardPile。普通支付时第一波消耗当前 Ammo 最多射击 6 次，随后无条件补满 Ammo，再在第二波消耗最多 6 发；0 Ammo 也可出牌，此时第一波为 0 次、仍换弹并执行第二波。每段基础伤害为 5，目标死亡后停止剩余伤害且不重定向，但已冻结的换弹与第二波资源支付仍提交。
- 机枪兵私有 `MachineGunnerReloadedVolleyResolver` 纯冻结首/次波 effect shot、actual Ammo、波间补满前后值、全卡唯一 Stim 段、Guerrilla nominal Ammo 与最终 Ammo；它不读写战斗对象。免费 Attack 仍执行两波与换弹，实际 Energy/Ammo 为 0，名义 Ammo 按两波上限计算，Stim 激活时为 13；成功归宿后消费 V2T 授权，费用、目标、Shackle 或快照失败保持授权与全部事实零写入。逐 hit 伤害继续复用既有命中后链，每个来源段按原顺序触发 IncendiaryAmmo，再触发 PortableHelper。
- 精确 TDD 红灯 `5206873b56c84c27a462dd27edcaf375` 为 1/1 failed，锁定 Program 57 尚不受支持。六项逐片绿灯依次为 `ea81efa9f48c408da2e3f51573805b23`、`b0b40621fd3740798e9bd5dd91277507`、`968e6f6870d84ab58a6d61caf9aae44d`、`aa2f33f94df14b1a8913d299c325445c`、`b7eb8adef3de4e9f8b490093171de1c0`、`4605fc4c790940f5a5a2eb4169ac1d2e`，各为 1/1 passed。slice 4 首轮 `e2caeac9cbe1440598c3a2075de14075` 只暴露测试场景供能前提错误；修正测试后生产未改。
- 正式作者表只把 Q147（3257）从 `CatalogOnly` 翻为 `Implemented`，`battle.card.xlsx` SHA-256 为 `7131597FD5F3D948921F54926C0205E24E31F747D7C9B1206B78902AE6BEF818`。Luban 于 2026-08-13 03:00:27 成功；生成 JSON SHA-256 为 `28324422913241FC627F5C3A0BCF715332E4F2B3DCDFA94E4B6E4FF3ED7A6306`，全项目 168 张，Marine 82 为 **75 张 `Implemented` / 7 张 `CatalogOnly`**，V1 为 59/5、V2 为 16/2；3257 为 status 0、Program 57、Attack/Rare、3E Fixed、upgraded cost 2、Enemy、DiscardPile、空 bindings 与非 Innate。
- Localization import 7.350 秒、显式 validate 3.124 秒；`Sync and Build All` 端到端 18.482 秒，Addressables 12.173 秒，BuildLayout 为 `Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.03.02.34.json`。Runtime 静态编译 0 error / 6 warning，Editor 0 error / 12 warning。
- Starter `9fec961053ae45ea869ad9aa211c13fa` 为 148/148，正式目录快照 `4f7cbbf8343c472f9e51e2f1862c2a3c` 为 37/37，正式聚合 `a7041271f4f343588b30e74edfd5b741` 为 **220/220 passed** 并包含真实 Addressables AB 加载，完整 EditMode `08565f2677824aff8e45043cdd8dc1eb` 为 **728/728 passed**。
- 本切片不实现升级实例、升级 2 Energy / 6 伤、默认 Deck、奖励、Run、UI、多人、Scene/Prefab、自动免费攻击链或剩余 7 张目录卡；决策与验证见 `CODE_DECISIONS.md` CD-101 和 `06_testing/2026-08-13-machine-gunner-v2u-twelve-hits-runtime.md`。

## 2026-08-13 Marine Game 机枪兵 V2T 战术推进二元免攻与共享费用冻结（已完成 Unity 原生验证）

- V2T 已为 `TacticalAdvance` (3234) 注册可执行基础态：2 Energy、Skill、Self、Hand→DiscardPile；成功时先获得 10 Block，再刷新一份“下一张成功 Attack 免费”的二元授权。连续施放不叠加次数；授权跨回合保留，Skill、Shackle、目标/费用/计划或卡区失败均不消费，第一张成功 Attack（含致死）完成归宿后才消费，下一张恢复正常费用。Shackle 继续在费用解算前由上游 Attack 门禁拒绝。
- 授权由职业运行时独立 `bool + revision` 持有，不加入 `MachineGunnerCombatantStatus`；私有状态身份继续是 16 种，3277 / 3278 的状态种类计数不变。当前 `README web.md`、正式表与 i18n 一致采用基础 10 Block / 升级 14 Block，历史 `HANDOFF.md` 的 12/16 已被当前来源覆盖；升级 14 仍只是元数据。
- 新增共享 `BattleCardCostResolver`，为 Fixed / X 冻结实际支付、效果值与触发器名义值；通用 `BattleCardPlayRules` + `BattleTurnController` 的普通 Fixed 与机枪兵运行时是两个真实适配器，既有通用 X 未扩展。机枪兵在同一准备成本中分离 Energy、Ammo actual / effect / nominal 与 Stim：Waived 只把实际支付归零；Fixed / UpToLimit 的 Stim 段仍生效并计入 Guerrilla 名义耗弹，AllAvailable 保留既有免费 Stim 段；ComboElbow 分类不变。
- 正式作者表只把 Q124（3234）从 `CatalogOnly` 翻为 `Implemented`，`battle.card.xlsx` SHA-256 为 `55D43141149D7A86D7957B1C43ED9303B9E9D091094E0CFAF2CF39FE2F73C569`。Luban 于 2026-08-13 01:44:15 成功生成全项目 168 个 Card JSON；Marine 82 模板为 **74 张 `Implemented` / 8 张 `CatalogOnly`**，V1 为 58/6、V2 为 16/2；3234 精确为 status 0、Program 34、空 bindings 与非 Innate。Localization import / validate 均通过。
- `Sync and Build All` 端到端 16.852 秒，Addressables 14.762 秒，BuildLayout 为 `Library/com.unity.addressables/BuildReports/buildlayout_2026.08.13.01.45.29.json`；Runtime 静态编译 0 error / 6 warning，Editor 0 error / 12 warning。TDD 红灯 `5a0823dd6a2241e0818512b8855877a6` 为 1/1 failed 并精确暴露不支持 Program 34；最终 V2T `cddab7f295844f71999568465dc1f85e` 为 6/6，致死/溢出补强 `da3a7e7e7c6540eca8400e99ba5c0ca4` 为 1/1，Starter `e1dcce3dfc6c4078b769a921a98145b4` 为 142/142。
- 正式目录快照 `d64c71abd6c2436a8f820efc976a9196` 为 36/36，正式聚合 `e4e6f701845547149384c1f6e792269e` 为 **213/213 passed** 并包含真实 Addressables AB 加载，完整 EditMode `fe108672fde44832a7fb4819116136c1` 为 **721/721 passed**。最终双轴 production / spec review 均为 0 blocker；Standards 指出的冗余 `CardTemplateId` 已删除。默认 Deck、奖励、Run、UI 专属提示、多人、Scene/Prefab、升级实例、自动免费攻击链与战士免攻策略均未实现；决策与验证见 `CODE_DECISIONS.md` CD-100 和 `06_testing/2026-08-13-machine-gunner-v2t-tactical-advance-runtime.md`。

## 2026-08-13 STS2 Ironclad 首批四张基础卡与通用 DrawCards（已完成 Unity 原生验证）

- 冻结 v0.107.1 Ironclad 目录已开放首批四张基础态：Pommel Strike（3113）为 1 Energy / Enemy / `Damage 9 → Draw 1 → Discard`，Shrug It Off（3115）为 1 Energy / Self / `Block 8 → Draw 1 → Discard`，Twin Strike（3120）为 1 Energy / Enemy / `Damage 5` 独立结算两次，Bludgeon（3123）为 3 Energy / Enemy / `Damage 32`。Twin 的有序绑定键固定为 `damage` / `damageRepeat`。
- 公共数据层新增 Effect 4007～4011 与 `EffectType.DrawCards = 4`；普通卡 Effect 组合按绑定原序冻结 Draw 前战斗 Effect、至多一次 Draw 与 Draw 后战斗 Effect。全部子计划在首次权威写入前联合预构建和校验，Draw 后 Effect 延续 Draw 前完整战斗投影，多次引用同一伤害 Effect 仍保留独立逻辑段。
- CardZones 的 `PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 独占 Hand 10 上限、旧 DiscardPile 重洗、洗牌随机前后状态、连续 settlement 与最终布局。Draw 前致死仍执行已冻结抽牌；满 10 手牌时抽 0 且不推进随机，当前卡随后正常归宿；能量、目标、绑定或快照失败继续保持战斗事实、资源、卡区、随机与表现结果零写入。
- 正式作者表 SHA-256 为 Card `54DA52D0C80885A2D55AEC8E260207E2D4E27AC8251304BF0710DB180EBC4EBB`、Effect `B616F993F5373AFF2DDD764E9C431A2C13F66CD3C5B2F39595B4A813FB7863BC`、Enums `B9F8DD24C77EE64FA36C6DC7FEA5C0D83229011F45463A99ABAE30B3A7870B26`、i18n `7E91C7F46AEBBBF20188690EC49B1B5C3F6C84C2EF3A9531D22D42E3E23644F8`。Luban 于 00:42:37 成功；Ironclad 85 张为 **8 `Implemented` / 77 `CatalogOnly`**。
- Localization import / validate 通过。诊断保留两轮事实：先发现读取 stale config；刷新后才暴露参数规范不允许下划线，最终统一 `damageRepeat` 且没有放宽 validator。`Sync and Build All` 成功，Addressables 13.595 秒，BuildLayout 为 `buildlayout_2026.08.13.00.44.31.json`；Runtime 静态编译 0 error / 6 warning，Editor 0 error / 12 warning。
- TDD 红灯 `2f8fa9d405e94893b9a0cc600faff777` 为 2 passed / 2 `UnsupportedEffectType`；后续单元任务前缀 `8e1a…`、`839889…`、`0f8efd…` 分别为 7/7、3/3、42/42。正式 smoke `49d34997a550459f98b80d6ee88deec0` 为 **20/20 passed**（1.0011866 秒），正式聚合 `c3281b04224845eaa4138ea5024904a0` 为 **67/67 passed**（26.3892428 秒），完整 EditMode `0856b63a9ad44ea08a8a37d0df803571` 为 **713/713 passed**（22.4028613 秒），均无 failed / skipped。
- 升级实例与升级数值、其余 77 张目录卡、默认 Deck、奖励、Run、UI 与多人均未实现；本轮也不宣称 I5 的“每步独立目标”整体完成。需求摘要、决策与验证分别见 `01_requirements/2026-08-13-sts2-v01071-ironclad-first-four-runtime-digest.md`、`CODE_DECISIONS.md` CD-099 与 `06_testing/2026-08-13-sts2-ironclad-first-four-effect-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2S 先发制人按来源起始状态种类抽牌（已完成 Unity 原生验证）

- V2S 已为 `PreemptiveStrike` (3277) 注册可执行基础态：0 Energy、1 Ammo、Uncommon、Attack、显式 Enemy、`Tags.None`、Hand→DiscardPile。成功时造成基础 8 点普通 Attack；Damage 与既有 post-hit 链完成后，按命令开始时来源的活跃状态种类冻结值执行普通抽牌，目标致死仍抽，最后把 3277 移入 DiscardPile。升级 12 点伤害仍只是作者表元数据。
- 冻结数量 `N` 的项目口径为：来源 Strength 非零、Vulnerable 大于零，以及 16 种 `MachineGunnerCombatantStatus` 的每一种正层数各计一种，同种多层不重复；Power、Stim、scheduled effect、Block 与资源不计。Shackle 身份保留在精确集合中，但既有上游攻击门禁会在首次写入前拒绝带 Shackle 的来源施放 3277，测试锁定完整零写入；其余 15 种私有状态已逐项覆盖。这些集合、时点与排除项是受控实现决定，不伪称为来源逐字规则。
- 普通抽牌复用 `BattleCardZonesData.PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 深事务，在首次战斗写入前冻结数量、Hand 上限、DrawPile / DiscardPile、重洗随机状态、最终布局与移动 settlement；Damage / post-hit 后只提交该旧计划。目标、资源、Shackle 或计划校验失败继续保持 Energy、Ammo、伤害、状态、随机流、卡区与表现结果零写入，`BattleCommandQueue.Submit` 仍是唯一共享写入入口。
- 正式作者表仅将 Q167（3277）的 `implementation_status` 翻为 `Implemented`，U167 `is_innate=false`，`battle.card.xlsx` SHA-256 为 `6C9120A317622F103F9A0DDEEEBB994B28F88230B679BA7E0B1D28201F8E2648`。Luban 于 2026-08-12 23:26:12 成功生成；全项目 Card JSON 168 个，Marine 82 个模板为 **73 张 `Implemented` / 9 张 `CatalogOnly`**，V1 为 57/7、V2 扩展为 16/2；3277 精确为 status 0、Program 77、DiscardPile 与非 Innate。
- Localization import 与显式 validate 成功；Runtime 静态编译为 0 error / 6 warning，Editor 为 0 error / 12 warning。`Sync and Build All`、本地 Addressables 与 BuildLayout 均成功，Addressables 构建耗时 13.966 秒。正式聚合首轮 `629f7e51d61d4bb49a6bdb6232239ca6` 为 213/214，唯一红项是旧 Bully 操作名称 oracle，生产未改；最终任务 `ea21d256c5b840629a270eed7a10bd90` 为 **214/214 passed**（16.8155368 秒），完整 EditMode `1484317edb124dcdb6cb0f0862a8758a` 为 **702/702 passed**（25.5067985 秒）。
- TDD 首轮 `634966a39d1a434886289cca3382e8f9` 为 3/5，两项红色分别来自重洗记录顺序 oracle 与忽略 Shackle 上游攻击门禁的测试前提；均只修测试。最终 V2S `73d5e79a25164857b48bb5b1fba5d92a` 为 **5/5 passed**（0.7261073 秒），production 审查无 blocker。默认 Deck、奖励、Run、升级实例、升级 12 伤、UI 与多人均未实现；验证记录见 `06_testing/2026-08-12-machine-gunner-v2s-preemptive-strike-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2R 霸凌按目标起始状态种类抽牌（已完成 Unity 原生验证）

- V2R 已为 `Bully` (3278) 注册可执行基础态：0 Energy、Uncommon、Attack、显式 Enemy、Hand→DiscardPile。成功结算保留零费用事实，随后造成基础 6 点普通 Attack；伤害及既有非射击命中后链完成后，按命令开始时目标的活跃状态种类冻结值执行普通抽牌，最后把 3278 移入 DiscardPile。升级 9 点伤害仍只是作者表元数据。
- 来源只明确“目标每有一种状态抽 1 张”，没有规定计数时点与状态集合；本切片把受控实现口径冻结为：通用 Strength 非零、Vulnerable 大于零，以及每一种 `MachineGunnerCombatantStatus` 正层数各计一种，同种状态不论层数只计一次。HP、Block、资源、PowerPile 卡实例、Stim 与延迟实例不计；伤害消费状态、命中后新增 Oil 或目标致死都不反向改变已冻结抽牌数。这些边界是项目实现决定，不伪称为来源逐字规则。
- 普通抽牌复用 `BattleCardZonesData.PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 深事务：在首次战斗写入前冻结手牌上限、DrawPile / DiscardPile、洗牌随机状态、最终布局与移动 settlement。0 种状态合法抽 0；Hand 已满 10 时不推进洗牌随机，当前牌最后离手后 Hand 为 9；目标错误、计划漂移或其他失败继续保持 Energy、伤害、状态、随机流、卡区与表现结果零写入。`BattleCommandQueue.Submit` 仍是唯一共享写入入口。
- 正式作者表仅将 Q168（3278）的 `implementation_status` 翻为 `Implemented`，U168 `is_innate=false`，`battle.card.xlsx` SHA-256 为 `878812D99F68C8F9B9A7BC620E2794180F6E8A3F21B5252B16A12BDB70915499`。Luban 于 2026-08-12 22:48:16 成功生成；全项目 Card JSON 168 个，Marine 82 个模板为 **72 张 `Implemented` / 10 张 `CatalogOnly`**，V1 为 57/7、V2 扩展为 15/3；3278 精确为 status 0、Program 78、DiscardPile 与非 Innate。
- Localization import 与显式 validate 成功；Runtime 静态编译为 0 error / 6 warning，Editor 为 0 error / 12 warning。`Sync and Build All` 与 BuildLayout 写入成功，Addressables 构建耗时 16.521 秒。正式聚合定向任务 `9d67623c6fcc445ebb658b8eea6709c0` 为 **209/209 passed**（0 failed/skipped，30.7641594 秒），包含 CardIllustration 真实 Addressables AssetBundle 加载；完整 EditMode 任务 `598d7b50593e463db922f1ad88472d99` 为 **697/697 passed**（0 failed/skipped，19.7357848 秒）。
- TDD 任务 `36ffd31603d14de38de4912faf8fb4c1` 枚举 3 项、2 绿 1 红，唯一失败来自测试误把 Damage 当作 Order 0、遗漏生产一直保留的 `EnergySpent(0)`；本次只修测试预期，生产实现未改。补强任务 `8d099f89842e4024925465adf9b3e370` 为 **6/6 passed**（0.4367945 秒），非表格聚合 `94110e65e6b649ea99b901ad49ab4bdd` 为 **150/150 passed**（1.4560424 秒）。默认 Deck、奖励、Run、升级实例、升级 9 伤、多人、Scene 与 Prefab 均未实现；验证记录见 `06_testing/2026-08-12-machine-gunner-v2r-bully-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2Q 固定机枪与临时卡生产（已完成 Unity 原生验证）

- V2Q 已冻结 `FixedMachinegun` (3261) 的基础态运行口径：2 Energy、Rare、Skill、Self、Hand→ExhaustPile。成功时先获得 10 Block，再让来源卡进入 ExhaustPile；随后把当时剩余 Hand 按原顺序全部移入 DiscardPile，并为每张被弃的旧手牌创建一张新的 `MachinegunBurst` (3263) 到 Hand。剩余 Hand 为空时创建 0 张；升级 15 Block 仍只是作者表元数据。
- 该复合变化由 `BattleCardZonesData` 的单一 Prepare / Validate / Commit 深计划拥有：准备阶段冻结原布局、卡实例分配状态、来源归宿、剩余手牌原序、临时实例及最终布局；成功提交只发布一次最终 `Layout`。创建使用独立 `CardCreated` settlement，不伪装成 DrawPile→Hand，也不把临时实例写回 Deck、奖励或 Run。
- 表现层按 settlement 身份区分既有 Hand→Discard、来源 `HandToExhaust` 与临时实例 `CreatedToHand`；动态 3263 模板依赖从职业程序 registry 传递到 `Session.AvailableCardTemplateIds`，再由 Hand 异步预载，因此不要求 3263 预先存在于本局 Deck 才能显示新实例。
- 正式作者表只把 3261 的 `implementation_status` 翻为 `Implemented`，`battle.card.xlsx` SHA-256 为 `02F549502D14214C98B4BA97212962B05E58A9B768EF1D7E4CAD441E1DCD6FB7`，`is_innate` 保持 false。Luban 于 22:00:11 成功生成：全项目 Card JSON 168 个、Marine 82 个模板为 **71 张 `Implemented` / 11 张 `CatalogOnly`**，V1 为 57/7、V2 扩展为 14/4；3261 精确保持 status 0、Program 61、Exhaust 与非 Innate。
- Runtime 静态编译为 0 error / 6 warning，Editor 静态编译为 0 error / 12 warning；Localization import 与显式 validate 成功。`Sync and Build All` 完成且 Addressables 构建耗时 13.42 秒；随后通过 force scripts 完成域重载。正式聚合定向任务 `ba19d1744f084167927568f5572f91e6` 为 **262/262 passed**（0 failed/skipped，30.1698095 秒），覆盖目录快照、CardIllustration、Session、Hand、Queue 与 UI；完整 EditMode 任务 `dc6a1453b602487c8bfbbe7e42c3968d` 为 **690/690 passed**（0 failed/skipped，20.8279366 秒）。
- TDD 红测保留为诊断证据：任务前缀 `404d20…` 暴露多张 Hand→Discard 使旧 prelude 抛错，任务前缀 `2045cc…` 锁定 `CardCreated` 结果 guard；修复后核心任务前缀 `d6db34…` 为 12/12、非表格定向任务前缀 `f415877…` 为 195/195。最终审查另发现“Deck 不含 3263 时动态插画未预载”的 blocker；改为 registry→`Session.AvailableCardTemplateIds`→Hand async preload 后，动态精确任务前缀 `6bf4…` 为 2/2，最终 262/262 与 690/690 也覆盖该链路。升级实例、升级 15 Block、默认 Deck、奖励排除、Run、多人、Scene 与 Prefab 均未实现。验证记录见 `06_testing/2026-08-12-machine-gunner-v2q-fixed-machinegun-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2P 机枪扫射基础态（已完成 Unity 原生验证）

- V2P 已为 Hero 1002 注册临时卡 `MachinegunBurst` (3263) 的可执行基础态：0 Energy、Attack、RandomEnemy、Hand→ExhaustPile、无升级。成功时进行两段独立的基础 5 点普通 Attack；每段开始都从当时仍存活的敌人中重新随机选择，因此首段击杀会改变第二段候选。
- 3263 的实际 Ammo 成本固定为 0，出牌不会改变 Ammo，也不生成 `AmmoSpent`；只有游击战术读取名义弹耗覆盖值 2，故已有两层 Guerrilla 会在伤害完成后、当前卡离手前获得 4 Block。来源没有给 3263 声明 Shoot 标签，本项目冻结 `Tags.None`：不从名称推断射击，不使用 Stim、IncendiaryAmmo、FirePower 或 PortableHelper；同时通过单一分类属性显式退出 KungfuMech、AgedOil 与 `NonShootAttackRecent`，但伤害仍走普通 Attack 公式与生命周期。
- 作者表只将 3263 的 `implementation_status` 翻为 `Implemented`，正式 `battle.card.xlsx` SHA-256 为 `B65D97253A43B2FF8575BCEE6F230B651EFD36FE84A10B7ACBFC0BCC62A0AB29`。Luban、Excel 本地化导入/校验与 `Sync and Build All` 均通过，本地 Addressables 构建耗时 11.757 秒；当前 82 模板为 **70 张 `Implemented` / 12 张 `CatalogOnly`**，V1 为 56/8，V2 扩展为 14/4。
- Unity MCP 最终聚合定向任务 `0f60a2e799904069ab68ae6f13a91953` 为 **154/154 passed**（0 failed/skipped，2.698029 秒）；CardArt 域重载探针 `f87e7034664a4126bb0b32c2888751e9` 为 **1/1 passed**（10.3685051 秒）；完整 EditMode 任务 `a078688b69bd4f198bb736c6285ab5e7` 为 **678/678 passed**（0 failed/skipped，47.3413725 秒）。同一 Editor 重建 Addressables 后、域重载前的完整任务 `dd80e8747e394c7387f8c57497c88f7d` 与精确单测 `b4f20bc3f8364329a374df26c76925b6` 曾分别在同一 CardArt 加载处等待 180 秒；保持 bundle、timeout 与缓存不变并完成域重载后，探针及全量均通过，诊断为 Editor 内陈旧 Addressables 静态状态，不作为生产缺陷或绿色验收证据。
- 3263 的直接程序与失败零写路径已验证，但来源声明它只能由 `FixedMachinegun` (3261) 创建，而 3261 仍为 `CatalogOnly` 且没有生产临时卡生成入口。因此本切片不把 3263 记为正常产品流程可获得，也没有实现奖励排除、默认 Deck、Run、升级、UI、多人、Scene 或 Prefab。验证记录见 `06_testing/2026-08-12-machine-gunner-v2p-machinegun-burst-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2O 隐秘行动与固有起手（已完成 Unity 原生验证）

- V2O 已为 Hero 1002 完整接入 `StealthAction` (3275) 基础态，并把“固有”从卡名/程序特判提升为 `Card` 的强类型 `is_innate` / `IsInnate` 配置事实。作者表只把 3275 标记为固有和 `Implemented`，其余卡保持 `is_innate = false`；Luban 已生成对应 C# 与 JSON。当前目录为 **69 张 `Implemented` / 13 张 `CatalogOnly`**，V1 为 55/9，V2 扩展为 14/4。
- `BattleTurnController` 在 `StartBattle` 的任何状态/资源/布局写入前，按生成配置的 `IsInnate` 为全部存活玩家收集固有实例并冻结起手计划，不按 ProgramId 或卡牌 ID 特判；CardZones 拥有具体选择与布局提交。固有实例按已经洗牌后的 DrawPile 抽取顺序进入 Hand：固有数不超过 5 时以普通牌补到默认起手 5；6～10 张时全部固有入手且不补普通牌；超过 Hand 上限 10 时返回 `InvalidOpeningHandConfiguration` 且零写入。成功只发布一次最终 `Layout`，不推进洗牌随机，移动 settlement 保持连续顺序。
- 3275 的成功出牌顺序冻结为 `EnergySpent(1) → Invisible +1 → 普通 DrawCards(1) → 当前牌 Hand→DiscardPile`。抽牌沿用普通出牌时序，计算时 3275 仍在 Hand；因此满 10 手牌时本次抽牌为 0，随后弃置本卡后 Hand 为 9。显式目标或能量失败继续保持资源、状态、卡区与随机流零写入。
- 正式 `battle.card.xlsx` SHA-256 为 `172FEB0A50DA4F3DC6A580F83C73C97266B6F70A72D1FA01CCDD3D15B1B9F6C9`，`__beans__.xlsx` 为 `A899AC4D58890C5E2B5D75C9AF09A9B0769078F5218FE11889AD3F8688C178FB`；Luban、Excel 本地化导入/校验、`Sync and Build All` 与本地 Addressables 均通过，Addressables 耗时 18.363 秒。正式目录快照任务 `8acfa22da51c4f2fb757bbe102fb945c` 为 21/21，最终聚合定向任务 `982a4f4c4af24ba78e678bf0e66f2ce1` 为 **237/237 passed**（0 failed/skipped，4.4515056 秒），完整 EditMode 任务 `91d060c915ff4dfea42608b7c22669ab` 为 **673/673 passed**（0 failed/skipped，123.9614109 秒）。
- 本切片不修改默认 Deck 内容、奖励、Run、UI、多人、Scene 或 Prefab；升级的 Invisible +2 / Draw 2 仍只是作者表元数据。验证记录见 `06_testing/2026-08-12-machine-gunner-v2o-stealth-action-innate-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2N 极限过载基础态（已完成 Unity 原生验证）

- Hero 1002 的同一职业运行时现已注册 `LimitOverload` (3260) 基础态：0 Energy、Rare、Skill、Self、Hand→DiscardPile。成功时先记录 0 费的 `BattleEnergySpentSettlement`，再即时获得 1 Energy（受 EnergyMaximum 裁剪，满能量不伪造获能记录），当前卡逻辑离手后抽至 10 张，最后累计 `NextRoundEnergyGainPenalty +3`。
- `BattleCardZonesData` 新增离手抽至上限的 Prepare / Validate / Commit 深事务 seam：准备阶段基于当前卡成功归宿后的投影 Hand 冻结缺口、原布局、洗牌随机状态、最终布局和全部 settlement，且保持零写入；校验拒绝跨聚合、布局/随机漂移或重复提交；提交不再随机，只发布一次最终 `Layout`，不会暴露 11 张手牌的瞬时状态。
- 联合顺序冻结为 `EnergySpent(0) → 可选 EnergyGained(1) → 当前牌 Hand→DiscardPile → 旧 DiscardPile→DrawPile/重洗/抽牌 → Penalty +3`。同次重洗只包含解算前已在弃牌堆的牌，3260 本身最后才进入弃牌堆，不会在同次解算中抽回。下一回合继续复用 V2J 的 `max(0, baseGain + bonus - penalty)`，再按 EnergyMaximum 裁剪并清除一次性状态。
- 作者表仅将 Q150（3260）的 `implementation_status` 翻为 `Implemented`。Luban 后机枪兵 82 模板为 **68 张 `Implemented` / 14 张 `CatalogOnly`**，其中 V1 为 55/9，V2 扩展为 13/5；升级“+2 能量”仍只是作者表元数据。
- Luban、Excel 本地化导入与校验、`Sync and Build All` 及本地 Addressables 构建均成功（15.828 秒）。Unity MCP 正式定向任务 `feda36c5daef4fffab34065ba5988686` 为 **169/169 passed**（0 failed/skipped，2.2836982 秒）；完整 EditMode 任务 `a84b5bb4f7dd4ca1b9791c81bb930973` 为 **659/659 passed**（0 failed/skipped，282.0044831 秒）。CardArt 与 Character Prefab 的 Addressables 冷加载较慢，但均在完整任务中通过。
- 本切片未实现升级 `CardInstance`，未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab；也没有把 3260 分类为 Attack/Shoot，不消耗 Ammo、Stim，不触发 IncendiaryAmmo 或 PortableHelper。其余 14 张 `CatalogOnly` 继续由目录门禁拒绝。详见 `06_testing/2026-08-12-machine-gunner-v2n-limit-overload-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2M 天空之怒基础态（已完成 Unity 原生验证）

- Hero 1002 的同一职业运行时现已注册 `SkyWrath` (3266) 基础态：1 Energy、Rare、Power、Self，成功后进入 PowerPile 并增加一层整场持续、可叠加的天空之怒 Power；卡本身不造成即时伤害，也不推进随机流。
- 受限触发入口只位于当前 README 声明的四类原始 Support 逻辑段末尾：女妖打击每 hit、火力支援每 hit、燃烧轰炸每 wave、三连击延迟 Support 一次。燃烧轰炸必须先完成该波全部目标的 `Damage → 存活后 Burn → Oil`。旧 HANDOFF 把钢针纳入触发的描述不覆盖当前 README；Needle Delayed、Bomb、回合末 Burn、即时 Attack/Shoot、PortableHelper 与天空之怒自身均不触发。
- 每一层在开始时重新读取投影中的存活敌人并调用一次随机流，候选只有 1 名时也推进 `NextInt(1)`；先对随机主目标造成基础 8 点 Support，再按该层开始时快照的 Encounter 顺序对其余目标各造成基础 4 点 Support。下一层看到前层致死后的新候选；没有存活敌人时停止且不推进随机流。天空之怒的 8/4 先受当前 Bombard 层数按既有 half-up 规则缩放，再进入目标 Smoke、Vulnerable 与 ArmorBreak 的 Support 管线；Bombard 4 层时为 11/6。
- 作者表仅将 Q156（3266）的 `implementation_status` 翻为 `Implemented`。Luban 后机枪兵 82 模板为 **67 张 `Implemented` / 15 张 `CatalogOnly`**，其中 V1 为 54/10，V2 扩展为 13/5；升级列仍只是元数据。
- Luban、Excel 本地化导入、`Sync and Build All` 与本地 Addressables 构建均成功（12.956 秒）。Unity MCP 翻表前运行时任务 `eefded85c7aa4a099d3b16ee4577e704` 为 **117/117 passed**；正式定向任务 `3a279411d63749abaf8eca64ec4236cc` 为 **139/139 passed**；完整 EditMode 任务 `a46a25a9da924131965130d6e2b07b8b` 为 **650/650 passed**（0 failed/skipped，174.2163423 秒）。CardArt 与 Character Prefab 的冷加载较慢，但均在完整任务中通过。
- 开发中两轮红测均是测试前提错误：首版分层场景的 `2 × SkyWrath + TripleStrike` 总需 6 Energy，超过 fixture 的 5 Energy 上限，后改为总计 4 Energy 的 Banshee 场景；随后 oracle 把 raw random state 直接作为构造 seed，后改为与生产一致的初始化后赋 State。两次都只修测试，生产实现未改变。本切片未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab；其余 15 张 `CatalogOnly` 继续由目录门禁拒绝。详见 `06_testing/2026-08-12-machine-gunner-v2m-sky-wrath-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2L 狂轰滥炸基础态（组合验收完成，完整任务保留冷加载超时边界）

- Hero 1002 的同一职业运行时现已注册 `Bombard` (3265) 基础态：1 Energy、Power、Self，成功后进入 PowerPile 并增加 4 层整场持续、可线性叠加的狂轰滥炸 Power。已创建的延迟效果不快照层数；女妖打击、火力支援、燃烧轰炸与三连击延迟 Support 在每次实际触发时读取当前 Power 层数。
- 每层把声明的支援载荷提高 10%，正值使用 half-up：`floor((baseValue × (100 + 10 × stacks) + 50) / 100)`。这是来源没有规定小数处理时、经用户授权“脑补”后冻结的决定。缩放后的伤害再进入既有 Support 的目标 Smoke、Vulnerable 与 ArmorBreak 管线；燃烧轰炸还分别缩放 Damage、Burn、Oil，并保持 `Damage → 存活后 Burn → Oil`。
- 该入口只允许上述四种 scheduled Support。GuidedNuke / FiveHundredPounder 的 Bomb、NeedleStorm 的 Delayed、回合末 Burn、即时攻击、便携帮手及其他非声明来源均不放大；命中数、波次数、倒计时、目标选择与既有生命周期 settlement 不变，也没有建立全局伤害事件。
- 作者表仅将 Q155（3265）的 `implementation_status` 翻为 `Implemented`。Luban 后机枪兵 82 模板为 **66 张 `Implemented` / 16 张 `CatalogOnly`**，其中 V1 为 54/10，V2 扩展为 12/6；升级列仍只是元数据。
- Luban、Excel 本地化导入、`Sync and Build All` 与本地 Addressables 构建均成功（12.963 秒）。Unity MCP 定向任务 `9c21aa7c79b94f1980988945d35636dd` 为 **134/134 passed**（1.4521749 秒）；精确重跑 `CardArtLogicalAddresses_LoadSprites` 的任务 `da1d1e3969014e81b06cb57a2392de13` 为 **1/1 passed**（106.8572486 秒）。
- 两次完整任务 `828d66e749a54e66813b3e5d492d4d80`、`492466a3ac7240c29bb227a60945a3c0` 均完成 645 项枚举，但唯一非绿项都是上述素材真实加载用例的 180 秒 timeout；整类 `CardIllustrationConfigurationTests` 任务 `2f3a0ddf6a254a5ab7d8eff8ff5116d5` 完成 5 项枚举时也只保留同一 timeout。V2L 因而按“卡牌定向全绿 + 精确真实加载全绿”的组合门禁验收；不把当前证据写成完整 EditMode 单任务全绿。
- 开发中曾修正一处测试前提：预置 Vulnerable 会在触发前的敌方行动衰减，因此夹具从 1 层改为 2 层，生产公式未变。本切片未实现升级实例，未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab；其余 16 张 `CatalogOnly` 继续由目录门禁拒绝。详见 `06_testing/2026-08-12-machine-gunner-v2l-bombard-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2K 便携帮手基础态（已完成 Unity 原生验证）

- Hero 1002 的同一职业运行时现已注册 `PortableHelper` (3267) 基础态：1 Energy、Power、Self，成功后进入 PowerPile 并增加一层整场持续、可叠加的便携帮手 Power。每一段来自卡牌程序的即时 `IsShootCategory` 实际伤害，在来源伤害和既有命中后/全局效果全部完成后，只要原目标仍存活，就按帮手层数依序对同一目标各追加一次基础 1 点帮手伤害；来源段致死时不触发，某个帮手致死时停止剩余帮手，不重定向也不递归。
- 帮手伤害使用独立伤害类型，只读取来源 `FirePower`、目标 `Vulnerable` 与 `ArmorBreak`，并正常经过 Block/HP；不读取 Strength、Weakness、双方 Smoke、目标 Invisible 或狙击倍率。帮手段没有卡牌标签，不触发 Stim、IncendiaryAmmo、AgedOil、KungfuMech、再次帮手、Ammo 支付或 Invisible 生命周期。来源射击原有的后置效果先完成；燃烧弹药的直接回归锁定顺序为 `来源 Damage → Burn → 帮手 Damage`。
- 作者表仅将 3267 的 `implementation_status` 翻为 `Implemented`。Luban 后机枪兵 82 模板为 **65 张 `Implemented` / 17 张 `CatalogOnly`**，其中 V1 为 54/10，V2 扩展为 11/7；升级为 0 费仍只是作者表元数据。
- Luban、Excel 本地化导入、`Sync and Build All` 与本地 Addressables 构建均成功（12.163 秒）。开发中 Starter 补强任务 `f4c3fc07550d4237b029a112b1ce2563` 为 98/98；最终 Unity MCP 定向 EditMode 任务 `95707f1918fa4633b671c6a10f9b0da3` 为 **120/120 passed**（0.918363 秒），完整 EditMode 任务 `8c0ce8f925e94a35b893f5b5892ef447` 为 **639/639 passed**（131.4561842 秒）。
- Shotgun 通过 `IsShootCategory` 的共用分类在结构上会进入同一即时命中钩子，但当前没有 Shotgun 卡实例，因此本轮没有直接运行用例；延迟 Support/Bomb/Needle/TripleStrike 延迟段不经过即时卡牌命中入口，故不触发帮手，这同样是代码结构证据，不伪报为跨模块运行验收。本切片未实现升级实例，未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab；其余 17 张 `CatalogOnly` 继续由目录门禁拒绝。详见 `06_testing/2026-08-12-machine-gunner-v2k-portable-helper-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2J 回合能量修正基础态（已完成 Unity 原生验证）

- Hero 1002 的同一职业运行时现已开放两张基础态能量卡：`Overload` (3213) 为 0 Energy / Self / Hand→DiscardPile，在当前回合即时获得 2 Energy、受 EnergyMaximum 硬上限裁剪，并累计一层 `NextRoundEnergyGainPenalty`；`DefensiveStance` (3271) 为 1 Energy / Self / Hand→DiscardPile，在同一出牌事务中先获得 8 Block，再累计一层 `NextRoundEnergyGainBonus`。
- Bonus 与 Penalty 是两项独立、非负且可分别叠加的一次性职业私有状态。下一玩家回合开始时，能量补给使用 `max(0, baseGain + bonus - penalty)`，再按 EnergyMaximum 裁剪；补给结算后两项状态在同一次回合开始流程中分别清零。当前回合的主动获得使用 `BattleEnergyGainedSettlement`，回合开始的补给使用 `BattleEnergyRefilledSettlement`，两种事实不合并。
- 作者表只将 3213 与 3271 翻为 `Implemented`。Luban 后机枪兵 82 模板为 **64 张 `Implemented` / 18 张 `CatalogOnly`**，其中 V1 为 54/10，V2 扩展为 10/8；升级列仍只是元数据。
- Luban、Excel 本地化导入、`Sync and Build All` 与本地 Addressables 构建均成功（11.72 秒）。补强获能上限、净修正、补给下限、一次性清除顺序与零反馈表现契约后，Unity MCP 定向 EditMode 任务 `3e73f867e7404be8a3180660e4999d20` 为 **136/136 passed**；完整 EditMode 任务 `56274033527e4c78b50a78313bcc0f6c` 为 **631/631 passed**（17.642 秒）。
- `LimitOverload` (3260) 明确延期且继续为 `CatalogOnly`：当前出牌流程在程序操作提交后才把本卡移出 Hand，若直接复用 `DrawCards(10)` 实现“抽到手牌满”，计算时会把 3260 自身仍计入手牌并少抽一张。它需要独立的“抽至满手”卡区预演/提交 seam，能够基于本卡成功归宿后的投影手牌冻结抽牌数量；V2J 不以固定抽牌数或提前移牌伪装实现。
- 本切片未实现升级实例，未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab。详见 `06_testing/2026-08-12-machine-gunner-v2j-round-energy-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2I 充能爆射基础态（已完成 Unity 原生验证）

- Hero 1002 的同一职业运行时现已注册 `ChargedBurst` (3282) 基础态：2 Energy、Attack、AllEnemies，成功后进入 DiscardPile；施放时按 Encounter 顺序快照全部存活敌人，基础 12 按序号线性增加 50%，前三段为 12/18/24。
- 前段致死不会重排快照或降低后续段伤害。该程序只声明 `Sniper`，不吃 Stim 的额外段或 FirePower，逐目标读取 IncendiaryAmmo，并在成功攻击后保留 Invisible；显式 `TargetId` 和能量不足路径均保持参与者、资源、状态、随机流、卡区与表现结果零写入。
- 作者表只将 3282 翻为 `Implemented`。Luban 后机枪兵 82 模板为 **62 张 `Implemented` / 20 张 `CatalogOnly`**，其中 V1 保持 53/11，V2 扩展为 9/9；升级列仍只是元数据。
- Luban、Excel 本地化导入、`Sync and Build All` 与本地 Addressables 构建均成功（11.456 秒）。Unity MCP 定向 EditMode 任务 `1d5c9e1d96fe4ebcadd990fcc73fccdc` 为 **94/94 passed**；完整 EditMode 任务 `822d066bc54c43d78ac206072789f840` 为 **622/622 passed**（18.193 秒）。首次以 `bdaf0` 开头的任务在初始化阶段超时且实际执行 0 项，因此不计为失败回归；随后真实执行任务已全绿。
- 本切片未实现升级实例，未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab，也未接入临时卡、选择、自动免费攻击、AnyAlly 或其他跨卡协议。详见 `06_testing/2026-08-12-machine-gunner-v2i-charged-burst-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2H 焚风基础态（已完成 Unity 原生验证）

- Hero 1002 的同一职业运行时现已注册 `FoehnWind` (3276) 基础态：2 Energy、Skill、显式 Enemy，成功后进入 DiscardPile；结算时读取施放者当时的全部 Smoke，以该值向目标施加一次 Burn。
- Smoke 大于 0 时，专用联合操作先按既有 `ApplyBurn` 规则把目标旧 Oil 计入 Burn 并减半 Oil，再把来源 Smoke 清零；settlement 顺序固定为目标 Burn → 可选目标 Oil → 来源 Smoke。Smoke 为 0 时仍正常支付费用并弃牌，但不产生私有状态写入。能量不足或目标规则错误继续保持资源、状态、随机流和卡区零写入。
- 作者表只将 3276 翻为 `Implemented`。Luban 后机枪兵 82 模板为 **61 张 `Implemented` / 21 张 `CatalogOnly`**，其中 V1 保持 53/11，V2 扩展为 8/10；升级列仍只是元数据。
- Luban、Excel 本地化导入、`Sync and Build All` 与本地 Addressables 构建均成功（12.164 秒）。Unity MCP 定向 EditMode 任务 `69b8ded02aaa46368cad35e620567fd2` 为 **89/89 passed**；完整 EditMode 任务 `4a90229794d24d3f8fd85154ab79c250` 为 **617/617 passed**（17.657 秒）。首次以 `902bbc` 开头的任务在初始化阶段超时且实际执行 0 项，因此不计为失败回归；随后真实执行任务已全绿。
- 本切片未实现升级实例，未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab，也未接入临时卡、选择、自动免费攻击、AnyAlly 或其他跨卡协议。详见 `06_testing/2026-08-12-machine-gunner-v2h-foehn-wind-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2G 私人改装基础态（已完成 Unity 原生验证）

- Hero 1002 的同一职业运行时现已注册 `PrivateMod` (3268) 基础态：1 Energy、Power、Self，成功后进入 PowerPile；在同一出牌事务中把 AmmoMaximum +1、保持当前 Ammo 不变，并增加 `FirePower +1` 与一层 PrivateMod Power。
- 私人改装不新增射击事件或命中后钩子。既有 Shoot 伤害管线会让后续每一段射击读取 FirePower；装填则按已提高的新 AmmoMaximum 补充。能量不足仍在权威出牌门禁失败，资源、状态、Power 层数、随机流和卡区保持零写入。
- 作者表只将 3268 翻为 `Implemented`。Luban 后机枪兵 82 模板为 **60 张 `Implemented` / 22 张 `CatalogOnly`**，其中 V1 保持 53/11，V2 扩展为 7/11；升级列仍只是元数据。
- Luban 与 Excel 本地化导入成功。首轮 `Sync and Build All` 的 Addressables 构建为 11.092 秒；重新导入本地化后执行的最终同步构建为 4.376 秒。Unity MCP 定向 EditMode 任务 `cfcf49e9a16e447fb4033af6108c8dd9` 为 **85/85 passed**；完整 EditMode 任务 `0f17edd2f31d40d2aba328def4448f3c` 为 **613/613 passed**（18.617 秒）。首次以 `9e20` 开头的任务在初始化阶段超时且实际执行 0 项，因此不计为失败回归；随后真实执行任务已全绿。
- 本切片未实现升级实例，未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab，也未接入临时卡、选择、自动免费攻击或其他跨卡协议。详见 `06_testing/2026-08-12-machine-gunner-v2g-private-mod-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2F 烟雾、防御与标记即时卡（已完成 Unity 原生验证）

- Hero 1002 的同一职业运行时现已注册 3 张 V2 扩展基础态：`ChainSmoke` (3269) 支付 1 Energy、只为施放者增加 `Smoke +5`；`EmergencyCooling` (3272) 支付 1 Energy、严格先获得 8 Block 再增加 `Smoke +3`；`Mark` (3280) 支付 1 Ammo、对显式敌人造成 5 点普通 Attack，并仅在目标仍存活时追加 `ArmorBreak +2`。三张均成功进入 DiscardPile。
- 3269 的本地化卡名即使含“抽”字也不构成抽牌协议，运行时只按 `ProgramId.ChainSmoke` 执行来源 Smoke 操作，不生成 Draw 或补牌。3280 显式声明 `MachineGunnerCardTag.None`，不会因 Attack 类型或 Ammo 成本被推断为射击；Stim、FirePower 与 IncendiaryAmmo 均不参与该卡基础态。
- 作者表只将 3269、3272、3280 翻为 `Implemented`。Luban 后机枪兵 82 模板为 **59 张 `Implemented` / 23 张 `CatalogOnly`**，其中 V1 保持 53/11，V2 扩展为 6/12；升级列仍只是元数据。
- Luban、Excel 本地化导入、唯一既有 Unity Editor 的 `Sync and Build All` 与本地 Addressables 构建均成功（Addressables 11.414 秒）。Unity MCP 定向 EditMode 任务 `054f72bc921749b5bad6d2efcc358b73` 为 **83/83 passed**；完整 EditMode 任务 `ba418ab34a6d44038dddddc0233a03f8` 为 **611/611 passed**（18.618 秒）。首次以 `056109` 开头的 MCP 任务在初始化阶段超时且实际执行 0 项，因此不计为失败回归；后续真实执行任务已全绿。
- 本切片未实现升级实例，未修改默认 Deck、奖励、Run、UI、多人、Scene 或 Prefab，也未接入临时卡、选择、自动免费攻击或其他跨卡协议。详见 `06_testing/2026-08-12-machine-gunner-v2f-smoke-block-mark-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2E 延迟效果与支援链（已完成 Unity 原生验证）

- Hero 1002 的职业运行时现已为 7 张卡注册基础态延迟程序：`GuidedNuke` (3237)、`BansheeStrike` (3238)、`FireSupport` (3239)、`FireBombardment` (3240)、`FiveHundredPounder` (3241)、`TripleStrike` (3264) 与 `NeedleStorm` (3274)。每次成功施放创建独立、按插入顺序结算的职业私有延迟实例；多实例不合并为 Power 层数。随机段在触发时从当前投影中的存活敌人逐段选择，并只随完整计划成功提交随机状态。
- 回合开始的延迟计划位于最后一名敌人行动成功之后、敌方 Smoke 清理、玩家资源补充与抽牌之前；女妖每次触发先锁定当前最近敌人，火力支援逐段随机，燃烧轰炸逐目标执行 `Support → 存活后 Burn → Oil`，三连击延迟段选择当前最远敌人，钢针逐段执行 `Delayed → 存活后 ArmorBreak`。回合结束中，当前已接入顺序为弃牌、清除 Shackle/LoseStrength/既有临时状态、炸弹类延迟、Burn；来源中的恢复卡尚未实现，因此本切片没有伪造恢复结算。战斗已终局时不再执行后续延迟工作，遗留实例随 BattleEnded 清空。
- 基础态冻结为：3237 支付 5 Energy、立即 `Shackle +1`、施放回合末开始倒计时并在第三个未来回合末对全体造成 99 Bomb；3241 支付 3 Energy，在第二个未来回合末对全体造成 60 Bomb；3238 支付 2 Energy，在后续两个玩家回合开始各执行最近目标 `Support 8×2`；3239 支付 1 Energy，在下回合开始执行 5 次随机 `Support 2`；3240 支付 2 Energy，在下回合开始执行两波全体 `Support 2 → Burn +4 → Oil +3`；3264 支付 4 Energy 与 3 Ammo、先得 Invisible +2 再对显式敌人执行两次 Sniper 12、进入 ExhaustPile，随后下回合对最远敌人造成 Support 20；3274 支付 1 Energy，下回合开始进行 4 次随机 Delayed 1，并仅在该段后目标存活时加 `ArmorBreak +1`。
- 本批“脑补”已被冻结为项目实现事实：Support 只读取目标 Smoke、Vulnerable 与 ArmorBreak；Bomb 只读取目标 Smoke；钢针 Delayed 只读取目标 Smoke，不读取 Vulnerable、ArmorBreak 或来源修正；同阶段多实例按创建顺序处理，每个随机段从当时投影的存活敌人重新取候选。Shackle 只阻止 Attack，技能仍可使用，并在玩家当前行动结束清除。升级数值仍只是作者表元数据，未实现升级 CardInstance。
- 作者表只将上述 7 个精确身份翻为 `Implemented`。Luban 后机枪兵 82 模板为 **56 张 `Implemented` / 26 张 `CatalogOnly`**，其中 V1 为 53/11、V2 扩展为 3/15；未把这些卡加入默认 Deck、奖励或 Run，也未修改 UI、多人或升级实例。
- Luban、Excel 本地化导入、唯一既有 Unity Editor 的 `Sync and Build All` 与本地 Addressables 构建均成功（Addressables 14.252 秒）。Unity MCP 定向 EditMode 任务 `586264ec18e549d89d1a063aac4d7b93` 为 **101/101 passed**；完整 EditMode 任务 `89cfdfe8441b45d39d0cd57d939734c7` 为 **606/606 passed**（46.847 秒）。已知边界是：round-start 延迟阶段内部按单份投影计划联合准备/校验/提交，但它发生在最后一名敌人的行动事务已经提交之后，当前没有覆盖“敌人行动 + round-start 延迟阶段”的跨事务回滚；正常 Queue 串行路径已验证，异常提交故障下的完整跨域原子回滚仍未提供。详见 `06_testing/2026-08-12-machine-gunner-v2e-delayed-support-scheduler-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2D 击退射击与失去力量（已完成 Unity 原生验证）

- `KnockbackShot` (3223) 已开放基础态运行时：支付 1 Ammo，不使用 `Shoot` / `Sniper` 标签；施放时按 Encounter 顺序快照前两名存活敌人，第一目标依次承受 7 点 Attack 与存活后 `LoseStrength +2`，第二目标依次承受 3 点 Attack 与存活后 `LoseStrength +2`。只有一名存活敌人时第二段跳过；第一段击杀不会从更后的敌人递补；该程序拒绝显式 `TargetId`，目标完全由职业运行时推导。
- `LoseStrength` 是独立于 `Weakness`、`Vulnerable` 和永久 `Strength` 的职业私有非负状态。Attack 的来源力量项按 `max(0, baseDamage + Strength - LoseStrength)` 计算，Burn 不读取该状态。携带者在自己的行动结束时清零并写入私有状态 settlement；敌人按“行动 Effect / completion → 清除 LoseStrength → intent advance”结算，玩家则在自己的行动结束阶段、回合末 Burn 之前清除。预演、校验、提交和连续 Order 仍沿用既有 Queue 权威事务，没有新增第二条共享写入路径。
- 作者表仅将 3223 的 `implementation_status` 翻为 `Implemented`；Luban 后机枪兵目录保持 82 项，当前为 **49 张 `Implemented` / 33 张 `CatalogOnly`**，其中 V1 为 47/17，V2 扩展为 2/16。当前没有升级 CardInstance，因此 9/5 伤害与 `LoseStrength +3` 仍只保留为作者表元数据；本轮未把卡加入默认 Deck、奖励或 Run。
- `dotnet build TinySpire.sln --no-restore -v:minimal` 为 **0 errors / 12 warnings**。Luban、Excel 本地化导入、唯一既有 Unity Editor 的 `Sync and Build All` 与本地 Addressables 构建均成功（Addressables 13.783 秒）；Unity MCP 定向 EditMode 为 **10/10 passed**（0.3051449 秒），完整 EditMode 为 **597/597 passed**（17.8830785 秒）。详见 `06_testing/2026-08-12-machine-gunner-v2d-knockback-lost-strength-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 V2C 破甲即时卡（已完成 Unity 原生验证）

- 在 V2 扩展目录中，仅将来源完整且复用既有结算链的两张基础态卡翻为可执行：`ThermiteBomb` (3273) 为 1 Energy / AllEnemies / Hand→DiscardPile，程序按操作顺序先对 Encounter 中每名存活敌人施加 `Burn +4`（沿用已有 Oil 消耗与减半），再施加持续的 `ArmorBreak +2`；`Crush` (3281) 为 1 Energy / 自动最近敌人 / Hand→DiscardPile，先进行 9 点普通 Attack，目标在该段后仍存活时才施加 `ArmorBreak +4`。两张的升级数值仅保留作者表元数据，当前没有升级 CardInstance。
- `ArmorBreak`、全体 Burn、自动最近目标、攻击后置状态、私有状态 settlement 与 Burn 伤害规则均复用既有职业运行时；未增加新的状态、生命周期、Queue 写入口或表现通道。扩展目录门禁由“新身份全为 CatalogOnly”的录入期规则收敛为精确状态快照：只有 `MARINE_THERMITE_BOMB` 与 `MARINE_CRUSH` 为 `Implemented`，其余 16 张 V2 新身份仍为 `CatalogOnly`。机枪兵 82 模板当前为 **48 张 `Implemented` / 34 张 `CatalogOnly`**。
- 作者工作簿仅将 3273 与 3281 的 `implementation_status` 翻为 `Implemented`；Luban 生成成功，Excel 本地化导入成功，唯一既有 Unity 6000.5.5f1 Editor 的 `Sync and Build All` 成功并完成本地 Addressables 内容构建（19.571 秒）。最终 Unity MCP 定向 EditMode `f60e712386064ac1b558bfc3f66a0c8f` 为 **81/81 passed**，完整 EditMode `65b4d39df04142409f9b8f0355a6d063` 为 **589/589 passed**。未暂存、提交或推送；详见 `06_testing/2026-08-12-machine-gunner-v2c-armor-break-instant-cards.md`。

## 2026-08-12 Marine Game 机枪兵 V2B 82 模板目录扩展（已完成 Unity 原生验证）

- V2 行为来源 `00_inbox/README web.md` 的目录目标现已完整落到作者表：5 张初始、76 张奖励、1 张临时，共 **82 个模板**。本批在 V2B 初始录入时新增 3265–3282 共 18 个 V2 奖励身份，并补齐其 Program 65–82、双语文本、升级元数据与占位插图键；当时新增身份统一保持 `CatalogOnly`，没有因“已经录入”而被误标为可战斗使用。
- 构建门禁保留原 V1 64 模板快照，另以 `marine-game-v2-20260812-cards` 独立检查 18 个扩展身份、连续 ID/Program、精确实现状态、空 effect bindings、占位图与升级元数据。V2B 验收时目录为 **46 张 `Implemented` / 36 张 `CatalogOnly`**；随后 V2C 只打开 ThermiteBomb 和 Crush，余 16 张 V2 卡仍在运行时程序之前被状态门禁拒绝，也未加入默认 Deck、奖励或 Run。
- Luban 和 Unity 本地化导入均成功；唯一既有 Unity 6000.5.5f1 Editor 的 `Sync and Build All` 成功，并完成本地 Addressables 内容构建（18.781 秒）。Unity MCP 定向 EditMode `40db6aacf30e4bfbbebfe725d734695f` 为 **112/112 passed**，完整 EditMode `a5646a7a960d43348acdf39083b23f95` 为 **586/586 passed**。未暂存、提交或推送；详见 `06_testing/2026-08-12-machine-gunner-v2b-catalog-extension.md`。

## 2026-08-12 Marine Game 机枪兵 V2A 伤害语义、电磁增压与防御靶机（已完成 Unity 原生验证）

- 用户指定 `00_inbox` 的当前 `README web.md` 为新版卡牌规则来源；同目录 `READ.md` 与 `HANDOFF.md` 中冲突的旧 Mod 说明不再参与行为裁决。新目标目录为 82 个模板，但现有作者表仍为 64 个；本批只更正可验证的既有卡和基础运行时，当前为 **46 张 `Implemented` / 18 张 `CatalogOnly`**，不把尚未录入的 18 张新奖励卡伪报为完成。
- 伤害请求现在以 `MachineGunnerDamageKind` 与 `MachineGunnerCardTag` 声明。Pipeline 内部规则档案分别处理 Attack、Support、Bomb、Burn、Debuff；`Shoot` 才读取 Stim/FirePower，`Sniper` 读取燃烧弹药和狙击倍率但不读 FirePower，`SpikeShot` (3248) 显式为 `Shoot | Sniper`。这同步修正了 Spike 的新版射击/狙击双词条和易伤数值回归。
- `ElectroBoost` (3236) 按新表更新为 1 Energy / Uncommon / Power / Hand→PowerPile，基础态 `FirePower +3` 可叠加并持续到战斗结束。`DefenseTarget` (3262) 以 2 Energy / Self / Hand→ExhaustPile 开放，最少 2、最多 9 弹，每实际 3 弹获得 1 层 Intangible；2 弹成功但不产生零值状态 settlement。
- Intangible 只对正值 incoming Attack 在 Block 前封顶为 1，并在 Damage settlement 后消费一层、不随回合衰减。Effect 链在局部投影中预留 Buffer/Intangible 后效；同段两者并存时 Buffer 优先、完全抵挡不消费 Intangible，此组合优先级已记录为项目实现决定。Luban、从 Excel 导入本地化、唯一既有 Unity 6000.5.5f1 Editor 的 `Sync and Build All` 与本地 Addressables 均成功；最终 MCP 定向 EditMode `6426b623af174b908464886033acfda5` 为 **110/110 passed**，完整 EditMode `3e20086749f1423f99784361f0477cf5` 为 **584/584 passed**。未暂存、提交或推送；详见 `06_testing/2026-08-12-machine-gunner-v2a-damage-taxonomy-defense-target.md`。

## 2026-08-12 Marine Game 机枪兵 MG14B 游击战术（已完成 Unity 原生验证）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 已注册 `GuerrillaTactics` (3251) 的基础态程序：支付 1 Energy、Self、Hand→Power。每张成功归宿的能力牌声明 `PowerStackGain = 2`，因此 PowerPile 中的一张实体卡对应 2 层游击；两张实体卡保留为两个不同实例、总层数为 4。当前没有升级 CardInstance，作者表中的升级 3 层只保留为元数据。
- `MachineGunnerCostResolution` 现在明确区分实际扣除的 `AmmoSpent` 与仅供游击触发读取的冻结名义值 `AmmoSpentForGuerrilla`。当前普通支付令两者相等：每张成功卡在原有操作后按“游击层数 × 名义弹耗”追加 Block；能力牌本身不耗弹，故不会立即给 Block。Stim 使射击实际/名义耗 2 弹时，2 层游击给 4 Block。该分离只为未来免费攻击和虚拟耗弹提供受控接缝，本轮未实现 TacticalAdvance、固定机枪或临时 MachinegunBurst。
- 作者工作簿仅将 Q141（3251）的 `implementation_status` 从 `CatalogOnly` 翻为 `Implemented`；Luban 生成成功，生成目录核验为 **45 张 `Implemented` / 19 张 `CatalogOnly`**。`dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore -v:q` 为 **0 errors**（保留既有 12 条 `MSB3277` 警告）；唯一既有 Unity 6000.5.5f1 Editor 的 `TinySpire/Build/Sync and Build All` 成功，Console 记录本地 Addressables 内容构建和同步完成。
- Unity MCP 定向 EditMode `02370c5357374fb1aaff48682cf22532` 为 **6/6 passed**，覆盖目录门禁、3251 元数据、两层叠加、Stim 的实际 2 弹→4 Block、失败零写入和 PowerPile 实例归宿；完整 EditMode `d3968d32a61f4a8cb9bf9c3396b905b0` 为 **574/574 passed，0 failed，0 skipped**（57.9375737 秒）。未暂存、提交或推送；详见 `06_testing/2026-08-12-machine-gunner-mg14b-guerrilla-tactics-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 MG14A 撤退与快速翻滚（已完成 Unity 原生验证）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 已注册 `Retreat` (3216) 和 `QuickRoll` (3235) 的基础态程序。撤退支付 2 Energy 后获得 15 Block、预约下回合补满当前 Ammo 上限、Hand→DiscardPile，并请求结束本次玩家行动；快速翻滚支付 1 Energy 后获得 5 Block、叠加 5 层下挡、Hand→DiscardPile。当前没有升级 CardInstance，作者表的升级数值只保留为元数据。
- `NextRoundBlock` 和 `ReloadAmmoAtNextPlayerRound` 是机枪兵私有战斗状态。下一玩家回合的冻结顺序为：清除既有 Block；清除下挡并给予其总值 Block；应用既有资源档案的普通补充；清除补满弹预约并将 Ammo 填至当前最大值；最后走既有抽牌。两张快速翻滚会把预约值累加为 10，且只在下次开始结算一次；撤退测试锁定 Ammo 0→1→5。
- 撤退不从职业运行时嵌套提交命令：`BattleTurnOperationResult` 只携带请求结束的 actor，`BattleCommandQueue` 在成功 Play 后用 system token 冻结 `EndPlayerActionCommand` continuation；控制器在发布前设同 actor 的强制结束锁，普通 Play/End 重入零写入拒绝，系统结束完成后清锁。Queue 仍独占顺序、展示屏障、continuation 与 drain。
- 作者工作簿仅把 Q106（3216）和 Q125（3235）的 `implementation_status` 从 `CatalogOnly` 翻为 `Implemented`；最终 `battle.card.xlsx` SHA-256 为 `DFDA339D3E1654176A75E6A8F7E3875B33021676477D37FE55CB7179D5128E05`。Luban 生成成功，生成目录核验为 **44 张 `Implemented` / 20 张 `CatalogOnly`**；3234 `TacticalAdvance` 未改，继续 CatalogOnly。
- `TacticalAdvance` 的“下一张攻击免费”与 Stim 额外射击的弹药支付优先级、以及 Bound 前置规则尚未有可执行口径，因此本轮没有猜测或实现它。静态 `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore` 为 **0 errors**（保留既有 12 条 `MSB3277` 警告）；唯一既有 Unity 6000.5.5f1 Editor 的 `TinySpire/Build/Sync and Build All` 成功。本轮定向 Unity MCP EditMode `534c896788734535bf40275aadf41083` 为 **7/7 passed**，收紧普通卡辅助断言后的完整任务 `352921ecce6246cda6cf792348a0c393` 为 **571/571 passed，0 failed，0 skipped**（221.6869278 秒）。未暂存、提交或推送；详见 `06_testing/2026-08-12-machine-gunner-mg14a-retreat-quick-roll-runtime.md`。

## 2026-08-12 Marine Game 机枪兵 MG13 全息诱饵（已完成 Unity 原生验证）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 为 `HoloDecoy` (3259) 注册基础值程序：支付 1 Energy、Self 输入、以既有私有状态 settlement 施加 `Buffer +1`，并从 Hand 移至 ExhaustPile。`Buffer` 是可叠层、无回合衰减的职业私有状态；它只对正值的 incoming Attack 生效，使本次伤害完全不改变 Block/HP，随后消费 1 层。零值攻击不消费 Buffer。基础卡以外的奖励/Run、HUD、Scene、Prefab、默认 Hero/Deck 与第二条写入链均不在本切片范围内。
- 为避免 Effect 预演阶段写入真实战斗状态，通用伤害覆盖改为按单个 Effect 链创建局部 `IBattleDamageFormulaOverrideSequence`：它只在局部投影中预留 Buffer，并在提交时严格写出“原始 Damage settlement → Buffer 状态 settlement”。`BattlePreparedEffectPlan.PlannedSettlementCount` 统一包含这类后续 settlement，敌人意图和玩家卡牌的下一个 Order 均以该总数推进，因此同一 Effect 链的两段伤害只会消费一个 Buffer，且不会与下一段/下一敌人的 Order 重叠。
- 作者工作簿仅把 Q149（3259）的 `implementation_status` 从 `CatalogOnly` 翻为 `Implemented`，值差异、重导入、渲染与公式错误扫描没有其他变化；最终 SHA-256 为 `926CE36A1E6190B4B1BFD1EF93AC6C396AFC1B37E37F840622437C89864BB57A`。Luban 生成成功并恢复其移除的 `game-config.json`；生成 JSON 核验为 **42 张 `Implemented` / 22 张 `CatalogOnly`**，3259 为 Program 59、Cost 1、Self、基础/升级均 ExhaustPile。
- 来源存在一处明确未裁决冲突：`README web.md` 声称 3259 升级后“不消耗”，作者表的 `upgraded_play_destination` 仍是 ExhaustPile，且项目尚无 CardInstance 升级状态。本轮保持作者表字段、不伪造升级运行时，并将该问题留给升级实例/作者表裁决切片。
- 静态 `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore` 为 **0 errors**（保留 Unity 依赖图既有的 12 条 `MSB3277` 警告）。2026-08-12 在唯一既有 Unity 6000.5.5f1 `TinySpire` Editor 运行 `TinySpire/Build/Sync and Build All`；首次调用因资源导入域重载中断，重试后的 Console 明确记录 BuildLayout 写入、`Addressable content successfully built (duration : 0:00:21.093)` 与整体同步完成。最终 Unity MCP EditMode 任务 `dff6b79a8f4e486297d1cdd410c029a6` 为 **119/119 passed，0 failed，0 skipped**（0.5207738 秒），覆盖 Buffer 的施加/消耗/叠层/零写入、单链双段伤害、连续 Order、目录快照和构建门禁。未暂存、提交或推送；详见 `06_testing/2026-08-12-machine-gunner-mg13-holo-decoy-runtime.md`。

## 2026-08-11 Marine Game 机枪兵 MG12 光学迷彩（已完成 Unity 原生验证）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 为 `OpticalCamo` (3249) 注册基础值程序：支付 2 Energy、Self 输入、从 Hand 移至 DiscardPile，并以既有职业私有状态操作施加 `Invisible +2`。表内升级费用 1 继续只作为作者表元数据保留；项目尚无通用 CardInstance 升级态，未硬编码升级运行时数值。既有伤害管线的隐身受击减半语义保持不变，本切片不新增 HUD、角色半透明或场景表现。
- 隐身生命周期收敛为调用方显式定义时机：玩家行动结束减少 1 层；普通攻击仅在整张卡已成功进入卡区归宿后减少 1 层，资源不足等失败路径不减少。`MachineGunnerCardProgram.PreservesInvisibleAfterSuccessfulAttack` 只允许攻击程序声明，3247 `SniperShot` 与 3248 `SpikeShot` 显式保留隐身。该字段不能复用 `IsSniper`：3248 是“射击 + 狙击/不破隐”语义，但现有伤害公式仍需保留其开火加成；本切片不假装解决双词条的更广伤害建模缺口。
- 作者工作簿仅把 Q139（3249）的 `implementation_status` 从 `CatalogOnly` 翻为 `Implemented`，值差异、重导入、渲染与公式错误扫描均无额外变化；最终 SHA-256 为 `4F46A9D6D7F570686D898394AC0D249E4150BBD9BC3661204CDC11495546327F`。Luban 生成成功并恢复其移除的 `game-config.json`；生成 JSON 核验为 **41 张 `Implemented` / 23 张 `CatalogOnly`**，3249 为 Program 49、Cost 2、升级 Cost 1、Self、DiscardPile。
- 静态 `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore` 为 **0 errors**（保留 Unity 依赖图既有的 12 条 `MSB3277` 警告）。2026-08-11 已在唯一既有 Unity 6000.5.5f1 `TinySpire` Editor 重新执行 `TinySpire/Build/Sync and Build All`；Console 明确记录 BuildLayout 写入、`Addressable content successfully built (duration : 0:00:28.345)` 与整体同步完成。最终 Unity MCP EditMode 任务 `e2f9b873188a4ed7a12a2f073f90b492` 为 **75/75 passed，0 failed，0 skipped**（1.5422825 秒），覆盖机枪兵运行时、伤害管线、卡牌规则、目录快照与构建门禁，含四项 3249 行为回归。未修改升级实例、奖励/Run、Power HUD、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径；未暂存、提交或推送。详见 `06_testing/2026-08-11-machine-gunner-mg12-optical-camo-runtime.md`。

## 2026-08-11 Marine Game 机枪兵 MG11 爆炸肘（已完成 Unity 原生验证）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 为 `ExplosiveElbow` (3252) 增加基础值程序：自动选择最近存活敌人，支付 2 Energy 后先进行 10 点普通 Attack。若该次攻击后目标仍存活，运行时继续沿用既有逐段投影顺序：卡牌后置状态、`IncendiaryAmmo`、`AgedOil`，最后读取该时刻的 Burn 追加一次等值 Debuff 伤害。该立即触发不消耗或改写 Burn/Oil；Debuff 只使用 Block/HP 伤害路径，不读取 Weakness、Smoke 或 Vulnerable，且不消耗 Armor。普通攻击已致死则跳过后续 `AgedOil` 和立即 Burn；卡牌最终从 Hand 移至 DiscardPile，终局仍在整张卡提交后统一派生。
- 需求来源继续遵循当前摘要的优先级：`marine-game/cards.json` / `battle.html` / `README.md` 的 2 Energy、基础攻击 10 和立即触发既有 Burn 被采用。`00_inbox/HANDOFF.md` 中未纳入该摘要来源链的 STS2 Mod “1 Energy / 8（升级 11）”历史说明未用于本卡，故未修改作者表已有的费用、`Enemy` 目录分类或 DiscardPile 归宿；`Enemy` 仍是目录输入映射，运行时最近目标由职业程序自动推导。
- 作者工作簿仅把 Q142（3252）的 `implementation_status` 从 `CatalogOnly` 翻为 `Implemented`，并经值差异、重导入、渲染与公式错误扫描复核；最终 SHA-256 为 `47490664EFAFB9553BC80CD181301FE65F810B2B1F433734094A38346DF0B7D6`。Luban 生成成功并恢复其移除的 `game-config.json`；生成 JSON 核验为 **40 张 `Implemented` / 24 张 `CatalogOnly`**，3252 为 Program 52、Cost 2、Enemy、DiscardPile。
- 静态 `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore` 为 **0 errors**（保留 Unity 依赖图既有的 12 条 `MSB3277` 警告）。2026-08-11 已在唯一既有 Unity 6000.5.5f1 `TinySpire` Editor 执行 `TinySpire/Build/Sync and Build All`；首次传输因编辑器域重载断连，重连后的 Console 明确记录 `Addressable content successfully built (duration : 0:00:52.963)`、BuildLayout 写入和整体同步完成，未出现 Error。最终将“当前 Burn”读取收敛到同一操作的职业状态投影后，Unity MCP EditMode 任务 `e336ac7ff03548d6929571c4b9c5f803` 为 **70/70 passed，0 failed，0 skipped**（1.1632452 秒），覆盖机枪兵运行时、伤害管线、卡牌规则、目录快照和构建门禁；其中四项 3252 行为回归全部通过。未修改升级实例、奖励/Run、Power HUD、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径；未暂存、提交或推送。详见 `06_testing/2026-08-11-machine-gunner-mg11-explosive-elbow-runtime.md`。

## 2026-08-11 Marine Game 机枪兵 MG10B 不充分爆燃（已完成 Unity 原生验证）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 为 `IncompleteCombustion` (3222) 增加专用预演/提交操作：先按 Encounter 顺序冻结开始时“存活且 Burn > 0”的燃烧来源；每个冻结来源再对当时仍存活的敌人逐一造成等于其冻结 Burn 的 Debuff 伤害。来源即使已被前一来源杀死仍按快照继续造成伤害，死亡目标跳过；全部伤害完成后，才按 Encounter 顺序对仍存活敌人写入 `Smoke += Burn`、`Burn = 0`。伤害 settlement 的来源保持为该燃烧敌人，状态 settlement 的来源为玩家；全过程不读取、不写入 Oil，也不调用会消耗 Oil 的 `ApplyBurn`。
- 已将职业程序的卡区归宿受限扩展为支持注册卡的 `ExhaustPile`：3222 不需要显式目标，支付 3 Energy 后从 Hand 移至 ExhaustPile；它的表内 `target_rule` 仍是 `Self`，全敌交叉目标只在此专用程序内部按 Encounter 顺序推导。所有共享写入仍经既有 `BattleCommandQueue.Submit` 事务，终局保持在整张卡提交后统一派生。
- 作者工作簿仅把 Q112（3222）的 `implementation_status` 从 `CatalogOnly` 改为 `Implemented`，值差异、重导入、渲染与公式错误扫描均通过；最终 SHA-256 为 `1AEDDC31EF90888F8B37A4A4B69807E74B3E287E21E15C2E920351AA58347471`。Luban validation/生成成功并恢复其移除的 `game-config.json`；生成 JSON 核验为 **39 张 `Implemented` / 25 张 `CatalogOnly`**，3222 为 Program 22、Cost 3、Self、ExhaustPile。
- 静态 `dotnet build TinySpire/Assembly-CSharp-Editor.csproj --no-restore` 为 **0 errors**（保留 Unity 依赖图既有的 12 条 `MSB3277` 警告）。2026-08-11 已绑定唯一已连接的 Unity 6000.5.5f1 `TinySpire` Editor，并执行 `TinySpire/Build/Sync and Build All`；菜单调用期间 MCP 传输断连，但重连后的 Console 明确记录 `Addressable content successfully built (duration : 0:00:25.627)`，且没有 Error。随后 Unity MCP EditMode 任务 `94b4d610258b4b05a896adfd20ca6428` 为 **65/65 passed，0 failed，0 skipped**，覆盖机枪兵运行时、伤害管线、卡牌规则、目录快照和构建门禁；其中四项 3222 行为回归全部通过。未修改升级实例、奖励/Run、Power HUD、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径；未暂存、提交或推送。详见 `06_testing/2026-08-08-machine-gunner-mg10b-incomplete-combustion-runtime.md`。

## 2026-08-07 Marine Game 机枪兵 MG10A 烈火烹油回合末运行时（已完成）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 为 `BurningOil` (3254) 增加独立的回合末预处理：最后一名存活玩家结束行动后，先按 Encounter 顺序扫描存活敌人；当该 Power 至少持有一张且敌人已有 Burn 时，写入 `Burn += 1 + Oil`，不减半、不消耗 Oil，也不作用于玩家。所有增长 settlement 先于既有 Burn Debuff 伤害；随后仍在原有 `EndPlayerAction` / `BattleCommandQueue.Submit` 单一命令内结算 Block、死亡与胜负收口。多张烈火烹油可累积 PowerPile 层数，但只启用一次固定增长。
- 作者工作簿仅把 Q144（3254）的 `implementation_status` 从 `CatalogOnly` 改为 `Implemented`，并经值差异、重导入、公式错误扫描与前后渲染复核；最终 SHA-256 为 `E005A9548A02D79D67C1CF9F8EC848F66399B951C7B6A1FCE88F408DC57406F8`。Luban validation/生成成功并恢复其移除的 `game-config.json`，生成目录精确为 **38 张 `Implemented` / 26 张 `CatalogOnly`**；3254 为 Program 54、Cost 2 的 Power → PowerPile。
- 单一已连接 Unity 6000.5.5f1 Editor 已执行 `TinySpire/Build/Sync and Build All`；Console 明确记录本地 Addressables 内容构建成功（10.402 秒）和 `TinySpire sync and local content build completed successfully.`。Unity MCP EditMode 任务 `4afefa7766cb454eb0aeb9b8da061afe` 为 **60/60 passed，0 failed，0 skipped**。未修改升级实例、奖励/Run、Power HUD、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径；未暂存、提交或推送。详见 `06_testing/2026-08-07-machine-gunner-mg10a-burning-oil-runtime.md`。

## 2026-08-07 Marine Game 机枪兵 MG9 逐段命中后置状态运行时（已完成）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 为攻击型程序新增受限的 `PostHitOperations`：每一段实际命中都在同一张卡的本地投影中按“伤害 → 程序命中后状态 → 全局命中钩子”预演，并随同原有 `BattleCommandQueue.Submit` 命令原子提交。目标在伤害后仍存活时才接收后置状态；被 Block 完全吸收但仍存活的命中仍会触发，致死命中则不会留下状态。`SpikeShot` (3248) 因此每段依次施加 `Weakness +1`、`Vulnerable +1` 和燃烧弹药的 Burn，Stim 追加命中完整重复该序列，前段易伤会影响后段伤害。
- `IncendiaryAmmo` (3210) 进入 `PowerPile` 后可叠层，任何实际 `IsShoot` 命中（含狙击）均在伤害后、目标仍存活时施加“层数 × 1”的 Burn；它与 MG8 的开火保持独立，所以狙击仍不读取 `FirePower`，但会触发燃烧弹药。`AgedOil` (3253) 仅在非射击攻击的每一段存活命中后施加固定 `Oil +2`；多张牌只启用该钩子而不放大数值。`FlameElbow`、`KidneyShot`、`PainfulElbow` 和 `SniperShot` 的既有后置状态同步迁入此顺序，保证烈焰肘击先消耗旧 Oil 生成 Burn、再由陈年机油补 Oil。
- 作者工作簿仅把 Q100、Q138、Q143（3210、3248、3253）的 `implementation_status` 从 `CatalogOnly` 改为 `Implemented`，值差异、重导入、渲染和 SHA-256 部署复核均通过；最终 SHA-256 为 `024B76E9E284B00247FD111EB5E0349CD988782C5CC281BB91C5808B54C1623E`。Luban validation/生成成功并恢复其移除的 `game-config.json`，快照为 **37 张 `Implemented` / 27 张 `CatalogOnly`**。单一已连接 Unity 6000.5.5f1 Editor 已调用 `TinySpire/Build/Sync and Build All`，MCP 返回调用成功；本轮 Console 未回传可存档的完成行，随后 Refresh 编译无产品错误。Unity MCP 定向任务 `2ec0afd4a36a46358aaba107ca8a5d2d` 为 **57/57 passed，0 failed，0 skipped**。未修改升级实例、奖励/Run、Power HUD、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径；未暂存、提交或推送。详见 `06_testing/2026-08-07-machine-gunner-mg9-per-hit-runtime.md`。

## 2026-08-07 Marine Game 机枪兵 MG8 功夫机甲、开火与连肘运行时（已完成）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 新增 `KungfuMech`、`ElectroBoost` 和 `ComboElbow` 三个基础值程序。功夫机甲进入既有 `PowerPile` 并叠加；每张成功完成的非射击攻击只在整张卡操作之后获得 `4 × 层数` Block，射击不触发。开火是玩家单场私有 `FirePower`，电磁增压每次 +2、可叠加、在玩家行动结束时清零；每段常规射击在 Weakness 前加开火，而更新包的可执行原型把狙击明确排除。连肘为最近敌人 10 点攻击，只有当前玩家回合紧邻上一张成功牌为非射击攻击时免费；成功连肘可续链，失败卡不污染事实，新玩家回合重置。
- `BattleCardPlayRules` 与队首提交共用 `MachineGunnerBattleRuntime.TryPreviewCost` 的只读成本预览，因而剩余 0 Energy 的合格连肘不会被通用静态费用提前拒绝，也不会形成 UI 和执行使用不同费用语义的第二份规则。所有共享写入仍只由既有 `BattleCommandQueue.Submit` 链路完成。
- 作者工作簿只把 Q102、Q126、Q132（3212、3236、3242）的 `implementation_status` 从 `CatalogOnly` 改为 `Implemented`。工作簿已用值差异、重新导入和前后渲染复核；部署后 SHA-256 为 `7956CD884E0C97585C60DA3C209E84761EEB4CA88421D7D9A0EDACB5DBA53D73`。Luban validation/生成成功并恢复 `game-config.json`；`marine-game-v1-20260807-cards` 精确为 **34 张 `Implemented` / 30 张 `CatalogOnly`**。
- 单一已连接 Unity 6000.5.5f1 Editor 的 `TinySpire/Build/Sync and Build All` 成功完成本地 Addressables 内容构建（11.372 秒）。Unity MCP EditMode 任务 `760444327c1242a5b737f375eef4aaec` 为 **51/51 passed，0 failed，0 skipped**，覆盖职业运行时、伤害管线、卡牌合法性、生成目录快照与构建门禁；刷新编译的 error console 为 0。未修改升级实例、奖励/Run、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径；未暂存、提交或推送。详见 `06_testing/2026-08-07-machine-gunner-mg8-kungfu-firepower-combo-runtime.md`。

## 2026-08-07 Marine Game 机枪兵 MG7 Burn/Oil 生命周期与首批依赖卡（已完成）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 在最后一名存活玩家结束行动后、敌方行动前只结算一次 Burn：按 Encounter 顺序结算存活敌人，随后结算机枪兵玩家。Burn 使用既有 `Debuff` 管线，可被 Block 吸收，不读取 Weakness/Smoke/Vulnerable/Armor，且不衰减。结算仍在当前 `EndPlayerAction` 的单一 `BattleCommandQueue.Submit` 命令中，没有新增命令、队列或 UI 写入口。
- 敌方 Burn 消灭最后一名敌人时立即结束本轮末结算并派生 Victory，跳过玩家自燃；玩家自燃死亡时同一命令派生 Defeat，不会继续敌人阶段。`ApplyBurn` 复用一条纯计算规则：只读取旧 Oil，`Burn += baseBurn + oldOil`，`Oil = floor(oldOil / 2)`；Napalm 先 Burn 后 Oil，故本次新增 Oil 不会自触发。
- 本切片精确开放 `GasPump` (3217)、`Napalm` (3218)、`Molotov` (3219)、`FlameElbow` (3255) 的基础值程序。GasPump 为所有存活敌人 Oil +5；Napalm 对所有存活敌人 Burn 3 后 Oil +5；Molotov 对显式敌人 Burn +5；FlameElbow 对最近敌人先造成 6 点攻击伤害，再只对仍存活目标 Burn +3。致命肘击不会留下 Burn；升级数值未硬编码，因为项目尚无通用 CardInstance 升级态。
- 工作簿只将四个 `implementation_status` 单元格从 `CatalogOnly` 翻为 `Implemented`，并经值差异、重新导入、渲染和 SHA-256 部署复核。Luban validation/生成成功并恢复其移除的 `game-config.json`；`marine-game-v1-20260807-cards` 精确为 **31 张 `Implemented` / 33 张 `CatalogOnly`**。唯一已连接 Unity 6000.5.5f1 Editor 的 `TinySpire/Build/Sync and Build All` 成功完成本地 Addressables 内容构建。
- Unity MCP EditMode 任务 `5db8f11868324b7788a2ef822c9b0ec9` 为 **37/37 passed，0 failed，0 skipped**，覆盖当前机枪兵程序、Burn/Oil 生命周期、配置快照和目录门禁。只读复核后补充“前一敌人死亡但后续敌人与玩家仍继续结算”及“Oil 记录负向减半”两条回归，刷新编译无误后任务 `f2194e4553304b2892deca56de629f3e` 为 **28/28 passed，0 failed，0 skipped**。此前任务 `547990575054409ca86affd800563672` 因连接器 15 秒初始化时限失去回调，控制台显示 Test Runner 已启动/结束；不将其视为产品测试失败，后续成功任务是本切片验收事实。未修改奖励/Run、升级实例、Scene、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径；未暂存、提交或推送。详见 `06_testing/2026-08-07-machine-gunner-mg7-burn-oil-runtime.md`。

## 2026-08-07 Marine Game 机枪兵 MG6 已有 Power 程序门禁开放（已完成）

- 经 64 张机枪兵目录与运行时依赖审计确认，`CoreExpansion` (3206)、`OutputAdjust` (3207)、`BlastShield` (3208)、`MagExpansion` (3209)、`SmokePersist` (3211) 和 `PowerOverclock` (3245) 已在同一 `MachineGunnerBattleRuntime` 内具备注册、支付、Hand→PowerPile 归宿、资源/状态提交与回合读取接线；此前仅因作者表的 `implementation_status = CatalogOnly` 被门禁隔离。
- 使用工作簿值差异、重新导入和前后渲染复核后，只将上述六个单元格翻为 `Implemented`；作者工作簿实际文件与已验证导出文件 SHA-256 一致。Luban 等价命令成功生成 `battle_tbcard.json`，并恢复其会删除的 `game-config.json` 基础设施清单。`marine-game-v1-20260807-cards` 当前精确为 **27 张 `Implemented` / 37 张 `CatalogOnly`**，不以连续 ID 推断可用集合。
- 已在唯一已连接的 Unity 6000.5.5f1 Editor 内执行 `TinySpire/Build/Sync and Build All`，控制台记录本地 Addressables 内容构建成功（6.285 秒）及整体同步成功。Unity MCP 定向 EditMode 任务 `f46ca19e2cfe4785bbca0da4c1769487` 为 **3/3 passed，0 failed，0 skipped**：配置快照门禁、六种 Power 注册表和六张能力的资源/护甲/烟雾/额外抽牌行为均通过。
- 未新增奖励、Run、Power HUD、升级实例、场景、Prefab、默认 Hero/Deck、ProjectSettings、asmdef、HybridCLR 或第二条命令写入链；其余 37 张仍保持 `CatalogOnly`。下一独立切片是回合末 Burn/Oil 生命周期及其胜负中断，随后才开放依赖该生命周期的燃烧卡；未暂存、提交或推送。

## 2026-08-07 Marine Game 机枪兵 MG5 即时状态首批运行时（已完成）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 新增“先预演、再校验、后提交”的状态程序操作：私有 `Weakness` / `Smoke` 使用职业结算记录保留权威顺序但不伪造通用 Effect；通用 `Vulnerable` 复用既有 `BattleStatusAppliedSettlement` 与图标脉冲，职业原生操作以 `EffectId = null` 表达来源。全部共享写入仍只经既有 `BattleCommandQueue.Submit` 链路完成。
- 本切片精确开启 5 张卡：`StunGrenade` (3215) 对全体造成 8 点攻击伤害后仅给投影仍存活目标 `Weakness +1`；`SmokeBomb` (3221) 给自身 `Block +10` 并给自身与全体存活敌人 `Smoke +3`；`KidneyShot` (3228) 为 8 点攻击伤害后 `Weakness +1`；`PainfulElbow` (3229) 为 10 点攻击伤害后 `Vulnerable +2`；`SniperShot` (3247) 自动选择最远敌人、支付 1 Energy/2 Ammo、使用狙击倍率、不接收 Stim 额外命中，随后 `Vulnerable +1`。
- `battle.card.xlsx` 已通过工作簿值差异、重新导入和渲染复核，仅把上述 5 个 `implementation_status` 单元格从 `CatalogOnly` 改为 `Implemented`；Luban validation/生成成功，快照 `marine-game-v1-20260807-cards` 现为 **21 张 `Implemented` / 43 张 `CatalogOnly`**。单一已连接 Unity Editor 执行 `TinySpire/Build/Sync and Build All` 成功，本地 Addressables 内容构建耗时 10.27 秒。
- Unity 刷新编译无产品脚本错误。Unity MCP 的测试任务 `bd475e4578fe4572a6751c80e7f1cf47` 因回调未在 60 秒内上报而被连接器标为初始化失败，但同一次 Unity Test Runner 写入的 `TestResults.xml` 明确为 **2/2 passed，0 failed，0 skipped**，覆盖新增即时状态程序和目录快照门禁；唯一 error-filter 日志是 Test Framework 的“Saving results to TestResults.xml”输出。该 MCP 任务状态偏差作为连接器观察记录，不伪报为产品测试失败。
- 未开启 `SpikeShot` 的逐段“伤害后立即上状态”语义；Burn 相关卡仍等待“玩家行动结束、敌人行动前”的燃烧结算及伤害可否被 Block 阻挡的口径；`IncompleteCombustion` 仍等待 Exhaust、燃烧者×存活目标动态交叉结算与 Burn→Smoke 转换。升级实例、奖励/Run、动态临时卡、选择协议、Scene/Prefab/ProjectSettings/asmdef/HybridCLR/启动 DI 与受保护美术路径均未修改；未暂存、提交或推送。详见 `06_testing/2026-08-07-machine-gunner-mg5-immediate-status-runtime.md`。

## 2026-08-07 Marine Game 机枪兵 MG5 X 费与多段射击首批运行时（已完成）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 现在支持 `CardCostKind.X`、固定/最多/全量弹药支付、Stim 的支付快照、随机多段攻击与逐命中投影。X 在执行前冻结为当前 Energy，允许为 0；随机程序每段只从投影存活敌人中选择，显式复制 `GameRandom.State`，只有整张卡成功完成资源、效果和卡区提交后才回写职业随机流，因此 X=0 和失败路径不会污染后续随机序列。
- 生产配置精确开启 11 张经验证程序：3214、3220、3224--3227、3230、3232、3233、3256、3258。加上既有初始牌后，`marine-game-v1-20260807-cards` 快照为 16 张 `Implemented` / 48 张 `CatalogOnly`；`BattleCardCatalogBuildValidator` 从“连续前五张”改为外部 key 精确集合，防止非连续 ID 的后续卡被误开放。
- `battle.card.xlsx` 已经值差异、重导入与渲染复核，随后 Luban validation/生成成功。单一已连接 Unity Editor 执行 `TinySpire/Build/Sync and Build All`，控制台记录本地 Addressables 内容构建成功（13.262 秒）。定向 EditMode 任务 `e7a502caaa4c4d738cb9a9a96ae6c6d7` 为 **15/15 passed，0 failed，0 skipped**；测试后出现的两条 `Saving results to TestResults.xml` Exception 为 Unity Test Framework 输出，任务结果本身通过且没有产品代码堆栈。
- 未触碰默认 Hero、Hero/Deck 表、奖励/Run、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动 DI 或受保护美术路径；未暂存、提交或推送。下一切片仅处理可在当前私有状态模型内闭环的即时状态卡，延迟/下回合资源/结束行动/选择/动态临时卡和超上限 Energy 语义继续分开处理。详见 `06_testing/2026-08-07-machine-gunner-mg5-x-multishot-runtime.md`。

## 2026-08-07 Marine Game 机枪兵 MG4 职业私有状态与伤害链（已完成）

- Hero 1002 的同一 `MachineGunnerBattleRuntime` 现在持有 `MachineGunnerCombatState`：Weakness、Smoke、Burn、Oil、Armor、Invisible 都是单场职业私有事实，不向默认 Hero 或通用 `CombatantData` 添加字段。攻击伤害在 Effect 预构建期冻结，严格按力量 → Weakness → 攻击者/受击者 Smoke → Vulnerable → Block → HP；Debuff 不吃攻击修正，Burn 施加只读取旧 Oil 后把 Oil 减半。敌方攻击同样使用该职业公式，Armor 只在真实穿透生命后消耗一层。
- 玩家回合开始由职业运行时清空 Smoke，已有 SmokePersist 时改为减一；Weakness 在所属参与者行动结束时减一。通用 Block/Vulnerable 时机仍由既有 `BattleStatusTiming` 所有，Queue 仍是唯一共享写入口。默认 Hero 1001 的 Session 明确断言不装配职业运行时，仍复用共享补至 5 张手牌规则。
- `BattleCardZonesData` 增加 `PowerPile`、职业手牌上限 10 和按原手牌顺序的 `DiscardHandExcept`。目前只有核心扩容、出力调整、防爆护盾、扩容弹夹、烟雾弥漫、动力强化六个具真实私有规则的能力程序可以进入该归宿；生产表内其余 59 张目录卡继续为 `CatalogOnly`，没有奖励、角色选择或 Power UI 路径将其暴露给玩家。Hand→Power 的事实结算暂不生成飞行动画。
- 单一已连接 Unity Editor 刷新编译后 Console 产品错误为 0；定向 EditMode 任务 `d283762aa2ea454ab4638a8ff6165cde` 为 **33/33 passed、0 failed、0 skipped**，覆盖初始牌、敌方伤害/护甲、攻击与 Debuff 公式、Burn/Oil、Smoke、PowerPile/手牌上限/保留顺序、表现路由、随机事务及默认 Hero 回归。详细证据见 `06_testing/2026-08-07-machine-gunner-mg4-private-runtime.md`。
- MG5 才处理 X 费、免费攻击、资源修饰、延迟伤害和结束行动；复杂 Power、临时卡、自动连锁出牌和驻防/排气散热等 PendingResolution 选择均未实现。未改 DataTables、生成 JSON、Localization、Addressables、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动/DI、默认 Hero 或受保护美术路径；未暂存、提交或推送。

## 2026-08-07 Marine Game 机枪兵 MG3 目标与卡牌随机流（已完成）

- 在现有 Hero 1002 会话私有 `MachineGunnerBattleRuntime` 内完成稳定目标选择基础：显式敌人、最近、最远、全体、随机和自身均基于存活敌人的 Encounter 顺序解析；`TryGetLivingEnemyAt` 以同一只读快照提供第二近等可选后续目标。默认 Hero 1001、默认战斗和现有通用目标规则未改。
- 职业随机流不再由目标解析直接写入。运行时先用当前 `CardRandomState` 创建本地 `GameRandom` 副本，只有程序完成资源/效果/卡区提交、当前卡成功离开手牌后，才把候选状态写回；目标非法、无存活敌人和提交前的普通失败均保持随机流不变。这为后续扫射、X 费随机多段等卡牌提供不污染失败路径的基础。
- Unity 单一已连接 Editor 刷新编译后无产品错误；定向 EditMode `c9c735c3070342d6879a1d4d1d01b462` 为 **9/9 passed、0 failed、0 skipped**，覆盖原有初始牌 4 条队列用例以及目标选择的 Encounter 顺序、显式目标、固定种子重放、伪造随机目标、无活敌随机零推进。详细证据见 `06_testing/2026-08-07-machine-gunner-mg3-target-random.md`。
- MG4 才处理 Weakness/Smoke/Vulnerable/Block/HP 的伤害顺序及 Burn/Oil/Armor 时机；未把 Weakness 映射为 Vulnerable，未翻转任何额外 CatalogOnly 卡，未改 DataTables、生成 JSON、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、默认 Hero、受保护美术路径，也未暂存、提交或推送。

## 2026-08-07 Marine Game 机枪兵 MG2B 初始牌运行时（已完成）

- 用户要求在 Unity 内单独为机枪兵增加运行时支持，同时保持“只管卡牌部分”的范围：不改地图、敌人配置、奖励/Run、Scene、Prefab、角色选择、启动/DI 或受保护 Targeting/Candidates/Hermes 美术路径。当前只把首批 5 张初始牌翻为可玩；其余 59 张维持 `CatalogOnly`，不会被 Hero 1002 初始牌组或默认战斗引用。
- `battle.Hero` 增加 `runtime_profile`，Hero 1002 标记为 `MachineGunner` 并使用独立初始牌组；`battle.Card` 增加 `program_id`。`BattleSession` 仅在该档案创建 `MachineGunnerBattleRuntime`，`BattleTurnController`、`BattleCardPlayRules` 与 `HandCardContainer` 使用同一私有实例；默认 Hero 1001 继续走 Legacy 路径。程序只根据生成的强类型 `MachineGunnerProgramId` 解释，不按模板 ID、外部 key、卡名或文本分支。
- 初始程序经 `BattleCommandQueue.Submit` 的现有权威写链执行：射击显式选活敌并支付 Ammo，肘击自动取最近活敌，防御给自身 Block，装填补满 Ammo，兴奋剂抽 2 并在剩余 Ammo 足够时为射击追加一次命中。弹药不足返回 `InsufficientAmmo` 且不写入资源、参与者或卡区；自动/自身目标通过规则投影告知 Hand UI，不会被误判为必须选敌。
- 生成的 `Card.Deserialize` / `Hero.Deserialize` 将 `program_id` / `runtime_profile` 作为必填字段，首次完整 EditMode 暴露旧测试 JSON 缺字段会抛 `ArgumentNullException`。已只在实际反序列化的手写夹具中补 `MachineGunnerProgramId.None` / Legacy `runtime_profile=0`，没有改动直接验证原始 `JObject` 的目录门禁测试。同步发现 `i18n.xlsx` 第 466 行 `smart` 被写为布尔值，已保持样式改为文本 `false` 并重导入/渲染复核。
- `DataTables/gen.bat` 完成 Luban validation 和生成；单一已连接 Unity Editor 的 `TinySpire/Build/Sync and Build All` 成功完成 Localization 导入和 Local Addressables 内容重建。受生成字段影响的定向任务 `b4fe36bc267b43c09764075715c12f2c` 为 **58/58 passed**；完整 EditMode `36884b711939459f932297342218fddc` 为 **500/500 passed、0 failed、0 skipped**。详细验收见 `06_testing/2026-08-07-machine-gunner-mg2b-starter-runtime.md`。
- MG3--MG7 仍未完成：稳定卡牌随机流、Weakness/Smoke/Burn/Oil/Armor 伤害链、抽牌上限/X 费/延迟效果、Power，以及驻防/排气散热的权威 PendingResolution 选择协议。它们不能由当前 UI 私有状态或第二个写入入口代替；未暂存、提交或推送。

## 2026-08-07 Marine Game 机枪兵 MG2A 卡牌目录（已完成）

- 用户将 `00_inbox/marine-game.zip` 的扩展需求收紧为“只管卡牌部分”：只接入机枪兵 64 个卡牌模板及其未来单场规则，不导入地图、敌人、奖励、篝火、事件、跨战斗状态、Run、Scene、Prefab、角色选择或 UI 流程。旧 JSON 摘要/计划已由 2026-08-07 Marine Game 需求摘要和卡牌实施计划替代；MG1 的首回合 Energy `3`、上限裁剪和共享补至 `5` 手牌继续有效。
- `battle.card.xlsx` 已按快照 `marine-game-v1-20260807-cards` 录入 `3201`--`3264`：starter 5（12 个初始实例）、reward 58、temporary 1（仅 `MARINE_MACHINEGUN_BURST`）。64 张均为 `CatalogOnly`、空 `effect_bindings`、`art_placeholder`；63 张声明升级，唯一无升级的是临时机枪扫射。`BattleCardCatalogBuildValidator` 新增精确身份/ID/占位/空程序/升级门禁，阻止这些目录卡被误翻为可玩或被 Deck 引用；新增 `MachineGunnerCatalogSnapshotMG2ATests` 固定该快照。
- `i18n.xlsx` 已增加 192 条 key。中文基础说明逐字保留压缩包 `cards.json.desc`，63 条升级说明保留 `known_upgrades.change`；源文件没有英文规则原文，en 列据同一压缩包的结构化字段与行为说明录入项目内英文翻译。中文来源仍是追溯依据，英文文案不表示玩法已实现或完成最终策划校对。卡牌的 CatalogOnly 状态继续由配置/规则门禁表达，而非用通用“未实现”文案替代规则。
- 作者表经工作簿重导入、唯一性/Smart String/公式错误扫描及渲染检查；`DataTables/gen.bat` 成功完成 Luban validation 和 JSON 生成。生成 `battle_tbcard.json` 手工核对为 64 张、ID `3201`--`3264`、64 张 CatalogOnly、64 组空绑定、64 个占位图、63/1 升级拆分。`dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error、12 条既有程序集版本冲突 warning。
- Unity MCP 随后恢复为单一可连接的 `TinySpire` 6000.5.5f1 实例，处于非 Play、idle 状态；只读连接/Console 探测成功。通过同一 Editor 执行 Refresh，生成 `MachineGunnerCatalogSnapshotMG2ATests.cs.meta` 且无产品编译错误；随后 `TinySpire/Build/Sync and Build All` 成功完成 Localization 导入与 Local Addressables 内容构建（约 8.341 秒），Console 明确记录整体成功。显式本地化验证也通过。
- Unity EditMode 定向任务 `0098fec0e0204ade8ef8d22a6245709e` 为 **21/21 passed**：新增机枪兵快照 4/4、卡牌目录门禁 10/10、Ironclad 目录回归 7/7。第二个相关任务 `11741e4d623947c4b7335b1de013a048` 为 **19/19 passed**，覆盖 M10 黄金基线、Hero 资源档案、配置表清单、战斗本地化和牌面配置。Console error 过滤仅显示本地化“validation passed”与 Test Runner 保存 `TestResults.xml` 的已知日志分类项，没有产品编译、运行时或 InvalidKey 失败。
- 未新增 Hero/Deck、未把卡牌放入默认战斗、未实现 Card Program、Ammo 支付、最近/随机目标、Weakness/Smoke/Burn/Oil/Power、升级实例、奖励或 Run；未修改 Queue 共享写入 seam、场景/Prefab/ProjectSettings/asmdef/HybridCLR/启动/DI，也未触碰 Targeting/Candidates/Hermes 受保护美术路径；未暂存、提交或推送。

## 2026-08-06 机枪兵 MG1 Hero 资源档案（已完成）

- 用户确认的资源规则已冻结：首个玩家回合的 Energy 为 3，不叠加当回合 `+3`；资源上限降低时当前值立即取 `min(current, max)`；默认抽牌数仍是共享的补至 5，不新增职业专属抽牌字段。
- `battle.hero.xlsx` 现为每个 Hero 声明 `initial_energy`、`max_energy`、`energy_gain_per_round`、`initial_ammo`、`max_ammo`、`ammo_gain_per_round`。当前唯一生产 Hero `1001` 明确为 Energy `3/3/+3`、Ammo `0/0/+0`；未新增 Hero `1002` 或 Deck，未来机枪兵仅由同一档案形状预留。
- `BattleSession` 在任何参与者/随机聚合前校验并冻结 `CombatantId → BattlePlayerResourceProfile`；`BattleTurnController` 是唯一重建 `PlayerTurnData` 资源事实的位置。首回合使用 `initial_*`，后续回合使用当前上限和增量的 capped 补充；`PlayerTurnData` 构造时立即裁剪当前值。回合结算顺序保持清 Block → Energy → Ammo → 卡区补至共享目标手牌数，Ammo 记录目前只供事实/后续机制读取，不制造可见 UI 步骤。
- 保留 `GameConfig.EnergyPerRound` 供旧测试入口与当前仅有 Hero `1001` 的 HUD 基线使用；生产 `BattleLifetimeScope` 已改为传递 Hero 档案映射。敌人联合事务快照同步冻结全部资源标量，避免上限、增量或 Ammo 漂移绕过首写前验证。构建入口在本地化/Addressables 前校验生成 Hero JSON 的资源约束。
- 验证：`dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error（12 条既有程序集版本冲突 warning）；Unity EditMode 新增档案/门禁 8/8、相关回归 93/93、受 Hero fixture 扩展影响的 UI 类集 27/27 均通过。`TinySpire/Build/Sync and Build All` 已完成 Luban、配置门禁、本地化导入与本地 Addressables 内容构建，Console 记录成功；详细证据见 `06_testing/2026-08-06-machine-gunner-mg1-hero-resource-profile.md`。
- 未修改或新增机枪兵 Hero、Deck、卡牌、状态、Power、UI、Scene、Prefab、素材、角色选择、奖励/Run 或受保护 Targeting/Candidates/Hermes 路径；未暂存、提交或推送。下一停止点是仅在单独授权后才开始的 MG2。

## 2026-08-06 机枪兵 MG0 设计归一化（已完成，等待玩法确认）

- 新增需求摘要 `01_requirements/2026-08-06-machine-gunner-card-design-digest.md`，把 `00_inbox/卡牌设计-机枪兵.json` 的 5 个 starter 模板、12 张实例、23 张 reward 模板、7 类状态、5 个已声明奖励升级和 18 个未声明奖励升级归入单一矩阵；原始 JSON 保持 source-only 且未改写。
- 代码/配置只读审计确认：Hero 表没有资源档案；当前每轮能量重置为全局 3、抽牌补至 5；参与者/效果只支持 Health/Strength/Block/Vulnerable；Self/Enemy 之外的目标规则、Power 归宿、Ammo、随机射击和机枪兵状态均未实现。`weakness` 已明确为独立于 `Vulnerable/易伤` 的攻击减伤状态，禁止映射到 `ApplyVulnerable`。
- 上游 `Hermes_Pegasus` 的 P-004 是当前“基础抽牌数固定”的暂定结论；用户已确认默认抽牌数为 5，故机枪兵复用现有“补至 5”基线，不新增职业专属 `draw_per_turn` 字段。MG1 的生产前置据此收敛为 R1--R2：首回合 Energy、资源上限变动时的当前值裁剪；MG1 维持当前清 Block → 补 Energy/Ammo → 补至 5 的基础顺序，Power/状态时机留后续切片。默认 Hero 1001 与 M10 基线保持不变；未确认前不写 C#，不把未完成卡接入 Deck。
- 本轮新增需求摘要并更新需求索引、实施计划和状态日志；未修改原始 JSON、C#、测试、DataTables、生成 JSON、Localization、Addressables、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、DI 或受保护美术路径，未运行 Unity、Luban、Addressables、构建或测试，未暂存、提交或推送。

## 2026-08-06 机枪兵单场战斗内容接入计划（计划就绪，未实施）

- 新增 `plans/2026-08-06-machine-gunner-card-pool-integration.md`。它把 `00_inbox/卡牌设计-机枪兵.json` 明确标为 source-only：设计稿含 5 个初始牌模板（12 张实例）、23 张奖励卡、能量/弹药、7 类状态和部分升级信息，但不是可直接导入运行时的配置。
- 计划先要求 MG0 把 28 个模板归一化为机制、时机、升级、文本、素材和未决项矩阵；机枪兵默认是新增候选 Hero，默认 Hero 1001、M10 的 3 能量/5 手牌黄金基线及当前 Deck 均不变。未完成卡不能引用进正式 Deck，也不能翻为 `Implemented`。
- 已记录的关键口径：机枪兵 `weakness` 是造成攻击伤害 -25% 后再结算格挡、每回合 -1 层，不能映射到当前目标承伤 ×1.5 的 `Vulnerable/易伤`。最近/随机/全体选择器、弹药、Smoke、Burn/Oil、Armor、Stim、Power、X 费和奖励规则仍须在 MG0 由策划确认。
- 后续 MG1--MG8 必须复用 Ironclad I5--I11 的通用执行器、目标选择、卡区、资源、升级和 Power 能力，保持 `BattleCommandQueue.Submit` 为唯一共享写入 seam；不复制卡牌执行链，不按卡名/ID分支，也不把奖励/Run/存档/角色选择提前塞进单场战斗。
- 本轮只新增计划并更新计划索引/状态日志；未修改原始 JSON、C#、测试、DataTables、生成 JSON、Localization、Addressables、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、DI 或任何受保护美术路径，未运行 Unity、Luban、Addressables、构建或测试，未暂存、提交或推送。下一独立停止点是仅文档的 MG0 需求矩阵，须先获得策划口径确认。

## 2026-08-06 STS2 v0.107.1 Ironclad 卡池 I4 成功归宿与 Tremble（已完成）

- 用户单独确认 I4 后，先经公开 `BattleCommandQueue.Submit` 固定真实 Tremble 命令顺序。精确红灯 `60927f80b92b432d95c7681759fa1e82` 为 0/1：期望 Hand→ExhaustPile，旧实现实际为 Hand→DiscardPile；最小 Turn 改动后 `f68c558591014dc285d6a1888fa8717b` 为 1/1。`TryPlayCard` 现在在首次权威写入前冻结基础 Discard/Exhaust 归宿，仍按 EnergySpent → Effect → CardMoved 结算，并复用既有 CardZones 原语；未知/Power 归宿在首写前 fail-fast。
- 生产 Tremble（3118）已改为 1 费 Enemy、`vulnerable:4006`、ApplyVulnerable 3、Exhaust，使用项目自有 en/zh-CN Smart String；生成数据红灯 `97b377dce0c34921938064049ef89c0f` 先得到期望 Implemented、实际 CatalogOnly。构建门禁另以 `bab167110fba448ea937e4e961092946` 固定 4/81 数量不变但 Tremble/Anger 身份互换的失败，旧代码只报告 3/82；身份门禁绿灯 `90bff86aca1d4225a3ad0715ae2c5297` 为 1/1。
- 三份作者工作簿经 Artifact Tool 候选检查、公式错误扫描、渲染目视、写回后重导入与再次渲染，均通过；Luban validation 与 JSON 生成成功。唯一 Unity Editor 的 `TinySpire/Build/Sync and Build All` 已同步 Localization 并重建 Local Addressables，Console 记录 Addressables 成功构建约 29.244 秒及整体完成日志；同步构建后、测试前 error 过滤为 0。最终 Console 保留 3 条“Localization validation passed”和 3 条保存 TestResults.xml 的 Test Runner 捕获记录，InvalidKey 为 0，没有产品编译或运行时错误。Tremble 继续使用 `art_placeholder` / `card-art/art_placeholder`，未生成、下载或引用新美术；缺图清单仍为 82 张，其中 Tremble 为 Implemented、其余 81 张为 CatalogOnly。
- 最终窄回归 `4892bc7cad4c42769aaef7ff4a349837` 为 3/3，相关 Turn/CardZones/Queue/展示边界/构建门禁任务 `5ba43fb9daed4a9c9e5467dfb3e69762` 为 61/61；第一次相关任务 `f3cea318acd94378bb27a2cdaf1b7b7c` 只发生 Test Runner 初始化超时，0 项开始，不是用例失败。完整 EditMode `65ba008cb21947f3bfb2da54539912af` 为 **482/482 passed、0 failed、0 skipped**；solution build 为 0 error、12 条既有程序集版本冲突 warning。
- 新增 CD-060 与验收页 `06_testing/2026-08-06-sts2-ironclad-i4-success-destination.md`。STS2 子集当前为 4 张 Implemented / 81 张 CatalogOnly，加项目自有 Strength 后生产表为 5 / 81；默认 Deck 未加入 Tremble。I4 不包含 Exhaust 飞行动画，也未修改 Queue/settlement/CardZones 公共契约、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、DI 或启动流程。本切片没有声称真实 Game View 展示 Tremble；逐卡真实 BattleScene 验收留到 I14。I5 尚未开始，必须取得新的明确确认。用户随后授权以一次本地提交收口 I0–I4 与根 `.codex_work/` 忽略规则；不推送远端。

## 2026-08-06 STS2 v0.107.1 Ironclad 卡池 I3 目录与占位素材（已完成）

- 冻结快照 `sts2-v0.107.1-23811903-59260271` 的 85 张单人战士卡已全部录入 `battle.Card`，明确排除多人专用 `DEMONIC_SHIELD` 与 `TANK`。STS2 子集为 3 张 `Implemented`、82 张 `CatalogOnly`；加项目自有 Strength 后生产表共 86 行、4 张 `Implemented`、82 张 `CatalogOnly`。目录补齐稳定外部 key、快照、类型、稀有度、Fixed/X 费用、目标、成功归宿、升级费用/说明与 `has_upgrade`，当前 Deck 仍只引用 `Implemented`。
- 未生成、下载或引用官方/候选卡图。82 张缺图卡统一复用项目既有 `Assets/Arts/Runtime/Card/Texture/art_placeholder.png`，短键为 `art_placeholder`，逻辑地址为 `card-art/art_placeholder`；当前唯一 Unity Editor 将其校验为 `Sprite / Single / no mipmap`。逐卡交付清单见 `10_communication/2026-08-06-sts2-ironclad-card-art-checklist.md`；后续 Agent 不得自行生成或下载卡图，没有用户提供或明确授权素材时继续占位。
- 精确红灯任务 `f7b8315680f54d539e70a446d989b1fa` 先得到期望 85、实际 0。最终身份/漂移/双语任务 `c3a9df29333e45448edf17447a4f84fc` 为 **5/5**，I2+I3 构建门禁 `078210dc2aad49d2894a404a29bab357` 为 **10/10**，敌人目标 fail-fast `c865f8dab8f145b8bf5666f1e5174798` 为 **20/20**，真实 Addressables Sprite 加载 `0b6e50d98b7245098c312d06839fed8e` 为 **5/5**，夹具相关回归 `b0fe67cd2f45443a82a0d51de15c2d8c` 为 **86/86**，完整 EditMode `7e7738c02c4c411294596b1e9d040324` 为 **479/479**。solution build 为 0 error、12 条既有程序集版本 warning；Luban 与 `TinySpire/Build/Sync and Build All` 成功完成。
- 新增 CD-059 与验收页 `06_testing/2026-08-06-sts2-ironclad-i3-card-catalog.md`。I3 没有实现 Twin Strike、Bludgeon 或其他新规则，没有修改 Queue、Turn、settlement、公式、Scene、Prefab、ProjectSettings、asmdef 或 HybridCLR。I4 尚未开始；它将首次修改 `BattleTurnController` 的成功归宿，必须另行确认。未暂存、提交或推送 I0-I3 工作区改动。

## 2026-08-06 STS2 v0.107.1 Ironclad 卡池 I2 CatalogOnly 构建隔离（已完成）

- 新增独立 Editor `BattleCardCatalogBuildValidator`，并接入 `TinySpire/Build/Sync and Build All` 的 Luban/Refresh/四源表清单之后、Localization 与 Addressables 之前。它拒绝 Deck 引用缺失或 `CatalogOnly` 卡、未知状态、`Implemented` 空/漂移程序、非法或缺失牌面短键，并要求 Deck/Card/Effect JSON 顶层键与运行时内嵌 `id` 一致；没有修改 Queue、Turn、settlement 或其他权威写链。
- I2 测试阶段为 `CatalogOnly` 预留占位短键 `card_art_catalog_placeholder`，且仍由现有真实 AssetDatabase/Sprite 解析器校验，不允许伪造路径；当时生产表仍为 4 张 `Implemented`、0 张 `CatalogOnly`，因此没有添加占位资产或新卡。I3 已按 CD-059 将生产标准最终锁定为项目既有 `art_placeholder`。
- 原始精确 Deck 任务 `cfaf05a194e34796b9c3f96808126cea` 为 1/1；审计追加的 Effect key/id 漂移红灯 `0107906ab3824efea6cb20e40902b798` 转绿任务为 `76cf944b4efc4ae7a1c037efcd7b9122` 1/1。最终 Luban 与 `Sync and Build All` 成功，相关任务 `e35a3b7e3f0f4bacab7c34ce9e6d0e31` 为 **102/102 passed**，真实 `card-art/{key}` 加载任务 `b6391ca3dde14ff189837a5401bcd310` 为 **1/1 passed**；BuildLayout 证明四个地址同属 `TinySpire Card Art` PackTogether 物理 bundle，并使用 `AssetBundleProvider / IAssetBundleResource`。solution build 为 0 error、12 条既有 warning，Console 无 InvalidKey。
- 第一次真实地址协程任务在 Editor 未聚焦时按 180 秒超时，并明确报告 `editor_unfocused`；相同任务在 focused/idle 后通过，没有修改超时或 ProjectSettings。新增 CD-058 与验收页 `06_testing/2026-08-06-sts2-ironclad-i2-build-isolation.md`。I2 独立停止点完成，I3 尚未开始；未暂存、提交或推送 STS2 改动。

## 2026-08-06 STS2 v0.107.1 Ironclad 卡池 I1 CatalogOnly 运行时隔离（已完成）

- 精确红灯 `Submit_CatalogOnlyCard_FailsBeforeEnergyOrCardZoneWrites` 先在 solution build 得到 `CS0234`：缺少 `cfg.battle.CardImplementationStatus`。最小实现给 `battle.Card` 增加必填 `Implemented / CatalogOnly` 状态，并让 `BattleCardPlayRules.Evaluate` 在费用、目标与 Effect 之前以 `CardNotImplemented` 拒绝目录占位卡；Queue、Turn、settlement 与公式均未修改。
- Queue seam 测试断言 typed failure、空 settlement、Queue 不 fault，并保持同一 Turn/Layout、能量、Hand/Discard/Exhaust 与玩家/敌人的 Health/Strength/Block/Vulnerable 标量；目录卡绑定真实 `Strength +9` Effect，确保测试能发现参与者内部写入。`TinySpire/Build/Sync and Build All` 后最终精确任务 `119aeec7577640109aa4173c41c2566b` 为 **1/1 passed**，相关七类任务 `9e54ef937764492ba2ef41bcdfcad930` 为 **86/86 passed**。额外完整 EditMode 任务 `ad9b7b3e47a340bba4ce38e368c8628a` 完成 461 项，但 Editor 未聚焦时两项既有 Addressables 实例化测试各超时 180 秒；服务此前明确报告 `editor_unfocused`，未报告其他失败，故未把该任务伪报为全绿或作为 I1 通过依据。
- `DataTables/gen.bat` 已成功生成 `CardImplementationStatus.cs`、更新 `Card.cs` 与 `Assets/GameData/battle_tbcard.json`；四张既有生产卡均显式为 `Implemented`，插列后的 Effect/Illustration 工作簿样式也已恢复。`CardNotImplemented` 追加在既有失败枚举末尾，不改变旧整数值。solution build 为 **0 error、12 条既有程序集版本冲突 warning**。当前唯一 Unity 6000.5.5f1 Editor 完成同步与本地 Addressables 构建，Console 明确报告 `TinySpire sync and local content build completed successfully.`；测试后的 error 过滤仅有三条 TestRunner 保存 `TestResults.xml` 的记录，没有编译、运行时或 InvalidKey 错误。
- 新增 CD-057 与验收页 `06_testing/2026-08-06-sts2-ironclad-i1-catalog-runtime-gate.md`。I1 未录入新卡、未改 Deck/Localization/Scene/Prefab/ProjectSettings/asmdef/HybridCLR 或素材地址；`CatalogOnly` 构建期 Deck/程序/牌面校验尚未实现，I2 必须从独立红灯开始。未暂存、提交或推送本次 STS2 改动。

## 2026-08-06 STS2 v0.107.1 Ironclad 卡池 I0 快照（已完成）

- 新增长期实施计划 `plans/2026-08-06-sts2-v01071-ironclad-card-pool.md` 与研究页 `04_research/2026-08-06-sts2-v01071-ironclad-card-snapshot.md`。本机 Steam public/main manifest、`release_info.json` 与 Spire Codex stable changelog/API 交叉一致：`v0.107.1`、build `23811903`、commit `59260271`；本快照固定为英文源、2026-08-06 提取。
- API 返回 87 个 Ironclad 实体；排除 `multiplayer_only` 的 `Demonic Shield` 与 `Tank` 后，单人基线为 **85 张**：Basic 3、Common 20、Uncommon 35、Rare 25、Ancient 2；类型为 Attack 37、Skill 29、Power 19。两张多人卡等待 `DEP-008`，不以单玩家状态假实现。
- 当前能忠实复用固定费用、Self/单体 Enemy、Damage/Block/Strength/Vulnerable 与普通弃牌的只有 Bash、Defend、Strike、Twin Strike、Bludgeon 五张。其余卡涉及 Effect 独立目标、多目标/随机/重复、抽牌、能量/X 费、Exhaust、Power 触发、选择、生成、升级、条件、状态或 Run 权威边界；完整重叠矩阵已记录在研究页。
- 现有空 `effect_bindings` 会成功扣能量并弃牌，因此不能直接把未实现目录行塞入生产 Card 表。I1 先增加 `Implemented / CatalogOnly` 运行时门禁，I2 再增加 Deck/程序/牌面构建隔离，均不改 Queue、Turn 或 settlement；I3 才安全录入 85 张目录。I4 起需要修改 Turn/settlement，必须依用户此前停止边界另行报告并确认。
- 仓库不提交 STS2 官方卡图、二进制、解包结果或完整英文规则文本镜像；目录使用结构化机制事实，双语说明采用项目自有表述，牌面只使用 TinySpire 自有/占位素材并继续走 Addressables/AssetBundle。I0 只修改文档，未改 DataTables、生成文件、Localization、代码、Scene、Prefab 或 Addressables 内容，未暂存、提交或推送。

## 2026-08-05 M9C 伤害飘字渲染层级修正（已完成）

- 用户实机所见“没有伤害数字”已定位为局部表现层排序缺陷，而不是伤害结算、Queue、Turn 或 settlement 未产生反馈：`BattleScene` 的 HUD Canvas 使用 `Default/0`，角色 SpriteRenderer 使用 `Character/0`，位于角色中心的 `FeedbackAnchor` 原先没有局部 Canvas，因此已创建并播放的纯字符飘字会被角色精灵遮住。
- 精确渲染红灯 `CreateCombatFeedbackTween_HealthLossRendersAboveCharacterSprite` 通过真实 `ParticipantHudView.CreateCombatFeedbackTween`、正式飘字 Prefab、Screen Space-Camera Canvas 与重叠角色 SpriteRenderer 渲染到 RenderTexture；修复前任务 `0ca6b14458944965b205982acddddd9d` 与复跑 `8a125a28e0074e4392f164d3bfeb00c7` 均为 0/1，角色中心可见红色字形像素为 0。最小修复只给 `ParticipantHudView.prefab/FeedbackAnchor` 增加 `overrideSorting=true` 的局部 Canvas，使用既有 `Character` 层、order `1`，不加 GraphicRaycaster；HUD 根与纯字符飘字 Prefab 仍无额外 Canvas。
- 修复后渲染测试任务 `a230c1024de14eb68c2bf6a4384cd44c` 为 1/1；渲染与 Prefab 合约组合任务 `1a0a8627c1444029a2918d69b8b6bfe4` 为 2/2；最终相关五类回归任务 `3f1f21cba5424f2783f5b6ac1cb3af92` 为 42/42；完整 EditMode 任务 `1023abd0836f475db43e7e5ce62507ca` 为 **460/460 passed，0 failed，0 skipped**。`dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 **0 error、12 条既有程序集版本冲突 warning**。
- 当前唯一 Unity 6000.5.5f1 Editor 已执行 `TinySpire/Build/Sync and Build All`；Addressables 本地内容构建成功（约 50.923 秒），输出 `Library/com.unity.addressables/aa/Windows/settings.json` 与最新 build layout。正常 Bootstrap 中真实 End Action 按钮 listener 令玩家生命从 30 降到 24、Queue 无 fault；为取得稳定前景画面，另在同一真实运行时 HUD 上调用既有内部反馈 seam 并暂停实际 tween，玩家与敌人中心均清晰显示红色 `-6`。该稳定截图只证明前景排序，不冒充自然结算时序；诊断完成后 `numbers=0 / activeTweens=0 / playingTweens=0`，权威生命恢复为玩家 30、两名敌人各 20，Console 为 0 Error / 0 Warning，Play Mode 已退出。
- 未修改 Queue、Turn、settlement、公式、Combatant/CardZones/Intent 权威事实、Presenter 映射、BattleScene、DI、DataTables、Localization、ProjectSettings、asmdef 或 HybridCLR；没有新玩法、新输入锁、第二动画队列或第二份权威状态。完整证据见 `06_testing/2026-08-05-m9-post-validation-bug-triage.md` 与 M9C 验收页的后验修正说明；未暂存、未提交、未推送。

## 2026-08-05 DOTween Pro 仓库净化、NOTICE 补全与远端历史更新（已完成）

- 从索引移除 `DOTweenPro/`、`DOTweenPro Examples/`、对应目录 Meta 与 `readme_DOTweenPro.txt`/Meta 共 46 个跟踪项，并在 `TinySpire/.gitignore` 添加六条精确规则；免费 `DOTween/` 与 `DemiLib/` 继续跟踪 307 项。新增 CD-056，并修订 CD-003，明确 Pro 只允许持证开发者本地安装，不是 Clone、构建或运行前置条件。
- `THIRD-PARTY-NOTICES.md` 已从错误的“全部 MIT”概括补为真实分类：免费 DOTween 自定义许可、Luban 及其 MIT/BSD/Apache/MPL 内含依赖、UPM/NuGet、Unity 专用许可、模板、子模块和本地专有工具；`Tools/Luban/NOTICE.md` 已指向根依赖清单。
- 本地 Pro 临时移出后，唯一 Unity 6000.5.5f1 Editor 全量重编译成功，完整 EditMode `5b817700afff40f1a4928b2e78f01a25` 为 459/459，通过独立正常 Bootstrap 进入 BattleScene、唯一 `BattleLifetimeScope` 与 Console 0 Error / 0 Warning。恢复后 50 个物理文件与备份 SHA-256 全部一致，Editor 再次编译回到 idle、Console 0 Error / 0 Warning。
- 清理提交旧 SHA `bec2f892c8f38f995046e8f11f088e0921b5c2e2` 已生成本地新历史 `3c831013046e9f5fb30097701533b66c80abeb0e`；独立镜像重放得到相同 HEAD，tip tree 与过滤前同为 `f8003ddb36b14f79fc5c2e68ddfbd0f937043887`。镜像 81 个提交中目标路径可达对象/路径提交均为 0、只有 `main`、无 Tag，`git fsck` 通过；本地完整 bundle 与逐文件备份保存在已忽略的 `.codex_work`。
- 首次使用旧远端 SHA 的精确 `--force-with-lease` 推送被 GitHub `GH008` 原子拒绝；缺失对象精确为一张 M10D 证据图、三张 Hermes scene candidates 与一张 Battle Candidates 图，均非 Pro。用户随后明确授权只上传这五个已核验对象，同时继续禁止 Pro、`--all`、`.gitattributes` 变更与 LFS 清理；精确 `--object-id` 上传完成 5/5、12 MB。
- 再次确认远端仍为旧 SHA 后，只将 `refs/heads/main` 从 `3e7b8e5100015686a3c12260155e9b7076456a26` 精确 lease 强制更新到净化后的 `c391f37036f6eda60a49adb725e5418868743693`。回读时 `HEAD`、`origin/main` 与 `ls-remote` 完全一致，六个 Pro 目标路径的远端可达对象/路径提交均为 0；免费 DOTween/DemiLib 仍为 307 项，本地 Pro 50 个文件与备份哈希仍全部一致。完整证据见 `06_testing/2026-08-05-dotween-pro-repository-sanitization.md`。

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
