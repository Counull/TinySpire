---
title: Codex 实施 Prompt 路 TinySpire BattleScene M10
page_type: handoff
lifecycle: active
created: 2026-08-05
updated: 2026-08-05
companion_plan: 2026-08-05-m10-battlescene-conformance.md
status_source: ../SESSION_LOG.md
---

# Codex 实施 Prompt 路 TinySpire BattleScene M10

复制以下内容到新会话：

```text
/goal

完成 TinySpire BattleScene M10：配置 fail-fast、默认数值与双语内容黄金基线、确定性/帧率/生命周期回归，以及交付级验证。唯一实施计划是 `Docs/Copilot_Daedalus/plans/2026-08-05-m10-battlescene-conformance.md`；严格串行执行 M10A → M10B → M10C → M10D。每个切片必须先有精确红灯或可观察失败，再做最小实现、相关自动回归、文档同步和独立停止点，禁止一次完成多个切片。

开始时不要改文件。完整阅读根 `AGENTS.md`、唯一 M10 计划、`Docs/Copilot_Daedalus/ROADMAP.md` 的 M10、`DEPENDENCIES.md`、`SESSION_LOG.md` 当前条目、`CODE_DECISIONS.md` 的 CD-013/相关配置决策，以及 M9 最终验收。随后报告：
1. `git rev-parse HEAD`；
2. `git status --short` 的 tracked/untracked 基线；
3. 当前唯一 Unity Editor/Play Mode/Console 状态；
4. M10A 的精确红灯测试、预计文件和停止条件。

永远保护并排除：
- `Docs/Copilot_Daedalus/07_retrospective/README.md`；
- `Docs/Hermes_Pegasus/art/asset-index.md`；
- `Docs/Hermes_Pegasus/art/assets/art-style/scenes/candidates/**`；
- `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates.meta`；
- `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates/**`；
- `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/**` 与其 Meta；
- `TinySpire/Packages/packages-lock.json`、`TinySpire/.codex_work/` 及其他无关工作区改动。

不得清理、回退、移动、引用或暂存上述路径；不得启动第二个 Unity Editor、结束用户的 Unity/Git 进程、删除锁文件或清理 Library/Temp。所有新函数写中文注释。需要新 Meta/Prefab 时优先用当前唯一 Unity MCP；只有修改 DataTables、Localization 或可寻址内容时，才按项目规则运行 Luban 与 `TinySpire/Build/Sync and Build All`。

M10A 只处理配置原子性与表清单 fail-fast：ConfigService 不能在 game-config 或必需表失败后静默回退默认值；失败不能发布半成品；手写表清单必须有生成或构建期漂移校验。不要提前改 Bootstrap Scene、表格、Localization 或战斗规则。

M10B 才把 M10A 的 typed failure 接入最小可见 Bootstrap 失败路径，并把默认 5/3、5×Strike/4×Defend/1×Bash、6/5/8/2、30/20 和 en/zh-CN 文本做成配置驱动的黄金基线。若修改表格或 Localization，必须完成生成、同步、本地 Addressables、稳定地址和 Bootstrap 成功/失败双路径验证；不要增加新牌、新敌人、状态、Run 或 MainMenu。

M10C 只建立从 `BattleCommandQueue.Submit` 和只读 Queue/Turn/BattleSession/CardZones 取证的确定性、30/60/120 FPS 与生命周期回归。不能改 Queue/Turn/settlement 公共契约、存第二份权威状态、增设全局输入锁或新动画队列。需要改这些边界时立即停止。

M10D 只汇总交付级验证和可重复性能基线。没有用户给出的设备、帧时间或分配预算时，报告环境和回归差异，不做猜测性性能重构，也不把“主观流畅”写成性能通过。最终验证必须区分计划、静态检查、自动测试、Addressables/Bootstrap、真实 Game View 和 Console 证据。

任何时候如需改 Queue/Turn/settlement/公式、公开新的权威写入口、保留第二份 Hand/CardZones/Combatant/Intent 事实、引入 Run/new seed/多人/网络/Exhaust/新玩法、重构 DI 或场景启动、修改 ProjectSettings/asmdef/HybridCLR，或验证被用户的 Editor/外部环境阻塞，立即停止并报告文件、风险、回滚方式及所需确认。未经新的明确授权，不暂存、不提交、不推送。
```

本提示词只授权按计划实施 M10；不构成对提交、推送或计划外重构的授权。
