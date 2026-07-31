---
created: 2026-07-06
updated: 2026-07-31
---

# Daedalus · 代码决策记录

> 代码级决策，补充 `Hermes_Pegasus/design/decisions.md`（玩法级决策）。

## CD-001：LoadingScene 采用最短展示时间

**问题**：资源准备很快时，LoadingScene 会一闪而过，无法形成稳定过渡。

**选择**：LoadingScene 完成切入后开始真实时间计时，目标场景加载前保证累计展示至少 1 秒；内容准备耗时计入展示时间。

**理由**：快加载时补足视觉过渡，慢加载时不叠加无意义的固定等待；真实时间延迟避免受到游戏暂停或 `Time.timeScale` 的影响。

**影响**：`TinySpire/Assets/Scripts/Core/SceneFlowService.cs` 的所有带 LoadingScene 的场景切换。

## CD-002：BattleScene 基础选牌 UI 复用 UGUI CardView

**问题**：BattleScene 需要先展示并选择卡牌，但当前切片尚未实现卡牌数据、ViewModel 和出牌链路。

**选择**：在现有 UGUI Canvas 下直接实例化 `CardView.prefab`，由场景级 `Toggle` + `ToggleGroup` 提供单选表现；选择结果暂时只存在于 UGUI 控件状态，不建立运行时业务模型。

**理由**：`CardView` 已经是 UGUI 预制体，复用它可以用最小场景改动先验证布局和交互；同时避免把 UI 构造任务扩成数据与战斗逻辑实现。现有 Canvas 继续使用 Screen Space - Camera，并把平面距离设为 1，使 UI 位于背景 Sprite 之前。

**影响**：`TinySpire/Assets/Scenes/BattleScene.unity`。后续接入动态手牌时，需要由运行时层生成卡牌并把 UI 选择同步到明确的 ViewModel/命令入口。

## CD-003：引入 DOTween 作为 UI 补位/过渡动画方案

**问题**：手牌 UI 需要悬停抬起、拖拽回弹、重排补位等平滑过渡，需要一个缓动方案；项目此前未正式引入 DOTween（`Hermes_Pegasus/STATUS.md` 中曾是 Open Question）。

**选择**：使用 DOTween（用户已将 DOTween/DOTweenPro 导入 `TinySpire/Assets/Plugins/Demigiant/`）驱动手牌 UI 的悬停与补位动画：悬停抬起/落下约 0.15s、`Ease.OutBack`；手牌重排补位约 0.2~0.25s、`Ease.OutQuad`/`Ease.OutCubic`；每张卡自行维护 Tween 引用，新动作前先 `Kill` 旧 Tween。

**理由**：DOTween 是 UI 缓动最成熟的现成方案，避免自建 easing/可中断的补位小工具；项目技术栈本来就把 DOTween 列入过考虑范围。

**影响**：`TinySpire/Assets/Scripts/UI/` 下新增的手牌交互脚本；不影响计算层、状态层。详见 `plans/2026-07-29-battlescene-hand-ui-sts-style.md`。

## CD-004：手牌交互模型由单选 Toggle 替换为悬停/扇形/拖拽视觉

**问题**：CD-002 的 `Toggle` + `ToggleGroup` 单选高亮与杀戮尖塔式的悬停抬起 + 拖拽跟手交互互斥，无法共存。

**选择**：移除 `BattleScene` 中的 `Toggle`/`ToggleGroup` 组件与选中高亮面板，替换为：悬停抬起（位移+旋转归零+缩放+`Canvas.sortingOrder` 临时提升）、扇形布局（基于归一化位置 `t` 的旋转/下沉曲线）、拖拽跟手视觉（其余卡重排填空，松手不打出则按原顺序回弹）。仍然不做出牌判定/合法目标选择，复用 `CardView.prefab` 不做本体改动。

**理由**：新交互模型是本轮明确的产品方向（杀戮尖塔式手牌体验），与旧的单选语义无法叠加；CD-002 记录保留作为历史决策，不删除，仅在此说明已被本决策替换。

**影响**：`TinySpire/Assets/Scenes/BattleScene.unity`；`TinySpire/Assets/Scripts/UI/` 下新增手牌容器与单卡交互脚本。详见 `plans/2026-07-29-battlescene-hand-ui-sts-style.md`。

## CD-005：新增 HandState 收回手牌数量的数据归属权

**问题**：`handCount` 一直是 `HandCardContainer`（UI 组件）自己持有并自增自减的字段，一旦接入“出牌”判定就需要修改这个数量，会让 UI 组件变成事实上的权威运行时状态持有者，与三层架构（计算/状态/时序/UI 分层）冲突。

**选择**：新增 `HandState`（纯 C# 类，不依赖 `MonoBehaviour`/Unity API）：内部持有手牌卡牌 ID 列表（本轮仍是占位 ID），对外只暴露只读快照、`PlayCard(int cardId)` 方法、一个 `event Action` 变化通知。`HandCardContainer` 不再自己持有/自减 `handCount`，只订阅 `HandState` 的变化去重建视觉；出牌判定成立时调用 `HandState.PlayCard`。

**理由**：把手牌数据的归属权从 UI 里收回，是后续真正接入 Luban 数据源、Effect 系统、BattleState 的必要前提；现在不做，将来所有涉及手牌数量的改动都要同时改 UI 和数据两处。`event Action` 是过渡形态，暂不引入 R3，避免在状态层设计未定时提前锁定响应式方案。

**影响**：`TinySpire/Assets/Scripts/UI/Battle/Hand/` 下新增 `HandState`；`HandCardContainer` 改为订阅它而非自持状态。详见 `plans/2026-07-29-battlescene-drag-to-play-minimal.md`。

## CD-006：拖拽出牌用可调 Y 轴阈值判定，不做目标/费用检查

**问题**：现有拖拽跟手视觉松手无条件回弹，没有“怎么算打出”的判定；同时打出后是否需要选目标、扣费用、播放过渡动画都还没有数据支撑（怪物/玩家锚点、能量系统、Effect 系统均未落地）。

**选择**：加一条可调的 Y 轴出牌线阈值，松手时对比被拖卡当前位置，超过判定为打出，否则回弹。判定为打出后：不做目标选择/检测（预留恒为 `null` 的 `targetId` 字段）、不做费用检查、`HandCardVisual` 立即 `Destroy`、拖过线时只做未经美术设计的最简占位视觉反馈。四处均在代码中用 `TODO(DEP-001)`~`TODO(DEP-004)` 标记为待后续依赖项解决。

**理由**：目标检测方式依赖尚未确定的怪物/玩家锚点形态（UI 还是 World Space），费用系统、过渡动画依赖尚未落地的能量系统与 Effect 系统；在这些前置条件明确前设计会建立在错误地基上。用带 ID 的 TODO 标记，方便未来按依赖项逐条替换实现，而不是现在做投机性设计。

**影响**：`TinySpire/Assets/Scripts/UI/Battle/Hand/HandCardContainer.cs`、`HandCardVisual.cs`。详见 `plans/2026-07-29-battlescene-drag-to-play-minimal.md`。

## 决策模板

## CD-024：可旋转卡牌插图使用 Stencil Mask 裁剪

**问题**：`CardContent` 在手牌扇形布局中旋转，但其插图区的 `RectMask2D` 只按轴对齐矩形裁剪；旋转卡片时会裁掉插图边缘并露出下层 `CardBase` 的灰色区域。

**选择**：`CardView.prefab` 的 `IllustrationMask` 保留现有 `Image`、尺寸与子层级，将裁剪组件从 `RectMask2D` 替换为 `Mask`，并设置 `m_ShowMaskGraphic: 0`。子节点 `Illustration` 继续作为唯一被裁剪的卡图。

**理由**：Stencil Mask 使用遮罩 Graphic 自身的变换写入模板缓冲，能与 `CardContent` 一起旋转；它只增加一个局部 UI 裁剪层，不改变卡牌布局、交互、贴图、运行时数据或资源地址。

**影响**：`TinySpire/Assets/Arts/Runtime/Card/Prefab/CardView.prefab`。本次不修改手牌扇形旋转算法、场景、C# 脚本、卡图资源、Luban 表或 Addressables 配置；需要在当前 Unity Editor 中重建本地 Addressables 内容并进行旋转卡人工验收。

```markdown
## CD-XXX：决策标题

**问题**：一句话描述

**选项**
- A: ...
- B: ...

**选择**：X

**理由**：为什么选 X。

**影响**：哪些模块/文件受影响。
```

## CD-007：新增最小纯 C# 状态机 Core

**问题**：项目需要一个可跨多帧运行的状态机基础，但游戏领域状态、事件队列、异步调度和嵌套协议尚未确定；提前把这些语义放进 Core 会制造不必要的复杂度。

**选择**：新增 `TinySpire.Core.StateMachine` 下的最小同步状态机：状态提供 `Enter`、`Tick(TimeSpan)`、`Handle(event)`、`Exit` 四个生命周期入口；状态通过 `StateTransition` 返回“保持当前状态”或“切换到新状态”；调用方负责 Update/Tick 驱动和事件排队；`Stop()` 终止当前实例，重新运行时创建新实例。

**暂不包含**：Context 抽象、内部事件队列、异步/定时器、嵌套状态协议、并行状态、游戏数据查找和领域事件。

**理由**：先保留一个小而明确的接口，验证跨帧保持、同步事件转换和生命周期顺序；后续只有在真实用例证明需要时，才通过外部组合或新的决策扩展能力。

**影响**：新增 `TinySpire/Assets/Scripts/Core/StateMachine.cs` 及其 Unity 元数据；不接入现有游戏代码，不改变 HandState、配置服务或 BattleScene。

## CD-008：场景级服务用挂载在场景内的 LifetimeScope，不由代码动态创建/销毁

**问题**：BattleScene 未来需要回合调度器、抽牌堆、弃牌堆等战斗局内运行时服务，需要一个专属的 DI 容器边界。此前一版方案尝试由 `SceneFlowService` 用 `CreateChild` 动态创建/持有每个场景的子 `LifetimeScope`（验证记录见已归档的 `99_archive/2026-07-30-scene-child-scope.md`），但该方案已被用户撤回、代码已还原，需要重新给出结论。

**选择**：场景级服务（战斗/地图/商店等）的 `LifetimeScope` 直接作为 GameObject 挂载在对应场景（或场景引用的 prefab）里，`parentReference` 按类型指向根 Scope（现阶段是 `Bootstrap`）。生命周期完全依赖 Unity 场景加载/卸载：YooAsset 以 `LoadSceneMode.Single` 切换场景时销毁旧场景全部 GameObject，其中的场景级 `LifetimeScope` 随之 `Dispose`；`SceneFlowService` 不需要知道、也不需要修改任何代码来配合这件事——VContainer 的父级解析（`LifetimeScope.FindParent`）在 `Awake` 时按类型全局查找已加载的父 Scope 实例，和“这个场景是怎么被加载出来的”完全解耦。

**理由**：Unity 场景系统已经免费提供“场景卸载 = GameObject 销毁 = 容器 Dispose”的清理时机，手动管理子 Scope 创建/销毁只是重新发明这个机制，还引入额外的时机竞态风险；符合 AC-002 不做投机性抽象。

**影响**：未来新增战斗/地图/商店场景时，直接在场景里放置对应的 `LifetimeScope` 子类组件即可；不涉及 `SceneFlowService.cs` 的任何改动。已归档 `06_testing/2026-07-30-scene-child-scope.md` → `99_archive/2026-07-30-scene-child-scope.md`（该验证记录描述的方案已撤回，且原文件头的 `source: CD-008` 是错误引用）。

## CD-009：存档层（RunScope）需要显式生命周期管理服务，场景挂载方案不适用（前瞻，未实现）

**问题**：未来加入“地图”后会形成 Game → 存档 → 地图 → 具体事件（战斗/奇遇/商店）的结构。CD-008 的“场景挂载”方案只适用于生命周期恰好等于单一场景的层；存档层的数据（卡组、遗物、金钱、当前地图进度）要跨越地图/战斗/商店的多次场景切换持续存活，没有天然对应的场景加载/卸载事件。

**选择**：这一层（暂命名 `RunScope`）不挂载在任何单一场景里，而是由一个新的、与 `SceneFlowService` 平级但职责分离的显式流程服务（暂命名 `RunFlowService`）在“开始新游戏/读档”时创建（例如从 `Bootstrap` `CreateChild` 出一个 `DontDestroyOnLoad` 的子 Scope），在“本局结束/返回主菜单”时显式 `Dispose`。届时事件层场景 Scope 的 `parentReference` 改为指向这一层，而不是 `Bootstrap`。“三张地图”本身不需要单独一层 Scope，只作为 `RunScope` 内 `RunState`（纯 C# 数据）的字段存在，除非某张地图确有独立的运行时服务需求。

**理由**：DI Scope 层级应该对应生命周期边界，而不是照搬数据模型的层级；`RunScope` 是唯一一处生命周期不对齐任何单一场景、因此必须手动管理创建/销毁的层，其余层继续用 CD-008 的场景挂载方案。

**影响**：本轮不创建 `RunFlowService`、`RunLifetimeScope` 或 `RunState` 代码（按 AC-002，不做投机性抽象），仅作为未来“存档/地图”功能立项时的架构前提记录。

## CD-010：BattleState 作为战斗参与者与目标解析的唯一事实源

**问题**：卡牌效果需要按稳定 ID 解析玩家/敌人目标；若同时维护“全部参与者”“存活参与者”“玩家列表”“敌人列表”等多个可变集合，死亡、加入战斗或换阵营时会出现同步遗漏与状态分叉。

**选择**：`BattleState` 内只持有一个权威的 `CombatantId → CombatantState` 字典，并只以 `IReadOnlyDictionary<CombatantId, CombatantState>` 暴露它。当前不预置玩家、敌人或存活视图；出现真实目标规则后再从该字典的值按需派生。`TryGetCombatant` 直接委托给该唯一事实字典。`PlayerCombatantState` 与 `EnemyCombatantState` 仅继承共同的 `CombatantState`，不在父类中预置牌组、AI 或场景对象字段。

**理由**：参与者的稳定 ID 已是目标解析的领域主键，因此 ID 到参与者的映射本身就是最贴合领域的唯一事实，并能直接完成查询。避免同时保留 `List` 与字典；后续只有在“顺序”成为明确业务事实时，才单独建模其语义，不能依赖字典遍历顺序。

**影响**：新增 `TinySpire/Assets/Scripts/Battle/CombatantState.cs`、`BattleState.cs` 与 `TinySpire/Assets/Editor/Tests/BattleStateTests.cs`。不接入 `HandState`、卡牌实例、效果、敌人意图、能量、场景锚点或 DI 注册。

## CD-011：战斗配置只描述静态模板，不镜像运行时状态

**问题**：玩家、敌人、卡牌需要可编辑、可生成的配置来源；若把当前生命、存活状态、手牌或卡牌实例等局内可变值写进表格，会和 `BattleState`、`HandState` 形成两份事实。

**选择**：以 Luban 表定义六类静态模板：`battle.Hero`、`battle.Enemy`、`battle.Deck`、`battle.Card`、`battle.CardEffect`、`battle.Encounter`；目标规则、效果类型与可修改属性定义为 `battle.TargetRule`、`battle.EffectType`、`battle.Attribute` 枚举。表之间仅保存模板 ID 关系，运行时再由 `BattleState`、未来的卡牌实例和手牌状态实例化并持有可变数据。

**理由**：模板 ID、基础生命、基础力量、费用、效果数值和遭遇组成是可复用的设计事实；`CombatantId`、当前生命、`IsAlive`、手牌/抽牌/弃牌堆、临时费用、升级、敌人意图和控制者则只在某一局战斗中成立，不能回写为配置状态。

**影响**：`DataTables/Datas/__tables__.xlsx`、`__enums__.xlsx` 与六个 `battle.*.xlsx` 数据源；Luban 输出 `TinySpire/Assets/Scripts/Core/Generated/Config/battle/` 和 `TinySpire/Assets/GameData/battle_*.json`，与 `ConfigService` 的 YooAsset 地址一致。重新生成数据后必须重建 YooAsset `Main` 内置包，使新 JSON 进入离线清单；不修改目标解析或效果执行代码。

## CD-012：Luban 表数据以资源路径加载，并在生成后重建离线清单

**问题**：`ConfigService` 通过 `Assets/GameData/<table>.json` 加载表数据；若 Luban 输出到 `StreamingAssets/GameData`，或仅刷新 Unity 而不重建 YooAsset `Main` 包，离线清单不会包含新表，运行时会报资源地址无效。

**选择**：Luban 统一输出至 `TinySpire/Assets/GameData`；保留 `ConfigService` 的资源路径加载方式。每次生成或变更 `Assets/GameData` 后，用现有 `Main` / `BuiltinBuildPipeline` 重建内置包。

**理由**：当前 `Main` 包未启用以自定义地址替代资源路径，运行时清单以资源路径为定位键；维持这条既有约定比改写整个资源定位策略影响更小。资源收集器已经覆盖 `Assets/GameData`，重建内置包即可更新离线清单。

**影响**：`DataTables/gen.bat`、`Assets/GameData/`、`Assets/StreamingAssets/yoo/Main/`。生成配置并不自动更新 YooAsset 清单，构建是必要的后续步骤。

## CD-013：卡牌模板用效果 ID 列表表达复合效果

**问题**：单一 `effect_id` 只能表达一张卡的一项效果，无法完整描述战士 `Bash` 的“造成伤害并施加易伤”。

**选择**：`battle.Card` 将 `effect_id` 改为 `effect_ids` 数组。新增静态枚举项：`TargetRule.Enemy`、`EffectType.DealDamage`、`EffectType.GainBlock`、`EffectType.ApplyVulnerable`，并以 `Attribute.None` 表示不涉及属性修改的效果。

**理由**：复合效果是卡牌模板的稳定设计事实；以 ID 列表保留执行顺序，既能表示当前 STS 初始卡组，也不需要提前实现运行时效果执行器。

**影响**：战士初始卡组为 5×Strike（6 伤害）、4×Defend（5 格挡）、1×Bash（8 伤害、2 易伤）；`game-config.json` 初始手牌维持 STS 对标的 5。运行时伤害、格挡、易伤结算仍未实现。

## CD-014：BattleSession 只把静态模板实例化为运行时事实

**问题**：Luban 战斗表已经能加载，但 `BattleState` 与 `HandState` 仍由 UI 或测试手工创建；同时初始卡组含 5 张相同 Strike，若继续把模板 ID 当运行时卡牌 ID，视图查找和单卡移除会把重复模板混为一张。

**选择**：新增场景级 `BattleSession` 作为配置到运行时的装配边界：从 `Hero`、`Encounter`、`Enemy`、`Deck` 与 `GameConfig` 创建 `BattleState` 和 `HandState`。每张运行时卡牌使用唯一 `CardInstanceId`，并以 `TemplateId` 引用静态 `Card`；不复制名称、费用或效果字段。`BattleLifetimeScope` 注册该会话，`HandCardContainer` 只消费会话中的手牌状态和静态卡牌模板。

**临时限制**：正式牌堆尚未建立，初始手牌暂按卡组顺序取前 `initialHandCount` 张，由 `DEP-006` 明确标记；这不是正式洗牌/抽牌规则。

**理由**：配置模板与运行时实例是不同身份域。把实例身份分离后，重复卡牌可独立移动和打出；同时 `BattleSession` 只负责一次性实例化，不保存配置镜像，不破坏 `BattleState`/`HandState` 的唯一事实归属。效果器仍可在目标、费用和牌堆边界稳定后单独实现。

**影响**：新增 `TinySpire/Assets/Scripts/Battle/BattleSession.cs`；扩展 `BattleState`/`CombatantState`、`HandState`、`BattleLifetimeScope` 和手牌 UI。未修改配置表、生成数据、效果执行、牌堆、敌人行为或回合流程。

## CD-015：规则随机使用实例流，卡牌区域由一个聚合持有

**问题**：`UnityEngine.Random` 使用全局静态状态，视觉或其他系统多消耗一次随机值就可能改变洗牌和敌人行为；同时只保存当前手牌无法表达抽牌、弃牌、消耗和空堆重洗，也无法保证一张实例只存在于一个区域。

**选择**：新增 `GameRandom`，封装项目现有 `Unity.Mathematics.Random` 的实例状态、`NextInt` 与 Fisher–Yates `Shuffle`。`CardZoneState` 持有全部 `CardInstanceState` 定义，以及抽牌堆、手牌、弃牌堆和消耗区四个互斥有序列表；洗牌随机流由 `CardZoneState` 独占。`BattleSession` 用战斗种子创建该随机流，先洗牌再抽取初始手牌。

**理由**：实例随机流不会被加载界面或表现随机调用推进，`uint State` 可以直接作为后续随机序列的唯一事实保存和恢复；项目不需要自研 PRNG。区域归属只由四个列表表达，不在卡牌实例上再保存 `Zone`，因此移动入口可以原子维护互斥关系，计数按列表派生。

**兼容与后续**：`UnityEngine.Random` 仍可用于不影响规则的纯视觉随机。地图、奖励和敌人行为后续分别创建独立 `GameRandom`，不得共享洗牌流。当前 BattleScene 种子由 Inspector 提供；Run 生命周期建立后由 `RunState` 派生/恢复，见 `DEP-007`。

**影响**：新增 `TinySpire/Assets/Scripts/Core/GameRandom.cs`、`TinySpire/Assets/Scripts/Battle/CardZoneState.cs` 及测试；`BattleSession` 和手牌 UI 改读 `CardZoneState`，解决 CD-014 的临时前 N 张限制与 `DEP-006`。未实现效果器、目标、费用或回合流程，也未修改表格和资源包。

## CD-016：本地资源管线从 YooAsset 迁移到 Addressables

**问题**：项目同时计划使用 Unity Localization，而继续保留 YooAsset 会形成两套 catalog、构建和生命周期规则；现有 YooAsset 仅承担本地场景与 GameData 加载，尚未形成必须保留的远程更新资产。

**选择**：移除 YooAsset 包、初始化服务与扫描/收集设置，由 Unity Addressables 2.9.1 统一加载场景、Luban JSON 和 Localization 资源。场景及 GameData 的地址继续使用完整 `Assets/...` 路径；`AddressableAssetService` 和 `SceneFlowService` 在模块内部管理 handle，不向业务层泄漏。当前只构建本地内容，不配置远程 catalog。

**理由**：一个资源管线即可覆盖当前全部需求，并与 Unity Localization 的原生 Addressables 集成保持一致。保留稳定地址把迁移影响限制在资源边界，不要求同步改写表格或运行时领域模型。

**修订**：本决策替代 CD-012 中“生成后重建 YooAsset Main 包”的当前流程；`Assets/GameData` 仍是 Luban JSON 的正确输出位置，但后续动作改为 `TinySpire/Addressables/Build Local Content`。CD-008 的场景生命周期结论不变，只是场景加载实现由 Addressables 承接。

**影响**：`Packages/manifest.json`、`packages-lock.json`、`Bootstrap`、`GameLauncher`、`ConfigService`、`SceneFlowService`、`AddressableAssetService`、`AddressableAssetsData`、编辑器构建工具与 `AGENTS.md`。

## CD-017：卡牌文本使用 Unity Localization，效果引用升级为命名绑定

**问题**：CD-013 的 `effect_ids` 可以表达复合效果，却无法把“哪个效果提供 damage/vulnerable 参数”稳定交给本地化模板；自行维护 JSON 模板又会重复 Unity Localization 已有的 locale、fallback、Smart String 和 Addressables 集成。

**选择**：`battle.Card` 使用 `name_i18n_key`、`description_i18n_key` 和有序 `CardEffectBinding[]`；每个绑定保存 `argument_key` 与 `effect_id`。文本后端使用 Unity Localization 1.5.12 的 `Battle Cards` String Table，初始 locale 为 `en`、`zh-CN`，说明条目使用 Smart Strings。`LocalizationSettings.SelectedLocale` 是唯一语言事实，`LocalizationService` 只做薄封装和变化通知。

**理由**：命名绑定同时保留复合效果顺序和可读模板参数。卡牌实例只保留身份，格式化器从静态效果、当前参与者事实和当前 locale 即时派生文字，避免产生第二份展示状态。

**修订**：本决策以 `CardEffectBinding[]` 替代 CD-013 的裸 `effect_ids` 字段；CD-013 对“复合效果是有序静态事实”以及“不提前实现效果执行器”的结论继续有效。

**影响**：Luban bean/card 表、生成配置、Unity Localization 资源、`LocalizationService`、`CardTextFormatter`、`CardValueCalculator`、手牌 UI、编辑器校验与 M2A 测试。仍不实现 Effect 执行、费用、目标选择或状态施加。

## CD-018：状态通知改用 R3 只读事件流，卡牌视图拥有其展示引用

**问题**：`event Action` 需要 UI 手写订阅/反订阅，且 `HandCardContainer` 曾以 `GameObject` 搜索子 `Text` 并按对象名写入卡牌内容，使容器同时掌握卡牌预制体的内部结构和展示职责。

**选择**：`BattleState`、`CardZoneState` 与 `LocalizationService` 私有持有 `Subject<Unit>`，对外只暴露 `Observable<Unit>`；UI 使用 `Subscribe(...).AddTo(this)` 让订阅随 `HandCardContainer` 销毁。`CardView.prefab` 根节点持有 `HandCardVisual`，并序列化 Canvas、CardContent 与四个文本控件引用；容器只创建/排布视觉并调用 `HandCardVisual.Bind`，不再搜索子节点或直接写文本。

**理由**：R3 流只通知“事实已改变”，字典、卡区、语言与格式化文本仍分别从其唯一事实源读取或即时派生，因此不引入第二份状态。将预制体结构收回视图组件后，容器不再依赖 `TitleText` 等对象名，卡牌显示的所有权也更清晰。

**影响**：`BattleState`、`CardZoneState`、`LocalizationService`、`HandCardContainer`、`HandCardVisual`、`CardView.prefab` 与对应 EditMode 测试；本轮不引入 Effect 执行、费用结算、目标选择、敌人行为或完整回合流程。

## CD-019：运行时数据统一使用 `Data` 尾缀，并以 R3 属性公开可绑定事实

**问题**：`*State` 同时可能指局内数据、状态机状态或状态模式对象，领域含义含混。CD-018 又将 R3 降格为 `Subject<Unit>` 失效通知：UI 必须收到泛化事件后重新遍历聚合，不能直接绑定具体运行时值。

**选择**：局内数据统一以 `Data` 结尾：`CombatantData`、`PlayerCombatantData`、`EnemyCombatantData`、`BattleCombatantsData`、`CardInstanceData`、`CardZoneLayoutData`、`BattleCardZonesData`；文件名与类型名同步。`Health`、`Strength` 等标量事实由私有 `ReactiveProperty<T>` 唯一持有，并只读公开；四个卡区的完整有序归属由 `ReactiveProperty<CardZoneLayoutData>` 原子发布。UI 订阅手牌布局、玩家力量与 Locale 的实际值，不再订阅泛化 `Changed`。

**理由**：`Data` 说明对象是本局运行时事实，给未来 `BattleStateMachine` 等时序/状态模式类型保留清晰术语。R3 属性成为事实本身或完整原子快照，而不是第二份镜像；订阅方因此有直接、窄且可测试的绑定点。

**修订**：本决策替代 CD-018 中 `Subject<Unit>`/`Observable<Unit>` 的通知方案；CD-018 关于 `HandCardVisual` 拥有预制体展示引用的结论继续有效。

**影响**：战斗运行时数据、`BattleSession`、卡牌格式化器、手牌 UI、编辑器测试、文件名和领域术语文档。本轮仍不实现 Effect 执行、费用、目标、敌人行为或状态机。

## CD-020：Unity Localization 表资源是翻译文本的唯一来源

**问题**：`LocalizationBuildTools` 曾在 C# `LocalizedEntry[]` 中保存中英文卡牌与关键词文本，并在执行菜单时回写 String Table。翻译内容同时存在于 C# 与 Localization 资源中，新增或修改卡牌时容易双写漂移，且一次校验操作可能覆盖编辑器内的翻译。

**选择**：删除 `LocalizedEntry[]`、配置/补全菜单及所有写表辅助方法。`Assets/Localization` 下的 Unity String Table 资源独占翻译文本；`LocalizationBuildTools` 仅以 `TinySpire/Localization/Validate Battle Card Text` 校验所需语言、key、Smart String、参数与效果引用。

**理由**：内容创作应在 Unity Localization 编辑器内完成，运行时也只经 `LocalizationService` 读取同一资源。校验器保留结构性约束，但不持有或生成任何本地化正文，避免第二事实源。

**影响**：新增/修改卡牌 i18n key 后，先更新 `Battle Cards` 的每个 locale，再运行校验与 Addressables 本地构建；本轮未更改任何 String Table 条目、表格或运行时效果执行逻辑。

## CD-021：i18n Excel 是本地化内容的编辑源，String Table 是生成的运行时资源

**问题**：CD-020 移除 C# 硬编码正文后，若仍要求内容人员直接编辑 Unity String Table，卡牌表与翻译源分散在不同编辑器中；同时 String Table 被 Addressables 直接加载，必须避免把它和 Excel 当成两个可随意编辑的权威来源。

**选择**：新增 `DataTables/Datas/i18n.xlsx`，固定 `i18n` 工作表与 `key`、`en`、`zh-CN`、`smart` 列。`I18nExcelReader` 仅读取项目约定的 OpenXML 文本工作簿；`TinySpire/Localization/Import Battle Card Text from Excel` 将其同步到 `Battle Cards` 的 `en`、`zh-CN` 表并写入 Smart String 标记。校验菜单同时验证 Excel 的结构、当前卡牌/关键词覆盖范围和导入结果一致性。

**理由**：Excel 集中承担可编辑文本事实，Unity Localization 继续承担 locale、fallback、Smart String 与 Addressables 运行时集成。导入不是运行时依赖，运行时仍只通过 `LocalizationService` 读取 String Table；因此没有引入第二条运行时加载链路。

**影响**：本地化修改流程变为：编辑 Excel → 导入 Unity Localization → 校验 → 重建 Addressables。本轮不把 i18n.xlsx 加入 Luban 配置定义，不生成 i18n JSON，也不改变卡牌配置、运行时文本格式化或效果执行边界。

## CD-022：配置与本地内容发布使用单一编辑器入口

**问题**：修改静态表格或 `i18n.xlsx` 后，Luban 生成、Unity 资源刷新、本地化导入/校验和 Addressables 构建分散为多个菜单和命令，容易漏掉任一步，导致 JSON、String Table 与本地 catalog 不一致。

**选择**：新增 `TinySpire/Build/Sync and Build All`。该入口固定依次执行与 `DataTables/gen.bat` 相同参数的 Luban 生成、`AssetDatabase.Refresh`、i18n Excel 导入及校验、Addressables 本地内容构建；任一步抛错即中止，不继续发布后续内容。细粒度菜单保留给诊断使用。

**理由**：表格和本地化变更的正确交付链路是稳定且顺序固定的。将顺序与失败边界收敛到编辑器工具，能降低漏构建风险，同时不改变配置、Localization 或运行时加载的唯一事实来源。

**影响**：内容人员修改 `DataTables/Datas/` 下任何配置表或 i18n.xlsx 后，只需执行一个菜单。该入口只构建本地 Addressables 内容，不引入远程 catalog、效果执行或新的运行时加载链路。

## CD-023：参与者视觉从模板地址生成，HUD 只绑定运行时事实

**问题**：BattleScene 已能从配置创建 `CombatantData`，但玩家和敌人尚无场景视图。若在场景预摆角色、在 UI 内按模板 ID 分支或为 HUD 镜像生命/力量，会使遭遇数量、资源替换与运行时事实失去单一来源。

**选择**：`battle.Hero` 与 `battle.Enemy` 使用 `name_i18n_key` 和 `view_prefab_address` 描述静态名称和完整 Addressables Prefab 地址。`BattleParticipantPresenter` 是场景生命周期编排者：它从 `BattleSession.Combatants` 按 `CombatantId` 创建和释放世界空间角色与对应 HUD View；角色与 HUD 都不持有生命、力量或阵营的可变副本。`BattleSession.EnemyCombatantIdsInEncounterOrder` 是由 `Encounter.enemy_template_ids` 实例化得到的明确顺序事实，供布局使用，不能从参与者字典的枚举顺序反推。Prefab 通过 `Addressables.InstantiateAsync` 创建，销毁时使用 `Addressables.ReleaseInstance`；M3A 世界角色 Prefab 必须包含 `SpriteRenderer`。地址、加载或 Prefab 合约错误直接抛出，不提供降级显示。

**理由**：模板决定可复用的美术和名称，`CombatantData` 决定本局事实，场景 Presenter 只协调两者的生命周期。Addressables 地址留在表中后，扩展英雄/敌人外观不需要改 UI 代码；直接失败可让构建/发布问题尽早暴露，而不让画面与战斗事实脱节。

**影响**：M3A 只展示名称、生命和非零力量，支持一名玩家和一至三名按 Encounter 配置顺序从右向左布局的敌人。格挡、状态、意图、能量、回合、死亡表现和胜败覆盖层不在本决策实现，分别等待 M3B-M3E 的前置事实。详见 `plans/2026-07-30-battlescene-participant-views.md`。

## CD-025：卡牌模板持有牌面稳定地址，手牌 View 管理加载生命周期（地址表示已由 CD-026 替代）

**问题**：牌面 PNG 已进入项目，但卡牌模板没有资源地址。若按模板 ID 在 UI 中硬编码 Sprite、把所有图片直接序列化到场景，或让运行时卡牌实例保存资源对象，都会重复静态事实并使新增卡牌需要修改 UI 代码。

**选择**：`battle.Card` 新增 `illustration_address`，保存完整 `Assets/...` Sprite 地址；Luban 生成的 `battle_tbcard.json` 是 Addressables 构建工具收集牌面条目的输入。四张牌面以 `Sprite / Single / no mipmap` 导入，`TinySpire Card Art` 本地组每次构建都与表中地址集合完全同步并移除失效条目。`HandCardContainer` 按本场牌组中的唯一模板预加载 `Sprite`，持有 `AsyncOperationHandle<Sprite>` 并在销毁时统一释放；`HandCardVisual` 让 Sprite 等比覆盖插图区，再由既有 Stencil Mask 裁切溢出内容，不保存模板 ID 到资源地址的映射。

**理由**：静态模板继续决定展示资源，卡牌实例只保存身份与局内事实，View 负责其实际使用期内的资源句柄。新增或替换牌面只需修改表格与资源，不需要增加 UI 分支；地址、导入类型或加载失败会直接暴露为构建/运行错误。

**影响**：`DataTables/Datas/battle.card.xlsx`、Luban 生成的 `Card`/JSON、四张牌面导入设置、`AddressablesBuildTools`、`TinySpire Card Art` 资源组、`HandCardContainer`、`HandCardVisual` 与 `CardView.prefab`。本决策不新增能量、回合、意图、状态、效果执行、胜败覆盖层或其他尚无运行时事实的 UI。

## CD-026：卡牌配置保存牌面短键，Addressables 构建期生成逻辑地址

**问题**：CD-025 把完整 `Assets/...` 路径写入 `battle.Card`。这使牌面移动或目录整理必须同步改表，也让策划承担 Unity 工程路径知识；直接用文件名搜索又需要明确处理重名、缺失和错误导入。

**选择**：`battle.Card` 只保存无目录、无扩展名的 `illustration_key`。动态牌面统一放在 `Assets/Arts/Runtime/Card/Illustrations/`；`AddressablesBuildTools` 递归建立不区分大小写的文件短名索引，在构建期拒绝重名、缺失、大小写不一致和非 `Sprite / Single / no mipmap` 资源，并将牌表引用映射为 `card-art/{key}`。运行时只通过 `CardIllustrationAddress.FromKey` 生成相同逻辑地址，继续使用 `Addressables.LoadAssetAsync<Sprite>` 与本地 `PackTogether` AssetBundle。

**理由**：配置表达业务身份，目录和扩展名属于资产组织细节，Addressables 地址属于发布细节。短键使素材在专用目录内移动或替换时不必改表；集中转换函数和构建期索引让运行时与构建工具共享同一地址规则，并把名字冲突或资源错误提前为构建失败。

**影响**：CD-025 关于手牌预加载、句柄释放和 View 生命周期的选择继续有效，但其“配置保存完整地址”部分被本决策替代。角色 Prefab 等其他字段仍保留完整地址，本次不扩展为全项目通用资源键系统。

## CD-027：M4 以多人共享玩家阶段为调度根，当前只接入单玩家

**问题**：现有 M4 路线图曾用单一 `CurrentEnergy` 和“玩家回合结束”描述当前 BattleScene，但 Pegasus 已锁定多人共享敌人阵列、玩家交错出牌、所有玩家结束后敌人才行动。若先按单人轮流模型实现调度器，后续加入多人时必须推翻能量归属、阶段命名和命令入口。

**选择**：`BattleTurnController` 作为战斗时序层的深模块，所有玩家共享 `PlayerAction` 阶段；每名玩家的能量和结束行动标记按 `CombatantId` 存入不可变 `BattleTurnData` 快照，不保存全局 `CurrentEnergy` 或 `CurrentPlayer`。玩家通过统一命令交错出牌并独立结束行动，全部玩家结束后才按 `BattleSession.EnemyCombatantIdsInEncounterOrder` 进入敌人阶段。当前 BattleScene 只把唯一玩家及其 `BattleCardZonesData` 接入该模型，多玩家牌组装配登记为 `DEP-008`。

**接口**：调用方只通过 `StartBattle`、提交出牌、结束玩家行动、完成当前敌人行动四类命令和只读 `Turn` 事实使用该模块。UI 不直接设置阶段、能量或结束标记，也不再直接把拖拽卡移入弃牌堆。内部状态节点与 `StateMachine<TEvent>` 组合不属于外部测试 seam。

**理由**：小 interface 隐藏阶段转换、多人结束门槛、能量校验和敌人顺序，能让 UI、M5 敌人行为和后续 Effect 模块共享同一个稳定 seam。当前单玩家接线只是适配范围，不改变根模型；因此可以先补战斗根基，再按实际需要聚合子系统，而不提前实现联网或多玩家 UI。

**影响**：M4 分为纯 C# 调度骨架、能量与出牌命令、玩家结束与敌人交接、当前单玩家 UI 接线、全量验证五步。`BattleSession` 的初始抽牌移交给调度器的 `BattleStart/PlayerRoundStart`；`GameConfig` 后续承载每轮基础能量静态规则；`BattleLifetimeScope` 后续注册控制器。完整计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`。
