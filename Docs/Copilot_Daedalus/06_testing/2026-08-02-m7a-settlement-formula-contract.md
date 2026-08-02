---
title: M7A 结算记录与公式契约
page_type: testing
lifecycle: active
date: 2026-08-02
plan: ../plans/2026-08-02-m7-effect-executor.md
status_source: ../SESSION_LOG.md
---

# M7A 结算记录与公式契约

## 验收范围

- 新增强类型 `BattleEffectId`、最小 Effect/结算枚举、不可变 `BattleSettlementRecord` 体系与纯公式输入/输出值对象。
- `BattleCommandExecutionResult.Settlements` 始终为非 null、冻结的只读列表；失败命令记录为空，presentation adapter 只转交当前执行结果。
- `BattleEffectFormula.Calculate(context)` 统一 DealDamage、GainBlock、ModifyAttribute 与 ApplyVulnerable 的纯数值口径；无目标结果供卡牌文本与敌人意图展示投影。
- `BattleEffectValueCalculator` 保持原公开签名，只负责把 Luban 类型和当前来源 Strength 适配到共享公式。
- 保持 M7A 边界：尚未新增 Block/Vulnerable 权威事实，没有执行正式 Effect，M6 成功出牌的卡区与能量行为未改变。

## TDD 证据

1. 首个结算 tracer 经 Unity 编译得到缺少 `BattleSettlementRecord` 与 `Settlements` 的预期红灯；最小接入后既有命令空记录用例转绿。
2. 八种记录类型用例随后经 Unity 编译得到八个缺失类型错误；补齐 sealed、getter-only 记录后，任务 `00f72ef01da648d9aa17459d61ca9b68` 为 **9/9 通过**。
3. 纯公式首个 tracer 经 Unity 编译得到缺少 context/result/module 的四个错误；最小无目标伤害投影转绿后，再加入易伤与非负操作用例，任务 `12609e90456245fa9cf6d2013baa9127` 先以 **3 个行为失败**证明旧最小实现缺口。
4. 易伤、伤害目标推演与非负规则接入后，公式与结算契约任务 `0c040ea611ae4820a97c34a3cae4bd91` 为 **20/20 通过**。
5. 展示适配器负 GainBlock/Vulnerable 用例先在任务 `7c56129a1c3d4dae8175e2e84ece4220` 得到 **2 个预期失败**；委托共享公式后，公式、结算与显示计算器任务 `826eb59631054330af0fa8ec75ea0afb` 为 **29/29 通过**。

覆盖行为包括：

- 伤害先计算 `max(0, configured + Strength)`；无目标时不伪造格挡或生命结果。
- 目标易伤大于零时，奇数攻击 `7 * 3 / 2` 向下取整为 `10`。
- GainBlock 与 ApplyVulnerable 对负配置值钳制为 0；ModifyAttribute 保留正负变化。
- 每条结算记录具有稳定命令内顺序、可空 Effect/来源/目标关联和可辨识类型；公开状态没有 setter。
- 失败执行结果和尚无 M7 写入的既有命令都返回非 null 空记录列表；外部不能修改列表。

## 回归与静态编译

| 检查 | 结果 |
|---|---|
| M7A 定向 EditMode | **83/83 通过**，0 failed、0 skipped；任务 `c62162836bd5451487ac273793d461a3` |
| 覆盖集合 | 结算契约、公式、`BattleEffectValueCalculator`、队列、presentation、敌人意图核心与 HUD 投影 |
| Unity 脚本刷新与 Meta 生成 | 通过；新增目录、运行时脚本与测试 Meta 均由当前唯一 Editor 生成，Console Error 0 |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning |
| `git diff --check` | 通过；仅报告仓库既有 CRLF/LF 提示 |

## 停止点结论

- `BattleCommandQueue.Submit`、只读 `Queue` / `Turn`、权威序号、展示屏障和轮次栅栏保持不变。
- 记录只属于一次执行结果，没有形成第二份全局可变日志；presentation 尚未实现动画消费。
- 未修改 `CombatantData`、`BattleCombatantsData` 或 `BattleTurnController`，因此没有正式 Effect 或参与者状态写入。
- 未修改 DataTables、生成配置、Localization、Addressables 内容、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、Run/网络、DI 或 M9 Targeting 美术；无需 Luban/Addressables 重建。
- M7A 独立停止点完成，下一步严格进入 M7B 参与者权威状态与伤害操作。
