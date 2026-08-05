---
created: 2026-07-06
updated: 2026-08-05
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

**2026-08-05 修订**：本条“DOTween/DOTweenPro 已导入”仅记录当时本地环境；当前公开依赖边界由 CD-056 取代。项目只分发并依赖免费 DOTween，DOTween Pro 仅允许持证开发者本地安装，不得成为生产代码或仓库内容依赖。

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

**M4C 修订**：CD-029 将“构造时抽初始手牌”改为“`BattleSession` 只创建并洗牌，`PlayerRoundStart` 统一负责首轮与后续轮次抽牌”。本决策关于实例随机流、四区唯一归属和弃牌重洗的结论继续有效。

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

**2026-08-05 修订**：上述 `view_prefab_address` 与“配置保存完整角色 Prefab 地址”只保留为 M3A 历史事实；当前由 CD-055 的 `view_prefab_key` → `character-view/{key}` 规则取代。Presenter 仍通过 `Addressables.InstantiateAsync` / `ReleaseInstance` 管理实例，其他参与者事实与生命周期结论不变。

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

**2026-08-05 修订**：“角色 Prefab 等其他字段仍保留完整地址”是当次牌面迁移的范围限制，现已由 CD-055 取代；当前所有 DataTables Unity 素材业务字段统一使用各素材域短键。

## CD-027：M4 以统一权威命令队列调度多人行动，当前只接入单玩家

**问题**：现有 M4 路线图曾用单一 `CurrentEnergy` 和“玩家交错出牌”描述多人行动。用户已明确修订为《杀戮尖塔 2》式口径：玩家可以同时提交命令，由统一权威顺序逐条执行和展示。若 UI 直接调用调度器并立即修改状态，就无法区分提交与执行，也无法在多人并发输入下保持确定顺序。

**选择**：`BattleCommandQueue` 成为玩家、系统阶段与未来敌人/Effect 的唯一外部提交 seam；提交可以在当前命令执行或展示期间继续发生。单机本地为命令分配单调权威序号，未来网络 adapter 负责把 Host 已确认命令送入同一执行路径。队列一次只执行一个命令，并等待其权威状态写入与展示完成；最终合法性在队首执行时重新校验。`BattleTurnController` 退到队列内部，只写阶段、轮次、按 `CombatantId` 保存的能量与结束状态，不向 UI 暴露直通写方法。

**接口**：调用方只使用 `Submit(BattleCommand)` 以及只读 `Queue`/`Turn` 事实。首批命令为开始战斗、出牌、结束玩家行动和完成当前敌人行动。`BattleCommandSubmissionResult.Accepted` 与最终 `BattleCommandExecutionResult` 分离；UI 不传费用、不直接扣能量或移动卡牌。锁定的是逻辑上的统一权威顺序，不要求未来网络实现使用单一物理 FIFO。

**理由**：并发提交消除玩家输入等待，串行执行保证能量、卡区、伤害、触发和表现顺序确定。小 interface 同时隐藏排序、执行期校验、阶段转换和展示等待；当前单玩家也走相同路径，因此后续加入网络时不需要把直通 UI 调用重写为命令。

**影响**：M4A 同时建立命令队列与调度事实骨架；M4B 实现队列化出牌、能量和执行期校验；M4C 实现队列化结束行动、敌人交接和生产接线；M4D 接 UI；M4E 全量验证。命令中途需要本地输入登记为 `DEP-010`，未来网络权威确认与重放登记为 `DEP-011`。完整计划见 `plans/2026-07-31-m4-turn-scheduling-energy.md`。

**M4E 复审澄清（2026-08-01）**：玩家命令的执行期合法性还包括其所属玩家行动窗口。上一轮已执行结束行动后，同一玩家排在其后的命令不得因为自动阶段链已经进入下一轮 `PlayerAction` 而重新变为合法；跨过轮次边界的旧玩家命令必须失败且零写入。该约束是 CD-027 原有“执行时事实重校验”和计划中“结束命令后的命令失败”的细化，现已按 CD-031 的队列内部轮次栅栏落实并通过 M4E 验收。

## CD-028：M4B 在队首从权威卡区与静态模板解析出牌费用

**问题**：提交 `PlayCardCommand` 时看到的手牌和能量可能在排队期间过期；若命令携带 UI 计算的费用，或先扣能量再发现卡牌已经离手，会产生透支、跨玩家卡区误用和半完成写入。

**选择**：`BattleTurnController` 通过构造参数接收 `BattleCombatantsData`、`CombatantId -> BattleCardZonesData`、Luban `Tables` 与每轮基础能量。`PlayCardCommand` 到达队首后依次校验阶段、玩家身份与存活、结束行动标记、玩家卡区、手牌归属、静态卡牌模板和当前能量；命令不携带费用。全部通过后才把指定运行时实例移入弃牌堆，并以新的 `PlayerTurnData` 快照扣除该玩家能量。任一失败只产生明确 `BattleCommandExecutionFailureReason`，不发布新的回合或卡区事实。

**理由**：权威顺序只能保证写入串行，不能保证提交时预览到的事实仍有效。把身份、卡区归属和静态费用解析集中到队列内部，可让不同玩家继续提交而不提前占用能量，并让同一玩家基于旧能量排队的后续命令在执行时自然失败。

**影响**：`GameConfig.EnergyPerRound` 与 `game-config.json` 默认均为 3；M4B 暂以进入弃牌堆作为出牌结束位置，不执行 Effect。`BattleLifetimeScope`、现有单玩家 UI、结束行动、敌人交接和初始抽牌迁移仍分别留给 M4C/M4D，不在本决策中接线。

## CD-029：M4C 由回合开始统一发牌，并按 Encounter 顺序逐帧交接敌人

**问题**：若 `BattleSession` 在构造时先抽初始手牌，首轮与后续轮次会拥有两条不同的发牌路径；若敌人阶段依赖参与者字典枚举或场景脚本直接跳阶段，也无法保证遭遇顺序、死亡跳过和重复完成信号的幂等性。

**选择**：`BattleSession` 只实例化参与者、运行时卡牌与确定性洗牌后的未发牌抽牌堆；`StartBattleCommand` 进入 `PlayerRoundStart` 时，与所有后续轮次共用同一入口重置每玩家能量和结束标记，并补抽到 `GameConfig.InitialHandCount`。`EndPlayerActionCommand` 到达队首后弃置该玩家剩余手牌并设置结束标记，只有全体存活玩家都结束才进入敌人阶段。敌人只按 `EnemyCombatantIdsInEncounterOrder` 查找下一名存活参与者，阶段事实一次只公开一个 `CurrentActingEnemyId`，且只有匹配当前敌人的完成命令能够推进。

**生产接线**：`BattleLifetimeScope` 注册 `BattleCommandQueue`、可替换的即时表现 adapter 与实现 `IStartable`/`ITickable` 的 `BattleCommandRuntimeDriver`。驱动器启动时提交唯一 `StartBattleCommand`；队列空闲且处于 `EnemyAction` 时，每帧最多为当前无行为敌人提交一条完成命令。当前 Session 只有一套卡区，因此生产工厂只映射唯一玩家并在出现第二名玩家时明确失败；多人根 interface 不变，完整生产多人装配继续由 `DEP-008` 跟踪。

**理由**：首轮与后续轮次共用一条发牌路径后，轮次、能量、结束标记与卡区事实不会因启动特例分叉。显式 Encounter 顺序和当前敌人身份让死亡跳过、错误完成与重复回调都能在队首按权威事实判定；逐帧只提交一个系统命令则保留了未来 M5 敌人行为替换 seam，不让同步即时 adapter 在同一帧越过整轮敌人。

**影响**：M4C 已把队列和阶段模块接入生产生命周期，但未改 `HandCardContainer`、场景或 UI；玩家拖牌、能量/轮次显示与结束按钮仍由 M4D 接到同一 `Submit` seam。当前敌人命令没有行为内容，M5 将替换内容而不替换顺序根；真实 Effect、目标和动画不在本决策范围。

## CD-030：M4D 用生产展示 adapter 统一命令反馈，卡牌只持有按序号关联的 pending 视觉

**问题**：CD-029 的即时生产表现会在提交调用栈内立刻完成命令，UI 无法稳定区分“已排队”“执行失败”和“执行完成”。若手牌在提交时先扣能量或移出权威卡区，执行期失败无法无损恢复；若用全局等待锁住输入，又会违背命令展示期间仍可继续提交合法意图的队列约定。

**选择**：生产 `BattleCommandPresentationAdapter` 在被队列接受后发布排队反馈，在 `Tick` 中分别发布执行失败或执行完成反馈，并为当前结果保留最短 0.35 秒的非缩放时间展示后才调用完成回调。`HandCardContainer` 只把 `PlayCardCommand` 交给 `BattleCommandQueue.Submit`，用临时的“权威序号 → CardInstanceId”关联恢复对应 View；每张牌的 pending 只阻止自身再次提交，不改变能量、卡区或其他卡牌输入。失败时清除 pending 并恢复交互，成功时等待权威 `CardZones.Layout` 删除离手 View。`BattleTurnHudView` 从 `Turn`、卡区和展示反馈即时派生能量、轮次、阶段、按钮与状态文字；结束按钮只提交 `EndPlayerActionCommand`。

**理由**：权威事实仍只在队首执行时改变，展示层仅持有短生命周期的视觉关联，因此快速连续提交、排队期间事实变化和执行失败都沿同一顺序根收敛。单玩家解析只存在于当前 View/装配边界；命令与回合核心继续以 `CombatantId` 和玩家映射表达多人事实。

**影响**：M4D 新增静态 `BattleTurnHud` Prefab，并在 `BattleScene` 复用既有能量球、结束回合按钮和玩家回合横幅资源；`BattleLifetimeScope` 用新 adapter 替换即时生产表现。没有新增敌人意图、真实 Effect、伤害、状态、胜败或结算占位数据。`DEP-002` 因 UI 不再绕过命令费用校验而解决；`DEP-001` 与 `DEP-004` 保持 open，完整多人生产装配继续由 `DEP-008` 跟踪。

**M4E 复审补充（2026-08-01）**：pending 视觉不能只保存布尔值。`HandCardVisual` 以 nullable 权威序号作为 pending 身份，失败反馈只清除匹配序号；`HandCardContainer` 在同一 `CardInstanceId` 的 View 因卡区布局重建时，从“权威序号 → CardInstanceId”短生命周期映射恢复最新待定序号。该关联仍不是玩法事实，不复制卡区或命令结果；它只防止旧反馈解锁新意图或重建 View。

## CD-031：玩家命令以队列内部提交轮次为行动窗口栅栏

**问题**：M4 原先只在玩家命令到达队首时检查当前 `Phase`、玩家结束标记、能量与卡区。最后一名玩家结束行动且全部敌人已死亡时，阶段链会在同一命令内同步进入下一轮并重置这些事实；上一轮仍在队列中的重复结束或旧出牌因此可能在新一轮重新合法并产生写入。

**选择**：`BattleCommandQueue` 在不可见的 `QueuedBattleCommand` 信封中记录命令提交时的 `BattleTurnData.RoundNumber`。`PlayCardCommand` 与 `EndPlayerActionCommand` 到达队首时，先比较提交轮次与当前轮次；不一致时返回 `BattleCommandExecutionFailureReason.PlayerActionWindowExpired`，不调用 `BattleTurnController`。开始战斗和完成敌人行动属于系统命令，不使用玩家轮次栅栏。公共命令构造参数、`Submit` / `Queue` / `Turn` seam 与 `BattleTurnData` 保持不变。

**理由**：用户确认的规则是“玩家命令只能属于提交时的轮次”，因此轮次号已经是所需的权威行动窗口身份，不需要增加可变 epoch、把提交轮次暴露给 UI，或让阶段模块反向依赖队列。栅栏位于队列进入控制器写链之前，可直接保证跨轮失败零写入；同时不改变同轮展示期间继续入队和队首最终事实校验。

**影响**：玩家命令不能在 `BattleStart` 或 `EnemyAction` 为未来轮次预排；即使同一张卡在下一轮重抽、能量重置且阶段重新变为 `PlayerAction`，旧命令也不会恢复合法。展示层同时按 CD-030 的精确权威序号关联 pending，避免跨轮失败反馈误解锁更新命令或重建 View。当前只显式覆盖已有的两类玩家命令；未来新增玩家命令时必须纳入同一判定，但本次不为尚不存在的类型引入命令分类抽象。验证见 `06_testing/2026-08-01-m4e-full-validation-review.md`。

## CD-032：敌人当前意图以 BehaviorId 快照持有，并使用独立确定性随机流

**问题**：M4 只提供敌人行动顺序与完成命令，尚无敌人行为事实。若 HUD、队列或未来 Effect 在读取时各自随机，或让敌人行为共用洗牌随机流，同一场战斗会因展示刷新、订阅顺序或卡区操作得到不同序列；若运行时复制行为类型、目标和数值，又会与 Luban 模板形成多份事实。

**选择**：`battle.Enemy` 只引用有序的 `EnemyBehaviorGroup`，行为模板保存 `EnemyIntentType`、`TargetRule`、既有 `CardEffect` 引用、正整数权重、冷却选择次数和最大连续次数。`BattleEnemyIntentsData` 按 `EnemyCombatantIdsInEncounterOrder` 建立每名敌人的权威 `BehaviorId`，以一个不可变完整 `EnemyIntentLayoutData` R3 快照发布；行为细节和显示值始终回查静态表与当前参与者事实。该聚合拥有由战斗种子和固定命名域盐派生的独立 `GameRandom`，单候选不消费随机，多候选按行为组稳定顺序只执行一次整数权重抽样。

**原子性**：每名敌人的冷却与连续次数只记录已完成行为所需的最小历史。完成当前行为时先复制历史并保存随机状态，再过滤候选和选择；只有成功后才替换该敌人历史并一次发布完整快照。无候选或配置契约错误会恢复随机状态，不修改当前意图或权威历史；不提供随机回退、静默跳过或通用条件 DSL。

**理由**：`BehaviorId` 足以表达本局已经作出的选择，其他字段均可派生，因此 HUD、命令队列与未来 Effect 可以读取同一事实而不产生镜像。独立实例随机流保证卡区洗牌与敌人选择互不推进，明确 Encounter 顺序则消除字典枚举对随机消费次序的影响。

**影响**：M5A 新增两张行为工作簿、意图枚举、生成配置、`BattleEnemyIntentsData` 与纯 C# 测试；固定敌人和加权随机敌人共用同一选择核心。`BattleSession`、M4 队列与 HUD 的生产接线分别留给 M5B/M5C；真实 Effect、伤害、格挡、状态、胜败和 Run 根种子不在本决策范围。验证见 `06_testing/2026-08-01-m5a-enemy-behavior-selection.md`。

## CD-033：敌人完成命令先选择下一意图，再保证推进 Encounter 顺序

**问题**：M4 的 `TryCompleteEnemyAction` 把阶段/身份校验和 Encounter 推进放在一个方法中。M5 若先调用它再选下一意图，无候选配置错误会留下“回合已推进、意图未推进”的半完成状态；若意图发布后再次执行可能失败的校验，则“失败零写入”又依赖没有重入或状态变化的隐含前提。

**选择**：`BattleCommandQueue` 对敌人完成使用固定三段写链：`ValidateCompleteEnemyAction` 只读校验当前阶段、敌人类型与 `CurrentActingEnemyId`；成功后调用同一 Session 持有的 `BattleEnemyIntentsData.CompleteAndSelectNext`；意图成功发布后调用无参数、不可失败的 `AdvanceAfterValidatedEnemyAction`，只负责向既有 M4 状态机派发完成事件并 Tick。无候选异常不转成普通表现失败，而是停住当前队首命令并显式暴露配置契约错误。

**所有权**：`BattleSession` 在敌人创建完成后按同一 Encounter 顺序建立并公开意图聚合，正常销毁时统一释放；`BattleCommandQueue` 和 HUD 只借用该实例，不创建或释放第二份。生产驱动继续只提交 `CompleteEnemyActionCommand`，不直接调用意图选择。

**理由**：所有可失败步骤都发生在回合写入之前，因此错误阶段、错误/重复敌人和无候选都不会部分推进。意图成功后只剩既有状态机的确定性交接，保证下一名敌人看到的回合事实与已经发布的下一意图一致；公共队列 seam 与 M4 权威顺序不变。

**影响**：M5B 修改 `BattleSession`、`BattleCommandQueue`、`BattleTurnController` 和 `BattleLifetimeScope` 的最小接线及测试；`BattleCommandRuntimeDriver` 只更新过期的“无行为敌人”注释。没有新增命令类型、普通失败枚举、行为执行层、场景或 Prefab，也不实现真实 Effect、伤害或死亡事件驱动交接。验证见 `06_testing/2026-08-01-m5b-session-command-queue-wiring.md`。

## CD-034：敌人意图 HUD 只投影权威 BehaviorId，并与卡牌文本共享效果值计算

**问题**：若 HUD 保存第二份意图类型或预测数值，意图切换、力量变化、死亡和 View 重建都可能让画面偏离 Session 的权威事实；若敌人预测另写一套数值公式，又会与既有卡牌动态文本对同一 `CardEffect` 得出不同结果。运行时动态创建固定意图节点还会把静态 UI 结构与 Prefab 资产分离。

**选择**：`ParticipantHudView.prefab` 静态持有意图根、正式图标和数值文本，玩家隐藏，敌人订阅同一 `BattleEnemyIntentsData.Layout`。展示层只读取当前 `BehaviorId`，再从 Luban `EnemyBehavior.IntentType / EffectId` 与当前 `EnemyCombatantData` 派生可见性、Sprite 和数值；力量、生命、Locale 或意图变化均重新投影，不保存玩法事实镜像。原 `CardValueCalculator` 保留 Meta GUID 并最小更名为 `BattleEffectValueCalculator`，卡牌文本与敌人 HUD 通过同一个纯计算入口解释 `CardEffect`，但不执行 Effect。

**资源与布局**：Prefab 只序列化 `ui_battle_intent_attack/defend/buff/debuff/special` 五类正式 Sprite，拒绝 `_ref_` 参考图；意图行位于名称上方，继续由既有世界点投影跟随角色。1～3 敌人仍使用 M3A 的 Encounter 顺序和等间距世界布局，不创建第二套敌人或 HUD 布局事实。

**理由**：`BehaviorId`、静态模板和当前参与者事实已经足以得到完整展示，把所有投影留在 View 边界可确保刷新不消费随机，且未来真实 Effect 入口仍可读取同一意图。静态 Prefab 结构让引用、导入模式和相对布局可在 EditMode 合约测试中验证，生产运行只实例化可变数量的 HUD。

**影响**：M5C 修改 `ParticipantHudView.prefab`、`ParticipantHudView`、`ParticipantHudPresentation`、`BattleParticipantPresenter`、共享效果值计算器及对应测试；不修改 `BattleScene.unity`、Session/队列公共 seam、DI 架构或启动流程，也不增加伤害、格挡、状态、死亡动画或胜败。验证见 `06_testing/2026-08-01-m5c-enemy-intent-hud.md`。

## CD-035：出牌合法性由一个纯规则 module 从当前权威事实派生

**问题**：M4 的出牌校验只存在于队首写链，M6 又需要在 UI 选择目标前预览费用、目标规则和合法候选。若 UI 复制一套规则或保存 `CanPlayCard`、存活敌人列表与目标合法性，预览会在排队、生命或阶段变化后偏离队首事实；若命令携带这些结果，又会把非权威预判混入写链。

**选择**：`PlayCardCommand` 只增加可空的单个运行时 `TargetId`，不携带费用、规则或合法性结果。具体纯 C# `BattleCardPlayRules` 从调用方提供的当前 `BattleTurnData`、唯一 `BattleCombatantsData`、玩家卡区、静态 `Tables` 与 `EnemyCombatantIdsInEncounterOrder` 即时产生不可变 `BattleCardPlayEvaluation`；结果包含稳定失败原因、静态 `TargetRule`、能否开始交互、费用是否可支付及一次性冻结的合法目标快照。Self 只派生 Actor，Enemy 只按 Encounter 顺序派生存活敌人；未知规则明确失败。重复读取不写 Turn、卡区、生命或随机流。

**理由**：同一具体 module 可以在 M6B 队首与 M6C UI 预览复用，而不额外建立 validator/resolver interface、响应式合法性镜像或第二套目标注册表。命令目标可空让缺失目标成为可观察的执行失败；非空结构无效标识仍在构造期拒绝。最终权威性继续由 `BattleCommandQueue.Submit` 排序和队首当前事实重校验保证。

**影响**：M6A 新增规则 module、评估结果与纯规则测试，并扩展命令契约和执行失败枚举；没有修改队列执行、回合写入、场景、Prefab、配置或 Effect。M6B 负责在首次写入前接入同一规则，M6C 负责生产 UI 的显式目标迁移并移除构造器默认值。验证见 `06_testing/2026-08-01-m6a-card-play-rules.md`。

## CD-036：队首以当前目标事实通过同一规则后才写卡区与能量

**问题**：UI 或提交时看到的合法目标可能在命令等待表现期间死亡、离开 Encounter 或因其他权威事实变化而失效。若先弃牌、扣能量再检查目标，会留下半完成写入；若队列另写一套目标校验，又会与 M6A 预览规则分叉。

**选择**：`BattleTurnController.TryPlayCard` 在任何卡区或 Turn 写入之前，把当前 `BattleTurnData` 与完整 `PlayCardCommand` 交给同一 `BattleCardPlayRules` 重新评估。只有全部规则成功，才执行既有的指定实例弃牌和能量扣除；规则失败只返回稳定执行结果。玩家命令的提交轮次栅栏仍位于队列层并优先于控制器评估，`BattleCommandQueue.Submit` 与只读 `Queue` / `Turn` seam 不变。

**理由**：所有可失败的目标、费用、身份、阶段与卡区检查都发生在首次权威写入之前，目标排队后失效自然得到零写入失败；UI 预览和队首执行共享一份规则语义，同时不把提交时预判携带进命令，也不增加第二条执行链。

**影响**：M6B 的合法 Self/Enemy 命令仍只扣能量并把指定实例移入弃牌堆，Enemy 生命、格挡和状态不变，真实 Effect 继续属于 M7。测试夹具显式提供 `TargetRule`、目标与 Encounter 存活事实；生产 UI 的目标组成、箭头与命中仍由 M6C 完成。验证见 `06_testing/2026-08-01-m6b-queue-head-target-revalidation.md`。

## CD-037：目标交互只编排派生规则、现有参与者 View 与同一命令提交 seam

**问题**：M6C 既要让 Self 卡自动选择自身、Enemy 卡在世界角色上精确命中，又不能让 UI 保存第二份合法目标/存活事实、增加 Collider/Raycaster 或绕过权威队列。BattleHand 还位于带缩放与相机深度的 Screen Space Camera Canvas 中；若箭头直接继承该层级，屏幕坐标转换和缩放会受父级影响，无法稳定覆盖整个 Game View。

**选择**：`HandCardContainer` 只从 M6A 同一 `BattleCardPlayRules` 重派生当前交互与一次性合法目标快照，并把显式 `TargetId` 交给既有 `BattleCommandQueue.Submit`。Self 越线使用 `ActorId`；Enemy 首次越线后冻结卡牌，由 `BattleParticipantPresenter` 按 Encounter 顺序把现有世界 `SpriteRenderer.bounds` 投影为屏幕矩形，再通过无状态 selector 选择“包含指针且中心最近”的候选，同距保留先遇到者。HUD 只接收 Legal/Hovered 表现状态，不持有玩法合法性。

**箭头与生命周期**：箭头作为 `BattleHandUI.prefab` 的序列化依赖存在，但容器启动时把其实例脱离带缩放/深度的父 Canvas，提升为同场景独立 `ScreenSpaceOverlay`；容器统一隐藏、释放并在销毁时回收该脱离实例。箭头与高亮 Graphic 均不接收 Raycast。该选择不修改 `BattleScene.unity`、相机、角色 Prefab、`CardView.prefab`、Physics 或 Addressables 地址接口。

**理由**：规则仍由同一纯 module 派生，参与者身份与世界 View 仍由现有 Presenter 映射持有，唯一玩法写入仍是权威队列；命中和箭头只交换屏幕坐标，不形成第二套注册表或状态事实。独立 Overlay 隔离了父 Canvas 的零缩放、缩放倍率和相机平面，同时保留静态 Prefab 依赖和可测试的默认隐藏/非 Raycast 契约。

**影响**：M6C 新增纯屏幕 selector、释放目标 resolver、箭头 View/Prefab、HUD 高亮和对应 EditMode/Prefab 合约测试；费用不足只在精确 `InsufficientEnergy` 时改变费用颜色。自动验证、Bootstrap 诊断与真实 Game View 物理拖拽均已通过，`DEP-001` resolved；验收状态见 `06_testing/2026-08-01-m6c-self-enemy-target-selection.md`。

## CD-038：费用不足只放开视觉拖动，不放开出牌语义

**问题**：M6C 初版把规则 `CanStartInteraction=false` 直接映射为 CardView 不接收输入。真实 Game View 人工审阅确认费用红色和规则结果正确，但完全无法拿起费用不足卡会让玩家无法通过拖动理解“这张牌想打但当前不能支付”；若直接把规则改为可交互，又可能错误进入 Enemy 瞄准或提交权威命令。

**选择**：保持 `BattleCardPlayRules` 对 `InsufficientEnergy` 的 `CanStartInteraction=false` 与队首失败优先级不变，只在 UI 边界用纯策略 `HandCardInteractionAvailability.ResolveMode` 收敛 `Disabled / VisualOnly / Playable`。该策略仅把“规则已允许”映射为 Playable、把精确 `InsufficientEnergy` 映射为 VisualOnly；阶段、身份、卡区、目标、战斗终止等其他失败均为 Disabled。`HandCardDragTransitionPolicy` 再把当前拖拽阶段、被拖牌是否仍在手中、交互模式与目标规则收敛为一份不可变转换结果，统一表达保留/取消、排除手牌重排、下一阶段、清反馈/目标表现和重建 Enemy 瞄准。`RebuildCards` 与 Turn/生命刷新直接消费该结果；`HandleDrag`、释放 resolver、提交前最终评估和 `BattleCommandQueue.Submit` 仍全部要求原始 `CanStartInteraction`/`Succeeded`。

**理由**：拿起/拖动是可逆的视觉可供性，不等于组成合法命令。把例外限制在 UI 边界后，费用不足卡可以跟手并回弹，同时不会生成箭头、高亮、目标、pending 或权威序号，也不需要复制能量或目标事实。规则 module 和队首权威写链无需为手感需求分叉。

**影响**：公开 transition、交互模式与 resolver seam 测试覆盖 `CardZones → Turn → 被拖牌自身离手`、三种交互模式及“费用不足不能产生释放目标”，CardView 合约测试覆盖红色时仍接收 Raycast；两轮独立审计补齐 `Playable → VisualOnly` 与真实发布顺序后，M6 定向 EditMode 53/53、串行 solution build 0 error。真实 Game View 已确认费用不足卡的跟手、无瞄准和回弹，M6C 完成；最终聚焦/弃牌动画与 Effect 继续属于 M9/M7。

## CD-039：结算是一次执行结果，Effect 数值由无状态公式 module 统一解释

**问题**：M7 需要让生产命令向表现层交付足以审计和编排的结算变化，同时卡牌文本、敌人意图与未来真实执行不能各自维护伤害公式。若记录保存为全局可变日志，会与参与者和卡区事实形成第二份状态；若公式直接读取 Luban、R3 或 `CombatantData`，则展示投影和目标结算难以在同一纯 seam 下验证。

**选择**：`BattleCommandExecutionResult` 持有按命令内顺序复制冻结的 `IReadOnlyList<BattleSettlementRecord>`；记录以 sealed 类型表达 Energy、Damage、Block、Attribute、Status、CardMoved、CardsReshuffled 与 OperationSkipped，并可关联强类型 `BattleEffectId?`、来源和目标。失败结果列表非 null 且为空，production presentation adapter 只转交当前结果，不保存或回写。`BattleEffectFormula.Calculate(context)` 只消费领域操作、配置值、来源 Strength 与可选 Health/Block/Vulnerable 标量快照，返回有效值及可选伤害推演；它不读取 Tables、R3、场景对象或结算元数据。

**理由**：一次性冻结记录让未来 M9 能按权威顺序表现，而各聚合的只读事实仍是当前状态唯一来源。纯公式既能提供无目标展示投影，也能在 M7B/M7C 接入目标状态，不需要复制公式或伪造目标对象；Luban 类型只在适配边界映射，裸 Effect ID 不穿过新管线。

**影响**：M7A 只增加契约和纯计算，尚未新增 Block/Vulnerable 事实、执行正式 Effect 或改变 M6 出牌事务。后续 M7B 负责唯一参与者状态写入口，M7C 负责预校验与有序执行，M7D 才把记录接入能量、Effect 与卡区事务；表现动画仍属于 M9。验证见 `06_testing/2026-08-02-m7a-settlement-formula-contract.md`。

## CD-040：参与者四项标量由同一实例持有，伤害只经 Effect 内部操作写入

**问题**：原 M3 参与者模型只有 Health/Strength，且 `BattleCombatantsData.ApplyDamage → CombatantData.ApplyDamage(int)` 暴露一条没有格挡、易伤或结算结果的 public 直通。M7 若在旁边增加新伤害 executor，会形成两条生产写链；若在外层先扣 Block、内层再扣 Health，又无法保证公式、状态和记录来自同一次计算。

**选择**：`CombatantData` 同时私有持有 Health、Strength、Block、Vulnerable 四个 R3 标量，对外只暴露只读事实和同步值；存活继续只由 Health 派生。internal concrete `BattleCombatantEffectOperations` 绑定本场唯一 `BattleCombatantsData`，独占 GainBlock、ModifyStrength、ApplyVulnerable、ApplyDamage 调用；Damage 以当前 source/target 标量调用一次共享公式，并把得到的 Block/Health before/after 在一个 `ApplyDamageOutcome` 同步调用内写回。旧聚合 public ApplyDamage 与旧生命直扣方法删除。

**理由**：四项事实的所有权、响应式生命周期和同步读取仍集中在单个参与者，不需要状态镜像。单次 damage outcome 同时驱动写入和后续结算记录，避免格挡与生命各算一次；目标已经死亡时内部操作明确返回 `TargetNotAlive`，为 M7C 的成功命令内 skipped 记录提供输入。

**影响**：M7B 只建立内部状态能力，尚未读取绑定或改变 M6 出牌；既有死亡测试夹具临时经该路径建立事实。M7C 的 `BattleEffectExecutor.Execute` 将成为生产与测试共同 seam，并收回 M7B 临时 Editor friend access；Block 清理、Vulnerable 衰减、HUD 与敌人真实执行仍分别属于 M8/M3E/M9。验证见 `06_testing/2026-08-02-m7b-combatant-effect-operations.md`。

## CD-041：Effect executor 先冻结完整顺序计划，再独占内部状态写入口

**问题**：M7C 必须按 `effect_bindings` 顺序执行 Strength、Damage、Block 与 Vulnerable，同时保证任一缺表、未知类型/属性或数值溢出都发生在首次写入前。若边解析边写入，后序错误会留下前序状态；若测试继续直连 M7B internal 操作，又会把临时 friend access 固化为第二个生产可见 seam。

**选择**：concrete `BattleEffectExecutor` 构造时绑定静态 `Tables` 与本场唯一 `BattleCombatantsData`。公共 `Execute(request)` 复制冻结来源、单个显式目标和有序 Binding；internal `Prepare` 先验证来源/目标存在且初始存活，再完整解析每个 Binding、Effect 表项、类型、属性与数值，并按顺序在 Health/Strength/Block/Vulnerable 标量快照上模拟结果。非属性 Effect 必须配置 `Attribute.None`，ModifyAttribute 只接受 Strength；所有 checked 溢出在首次写入前返回明确失败。前序模拟致死不会停止后序配置校验，合法后序操作被预标记为 TargetNotAlive skip。成功计划绑定创建它的 executor 与起始参与者快照，`ExecutePrepared` 先验证计划归属、事实未漂移和记录顺序容量，再只经 internal `BattleCombatantEffectOperations` 按原顺序写入并生成记录。

**理由**：完整计划把普通可预见失败全部推到写入前，既能让 M7D 在支付能量前复用预构建，又不用给每种 Effect 增加 public handler/adapter。顺序模拟让后序操作读取前序事实，Bash、易伤伤害和同目标 Strength 链不需要重排或第二份状态。测试只通过公共 executor 观察事实与记录，因此可以删除临时 `InternalsVisibleTo`，内部状态能力仍由一个生产 module 独占。

**影响**：M7C 已通过公共 seam 覆盖正式四卡、重复执行、致死跳过和全部预构建失败原子性；生产 `TryPlayCard` 尚未接入 executor，M6 出牌行为仍不变。M7D 只允许使用同一 internal `Prepare -> ExecutePrepared` 组合完成“预构建 -> 支付 -> Effect -> 归堆”，不得在队列层复制 Effect 分发。验证见 `06_testing/2026-08-02-m7c-ordered-effect-executor.md`。

## CD-042：出牌与阶段卡区变化都归属于当前权威命令的有序结算

**问题**：M6 出牌在规则通过后先弃牌再扣能量，尚未执行 Effect；抽牌、弃手与重洗虽然由同一回合状态机触发，却只发布最终 Layout。M7 若让队列按 EffectType 写状态，或让未来表现层比较前后布局猜变化，会复制执行规则、丢失重洗顺序，并把展示屏障之外的卡区变化变成第二条隐式命令路径。

**选择**：`BattleTurnController.TryPlayCard` 保留 M6 同一规则重校验，并在首次写入前调用 M7C `Prepare` 与 prepared-plan 快照校验；成功事务严格为 EnergySpent → `ExecutePrepared` 原绑定记录 → 当前 CardInstance Hand 到 DiscardPile → 一次当前阶段 Turn 发布。内部 `BattleTurnOperationResult` 把失败原因和冻结记录交给队列，`BattleCommandQueue.Execute` 只协调现有命令，不解析 Effect。`BattleCardZonesData` 的写方法返回冻结 `BattleCardZoneOperationResult`；Draw 记录每次跨区移动及重洗后完整抽牌堆顺序，回合控制器在 StartBattle、EndPlayerAction 和最后敌人完成的同一同步调用作用域中按连续序号收集它们。

**理由**：能失败的规则、Binding、Effect 表项、类型/属性、数值与 prepared 快照都在 Energy 首写前完成，普通失败自然得到零写入和空记录。卡区深 module 仍独占随机、抽顶、重洗和单次 Layout 发布，返回明确结果比外层差分更深，也让未来 M9 可以按真实发生顺序表现。阶段状态机、Submit、权威序号、轮次栅栏与 completion 屏障不需要重写。

**影响**：四张当前卡成功后统一进入 DiscardPile，因为配置尚无归宿字段；不按模板 ID 硬编码 Exhaust，`DEP-012` 保持 open。M7D 已经由公开队列 seam 覆盖四卡、致死 skipped、全部关键失败与阶段卡区记录；敌人真实 Effect、Block/Vulnerable 时机、队列事件化和 pending 协作者仍留 M8，最终动画与状态 HUD 仍留 M3E/M9。验证见 `06_testing/2026-08-02-m7d-card-effect-transaction.md`。

## CD-043：预注册句柄只负责对账，Queue 内部核心独占排序、token、屏障与 fault

**问题**：M4～M7 的 View 在调用 `BattleCommandQueue.Submit` 后才保存序号并手工发布 Queued；同步执行/回调会让结果先于 pending 注册。旧 Queue 又直接递归执行，没有 continuation 边界、非重入 drain 或冻结 fault。若把这些职责分散给 Hand、Turn、runtime polling 与 presentation，序号、pending 和阶段推进会继续出现时序窗口。

**选择**：提交方先向 concrete `BattleCommandSubmissionCoordinator` 预注册不透明 `BattleCommandHandle`，随后只调用既有 Queue seam；handle 不暴露或替代权威序号。internal `BattleCommandSchedulingCore` 是未来生产 Queue 唯一持有的调度子部件：接受时分配序号并形成唯一 Queued，外层迭代 drain 防止 callback 重入；Execute 返回后、Present 前把 `CompleteEnemyAction` continuation 追加 FIFO，并自动消费所属 Queue 签发的一次性 system token。非空 settlement 自动建立一次 completion 屏障；普通失败为空 settlement，确定性 fault 固定为首次写入前，只有提交后不可预期异常可显式标记 `MayHavePartialWrites=true`。fault 独立保存在只读 Queue 事实，不继承 settlement。

**理由**：coordinator 只解决“Submit 前已有对账身份”，不分配序号或调度；核心保持 internal，避免形成 `BattleCommandQueue.Submit` 之外的第二条公开写/排序 seam。system continuation 无法由外部伪造，拒绝路径撤销 handle 且不消耗序号；current/pending 在 fault 中保留，便于稳定诊断。M8A 为直接验证 internal 契约临时加入 `Assembly-CSharp-Editor` friend access；它仅是 Editor 测试能力，不是生产接口。

**影响**：M8A 尚未迁移现有 Queue、View feedback、pending、runtime polling 或自动阶段。M8B 必须让真实 Queue 唯一持有并消费该核心，把 Hand/Turn 迁到 coordinator，并从公开 Submit/Queue seam 重测全部生命周期；M8B～D 应逐步迁移 internal 测试，M8E 必须复审 `AssemblyInfo.cs` 并在不再需要时删除。验证见 `06_testing/2026-08-02-m8a-command-status-terminal-contract.md`。

## CD-044：敌人行动以同一初始权威快照联合验证，目标、状态与终局均即时派生

**问题**：敌人行动要先清 source Block，再在投影事实上执行 Effect，随后衰减 Vulnerable、推进 intent/history/random 并排入 continuation。若每段分别读取或复验 live facts，Self defend 会把旧 Block 带入结果，Effect 后复验又会把本事务自己的写入误判为漂移；若保存目标、胜负或状态阶段镜像，则会产生第二份事实。

**选择**：internal 敌人联合快照一次冻结 source/target 现有四标量、完整 `BattleTurnData`、当前 Intent Layout、目标敌人的 last/consecutive/cooldowns history、随机状态、恰好一个 ordered `BattleEffectId` 与可选 `CompleteEnemyAction` continuation。状态投影复用现有 `BattleEffectTargetSnapshot`：行动前 source Block=0，Effect 后 source Vulnerable 最多减 1。joint guard 只允许首次写入前一次 validate 与一次 commit；commit 不接收当前事实，因此不复验本事务中间写入。死亡 source 在目标解析前成功 skip；活 source 先即时统计存活玩家，零名 terminal、多名 configuration fault、唯一一名才解析 Self/Enemy。胜负每次从当前存活阵营派生，Turn 只保存中立 `BattleEnded` phase。

**理由**：一个初始快照和一次 validate 能把所有普通失败/配置 fault 推到首次写入前，同时让清 Block、Effect、衰减与意图推进共享同一事实基础。复用 M7 标量快照与状态投影避免复制公式；source-only skip 不伪造 Effect/target，唯一玩家规则也不私定多人目标策略。中立 phase 与派生 outcome 保持 Turn 单一事实来源。

**影响**：M8A 只建立并测试 contract module，没有执行 enemy Effect 或接生产链。玩家 Block → Energy → Draw、Discard → Vulnerable 在本切片只以纯 settlement 顺序口径记录，M8D 必须以真实公开 Queue 结算顺序替换该手工组合防回归；M8C 负责真正的 Effect/intent 三段式联合事务，M8D 才负责状态、死亡、稳定 Encounter 顺序与终局接线。验证见 `06_testing/2026-08-02-m8a-command-status-terminal-contract.md`。

## CD-045：Queue 先发布唯一生命周期，再以一次屏障串行消费 continuation

**问题**：M4～M7 的 Hand、Turn HUD 与 presentation 分别持有序号或手工 Queued，runtime 又轮询敌人阶段提交完成命令。同步执行会让结果早于 pending 登记，回调内提交可能重入；若 continuation 在表现完成后才入队，表现期间的新玩家命令会越过阶段推进；若同步 completion 在 `Present` 返回前直接清 current，随后抛出的表现异常又无法冻结正确 fault。

**选择**：生产 `BattleCommandQueue` 唯一持有 M8A scheduling core。提交方以同一命令引用向 concrete coordinator 预注册 opaque handle，Queue 接受时先占有 drain、分配序号并发布唯一 Queued，再发布 Queue 快照；拒绝撤销 handle。执行返回后、`Present` 前，Queue 为预定 `CompleteEnemyAction` 签发并消费一次性 token，按 FIFO 入队并先发布其 Queued。每条命令只聚合一次前后 Turn 的 `BattlePhaseChanged`；非空结算建立一次 completion 屏障，零结算直接通过。同步 completion 先缓存，只有 `Present` 正常返回且当前终态已经发布后才生效；异常则取消缓存并冻结 fault。Hand/Turn 只按精确 handle 对账，runtime driver 只负责启动命令，不再轮询。

**理由**：handle 解决调用方在 Submit 前建立身份的问题，权威序号、Queued、顺序、continuation 与 fault 仍全部留在 Queue 内部；因此 callback Submit 只能排队而不能重入执行，既有 accepted → continuation → presentation 期间新提交的顺序不依赖帧时机。表现屏障由真实非空结算自动产生，也不会被 adapter 或调用方以布尔参数绕过。

**影响**：公开 seam 仍是 `BattleCommandQueue.Submit` 与只读 `Queue` / `Turn`；coordinator 只额外发布生命周期供 View 对账，不成为第二条排序或写入入口。普通失败不调用 presentation，当前 Failed/Faulted 只清除匹配 handle，旧终态/旧 completion 不影响新 pending。M8B 的 typed `BattleNoLegalNextIntentException` 仅是旧一步式意图推进到 fault 的稳定过渡桥；M8C 必须以三段式 intent plan 和联合事务替代，M8D 才接真实敌人 Effect、状态和终局。验证见 `06_testing/2026-08-02-m8b-command-lifecycle-presentation-barrier.md`。

## CD-046：Effect 核心消费有序 ID，敌人行动以投影事实联合提交

**问题**：M7 的 Effect request 直接携带 Card binding，敌人若复用就必须伪造卡牌语义；旧一步式 intent 完成又会先推进真实 RNG 再尝试回滚。敌人行动还必须先清 source Block，再从该投影执行 Effect、衰减 Effect 后 source 的 Vulnerable 并推进下一意图；若各段独立抓取或复验 live facts，Self defend 会叠加旧 Block，本事务自己的合法写入也会被误判为漂移。

**选择**：`BattleEffectExecutionRequest` 只冻结 source、显式 target 与 ordered `BattleEffectId`，`BattleTurnController` 是唯一 Card binding → ID 边缘适配。Effect prepare 在实际或调用方提供的投影标量上完整解析和顺序模拟，同时保留真实初始参与者快照；首次写入前校验后，commit 不复验中间事实。`BattleEnemyIntentsData` 以同一 `BattleEnemyIntentAuthoritySnapshot` 建立 `PrepareCompletion → ValidatePreparedCompletion → CommitPreparedCompletion`，Prepare 用复制 history 和显式恢复 state 的本地 `GameRandom` 冻结 next history/random/Layout。internal concrete `BattleEnemyActionExecutor` 联合持有 Block、Effect、Vulnerable、Intent component plan 与 continuation 副本，唯一校验后按 Block → Effect → Vulnerable → Intent 提交。

**理由**：ordered ID 让 Card 与 Enemy 共享同一个深 Effect module，却不把 Card 配置结构泄漏到敌人领域。投影事实把未来合法状态变化前移到零写入 prepare；同一初始快照和本地 RNG 又保证配置、目标、随机、序号或 authority 错误都在首次写入前形成空结算 fault。状态时机只写自己拥有的标量，因此不会覆盖 Self Effect；所有数值仍由 M7 公式和状态操作产生。

**影响**：死亡 source 在 Behavior/target/Effect/Intent 之前直接产生 source-only skip；当前活 source 只允许唯一存活玩家，零玩家 terminal、多玩家 fault。M8C 只交付纯 module/fixture，未注册 Queue/LifetimeScope，生产敌人仍保持占位；M8D 才负责 Encounter continuation、玩家状态时机、死亡中止和终局接线。验证见 `06_testing/2026-08-02-m8c-enemy-effect-transaction.md`。

## CD-047：当前命令原子发布权威阶段，表现 completion 只释放后继执行屏障

**问题**：敌人命令成功后必须同时提交 Damage/Block/Vulnerable/Intent 与 Encounter 交接，但又要求“反馈完成前不切换下一敌人/轮次”。若为满足表现等待而把 Turn 暂存在 Queue 外，或让 completion 回调再写 Turn/Combatant/Intent/CardZones，会产生第二份阶段事实、破坏失败边界，并使同步/迟到 completion 可以越过权威排序。

**选择**：每条命令仍在一次同步 `Execute` 中提交其完整事务和命令前后唯一 `BattlePhaseChanged`；因此只读 Turn 在 `Present` 前已经是该命令的权威终态。Queue 在同一时点预定并排入 frozen continuation，但非空 settlement 建立的 `IsWaitingForPresentation` 屏障阻止后继命令执行。presentation completion 只按精确序号解除屏障并重新进入非重入 drain，不写 Turn、Combatant、Intent 或 CardZones。敌人行动后若 terminal，Queue 丢弃已冻结后继并发布 `BattleEnded`；否则按 Encounter 顺序或玩家 RoundStart 继续。

**理由**：命令结果、Turn 和 settlement 保持同一原子提交边界，presentation 只决定何时消费下一条命令，而不是何时让当前事实生效。这样既满足 continuation 在 Present 前获得权威序号和 FIFO 位置，也保证反馈期间下一敌人/下一轮的 Effect、状态、Intent 与卡区完全不执行；迟到 completion 仍不能跨过新屏障。

**影响**：玩家 RoundStart 固定为 Block → Energy → Draw，EndPlayerAction 固定为 Discard → Vulnerable；敌人固定为 Block → Effect → Vulnerable → Intent，再由 Queue 派生 terminal 或 continuation。死亡 source、玩家致死中止、终局拒绝和 fault partial 语义均沿此边界实现。表现层可在屏障期间看到 Turn 已指向预定下一行动者，但不得把该指针误解为后继已经执行；验证见 `06_testing/2026-08-02-m8d-status-death-battle-loop.md`。

## CD-048：M8 深 module 只向 Editor 测试开放友元，不保留生产 public 写旁路

**问题**：M8 的 scheduling、状态时机、意图三段式计划、联合快照与敌人事务需要直接验证一次性 guard 和首次写入前原子性；若全部只测 Queue，会把细粒度失败原因藏在长链中。但把旧意图完成入口、目标 resolver 或 terminal rules 保持 public，又会让生产调用方绕过 `BattleCommandQueue.Submit` 或把内部规则误当扩展 API。

**选择**：保留 `AssemblyInfo.cs` 对 `Assembly-CSharp-Editor` 的单一 `InternalsVisibleTo`，只供 Editor 契约测试访问 internal 深 module。旧 `CompleteAndSelectNext`、敌人目标解析结果/resolver 与 terminal outcome/rules 全部收窄为 internal；生产外部继续只持有 Queue `Submit` 与只读 `Queue`/`Turn`。敌人联合计划不再复制 validation/commit 状态，唯一一次完整 component validate 与 commit 均由 `BattleEnemyActionJointCommitGuard` 消费。

**理由**：友元只扩大测试程序集可见性，不扩大生产 public API；因此既能直接证明联合预构建、漂移、失败零写入和重复消费保护，也不产生 Queue 外的权威写入 seam。单 guard 删除锁步状态，避免两个布尔状态机未来漂移。

**影响**：现有 M5 intent/session/HUD 测试继续通过友元调用兼容入口，生产代码没有该入口的消费者；M8E 最终双轴复审确认 public 旁路与重复 guard finding 均已关闭。若未来 internal 契约测试全部迁到同等强度的公开可观察 seam，可再删除 `InternalsVisibleTo`，但本轮不以降低测试证据为代价提前移除。验证见 `06_testing/2026-08-02-m8e-full-validation-review.md`。

## CD-049：M9 以冻结命令结果驱动单一表现时间线，常驻 HUD 只重投影当前事实

**问题**：M8 已让 Queue 独占权威顺序、continuation、一次屏障与 fault，但 M4D 的固定 0.35 秒 adapter 只能表达占位等待。若伤害、死亡、卡区运动、阶段横幅和终局各自订阅 settlement 或拥有独立动画队列，就会出现第二排序根、多个 completion 与场景销毁后的迟到回调；若 HUD 或终局面板缓存 Health、Intent、Hand、Turn 或 outcome，又会形成权威事实镜像。PlayCard 还需要在原始 Order 0 之前表现离手到目标，但不能伪造或重排 settlement。

**选择**：保留唯一 Queue-facing `IBattleCommandPresentation.Present(result, completion)`，由 concrete `BattleCommandPresentationAdapter` 把当前冻结结果同步转换为不可变 `BattleCommandPresentationPlan`。每个命令至多派生一个互斥的 StartBattle 或 PlayCard `CommandPrelude`；PlayCard 仅由唯一 Hand→Discard 与首个可见 Effect 的冻结身份派生，Prelude 不属于 settlement。随后所有可见步骤继续按 settlement `Order` 与稳定子步骤顺序进入同一个 `BattleCommandPresentationRunner` 父时间线。runner 唯一持有 readiness、速度、精确 cue 快进、立即完成、一次 completion、Tween lease 与取消/构造异常清理；完成、重复完成、owner/Scene 销毁均幂等，旧 Scope 不得补发 completion。离手 transient 的 Prelude 与 Hand→Discard lease 共享同一个幂等释放边界，任一后续 cue 构造失败也能立即收口。

**事实与输入边界**：Participant、Turn、Intent、Hand 与 pile HUD 始终从当前 Combatant、Intent Layout、CardZones Layout 和 Turn 重新投影，不保存玩法镜像；数字、抖动、脉冲、轨迹、死亡和横幅只消费当前冻结结果。`BattleEnded` 步骤只在同程序集内临时调用 internal `BattleTerminalRules`，立即映射本地化 key，不公开规则或保存 outcome。表现屏障期间仍允许既有合法玩家命令进入 Queue；只有离手 ghost、StartBattle 覆盖层、终局战斗输入与场景按钮使用局部指针锁。Restart 复用现有 SceneFlow 重载同一 BattleScene/Inspector seed，Exit 只调用应用退出薄 seam。

**理由**：Plan 把“派生什么”与 concrete View 的“如何补间”分开，而 runner 仍只有一个父顺序和一个 Queue completion；因此表现可以深化而不改变 Queue、Turn、settlement、公式、目标或终局契约。常驻事实即时投影、一次性反馈冻结读取，使加速、取消、重建和 locale 切换都不会反向写入战斗状态或留下第二份事实。

**影响**：本决策取代 CD-030 的固定 0.35 秒占位时长，但保留其“其他合法输入不全局锁定、pending 按权威身份对账”的边界。M9 只深化 `TinySpire/Assets/Scripts/UI/Battle/**` 与列明的 concrete Prefab/Localization 资源，没有新增 settlement 事件总线、每记录 presenter interface、第二动画队列、public terminal API、RunState、MainMenu 或 DI seam。2026-08-05 的后验修正进一步锁定：`HandCardContainer` 只能把未展示的当前 Hand View 准备到 base pose；其可见性由对应冻结 `Draw→Hand` cue 开始时取得，局部 View lifecycle 不能成为 Hand/Turn/settlement 的第二事实。验证见 `06_testing/2026-08-05-m9-post-validation-bug-triage.md`。

## CD-050：UI 大改前，Participant HUD 临时投影到角色头顶

**问题**：`BUG-UI-001` 已在五种 M9 宽高比中确认生命 HUD 与 Overlay 手牌发生屏幕相交；现有 HUD 又位于 Camera Canvas，故相交时会被手牌遮挡。用户已明确后续 Battle UI 将整体重做，但当前仍需要让生命信息可读。

**选择**：保持现有 Canvas、Scene、排序和参与者事实不变。`ParticipantHudView` 将 `VitalsAnchor` 从角色脚下改投影至精灵 bounds 顶部外侧，并把 `NameAnchor` 固定置于生命 HUD 上方；唯一新增的 Prefab 序列化参数是两者的垂直间距，便于后续 UI 替换或微调。

**理由**：这是一项可替换的止血改动，避开当前手牌主要覆盖的下方区域，且不把临时可读性要求扩大为 Canvas 层级、场景布局或完整 HUD 架构选择。HUD 仍只读取当前 `SpriteRenderer` bounds 与当前 Combatant 事实，不复制生命、意图或回合状态。

**影响**：只影响 `ParticipantHudView.cs`、`ParticipantHudView.prefab` 与其 Editor 投影测试；不修改 `BattleScene.unity`、Queue、Turn、settlement、目标、终局、DI、DataTables、Addressables 配置或 Candidates。未来 Battle UI 重设计应整体替换该临时投影，不把其偏移量视为最终视觉规范。验证见 `06_testing/2026-08-05-m9-post-validation-bug-triage.md`。

## CD-051：PlayCard 前奏持有离手卡，不飞向目标

**问题**：M9 原实现把 `PlayCard` Prelude 的冻结目标身份路由为 `Hand → Target` 卡牌运动。用户反馈该轨迹不符合当前出牌表现；但直接删除 Prelude 又会破坏它先于 Order 0 的既有编排契约，并可能让已离手的 transient 在后续 cue 同步构造异常时失去 runner 的清理所有权。

**选择**：`BattleCardMotionCue` 不再承载目标身份，移除 `PlayCardToTarget`。`PlayCard` Prelude 仍由同一 `BattleCardMotionTweenFactory` 消费，但只形成零时长、无位移的 `PlayCardTransientHold` lease；它不读取 participant 屏幕锚点，且与后续 `Hand → DiscardPile` cue 共享幂等 transient 清理边界。卡牌真正移动时只消费冻结的 `CardMoved(Hand → DiscardPile)` settlement，并严格位于其原始 `Order`。

**理由**：保留 Prelude 可以维持 M9 单一 runner、一次 completion、异常/取消清理与 settlement 顺序；撤销目标锚点与目标身份则使卡牌运动接口无法再表示“飞向怪物”。该变化只修正可见表现，不重排或改写权威战斗事实。

**影响**：本决定取代 CD-049 中“PlayCard 前奏表现离手到目标”的可见轨迹部分，保留其余冻结结果、顺序与生命周期边界。未修改 Queue、Turn、Effect、CardZones、目标合法性、目标箭头、Scene、Prefab、DataTables、Addressables 或 Candidates。验证见 `06_testing/2026-08-05-play-card-no-target-flight.md`。

## CD-052：目标箭头在视图内部组合曲线片段，锁定框按参与者边界定位

**问题**：攻击箭头以单根拉伸图片表示时，曲线方向与箭身朝向不自然；目标锁定高亮由左右两片拼接且使用固定大小，容易像垫在怪物身后而不是明确框住怪物。

**选择**：保持 `BattleTargetingArrowView.Show / UpdateArrow / Hide` 的外部接口不变，在视图内部将箭头拆成终点 head 与可复用 fragment 池。曲线使用局部三次贝塞尔采样，每段 fragment 和 head 的旋转都取该采样点切线。`ParticipantHudView` 对合法与悬停状态各持有四个角件，运行时根据 `SpriteRenderer.bounds` 投影得到的 Canvas 边界加可调留白来定位。

**理由**：调用者仍然只传入起点与终点，不需要知道曲线、池或片段数量；箭身的局部切线使曲线可读。锁定框直接从参与者实际渲染边界派生，避免维护固定尺寸副本，并为后续整体 UI 改版保留 padding、fragment 长度、间距与弯曲幅度等局部参数。

**影响**：`TinySpire/Assets/Scripts/UI/Battle/Targeting/BattleTargetingArrowView.cs`、`TinySpire/Assets/Scripts/UI/Battle/ParticipantHudView.cs` 及其两个 Prefab 和契约测试。继续复用已有正式 Targeting 精灵，不修改源图片或 Meta；不改目标合法性、`BattleCommandQueue`、`Turn`、结算或场景结构。

## CD-053：配置服务仅发布完整快照，表清单在同步构建时做四源漂移校验

**问题**：`ConfigService` 先发布 `Tables`、后读取 `game-config.json`，后者失败时会记录 warning 并构造代码默认值；这既允许半成品服务进入后续链路，也让资源损坏伪装成默认内容。运行时的手写 `TableNames` 又没有同 Luban 表定义、生成代码和 GameData 输出比较，新增/删除表时容易发生漂移。

**选择**：保留 `AddressableAssetService` 作为唯一生产读取边界，但让它实现仅供 Core/Editor 使用的内部 `IConfigTextLoader`。`ConfigService` 先在局部加载、解析和构造全部表与 `GameConfig`，成功后才同时发布属性；所有配置失败统一为带地址、可选表名与稳定 reason 的 `ConfigInitializationException`。`TinySpire/Build/Sync and Build All` 在生成和强制导入后，比较 Luban `__tables__.xlsx` 的客户端 battle 表、生成 `Tables.cs` loader 名、`Assets/GameData` JSON 和运行时必需清单，并拒绝缺项、额外项或重复项。

**理由**：测试 fake 只替换最窄的文本读取边界，不扩大 Bootstrap、DI 或战斗模块的 API。局部构造让失败具有零发布语义；四源校验把表漂移前置到既有同步构建，而不再维护第二份人工 manifest。这个决定不验证或呈现 Bootstrap 错误 UI，后者留给 M10B。

**影响**：`ConfigService`、`AddressableAssetService`、最小 Core failure/seam 类型、Editor 清单验证器、既有同步构建入口和相应 Editor 测试。未修改 DataTables、生成 JSON、Localization、Addressables 配置、Bootstrap、Scene、Prefab、Queue、Turn、settlement 或任何战斗规则。验证见 `06_testing/2026-08-05-m10a-config-fail-fast.md`。

## CD-054：Bootstrap 只转交已分类配置失败，并以无本地化依赖的最小 View 停止启动

**问题**：配置可能在 Localization、UI 资源和首场景之前失败；只记录异常既不是可见失败体验，也会让后续启动逻辑误以为仍可进入 Loading/BattleScene。若 Bootstrap 直接吞掉所有异常、增加重试流或引入第二场景/DI 启动体系，则会扩大启动架构并掩盖未知故障。

**选择**：`GameLauncher` 保持编排职责，只捕获 `ConfigInitializationException` 并交给 `IBootstrapFailurePresenter`，随后返回；任何其他异常仍原样上抛。`Bootstrap` 在自身现有 GameObject 上按需提供 `BootstrapFailureView`，其运行时覆盖层只显示稳定 `CFG-001`～`CFG-007`、资源地址和修复后重启指引，不依赖尚未初始化的 Localization，也不提供重试、MainMenu、Run、配置写入或场景切换。

**理由**：M10A 已把失败收敛为带地址和原因的 typed failure，因此 M10B 只需在最窄启动边界转交它；无本地化依赖的最小 View 可以在内容加载失败时仍可用，且不会复制 `Tables`、`GameConfig` 或战斗事实。内部编排 seam 仅供 Editor 测试验证“失败不继续、成功仍加载首场景、未知异常不吞掉”，不扩展生产写入口。

**影响**：影响 `Bootstrap`、`GameLauncher`、最小失败展示接口/View 与对应 Editor 测试；未改 Scene、Prefab、Addressables 内容、表格、生成 JSON、Localization 资产、Queue、Turn、settlement、战斗规则或 Targeting/Candidates。验证见 `06_testing/2026-08-05-m10b-bootstrap-golden-baseline.md`。

## CD-055：配置表 Unity 素材统一保存短键，构建期解析为 Addressables 逻辑地址

**问题**：牌面已按 CD-026 使用 `illustration_key`，但 Hero/Enemy 仍把完整 `Assets/...prefab` 写入 `view_prefab_address`。这迫使配置作者掌握 Unity 目录结构，也让移动素材、大小写漂移、同名素材或错误 Prefab 直到运行时才暴露。完整路径虽然看似磁盘路径，旧实现实际把它同时写成 Addressables catalog 地址并调用 `Addressables.InstantiateAsync`：Packed/Player 经 `BundledAssetProvider` → `AssetBundleProvider`，Fast Mode 则经 `AssetDatabaseProvider`。问题是配置和发布细节耦合，而不是运行时直接读取磁盘路径。

**选择**：所有 `DataTables` Unity 素材业务字段统一保存无目录、无扩展名、大小写精确匹配文件名的 `*_key`。每个素材域拥有专用目录、唯一逻辑地址前缀、运行时转换函数和 Editor 构建期解析器：卡牌为 `illustration_key` → `card-art/{key}`，角色为 `view_prefab_key` → `character-view/{key}`。构建工具只从生成表读取实际引用，扫描专用目录并拒绝空键、路径/扩展名、忽略大小写后的重名、大小写漂移、缺失素材及素材域契约错误；角色 Prefab 必须含运行时可发现的 active `SpriteRenderer`。专用 Addressables Group 与实际引用集合精确同步。运行时继续只用 Addressables API 加载和释放。

**边界**：配置短键不是 catalog 中全部地址的全局替代。`Assets/Scenes/*.unity`、`Assets/GameData/*.json` 等资源系统基础设施项继续使用完整 `Assets/...` 稳定地址；真实资产路径只允许由 Editor 构建工具用于索引、校验与生成 Addressables 条目，不得回写到业务配置或成为运行时文件系统加载入口。Fast Mode 的 `AssetDatabaseProvider` 只适合编辑迭代，不能证明 AB；地址或加载实现变化必须另外以 BuildLayout 和 Packed Play Mode/Player 证明 `AssetBundleProvider` 与物理 bundle。

**理由**：短键表达策划所关心的素材身份，Editor 资产路径和 Addressables 打包方式留在发布边界。运行时与构建工具使用同一逻辑地址规则，既保留 Addressables/AssetBundle 生命周期，也把冲突、漂移、缺失和契约错误提前为同步构建失败。

**影响**：`battle.Hero`、`battle.Enemy` 改用 `view_prefab_key`，`BattleParticipantPresenter` 经 `CharacterViewAddress` 生成逻辑地址；`AddressablesBuildTools` 从 Hero/Enemy 生成 JSON 精确同步 `TinySpire Characters`。CD-023 的完整角色地址与 CD-026 的“仅牌面短键”限制被本决策取代；Scene/GameData 地址、Queue/Turn/settlement、战斗规则、DI 与场景启动不变。验证见 `06_testing/2026-08-05-config-asset-logical-keys.md`。

## CD-056：公开仓库只分发免费 DOTween，DOTween Pro 作为每席位本地依赖排除

**问题**：DOTween Pro 属于按席位授权的付费 Editor Extension；把其源码、DLL、示例或说明文件保留在公开 Git 当前树或可达历史，会把本地持证工具误当成项目可再分发依赖。若直接删除全部 Demigiant 内容，又会让当前大量只依赖免费 DOTween API 的表现代码无法在干净 Clone 中编译。

**选择**：公开仓库继续跟踪可再分发的免费 DOTween 与其必要 DemiLib/官方说明，只允许生产代码依赖免费 `DG.Tweening` API。`DOTweenPro/`、`DOTweenPro Examples/`、各自目录 Meta，以及 `readme_DOTweenPro.txt` 与其 Meta 从当前索引和所有可达 Git 历史移除，并由精确 `.gitignore` 规则永久排除；持有合法席位的开发者可在相同 Unity 路径本地安装并保留 Pro，但它不是 Clone、构建或运行 TinySpire 的前置条件。历史重写按用户授权强制更新远端相关分支/标签；不清理 GitHub LFS 存储对象，也不处理既有 Fork/Clone。

**理由**：免费运行时依赖留在仓库可保持可复现构建；把付费扩展限定为开发者本地安装，可避免再次公开分发或让本地 Pro 掩盖无 Pro 的依赖。精确忽略规则和无 Pro 干净检出验证共同锁定该边界。

**影响**：修改 `.gitignore`、`THIRD-PARTY-NOTICES.md` 与 Git 历史；所有被重写提交 ID 改变，协作者需重新克隆或显式迁移。免费 `DOTween/` 与必要 `DemiLib/` 保留；不得新增 Pro-only API、组件或序列化依赖。Unity 场景、Prefab 与业务代码不因本决策修改；为满足净化后 `main` 的完整性，另经用户单独明确授权只上传五个非 Pro LFS 对象，未修改 `.gitattributes` 或清理既有 LFS 存储。验证见 `06_testing/2026-08-05-dotween-pro-repository-sanitization.md`。

**2026-08-05 执行状态**：本地新历史与独立镜像已按本决策净化且验证一致。首次远端 force-push 因五个非 Pro LFS 对象缺失被 GitHub `GH008` 原子拒绝；用户随后明确授权只上传这五个对象，禁止 Pro、`--all`、`.gitattributes` 变更与 LFS 清理。精确对象上传完成后，`main` 已用旧 SHA 的精确 lease 成功更新；远端回读的 Pro 可达对象与路径提交均为 0，免费 DOTween/DemiLib 仍为 307 个跟踪项。
