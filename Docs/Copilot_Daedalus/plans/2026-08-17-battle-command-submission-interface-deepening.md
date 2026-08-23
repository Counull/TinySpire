---
title: BattleCommandQueue 提交接口深化
page_type: plan
lifecycle: archived
created: 2026-08-17
updated: 2026-08-24
status: implemented-and-verified
scope: Battle command submission interface only
source: improve-codebase-architecture seam audit；2026-08-17 用户实施授权
status_source: ../STATUS.md
---

# BattleCommandQueue 提交接口深化

## 目标与结论

把生产调用者实际依赖的 `PreRegister → pending → Submit → rejection rollback` 四步协议收回 `BattleCommandQueue`，让公开写入口重新与名义接口一致：调用者只提交一个不可变命令，并通过 Queue 的只读生命周期观察接受与终态。

本方案已经完成。它没有拆分 Queue，也没有新增抽象 interface；排序、drain、continuation、system token、表现屏障、completion 与 fault 仍由原模块拥有。

## 冻结设计

| 问题 | 选择 | 不采用 |
|---|---|---|
| handle 签发 | `Queue.Submit` 内部接受前签发 | 调用者显式 `PreRegister` |
| pending 建立时机 | `Queued` 事件中同步建立，终态按同一 handle 清除 | Submit 前乐观 pending + 拒绝回滚 |
| UI 如何识别命令 | lifecycle 携带原始不可变 `BattleCommand`；现有 Hand/HUD 按 concrete command 识别 | 新 callback、可释放 submission object 或新增 DTO 层 |
| 生命周期入口 | `BattleCommandQueue.Lifecycle` | View 直接注入 coordinator |
| coordinator 边界 | 注册/匹配/取消/对账/lifecycle 源保持 internal；仅 Queue/scheduling 消费 | 删除 scheduling core 或重议 CD-043/045 的调度语义 |

## 实施切片

1. 先以 Queue 合同测试锁定：结构性拒绝无序号、无 lifecycle；已接受命令的 Queued/终态携带同一原始 Command。
2. Queue 内部执行注册，公开只读 lifecycle，并把 coordinator 操作降为 internal。
3. 迁移 runtime driver、Turn HUD 与 Hand：删除 coordinator 注入、预注册和拒绝回滚，在 Queued 回调建立 pending。
4. 删除测试 `SubmitRegistered` 扩展、Queue→coordinator registry 与 UI 反射注入；普通测试改走真实 `Submit`，低层 scheduling 合同测试保留内部算法覆盖。
5. 运行编译、定向与完整 EditMode，最后同步 CD-115、SESSION_LOG 与验收页。

## 影响与排除

生产影响仅限 Battle command submission/lifecycle 与两个 UI 消费者；测试以机械迁移调用点为主。没有修改 `BattleCardPlayEvaluation`、卡牌规则/数值、Run、存档、配置表、GameData、Localization、Scene/Prefab、ProjectSettings、asmdef、Addressables 或包依赖。

本方案不把 coordinator 删除为新架构项目，也不新增 replay/submission state object；对当前两三人维护规模，`Submit + Lifecycle` 已提供足够深且更小的接口。

## 验收与回滚

- Unity 编译 Console 0 error；M8B 11/11，相关聚合 116/116，完整 EditMode 953/953；`git diff --check` 通过。
- 没有运行 PlayMode、Player build、Addressables 或人工 BattleScene smoke，不能把本记录当作场景交互实拍证据。
- 若回滚，以 Queue/lifecycle、三个生产调用者与测试调用迁移作为一个原子单元恢复；不涉及资产、配置生成物或 Scene 文件。

完整 job 与 RED→GREEN 证据见 `../06_testing/2026-08-17-battle-command-submission-interface-deepening.md`。
