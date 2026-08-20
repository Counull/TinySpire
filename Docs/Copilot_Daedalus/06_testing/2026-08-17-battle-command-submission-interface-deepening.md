---
title: BattleCommandQueue 提交接口深化验收
page_type: testing
lifecycle: active
date: 2026-08-17
scope: Battle command submission interface only
status_source: ../SESSION_LOG.md
source: ../plans/2026-08-17-battle-command-submission-interface-deepening.md
---

# BattleCommandQueue 提交接口深化验收

## 1. 结论

本次架构维护在当前唯一 Unity 6000.5.5f1 Editor 中通过编译、M8B 11/11、相关聚合 116/116 与完整 EditMode 953/953。生产调用者现在只依赖 `BattleCommandQueue.Submit` 和只读 `Lifecycle`；旧 concrete coordinator 预注册顺序协议已从 runtime driver、Turn HUD、Hand 与普通测试中移除。

本记录只证明代码与 EditMode 合同。没有运行 PlayMode、Player build、Addressables 或人工 BattleScene smoke；没有资产、配置或场景变化。

## 2. RED 与迁移证据

| 阶段 | 结果 | 证明 |
|---|---|---|
| lifecycle 合同 RED | Editor compile 出现两条 CS1061：`BattleCommandLifecycleEvent` 缺少 `Command` | 旧 lifecycle 只有 type/submitter，UI 无法在 Queued 阶段从 Queue 自身识别具体卡牌/行动 |
| Queue 内部注册首轮 | M8B 任务 `a2c8fb68312c45308616bed8d5ee90db` 失败，旧 helper 报“同一命令引用只能存在一个尚未提交的预注册句柄” | Queue 已收回注册后，测试仍执行旧外部协议会被确定性暴露 |
| 迁移补强 | M8B 任务 `c38f967bda5549a7ae4c56b66b28b183` 为 10/11，唯一失败是 fault 拒绝用例仍显式预注册 | 剩余旧协议被定位到单一测试，不以兼容层掩盖 |
| M8B GREEN | `bb558ee7356b4374b163d37c0a72921c`：11/11 passed | 结构性拒绝零 lifecycle；同一命令 Queued/终态、callback 非重入、continuation、屏障与 fault 全部保持 |

## 3. 最终自动化

- 相关聚合 job `aa89d8ef1f0742faa18971784753286a`：**116/116 passed**、0 failed、0 skipped。覆盖 Queue、scheduling core、presentation adapter、participant/HUD routing、Hand selection/target focus/playability、enemy continuation 与 M10C 确定性。
- 完整 EditMode job `09c7b62ffe5c4bcfa8d239b99e30f51a`：**953/953 passed**、0 failed、0 skipped、21.7565442 秒。
- 最终 Unity scripts refresh 后编译 Console error 查询为 0。完整套件结束后 error filter 有 2 条测试期诊断：本地化 validator 的 `passed` 日志位于 NUnit `DoesNotThrow` 调用栈，以及 Test Runner 保存 `TestResults.xml`；两者都没有对应失败测试或产品异常。
- 目标代码/测试路径的 `git diff --check` 返回 0。

## 4. 结构断言

- `BattleCommandRuntimeDriver`、`BattleTurnHudView`、`HandCardContainer` 不再持有或注入 `BattleCommandSubmissionCoordinator`。
- 生产 `PreRegister` 调用只存在于 `BattleCommandQueue` 与 internal scheduling continuation；Editor 中只有 `BattleCommandSchedulingContractTests` 直接验证该内部算法。
- 仓库中不再存在 `SubmitRegistered`、测试 Queue→coordinator registry、UI `_commandCoordinator` 字段或反射注入。
- lifecycle 的 `CommandType` / `SubmitterId` 从同一原始不可变 Command 派生；coordinator 对账升级为 handle + authority sequence + exact command reference。
- `BattleCardPlayEvaluation`、Queue execution、卡牌规则、continuation token、表现屏障与 fault 类型没有改动。

## 5. 未验证边界

本轮没有真实点击 HUD/Hand、没有截图、没有 Packed Play/Player、没有 Addressables 重建，也没有目标平台验证。完整 EditMode 已覆盖现有 UI/Queue 合同，但不能替代未来涉及 Scene/Prefab 或输入接线变更时的真实交互验收。
