---
title: M7D 出牌事务与卡区结算记录
page_type: testing
lifecycle: active
date: 2026-08-02
plan: ../plans/2026-08-02-m7-effect-executor.md
status_source: ../SESSION_LOG.md
---

# M7D 出牌事务与卡区结算记录

## 验收范围

- `BattleCommandQueue.Submit` 仍是唯一外部写入 seam；队列只协调 `BattleTurnOperationResult`，没有按 EffectType 分发，也没有改变权威序号、轮次栅栏或 presentation completion 屏障。
- `BattleTurnController.TryPlayCard` 固定执行“同一 M6 规则重校验 → 全量 Effect 预构建与快照校验 → 记录并支付能量 → 按 `effect_bindings` 原顺序执行 → 当前卡牌进入弃牌堆 → 发布一次当前阶段 Turn 快照”。
- 任一规则或预构建失败都发生在首次写入前，返回空结算记录；成功结果按命令内连续序号冻结 Energy、Effect 与 CardMoved。
- `BattleCardZonesData` 的 Draw、DiscardHand、DiscardFromHand 与 ExhaustFromHand 返回不可变 `BattleCardZoneOperationResult`；抽牌记录保留“残余抽牌 → 弃牌按原序移回抽牌堆 → 冻结重洗后完整顺序 → 继续抽牌”的实际发生顺序。
- StartBattle、EndPlayerAction 与最后一名敌人完成后进入新轮的卡区变化，由原状态机调用栈直接追加到当前命令结果；没有新增系统命令、全局卡区日志或布局前后差值推断。
- 当前配置没有归宿字段，所有成功出牌只进入 DiscardPile；生产事务没有按模板 ID、卡名或 EffectType 硬编码 Exhaust。

## TDD 证据

1. 卡区接口先改为期望不可变操作结果，旧 `int` / `bool` 返回形成 7 项编译红灯；最小实现后的首轮任务 `0e716ec0352b42c28bdcdad55d3847f4` 为 **5/5 通过**。
2. 公开队列 tracer 首先证明旧链路会让后序缺失 Effect 的卡错误成功，且 Strength 成功结果没有结算记录；红灯任务 `615109a1f1634d90b20034ed12104c73` 为 **0/2 通过**。
3. 接入预构建、事务顺序与结果透传后，首轮队列事务任务 `549a746b740543a8bf58a2ed3f478a79` 为 **2/2 通过**；扩展四卡、致死与阶段记录后任务 `130281c087c6402e988d3cef2ed180e0` 为 **7/7 通过**。
4. 旧队列/屏障/轮次用例任务 `582ce2880349453bbc2f49e095c8161c` 为 **25/25 通过**；最终 M7D 定向及 M2～M6 回归任务 `873fd4ba9e844cf3a44b0b34529e691c` 为 **139/139 通过**。

覆盖行为包括：

- Strength：3 能量下支付 0，Strength `1 → 4`，然后当前卡 Hand → DiscardPile；记录顺序为 Energy、Attribute、CardMoved。
- Strike：来源 Strength 2、目标 Vulnerable 1、Block 5 时，`(6 + 2) * 3 / 2 = 12`；Block `5 → 0` 后 Health `40 → 33`，最后弃牌。
- Defend：支付 1 后 Block `0 → 5`，最后弃牌。
- Bash：支付 2 后按绑定记录 Damage 8、Vulnerable `0 → 2`、CardMoved；目标初始 Health 8 时第二绑定改为 `OperationSkipped(TargetNotAlive)`，命令仍成功并归堆。
- 后序 Effect 缺表会在能量、Turn、卡区与四项参与者事实写入前整体失败；费用不足、卡牌离手、目标排队后死亡、卡模板缺失和跨轮旧出牌也都显式断言空记录。
- StartBattle 的初始抽牌、EndPlayerAction 按权威手牌顺序弃置，以及最终敌人完成触发的残余抽牌/重洗/继续抽牌均由当前命令记录；同种子重洗冻结完全相同的抽牌堆顺序，混合 Draw 只发布一次 Layout。

## 回归、构建与生产 Bootstrap

| 检查 | 结果 |
|---|---|
| M7D 定向与 M2～M6 回归 | **139/139 通过**，0 failed、0 skipped；任务 `873fd4ba9e844cf3a44b0b34529e691c` |
| 覆盖集合 | 结算、公式、参与者状态、executor、卡区、四卡事务、队列/轮次/pending、M5 意图、M6 规则/目标、M2 随机 |
| Unity 脚本刷新与 Meta | 当前唯一 Editor 完成刷新；Console Error 0；新增卡区结果与事务测试 Meta 齐全 |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning |
| `git diff --check` | 通过；仅报告仓库既有 CRLF/LF 提示 |
| Bootstrap 生产实跑 | 从 `BootstrapScene` 进入 `BattleScene`；Console Error、InvalidKey、VContainer、Effect 过滤结果均为 0 |

## 停止点结论

- Strength、Strike、Defend、Bash 已经由公开队列 seam 进入同一生产 Effect 事务，公式、绑定顺序、格挡吸收、易伤、致死跳过、能量与归堆均有可观察记录和只读事实证据。
- 失败命令保持零新增写入且结算为空；M4 权威序号/轮次栅栏、M5 意图顺序与 M6 pending 恢复回归未退化。
- 阶段抽牌、弃手和重洗继续由既有状态机触发，只增加当前命令内记录；随机域、抽顶算法与单次 Layout 发布保持不变。
- 未修改 DataTables、生成配置/JSON、Localization、Addressables 内容、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、Run/网络、DI 或 M9 美术；无需 Luban/Addressables 重建。
- 本切片只完成自动 Bootstrap，不冒充 M7E 的真实 Game View 物理拖拽。M7D 独立停止点完成，下一步严格进入 M7E 全量验证、真实 Game View、复审与文档收口。
