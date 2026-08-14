---
title: M9B 参与者状态、Block 与既有意图 HUD
page_type: testing
lifecycle: active
date: 2026-08-02
updated: 2026-08-14
status: passed
scope: Participant HUD、正式状态资源、既有意图投影、Prefab、Addressables、Bootstrap 与范围审计
plan: ../plans/2026-08-02-m9-sts-feedback-outcome-restart.md
status_source: ../SESSION_LOG.md
---

# M9B 参与者状态、Block 与既有意图 HUD

## 当前结论

M9B 已通过。玩家与敌人的 `ParticipantHudView` 现在从当前 `CombatantData` 即时派生非零 Block、Strength、Vulnerable 图标与层数；零值和死亡时隐藏对应状态行。敌人意图继续从当前权威 `BehaviorId`、静态 Effect 与共享 `BattleEffectValueCalculator` 派生，不保存 HUD 数值、意图或参与者镜像，也不推进 Intent RNG。

本切片只在死亡时隐藏状态行和意图行；0 HP 生命 HUD、参与者 HUD 根节点及世界 View 继续保留，供 M9C 的 fatal 数字、抖动与死亡过渡消费。M9B 当时没有实现 Weak、Poison 或其他新状态；Poison 的后续领域实现见本文末尾 2026-08-14 注。

## 测试先行与红绿证据

| 契约 | 红灯 | 最小实现 | 最终绿灯 |
|---|---|---|---|
| 当前 Block / Strength / Vulnerable 事实投影 | 首个 `DeriveStatus_UsesCurrentFactsAndHidesAllStatusesAfterDeath` 因缺少 `ParticipantStatusPresentationData`、`DeriveStatus` 与 `FormatStatusValue` 产生 6 个编译错误；任务 `35b5395a3f094ad7be2bd9a6e824c252` 无法发现测试 | 新增不可变只读投影，使用 invariant 数值格式；零值逐槽隐藏，死亡整行隐藏 | 任务 `baec509d5bfe4e23a6733d5b8a650965`，1/1 passed |
| 静态状态层级与正式 Sprite | `ParticipantHudPrefab_HasStaticStatusHierarchyAndOfficialSprites` 因 Prefab 缺少 `StatusRow` 红灯；任务 `6007577145da426481323b9feff47e01` | 通过当前唯一 Unity MCP 在既有 `ParticipantHudView.prefab` 增加 Block / Strength / Vulnerable 静态非交互节点并序列化引用 | Prefab 与递归依赖合约进入最终 17/17 |
| 真实 View 订阅、清零、衰减、死亡与重建 | 首个公开 `Bind` 测试因缺少状态层级得到 `NullReferenceException`；任务 `1ef93f2d65724623b592d46f85753254` | View 直接订阅 Health、Block、Strength、Vulnerable 与 LocaleChanged；每次从当前参与者事实重投影 | 最终覆盖玩家及敌人的活体、死亡、死亡重建和 locale 重投影 |
| 状态行整体隐藏与多位数可读性 | 复审发现只隐藏三个子槽、`StatusRow` 仍激活，且 19px 文本会裁切合法多位数；补入两个红灯断言后任务 `0f22b55fb401453c830e8de0b0d174b7` 为 0/2 passed | `RefreshStatus` 消费 `IsVisible`；状态行默认关闭，三个数值框扩大到 31px 并启用 12～18 Best Fit | 任务 `75a310b4b7dc4ec4baacaeb316c4ec75`，4/4 passed |
| 敌人死亡意图与 Bind 后 locale | 复审指出原玩家夹具不能证明敌人真实意图隐藏，也未覆盖 Bind 后语言切换 | 新增真实 `BattleEnemyIntentsData` 的敌人 View 测试；死亡及死亡重建保留同一参与者、Layout、BehaviorId 与 RNG，语言切换从当前事实恢复名称和 Strength | 两项进入任务 `75a310b4b7dc4ec4baacaeb316c4ec75` 并通过 |

## 常驻 HUD 与当前事实

- `ParticipantStatusPresentationData` 只复制一次投影所需的 Block、Strength、Vulnerable 与可见性；它不是长期状态，也不被 View 回写。
- View 订阅同一参与者的当前 Health、Block、Strength、Vulnerable。Block 清零、Strength 修改、Vulnerable 衰减及 View 重建都重新读取当前事实。
- Strength 变化仍调用既有敌人意图刷新，意图数值继续走 `BehaviorId → CardEffect → BattleEffectValueCalculator`；重复投影与 locale 切换不消费随机、不替换 Intent Layout。
- Bind 后切换 `en → zh-CN` 的测试先把纯 View 名称和 Strength 文本写成 stale，再由 `LocaleChanged` 从当前事实恢复为“典狱长”和 `2`；权威 Strength、Layout 与 RNG 均未变化。
- 三敌隔离测试给每名敌人不同 Block / Strength / Vulnerable，并证明清零一个参与者不会串写其他 HUD。
- 玩家和敌人的死亡、死亡 View 重建均只隐藏 `StatusRow` / `IntentRoot`；`VitalsAnchor`、HealthBar、HealthText、HUD 根节点和世界 View 保持活动，权威 `CombatantData` 与意图映射仍存在。

## Prefab、正式资源与 Addressables

既有 `ParticipantHudView.prefab` 新增静态层级：

`VitalsAnchor / StatusRow / (Block | Strength | Vulnerable) / (Icon | Text)`

- `StatusRow` 和三个状态项默认隐藏；全部 Image / Text 不接收 Raycast。
- 状态行位于生命条下方，HealthBar / HealthText 仍是 `VitalsAnchor` 的独立兄弟节点，不属于死亡隐藏组。
- 正式资源只引用：
  - `Assets/Arts/Runtime/UI/Battle/ui_battle_icon_block.png`
  - `Assets/Arts/Runtime/UI/Battle/ui_battle_icon_strength.png`
  - `Assets/Arts/Runtime/UI/Battle/ui_battle_icon_vulnerable.png`
- 三张资源均保持当前单 Sprite、mipmap 关闭的导入契约；BattleScene 递归依赖测试证明它们只经既有 Participant HUD Prefab 接入。
- Prefab 内不存在 Weak / Poison 节点；M9B 发布时生产 C# 也没有对应分支。CD-106 后 Poison 已有领域 / 时序分支，但 Participant HUD 仍没有 Poison 分支。

最终 Prefab 修订后重新执行 `TinySpire/Addressables/Build Local Content`；`catalog.bin` 与 `catalog.hash` 的最终时间为 **2026-08-02 19:15:12**，命令与 Console 均无构建错误。没有新增地址、修改 Addressables 配置或改动 Scene。

## 最终自动验证与静态构建

| 检查 | 结果 |
|---|---|
| P2 修正聚焦：Prefab 状态行、玩家死亡重建、敌人死亡重建、locale 重投影 | **4/4 passed**；任务 `75a310b4b7dc4ec4baacaeb316c4ec75` |
| Participant presentation / Prefab / View + Intent HUD | **17/17 passed，0 failed，0 skipped**；任务 `67f758d8702b40289fad2d27004dbb68` |
| Participant、Combatants、StatusTiming、Effect、Intent、M8D terminal/enemy loop、targeting 相关回归 | **130/130 passed，0 failed，0 skipped**；任务 `3df95152b461404c9ee8c5a450c7540c` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings**；均为既有 Unity / R3 / UniTask 依赖程序集版本冲突 |
| `git diff --check` | 生产、Prefab、测试及文档停止点前通过 |
| Unity Console | Local Content、Bootstrap 与退出 Play Mode 后 Error / Warning 均为 **0/0** |

相关回归覆盖当前参与者集合、Effect 操作/公式/执行/Queue、Block 与 Vulnerable 状态时机、当前 Intent 与 RNG、M8D terminal/enemy loop、1～3 敌人布局、参与者目标映射及 targeting Prefab；未修改或替换这些契约。

## Bootstrap 静态 HUD

当前唯一 Unity Editor 从 BootstrapScene 经 LoadingScene 进入生产 BattleScene。最终 Game View 证据为 `TinySpire/Temp/CodexEvidence/m9b_final_initial_status.png`：

- Round 1 / PlayerAction 正常启动，玩家与两名敌人的生命 HUD 均可见。
- 初始 Block / Strength / Vulnerable 全为零，因此三个 `StatusRow` 均隐藏；画面没有空状态占位。
- 两名敌人的既有意图图标和值可见，玩家意图隐藏。
- 只读运行期快照为两名敌人 `health=20 / 20, status=False, intent=True`，玩家 `health=30 / 30, status=False, intent=False`；三个 HUD 根节点均保持活动。

动态状态增减、敌人真实意图死亡隐藏和 0 HP 保留由公开 production `Bind` 自动测试证明；本切片的 Bootstrap 要求是静态 HUD，未把直接 listener、单张截图或测试夹具冒充真实系统指针和 M9C 动画时序。

## 范围与工作区保护

M9B authored 生产范围严格为：

- `TinySpire/Assets/Scripts/UI/Battle/ParticipantHudPresentation.cs`
- `TinySpire/Assets/Scripts/UI/Battle/ParticipantHudView.cs`
- `TinySpire/Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab`
- 对应 `ParticipantHudPresentationTests`、`ParticipantHudPrefabContractTests` 与新增 `ParticipantHudViewTests.cs/.meta`

没有修改 Queue、Turn、settlement、Combatant、Intent、CardZones、StatusTiming、Effect 公式/执行、目标、终局规则、`BattleLifetimeScope`、Scene、其他 Prefab、DataTables、Localization、生成战斗配置、GameData、ProjectSettings、asmdef、HybridCLR、启动 / DI / Run / 网络 / 多人架构。启动基线中的 Hermes/Candidates 四组用户改动未读取为资源、未引用、未修改、未回退、未移动、未清理、未暂存。未 commit、未 push。

## 停止点判定与后续

### 2026-08-14 Poison 运行时后续注

CD-106 已在领域与时序层接入通用 Poison，并让 tick 只复用既有 `HealthLossNumber`、致死时再复用 `DeathTransition`；它不派生 Attack hit-shake、Block absorbed 或状态 pulse。本切片没有获得 Prefab 修改授权，因此本页“Prefab 没有 Poison 节点”的结论仍然成立：当前没有常驻 Poison 图标、层数文本或脉冲。不得把 tick 数字 / 死亡过渡的已有表现支持误写成 Poison HUD 已完成。

玩家及 1～3 名敌人的 Block 增减/清零、Strength、Vulnerable 施加/衰减、View 重建、死亡行隐藏和 0 HP 保留均有自动证据；M9B 发布时 Weak / Poison 无生产分支，CD-106 后 Poison 只有领域 / tick 表现、仍无常驻 HUD。既有意图预测继续使用共享公式且不消费随机。定向/相关回归、串行 build、Prefab 合约、最终 Local Content、Bootstrap 静态 HUD、Console、范围审计和本文档同步均已完成。

M9B 停止点完成。下一步只进入 M9C · 数字、格挡、受击与死亡过渡；M9D～M9G 仍保持待实施。
