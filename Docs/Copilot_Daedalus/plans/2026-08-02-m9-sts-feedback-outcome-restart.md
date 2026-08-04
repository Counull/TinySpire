---
title: TinySpire BattleScene M9 · STS 式反馈、胜负与重开
page_type: plan
lifecycle: archived
created: 2026-08-02
updated: 2026-08-05
scope: BattleScene M9A-M9G
status_source: ../SESSION_LOG.md
source: ../ROADMAP.md M9；../DEPENDENCIES.md DEP-003/DEP-004；../10_communication/2026-08-02-battle-card-motion-feedback-brief.md；../06_testing/2026-08-02-m8e-full-validation-review.md
depends_on: 2026-08-02-m8-enemy-actions-status-timing-battle-loop.md（M8 已完成）
---

# TinySpire BattleScene M9 · STS 式反馈、胜负与重开

## 当前结论

本页是 M9 实施期间的**唯一实施计划，现已归档**。M9 没有新增战斗规则，而是在 M4～M8 已完成的 `BattleCommandQueue.Submit`、只读 `Queue` / `Turn`、统一 coordinator、确定性意图、显式 Self/Enemy 目标、共享 Effect 公式、不可变结算记录、表现屏障、queue fault 与规则层终局上，完成 M3E 和 ROADMAP M9 的生产表现。最终验收见 `../06_testing/2026-08-02-m9g-full-validation-review.md`。

M9 已按 **M9A → M9B → M9C → M9D → M9E → M9F → M9G** 串行完成；每个切片均在测试先行、定向/相关回归、串行 solution build、必要的 Addressables 验证、范围审计、独立验收页和 `SESSION_LOG.md` 同步后才进入下一切片。

M8 代码基线提交为 `6545640963e3f184bcd7915706e87bea4a142afa`。正式 M9 Goal 开始时仍须现场记录实际 HEAD 与全部 tracked/untracked 改动，不得假设工作区干净。当前已知用户改动 `Docs/Hermes_Pegasus/art/asset-index.md`、`Docs/Hermes_Pegasus/art/assets/art-style/scenes/candidates/**`、`TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates.meta` 与 `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates/**` 全部排除并保护。

配套交接文档：

- 可复制 `/goal`：[`2026-08-02-m9-sts-feedback-outcome-restart.goal.md`](2026-08-02-m9-sts-feedback-outcome-restart.goal.md)
- 可复制启动 Prompt：[`2026-08-02-m9-sts-feedback-outcome-restart.codex-prompt.md`](2026-08-02-m9-sts-feedback-outcome-restart.codex-prompt.md)

## 执行纪律

1. 开始时完整重读根 `AGENTS.md`、本计划、`../ARCHITECTURE_CONVENTIONS.md`、CD-027～CD-048、DEP-003/004/007/008/010/011/012、M6D/M7E/M8E 验收与卡牌运动需求页；执行 `git status --short`、`git rev-parse HEAD` 并记录全部既有改动。
2. 测试先行，只测试公开 `IBattleCommandPresentation.Present`、只读事实、现有 View seam 或新 presentation 深 module 的最小接口；不为表现测试公开 Queue/Turn/Combatant/CardZones 写入口。所有新增函数至少有中文注释。
3. `BattleCommandQueue` 继续唯一拥有权威序号、Queued、非重入 drain、continuation FIFO、一次性 token、一次表现屏障与 fault；M9 不修改 Queue、Turn、settlement 类型或顺序，也不增加第二个 completion/计时/排序根。
4. 最终 HUD 只订阅当前权威事实；一次性数字、抖动、轨迹、状态脉冲、死亡与横幅只消费本次冻结结果。动画、Tween 回调、R3 subscriber 和重开/退出 View 不得写 Health、Block、Status、Intent、CardZones、Turn 或 outcome。
5. 表现期间仍保持 M4～M8 已确认的“可继续提交、由 Queue 排序”能力；不得因 `IsWaitingForPresentation` 全局锁住仍合法的玩家输入。只有离手临时 View、战斗开始覆盖层、终局战斗输入和场景切换按钮使用局部、可丢弃的表现锁；其中覆盖层只阻断系统指针，不改变 Queue 接受/排序语义。
   经用户于 2026-08-03 明确确认，M9C 可先为同一战斗开始覆盖层补齐启动 readiness 底座：`BattleParticipantPresenter` 只读报告全部预期世界 View/HUD 已完成唯一映射；在此之前仅 `BattleTurnHudView` 与 `HandCardContainer` 阻断系统指针入口，直接 `BattleCommandQueue.Submit`、Queue 接受/排序及表现 completion 语义不变。不得读取 `IsWaitingForPresentation`、新增 readiness 事件总线/第二 completion/权威事实；加载失败、对象销毁与重开必须清理。M9F 只把可见 StartBattle 覆盖层接到这同一底座，不得再建立第二套启动锁。
6. 新 Meta 与 Prefab 修改优先复用当前唯一 Unity MCP；不得启动第二 Editor、结束用户 Unity/Git 进程、删除未授权锁或清理 `Library` / `Temp`。
7. 每次修改可寻址 Scene 依赖的 Prefab/资源后执行 `TinySpire/Addressables/Build Local Content`。M9F 修改 `DataTables/Datas/i18n.xlsx` 后还必须运行 Luban 生成、Localization 同步及完整本地 Addressables 构建，并审计生成差异。
8. 未经明确确认不 commit、不 push；获准提交时只暂存显式路径，禁止 `git add .`。

## 锁定的 M9 MVP 契约

| 主题 | 唯一口径 |
|---|---|
| Queue-facing seam | 保持 `IBattleCommandPresentation.Present(BattleCommandExecutionResult, Action)`。深化现有 concrete `BattleCommandPresentationAdapter`，不建立 settlement 事件总线、每类记录一个 `I*Presenter`、第二屏障或动画命令队列。 |
| 表现计划 | 在首次播放前复制并校验当前结果，最多生成一个不属于 settlement 的命令级 `CommandPrelude`：`StartBattle` 的战斗开始覆盖层或 `PlayCard` 的出牌轨迹，二者按命令类型互斥。Prelude 完成后，全部 settlement 派生步骤严格按 `BattleSettlementRecord.Order` 播放；一个记录可形成稳定子步骤，没有可见步骤的非空结果立即 completion，不再固定等待 0.35 秒。 |
| completion 与取消 | 正常播放、加速和测试用立即完成至多回调一次精确 completion；场景/owner 销毁时 Kill 全部 Tween、清理临时 View 并丢弃旧 completion，不允许迟到回调进入新 Scope。表现同步抛错继续沿 M8 的 post-write queue fault，不伪装普通失败。M9 不新增玩家可见 Skip 按钮。 |
| 记录与事实 | Damage/Block/Status/CardMoved/Reshuffled/Intent/Phase 等瞬时反馈只读 settlement；生命、Block、Strength、Vulnerable、意图、卡堆计数与终局条件继续从原聚合即时派生，不保存数值或 outcome 镜像。 |
| 出牌前奏 | 这是唯一允许只读跨记录派生并提前播放的 `PlayCard` 例外：从同一冻结结果中的唯一 Hand→Discard `CardMoved` 取得卡牌身份，从首个可见 Effect 取得目标，形成恰好一个命令级 `CommandPrelude`；它先于 Order 0，之后 Energy、各 Effect 与最终 CardMoved 清理仍严格按 Order，绝不把后置记录本身重排。不得按卡名、模板 ID 或 EffectType 复制规则/公式。 |
| 临时卡牌 View | 权威 Layout 发布后，离手 View 立即退出可交互 `_cards` 集合，关闭 raycast/pending/targeting，只以短生命周期 transient visual 保留；播放/取消后销毁，并从当前 Layout 重建真实手牌。它不是 Hand 或 CardZones 镜像。 |
| 目标聚焦 | Enemy 卡首次越线后补间到 `BattleHandUI.prefab` 的序列化 focus anchor，旋转归零并轻微缩放/呼吸；箭头起点跟随卡牌、终点跟随真实指针。合法性、命中、Self/Enemy、`TargetId` 与无效回弹继续复用 M6 seam。 |
| 可用性表现 | `Disabled` 灰化且无输入；精确 `InsufficientEnergy` 保持 M6 的 `VisualOnly` 可拖动、费用不足提示，但无箭头/高亮/提交；`Playable` 正常。pending、终局或阶段失效必须取消焦点、箭头、高亮与 Tween。 |
| 常驻 HUD | 玩家和敌人显示非零 Block、Strength、Vulnerable 图标与层数；零值隐藏。敌人意图继续只投影当前 BehaviorId 和共享公式。已有 Weak/Poison 图片不授权对应规则或运行时分支。 |
| 战斗反馈 | `DamageApplied` 直接使用 `BlockAbsorbed` / `HealthLoss` / `WasFatal`：格挡吸收与生命损失分别飘字；只有实际生命损失触发角色抖动；fatal 在数字/抖动后播放死亡过渡。`BlockGained` 显示增加量；状态/属性记录只脉冲对应现有图标。 |
| 死亡 View | 新发生 fatal 只在对应数字/抖动/死亡过渡完成后隐藏世界 View 与完整 HUD；M9C 绑定时若参与者已经死亡则直接恢复为隐藏终态。任何路径都不删除 `CombatantData`，也不提前从权威 Encounter/参与者集合移除；Addressables 实例仍由现有 Presenter 在场景销毁时统一释放。 |
| 卡区运动 | DrawPile→Hand 从抽牌堆 HUD 锚点进入当前手牌位置；Hand→Discard 按 settlement 顺序飞向目标/弃牌堆；`CardsReshuffled` 在弃牌与抽牌 HUD 之间显示重洗过渡。ghost 不接收输入，取消后不残留。若合法 `BeginDrag` 命中仍在入场补间的真实手牌 View，只立即快进该卡到当前权威 base pose、把该 cue 精确完成一次，再进入正常拖拽；其他合法卡不受锁定。M9 不实现 Exhaust 归宿。 |
| 横幅与终局 | `StartBattle` 结果先播放一次战斗开始 `CommandPrelude`；玩家/敌人横幅只在 `BattlePhaseChanged.PhaseBefore != PhaseAfter` 且 `PhaseAfter` 分别为 `PlayerAction` / `EnemyAction` 时触发，EnemyAction→EnemyAction 的行动者交接不得重播敌人横幅。`BattleEnded` 面板必须排在同命令的伤害、死亡和其他反馈之后。表现 adapter 与规则位于同一 `Assembly-CSharp`，M9 允许其在 `BattleEnded` 步骤中作为同程序集 internal consumer 临时调用现有 `BattleTerminalRules` 并立即映射文案；这不是新增 public/DI seam，也不保存 outcome。若程序集边界变化或需要公开 terminal API，立即停止并新增决策。 |
| 重开 | “重新开始”经现有 `SceneFlowService.LoadSceneWithLoadingAsync(GameStartupOptions.InitialSceneAddress)` 重载同一 BattleScene；沿用 Inspector 中同一 encounter/seed，创建全新的 Session、Queue、订阅与 Tween，保证可复现。不得生成新 seed 或引入 RunState。 |
| 退出 | 当前没有 MainMenu；M9 的“退出”固定为退出应用。Player 调用 `Application.Quit`；Editor 以 EditMode 薄调用 seam 加真实 Game View 系统指针点击验证接线，不把 Editor 中的 no-op 冒充 OS 进程已退出。M9G 还须把 Development Player 构建到仓库外临时目录，运行后真实点击退出并记录目标进程正常结束；不得修改 Build Settings，也不把返回主菜单或新 Scene 偷渡进 M9。 |
| 本地化 | 固定新增 `battle.ui.battle.start`、`battle.ui.turn.player`、`battle.ui.turn.enemy`、`battle.ui.result.victory`、`battle.ui.result.defeat`、`battle.ui.action.restart`、`battle.ui.action.exit` 七个 Unity Localization key；编辑源只改 `DataTables/Datas/i18n.xlsx`，运行时继续经现有 `LocalizationService`，不得硬编码生产文案。 |
| 美术 | 正式接线已有 Runtime 目标箭身/箭头/合法与悬停高亮、Block、Strength、Vulnerable 和玩家横幅。经用户 2026-08-03 明确确认，M9C 飘字只用纯字符与颜色，不接伤害底板；敌人横幅复用同一横幅 Sprite 的序列化危险色，胜负面板使用功能性 UGUI 底板/文字。不得使用当前 Hermes/Candidates，亦不在 M9 临时生成缺失装饰美术。 |

## 允许范围与明确排除

### 允许的最小生产范围

- `TinySpire/Assets/Scripts/UI/Battle/**`：深化现有 adapter、Hand、Participant、Pile、Turn HUD，并可新增具体 `Feedback/` presentation module。
- `TinySpire/Assets/Editor/Tests/**`：M9 定向测试、Prefab/资源合约与必要回归夹具。
- `TinySpire/Assets/Prefabs/UI/Battle/Hand/BattleHandUI.prefab`、`TinySpire/Assets/Prefabs/UI/Battle/Targeting/BattleTargetingArrow.prefab`、`TinySpire/Assets/Prefabs/UI/Battle/BattleTurnHud.prefab`、`TinySpire/Assets/Arts/Runtime/Prefabs/ParticipantHudView.prefab`，以及一个必要的具体飘字 Prefab/Meta。
- 只引用、不改写 `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/**` 与现有正式 Block/Strength/Vulnerable/伤害/横幅资源及其 Meta。
- M9F 仅允许 `DataTables/Datas/i18n.xlsx`、由现有工具生成的对应 Localization 资产及本地 Addressables 构建输出。
- `BattleLifetimeScope` 预计无需修改；若新增 concrete View 无法通过现有 hierarchy 注册取得，最多允许一条必要的 `RegisterComponentInHierarchy`，不得调整 DI 架构。

### 明确排除

- 不修改 `TinySpire/Assets/Scripts/Battle/**` 的 Queue、Turn、Effect、公式、目标、状态时机、终局或 settlement 契约；不延迟权威写入迁就动画。
- 不实现 Weak、Poison、Dexterity、遗物、触发器、行为树、通用 DSL、新 Effect、新目标语义、多/随机/链式目标、Exhaust 归宿、多人、网络、重放或命令中途选择。
- 不处理 DEP-007/008/010/011/012，不引入 RunState、Run seed、存档、奖励、地图、主菜单或新 Scene。
- 不修改 battle 配置表、生成配置 C#、`TinySpire/Assets/GameData/` 战斗 JSON、ProjectSettings、asmdef、HybridCLR 或启动/场景流架构。
- 不修改 `BattleScene.unity`；现有 Hand、Pile、Turn HUD 和 ParticipantPresenter 接线足够。若实现发现必须改 Scene 或计划外 Prefab，立即停止确认。
- 不修改、移动、暂存或引用当前 `Docs/Hermes_Pegasus/**/candidates/**`、`TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates*` 用户改动；不复制《杀戮尖塔》或其他作品的素材、曲线、构图和动画。

## M9A · 有序表现时间线、一次 completion 与取消

状态：**已完成**

实施：测试先行建立纯 presentation plan/runner，把当前 14 类 settlement 按 `Order` 映射为零个或多个稳定步骤；另只允许 StartBattle / PlayCard 各自形成至多一个、互斥的命令级 `CommandPrelude`。深化现有 adapter，移除固定 0.35 秒占位，统一拥有当前计划、Tween、加速/立即完成与清理。保持 Queue-facing interface、表现期间提交、continuation 位置与 fault 语义不变。

停止点：全部记录类型、同记录子步骤、无可见步骤直通、严格顺序、同步/异步 completion、重复 completion、立即完成、owner 销毁与表现异常均有自动证据；红灯必须锁定 StartBattlePrelude → settlement Order 0…n，以及 Strike/Bash 的 PlayCardPrelude → EnergySpent(0) → Effect(1…n) → Hand→Discard(n+1)，并证明每命令至多一个 Prelude、后置记录未重排。M8 Queue lifecycle/FIFO/barrier/fault/terminal 回归、串行 build、diff/范围审计通过。新增 `../06_testing/2026-08-02-m9a-ordered-presentation-timeline.md`，同步计划状态和 `SESSION_LOG.md` 后才进入 M9B。

## M9B · Block、状态与既有意图 HUD

状态：**已完成**

实施：在现有 `ParticipantHudView.prefab` 静态接入 Block、Strength、Vulnerable 图标/层数；View 订阅同一参与者事实即时派生零值隐藏和 locale 刷新。M9B 对死亡只隐藏状态/意图行，0 HP 世界 View 与生命 HUD 仍保留给 M9C 消费 fatal 时序；保留 M5 的 BehaviorId→意图图标/共享公式路径，不新增状态集合或 HUD 数值镜像。

停止点：玩家及 1～3 名敌人的 Block 增减/清零、Strength、Vulnerable 施加/衰减与 View 重建均正确；死亡后状态/意图行隐藏，但 0 HP 世界 View/生命 HUD 不在本切片抢先消失。Weak/Poison 没有生产分支，意图预测不消费随机且不复制公式。定向/相关回归、串行 build、Prefab 合约、Local Content、Bootstrap 静态 HUD 与 Console 通过；新增 `../06_testing/2026-08-02-m9b-combatant-status-hud.md` 并同步后才进入 M9C。

## M9C · 数字、格挡、受击与死亡过渡

状态：**已完成**

实施：由 M9A 计划把 Damage/Block/Attribute/Status/Intent 记录路由到现有 Presenter 唯一 `CombatantId → View/HUD` 映射；新增具体飘字 View，区分 Block 吸收、HealthLoss 与 BlockGained，播放角色抖动、状态/意图脉冲及 fatal 死亡过渡。新 fatal 只能由 settlement timeline 在过渡结束后隐藏；View 重新绑定到已经死亡的参与者时直接恢复隐藏终态。所有最终姿态可由立即完成或取消恢复/收口。另按上述 2026-08-03 授权，在现有 Presenter 内提供非权威、只读的完整映射 readiness，并只在它为 false 时关闭 Turn HUD 与 Hand 的系统指针入口；不阻断直接命令提交，不新增 DI、Scene、Prefab、事件总线或 completion。

停止点：全格挡、格挡溢出、普通/易伤伤害、Strength、Vulnerable、Defend、fatal、skipped、普通失败和 fault 的可见步骤与零伪反馈均被锁定；多记录严格按 Order，死亡后另一敌人映射不串位，终局面板尚未出现。自动测试与真实 Bootstrap 首帧必须证明：预期参与者映射未齐时 End Action/BeginDrag 不接收系统指针，映射齐全后恢复既有合法性，直接 Queue 提交始终保持原语义，加载失败/销毁不产生迟到解锁；不得以固定等待时间代替 readiness。定向/相关回归、串行 build、Prefab/Local Content、Bootstrap 生产反馈与 Console 通过；新增 `../06_testing/2026-08-02-m9c-settlement-combat-feedback-death.md` 并同步后才进入 M9D。

## M9D · 不可用样式、目标聚焦与正式目标素材

状态：**已完成**

实施：在 M6 同一交互模式与目标 seam 上补齐 Disabled 灰化、VisualOnly 费用不足提示与 Playable 复位；Enemy 首次越线后进入序列化 focus anchor，旋转归零、缩放/呼吸，箭头起点逐帧跟随卡牌。Runtime/Targeting 四张正式 PNG 已接入箭身、箭头和左右合法/悬停高亮 Prefab；Tween 回调不提交命令，表现屏障期间其他合法玩家输入继续可用。经用户 2026-08-03 明确选择方案 2，五宽高比允许以跨帧 `InputSystemUIInputModule → EventSystem raycast → BeginDrag / Drag / EndDrag` 真实 UI 事件链替代 OS 物理鼠标；禁止直接 listener、Container 入口或单帧截图冒充交互/时序。

停止点：Self、左右 Enemy、空白/玩家/死亡/非法释放、费用不足、阶段变化、队首失败、对象/Scene 销毁均保持 M6 目标与零写入语义；16:7 初版约 1.9 px 遮挡红灯已通过 anchor `(0,-40) → (-8,-40)` 最小修订消除，16:7、16:9、16:10、16:11、16:14 连续帧中聚焦卡均不遮挡参与者/HUD，箭头起终点、高亮和清理正确。聚焦回归 98/98、串行 build、Sprite/Prefab 合约、Local Content、Bootstrap 与 Console 通过；证据见 `../06_testing/2026-08-02-m9d-card-focus-targeting-feedback.md`。`DEP-003` 已改为 resolved，下一步只进入 M9E。

## M9E · 出牌、弃牌、抽牌与重洗运动

状态：**已完成**

实施：解决 Layout 同步发布后旧 View 立即销毁的表现缺口：离手卡变成非交互 transient visual，adapter 按 `CardMoved` / `CardsReshuffled` 和命令上下文播放出牌前奏、目标/弃牌轨迹、结束行动成组弃牌、抽牌和重洗；真实手牌与 pile HUD 始终从当前权威 Layout 派生。合法 `BeginDrag` 命中仍在入场补间的卡时，只快进这一张到当下权威 base pose、把其入场 cue 精确完成一次，再开始正常 drag；不得全局锁手牌或取消其他合法卡的交互。

停止点：PlayCard Prelude、End Action 多牌、下一轮抽牌与弃牌堆重洗已由同一 runner 严格消费冻结 `CardMoved` / `CardsReshuffled`；Strength/Strike/Defend/Bash、EnemyAction 无旧交互手牌、ghost/`↻` 非交互、最终 Hand 精确等于权威 Layout 均有生产或自动证据。真实 `InputSystemUIInputModule → EventSystem raycast → BeginDrag / Drag / EndDrag` 证明入场卡只快进目标 cue 一次，其他合法卡仍可用且权威事实不变；立即完成、取消和 owner/Scene 销毁无残留或迟到 completion。最终聚焦 88/88、相关回归 166/166、串行 build、Local Content、Bootstrap 与 Console 通过；证据见 `../06_testing/2026-08-02-m9e-card-zone-motion.md`。`DEP-004` 已改为 resolved，下一步只进入 M9F。

## M9F · 阶段横幅、胜负面板、重开与退出

状态：**已完成**

实施：为 StartBattle 增加一次战斗开始覆盖层和局部指针锁；只在 phase 实际变化到 PlayerAction/EnemyAction 时播放对应短横幅，敌人之间的 actor 交接不重复播放。最后一步由同程序集表现 adapter 临时调用 internal `BattleTerminalRules` 即时派生 Victory/Defeat 并显示终局面板，不公开规则或保存 outcome。加入七个正式本地化文案、单次重开 guard、同 seed BattleScene 重载与退出应用入口。终局前不显示面板，终局后除面板外的战斗输入全部稳定锁定。

停止点：StartBattle 覆盖层严格只出现一次并先于首个玩家横幅；PlayerAction/EnemyAction 只在 phase 真正变化时播放，EnemyAction→EnemyAction 不重播。胜负面板严格位于数字/抖动/死亡和其他冻结反馈之后，终局后的真实战斗输入不再分配序号。连续重开两次均经 Loading 创建新 Session/Queue/HUD 并以同一 `1001:5001:5` 重置 HP、Intent RNG 与卡区；七个 zh-CN/en 文案、退出薄 seam 与 Editor 实际 EventSystem 按钮链均通过。Luban、Localization、`TinySpire/Build/Sync and Build All`、Bootstrap 双 locale、定向 111/111、串行 build 和范围审计完成。仓库外非热更新 Development IL2CPP Player 经 Windows `SendInput` 命中同一 Exit 按钮，原生句柄确认 `ExitCode=0`、PID 消失且未强杀；Player log 保留一条 Development JobTempAlloc 警告并已如实记录。证据见 `../06_testing/2026-08-02-m9f-turn-terminal-restart-exit.md`；下一步只进入 M9G。

## M9G · 全量验证与双轴收口

状态：**已完成**

实施：M9 定向 **160/160**、M2～M8 回归 **262/262**、全量 EditMode **423/423** 全部 0 failed/0 skipped；串行 `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` 为 0 error、12 条既有 warning。最终 Local Content、Bootstrap、真实 Game View 战斗开始/回合、目标、卡牌/战斗/卡区时序、胜利、失败、连续重开、退出按钮、取消/立即完成和五种宽高比均完成验收；仓库外 Development Player 经真实 Exit 点击以 `ExitCode=0` 自然结束。

停止点：连续帧与只读 Queue/事实快照、真实系统指针、五种宽高比、同 seed 连续重开、Editor seam/按钮接线及仓库外 Player OS 退出均有证据。Standards 首轮两项 finding 修正并全量重跑后，末轮 Standards / Spec 均为 **0 Hard / 0 Judgement**。`../06_testing/2026-08-02-m9g-full-validation-review.md`、测试/计划索引、`SESSION_LOG.md`、`CODE_DECISIONS.md`、`DEPENDENCIES.md` 与 `ROADMAP.md` 已同步；DEP-003/004、M3E/M9 已确认，本计划归档。未 commit、未 push。

## 必跑回归与完成定义

- M9A：feedback planner/runner、settlement contract、presentation adapter、M8B Queue lifecycle/scheduling。
- M9B/C：Participant HUD/Prefab、Intent HUD、Combatants、StatusTiming、Effect、M8D terminal/enemy loop。
- M9D：CardPlayRules、release resolver、drag transition、target selector/arrow/Prefab 与 M6 全套目标回归。
- M9E：CardZones、Effect command queue、Hand visual/transition、Pile HUD、M7/M8 阶段记录顺序。
- M9F：StartBattle/Turn HUD、同程序集 internal terminal rules、Session、GameStartupOptions/SceneFlow、Localization、退出 Player 与重复场景生命周期。
- M9 完成时：所有当前 settlement 都被明确消费或零可见直通；常驻 HUD 无镜像；战斗开始、反馈顺序、取消、ghost、死亡、横幅、胜负、重开与退出闭环；DEP-003/004 有真实生产证据；M2～M8 回归、全量、build、Addressables、Bootstrap、真实 Game View、Development Player 退出、Console、文档与双轴复审齐全。

## 立即停止并请求确认的条件

- 需要修改 Queue、Turn、settlement 类型/顺序、continuation、fault、Effect 公式、目标合法性、状态时机或终局规则。
- 现有 settlement/只读事实不足以形成某个反馈，必须新增战斗事实、保存第二份 Hand/CardZones/Combatant/Intent/outcome，或让动画回调写权威状态。
- “重开”需要新随机种子/RunState，或“退出”需要 MainMenu、新 Scene、地图/奖励/启动架构。
- 需要 DataTables 中 `i18n.xlsx` 以外的表、生成战斗配置、GameData 战斗 JSON、Scene、计划外 Prefab、ProjectSettings、asmdef、HybridCLR 或非最小 DI 变更。
- 必须使用当前 Hermes/Candidates、缺失正式美术或其他未授权资源；现有功能性 UGUI 无法满足 MVP。
- 需要 Weak/Poison/Dexterity、新 Effect、新目标类型、Exhaust、多人、网络、重放、命令中途选择或其他排除能力。
- Luban/Localization/Addressables 生成出现计划外语义差异、稳定地址失效或需要清理/重建未授权目录。
- Unity MCP、真实系统指针、连续时序、多宽高比、重开、仓库外 Development Player 退出或 Console 证据不足；不得用单元测试、截图、直接 listener 调用或 Editor `Application.Quit` no-op 冒充真实验收。
- 需要启动第二 Editor、结束用户进程、删除锁或清理 `Library` / `Temp` 才能继续。
