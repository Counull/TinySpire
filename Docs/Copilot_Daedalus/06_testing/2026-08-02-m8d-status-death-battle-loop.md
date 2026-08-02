---
title: M8D 状态时机、死亡与完整战斗循环
page_type: testing
lifecycle: active
date: 2026-08-02
plan: ../plans/2026-08-02-m8-enemy-actions-status-timing-battle-loop.md
status_source: ../SESSION_LOG.md
---

# M8D 状态时机、死亡与完整战斗循环

## 验收范围

- 生产 `BattleCommandQueue` 已接入 M8C concrete enemy transaction。Queue 只协调当前系统命令、派生终局、冻结 continuation、合并连续 settlement 与建立一次表现屏障；Behavior、目标、Effect、状态和 Intent 联合 prepare/validate/commit 仍封装在 `BattleEnemyActionExecutor`。
- 玩家 `PlayerRoundStart` 固定为存活玩家 Block 清零 → 能量恢复（值变化时记录）→ Draw；成功 `EndPlayerAction` 固定为按手牌原序 Discard → 玩家 Vulnerable 最多减 1。敌人固定为自身 Block 清零 → ordered Effect → 自身 Vulnerable 最多减 1 → 下一 Intent/history/random。
- 双敌只按 `EnemyCombatantIdsInEncounterOrder` 推进。死亡 source 返回 `EnemyActionSkipped(SourceNotAlive)`，不读取目标、Effect 或 Intent；当前敌人致死玩家后仍完成本次 Intent commit，再进入中立 `BattleEnded`，且不排入剩余敌人；最后敌人被玩家击杀时同一出牌命令直接进入终局，无需再结束行动。
- 当前命令的权威 Turn 变化与其他 settlement 在 `Present` 前原子提交；表现 completion 只解除 Queue barrier。屏障期间后继命令已按权威序号排队，但不会执行，也不会改写下一敌人的 Combatant/Intent 或下一轮 CardZones。
- 普通阶段、行动者、目标与战斗结束失败仍为空 settlement、零权威写入；配置、多人目标、prepared 不变量等 direct fault 在首次写入前冻结且 `MayHavePartialWrites=false`，提交后的未预期异常才标记 `true`。

## 自动验证

| 检查 | 结果 |
|---|---|
| M8D Queue 定向 | **11/11 通过**，0 failed、0 skipped；任务 `c043935ab8f64ff2b95ea6631e77044c` |
| M8D + 旧阶段重洗聚焦回归 | **12/12 通过**，0 failed、0 skipped；任务 `d96ef64e291a4171ae77f06e83400c24` |
| 全量 EditMode（覆盖 M2～M7 回归） | **285/285 通过**，0 failed、0 skipped；任务 `b07b41b753a24865b50b73fb652be332` |
| 两项旧 settlement 迁移复核 | **2/2 通过**，0 failed、0 skipped；任务 `702b8ecd50be44e982db1bb5e9961fed` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning，无新增 warning 类别 |
| Queue / transaction 复审 | 普通失败零写入、direct fault `partial=false`、post-commit 异常 `partial=true`、source skip、致死中止、continuation FIFO、终局拒绝与 settlement 连续顺序均无剩余 Hard finding |
| diff / 排除路径审计 | `git diff --check` 通过；未修改配置、生成内容、Localization、Addressables、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动流程或 DI 架构 |

定向测试还锁定真实联合顺序：`DamageApplied → EnemyIntentAdvanced → CardMoved(Discard→Draw)×2 → CardsReshuffled → CardMoved(Draw→Hand)×2 → BattlePhaseChanged`；同一结果的 `Order` 从 0 连续递增，抽牌顺序与冻结重洗栈顶一致。

## Bootstrap 与真实 Game View

唯一现有 Unity 6000.5.5f1 Editor 从 `BootstrapScene` 进入生产 `BattleScene`，使用场景 seed 5。交互全部通过 Windows 系统指针在真实 `UnityEditor.GameView` 中点击或连续拖放；Unity MCP 只负责 Play/Stop、截图、只读运行时事实和一次性暂停探针，没有直接调用 Queue 或战斗写入口。

### 反馈屏障

- 真实点击首轮 `End Action` 后，一次性只读 Queue 订阅在首敌反馈屏障把 Editor 暂停。快照为 `current=3 / CompleteEnemyAction / pending=1 / waiting=true / fault=false`，Turn 已原子发布为 `EnemyAction / Round 1 / CurrentActingEnemyId=3`。
- 此时首敌伤害已使玩家 `30 → 24`，但下一敌仍为 `20 HP / 0 Block / 0 Vulnerable / Behavior 7003`；即后继已排队而尚未执行。恢复后才进入 `PlayerAction / Round 2`，下一敌完成 Defend 并得到 5 Block。
- 截图：`TinySpire/Temp/CodexEvidence/m8d_enemy_feedback_barrier_paused.png`。该目录被项目忽略，只保存本机验收证据，不进入交付 diff。

### 多轮胜利、状态与 Encounter 死亡跳过

- Round 1 真实打出 `Defend + Strike + Defend`：玩家得到 10 Block，先行动敌人 `20 → 14`。敌人阶段后玩家仍为 30 HP；Round 2 开始把残余 4 Block 清为 0，另一敌人的 Defend 产生 5 Block。
- Round 2 真实打出两张 Strike 与一张 Defend：先行动敌人 `14 → 2`；敌人阶段两次 attack 使玩家 `30 → 23`，且另一敌人在攻击前把自身旧 5 Block 清为 0。
- Round 3 在玩家行动期先以 Strike 击杀首敌，再对存活敌人打出 Bash：存活敌人 `20 → 12` 且 Vulnerable `0 → 2`。结束行动后 Encounter 从当前存活事实选择次敌，首敌没有获得敌人行动命令；次敌只行动一次并把自身 Vulnerable `2 → 1`，玩家 `23 → 17`。
- Round 4 对 Vulnerable 目标的 Strike 复用共享公式造成 9 点，`12 → 3`；第二张 Strike `3 → 0` 后同一玩家命令立即进入 `BattleEnded`，不要求 End Action。终局快照为玩家 17 HP、两敌 0 HP、Queue 空闲且无 fault；终局后再次物理点击 End Action，全部事实保持不变。
- 截图：`m8d_victory_round3_status_before_enemy.png`、`m8d_victory_terminal.png`。

“敌人命令排队后 source 才死亡”的专用路径由自动测试 `EnemyAction_SourceDiesAfterQueued_SkipsSourceOnlyThenContinues` 验证：测试在 End 已提交、后继敌人命令仍受表现屏障阻塞时杀死 source；解除屏障后，该 enemy transaction 的 action-specific 记录仅为 `EnemyActionSkipped(SourceNotAlive)`，Queue 随 Encounter 交接再追加 `BattlePhaseChanged`。目标、状态、Effect、Intent/history/random 均不读取或推进。真实物理路线只证明 Encounter 启动时跳过已经死亡的敌人，不把两条证据混为一谈。

### 玩家死亡中止剩余敌人

- 独立重启生产战局，前两轮不出牌；Round 3 真实打出一张 Defend 后结束，使玩家以 5 HP 进入 Round 4。致死前下一敌为 `20 HP / Behavior 7003`，Intent RNG 为 `853394020`。
- Round 4 真实点击 End Action 后，Encounter 首敌 attack 使玩家 `5 → 0`，立即进入 `BattleEnded`。剩余敌人仍为 `20 HP / 0 Block / 0 Vulnerable / Behavior 7003`，Intent RNG 仍为 `853394020`，证明未执行其 Effect、状态或下一意图选择。
- 截图：`TinySpire/Temp/CodexEvidence/m8d_defeat_first_enemy_stops_remaining.png`。最终生产 Console 为 **0 error / 0 warning**，随后通过 Unity MCP 正常退出 Play Mode。

Block、Vulnerable、Intent history/random 与 settlement 顺序当前不属于 M3E/M9 HUD 范围，因此截图只证明可见阶段、生命、能量、手牌与终局；隐藏事实由同一生产容器的只读 public seam 快照及自动测试共同验收，不把截图冒充隐藏状态证据。

## 停止点结论

- 单敌 fixture 与当前双敌生产均按 Encounter 顺序每轮各行动一次；0 值/死亡不造伪状态记录，玩家死亡中止剩余敌人，最后敌人死亡即时终局，终局后的新旧命令均稳定失败或拒绝。
- M8D 没有新增 `BattleLifetimeScope` 注册；继续复用 M8B 的唯一 coordinator 与 Queue，生产不再存在敌人 polling/第二条阶段推进链。
- 未实现 Block/状态 HUD、数字、抖动、死亡过渡、横幅、胜负面板、奖励、重开、最终动画或 LXX-6 美术；未实现 Weak、Dexterity、遗物、触发器、DSL、多/随机/链式目标、Exhaust、Run/网络或多人生产装配。
- 实际 M8 起始 HEAD 仍为 `937b6fe50ec890cb3e71048da13a67c9d6815067`。Hermes/Candidates 美术改动继续作为用户范围排除并保持未触碰。无需 Luban 或 Addressables 重建。
- 完成本页、测试索引、CD-047、计划状态与 `SESSION_LOG.md` 同步后，M8D 独立停止点成立，下一步严格进入 M8E 全量验证与双轴收口。
