---
title: M7B 参与者权威状态与伤害操作
page_type: testing
lifecycle: active
date: 2026-08-02
plan: ../plans/2026-08-02-m7-effect-executor.md
status_source: ../SESSION_LOG.md
---

# M7B 参与者权威状态与伤害操作

## 验收范围

- `CombatantData` 新增 Block、Vulnerable 私有 R3 持有者、只读事实、同步值与完整 Dispose 生命周期。
- 建立 internal concrete `BattleCombatantEffectOperations`，集中 GainBlock、ModifyStrength、ApplyVulnerable、ApplyDamage；内部结果携带标量前后值或完整 damage outcome。
- Damage 只计算一次共享公式，再由 `CombatantData.ApplyDamageOutcome` 在一个同步调用内写入 Block/Health。
- 删除 `BattleCombatantsData.ApplyDamage` 与旧 `CombatantData.ApplyDamage(int)`；既有死亡夹具改经新 Effect 状态路径。
- 保持 M7B 边界：不读取 `Card.EffectBindings`，不创建正式 executor，不接出牌事务、UI、敌人 Effect、状态衰减或格挡清理。

## TDD 证据

1. 初始事实 tracer 经 Unity 编译得到 Block/Vulnerable 四个缺失成员错误；补齐持有者、只读事实和同步值后，任务 `c84e80367b164d808387998b34394d0b` 为 **1/1 通过**。
2. GainBlock tracer 随后得到内部 operations/result/status 四个缺失类型错误；最小 internal 状态入口接入后，任务 `5d03846c623943a38941545398546baa` 为 **2/2 通过**。
3. Strength 与 Vulnerable tracer 得到四个缺失方法错误；只补齐对应状态操作后，任务 `f4e06b416b874461afd24baa26abb01c` 为 **4/4 通过**。
4. 首个 Block 吸收 tracer 得到缺少 `ApplyDamage` 的编译红灯；接入单次公式推演和 Block/Health 写入后，任务 `3ebe10c0531c491aa4e7df166acc650d` 为 **5/5 通过**。
5. 最终补齐格挡溢出、过量致死、重复攻击死亡目标与公式 outcome 用例；状态、参与者与公式任务 `8cc24387d2664e5cba1b17d27ad29973` 为 **24/24 通过**。

覆盖行为包括：

- 新参与者 Block/Vulnerable 初值为 0；两份只读 R3 事实由同一参与者实例持有和释放。
- GainBlock 与 Vulnerable 累加非负公式值；Strength 接受正负有符号修改。
- Damage 读取当前 Strength/Vulnerable，先吸收 Block，再把余量应用到 Health；生命最低为 0。
- Damage outcome 含攻击值、格挡与生命 before/after、吸收量、真实生命损失和致死标记。
- 目标被首击致死后，重复伤害返回明确 `TargetNotAlive`，不再写入且不替换只读事实对象。

## 回归与静态编译

| 检查 | 结果 |
|---|---|
| M7B 状态/公式核心 | **24/24 通过**，0 failed、0 skipped；任务 `8cc24387d2664e5cba1b17d27ad29973` |
| M7B 定向与相邻回归 | **72/72 通过**，0 failed、0 skipped；任务 `de864c324234402b86e4d9b2e2c79220` |
| 覆盖集合 | 状态操作、参与者、公式、M6 规则、队列、Session、目标、敌人意图与 HUD |
| Unity 脚本刷新与 Meta 生成 | 通过；新增运行时脚本、临时测试 assembly access 与测试 Meta 均由当前唯一 Editor 生成，Console Error 0 |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning |
| `git diff --check` | 通过；仅报告仓库既有 CRLF/LF 提示 |

## 停止点结论

- `BattleCombatantsData` 只保留参与者创建、唯一映射和只读查找；旧 public 伤害入口与旧单层生命扣减均已删除。
- internal operation/result/status 没有提升为 production public DTO；M7B 为直接验证内部状态写入口临时使用 Editor friend access，M7C 公共 `BattleEffectExecutor.Execute` 落地后必须迁移测试并删除该文件。
- Block/Health 是两个只读 R3 事实；当前保证一次同步 Effect 调用内按公式结果写入，不声称它们产生单一合并通知。
- 未读取 Card/Effect bindings，M6 成功出牌行为仍不执行 Effect；队列、回合、UI 与 presentation 未接入新状态写入。
- 未修改 DataTables、生成配置、Localization、Addressables 内容、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、Run/网络、DI 或 M9 美术；无需 Luban/Addressables 重建。
- M7B 独立停止点完成，下一步严格进入 M7C 有序 Effect 执行 module。
