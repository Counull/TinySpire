---
title: M8B 命令生命周期、continuation 与表现屏障
page_type: testing
lifecycle: active
date: 2026-08-02
plan: ../plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md
status_source: ../SESSION_LOG.md
---

# M8B 命令生命周期、continuation 与表现屏障

## 验收范围

- 生产 `BattleCommandQueue` 已成为唯一排序和生命周期 owner：提交方先由统一 `BattleCommandSubmissionCoordinator` 为同一命令引用预注册不透明 handle，再只调用 `Submit`。Queue 接受后分配权威序号并发布唯一 Queued；拒绝会撤销未绑定 handle，不消耗序号。
- Hand 与 Turn HUD 只按精确 handle 维护短生命周期 pending，不保存权威序号，也不手工发布 Queued。旧命令终态不能清除新 pending；当前命令 Failed/Faulted 会清除匹配状态，其余已经接受的 queued handle 保留。
- Queue 使用非重入迭代 drain。命令执行后、调用 presentation 前，预定 `CompleteEnemyAction` continuation 由 Queue 以一次性 system token 入队并发布 Queued；既有 accepted 命令、continuation 与 presentation 期间新提交保持锁定的 FIFO 次序。
- 每条成功命令只按执行前后 Turn 差异追加一次聚合 `BattlePhaseChanged`。非空结算建立一个精确 completion 屏障；零结算 system continuation 直接通过。同步 completion 会先缓存，只有 `Present` 正常返回并发布当前终态后才解除屏障；旧 completion 与重复调用无效。
- 普通执行失败保持冻结空结算且不调用 presentation。表现或不可预期执行异常冻结 Queue fault、current 与 pending；`NoLegalNextIntent` 通过稳定 typed fault 映射为首次写入前 fault，不依赖异常文本且不声明可能部分写入。
- `BattleCommandRuntimeDriver` 只在启动时预注册并提交唯一 `StartBattleCommand`，不再实现 `ITickable`、轮询敌人阶段或从 Queue 外部提交 continuation。生产敌人仍沿用 M5 占位意图推进，不执行真实 Effect 或状态时机。

## 自动验证

| 检查 | 结果 |
|---|---|
| M8B 公开 Queue 生命周期定向 | **11/11 通过**，0 failed、0 skipped；任务 `e58b73dbf30146af9c3c872452b480f8` |
| Queue / Presentation / Turn / Intent / Effect 相关回归 | **86/86 通过**，0 failed、0 skipped；任务 `9ff3cfac1fd04c8985225a8fab372f8d` |
| 全量 EditMode | **240/240 通过**，0 failed、0 skipped；任务 `4641e50e1b1b4f089997571a76d23a8f`，耗时 `14.355882s` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning，无新增 warning 类别 |
| Unity 脚本刷新与 Console | 当前唯一 Unity 6000.5.5f1 Editor 完成；Bootstrap 物理验收期间 Error/Warning 为 **0/0** |
| diff / 排除路径审计 | `git diff --check` 通过；DataTables、生成内容、Localization、Addressables、Scene、Prefab、ProjectSettings、asmdef、HybridCLR 与启动流程均无 M8B 改动 |

定向用例明确覆盖：缺少预注册拒绝、Queued → Completed/Failed/Faulted 顺序、回调内 Submit 不重入、Queue 快照回调可见的生命周期先后、外部 system command 拒绝、一次性 continuation、三段 FIFO、零表现直通、同步 completion 后 presentation 抛错进入 fault，以及旧 completion 不影响新 current。

## Bootstrap 与真实 Game View

- 从 Bootstrap 进入生产 BattleScene 后，初始证据保存于 `TinySpire/Temp/CodexEvidence/m8b_bootstrap_initial.png`。
- 物理点击“结束行动”使 Round 1 完整经过两名敌人 continuation 后进入 Round 2，状态显示 `Completed #4 · CompleteEnemyAction`；证据为 `TinySpire/Temp/CodexEvidence/m8b_after_calibrated_physical_end_action.png`。
- Round 2 对按钮进行 50ms 间隔的物理双击，只形成 `#5 EndPlayerAction` 与 `#6/#7 CompleteEnemyAction`，随后进入 Round 3，没有重复结束提交；证据为 `TinySpire/Temp/CodexEvidence/m8b_after_physical_double_click.png`。
- 使用系统绝对鼠标移动完成一张 Self 卡的真实拖放，状态显示 `Completed #8 · PlayCard`，能量 `3 → 2`、手牌 `5 → 4`、弃牌 `0 → 1`；证据为 `TinySpire/Temp/CodexEvidence/m8b_after_absolute_card_drag.png`。
- 上述运行期间 Queue 顺序、pending 解锁和输入恢复均来自生产 UI/Submit 链；Console Error/Warning 为 0/0，随后正常退出 Play Mode。

## 停止点结论

- M8B 已把 M4～M7 的 View sequence、手工 Queued 与 runtime polling 迁入统一 coordinator/Queue 所有权；公开写 seam 仍是 `BattleCommandQueue.Submit`，`Queue` / `Turn` 仍只读。
- 为 M8B 过渡加入的 `BattleNoLegalNextIntentException` 只提供稳定 fault 分类；M8C 将用正式 `PrepareCompletion → ValidatePreparedCompletion → CommitPreparedCompletion` 联合事务替代旧的一步式意图完成写链。
- 生产敌人仍不造成伤害、不清 Block、不衰减 Vulnerable，也没有提前接入死亡与终局；这些严格留给 M8C 纯 module 与 M8D 生产接线。
- 实际 M8 起始 HEAD 仍为 `937b6fe50ec890cb3e71048da13a67c9d6815067`。并发出现的 `Docs/Hermes_Pegasus/art/**` 与 `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates*` 始终作为用户改动排除并未触碰。
- 未修改配置/生成内容、Localization、Addressables 内容、Scene/Prefab、ProjectSettings、asmdef、HybridCLR、启动/Run/网络或 DI 架构；`BattleLifetimeScope` 仅增加 coordinator 注册与移除 runtime polling，无需 Luban/Addressables 重建。
- M8B 独立停止点完成；完成本页、测试索引、决策、计划状态与 `SESSION_LOG.md` 同步后，下一步严格进入只交付纯 module/fixture 的 M8C。
