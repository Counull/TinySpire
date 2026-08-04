---
title: M9G 全量验证、真实交互、Player 退出与双轴复审
page_type: testing
lifecycle: active
date: 2026-08-02
updated: 2026-08-05
status: passed
scope: M9A～M9F 全量回归、Addressables、Bootstrap、真实 Game View、仓库外 Development Player、范围审计与 Standards / Spec 复审
plan: ../plans/2026-08-02-m9-sts-feedback-outcome-restart.md
status_source: ../SESSION_LOG.md
---

# M9G 全量验证、真实交互、Player 退出与双轴复审

## 当前结论

M9G 已通过，M9 完成。自动测试、串行 solution build、最终 Local Content、Bootstrap、真实 Game View、五种宽高比、胜利/失败、连续重开、取消/立即完成和仓库外 Development Player 退出均已取得证据。Standards 首轮发现的 transient 构造异常清理与缺失长期决策记录均已修正并完成全量重跑；末轮 Standards / Spec 均为零 finding，M3E/M9 已完成，唯一计划与配套交接页已归档。

## 测试先行修正与自动验证

Standards 首轮指出：PlayCard Prelude 已把离手卡变为 transient 后，如果后续 cue factory 同步抛错，原 lease 只绑定在尚未返回的 Tween 上，runner 无法清理该 transient。新增 `HandCardMotionTests.PlayCardPrelude_LaterCueBuildThrows_ReleasesDetachedTransient` 先取得红灯：任务 `1c2d50ad7851429c8703704859b79771` 证明异常后 transient map 非空；最小修正让 Prelude 与 Hand→Discard lease 共享同一个幂等 `ReleaseTransientCard`。随后单项 **1/1 passed**（任务 `ef9ed6e142e7497397b337c7beee832a`），runner/card-motion/hand 相关 **24/24 passed**（任务 `2ab22171dc47491cb108b4b71ebd43ef`）。

| 检查 | 结果 |
|---|---|
| M9 定向 EditMode | **160/160 passed，0 failed，0 skipped**；任务 `d24881832cd74f3fb9e08b3e5c669fd1` |
| M2～M8 回归 | **262/262 passed，0 failed，0 skipped**；任务 `bae762012c7b4eec9304b4676b65b8d1` |
| 最终全量 EditMode | **423/423 passed，0 failed，0 skipped**；任务 `59ab79fab40944e6a480047f5366e548` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings**；均为既有 Unity/R3/UniTask 依赖程序集版本冲突 |
| 最终 Local Content | `TinySpire/Addressables/Build Local Content` 成功，耗时 **20.766s**；`catalog.hash` 为 `0f333c04c6f20921aab45e7c6bf9e827` |
| Addressables 稳定地址 | `TinySpire Scenes.asset` 中 BattleScene 仍为完整 `Assets/Scenes/BattleScene.unity`；`catalog.bin` SHA-256 `3CC03819D1DE0BF6B9D0AEBE68F94494C20193360BC14613BC13F6185C3D20A6`，`settings.json` SHA-256 `2BF90AD4013288D5EC0B22D779D5C205B5D12F87513A130855BBBB25D0857FEB` |

## Bootstrap 与真实 Game View

- 全程复用唯一 Unity 6000.5.5f1 Editor。最终清空 Console 后从 `Assets/Scenes/BootstrapScene.unity` 进入 Play，生产启动链加载 `Assets/Scenes/BattleScene.unity`；停止后恢复 BootstrapScene。最终 Console 只有 `game-config.json 已加载。`，Error/Warning 为 **0/0**。
- 真实系统指针与生产 `InputSystemUIInputModule` / EventSystem 链覆盖 `16:7`、`16:9`、`16:10`、`16:11`、`16:14`。每种比例均验证目标箭头、合法/悬停高亮、卡牌 focus 与稳定 base pose，未以直接 listener 调用冒充系统指针。
- 出牌与多轮卡区运动使用连续帧 `m9g-cardplay-00..07.png`、`m9g-round-00..05.png`，并以只读 Hand/CardZones/Turn/Queue 事实证明 PlayCard Prelude 严格早于 settlement Order 0，弃牌、抽牌、重洗不伪造手牌。
- fatal 失败顺序由 `m9g-fatal3-00-hp-zero.png` 至 `m9g-fatal3-04-stable.png` 连续记录：0 HP → 过渡 → world View/HUD 隐藏 → Defeat。胜利顺序由 `m9g-victory-00-all-enemies-zero.png` 至 `m9g-victory-04-stable.png` 连续记录：敌人归零 → 死亡过渡/隐藏 → Victory；终局旧 End Action 不再分配权威序号。
- Restart 通过真实按钮连续执行两次，每次均经 Loading 创建新 Session/Queue/HUD，Inspector encounter/deck/seed 保持 `1001:5001:5`，HP、Intent RNG、Hand/CardZones 和 authority 从新场次起点重置，无旧 Tween、订阅或终局面板残留。
- 立即完成由真实卡牌拖拽后同帧收口并验证重复 complete 幂等；取消路线在 cue 中途销毁 Scene/scope，再创建新 scope，旧 Tween/completion 没有迟到。对应帧为 `m9g-immediate-01-after.png` 与 `m9g-cancel-00-mid-cue.png`。
- Editor Exit 由薄 seam 自动测试和真实系统指针按钮点击共同证明接线；Editor 中 `Application.Quit` 的预期 no-op 没有被冒充为 OS 进程退出。

## 仓库外 Development Player 退出证据

常规 HybridCLR Development build 先按现有配置尝试，因 `[CheckSettings] MethodBridge.cpp DEVELOPMENT flag not found. Please run HybridCLR/Generate/All` 停止；本轮没有越权运行 `HybridCLR/Generate/All`。用户随后明确授权只临时修改当前 Editor 内存：`HybridCLRSettings.enable=false`，清空进程环境 `UNITY_IL2CPP_PATH` 与 `_CL_`，构建后精确恢复，且不保存 ProjectSettings。

| 证据 | 结果 |
|---|---|
| Development build | 任务 `build-5c4c9005fe` 成功，**0 errors、488 build warnings、330.1110362s、1952.83 MB**；仅含 `Assets/Scenes/BootstrapScene.unity`，输出 `C:/Users/Lxxr/AppData/Local/Temp/TinySpire-M9G-NoHotUpdate-20260805-012421/TinySpire.exe` |
| 设置恢复 | 构建前后磁盘 `HybridCLRSettings.asset` SHA-256 均为 `22BD4714FC1BC8B093457FFFE2818D99AB733BF45374BCE1E81CBE8DC86F1FE8`；Editor 内存 enable 与两个环境值已恢复，`EditorUtility.IsDirty=false` |
| 二进制 | `TinySpire.exe` SHA-256 `51ACC9182CC45B00A3838E7C633D056330C0B7DD4F396502EA75E111367C5F3D`；`GameAssembly.dll` SHA-256 `1CCBB505DBB5BA98E656D90DBE00777D4022B00797C257BFE12B4E0A50807541` |
| 首次系统弹窗 | 首次点击被 Windows Firewall 提示遮挡；只以真实 `SendInput` 点击“取消”，未授予或改变防火墙策略，随后复用同一 Player PID `45720`，没有启动第二个 Player |
| 真实失败路线 | 同一 PID/client `1600×900` 经实际 End Action 坐标点击进入 Round 3 并显示稳定“失败 / 重新开始 / 退出”；最终截图 `TinySpire/.codex_work/m9g_runtime/m9g-player-reviewfix-defeat-final.png` |
| 真实 Exit | `2026-08-04T17:35:59.5668110Z` 以 Windows `SendInput` 点击 screen `(803,800)`；Move/Down/Up 均为 1 |
| 自然进程结束 | 点击前已持有原生进程句柄；`WaitForSingleObject=0`、`GetExitCodeProcess=true`、`ExitCode=0`，PID 随后不存在，`ForceKillUsed=false` |
| Player log | `M9G-Player.log` SHA-256 `94F61EED5B96E1E60419B94EA054FB5848605D0382204CD9BA2E79D1070FED34`；无 InvalidKey、VContainer、Addressables 或未处理异常，并完成 Physics/InputSystem/CodeReload 正常 shutdown；退出阶段有两行同一 Development JobTempAlloc remaining-allocation 警告，故不声称 Player log 零 warning |

Player 构建自动生成的 `DefaultVolumeProfile.asset`、`UniversalRenderPipelineGlobalSettings.asset`、`ProjectSettings.asset`、`UnityConnectSettings.asset` 序列化噪声均在确认其为本次构建时点新产生后按精确 patch 恢复；首次失败构建产生的两份 PerformanceTest JSON/Meta 也按精确目标移除。最终 `ProjectSettings`、`Assets/Settings` 与 `Assets/Resources` 无 diff；没有使用 `git checkout`、清理 Library/Temp 或结束用户进程。

## Standards / Spec 双轴复审

固定比较点为 M8 代码基线 `6545640963e3f184bcd7915706e87bea4a142afa`。两轴读取完整 M9 tracked/untracked 实现、测试、Prefab、Localization 与 Daedalus 文档，同时明确排除 Hermes/Candidates、`packages-lock.json` 和 `.codex_work` 证据目录。

Standards 首轮有两项 P2，均已关闭：一是上文的后续 cue 构造异常 transient lease 泄漏，已测试先行修正并完成定向/相关/全量重跑；二是 M9 长期表现边界尚未写入 `CODE_DECISIONS.md`，已新增 CD-049。Spec 首轮对生产规格、行为和 scope creep 为零 finding；当时 M9G 验收页与最终归档尚在执行，未被提前写成通过。

末轮 Standards 复核为 **0 Hard / 0 Judgement finding**；末轮 Spec 复核为 **0 Hard / 0 Judgement finding**。首轮两项 Standards P2 与当时仍在执行的文档收口均已关闭，未引入新 finding。

## 范围与工作区保护

- M9 生产改动只落在计划列明的 `TinySpire/Assets/Scripts/UI/Battle/**`、M9 Editor 测试、四个既有 Battle Prefab、一个飘字 Prefab、`DataTables/Datas/i18n.xlsx` 及对应 Localization/Addressables 内容；`BattleLifetimeScope` 无改动。
- 未修改 Queue、Turn、settlement、continuation、fault、Effect 公式、目标/状态/终局规则、`BattleScene.unity`、其他 DataTables、生成战斗配置、GameData 战斗 JSON、ProjectSettings、asmdef、HybridCLR 磁盘设置、启动/DI、Run、网络、多人、奖励或 MainMenu；未新增 outcome/Hand/CardZones/Combatant/Intent 镜像、第二 completion、事件总线或动画命令队列。
- `packages-lock.json` 是外部基线差异。Hermes art index、Hermes scenes candidates、Runtime Battle Candidates 及其 Meta 未用作实现输入，未被生产/Prefab/Localization 引用，也未修改、移动、清理、回退或暂存。
- 未 commit、未 push。最终范围审计确认暂存区为空，Battle domain、BattleScene、ProjectSettings/Settings/Resources、GameData、HybridCLR、asmdef、启动/DI 与正式 Targeting 美术均无 diff。定向 C#/Markdown `git diff --check` 通过；全局检查只命中 Unity 自动序列化的空 `m_Name:` / `m_Text:` 标量。

## 停止点判定

M9A～M9G 已按独立停止点串行完成。DEP-003/004 已由 M9D/M9E 解决并在 M9G 完成最终复验；DEP-007 仍保持 open，连续重开只复用同一 Inspector seed，不引入 RunState/new seed。M3E 与 ROADMAP M9 完成；本计划、Goal 与启动 Prompt 已归档。未经用户明确确认仍不 commit、不 push。
