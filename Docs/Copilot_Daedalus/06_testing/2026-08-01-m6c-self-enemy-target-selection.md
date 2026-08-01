---
title: M6C Self / Enemy 目标选择 UI
page_type: testing
lifecycle: active
date: 2026-08-01
updated: 2026-08-02
status: passed
plan: ../plans/2026-08-01-m6-card-play-legality-target-selection.md
status_source: ../SESSION_LOG.md
---

# M6C Self / Enemy 目标选择 UI

## 当前结论

M6C 已完成并通过独立停止点。真实 Game View 人工审阅确认 Self、Enemy 箭头/高亮、无效释放、五类宽高比、结束行动/下一轮清理及 Console；费用不足修订复测进一步确认红色费用卡仍可拿起跟手，但不显示出牌反馈、箭头或目标高亮，越线释放回弹且能量、手牌与轮次不变。独立复核按真实 `CardZones → Turn` 发布顺序补齐另一张牌先离手时的拖拽转换，定向 EditMode 53/53 与串行构建通过；`DEP-001` 现已 resolved，可以串行进入 M6D。

## 实现范围

- `HandCardInteraction` 把完整 Pointer 事件交给容器，继续使用既有 `delta / Canvas.scaleFactor` 跟手路径。
- `HandCardContainer` 复用 M6A 同一纯规则，刷新交互、费用颜色和合法目标；Self 显式使用玩家自身，Enemy 只提交 Presenter 命中的精确存活敌人 ID。
- Enemy 首次越线后冻结卡牌，箭头端点继续跟随指针；释放前先清理临时箭头/高亮，再经唯一 `BattleCommandQueue.Submit` 提交，避免同步重入留下残留表现。
- `BattleParticipantPresenter` 复用现有参与者世界 View/HUD 映射和 Encounter 顺序；每次从当前 `SpriteRenderer.bounds` 投影屏幕矩形，不维护第二份存活敌人或目标注册表。
- `ParticipantHudView` 只显示 Legal/Hovered 高亮；目标箭头和所有新增 Graphic 默认隐藏且 `raycastTarget=false`。
- `PlayCardCommand` 目标默认参数已移除；生产与测试调用方全部显式表达目标。
- UI 拿起可供性与规则 `CanStartInteraction` 分离：只有精确 `InsufficientEnergy` 会继续允许视觉拖动；出牌反馈、Enemy 瞄准、释放 resolver 与队列提交仍要求规则许可，因此费用不足释放只回弹并保持零权威写入。
- 卡区先于能量发布时，纯 `HandCardDragTransitionPolicy` 一次返回保留/取消、是否排除重排、下一拖拽阶段及需要清理或重建的表现；`RebuildCards` 与 Turn/生命刷新直接消费同一结果。另一张牌离手时保留当前拖拽并排除该 View 的手牌重排，随后由 Turn 把它降级为 `VisualOnly` 并清除目标表现；被拖牌自身离手仍取消。

## TDD 与回归证据

| 检查 | 结果 |
|---|---|
| 纯屏幕目标选择 | 覆盖矩形外空结果、重叠候选中心最近、同距保留 Encounter 首项 |
| Self / Enemy 释放 resolver | 覆盖 Self 越线自动 Actor、Enemy 精确候选、空白/非法释放空结果 |
| Presenter 目标选择 | 覆盖合法存活目标、缺失 View/HUD 安全、结束选择清理高亮 |
| 箭头与 Overlay | 覆盖默认隐藏、Show/Hide、非 Raycast、从缩放父级脱离后仍保持隐藏与全屏 Overlay |
| Prefab 合约 | 覆盖 BattleHand 引用箭头、BattleScene 静态依赖包含 Hand/Arrow/HUD、HUD 高亮默认隐藏且非 Raycast |
| 费用预览 | 覆盖正常费用色和精确 `InsufficientEnergy` 不可支付色 |
| 人工审阅前 M6C 定向 EditMode | **51/51 通过**，0 failed、0 skipped；任务 `3b8af941470b4933a86f2c098d95098d` |
| 费用不足拖动修订 TDD | 先由公开纯策略用例证明缺少交互模式；接线后覆盖 `Disabled / VisualOnly / Playable`、resolver 不产生目标与其他失败仍锁定。第一次独立审计补出拖动中能量下降的三态降级；第二次按真实发布顺序发现 CardZones 回调会抢先取消。为避免只测成员 helper，用一条 `CardZones → Turn → 被拖牌自身离手` tracer test 先得到 11 个缺失 transition interface 编译错误，再以纯转换 module 收敛保留、排除重排、降级、清表现和重建瞄准决策 |
| 修订后 M6 定向 EditMode | **53/53 通过**，0 failed、0 skipped；任务 `6de86cddde1d4cd7ac38cbf72431bb91` |
| 串行 solution build | **0 error**，12 条既有 Unity/R3/UniTask 依赖版本 warning |
| `git diff --check` | 通过 |

代码复审同时指出：纯 resolver/selector、完整拖拽 transition seam 与 View/Prefab 合约已经覆盖，但 Unity adapter 的 `HandCardContainer.HandleBeginDrag → CardZones 发布 → Turn 发布 → HandleDrag → HandleEndDrag` 物理跟手/回弹仍主要由真实 Game View 验收覆盖；在物理验收完成前不得把该缺口写成已验证。

## 运行时问题与修复

1. 第一轮 Bootstrap 自动诊断发现 `BattleHandUI` 根节点缩放为 0，导致嵌套箭头 `lossyScale` 为 0。将手牌根缩放恢复为 1，并用 Prefab 合约测试锁定。
2. 第二轮诊断发现箭头仍嵌套在带缩放/深度的 Screen Space Camera Canvas 中，屏幕起止点转换失败后被隐藏。现在由容器把序列化箭头实例提升为独立 `ScreenSpaceOverlay`，并统一管理隐藏与销毁；增加 Overlay 脱离父级回归测试。
3. 修复后自动探针得到 1920×1080 全屏 Overlay、可见箭身、非 Raycast 图形、精确敌人命中及 Legal/Hovered 高亮。该结果证明接线可运行，但不证明物理拖拽手感。
4. 费用不足修订的二次复核按权威写链发现：另一张牌执行时先同步发布 CardZones，旧 `RebuildCards` 会在能量 Turn 发布前无条件取消当前拖拽。最终以 `HandCardDragTransitionPolicy` 取代浅成员 helper：CardZones 中仍在手时输出保留并排除重排，紧随的 Turn 输出 `Playable → VisualOnly`、清出牌反馈/箭头/高亮；自身离手或其他禁用事实输出取消。

## 人工审阅回填与后期安排

| 审阅意见 | 归属与处理 |
|---|---|
| Enemy 卡越线后原地冻结会遮挡视野、显得僵硬 | M6 功能性冻结/箭头验收通过；最终聚焦锚点与 DOTween 属于 M9 与 `DEP-003/004`，需求见 `../10_communication/2026-08-02-battle-card-motion-feedback-brief.md`。Linear [LXX-6](https://linear.app/lxxr/issue/LXX-6) 后续已完成四张目标 PNG 的独立美术验收，工单不交付交互或资源接线；这些文件及 Unity 后续生成的 Meta 未纳入 M6 Prefab、Addressables 或物理验收。 |
| 目标高亮正确，但没有伤害/格挡/状态 Effect | 计划明确要求 M6 不执行 `effect_bindings`，真实结算属于 M7 与 `DEP-009`。仓库路线与依赖已登记；本次 Linear 授权仅限 LXX-6 美术资源 Issue，不另建功能工单。 |
| 费用不足卡不应完全钉死 | 属于当前 M6C 可供性修订，已实现为“可拿起/拖动，但无出牌反馈、箭头、高亮、目标或提交”，并已通过下方物理复测。 |
| 进入 EnemyAction 后剩余手牌立即消失 | M2/M4 权威规则要求 `EndPlayerActionCommand` 将剩余手牌移入弃牌堆，不能继续展示为可交互手牌或复制假状态；从旧 View 到弃牌堆的可见过渡保留在 M9 文档，不属于 LXX-6 美术资源交付。 |

## Addressables 与 Bootstrap 证据

- 执行 `TinySpire/Addressables/Build Local Content` 成功；最新报告 `BuildError` 为空，`BuildResultHash=92b5408c9884e0ed9922ed56f9c10ffa`，耗时约 9.976 秒。
- BattleScene 继续使用完整稳定地址 `Assets/Scenes/BattleScene.unity`；静态依赖合约确认场景依赖 `BattleHandUI.prefab`、目标箭头 Prefab 与 `ParticipantHudView.prefab`。箭头作为嵌套静态依赖，不新增独立 Addressables 地址接口。
- 从 Bootstrap 自动进入 Loading 再到 BattleScene，观察到 5 张手牌与 3 个参与者 HUD；Console 未见 Error、InvalidKey 或 VContainer 错误。
- 诊断截图位于被忽略的 `TinySpire/Temp/M6CVerification/`，未写入生产资产或 Git 范围。

## 真实 Game View 物理验收

- [√] Self 卡越线释放，无需点击玩家即可提交自身；手牌与能量只按权威结果变化。
- [√] Enemy 卡首次越线后功能性冻结，箭头端点继续跟随物理指针；最终聚焦表现留给 M9，LXX-6 已交付的四张目标 PNG 未纳入本次 M6 验收。
- [√] 左、右敌人分别显示合法/悬停高亮；精确 `TargetId` 由 resolver/队列测试覆盖，M6 不以尚未实现的 Effect 冒充命中反馈。
- [√] 释放到空白、玩家或无效区域时不提交，卡牌回弹，能量、手牌和回合不变。
- [√] 初始能量耗尽后，剩余卡牌费用保持不可支付颜色；卡牌可被拿起/拖动，但不显示出牌反馈、箭头或目标高亮，越线释放仍回弹，且权威序号、能量、手牌和轮次不变。
- [√] 16:7、16:9、16:10、16:11、16:14 下箭头、屏幕命中和高亮均对齐。
- [√] 结束行动会按权威规则把剩余手牌移入弃牌堆；进入下一轮后能量/手牌恢复，箭头和目标高亮无残留。旧 View 的弃牌过渡只由 M9 文档承接。
- [√] 完整物理序列（含费用不足修订复测）的 Console 无 Error、InvalidKey 或 VContainer 错误。

## 排除项与保护结果

- 未修改 `BattleScene.unity`、`CardView.prefab`、角色 Prefab、ProjectSettings、Physics、asmdef、HybridCLR、Luban 或 Localization。
- 未执行 `effect_bindings`，未写生命、格挡、力量、状态、死亡或胜负；未提前实施 M7～M9。
- 未 commit、未 push，且保留启动前既有脏工作区与未跟踪 M5 retrospective。
