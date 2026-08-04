---
title: M9F 阶段横幅、胜负面板、重开与退出
page_type: testing
lifecycle: active
date: 2026-08-02
updated: 2026-08-04
status: passed
scope: StartBattle、阶段横幅、BattleEnded、Localization、同种子重开、退出应用、Development Player 与范围审计
plan: ../plans/2026-08-02-m9-sts-feedback-outcome-restart.md
status_source: ../SESSION_LOG.md
---

# M9F 阶段横幅、胜负面板、重开与退出

## 当前结论

M9F 已通过。StartBattle 仍是每命令唯一的 `CommandPrelude`，先播放一次战斗开始覆盖层，再进入冻结 settlement 的原 `Order`；玩家/敌人横幅只由 `BattlePhaseChanged` 的真实 phase 变化派生，`EnemyAction → EnemyAction` 的行动者交接不重播。`BattleEnded` 仍是同一命令时间线的末端步骤：表现 adapter 只在该步骤临时调用同程序集 internal `BattleTerminalRules`，立即映射 Victory/Defeat key，不公开规则、不注册新 seam，也不保存 outcome。

终局面板在全部前序数字、抖动、死亡与隐藏反馈之后稳定显示；战斗输入锁和 Restart/Exit 按钮锁均为局部表现状态，不修改 Queue、Turn、Combatant、Intent、CardZones 或终局事实。Restart 通过现有 `SceneFlowService.LoadSceneWithLoadingAsync(GameStartupOptions.InitialSceneAddress)` 重载同一 BattleScene，继续使用 Inspector 的 encounter/deck/seed；Exit 固定调用应用退出，不引入 MainMenu、RunState 或新 Scene。

## 测试先行与红绿证据

| 契约 | 红灯 | 最小实现 | 最终绿灯 |
|---|---|---|---|
| StartBattle 与阶段横幅顺序 | 新测试先要求覆盖层严格早于首个 Player banner，旧 factory/HUD 没有对应 cue | `BattleFlowFeedbackTweenFactory` 只消费 frozen Prelude/Phase step；HUD 复用一个非权威覆盖层和 tint | `Adapter_StartBattle_PlaysOverlayThenPlayerBannerBeforeSingleCompletion`、覆盖层与横幅测试进入 7/7 聚焦结果 |
| phase-only banner | 新测试先要求 PhaseBefore 与 PhaseAfter 相同不产生横幅，旧 adapter 无该过滤 | 只在 `PhaseBefore != PhaseAfter` 且进入 PlayerAction/EnemyAction 时创建 cue | 玩家/敌人真实切换播放，EnemyAction actor 交接不重播 |
| terminal outcome 即时派生 | 新测试先要求 BattleEnded 从当前 terminal facts 映射文案且非终局/非法事实抛错，旧 adapter 没有 outcome step | adapter 在 BattleEnded 步骤临时调用 internal `BattleTerminalRules` 并立刻只保留 localization key | `BattleEnded_MapsCurrentTerminalOutcomeBeforeCompletion` 与非法事实测试通过；无 outcome 字段/缓存 |
| 稳定终局面板与一次按钮 guard | 新测试先要求 cue 完成后面板保持、取消前不残留、Restart/Exit 只能有一个胜者 | HUD 只持有本地稳定面板和一次性 scene-action guard；取消/销毁幂等释放 | `BattleOutcome_CancelBeforeStableEnd_HidesPanelAndReleasesPointerLock` 与双按钮 guard 测试通过 |
| 同 Scene/seed 重开与应用退出 | 新测试先锁定现有 scene address 与薄退出 seam；旧 HUD 无接线 | Restart 调现有 SceneFlow，Exit 调薄 `Application.Quit` seam；不写战斗事实 | 自动 seam、Editor 实际按钮事件链、连续两次生产重开与外部 Player 自然退出均通过 |
| 七个正式本地化 key | 表中旧数据不存在七个 key | 只编辑 `DataTables/Datas/i18n.xlsx`，通过既有 Luban/Localization/Addressables 管线生成 | 7/7 语言测试及 zh-CN/en 生产切换均通过 |

## 自动验证与静态构建

| 检查 | 结果 |
|---|---|
| M9F 首轮 factory/HUD/Prefab 聚焦 | **7/7 passed，0 failed，0 skipped**；Unity 任务 `ba6dab7440e94380854cfdd64acdd777` |
| M9F adapter、HUD、Session、SceneFlow、Localization 与相关回归 | **111/111 passed，0 failed，0 skipped**；Unity 任务 `31ec7f5e33f644d2b0ea2735d343f106` |
| 七个 Localization key 精确值 | **7/7 passed**；Unity 任务 `4cb0e39ecaa649c3aff348f0d1482dd9` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings**；均为既有 Unity/R3/UniTask 依赖程序集版本冲突 |
| Luban、Localization 与完整本地构建 | Luban、Localization 同步和 `TinySpire/Build/Sync and Build All` 均成功；`AddressableAssetGroupSortSettings.asset` 构建前后 SHA-256 同为 `850CAB9AFB7D09C2DA11FF85B9B841BDBE218EBB0C605B96DA36428BA1217E36` |

## 七个正式文案与生成范围

| key | en | zh-CN |
|---|---|---|
| `battle.ui.battle.start` | Battle Start | 战斗开始 |
| `battle.ui.turn.player` | Player Turn | 玩家回合 |
| `battle.ui.turn.enemy` | Enemy Turn | 敌人回合 |
| `battle.ui.result.victory` | Victory | 胜利 |
| `battle.ui.result.defeat` | Defeat | 失败 |
| `battle.ui.action.restart` | Restart | 重新开始 |
| `battle.ui.action.exit` | Exit | 退出 |

编辑源 `DataTables/Datas/i18n.xlsx` 的最终 SHA-256 为 `C2321A31092C7AF8D84A897DB55A5EA2C0618E43D57A0F0AA2AF4A542CE0B981`。生成差异只落到对应 Battle Cards Shared Data、en、zh-CN Localization 资产和既有本地 Addressables 输出；没有修改其他 DataTables、生成战斗 C#、`Assets/GameData/` 战斗 JSON、稳定 Scene 地址或战斗配置。

## Bootstrap、时序与终局生产证据

- StartBattle 覆盖层只出现一次，并严格先于首个 Player Turn banner；第二次重开切到 en 后仍观察到 `Battle Start → Player Turn`。覆盖层期间只有 Turn HUD/Hand 的系统指针入口被局部阻断，直接 Queue seam 与排序没有变化。
- PlayerAction/EnemyAction 只在 phase 真正变化时播放；同一 EnemyAction 内从左敌交接到右敌不重播敌人横幅。
- 失败路线的连续帧为伤害数字 → 实际生命损失 → fatal fade → world View/完整 HUD 隐藏 → Defeat；首敌致死后剩余敌人不行动。胜利路线经真实卡牌拖拽证明最后伤害/死亡完成后才显示 Victory。
- 终局后再次命中旧 End Action 坐标不会分配新 authority sequence；面板按钮仍可经 EventSystem 操作。
- Restart 连续点击两次均经过 Loading；每次旧 Session/Queue/HUD 被销毁并创建新实例，authority sequence 从新场次起点开始，HP、Intent RNG、Hand/CardZones 全部重置，同一 Inspector 事实始终为 `1001:5001:5`，没有旧 Tween/订阅/HUD 残留。
- zh-CN 与 en 的七个运行时文案均与表值一致。Editor Exit 使用用户授权的跨帧 InputSystem 输入，经实际 `InputSystemUIInputModule → EventSystem raycast → PointerDown/PointerUp/PointerClick → Button.onClick` 命中 ExitButton；记录为 `observed=1`。Editor 中 `Application.Quit` 的预期 no-op 只证明接线，不冒充 OS 退出。

## 仓库外 Development Player 退出证据

用户明确允许本次验证 Player 不使用热更新。构建前只在 Editor 内存临时把 `HybridCLRSettings.Instance.enable` 设为 false，不调用 Save；构建后恢复为 true，并恢复 `UNITY_IL2CPP_PATH` / `_CL_`。磁盘 `ProjectSettings/HybridCLRSettings.asset` SHA-256 始终为 `22BD4714FC1BC8B093457FFFE2818D99AB733BF45374BCE1E81CBE8DC86F1FE8`，没有 HybridCLR、ProjectSettings、asmdef 或构建架构 diff。该路径使用 Unity 内置 IL2CPP；没有运行 `HybridCLR/Generate/All`。包的 preprocess 会使 ignored `HybridCLRData/AssembliesPostIl2CppStrip/StandaloneWindows64` 缓存失效，本次没有无快照猜测恢复或清理，后续正式热更新构建应按其流程重新生成。

| 证据 | 结果 |
|---|---|
| Development build | Unity 任务 `build-79a93a95b7` 成功，0 errors、628 build warnings、271.919s、1952.83 MB；仅含 `Assets/Scenes/BootstrapScene.unity`，输出位于仓库外 `%LOCALAPPDATA%/Temp/TinySpire-M9F-NoHotUpdate-20260803-1645/` |
| 可执行文件 | `TinySpire.exe` SHA-256 `51ACC9182CC45B00A3838E7C633D056330C0B7DD4F396502EA75E111367C5F3D` |
| 真实失败路线 | PID `43692`、可见 HWND `83888518`、client `1600×900`；Windows `SendInput` 三次命中实际 End Action，三次 Move/Down/Up 均返回 1，逐轮进入 Round 3 后出现稳定 Defeat 面板 |
| 终局截图 | 仓库外 `PlayerDefeatExitCode0-20260804-040032.png` 清楚显示“失败 / 重新开始 / 退出”及 Development Build 标识 |
| 真实 Exit | Windows `SendInput` 在 screen `(803,801)` 命中同一 PID 的 Exit；Move/Down/Up 均为 1，点击时间 `2026-08-03T20:00:49.8969653Z` |
| 自然进程结束 | 保留的原生进程句柄得到 `WaitForSingleObject=0`、`GetExitCodeProcess=true`、`ExitCode=0`；400ms 后 PID 不存在；脚本 `ForceKillUsed=false`，没有 `Stop-Process`/`taskkill` 路径 |
| Player log | `PlayerExitCode0-20260804-035845.log` 无 InvalidKey、VContainer、Addressables 或未处理异常，并完成 Physics/InputSystem/CodeReload 正常 shutdown；Development Player 在退出阶段报告一条 JobTempAlloc remaining-allocation 警告，因此本页不声称 Player log 为零 warning |

启动脚本首次因 PowerShell 保留变量 `$PID` 命名冲突在任何点击前停止；目标 Player 保持运行。最终证据附着并复用同一 PID，没有启动第二个 Player，也没有强制结束进程。

## 范围与权威边界

M9F 生产改动只位于既有 `TinySpire/Assets/Scripts/UI/Battle/**`、对应 M9 Editor 测试、`BattleTurnHud.prefab`、`DataTables/Datas/i18n.xlsx` 及其既有 Localization/Addressables 生成内容。没有修改 Queue、Turn、settlement、continuation、fault、Effect 公式、目标、状态时机、终局规则、`BattleLifetimeScope`、`BattleScene.unity`、GameData 战斗 JSON、ProjectSettings、asmdef、HybridCLR 磁盘设置、启动/DI、Run、网络、多人、奖励或 MainMenu；没有 outcome 镜像、第二 completion、事件总线或动画队列。

`packages-lock.json` 的外部差异继续排除。Hermes art index、Hermes scenes candidates、Runtime Battle Candidates 及其 Meta 均未读取、引用、修改、移动、清理、回退或暂存。未 commit、未 push。

## 停止点判定与后续

M9F 的阶段横幅、胜负面板、七个正式文案、同 Scene/seed 连续重开、Editor 按钮接线和仓库外 Player OS 退出均已具备自动与生产证据；Luban、Localization、完整本地 Addressables、Bootstrap 双 locale 和范围审计完成。M9F 停止点完成。

下一步只进入 M9G：重跑 M9 定向、M2～M8 回归、全量 EditMode、串行 solution build、最终 Local Content、Bootstrap/五宽高比/真实指针/胜败/重开/退出/取消，以及 Standards/Spec 双轴复审。本文不把这些最终重跑提前写为通过。
