---
title: M7C 有序 Effect 执行 module
page_type: testing
lifecycle: active
date: 2026-08-02
plan: ../plans/2026-08-02-m7-effect-executor.md
status_source: ../SESSION_LOG.md
---

# M7C 有序 Effect 执行 module

## 验收范围

- 新增 concrete `BattleEffectExecutor`，公共 `Execute(request)` 接收来源、单个显式目标和复制冻结的有序 `CardEffectBinding`；没有新增 public `I*` adapter。
- `Prepare` 在首次写入前完成来源/目标存在且存活、Binding 结构、Effect 表项、类型、属性和数值范围的全量校验，并按绑定顺序模拟四项参与者标量。
- 预构建成功后，Strength、Damage、Block、Vulnerable 只经 M7B internal 状态操作按原顺序写入，并为每个操作产生一条不可变结算记录。
- 前序伤害致死时，后续已验证绑定产生 `TargetNotAlive` skipped 记录；后续绑定仍在首次写入前完成配置校验。
- 所有预构建失败返回明确原因、空记录并保持 Health/Strength/Block/Vulnerable 的只读对象和值不变；溢出与结算序号容量也在写入前拦截。
- M7B 测试夹具已迁到公共 executor seam，临时 `InternalsVisibleTo` 文件及 Meta 已删除；生产 internal 状态写入口未向 Editor 测试公开。

## TDD 证据

1. 首个 Strength 公共入口 tracer 因缺少 executor/request/result 类型得到编译红灯；最小 Strength 实现后任务 `a9effdd856c74bb78c096ea9a28f6085` 为 **1/1 通过**。
2. Strike 与 Defend tracer 在只有 Strength 分支时得到两项预期失败；任务 `9e9c7d2155204f69ba7fb29db0080134` 记录该红灯。补齐 Damage 与 Block 后任务 `ca43f86349614512ab60f8aafc542389` 为 **3/3 通过**。
3. 扩展到 Bash、声明顺序、重复执行、致死跳过、空绑定和完整失败原子性矩阵后，任务 `2c4fe76f816f4723b865840cce09e78d` 以非属性 Effect 携带 Strength 仍被接受而 **1/15 失败**；补齐 Attribute 契约后任务 `090eb2a78ff6455fa7b22ab638b39d55` 为 **15/15 通过**。
4. 删除 friend access、把既有死亡/受伤夹具迁到公共 executor 后，最终定向与相邻回归任务 `aa249726f6c9464396471ee74f864a40` 为 **95/95 通过**。

覆盖行为包括：

- Strength `+3`、Strike `6 + Strength`、Defend `+5 Block` 与 Bash `8 Damage -> +2 Vulnerable` 的正式值和记录类型。
- Bash 首次执行先造成 8 点伤害再施加 2 层易伤；同一请求再次执行时读取最新易伤，伤害变为 12，证明没有随机数或旧快照复用。
- 人工 `Vulnerable -> Damage` 绑定即时得到 1.5 倍伤害，证明 executor 不按 EffectType 重排。
- Bash 首击致死后第二绑定产生 `OperationSkipped(TargetNotAlive)`，命令仍成功且易伤不写入。
- null/非正 Binding、缺失 Effect、未知 EffectType、未知/错配 Attribute、缺失/死亡来源或目标，以及后序 Strength 溢出均在首次写入前失败。
- “致死绑定后跟缺失 Effect”仍整体失败且首击不发生，证明预校验不会因模拟死亡提前结束。

## 回归与静态编译

| 检查 | 结果 |
|---|---|
| `BattleEffectExecutorTests` | **15/15 通过**，0 failed、0 skipped；任务 `090eb2a78ff6455fa7b22ab638b39d55` |
| M7C 定向与相邻回归 | **95/95 通过**，0 failed、0 skipped；任务 `aa249726f6c9464396471ee74f864a40` |
| 覆盖集合 | Executor、状态事实、公式、显示值、M6 规则、队列、presentation、敌人意图与 HUD |
| Unity 脚本刷新与 Meta | 当前唯一 Editor 完成刷新；Console Error 0；新增 executor/request/test Meta 齐全 |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error**；12 条既有 Unity/R3/UniTask 依赖版本冲突 warning |
| `git diff --check` | 通过；仅报告仓库既有 CRLF/LF 提示 |

## 停止点结论

- `BattleEffectExecutor.Execute` 已成为生产与测试共同的公开 Effect seam；表查找、顺序模拟、状态写入和记录生成仍隐藏在 concrete 深 module 内。
- `Prepare` / `ExecutePrepared` 与 prepared plan 保持 internal，只为 M7D 在支付能量前完成完整预构建；计划绑定创建它的 executor 和参与者起始标量，禁止漂移后执行。
- M7B internal 状态操作只由 production executor 使用；测试不再依赖 friend assembly 或 internal 写入口。
- 尚未把 executor 接入 `BattleTurnController.TryPlayCard`，生产出牌仍保持 M6 的扣能量与归堆行为，不执行正式 Effect，也不产生能量/卡区记录。
- 未修改 DataTables、生成配置、Localization、Addressables 内容、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、Run/网络、DI 或 M9 美术；无需 Luban/Addressables 重建。
- M7C 独立停止点完成，下一步严格进入 M7D 出牌事务与卡区结算记录接入。
