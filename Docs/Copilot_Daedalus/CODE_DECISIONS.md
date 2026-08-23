---
created: 2026-07-06
updated: 2026-08-24
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

## CD-057：卡牌目录身份与可玩实现状态显式分离，未实现卡在规则入口零写入失败

**问题**：STS2 v0.107.1 Ironclad 单人快照包含 85 张卡，但当前 TinySpire 只能忠实执行其中少数机制。若把其余目录行直接写入 `battle.Card`，现有空 `effect_bindings` 路径仍会成功扣除能量并把手牌移入弃牌堆，使“已录入”伪装成“可玩”，并产生半次权威出牌。

**选择**：`battle.Card` 增加必填枚举 `implementation_status = Implemented | CatalogOnly`，作者表、生成 JSON 与测试夹具均必须显式填写。`BattleCardPlayRules.Evaluate` 在费用、目标与 Effect 之前只接受 `Implemented`；其他状态统一返回 `BattleCommandExecutionFailureReason.CardNotImplemented`。该结果属于普通命令执行失败：不发布 settlement、不令 Queue fault，也不修改 Energy、CardZones、Combatant 或 Turn。Queue 仍只调用既有规则与执行链，不增加第二入口或权威状态。

**理由**：实现状态是卡牌目录本身的发布事实，不应由“Effect 列表是否为空”或 UI 文案隐式推断。把门禁放在既有纯规则入口，可让所有 Queue 调用者得到一致 typed failure，并在首次权威写入前结束；后续机制切片只需在完整回归通过后把对应卡从 `CatalogOnly` 翻为 `Implemented`。

**影响**：影响 `battle.card.xlsx`、Luban 生成的 `CardImplementationStatus` / `Card` / `battle_tbcard.json`、`BattleCardPlayRules`、命令失败原因与相关测试夹具。当前四张生产卡保持 `Implemented`，没有行为变化；I1 不修改 Queue、Turn、settlement、公式、Deck、Localization、Scene、Prefab 或 Addressables 地址。Deck 不得引用 `CatalogOnly`、`Implemented` 必须有有效程序和牌面键的构建期约束留给 I2，不能把本决策误读为这些校验已经交付。验证见 `06_testing/2026-08-06-sts2-ironclad-i1-catalog-runtime-gate.md`。

## CD-058：卡牌目录发布门禁在 Localization 前复用唯一真实素材解析规则

**问题**：I1 只保证 `CatalogOnly` 在运行时 Queue seam 零写入失败，不能阻止作者把目录卡放进生产 Deck、把 `Implemented` 留成空程序，或用不存在/伪路径牌面进入发布链。若等 Localization 或 Addressables 阶段才检查，失败前可能已经写入本地化资产；若另写一套牌面扫描器或 Effect 执行矩阵，又会产生与现有 Addressables/Executor 漂移的第二份规则。生成 JSON 顶层键与记录内 `id` 若不一致，也会使构建校验和运行时按不同身份解析记录。

**选择**：新增独立 Editor `BattleCardCatalogBuildValidator`，唯一接入顺序为 Luban → AssetDatabase Refresh → 四源表清单 → 卡牌目录门禁 → Localization → Addressables。门禁先要求 Deck/Card/Effect 的 JSON 顶层键与内嵌整数 `id` 精确一致，再检查 Deck 只引用存在的 `Implemented` 卡；`Implemented` 必须有非空 bindings、非空且卡内唯一的参数键和存在的 Effect 引用；`CatalogOnly` 可无程序，但只能声明构建器锁定的唯一占位短键。I2 测试阶段预留名为 `card_art_catalog_placeholder`，I3 在第一张生产目录卡进入表前按 CD-059 最终锁定为项目既有 `art_placeholder`。所有卡的短键语法、实际文件、大小写和 Sprite 导入契约都复用 `AddressablesBuildTools` 的同一解析 seam。

**边界**：I2 的“有效程序”是发布结构有效，不复制 `BattleEffectExecutor` 的 EffectType/Attribute/公式规则。某卡从 `CatalogOnly` 翻为 `Implemented` 时，相关机制切片仍必须通过公开 Queue、只读事实与 settlement 回归证明语义可执行。字段 shape 由同次成功 Luban 生成保证；损坏 JSON 仍会在 Localization 前失败，但 I2 不建立第二个通用 JSON schema 解析器或统一所有 Newtonsoft 错误。

**理由**：单一前置门禁让目录错误在任何可寻址/本地化写入前失败，并带 Deck/Card/Effect 身份；复用现有牌面解析器避免短键、文件大小写和导入规则分叉。记录键/id 一致性又使 Editor gate 与运行时 Luban 表 DataMap 使用同一身份口径。

**影响**：只影响 Editor validator、同步构建顺序、牌面解析器的 internal 复用 seam 与对应测试；运行时仍只通过 Addressables `card-art/{key}` 加载，且 `BattleCommandQueue`、Turn、settlement、公式、BattleSession、CardZones、Deck 内容和当前四张卡行为均不变。第一张 I3 `CatalogOnly` 行进入表前必须添加真实的 TinySpire 占位 Sprite。验证见 `06_testing/2026-08-06-sts2-ironclad-i2-build-isolation.md`。

## CD-059：冻结卡牌目录元数据，缺图复用既有占位并只维护交付清单

**问题**：I3 需要把冻结版本的全部单人战士卡录入 Card 表，但“录入目录”不能被误解为“规则已经可玩”，也不能为了补齐牌面而让 Agent 生成、下载或复制官方素材。目录还需要稳定表达版本身份、费用、目标、归宿与升级事实，并保证新增目标枚举不会意外放宽敌人行为配置。

**选择**：以 `sts2-v0.107.1-23811903-59260271` 为唯一目录快照，录入 85 张单人卡并排除多人专用 `DEMONIC_SHIELD`、`TANK`。Card 增加 `external_key`、`catalog_snapshot_key`、升级说明 key、类型、稀有度、Fixed/X 费用、升级费用、基础/升级归宿与 `has_upgrade`；`TargetRule` 增加 AllEnemies/RandomEnemy 只用于目录表达，敌人行为初始化仍显式只接受 Self/Enemy 并 fail-fast。3 张现有 STS2 卡保持 `Implemented`，其余 82 张保持 `CatalogOnly`。

缺图卡统一复用项目既有 `Assets/Arts/Runtime/Card/Texture/art_placeholder.png`，配置只写短键 `art_placeholder`，运行时地址固定为 `card-art/art_placeholder`，并只通过 Addressables/AssetBundle 加载。Agent 不得自行生成、下载或引用官方卡图；没有用户提供或明确授权的素材时继续使用占位图，并维护 `10_communication/2026-08-06-sts2-ironclad-card-art-checklist.md`。用户后续提供原创或已获授权素材时，才按清单短键替换，并执行 Luban、同步构建和真实 AB 加载验证。

**理由**：冻结的结构化目录让后续机制切片可以逐卡翻转状态而不反复重建身份；`CatalogOnly` 的运行时与构建期双门禁继续阻止空程序产生权威写入。复用既有占位图避免制造无授权素材，也避免增加第二套资源寻址规则；逐卡清单把美术缺口交给用户可见、可追踪的交付面。

**影响**：生产 Card 表共 86 行，其中冻结 STS2 单人卡 85 行，加项目自有 Strength 1 行；总计 4 张 `Implemented`、82 张 `CatalogOnly`。Card 文本需要 en/zh-CN 的名称、说明和升级说明 key；本地 Addressables 的 Card Art 组增加 `card-art/art_placeholder`。I3 不修改 Queue、Turn、settlement、公式、BattleSession、CardZones、Scene、Prefab、ProjectSettings、asmdef 或 HybridCLR。I4 将首次修改 `BattleTurnController` 的成功归宿，必须另行确认。验证见 `06_testing/2026-08-06-sts2-ironclad-i3-card-catalog.md`。

## CD-060：成功出牌归宿在首次写入前由 Card 配置冻结

**问题**：Card 目录已经能声明 Discard / Exhaust / Power，但 `BattleTurnController` 在全部 Effect 后固定调用 `DiscardFromHand`。真实 Exhaust 卡即使配置正确，也会落入弃牌堆；若归宿直到支付能量或提交 Effect 后才校验，非法配置还会留下半成品权威状态。

**选择**：`TryPlayCard` 在规则通过后、任何 Energy/Combatant/CardZones 写入前读取基础 `PlayDestination`，把 Discard / Exhaust 冻结为本次命令的内部选择；未知值与 Power 在首次写入前 fail-fast。成功路径继续保持 EnergySpent → 全部 Effect settlement → CardMoved，并分别复用既有 `DiscardFromHand` / `ExhaustFromHand`。不新增公开写入口、第二份归宿事实、settlement 类型或卡牌 ID 分支；升级实例归宿留给 I9，Power 留给 I11。

Tremble 作为 I4 的真实生产代表：1 费、Enemy、`ApplyVulnerable 3`、Exhaust，翻为 `Implemented` 但不进入默认 Deck。构建门禁同时锁定 STS2 可玩身份为 BASH / DEFEND_IRONCLAD / STRIKE_IRONCLAD / TREMBLE，状态拆分为 4 / 81，避免只改数量时把错误卡翻为可玩。

**理由**：归宿是一次成功出牌事务的末端路由，不是新的权威状态；在首写前冻结可以保持现有原子顺序，并让 Queue 与通用 CardMoved settlement 无需感知具体卡牌。用真实 Tremble 覆盖 Effect 后 Exhaust，能同时验证配置、命令顺序和 CardZones 事实。

**影响**：只修改 Turn 内部成功路径、I4 构建门禁、Tremble 作者表/生成数据/双语内容与测试。Hand→Exhaust 会更新 ExhaustPile 事实和 HUD 计数，但既有表现计划仍不创建飞行动画；I4 按用户边界明确不包含该动画。Queue、settlement、CardZones 公共契约、默认 Deck、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、DI 与启动流程不变。验证见 `06_testing/2026-08-06-sts2-ironclad-i4-success-destination.md`。

## CD-061：Hero 静态资源档案只装配每玩家事实，回合控制器独占补充与裁剪

**问题**：机枪兵需要与默认战士不同的 Energy/Ammo 初值、上限和后续回合补充，但全局 `GameConfig.EnergyPerRound` 无法表达角色差异。若把当前资源写回 Hero 表、HUD 或公开字典，会形成第二份可变事实；若首回合误叠加 `+3`，会把已确认的 3 Energy 变成满 5；若降上限后允许当前值超限，后续卡牌机制会读取非法状态。

**选择**：在 `battle.hero.xlsx` 为每个 Hero 增加六个静态字段：Energy/Ammo 的初值、上限和后续回合增量。`BattleSession.FromConfig` 在创建战斗聚合前校验这些值，并把 `CombatantId → BattlePlayerResourceProfile` 冻结后交给生产 Queue；`PlayerTurnData` 只保存当前值、当前上限和当前增量，`BattleTurnController` 是唯一重建它的位置。首回合使用静态 `initial_*`，后续回合只按当前事实执行 `min(current + gain, max)`；任意重建后的低上限立即以 `min(current, max)` 裁剪。

`max_ammo = 0` 表示该 Hero 未启用 Ammo，且要求其初值和增量都为零。Ammo 补充仍产生 source-only settlement，供未来机制和事务事实读取；当前表现计划显式忽略它，不提前创建 HUD 或动画。共享 `GameConfig.InitialHandCount` 继续决定补至 5；`GameConfig.EnergyPerRound` 暂保留给旧测试入口和当前只有 Hero `1001` 的 HUD 基线，生产路径不再把它作为战斗资源权威。

**理由**：静态档案与当前事实分层后，Hero 差异不需要全局资源字典或 UI 镜像，未来卡牌在同一 `PlayerTurnData` 事实上改变上限/增量也能被后续回合保留。首写前档案校验和敌人联合快照的完整资源比较保持 `BattleCommandQueue.Submit` 的原子性与既有权威边界。

**影响**：当前 Hero `1001` 明确为 Energy `3/3/+3`、Ammo `0/0/+0`，M10 的 3 Energy / 补至 5 基线不变。MG1 不创建机枪兵 Hero、Deck、卡牌、状态、UI、Prefab 或素材；真正可选择机枪兵时，HUD 必须在独立切片改为投影 `PlayerTurnData`，不能继续借全局能量值。验证见 `06_testing/2026-08-06-machine-gunner-mg1-hero-resource-profile.md`。

## CD-062：卡牌目录说明保留来源规则，CatalogOnly 仍由独立状态隔离

**问题**：Marine Game 压缩包的 64 张卡牌包含时机、触发、目标与升级等无法从现有 `Card` 元数据恢复的语义。若目录文本只显示“尚未实现”，作者表将失去回查 `cards.json` 的能力，也会把未来 Card Program 的行为差异隐藏在通用占位文案中；反过来，若因为文本已录入就把卡标为可玩，又会绕过 CD-057 的零写入隔离。

**选择**：以 `marine-game-v1-20260807-cards` 固定每张目录身份。`i18n.xlsx` 的 zh-CN 基础说明保留 `cards.json.desc`，升级说明保留 `known_upgrades.change`；源文件未提供英文规则原文，en 列以同一压缩包的结构化字段与行为说明录入项目内英文翻译。中文来源继续是规则追溯依据，英文文案不伪装为最终策划裁决。`implementation_status = CatalogOnly`、空 `effect_bindings`、Deck 门禁和占位图继续作为唯一的“尚未可玩”事实，牌面说明不承担该状态。

**理由**：把来源规则与可执行程序分开，既能让策划/实现人员从作者表追溯每张卡的原始语义，又能让当前运行时在任何费用、卡区或参与者写入前拒绝目录卡。后续 Card Program 将以这些说明和结构化源码为验收参照，但不能从自然语言直接执行或按卡名/ID 分支。

**影响**：影响 `battle.card.xlsx`、`i18n.xlsx`、生成的 `battle_tbcard.json`、卡牌目录门禁与 MG2A 测试；不改变现有四张 Implemented 卡、默认 Deck、Queue、Turn、Effect、Scene、Prefab 或 Addressables 逻辑地址。英文正式本地化与每张卡的可执行程序留给对应后续切片；验证状态见 `06_testing/2026-08-07-marine-game-mg2a-card-catalog.md`。

## CD-063：机枪兵程序是 Hero 会话私有深模块，唯一共享写入仍经 Queue

**问题**：机枪兵需要 Ammo、自动最近目标和兴奋剂额外命中，而默认战士不应承受职业状态或规则分支。若让 Hand UI、Card 配置文本或一个新的职业队列直接写资源/卡区/参与者，就会形成第二写入口；若按模板 ID、外部 key 或卡名分支，又会让配置身份和运行时规则耦合。

**选择**：生成 `HeroRuntimeProfile.MachineGunner` 与 `MachineGunnerProgramId`。只有 `BattleSession` 装配到该 Hero profile 时才创建 internal `MachineGunnerBattleRuntime`；`BattleCardPlayRules`、`BattleTurnController` 与 `HandCardContainer` 使用同一实例投影规则，所有事实写入仍从既有 `BattleCommandQueue.Submit` 到达回合控制器。首个可玩切片只映射五个强类型程序：`Shoot`、`Elbow`、`Block`、`Reload`、`Stim`；其余 enum 项在表中保持 `CatalogOnly`，不会伪装为已实现。

**理由**：Session-owned 深模块将职业可变事实限制在一场机枪兵战斗内，默认 Hero 1001、通用 Queue 与现有 Card Effect 语义无需复制或替换。生成 enum 是稳定边缘身份，避免从卡牌文本或原始配置 key 解释行为；同一规则实例再供 UI 只读使用，可防止自动/自身目标被错误禁用。

**影响**：初始牌可通过 Queue 原子支付 Energy/Ammo、结算参与者与卡区；Ammo 不足失败保持零写入。当前结算记录新增 Ammo 支付这一 source-only 类别，表现层显式不把它变成可见 cue。未来 MG3--MG7 必须在此深模块上增加可验证的目标/随机、状态/伤害、回合钩子与 Power；驻防、排气散热等选择不得借 UI 私有状态，需新增权威待决命令协议。验证见 `06_testing/2026-08-07-machine-gunner-mg2b-starter-runtime.md`。

## CD-064：机枪兵随机目标必须在整张卡成功提交后才推进职业随机流

**问题**：随机目标的候选需要在卡牌预构建期得到，但目标、弹药、效果和卡区的任一后续校验失败都不应改变这场战斗后续的随机序列。若选择器直接持有并推进运行时随机流，失败的出牌尝试会悄然污染扫射等卡牌的可复现结果。

**选择**：`MachineGunnerTargetSelector` 只消费调用方传入的 `GameRandom`；`MachineGunnerBattleRuntime` 在解析时克隆当前状态，保存候选状态，并只在效果和 Hand→Discard 提交完成后写回 `CardRandomState`。最近、最远、全体、第二近和自身不消耗随机；无存活敌人与伪造随机输入在候选生成前失败。

**理由**：将随机状态视作卡牌事务的一部分，使失败路径保持零写入，且不需要引入第二随机源或让 UI 提供随机结果。目标顺序仍唯一来自 Encounter，后续多段随机卡可以复用同一规则。

**影响**：目前五张初始牌的数值和随机状态均不变；MG3 的选择器测试固定了目标顺序、相同种子重放和失败零推进。后续启用随机程序时必须保持“本地预演、成功提交”边界，验证见 `06_testing/2026-08-07-machine-gunner-mg3-target-random.md`。

## CD-065：职业私有状态通过内部伤害公式覆盖接入既有 Effect 事务

**问题**：机枪兵的 Weakness、Smoke、Burn/Oil、Armor 与 Invisible 不能安全地塞入通用 `CombatantData`，但敌方已有 Effect 和职业初始牌都必须采用同一攻击顺序。若在 Effect 提交后再重算伤害，或让状态模块绕过 Queue 直接改参与者，会破坏预构建快照、结算顺序和唯一共享写入口。

**选择**：`MachineGunnerBattleRuntime` 独占 `MachineGunnerCombatState`，并以 internal `IBattleDamageFormulaOverride` 向既有 `BattleEffectExecutor` 提供只读计算和提交后的护甲钩子。攻击结果在预构建期冻结后才写入参与者；Armor 仅在冻结结果已经穿透生命时消耗。职业状态的回合钩子由既有 `BattleTurnController` 在 Queue 命令内调用，通用 Block/Vulnerable 仍归 `BattleStatusTiming`。

**理由**：这让同一职业状态同时覆盖初始牌和敌方攻击，却不扩张通用参与者模型、公开 Effect API 或 Queue 之外的写入面。纯公式与提交后钩子分离，也避免“护甲先扣、伤害后失败”或对已冻结伤害二次解释。

**影响**：只影响 Hero 1002 装配后的 Session；默认 Hero 1001 继续使用原始公式且不创建职业运行时。当前完整覆盖 Weakness、双向 Smoke、Vulnerable、Block、HP、Burn/Oil、Armor 与 SmokePersist 生命周期；延迟伤害、Invisible、恢复、束缚等其余机制留给后续卡牌切片。验证见 `06_testing/2026-08-07-machine-gunner-mg4-private-runtime.md`。

## CD-066：Power 是独立卡区事实，表现缺口不得伪装为可玩能力牌

**问题**：能力牌不能进入弃牌堆，否则持续效果没有可审计的归属；但当前手牌表现只有 Draw、Discard、Exhaust 三个 pile 锚点，动态临时牌还受初始插画预加载合同限制。若为了表现而把 Power 伪装成弃牌，或提前把所有能力牌翻成 `Implemented`，会让卡区事实和玩家看到的状态失真。

**选择**：`BattleCardZonesData` 增加第五个 `PowerPile`，并让 Hand→Power 保持普通、按序的 `BattleCardMovedSettlement`。表现计划明确可以为这一事实产生零可见步骤；只有拥有真实私有规则的 Power Program 才允许转入该卡区，其余目录卡继续由 `CatalogOnly` 门禁拒绝。职业手牌容量在同一私有运行时限制为 10，默认路径仍不改变原有抽牌目标。

**理由**：先固定真实归属和结算顺序，能让后续 Power HUD/图标/动画只是读取现有事实，而不是倒逼运行时存放 UI 镜像。零可见步骤是显式未实现边界，不会把不具备动态插画加载与奖励入口的卡伪装成可用内容。

**影响**：CardZones 的布局、唯一归属断言和手牌抽取都识别 `PowerPile`；既有 Hand→Discard/Draw→Hand 动画不变。当前生产 Deck 和默认 Hero 不含能力牌，未创建 Power UI、奖励/Run、临时牌动态加载或场景改动。验证见 `06_testing/2026-08-07-machine-gunner-mg4-private-runtime.md`。

## CD-067：X 费、变动弹药与随机多段命中在职业运行时内冻结为一张卡的支付快照

**问题**：机枪兵的 X 费、多段随机射击和“最多/全部消耗弹药”都依赖执行瞬间的资源与存活目标。若在每个 effect 结算时重新读取 Energy/Ammo，或让目标选择器直接推进会话随机流，会导致同一张卡的命中数随前段结果漂移，并使随后失败的卡牌尝试污染可复现随机序列。

**选择**：`MachineGunnerBattleRuntime` 在首次共享写入前解析 `MachineGunnerCostResolution`，冻结 Energy 支付、X 值、实际 Ammo 支付和 Stim 附加命中；随后基于局部投影预构建全部伤害/资源操作。随机程序从显式复制 `GameRandom.State` 的候选流逐段选择投影存活敌人，只有资源、效果和卡区移动整体成功后才把候选状态写回。`BattleCardPlayRules` 仅使用该运行时暴露的最小合法性和目标输入投影，不持有职业资源或随机事实。

**理由**：这把可变资源和 PRNG 一并视为卡牌事务的输入，而非每段 effect 的临时查询，保持失败零写入、随机可重放和 Queue 的唯一共享写入边界。显式复制状态避免底层随机构造器对种子的再次变换，故零段卡也不会意外推进随机流。

**影响**：本决定开启 TumbleReload、HoldLine、Spray、BayonetParry、WildRampage、QuickElbow、HeavyElbow、HurricaneElbow、PrecisionShot、SixHits 和 QuickManeuver；目录门禁以精确外部 key 集合锁定 16 张已实现卡。默认职业和其随机/费用规则不变。延迟伤害、超上限 Energy、手牌选择、动态临时卡、自动连锁与 Power 触发仍需独立协议。验证见 `06_testing/2026-08-07-machine-gunner-mg5-x-multishot-runtime.md`。

## CD-068：机枪兵即时状态以预演操作提交，私有状态不伪造 Effect ID

**问题**：机枪兵程序需要在同一张卡的伤害之后施加 Weakness、Smoke 或 Vulnerable。直接调用通用 `BattleEffectExecutor` 会绕过职业程序的整卡预演与零写入边界；私有状态没有对应的 `BattleEffectId`，而伪造枚举值会破坏配置来源和展示路由。

**选择**：`MachineGunnerProgramOperation` 显式表达目标范围、私有状态或通用 Vulnerable。运行时先在 `MachineGunnerProjectedCombatant` 中冻结伤害后存活、私有状态和 Vulnerable 的前后值，再在提交阶段校验实际前值仍与投影一致。Weakness/Smoke 提交为职业私有 `StatusApplied` settlement，保留来源、目标、顺序和值变更但不生成未知 UI cue；Vulnerable 调用既有 `CombatantData.ApplyVulnerableGain`，并以 `BattleStatusAppliedSettlement(EffectId = null)` 进入既有易伤图标脉冲。`BattleStatusAppliedSettlement` 的 Effect ID 因此允许为空，但只表示职业原生程序操作，而非虚构的配置 Effect。

**理由**：状态操作和前序伤害属于同一张卡的投影事务。这样全体伤害杀死的目标不会再收到状态，Smoke 的“自身加全体存活敌人”也可在一次预演内冻结；同时保留 Queue 的唯一共享写入边界和通用 Vulnerable 的既有时机/表现语义。

**影响**：本决定开启 StunGrenade、SmokeBomb、KidneyShot、PainfulElbow 和 SniperShot，使机枪兵快照精确达到 21 张已实现卡。它不提供逐段 OnShotHit、燃烧生命周期、Exhaust、动态多源交叉结算或私有状态 HUD；这些仍需各自的运行时切片与验收。验证见 `06_testing/2026-08-07-machine-gunner-mg5-immediate-status-runtime.md`。

## CD-069：已有完整 Power 程序以精确配置门禁开放，不为目录状态另造运行时分支

**问题**：六张机枪兵能力牌已经拥有程序注册、资源/状态提交、`PowerPile` 归宿和回合读取，但作者表仍把它们列为 `CatalogOnly`。若为“开放”再复制一套 Power 结算或由 UI 直接改动状态，会破坏 Hero 会话私有模块和 Queue 的唯一共享写入入口；反之，仅按数量翻转卡牌又可能误开放仍缺机制的目录卡。

**选择**：`BattleCardCatalogBuildValidator` 继续以 `MARINE_*` 外部 key 的精确集合冻结当前可执行集合，并把 `CoreExpansion`、`OutputAdjust`、`BlastShield`、`MagExpansion`、`SmokePersist` 与 `PowerOverclock` 纳入其中。只修改这六张作者表的 `implementation_status`，复用已有 `MachineGunnerCardProgramRegistry`、`CommitPowerActivation`、`BeginPlayerRound` 和 `GetPlayerRoundHandTarget`；不新增公开 API、Power UI、奖励入口或第二条写链。

**理由**：配置门禁应该反映已证明可执行的能力边界，而不是重复实现已存在的职业规则。用身份集合而非连续 ID 锁定可执行卡，能让后续某张未实现卡被误翻转时在构建和快照测试中立即失败。

**影响**：机枪兵快照变为 27 张 `Implemented` / 37 张 `CatalogOnly`；六张 Power 可经既有 Queue 事务进入 `PowerPile` 并影响资源、护甲、烟雾或下一回合抽牌目标。奖励/Run、Power 可视化、升级实例和其余 37 张卡仍不在本决定范围内。验证见 `06_testing/2026-08-07-machine-gunner-mg6-existing-power-runtime.md`。

## CD-070：Burn/Oil 由最后一名存活玩家结束行动的同一 Queue 命令结算

**问题**：Burn 的“玩家行动结束、敌人行动前”时机既不能为每名玩家重复结算全场，也不能新建绕过 `BattleCommandQueue.Submit` 的隐式伤害写入口。Oil 又要求 Burn 只消费施加前已有的 Oil，并允许 Oil 下降，不能复用只允许累加的通用状态预演记录。

**选择**：在 `BattleTurnController.TryEndPlayerAction` 已丢弃手牌、提交既有状态时机并标记当前玩家结束行动后，仅在 `HaveAllLivingPlayersEndedAction()` 为真时调用 `MachineGunnerBattleRuntime.ResolvePlayerRoundEnd`。该模块在同一命令内按 Encounter 顺序对存活敌人，再对机枪兵玩家生成 Debuff 伤害 settlement，并在敌方全灭后跳过玩家自燃；控制器立即重新派生终局，不再继续下一阶段。`ApplyBurn` 作为专门预演/提交记录，同时冻结 Burn 增加与 Oil 减少，并由一条纯计算函数供投影与真实状态共享。

**理由**：这样回合时机、结算顺序、Block 伤害、死亡中断和卡牌提交仍只有一条权威写链；Burn/Oil 的特殊双字段原子变化也不会放宽普通私有状态“只能增加”的错误边界。敌人先于玩家能避免全灭敌人与玩家自燃同时出现而项目不存在 Draw 终局模型的无效事实。

**影响**：开放 GasPump、Napalm、Molotov 与 FlameElbow 的基础值程序，机枪兵快照达到 31 张 `Implemented` / 33 张 `CatalogOnly`。这不实现升级实例、Burn HUD、BurningOil 的不耗 Oil 增长、逐段命中、Exhaust、奖励/Run 或第二条写入链。验证见 `06_testing/2026-08-07-machine-gunner-mg7-burn-oil-runtime.md`。

## CD-071：职业动态成本和本回合卡链只读预览与队首提交共用同一事实

**问题**：连肘是否免费依赖“本回合紧邻上一张成功卡”的会话私有事实，不能由静态 `Card.Cost` 判断。若 `BattleCardPlayRules` 先按通用固定费用拒绝、队首运行时又在稍后免除费用，0 Energy 的合格连肘会在交互层永远无法提交；若各自复制折扣逻辑，UI 与队首会随未来卡链规则漂移。

**选择**：将 `MachineGunnerBattleRuntime.TryPreviewCost` 作为不写状态的唯一成本派生函数，规则层和 `ExecutePlayerCard` 都读取它。运行时只在卡区成功归宿后记录 `NonShootAttack` 或 `Other`，并在 `BeginPlayerRound` 清空；合格连肘将本次冻结的 `EnergySpent` 置为 0，连续连肘因自身是非射击攻击而继续满足条件。功夫机甲和开火仍留在同一私有深模块：前者按成功非射击攻击卡的整卡完成触发一次，后者为每段常规射击在既有伤害管线中增加层数并在玩家行动结束清零。

**理由**：成本、卡链和伤害修正都从同一会话事实读取，既没有新命令或 UI 写入口，也不需要把职业规则扩张进默认 Hero。`battle.html` 的可执行分支虽然把狙击标作射击，但没有把 `firePower` 加到狙击伤害；本切片以该行为作为更具体的运行时口径，测试锁定“常规射击吃开火、狙击不吃开火”，同时不影响未来燃烧弹药对全部射击的独立判定。

**影响**：开放 KungfuMech (3212)、ElectroBoost (3236) 和 ComboElbow (3242)，机枪兵快照达到 34 张 `Implemented` / 30 张 `CatalogOnly`。升级实例、逐段 OnHit、BurningOil、Exhaust、奖励/Run、Power HUD 与第二条写链仍不在本决定范围内；验证见 `06_testing/2026-08-07-machine-gunner-mg8-kungfu-firepower-combo-runtime.md`。

## CD-072：逐段命中后的职业状态在同一张卡的投影事务内交错提交

**问题**：`SpikeShot`、`IncendiaryAmmo` 和 `AgedOil` 都以“每次命中后”为触发时机。若先累积整张卡的全部伤害、最后再统一上状态，Stim 的第二段命中读不到第一段新增的 Vulnerable，且 FlameElbow 的 Burn/Oil 顺序会被后续全局钩子反转；若命中后直接改写真实状态，又会绕过整卡预演、失败零写入和既有 Queue 权威链。

**选择**：`MachineGunnerCardProgram` 增加仅供攻击程序使用、只允许命中目标私有状态/Burn/Vulnerable 的 `PostHitOperations`。`MachineGunnerBattleRuntime` 预构建每一段实际伤害后，立即在同一局部投影中依序附加程序后置操作，再附加全局命中钩子：射击走 `IncendiaryAmmo`，非射击攻击走 `AgedOil`。所有操作仍作为原有 `ExecutePlayerCard` 的准备结果，通过同一 `BattleCommandQueue.Submit` 原子提交；伤害后死亡跳过后置状态，存活的零伤害或完全格挡命中仍执行后置状态。X/多段程序的钩子值不再被执行次数二次缩放。

**理由**：以逐段投影为唯一时序事实，既让后段伤害读取前段已生成的状态，也保持随机、资源、卡区和参与者写入的整卡原子性。`IncendiaryAmmo` 叠层来自原型的累加赋值；`AgedOil` 的原型是固定赋值，故多张只启用而不将 `Oil +2` 相乘。`HurricaneElbow` 的广义非射击逐段规则按每段 +2 实现，避免 X=3 被错误放大为 18；这比原型中遗漏该分支的局部代码更符合卡牌文字，且由回归明确锁定。

**影响**：开放 IncendiaryAmmo (3210)、SpikeShot (3248) 和 AgedOil (3253)，并将 FlameElbow、KidneyShot、PainfulElbow、SniperShot 的既有命中后状态纳入统一时序；机枪兵快照达到 37 张 `Implemented` / 27 张 `CatalogOnly`。不实现 BurningOil 的回合末不耗油增长、IncompleteCombustion 的 Exhaust/动态交叉结算、升级实例、奖励/Run、Power HUD 或第二条写入链；验证见 `06_testing/2026-08-07-machine-gunner-mg9-per-hit-runtime.md`。

## CD-073：烈火烹油作为回合末的专用非消耗 Burn 增长，而非普通 Burn 施加

**问题**：既有 `MachineGunnerCombatState.ApplyBurn` 的契约是读取旧 Oil 后把 Oil 向下减半，适用于 Napalm、Molotov 和 FlameElbow 的“施加 Burn”。`BurningOil` (3254) 则要求在回合末、所有 Burn 伤害前，对已有 Burn 的敌人增加 `1 + Oil`，Oil 不能变化，且多张副本不能把数值倍增。复用普通施加规则会直接改变 Oil 和本轮伤害；把它拆成新命令又会破坏既有回合末原子性与胜负中断。

**选择**：在 `MachineGunnerBattleRuntime.ResolvePlayerRoundEnd` 内保留既有存活敌人 Encounter 快照，并在 Burn 伤害循环前调用专用 `AppendBurningOilGrowthForLivingEnemies`。该 helper 只在 `GetPowerStack(BurningOil) > 0` 时，对每名仍存活且旧 Burn 大于零的敌人以 `_combatState.Add` 写入 `Burn += 1 + Oil`，并追加现有 `MachineGunnerPrivateStatusChangedSettlement`；不调用 `ApplyBurn`，不写 Oil，也不传入玩家。全部增长完成后才复用原有 Burn/Block/死亡/Victory 结算，仍由同一个 `BattleCommandQueue.Submit` 事务提交。

**理由**：这把“普通施加 Burn 且消费旧 Oil”与“持续 Power 在回合末读取 Oil 但不消费”的相反契约封装在同一职业私有深模块，而不泛化成目前只有一张卡使用的浅层 Burn 模式接口。持有层数继续可供 PowerPile/展示读取，但以大于零判定保证原型的赋值式启用语义；预先收集全部增长 settlement 则锁定原型的全体增长先于任一 Burn 伤害的顺序。

**影响**：开放 BurningOil (3254)，机枪兵快照达到 38 张 `Implemented` / 26 张 `CatalogOnly`。不实现 IncompleteCombustion 的 Exhaust、燃烧者×存活目标动态交叉结算和 Burn→Smoke，也不引入升级实例、奖励/Run、Power HUD 或第二条写入链；验证见 `06_testing/2026-08-07-machine-gunner-mg10a-burning-oil-runtime.md`。

## CD-074：不充分爆燃以专用预演记录冻结“来源快照 × 动态存活目标”

**问题**：`IncompleteCombustion` (3222) 同时包含“初始燃烧敌人是固定来源”和“每个来源出手时只打当前存活目标”两种不同快照语义。把它塞入通用 `Damage` 会丢失每段敌人来源身份；把 Burn→Smoke 提前或逐来源转换又会改变后续来源的伤害值。复用 `ApplyBurn` 还会错误消费 Oil，且普通状态增加记录不能表达 `Burn = 0`。

**选择**：在 `MachineGunnerBattleRuntime` 内建立仅供该 Program 使用的 `ResolveIncompleteCombustion` 预演操作及两种已准备记录。预演先按 Encounter 顺序捕获开始时存活且 `Burn > 0` 的来源和其 Burn 值；每个来源在随后按当时投影存活目标造成 Debuff 伤害，即使该来源已死亡也不取消其已捕获的一轮伤害。全部伤害后，才按 Encounter 顺序对仍存活敌人以 `Set` 写入 `Smoke += Burn`、`Burn = 0`。伤害 settlement 保留燃烧敌人为来源，状态 settlement 使用玩家为来源；不调用 `ApplyBurn`，不读写 Oil。卡牌的合法性仍由既有职业 Program 门禁和队首事务负责，归宿分支只为注册 Program 支持 `ExhaustPile`。

**理由**：专用记录把原型的双重快照、伤害先于转换、死亡来源继续出伤和 Oil 不变封装在职业私有深模块中，避免为一张卡放宽通用伤害/状态 API。预演和提交继续同属 `ExecutePlayerCard` 的一个 `BattleCommandQueue.Submit` 事务，因此卡牌、资源、目标、状态与终局的权威写链不增加第二条路径；终局仍在整张卡提交后统一判断。

**影响**：作者表将 IncompleteCombustion (3222) 翻为 `Implemented`，机枪兵生成目录达到 39 张 `Implemented` / 25 张 `CatalogOnly`。新增运行时回归覆盖完整交叉结算、死亡来源快照、无燃烧空操作和全敌死亡后的 Exhaust→BattleEnded 顺序，生成 JSON 快照另锁定费用、升级元数据、Self、ExhaustPile、状态与 Program；静态编译通过。2026-08-11 已在唯一既有 Unity Editor 执行 `Sync and Build All`，Console 记录本地 Addressables 成功构建（25.627 秒、无 Error），定向原生 EditMode `94b4d610258b4b05a896adfd20ca6428` 为 65/65 passed。升级实例、奖励/Run、Power HUD 与第二条写入链不在本决定范围内；记录见 `06_testing/2026-08-08-machine-gunner-mg10b-incomplete-combustion-runtime.md`。

## CD-075：爆炸肘将“立即触发现有 Burn”建模为全局命中钩子之后的 Debuff 追加段

**问题**：`ExplosiveElbow` (3252) 既是普通非射击攻击，又要求在命中后立即触发一次目标当前 Burn。若把这段伤害做成普通 Attack，会错误读取 Weakness、Smoke 和 Vulnerable 并消耗 Armor；若直接调用回合末 Burn 结算，会绕过整张卡预演/原子提交，并可能错误改变 Burn 或 Oil。旧 STS2 Mod handoff 的 1 Energy / 8 数值也与当前需求摘要明确采用的 `marine-game` 来源不一致。

**选择**：`MachineGunnerCardProgram` 以受限布尔声明 `TriggersCurrentBurnDebuffAfterGlobalHitEffects`，仅允许攻击程序启用。每段普通命中后，运行时保持既有“卡牌后置状态 → IncendiaryAmmo → AgedOil”投影顺序，再对仍存活且当前 Burn 大于零的目标追加同值 `MachineGunnerDamageKind.Debuff` 预演伤害；不调用 `ApplyBurn`，不写 Burn/Oil。3252 的目录 `Enemy` 保留为基础输入映射，程序内部仍按自动最近存活敌人选择；费用 2、基础 Attack 10 和 DiscardPile 继续采用摘要来源链，未采用未列入该链的 handoff 数值。

**理由**：一条受限程序声明既复用已存在的伤害计算、Block、投影和 settlement 提交，又不把只有 3252 需要的“当前 Burn 触发”泛化成新的通用伤害/回合 API。把追加段放在全局命中钩子之后，能让同一张卡先完成已有的燃烧弹药和陈年机油语义，再读取真正的当前 Burn；整张卡继续经唯一 `BattleCommandQueue.Submit` 事务提交，致死普通攻击自然跳过后续操作。

**影响**：作者表将 ExplosiveElbow (3252) 翻为 `Implemented`，机枪兵生成目录达到 40 张 `Implemented` / 24 张 `CatalogOnly`。回归锁定 Attack→AgedOil→Debuff 顺序、无 Burn、普通攻击致死跳过和 Debuff 致死后的 Discard→BattleEnded 顺序，以及生成 JSON 的原始元数据。升级实例、奖励/Run、Power HUD 与第二条写入链不在本决定范围内；验证见 `06_testing/2026-08-11-machine-gunner-mg11-explosive-elbow-runtime.md`。

## CD-076：光学迷彩将攻击后的隐身消耗与伤害词条解耦为受限程序声明

**问题**：`OpticalCamo` (3249) 需要在玩家行动结束和普通攻击成功后减少 Invisible，但狙击攻击不能破隐。现有 `IsSniper` 同时参与伤害公式：把 3248 `SpikeShot` 直接标为狙击来复用“不破隐”会错误移除其既有开火加成，且会把“伤害词条”和“隐身生命周期”混成一项事实。

**选择**：`MachineGunnerCardProgram` 新增只允许攻击程序设置的 `PreservesInvisibleAfterSuccessfulAttack` 声明；3247 `SniperShot` 与 3248 `SpikeShot` 显式设置为 `true`。`MachineGunnerBattleRuntime` 只在攻击牌已成功进入其卡区归宿后、且程序未声明保留时减少玩家 1 层 Invisible；失败出牌不触发。玩家行动结束仍独立减少 1 层。`MachineGunnerCombatState.ReduceDuration` 仅执行状态变化，具体生命周期时机由调用者定义。3249 本身是 Self 技能，支付 2 Energy 后用既有私有状态操作施加 `Invisible +2` 并进入 DiscardPile。

**理由**：把“成功攻击后是否保留隐身”作为一个受限声明，既复用现有状态、伤害和卡区原子事务，又不为单张卡扭曲 `IsSniper`/开火伤害语义。归宿成功后才减层使资源、目标或卡区失败路径保持零写入；行动结束时的独立减少保持来源所述的持续时间规则。该小型职业私有 seam 同时保留未来 3264 的狙击延迟行为另行设计，不提前泛化延迟/下回合时机。

**影响**：作者表将 OpticalCamo (3249) 翻为 `Implemented`，机枪兵生成目录达到 41 张 `Implemented` / 23 张 `CatalogOnly`。回归锁定施加与弃牌、普通肘击/射击消耗、3247/3248 保留且 3248 继续吃开火、行动结束先减层和失败攻击零消耗，并锁定生成 JSON 的费用、Self、DiscardPile、升级元数据、状态与 Program 49。角色半透明、Invisible HUD、升级实例、奖励/Run、Scene、Prefab、默认 Hero/Deck 与第二条写入链不在本决定范围内；验证见 `06_testing/2026-08-11-machine-gunner-mg12-optical-camo-runtime.md`。

## CD-077：Buffer 通过 Effect 链局部伤害序列冻结一次性受击防御

**问题**：`HoloDecoy` (3259) 需要使下一次正值攻击伤害完全无效，并在这次伤害之后消费一层可叠加、无回合衰减的 `Buffer`。通用 Effect 执行器会先预演整条 Effect 链；若预演直接写职业状态，失败路径会破坏零写入，若每一段都只读真实状态，同一条链的多段伤害又会错误地重复消费同一层。额外的 Buffer settlement 还必须计入下一条 Effect/敌人意图的 Order，否则连续 settlement 契约会被破坏。

**选择**：伤害公式覆盖以每个 `BattleEffectExecutionRequest` 的局部 `IBattleDamageFormulaOverrideSequence` 工作。序列仅在自身投影中预留目标的 Buffer，并为命中的正值攻击排入一次后续状态变化；提交时先写原始 `BattleDamageAppliedSettlement`，再以紧随其后的 Order 写 `MachineGunnerPrivateStatusChangedSettlement(Buffer before→before-1)`。`BattlePreparedEffectPlan.PlannedSettlementCount` 统一报告基础操作与所有局部后续 settlement，`BattleEffectExecutor`、`BattleEnemyActionExecutor` 和 `BattleTurnController` 都用该总数推进/校验 Order。零值攻击、非机枪兵目标或无 Buffer 不排入后续状态变化；被 Buffer 完全抵挡时不消耗 Armor。

**理由**：局部序列把“本条 Effect 链内已预留的防御层数”从真实状态隔离出来，既能先完整验证所有操作，又准确表达“同一条链两段攻击只由第一段消耗一层”。提交仍经原有 `BattleCommandQueue.Submit` 写入，原始伤害 settlement 保持在 Buffer 消耗之前，表现层可以看到完整、连续的事实顺序；通用执行器无需知道机枪兵私有状态的具体含义。

**影响**：作者表将 HoloDecoy (3259) 翻为 `Implemented`，机枪兵生成目录达到 42 张 `Implemented` / 22 张 `CatalogOnly`。回归锁定 Hand→Exhaust、一次/叠层 Buffer、零 Energy 的零写入、无回合衰减、一次 Effect 链内两段伤害只消费一次，以及敌人连续 Order；生成 JSON 快照锁定 Program 59、Cost 1、Self 与基础/升级 ExhaustPile。`README web.md` 的“升级后不消耗”与作者表的升级 ExhaustPile 相冲突，且当前没有升级 CardInstance，因此本决定只实现基础态、保留作者表字段，未把该差异伪装为已支持的升级行为。验证见 `06_testing/2026-08-12-machine-gunner-mg13-holo-decoy-runtime.md`。

## CD-078：下回合私有状态和 Queue 签发的强制结束行动

**问题**：`Retreat` (3216) 需要在成功出牌后结束玩家行动，并在下一玩家回合开始补满当前 Ammo 上限；`QuickRoll` (3235) 需要把可叠加的下挡总值转换为一次性 Block。若职业运行时直接调用结束行动或嵌套提交命令，会绕过 Queue 的 authority sequence、展示屏障和 continuation 所有权；若用全局延迟总线承载两个局部状态，又会提前扩大到尚未实现的延迟牌、奖励或 Run。玩家在撤退结算的发布期间若重入提交普通 Play/End，也不得获得额外行动窗口。

**选择**：将 `NextRoundBlock` 与 `ReloadAmmoAtNextPlayerRound` 作为 Hero 1002 的私有战斗状态，`MachineGunnerBattleRuntime.BeginPlayerRound` 仅返回冻结的回合开始结果。控制器按固定顺序写入：清除既有 Block；清除下挡并给予 Block；应用既有资源档案的普通补充；清除补满弹预约并将 Ammo 补至当前最大值；最后走既有抽牌。`MachineGunnerCardProgramExecutionResult` 只在卡片已成功归宿且战斗仍 Ongoing 时声明请求结束 actor，`BattleTurnOperationResult` 透传该最小事实；Queue 在本次 Play 的阶段 settlement 前保存它，并以 system token 冻结 `EndPlayerActionCommand` continuation。控制器在发布前设置同 actor 的强制结束锁，普通 Play/End 零写入拒绝，系统 continuation 成功后清锁。

**理由**：Queue 继续独占命令排序、token、continuation、展示屏障和 drain；职业模块只表达卡牌及回合开始的局部业务结果，不持有第二条写入链或待提交命令。两个延迟值保留在已经用于机枪兵私有状态 settlement 的会话对象中，因而可审计、可测试、不会跨战斗泄漏。固定时序使“下挡”和“补满弹”与既有 Block 清除、普通 Ammo +1 和抽牌的相对顺序可观察且稳定。

**影响**：作者表仅将 Retreat (3216) 与 QuickRoll (3235) 翻为 `Implemented`，机枪兵生成目录达到 44 张 `Implemented` / 20 张 `CatalogOnly`。回归锁定 15 Block、Hand→Discard、PlayCard→系统 EndPlayerAction、Ammo 0→1→5、下挡 0→5→10 后一次性清除与普通卡的单结果断言。`TacticalAdvance` (3234) 保持 CatalogOnly：免费攻击与 Stim 的额外弹药、以及尚未实现的 Bound 前置规则并未在本决定中猜测或实现。验证见 `06_testing/2026-08-12-machine-gunner-mg14a-retreat-quick-roll-runtime.md`。

## CD-079：游击战术将实际支付与名义触发弹耗分离

**问题**：`GuerrillaTactics` (3251) 每层要求“每消耗 1 弹药获得 1 Block”。将来免费的攻击和 `MachinegunBurst` 会分别出现实际不扣 Ammo、但仍应视为消耗弹药的规则；若只读取实际资源扣除，会使后续卡无法表达名义弹耗，若现在从卡名或卡牌 ID 推断，又会提前裁决尚未实现的免费攻击×Stim 组合语义。普通 Power 固定每张加 1 层，也无法表达 3251 的基础 2 层。

**选择**：`MachineGunnerCardProgram` 以仅供 Power 使用的 `PowerStackGain` 声明层数，默认 1，3251 显式为 2。`MachineGunnerCostResolution` 同时冻结 `AmmoSpent`（实际资源写入和 Ammo settlement）与 `AmmoSpentForGuerrilla`（游击触发用的名义值）；当前已有程序在成本预览中令二者相等。`TryPrepareOperations` 在原卡操作与既有功夫机甲后置钩子之后，仍在同一个投影序列中预演 `游击层数 × AmmoSpentForGuerrilla` 的 Block，只有整张卡能成功提交时才写入。

**理由**：层数、资源支付、Block 前值和失败零写入继续由同一职业深模块的预演事务冻结，提交仍只经过 `BattleCommandQueue.Submit`。分离字段保留未来免费/虚拟支付的明确声明位置，却不把尚未裁决的跨卡组合变成隐式默认；PowerPile 的实体卡数也继续与数值层数分开表达。

**影响**：作者表将 GuerrillaTactics (3251) 翻为 `Implemented`，机枪兵生成目录达到 45 张 `Implemented` / 19 张 `CatalogOnly`。回归锁定一张 Power=2 层、两张 Power=两个实例且总 4 层、正常 1 弹→2 Block、Stim 实际 2 弹→4 Block、成本失败零写入和 settlement 顺序。TacticalAdvance、固定机枪、临时 MachinegunBurst、升级实例、奖励/Run、Power HUD 与第二条写入链仍不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-mg14b-guerrilla-tactics-runtime.md`。

## CD-080：伤害段以 Kind + CardTag 声明并将防御后效局部预约

**问题**：新版 README 同时区分普通攻击、支援、炸弹、燃烧、射击和狙击。旧的 `Attack/Delayed/Debuff` 与分散的 `IsShoot/IsSniper` 布尔值无法表达“支援吃目标易伤但不吃来源力量”、“纯狙击不吃开火”或 Spike 的双词条；若每张程序传递力量/烟雾/易伤行为布尔值，规则会散落且无法稳定覆盖通用 Effect。DefenseTarget 的 Intangible 又需要在同一 Effect 链内逐段消费，不能在预演阶段写真实状态。

**选择**：`MachineGunnerDamageRequest` 只携带 `MachineGunnerDamageKind` 与 `[Flags] MachineGunnerCardTag`（`Shoot`、`Sniper`、`Shotgun`）。Pipeline 的 private rule profile 集中解释 Attack、Support、Bomb、Burn、Debuff 的修正；调用方不得选择 Smoke、Vulnerable、FirePower、ArmorBreak 或防御消费策略。`Shoot` 读取 Stim/FirePower/IncendiaryAmmo，纯 `Sniper` 只读取 IncendiaryAmmo 与狙击倍率、免来源 Smoke，`Shoot | Sniper` 两者并存。Effect 伤害序列以局部投影预约 Buffer/Intangible：Buffer 优先；否则正值 incoming Attack 在 Block 前封顶为 1 并预约 Intangible -1；提交始终是 Damage 后的私有状态 settlement。

**理由**：两维声明把“伤害段的结算方程”与“卡牌来源的横切词条”分开，同时把细节隐藏在职业深模块，避免外部调用者构造错误组合。局部预约保持预演失败零写入、多段逐段消费和连续 Order；Buffer 优先是来源未定义组合时记录在案的最小实现决定，而非伪装成原始规则。

**影响**：ElectroBoost (3236) 改为 Uncommon Power、FirePower +3 战斗持续；DefenseTarget (3262) 翻为 Exhaust/Implemented，当前既有 64 模板目录为 46 / 18。回归锁定纯狙击与双词条射击、Support/Bomb/Burn 方程、Intangible 分段封顶、Buffer 优先、2/3/9 弹阈值、失败零写入及生成元数据。尚未录入的新 18 张模板、延迟效果、升级实例、AnyAlly、奖励/Run、UI、Scene、Prefab 和第二条写入链不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2a-damage-taxonomy-defense-target.md`。

## CD-081：V2 扩展目录以独立快照冻结 CatalogOnly 身份

**问题**：新版 README 将机枪兵目录从既有的 64 个模板扩展到 82 个。若把新增 18 张与 V1 的 64 身份混入同一历史快照，旧目录回归会失去稳定的边界；若只生成 Program enum 而不额外校验状态、绑定和插图，新卡又可能因配置漂移被错误开放为可执行卡牌。

**选择**：保留 V1 64 模板快照与其原有门禁，另以 `marine-game-v2-20260812-cards` 建立扩展快照。扩展校验只接受 3265–3282、Program 65–82 的精确 18 个外部键，且每项必须为 `CatalogOnly`、`art_placeholder`、空 `effect_bindings`、带升级元数据。`ValidateCurrentProject()` 同时执行两个快照；运行时继续先由 `BattleCardPlayRules` 拒绝非 `Implemented` 卡，再触及职业程序注册表。

**理由**：目录身份、可玩状态和运行时程序是三个不同事实。独立扩展快照既保持已验证的 64 卡历史契约，也能在下一张 V2 卡真正具备程序、测试和精确表项翻转之前，阻止“配置已存在”等同于“可以打出”的误判；没有引入奖励/Run、默认 Deck 或第二条写入链。

**影响**：作者表、本地化与生成配置现在包含 82 个模板，目录计数为 46 `Implemented` / 36 `CatalogOnly`。V2B 回归锁定 18 个身份、连续 ID/Program、所有 CatalogOnly 属性与“把 Mark 改为 Implemented 必须失败”的构建门禁；Luban、本地化、Sync/Addressables、定向 112/112 与完整 586/586 EditMode 已通过。延迟效果、卡牌标签映射、升级实例、AnyAlly、默认 Deck、奖励/Run、UI、Scene、Prefab 与任何新 V2 卡的可执行程序仍不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2b-catalog-extension.md`。

## CD-082：V2C 破甲即时卡以既有状态与统一伤害链接入

**问题**：V2 目录中的铝热炸弹（3273）和踏碎（3281）都要求施加持续的破甲，但不能因为共享状态已存在就绕过卡牌程序、表状态和目录门禁；铝热炸弹还需要固定“燃烧与破甲”的程序顺序，踏碎则必须保证破甲只在普通攻击成功命中且目标仍存活后施加。

**选择**：3273 在同一出牌事务中声明两个既有操作：先 `ApplyBurn(4)`，再 `ApplyPrivateStatus(ArmorBreak, 2)`，两项均按 Encounter 顺序处理全部存活敌人；Burn 继续沿用油料交互，ArmorBreak 持续而不随回合衰减。3281 使用既有自动最近目标和 9 点 `Attack`，并以逐段命中后的既有操作在目标仍存活时施加 `ArmorBreak +4`。V2 扩展快照只把这两个外部键列为 `Implemented`，其余 16 张保持 `CatalogOnly`。

**理由**：现有 `MachineGunnerCombatState`、Burn/Oil 预演、攻击后置状态、伤害管线与私有状态 settlement 已完整表达这两个基础态，无需增设生命周期、延迟调度、目标输入、标签推断或第二写入链。把“先燃烧后破甲”和“攻击后再破甲”写入程序与回归，避免未来重排操作时静默改变规则。

**影响**：机枪兵目录现在为 82 模板、48 `Implemented` / 34 `CatalogOnly`；3273/3281 未加入默认 Deck、奖励或 Run，未实现升级实例、多人、UI、Scene 或 Prefab。Luban、本地化导入、Sync/Addressables、定向 81/81 和完整 589/589 EditMode 已通过；验证见 `06_testing/2026-08-12-machine-gunner-v2c-armor-break-instant-cards.md`。

## CD-083：击退射击以双目标快照和独立 LoseStrength 行动结束计划接入

**问题**：`KnockbackShot` (3223) 的目录目标规则只能表达 `Enemy`，但来源行为要求分别结算最近与第二近敌人；若逐段重新选目标，第一段击杀后会把第二段错误递补到第三名敌人。其“失去力量”又是对攻击力的独立减值和行动结束生命周期，不能复用伤害倍率型 `Weakness`、受击放大型 `Vulnerable`，也不能直接改写可能为负的永久 `Strength`。敌方清除时机还必须位于自己的行动完成后、意图推进前，并保持 settlement Order 连续。

**选择**：3223 使用职业私有的自动前两名存活敌人选择模式，并拒绝显式 `TargetId`。程序在施放预演时按 Encounter 顺序一次快照最多两名目标，将 7 点和 3 点 Attack 分别绑定到对应快照位置；缺少第二名时跳过第二段，任何前段死亡都不重新选择。每段只在该目标仍存活时追加 `LoseStrength +2`。卡支付 1 Ammo，但不因名称或弹药成本推断 `Shoot` / `Sniper` 标签。`LoseStrength` 作为独立非负职业私有状态进入伤害管线，Attack 的来源项固定为 `max(0, baseDamage + Strength - LoseStrength)`，Burn 等非 Attack 伤害不读取它。行动结束使用可预演、可校验、可提交的 actor 计划清零该状态并写私有状态 settlement：敌人在其 Effect / completion 后、intent advance 前提交，玩家在自己的行动结束阶段、回合末 Burn 前提交。

**理由**：一次快照把“最近”和“第二近”定义为同一张卡开始结算时的稳定身份，避免死亡造成隐式重定向；受限目标模式也不会把双目标语义扩散为通用 `TargetRule` 或 UI 输入协议。独立状态保留 `Strength` 的原有事实和可能为负的语义，并明确区分失去力量、虚弱与易伤。行动结束计划沿用既有准备/校验/提交事务和连续 Order，使敌人意图推进前的清除可观察、失败路径零写入，且不新增 Queue 之外的写入入口。

**影响**：作者表只将 KnockbackShot (3223) 翻为 `Implemented`；82 模板目录达到 49 `Implemented` / 33 `CatalogOnly`，V1 为 47/17，V2 扩展仍为 2/16。基础态的 7/3 Attack、各 `LoseStrength +2` 已接入；升级 9/5 与 +3 仍是元数据，卡也未加入默认 Deck、奖励或 Run。Luban、本地化导入、Sync/Addressables、静态编译、Unity MCP 定向 10/10 与完整 597/597 EditMode 已通过；验证见 `06_testing/2026-08-12-machine-gunner-v2d-knockback-lost-strength-runtime.md`。

## CD-084：V2E 以职业私有实例调度器和阶段联合计划承载延迟支援

**问题**：女妖、火力支援、燃烧轰炸、炸弹、三连击延迟狙击与钢针都跨越出牌命令和后续回合阶段，并允许同种效果多实例共存。若复用 Power 层数字典，会丢失每次施放各自的倒计时、冻结数值与创建顺序；若由卡牌程序在未来阶段直接写战斗状态，便会形成 `BattleCommandQueue.Submit` 之外的共享写入路径。来源还没有逐项裁决 Support/Bomb/钢针修正、同阶段多实例与触发时随机选择，因此继续隐式推断会让规则随调用点漂移。

**选择**：`MachineGunnerBattleRuntime` 私有持有独立的 ScheduledEffect 实例集合；实例冻结类型、来源、基础态数值、剩余触发/倒计时与单调插入顺序。成功出牌的职业计划在同一事务中创建实例并写 Created settlement；后续阶段由 Queue/Turn 的既有稳定接缝向 Runtime 请求一份可预演、可校验、可提交的联合计划，按实例插入顺序写 Triggered、Countdown、Removed 及伤害/状态 settlement，所有 Order 连续。round-start 计划位于最后一名敌人行动成功之后、敌方 Smoke 清理、玩家资源补充和抽牌之前；round-end 在弃牌与 Shackle/LoseStrength/既有临时状态清除之后、Burn 之前结算 Bomb 类实例。战斗终局跳过剩余计划，并清空未触发实例。

伤害和目标口径固定在职业模块内部：Support 读取目标 Smoke、Vulnerable 与 ArmorBreak，不读来源修正；Bomb 只读取目标 Smoke；钢针 Delayed 只读取目标 Smoke。每个随机段在触发时从当前投影的存活敌人重新选取，随机状态只随完整阶段计划成功提交。女妖每次触发先锁定一个当前最近目标，同次触发不重定向；三连击延迟段取当前最远目标。燃烧轰炸逐波逐目标执行 `Support → 若存活则 ApplyBurn（含旧 Oil 交互）→ Oil`。这些来源未完整说明的细节按用户授权的“脑补”明确冻结为项目决定，不冒充上游逐字事实。

**理由**：独立实例保留多次施放的身份、进度和稳定顺序；联合投影计划使同一阶段的随机选择、死亡过滤、Burn/Oil 和状态后效能在写入前整体校验，正常路径仍由 Queue 独占排序、drain、continuation、barrier 与 fault。以 DamageKind 统一解释 Support/Bomb/Delayed，可防止卡牌调用方各自拼装 Weakness、Vulnerable、Smoke 或 ArmorBreak 布尔值。Created/Triggered/Countdown/Removed settlement 让延迟生命周期可测试、可审计，同时表现层当前可选择静默忽略其内部记录。

**影响**：3237、3238、3239、3240、3241、3264、3274 的基础态翻为 `Implemented`；82 模板达到 56 `Implemented` / 26 `CatalogOnly`，V1 为 53/11、V2 为 3/15。Luban、本地化导入、Sync/Addressables、定向 101/101 与完整 606/606 EditMode 已通过。当前原子性保证止于每份 round-start 联合计划：最后一名敌人的行动事务已经在计划准备前提交，尚无横跨“敌人行动 + round-start 延迟阶段”的回滚；正常串行触发已验证，但异常提交故障下的完整跨域原子回滚不在本决定内。恢复仍未实现，升级 CardInstance、默认 Deck、奖励、Run、UI、多人及其余 26 张 `CatalogOnly` 也不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2e-delayed-support-scheduler-runtime.md`。

## CD-085：V2F 烟雾、防御与标记复用既有即时事务

**问题**：`ChainSmoke` (3269) 的本地化卡名带有抽牌暗示，但来源行为只声明 Smoke；若按名称推断 Draw，会绕过精确程序契约。`EmergencyCooling` (3272) 的 Block 与 Smoke 具有可观察顺序。`Mark` (3280) 虽然是消耗 Ammo 的 Attack，却没有 `Shoot` / `Sniper` 词条；若从类型或成本猜测标签，会错误触发 Stim、FirePower 与 IncendiaryAmmo。Mark 的破甲还必须只写入攻击后仍存活的目标。

**选择**：三张卡只通过生成的 `MachineGunnerProgramId` 注册基础态程序，不读取名称或显示文本。3269 使用来源范围的 `ApplyPrivateStatus(Smoke, 5)`，不创建 Draw 操作；3272 在同一事务中按 `GainBlock(8) → ApplyPrivateStatus(Smoke, 3)` 排序；3280 显式为 `Tags.None` 的普通 Attack，支付 1 Ammo、造成 5 点 Damage，并通过既有 post-hit 存活门禁追加 `ArmorBreak +2`。资源不足继续在预演/校验阶段失败，保持资源、状态、卡区和随机流零写入。

**理由**：现有 GainBlock、Smoke、普通 Attack、后置状态和事务门禁已能完整表达三张基础卡，无需新增 Draw、状态、伤害种类、事件总线或第二条写入路径。把 `Tags.None` 与操作顺序写进程序和回归，可防止未来因卡名、卡型或弹耗做隐式分类而改变规则。

**影响**：3269、3272、3280 的基础态翻为 `Implemented`；82 模板达到 59 `Implemented` / 23 `CatalogOnly`，V1 为 53/11、V2 扩展为 6/12。Luban、本地化导入、Sync/Addressables（11.414 秒）、定向 83/83 与完整 611/611 EditMode 已通过。升级实例、默认 Deck、奖励、Run、UI、多人、临时卡、选择和自动免费攻击仍不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2f-smoke-block-mark-runtime.md`。

## CD-086：V2G 私人改装基础态复用既有 Power 事务

**问题**：`PrivateMod` (3268) 同时要求“弹药上限 +1”和“开火 +1”，但提高 AmmoMaximum 不等于补充当前 Ammo。若复用装填语义或在扩容时把当前 Ammo 一并提高，会改变来源规则；若在 Power 提交后另行追加 FirePower，则会把一张卡拆成不可联合校验的两次写入。FirePower 已有逐段 Shoot 伤害语义，也不应为本卡再建命中后事件。

**选择**：3268 注册为 1 Energy / Self / PowerPile 的 `PrivateMod` Power 程序。既有 Power 预演/提交在成功出牌事务中增加一层 PrivateMod，把 AmmoMaximum +1 并原样保留当前 Ammo；同一程序操作再为来源增加 `FirePower +1`。后续 Shoot 的每段伤害继续通过既有伤害规则读取 FirePower，后续装填继续补至当时的 AmmoMaximum。资源不足仍在提交前失败，不产生资源、状态、Power、随机流或卡区写入。

**理由**：把上限扩展、Power 层数、FirePower 和卡区归宿保留在既有权威出牌事务内，既能表达来源的双重基础态，又不新增第二条共享写入路径或射击事件总线。显式保持当前 Ammo 也把“容量”和“现有弹量”区分为两个事实，避免未来扩容能力隐式获得装填收益。

**影响**：3268 基础态翻为 `Implemented`；82 模板达到 60 `Implemented` / 22 `CatalogOnly`，V1 为 53/11、V2 扩展为 7/11。Luban、本地化导入和最终 Sync/Addressables（4.376 秒；首轮 11.092 秒后重新导入再构建）、定向 85/85 与完整 613/613 EditMode 已通过。升级实例、默认 Deck、奖励、Run、UI、多人、临时卡、选择、自动免费攻击及其他跨卡协议仍不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2g-private-mod-runtime.md`。

## CD-087：V2H 焚风以跨参与者预演原子转换烟雾与燃烧

**问题**：`FoehnWind` (3276) 的基础值来自施放者结算时的当前 Smoke，而 Burn 与可能减半的 Oil 属于目标；它不是固定值 `ApplyBurn`，也不是伤害。若把“目标施加燃烧”和“来源烟雾清零”拆成两个普通操作，便无法在写入前联合冻结、核对跨参与者事实，还可能在前半段成功后留下未清除 Smoke。来源 Smoke 为 0 时又应成功支付费用并弃牌，而不是伪造 `0→0` 状态记录或判为失败。

**选择**：3276 注册为 2 Energy / Skill / 显式 Enemy / DiscardPile 的程序，并使用专用 `ConvertSourceSmokeToTargetBurn` 复合操作。预演阶段读取来源当前 Smoke；值大于 0 时，用它作为基础 Burn 调用既有 Burn/Oil 计算，联合冻结目标 Burn、目标 Oil 与来源 Smoke 快照。提交前先核对三项事实，再按目标 Burn → 仅在 Oil 变化时记录目标 Oil → 来源 Smoke 归零的顺序一次提交。Smoke 为 0 时该操作返回空，但出牌的能量支付和卡区归宿仍正常提交。能量或显式敌方目标门禁失败时不进入上述写链。

**理由**：专用复合操作把一张卡的跨参与者状态转换保留在既有权威出牌事务内，同时复用项目唯一的 Oil 加成与减半规则，避免复制燃烧公式或增加 Queue 外写入入口。显式的空 Smoke 成功语义也区分了“没有可转换状态”和“不能出牌”，并保持 settlement 只记录真实变化。

**影响**：3276 基础态翻为 `Implemented`；82 模板达到 61 `Implemented` / 21 `CatalogOnly`，V1 为 53/11、V2 扩展为 8/10。Luban、本地化导入、Sync/Addressables（12.164 秒）、定向 89/89 与完整 617/617 EditMode 已通过。升级实例、默认 Deck、奖励、Run、UI、多人、临时卡、选择、自动免费攻击、AnyAlly 及其他跨卡协议仍不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2h-foehn-wind-runtime.md`。

## CD-088：V2I 充能爆射按施放快照序号线性生成纯狙击段

**问题**：`ChargedBurst` (3282) 不是普通的全体同值伤害。它要求对施放时的存活敌人按 Encounter 序号，让基础 12 每名线性增加 50%；若逐段重新查询存活列表，前段致死会让后续目标向前递补并错误降低伤害。该卡还必须是纯 Sniper：不能因为 Attack 或全体多段而隐式获得 Stim、FirePower 或 Shoot 语义，同时仍需逐目标读取 IncendiaryAmmo 并保留 Invisible。

**选择**：3282 注册为 2 Energy / Attack / AllEnemies / DiscardPile 的程序，目标输入模式使用 `AllLivingEnemies`，显式 `TargetId` 由门禁拒绝。准备阶段按 Encounter 顺序冻结当时全部存活目标及其零基序号；`LinearDamageByTargetOrdinal` 按 `baseDamage + baseDamage × 50% × ordinal` 生成每段基础值，因此基础 12 依次为 12、18、24。后续结算只检查快照目标是否仍存活，不删除槽位或重排序号。程序只声明 `Sniper` 标签与 `preservesInvisibleAfterSuccessfulAttack`：不声明 `Shoot`，由既有伤害规则逐段读取 Invisible、Vulnerable 与 IncendiaryAmmo，但不读取 Stim 或 FirePower。

**理由**：冻结目标身份和序号把“谁是第几名”定义为同一张卡开始结算时的稳定事实，避免死亡改变后续段的基础值。专用执行类型只描述序号到基础伤害的映射，修正、燃烧弹药和隐身仍由既有强类型 Sniper 规则处理；这既不复制伤害公式，也不新增名称、UI 或调用方布尔值驱动的旁路。费用、逐段伤害、逐目标燃烧弹药与卡区归宿继续在同一权威出牌事务中提交。

**影响**：3282 基础态翻为 `Implemented`；82 模板达到 62 `Implemented` / 20 `CatalogOnly`，V1 为 53/11、V2 扩展为 9/9。Luban、本地化导入、Sync/Addressables（11.456 秒）、定向 94/94 与完整 622/622 EditMode 已通过。升级实例、默认 Deck、奖励、Run、UI、多人、临时卡、选择、自动免费攻击、AnyAlly 及其他跨卡协议仍不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2i-charged-burst-runtime.md`。

## CD-089：V2J 过载供能与防御姿态共享一次性下回合能量净修正

**问题**：`Overload` (3213) 需要在当前回合即时获得能量，并让下一回合少补 1 Energy；`DefensiveStance` (3271) 则需要先给 Block，再让下一回合多补 1 Energy。把两者压成一个可正可负字段会丢失各自叠层和清除事实；把当前回合主动获得也伪装为回合开始补给，则会混淆 `BattleEnergyGainedSettlement` 与 `BattleEnergyRefilledSettlement`。同时，下一回合的正负修正可能相互抵消或超过基础补给，必须在一次回合开始结算中冻结并避免负补给。

**选择**：3213 注册为 0 Energy / Self / DiscardPile，按 `GainEnergy(2) → NextRoundEnergyGainPenalty +1` 提交；即时获得受当前 EnergyMaximum 硬上限裁剪，仅记录实际变化量。3271 注册为 1 Energy / Self / DiscardPile，按 `GainBlock(8) → NextRoundEnergyGainBonus +1` 提交。Bonus 与 Penalty 使用两项独立、非负、可叠加的职业私有状态；下一玩家回合开始时以 `effectiveGain = max(0, baseGain + bonus - penalty)` 计算补给，再按 EnergyMaximum 裁剪，随后在同一次回合开始流程中分别清零两项状态。主动获得能量继续产生 `BattleEnergyGainedSettlement`，回合开始补给只产生 `BattleEnergyRefilledSettlement`。

**理由**：独立状态保留了来源、叠层与可观察清除语义，而回合开始只消费一次净修正，避免先加后减造成中间能量写入或错误越界。复用既有权威出牌事务、资源上限和回合开始资源档案，也让能量、Block、状态、卡区及失败零写入保持在现有 Queue 边界内，不新增第二条共享写入路径。

**延期边界**：`LimitOverload` (3260) 继续为 `CatalogOnly`。其“抽牌到手牌满”不能直接使用 `DrawCards(10)`：当前出牌事务在操作提交完成后才将本卡从 Hand 移至 DiscardPile，普通抽牌操作会把仍在手中的 3260 计入容量，从而少抽一张。该卡必须先建立以成功归宿后的投影 Hand 为输入、能在首次写入前冻结并校验抽牌数量的“抽至满手”卡区预演/提交 seam；在此之前不得用固定抽牌数、提前移牌或额外补抽伪装实现。

**影响**：3213 与 3271 基础态翻为 `Implemented`；82 模板达到 64 `Implemented` / 18 `CatalogOnly`，V1 为 54/10、V2 扩展为 10/8。Luban、本地化导入、Sync/Addressables（11.72 秒）、补强后的定向 136/136 与完整 631/631 EditMode（17.642 秒）已通过。升级实例、默认 Deck、奖励、Run、UI、多人及 3260 的抽至满手协议仍不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2j-round-energy-runtime.md`。

## CD-090：V2K 便携帮手作为即时射击段后的受限同目标伤害，而非通用命中事件

**问题**：`PortableHelper` (3267) 要求多个帮手在每次射击后分别攻击原目标，并读取开火、易伤与破甲。若建立全局“造成伤害”事件，帮手自身、延迟支援、炸弹或其他非卡牌伤害都可能误触发，甚至形成递归；若把帮手并入来源攻击数值，又会丢失逐帮手 Block/HP、致死停止和 settlement 顺序。来源射击还已有卡牌后置状态、IncendiaryAmmo、AgedOil 与当前 Burn 等顺序，帮手必须位于这些既有钩子之后。

**选择**：3267 注册为 1 Energy / Self / PowerPile 的 Power 程序，每次成功施放增加一层 `PortableHelper`。只在即时卡牌伤害的 `AppendPreparedHitAndPostHitOperations` 边界中，在来源 Damage 与全部既有 post-hit/global hooks 之后检查 `program.IsShootCategory`；若原目标仍存活，则按 Power 层数逐个向同一目标附加独立的 `MachineGunnerDamageKind.PortableHelper` 伤害操作，任一段致死后停止。该伤害类型的基础值为 1，只读取来源 FirePower、目标 Vulnerable 与 ArmorBreak，并经过目标 Block/HP；它忽略 Strength、Weakness、双方 Smoke、Invisible 与狙击倍率，使用 `Tags.None`，因此不会触发 Stim、IncendiaryAmmo、AgedOil、KungfuMech、帮手递归、Ammo 或 Invisible 生命周期。

**理由**：受限钩子把触发来源限定为“卡牌即时射击的真实分段”，同时复用既有局部投影和整张卡的 Queue 原子提交。每个帮手保留独立伤害记录，能够准确表达同目标、逐层格挡、逐层致死停止与连续 Order；专用伤害档案则把策划指定的三项增幅集中在伤害管线内，不让调用方散落布尔开关，也不扩大为当前没有消费者的通用事件总线。

**结构边界**：`IsShootCategory` 包含 Shoot、Sniper 与 Shotgun，因此 Shotgun 在未来出现即时卡牌伤害段时会进入同一钩子；当前没有 Shotgun 卡实例，故只有结构契约、没有直接运行验收。延迟 Support、Bomb、Needle 与 TripleStrike 延迟段不经过即时卡牌命中入口，结构上不会触发帮手；本决定不把这一跨模块排除伪报为实际卡牌测试，也不改变延迟调度器。

**影响**：3267 基础态翻为 `Implemented`；82 模板达到 65 `Implemented` / 17 `CatalogOnly`，V1 为 54/10、V2 扩展为 11/7。Luban、本地化、Sync/Addressables（12.163 秒）、定向 120/120 与完整 639/639 EditMode（131.4561842 秒）已通过。升级实例、默认 Deck、奖励、Run、UI、多人及其余 17 张目录卡不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2k-portable-helper-runtime.md`。

## CD-091：V2L 狂轰滥炸在四类延迟 Support 触发时读取当前层数并先缩放载荷

**问题**：`Bombard` (3265) 要让支援效果随 Power 层数提高，但现有延迟调度同时包含 Support、Bomb 与 Needle 等不同伤害语义，燃烧轰炸还组合 Damage、Burn 与 Oil。若按任意伤害 settlement 或卡名建立通用钩子，会误放大炸弹、钢针、回合末燃烧、即时攻击和便携帮手；若在创建延迟实例时快照层数，则先安排支援、后打出狂轰滥炸不会影响将来触发，也无法让多次触发读取当时的真实 Power。原始来源还没有规定百分比出现小数时如何取整。

**选择**：3265 注册为 1 Energy / Self / PowerPile 的 Power 程序，每次成功施放增加 4 层 `Bombard`，卡本身不产生即时伤害或状态。只在 `BansheeStrike`、`FireSupport`、`FireBombardment` 与 `TripleStrike` 四种 scheduled effect 的实际触发准备路径读取当前 Power 层数；每层对声明载荷增加 10%，正值按 `floor((baseValue × (100 + 10 × stacks) + 50) / 100)` half-up。该取整口径是经用户授权“脑补”后冻结的项目决定。女妖、火力支援与三连击只换算 Support 基础伤害；燃烧轰炸分别换算 Support Damage、Burn 与 Oil。缩放后的伤害继续进入既有 Support 管线，燃烧轰炸继续按 `Damage → 存活后 Burn → Oil` 准备和提交。

**理由**：以 scheduled effect kind 为白名单把增幅限制在需求明确的四类支援，不需要扩展全局伤害事件，也不改变现有调度实例的身份、顺序或事务边界。触发时读取让先创建的延迟支援能够看到后续 Power，同时让女妖的每次触发自然读取当时层数。先缩放声明基值、后走现有 Support 管线，保留目标 Smoke、Vulnerable 与 ArmorBreak 的单一公式来源；燃烧轰炸的状态载荷则沿用原 Burn/Oil 交互和致死门禁。

**排除边界**：GuidedNuke / FiveHundredPounder 的 Bomb、NeedleStorm 的 Delayed、回合末 Burn、即时攻击、便携帮手与其他非声明来源不调用该缩放入口。命中数、波次数、倒计时、目标选择、触发/移除 settlement 与 Support 伤害档案均不因本决定改变；本决定也不建立通用“支援增伤”状态或事件总线。

**影响**：作者表只将 Q155（3265）翻为 `Implemented`；82 模板达到 66 `Implemented` / 16 `CatalogOnly`，V1 为 54/10、V2 扩展为 12/6。Luban、本地化导入和 Sync/Addressables（12.963 秒）已通过；Unity MCP 定向 `9c21aa7c79b94f1980988945d35636dd` 为 134/134（1.4521749 秒），精确素材真实加载 `da1d1e3969014e81b06cb57a2392de13` 为 1/1（106.8572486 秒）。两次 645 项完整任务与一次 5 项素材类任务都只在同一 `CardArtLogicalAddresses_LoadSprites` 冷加载处触发 180 秒 timeout，因此本决定按组合门禁验收，明确不声称完整 EditMode 存在一次全绿任务。升级实例、默认 Deck、奖励、Run、UI、多人及其余 16 张目录卡不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2l-bombard-runtime.md`。

## CD-092：V2M 天空之怒在四类原始 Support 逻辑段后逐层随机结算

**问题**：`SkyWrath` (3266) 要在“每次支援”后按 Power 层数分别造成随机主目标与其余目标伤害，但当前 scheduled effects 中既有真正的 Support，也有 Needle Delayed、Bomb 与组合 Burn/Oil。如果从任意伤害 settlement 或显示文案推断触发，不仅会把钢针、炸弹、燃烧、即时攻击与便携帮手误判为支援，还会让天空之怒自身递归。原始需求也没有完整说明“每次支援”对应 hit、wave 还是整个 effect、层间死亡如何影响随机候选、单候选是否推进随机流，以及它与 Bombard 的组合顺序。

**选择**：3266 注册为 1 Energy / Self / PowerPile 的 Power 程序，每次成功施放增加 1 层整场持续的 `SkyWrath`，卡本身不产生即时伤害或随机写入。只在四类原始 scheduled Support 逻辑段完整结束后调用受限入口：BansheeStrike 每个 hit、FireSupport 每个 hit、FireBombardment 每个 wave、TripleStrike 的延迟 Support 一次。燃烧轰炸的入口位于一波全部目标的 `Damage → 存活后 Burn → Oil` 之后。每层先重新取得当前投影中的存活敌人，再调用一次 `NextInt(living.Count)` 选择主目标；即使只有一个候选也推进随机流。主目标先承受基础 8 点 Support，该层开始快照中的其余目标再按 Encounter 顺序各承受基础 4 点 Support。若后续层开始时已无存活敌人则停止且不调用随机流。

**组合与顺序**：天空之怒的 8/4 先通过既有 Bombard 正值 half-up 换算，再进入现有 Support 管线读取目标 Smoke、Vulnerable 与 ArmorBreak；Bombard 4 层时对应基础值为 11/6。每层主目标伤害和其余目标伤害使用独立 prepared operation，随后下一层从已更新投影重新取候选。天空之怒操作完成后不再次调用本入口，因此不递归；全部操作仍属于同一个 scheduled trigger 的联合计划、校验与提交，没有新增全局事件或共享写入路径。

**来源裁决与排除边界**：当前行为来源 `README web.md` 把支援明确列为女妖打击、火力支援、燃烧轰炸和三连击延迟段；旧 `HANDOFF.md` 中把钢针纳入触发的描述不能覆盖该来源。因此 `NeedleStorm` 的 Delayed、GuidedNuke / FiveHundredPounder 的 Bomb、回合末 Burn、即时 Attack/Shoot、PortableHelper 与天空之怒自身均不触发。该决定不改变四类原始支援的命中数、波次数、目标选择、载荷、倒计时或生命周期 settlement，也不扩大 Bombard 的 scheduled-effect 白名单。

**影响**：作者表只将 Q156（3266）翻为 `Implemented`；82 模板达到 67 `Implemented` / 15 `CatalogOnly`，V1 为 54/10、V2 扩展为 13/5。Luban、本地化导入和 Sync/Addressables（12.956 秒）已通过；翻表前运行时任务 `eefded85c7aa4a099d3b16ee4577e704` 为 117/117，最终定向任务 `3a279411d63749abaf8eca64ec4236cc` 为 139/139，完整任务 `a46a25a9da924131965130d6e2b07b8b` 为 650/650（174.2163423 秒）。开发中的两轮红测分别来自场景能量超过 fixture 上限及随机 oracle 构造方式不等价，两次均只修测试，生产实现未改变。升级实例、默认 Deck、奖励、Run、UI、多人及其余 15 张目录卡不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2m-sky-wrath-runtime.md`。

## CD-093：V2N 极限过载以当前牌离手后投影的卡区深事务抽至满手

**问题**：`LimitOverload` (3260) 要在 0 费获得 1 Energy 后“抽牌到手牌满”，并让下回合能量恢复 -3。现有出牌流程在程序操作后才将当前牌移出 Hand，若直接复用 `DrawCards(10)`，容量计算会把正在解算的 3260 仍计入 Hand 而稳定少抽一张；若先公开发布离手、再调用普通抽牌，又会暴露中间布局、破坏出牌原子性，并可能让本卡进入同次弃牌重洗而自抽。

**选择**：在 `BattleCardZonesData` 提供 `PreparePlayedCardDepartureAndDrawToHandLimit` / `ValidatePreparedPlayedCardDepartureAndDraw` / `CommitPreparedPlayedCardDepartureAndDraw` 三段 seam。Prepare 以当前 `Layout` 和洗牌随机状态为权威快照，在本地副本中先从 Hand 移除当前牌，再仅用原 DrawPile 与原 DiscardPile 计算并冻结抽至上限 10 的最终布局、随机后状态和 settlement；解算卡在抽牌计算结束后才放入目标弃牌堆，因此不参与同次重洗。Validate 纯只读校验所属聚合、一次性、布局引用与洗牌随机状态；Commit 通过后不再随机，仅写入冻结随机状态并发布一次最终 `Layout`。

**出牌事务与顺序**：Program 60 声明 `GainEnergy(1) → DrawToHandLimitAfterPlayedCardDeparture(10) → NextRoundEnergyGainPenalty +3`。运行时在首次对外写入前执行上述 CardZones Prepare/Validate，并把能量变化暂存在本地 `playerTurnAfter`。成功 settlement 顺序为 `EnergySpent(0) → 可选 EnergyGained(1) → 当前牌 Hand→DiscardPile → 旧弃牌重洗/抽牌 → Penalty +3`；满能量时不生成虚假 `BattleEnergyGainedSettlement`。复合卡区操作已包含当前牌归宿，普通出牌结尾不会再移牌；普通 `DrawCards` 与 `BattleCommandQueue.Submit` 唯一共享写入边界不变。

**理由与边界**：该 seam 把“当前卡归宿后的容量”、“只重洗旧弃牌”和“只发布一次最终卡区”收口在拥有布局与洗牌随机的深模块内，职业运行时只声明复合意图，不重复操作卡堆。失败或快照漂移时保持资源、状态、卡区、随机与表现零写入；下一回合仍复用 V2J 的 `max(0, baseGain + bonus - penalty)` 后上限裁剪与一次性清除，没有建立新的回合能量公式。3260 不是 Attack/Shoot，不消耗 Ammo、Stim，不触发 IncendiaryAmmo 或 PortableHelper。

**影响**：作者表只将 Q150（3260）翻为 `Implemented`；82 模板达到 68 `Implemented` / 14 `CatalogOnly`，V1 为 55/9、V2 扩展保持 13/5。Luban、本地化导入/校验和 Sync/Addressables（15.828 秒）已通过；Unity MCP 正式定向任务 `feda36c5daef4fffab34065ba5988686` 为 169/169（2.2836982 秒），完整 EditMode 任务 `a84b5bb4f7dd4ca1b9791c81bb930973` 为 659/659（282.0044831 秒）；CardArt 与 Character Prefab 的 Addressables 冷加载较慢但均通过。升级“+2 能量”仍只是元数据；默认 Deck、奖励、Run、UI、多人及其余 14 张目录卡不在本决定范围内。验证见 `06_testing/2026-08-12-machine-gunner-v2n-limit-overload-runtime.md`。

## CD-094：V2O Innate 以强类型卡牌配置驱动首轮起手且隐秘行动复用普通状态与抽牌语义

**问题**：`StealthAction` (3275) 要求“固有”，即无论洗牌结果都必须进入首次起手。若在 Turn 或职业运行时按 3275 / Program 75 特判，会把通用卡牌元数据泄漏到调度层；若在构造牌堆前把固有牌直接塞入 Hand，会绕过既有洗牌、移动 settlement 和单次 `Layout` 发布契约。多张固有超过默认起手 5 时，还必须明确是丢弃固有、超出目标还是挤占普通补牌。

**选择**：在卡牌作者表与 Luban `Card` bean 增加非空布尔字段 `is_innate`，生成运行时只读字段 `IsInnate`，默认值为 false；当前精确目录中只把 3275 标记为 true。`BattleTurnController` 在 `StartBattle` 的任何写入前，用静态表为每个存活玩家的现有卡牌实例收集无序固有集合，再把具体顺序与布局规划交给 CardZones；该通用路径不识别职业、卡牌 ID 或 ProgramId。CardZones 在原始 Deck 完成既有洗牌后，按 DrawPile 的真实抽取顺序选取固有实例，再以普通实例填充剩余起手槽位。

**数量、顺序与原子性**：固有实例数为 0～5 时全部进入 Hand，并用普通牌补到默认目标 5；为 6～10 时全部进入 Hand且不再补普通牌；超过 Hand 上限 10 时，全部玩家起手预演在状态机启动前失败并返回 `InvalidOpeningHandConfiguration`。CardZones 的 Prepare 冻结所属聚合、初始 `Layout`、洗牌随机状态、最终布局和起手顺序；Validate 拒绝跨所属、布局/随机漂移与重复提交；Commit 不推进随机，按“固有优先、各组保持已洗牌后的抽取顺序”生成连续 settlement 并只发布一次最终 `Layout`。后续回合继续走普通补牌，不重复应用 Innate。

**隐秘行动程序**：Program 75 只声明已有的 `Invisible +1` 与普通 `DrawCards(1)`，成功归宿仍为 DiscardPile。统一出牌事务顺序为 `EnergySpent(1) → Invisible +1 → Draw → 当前卡离手`；抽牌容量计算时当前卡尚在 Hand，因此 Hand 已满 10 时抽 0，随后弃置 3275 后为 9。该程序不是 Attack/Shoot，不消耗 Ammo，也不触发 Stim、IncendiaryAmmo、PortableHelper 或伤害链。

**理由与边界**：静态 `IsInnate` 是可由任意职业复用的内容事实，Turn 只负责在其既有静态配置/回合编排职责中收集实例身份并保证全部玩家先预演，CardZones 则继续拥有牌区选择、快照校验、顺序与原子发布。该 seam 不改变默认起手目标 5、所有职业共享的起手上限 10、普通回合补牌或现有随机算法，也不把升级描述转化为尚不存在的升级实例。

**影响**：正式表只让 3275 成为 `Implemented` / `is_innate = true`，其余模板为 false；82 模板达到 69/13，V1 为 55/9、V2 为 14/4。Luban、本地化与 Sync/Addressables（18.363 秒）已通过；正式目录快照 `8acfa22da51c4f2fb757bbe102fb945c` 为 21/21，最终聚合定向 `982a4f4c4af24ba78e678bf0e66f2ce1` 为 237/237，完整 EditMode `91d060c915ff4dfea42608b7c22669ab` 为 673/673。默认 Deck 内容、奖励、Run、UI、多人、Scene、Prefab 和升级实例不在本决定范围内。验证见 `06_testing/2026-08-12-machine-gunner-v2o-stealth-action-innate-runtime.md`。

## CD-095：V2P 机枪扫射分离实际零弹耗与游击名义弹耗并显式退出两类联动

**问题**：`MachinegunBurst` (3263) 是只能由 `FixedMachinegun` (3261) 创建的临时 Attack，来源明确要求随机 5×2、实际 Ammo 消耗为 0、但游击战术按消耗 2 Ammo 计算。若复用实际 `AmmoSpent`，就无法触发游击；若为游击伪造 2 点实际消耗，又会错误扣除 Ammo 并生成不存在的资源 settlement。来源也没有声明 3263 带 Shoot 标签；仅凭卡名或“机枪”语义把它归为 Shoot 会误触发 Stim、燃烧弹药、开火与便携帮手，而把 `Tags.None` 的 Attack 自动当作普通非射击攻击又会误触发功夫机甲、烈火烹油和连肘最近记录。

**选择**：Program 63 注册为 0 Energy / Attack / RandomEnemy / ExhaustPile / 无升级，执行两段独立的基础 5 点普通 Attack；每段开始都从当前投影的存活敌人重新选择随机目标，整张卡成功后才提交卡牌随机状态。程序的实际 Ammo 成本保持 0；为游击战术提供显式的 `AmmoSpentForGuerrillaOverride = 2`，未声明覆盖的程序继续使用实际弹耗。3263 冻结 `Tags.None`，因此不进入 Shoot 分类；同时以统一的 `ParticipatesInNonShootAttackSynergies` 派生事实控制功夫机甲、烈火烹油与 `NonShootAttackRecent` 三个入口，普通 Attack 默认参与，3263 显式为 false。

**理由**：把实际资源消耗和某个后置机制的名义数值分成两个强类型事实，可以保留真实 Ammo、settlement 与 UI 口径，同时精确表达游击的来源特例。射击分类和非射击联动资格也保持为两个独立维度，避免从名称、卡牌 ID 或一个 `Tags.None` 的反向判断推导跨卡语义。随机重选继续复用职业卡牌随机流与整张出牌事务；伤害仍走既有普通 Attack 公式、Block/HP、致死与连续 settlement 链，没有新增第二条 Queue 写入路径。

**影响与边界**：作者表只将 3263 翻为 `Implemented`，82 模板达到 70/12，V1 为 56/8、V2 扩展保持 14/4。正式 `battle.card.xlsx` SHA-256 为 `B65D97253A43B2FF8575BCEE6F230B651EFD36FE84A10B7ACBFC0BCC62A0AB29`；Luban、本地化与 Sync/Addressables（11.757 秒）已通过；最终定向 `0f60a2e799904069ab68ae6f13a91953` 为 154/154，域重载后的 CardArt 探针 `f87e7034664a4126bb0b32c2888751e9` 为 1/1，完整 EditMode `a078688b69bd4f198bb736c6285ab5e7` 为 678/678。3261 仍为 `CatalogOnly` 且临时卡创建 seam 尚未实现，所以 3263 只能由直接运行时夹具验证，不能据此宣称生产流程可生成或获得；奖励排除、默认 Deck、Run、升级、UI、多人、Scene 与 Prefab 均不在本决定范围内。验证见 `06_testing/2026-08-12-machine-gunner-v2p-machinegun-burst-runtime.md`。

## CD-096：V2Q 固定机枪以 CardZones 深事务原子替换剩余手牌并显式发布临时卡创建

**问题**：`FixedMachinegun` (3261) 要先获得 Block，再耗尽来源卡与其余手牌，并创建等量 `MachinegunBurst` (3263) 到 Hand。若依次调用来源离手、`DiscardHand` 和现有 `AddTemporaryToHand`，中途会多次发布 `Layout`，实例分配失败还可能留下“已弃牌但未创建齐”的部分写入；若把新实例伪装成 DrawPile→Hand，又会污染卡区来源事实和 UI 动画。3263 不在 Deck 时，Hand 视图也不能只按牌组模板预载，否则生产创建成功后仍可能缺少可显示模板。

**选择**：Program 61 的基础态声明 2 Energy、Self、ExhaustPile，并把成功效果冻结为 `GainBlock(10) → 来源 Hand→ExhaustPile → 剩余 Hand 按原顺序 Hand→DiscardPile → 等量创建 3263 到 Hand`。剩余 Hand 的数量在来源卡逻辑离手后计算；为空时仍合法获得 Block 并 Exhaust 来源，但创建数量为 0。新卡是 BattleSession 内的临时 `CardInstanceData`，不修改 Deck、奖励或 Run。升级 15 Block 没有对应升级实例，基础态仍只使用 10。

**卡区事务与 settlement**：`BattleCardZonesData` 以单一 Prepare / Validate / Commit 复合计划拥有全部卡区变化。Prepare 在首次写入前冻结所属聚合、原 `Layout`、卡实例分配状态、来源归宿、其余 Hand 原序、全部新实例、最终布局与连续 settlement；Validate 拒绝所属、布局、分配状态或一次性漂移；Commit 不再分配实例，只登记冻结的新卡并发布一次最终 `Layout`。旧手牌继续使用真实 Hand→Discard 记录，来源使用 Hand→Exhaust；每张新实例使用独立 `CardCreated` settlement 指向 Hand，不借用 DrawPile→Hand 或普通 Draw 语义。

**表现与动态模板**：表现计划按 settlement 类型生成既有 Hand→Discard、`HandToExhaust` 与 `CreatedToHand` cue。来源卡先离手、旧手牌按权威原序弃置、新实例再按创建顺序进入 Hand；表现只消费权威结果，不自行创建或重排卡牌。职业程序 registry 同时声明运行时可能创建的模板依赖，Session 汇总为 `AvailableCardTemplateIds` 并传递给 Hand 异步预载，使 3263 即使不在本局 Deck 也具有可用模板；该链路不把 3263 加入默认 Deck 或奖励目录。

**理由与边界**：布局、实例所有权与分配器状态都属于 CardZones，将复合替换封装为深事务可以在首次可见写入前完成所有可能失败的校验，并维持 `BattleCommandQueue.Submit` 唯一共享写入入口。显式创建 settlement 保留“从无到有”的领域事实，也让 UI 不必从最终 Hand 差异猜测来源。本决定不复用 Innate、普通 Draw、V2N 抽至满手或伪造卡堆移动来表达临时创建。

**影响与验收**：正式作者表只将 3261 翻为 `Implemented`，`battle.card.xlsx` SHA-256 为 `02F549502D14214C98B4BA97212962B05E58A9B768EF1D7E4CAD441E1DCD6FB7`，并保持 `is_innate=false`。Luban 于 22:00:11 成功生成全项目 168 个 Card JSON；Marine 82 模板为 71/11，V1 为 57/7、V2 扩展为 14/4，3261 为 status 0 / Program 61 / Exhaust / 非 Innate。Localization import/validate 与 Sync/Addressables 均成功，Addressables 13.42 秒；force scripts 域重载后，最终聚合定向 `ba19d1744f084167927568f5572f91e6` 为 262/262（30.1698095 秒），完整 EditMode `dc6a1453b602487c8bfbbe7e42c3968d` 为 690/690（20.8279366 秒），均为 0 failed/skipped。静态编译为 Runtime 0 error / 6 warning、Editor 0 error / 12 warning。升级 15 Block、升级实例、默认 Deck、奖励排除、Run、多人、Scene 与 Prefab 不在本决定范围内。验证见 `06_testing/2026-08-12-machine-gunner-v2q-fixed-machinegun-runtime.md`。

## CD-097：V2R 霸凌按命令开始时的目标活跃状态种类冻结普通抽牌数

**问题**：`Bully` (3278) 的来源只规定“0 费，6 伤；目标每有一种状态抽 1 张”，没有定义状态集合、计数时点、同种多层是否重复，也没有说明伤害消费状态、命中后新增状态或致死后是否改变抽牌数。若在伤害结算后动态读取目标，会让 Buffer、Intangible、Armor 等受击消费以及 IncendiaryAmmo / AgedOil 等后置效果反向改变同一次命令的抽牌数；若由卡牌程序直接逐张 Draw，又会绕过 CardZones 对手牌上限、重洗随机、布局快照与失败零写入的所有权。

**选择**：Program 78 注册为 0 Energy / Attack / 显式 Enemy / DiscardPile，使用普通基础 6 点 Attack 与 `Tags.None`；不从名称推断 Shoot。程序准备阶段在任何战斗写入前冻结命令起始目标的活跃状态种类数：通用 Strength 非零计一种、Vulnerable 大于零计一种，每个 `MachineGunnerCombatantStatus` 正层数各计一种，同种状态不论层数只计一次。HP、Block、资源、PowerPile 卡实例、Stim 与 scheduled effect 不计。伤害及既有 post-hit 链完成后仍使用该旧值抽牌，目标致死、受击消费或新状态都不回写冻结计数。

**PreparedDraw 深事务**：按状态种类抽牌复用 `BattleCardZonesData.PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw`。Prepare 在首次写入前冻结请求数量、Hand 10 上限、DrawPile / DiscardPile、洗牌随机前后状态、最终布局及连续移动 settlement；Validate 拒绝布局或随机快照漂移；Commit 不重新随机，只发布冻结结果。0 种状态合法抽 0；Hand 已满时不伪造移动且不推进洗牌随机，随后当前牌按普通成功归宿进入 DiscardPile。目标门禁、计划漂移或其他失败继续保持 Energy、伤害、状态、随机流、卡区与表现结果零写入，`BattleCommandQueue.Submit` 唯一写入边界不变。

**来源与脑补边界**：0 费、基础 6 伤、显式敌方目标、按目标每种状态抽 1 张和升级 9 伤来自当前 `README web.md`；“命令开始时”、精确状态集合、同种多层只计一次、排除 HP/Block/资源/Power/Stim/延迟实例，以及伤害后仍使用旧值，是为获得确定性和事务安全而冻结的项目实现决定。该决定不建立通用状态注册表，也不把职业私有状态与 Weakness、Vulnerable 或 Strength 合并；升级 9 伤仍只是作者表元数据。

**影响与验收**：正式作者表只将 Q168（3278）翻为 `Implemented`，U168 保持 `is_innate=false`，`battle.card.xlsx` SHA-256 为 `878812D99F68C8F9B9A7BC620E2794180F6E8A3F21B5252B16A12BDB70915499`。Luban 于 2026-08-12 22:48:16 成功；全项目 Card JSON 168 个，Marine 82 模板为 72/10，V1 为 57/7、V2 为 15/3，3278 为 status 0 / Program 78 / DiscardPile / 非 Innate。Localization、Sync/Addressables（16.521 秒、BuildLayout）、静态编译、正式聚合 209/209 与完整 697/697 EditMode 均通过。补强 6/6、非表格聚合 150/150 通过；TDD 初始唯一红项只是测试遗漏 `EnergySpent(0)`，生产未改。默认 Deck、奖励、Run、升级实例、多人、Scene 与 Prefab 不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2r-bully-runtime.md`。

## CD-098：V2S 先发制人按命令开始时的来源活跃状态种类冻结普通抽牌数

**问题**：`PreemptiveStrike` (3277) 的来源只规定“0 费 1 弹，8 伤；自己每有一种状态抽 1 张”，没有定义状态集合、同种多层是否重复、计数时点，也没有说明 Damage / post-hit 期间的状态变化或目标致死是否改变抽牌数。若直接复用 V2R 的目标状态读取会把“自己”错误换成敌人；若在伤害后重新读取来源，会让命中后链或其他状态生命周期反向改变同一次命令。Shackle 又是私有状态身份之一，但既有上游规则会禁止携带者打出任何 Attack，不能为了让它参与成功计数而绕过攻击门禁；卡牌程序直接逐张 Draw 也会绕过 CardZones 对容量、重洗随机、布局快照与失败零写入的所有权。

**选择**：Program 77 注册为 0 Energy / 1 Ammo / Attack / 显式 Enemy / DiscardPile，使用普通基础 8 点 Attack 与 `Tags.None`，不从名称推断 Shoot。程序准备阶段在任何战斗写入前冻结命令起始来源的活跃状态种类数 `N`：Strength 非零计一种、Vulnerable 大于零计一种，每个 `MachineGunnerCombatantStatus` 正层数各计一种，同种状态不论层数只计一次。当前 16 种身份完整保留；Power、Stim、scheduled effect、Block 与资源不计。Shackle 仍属于身份集合，但携带时继续由上游 Attack 门禁返回失败并保持零写入，其余 15 种私有状态分别具有成功计数回归。

**PreparedDraw 与时序**：按来源状态种类抽牌复用 `BattleCardZonesData.PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw`。Prepare 在首次战斗写入前冻结请求数量、Hand 上限、DrawPile / DiscardPile、洗牌随机前后状态、最终布局及连续移动 settlement；成功支付资源并完成 Damage 与既有 post-hit 链后，Commit 只提交旧计划、不重新计数或随机。目标致死仍按命令起点的 `N` 抽牌；目标门禁、Ammo 不足、Shackle、布局/随机漂移或其他失败继续保持 Energy、Ammo、伤害、状态、随机流、卡区与表现结果零写入，`BattleCommandQueue.Submit` 唯一写入边界不变。

**来源与脑补边界**：0 费、1 Ammo、基础 8 伤、显式敌方目标、“自己每有一种状态抽 1 张”和升级 12 伤来自当前 `README web.md`；“命令开始时”、精确状态集合、同种多层只计一次、排除 Power/Stim/scheduled effect/Block/资源，以及 Damage / post-hit 后仍使用旧值，是为确定性和事务安全冻结的项目实现决定。该决定不建立通用状态注册表，不合并 Strength、Vulnerable 与职业私有状态，也不把 Shackle 的身份存在解释成可绕过攻击禁用；升级 12 伤仍只是作者表元数据。

**影响与验收**：正式作者表只将 Q167（3277）翻为 `Implemented`，U167 保持 `is_innate=false`，`battle.card.xlsx` SHA-256 为 `6C9120A317622F103F9A0DDEEEBB994B28F88230B679BA7E0B1D28201F8E2648`。Luban 于 2026-08-12 23:26:12 成功；全项目 Card JSON 168 个，Marine 82 模板为 73/9，V1 为 57/7、V2 为 16/2，3277 为 status 0 / Program 77 / DiscardPile / 非 Innate。Localization、Sync/Addressables（13.966 秒、BuildLayout）、静态编译、最终 V2S 5/5、正式聚合 214/214 与完整 702/702 EditMode 均通过；TDD 与正式聚合首轮的三项红色均为测试 oracle，生产实现未因其改变，production 审查无 blocker。默认 Deck、奖励、Run、UI、多人、升级实例与升级 12 伤不在本决定范围内；验证见 `06_testing/2026-08-12-machine-gunner-v2s-preemptive-strike-runtime.md`。

## CD-099：Ironclad 首批四张基础卡通过通用 Effect 序列与 PreparedDraw 接入

**问题**：冻结 Ironclad 目录中的 Bludgeon 与 Twin Strike 可以复用现有伤害 Effect，但 Pommel Strike 与 Shrug It Off 还需要在同一次普通出牌中把战斗 Effect 和抽牌按作者顺序组合。若在伤害/格挡已经提交后直接调用卡区 `Draw`，后续失败会留下半次权威写入；若让 Effect Executor 自己管理牌堆、手牌上限和洗牌随机，又会复制 CardZones 的事实与随机所有权。Twin Strike 两次复用同一伤害模板还必须保留两个独立逻辑段，同时本地化参数键不能为了第二段执行而破坏既有 validator 规范。

**选择**：在公共 Effect 枚举末端新增 `DrawCards = 4`，本轮正式 Effect 4007～4011 分别表达 Damage 32、Damage 5、Damage 9、Draw 1 与 Block 8。普通卡统一由 `BattleCardEffectSequenceExecutor` 按 `effect_bindings` 原序分成 Draw 前战斗 Effect、至多一次 Draw、Draw 后战斗 Effect；Draw 必须是 `Attribute.None`、Value 非负，第二个 Draw 或非法数据在首次写入前返回稳定失败。Draw 后战斗 Effect 从 Draw 前计划的完整 Source / Target / Strength 投影继续预演，全部子计划联合校验后才允许支付与提交。

普通抽牌继续由 `BattleCardZonesData` 的 `PrepareDraw` / `ValidatePreparedDraw` / `CommitPreparedDraw` 深事务拥有：Prepare 冻结 Hand 10 上限、DrawPile / DiscardPile、旧弃牌重洗、洗牌随机前后状态、最终布局与连续 settlement；Validate 拒绝跨聚合、重复提交、布局或随机漂移；Commit 不重新随机，并只在实际抽牌时发布一次完整布局。Draw Effect 执行时当前牌仍在 Hand，因此满 10 张时抽 0、随机不推进，随后当前牌才按成功归宿离手；Draw 前致死不会取消已冻结的抽牌。

四张基础态的有序绑定固定为：Bludgeon `damage:4007`；Twin Strike `damage:4008, damageRepeat:4008`；Pommel Strike `damage:4009, cards:4010`；Shrug It Off `block:4011, cards:4010`。Twin 的第二段继续复用 Effect 4008，保留独立结算或致死后的跳过记录；说明文本只显示 `{damage}`，第二执行键统一使用 validator 已接受的 `damageRepeat`，不放宽规范去接受 `damage_repeat`。

**理由与边界**：战斗 Effect 投影、卡区布局与洗牌随机分别留在原有深模块，由一个内部组合计划负责顺序与首写前联合校验，可以保持 `BattleCommandQueue.Submit` 唯一共享写入入口，又不为 Ironclad 增加卡牌 ID、名称或文本分支。该切片只提供通用的“至多一次普通 Draw”组合，不等于完成 I5 的每步独立目标，也不推导全体、随机、X 费、选择、Power 或升级实例能力。

**影响与验收**：Pommel Strike（3113）基础 Damage 9 + Draw 1、Shrug It Off（3115）基础 Block 8 + Draw 1、Twin Strike（3120）基础 Damage 5 两次、Bludgeon（3123）基础 Damage 32 已翻为 `Implemented`；Ironclad 85 张达到 8/77。正式工作簿 SHA-256 为 Card `54DA52D0C80885A2D55AEC8E260207E2D4E27AC8251304BF0710DB180EBC4EBB`、Effect `B616F993F5373AFF2DDD764E9C431A2C13F66CD3C5B2F39595B4A813FB7863BC`、Enums `B9F8DD24C77EE64FA36C6DC7FEA5C0D83229011F45463A99ABAE30B3A7870B26`、i18n `7E91C7F46AEBBBF20188690EC49B1B5C3F6C84C2EF3A9531D22D42E3E23644F8`。Luban、Localization、Sync/Addressables（13.595 秒、BuildLayout）、静态编译、正式 smoke 20/20、正式聚合 67/67 与完整 EditMode 713/713 均通过。升级实例、其余 77 张目录卡、Deck、奖励、Run、UI 与多人不在本决定范围内；验证见 `06_testing/2026-08-13-sts2-ironclad-first-four-effect-runtime.md`。

## CD-100：V2T 战术推进以独立二元授权和共享费用解算冻结下一张成功攻击

**问题**：`TacticalAdvance` (3234) 要求 2 Energy 获得 Block，并让“下一张攻击牌不消耗费用”。现有机枪兵费用同时包含 Energy、Fixed / UpToLimit / AllAvailable Ammo、X 效果规模、Stim 额外段与 Guerrilla 名义耗弹，直接把卡牌费用改成 0 会把“实际支付”错误扩散为“效果值也为 0”，或让 Stim / Guerrilla 丢失原语义。若把免攻实现成第 17 种 `MachineGunnerCombatantStatus`，还会错误增加 3277 / 3278 的状态种类抽牌数；若在攻击开始时立即消费，Shackle、目标错误、卡区容量或后续计划失败都会浪费授权。

**选择**：Program 34 注册为 2 Energy / Skill / Self / DiscardPile，基础态成功时先获得 10 Block，再把职业运行时的独立 `_nextAttackFree` 二元授权设为 true 并推进费用修订号；重复施放只刷新同一授权，不累计次数。该授权跨回合保留，Skill 不读取或消费。Attack 先执行既有 Shackle 和输入门禁，再以当前授权及 revision 准备费用；只有整张 Attack 的效果、后置链与成功卡牌归宿全部提交后，才按准备快照消费授权。因此 Shackle、目标错误、费用/计划校验、卡区容量或其他失败保持授权和全部战斗事实不变；成功致死 Attack 仍消费。

**共享费用解算**：新增纯 `BattleCardCostResolver`，以 Normal / Waived 支付模式为 Fixed / X 冻结 `ActualEnergySpent`、`EffectValue` 与 `NominalEnergySpentForTriggers`。通用 `BattleCardPlayRules` 与 `BattleTurnController` 的普通 Fixed 支付成为一个真实适配器，机枪兵职业费用成为第二个真实适配器；既有通用 X 路径没有在本决定中迁移或扩展。机枪兵准备成本继续单独冻结 Energy 与 Ammo 的 actual / effect / nominal，以及 Stim 额外段：Waived 只把实际 Energy / Ammo 支付归零，Fixed / UpToLimit 保留基础效果与 Stim 段，并把两者纳入 Guerrilla 名义耗弹；AllAvailable 保留既有免费 Stim 段且不把它伪造成名义 Ammo。`ComboElbow` 最近攻击分类保持独立，不因免攻改变。

**理由与状态边界**：actual、effect、nominal 是三个不同问题：资源 settlement/UI 只看 actual，伤害段数看 effect，Guerrilla 等触发器看 nominal。共享纯解算器统一 Fixed / X 的数学与冻结结果，但把 Ammo、Stim、Shackle、Guerrilla 和成功生命周期留在机枪兵适配器内，既形成可复用 seam，又没有把职业规则泄漏到通用模块。授权不是“战斗状态种类”，因此使用独立 bool + revision 而不进入 16 种 `MachineGunnerCombatantStatus`；3277 / 3278 的计数集合保持原决定。

**来源裁决与边界**：当前 `README web.md`、正式作者表和 i18n 一致给出基础 10 Block、升级 14 Block；历史 `HANDOFF.md` 的 12/16 属于被当前来源覆盖的旧口径。基础 10 已运行验证，升级 14 仍只是作者表元数据，没有升级 `CardInstance`。本决定只实现下一张成功 Attack 的费用豁免，不实现自动免费攻击链、手牌选择/保留、默认 Deck、奖励、Run、UI 专属提示、多人、Scene、Prefab 或战士免攻卡；公共 resolver 只为未来适配器提供 seam，不等于这些消费者已完成。

**影响与验收**：正式作者表只将 Q124（3234）翻为 `Implemented`，`battle.card.xlsx` SHA-256 为 `55D43141149D7A86D7957B1C43ED9303B9E9D091094E0CFAF2CF39FE2F73C569`。Luban 于 2026-08-13 01:44:15 成功；全项目 Card JSON 168 个，Marine 82 模板为 74/8、V1 58/6、V2 16/2，3234 为 status 0 / Program 34 / 空 bindings / 非 Innate。Localization import/validate、Sync/Addressables（端到端 16.852 秒、Addressables 14.762 秒、BuildLayout `buildlayout_2026.08.13.01.45.29.json`）与静态编译均通过。精确 V2T 6/6、致死/溢出补强 1/1、Starter 142/142、正式快照 36/36、含真实 AB 的正式聚合 213/213 和完整 EditMode 721/721 均通过；最终双轴 production / spec review 为 0 blocker，Standards 指出的冗余 `CardTemplateId` 已删除。验证见 `06_testing/2026-08-13-machine-gunner-v2t-tactical-advance-runtime.md`。

## CD-101：V2U 不解释12连以纯两波资源计划冻结换弹射击

**问题**：`TwelveHits` (3257) 不是普通“按当前 Ammo 重复若干次”的单段费用：它必须先消耗当前 Ammo 最多射击 6 次，再立即补满 Ammo，并为第二波重新消耗最多 6 发；0 Ammo 也允许施放。V2T 的免费 Attack 又要求实际 Energy/Ammo 为 0，但效果与触发器仍按最大费用解释。若在逐 hit 循环中临时读取和改写 Ammo，目标致死、Stim、IncendiaryAmmo、PortableHelper 或失败回滚都会改变后续资源轨迹；若把波间换弹塞进公共 `BattleCardCostResolver`，则会把机枪兵专属语义泄漏给通用费用模块。

**选择**：Program 57 注册为 3 Energy / Rare / Attack / `AutomaticNearestEnemy` / DiscardPile，并使用专用 `ReloadedAmmoVolley` 执行种类。命令开始时只冻结一个最近存活敌人；第一波普通支付为 `min(initialAmmo, 6)`，之后无条件补到命令开始时 AmmoMaximum，第二波基础支付为 `min(AmmoMaximum, 6)`。0 Ammo 时第一波冻结 0 个效果段，但换弹和第二波照常执行。所有来源伤害段基础值为 5；目标投影死亡后停止两波剩余伤害，不重定向，已冻结的换弹和第二波支付不取消。

**深 resolver 与逐 hit 边界**：新增机枪兵私有纯 `MachineGunnerReloadedVolleyResolver`，输入只有 initial Ammo、AmmoMaximum、单波上限、Stim 是否激活与 Normal/Waived 支付模式；输出冻结首/次波效果段数、首/次波实际 Ammo、补满前后值、全卡唯一 Stim 段、Guerrilla 名义 Ammo 与最终 Ammo。该 resolver 不读取或写入战斗对象，也不负责伤害。公共 `BattleCardCostResolver` 继续只解析 Energy normal/waived；逐 hit 层继续复用既有 `AppendPreparedHitAndPostHitOperations`，因此每个来源段按现有顺序经过 Damage、IncendiaryAmmo 与 PortableHelper，且不会为 3257 复制第二套命中后系统。

**免费与失败事务**：Waived 时两波实际 Ammo 均为 0，但效果段按每波 6 冻结，波间仍补满；Stim 激活时只给第二波增加一个来源段，整卡 Guerrilla nominal Ammo 为 13，否则为 12，最终 Ammo 保持补满。正常支付只有在第二波基础 6 发后仍有容量时才追加 Stim 段。V2T 授权仍在整张 Attack 成功归宿后消费；Energy、目标、Shackle、费用/计划或快照失败在首次写入前终止并保留授权。成功致死仍提交资源轨迹并消费授权。

**来源与排除边界**：当前 `README web.md`、i18n 与作者表一致给出基础 3 Energy / 每段 5 伤，升级为 2 Energy / 每段 6 伤。V2U 只实现基础 `CardInstance`；升级 2E/6 伤仍仅是元数据。本决定不新增通用两阶段资源协议，不声称 Ironclad 可复用机枪兵换弹 resolver，也不实现默认 Deck、奖励、Run、UI、多人、Scene/Prefab、自动免费攻击链或剩余目录卡。

**影响与验收**：正式作者表只将 Q147（3257）翻为 `Implemented`，SHA-256 为 `7131597FD5F3D948921F54926C0205E24E31F747D7C9B1206B78902AE6BEF818`；生成 JSON SHA-256 为 `28324422913241FC627F5C3A0BCF715332E4F2B3DCDFA94E4B6E4FF3ED7A6306`。Luban 于 2026-08-13 03:00:27 成功；全项目 Card JSON 168 个，Marine 82 为 75/7、V1 59/5、V2 16/2。Localization import/validate（7.350 / 3.124 秒）、Sync/Addressables（18.482 / 12.173 秒，BuildLayout `buildlayout_2026.08.13.03.02.34.json`）与静态编译均通过。六项逐片 TDD、Starter 148/148、正式快照 37/37、含真实 AB 的正式聚合 220/220 和完整 EditMode 728/728 均通过；任务 ID 与红绿边界见 `06_testing/2026-08-13-machine-gunner-v2u-twelve-hits-runtime.md`。

## CD-102：V2V 排气散热以共享手牌单选协议和原子卡区事务执行

**问题**：`VentHeat` (3244) 的基础行为是“消耗另一张手牌，再获得 1 Energy；没有其他牌时无事发生”，而来源牌自身仍要按成功归宿弃置。普通 `PlayCardCommand` 原先只描述来源牌和战斗目标，无法把玩家选择的另一个 `CardInstanceId` 作为权威输入送到 Queue；若由 UI 直接移动卡牌或加能量，会产生第二条共享写链。若依次调用两个普通卡区移动，第二步失败会留下半次结算；若把选择保存在 `BattleTurnData`，又会把瞬时交互状态污染成战斗事实。表现层还必须同时处理两张从 Hand 离开的 transient，不能用一个伪造的 prelude 掩盖真实 settlement 顺序。

**选择与结算**：Program 44 注册为 0 Energy / Skill / Self / DiscardPile。若来源之外存在合法手牌，命令必须在 `SelectedCardIds` 中精确携带一个不同的当前手牌实例；运行时先提交 `EnergySpent(0)`，再把所选牌 Hand→ExhaustPile，随后按 `EnergyMaximum` 裁剪并仅在能量实际增加时提交 `EnergyGained(1)`，最后把来源牌 Hand→DiscardPile。来源是唯一手牌时无需选择，仍提交 0 费与来源弃置，但不产生能量结算；能量已满时选择牌仍被消耗，也不伪造 `EnergyGained`。空选择、多个选择、选择来源、自身不存在、跨 owner 或陈旧选择均在首次写入前返回稳定失败。

**共享选择 seam 与原子性**：`PlayCardCommand.SelectedCardIds` 是不可变权威输入；规则层以 `BattleHandCardSelectionRequest` 返回所需数量和合法实例集合；`BattleCardZonesData` 以 `BattlePreparedHandCardSelectionResolution` 联合 Prepare / Validate / Commit 所选牌 Exhaust 与来源牌 Discard。计划冻结 owner、起始 Layout、两张实例及最终所有卡区；Commit 只发布一次完整 `Layout` 和连续的两条 `CardMoved`，重复提交、跨 owner 或布局漂移均拒绝且零写入。职业运行时只负责把 Program 44 适配到该通用原语，Queue 的 ordering、drain、continuation、barrier 与 fault 所有权不变。

**UI 会话与表现**：`HandCardSelectionSession` 是 Hand UI 的局部不可变会话，不进入 `BattleTurnData`。会话冻结来源牌、合法候选、Layout、Turn 和 Queue 快照；候选左键确认并提交携带所选实例的新命令，来源牌左键或任意卡右键取消，未知点击忽略。选择期间所有牌停止拖拽，候选与非候选使用独立视觉角色但保持点击 raycast；Layout / Turn / Queue 漂移、容器禁用或销毁会清除会话且不产生权威写入。表现计划不伪造 prelude；已有两条真实 `CardMoved` 依次把 selected transient 路由到 Exhaust、source transient 路由到 Discard，并按 runner 的步骤转换时机清理。

**复用与边界**：这组 seam 表达的是普通“从当前手牌精确选择若干实例并原子解析归宿”的协议，不包含机枪兵卡名、Program、能量收益或目标牌规则，因此未来 Ironclad `Burning Pact` 可以提供自己的规则/效果适配器并复用命令、请求、卡区事务和 UI 会话。该可复用性不代表战士卡已经实现；本决定没有新增 Burning Pact 程序、翻转其目录状态或验证其运行时，也没有把升级能量 +2、任意多选、跨玩家选择、自动选择、Deck、奖励、Run、多人、Scene 或 Prefab 纳入范围。

**影响与验收**：正式作者表只将 Q134（3244）翻为 `Implemented`，SHA-256 为 `B3BA678FBC0C021F49C3F9FEDE4190099960EE109FFC302D96C77F29D54F4A6D`；i18n 只修改 B/C404-405，SHA-256 为 `8833E99F546B2C1195C4F0317A1B9208535ED083743F1ABF183874EFFFD23D77`。Luban 于 2026-08-13 14:55:40 成功；生成 JSON SHA-256 为 `5988DA20801C8BF724EF0E471466A0A746A5E732DE3450BD7680F00A735F2615`，全项目 168 张为 85/83，Marine 82 为 76/6、V1 60/4、V2 16/2。Localization Import/Validate、Sync/Addressables（15.85 秒，BuildLayout `buildlayout_2026.08.13.14.59.24.json`，134615 bytes）和静态编译均通过；行为 15/15、目录 38/38、含真实 AB 的正式聚合 306/306 与完整 EditMode 744/744 均通过。任务 ID、逐片红绿和 fixture/oracle 修正见 `06_testing/2026-08-13-machine-gunner-v2v-vent-heat-runtime.md`。

## CD-103：Burning Pact 以通用选择 Effect 和原子选牌抽牌归宿事务接入

**问题**：`Burning Pact`（3125）的基础行为是支付 1 Energy，选择并消耗另一张手牌，再抽 2 张；来源牌本身最后进入 DiscardPile。它同时需要 V2V 已建立的出牌前手牌实例选择、首批 Ironclad 已建立的普通抽牌，以及“选择牌离手后计算容量、来源牌仍占 Hand”的特殊投影。若先消耗选择牌再调用普通 Draw，后续布局或随机漂移会留下半次权威写入；若先把来源牌移出 Hand 再抽，则 Hand 10 场景会错误抽 2 张而非 1 张。把该卡按 ID、名称或职业写进 Queue 又会破坏通用配置语义和后续复用。

**数据语法与选择规则**：公共枚举新增 `EffectType.ExhaustSelectedHandCard = 5`，Effect 4012 固定为该类型、`Attribute.None`、Value 1；Effect 4013 固定为 `DrawCards`、`Attribute.None`、Value 2。3125 保持 `Program.None`，有序绑定固定为 `exhaustCards:4012,cards:4013`。通用解析器只接受选择 Effect 位于首项，后面恰好一个符合普通 Draw 约束的 Draw；缺失或重复任一项、Draw 在前、选择 Value 不为 1、非法 Attribute，或在两者前后夹入其他战斗 Effect，均在首次写入前返回稳定失败。运行时没有 3125、Burning Pact 显示名或 Ironclad 分支；牌面文本继续通过 `{exhaustCards}` / `{cards}` 从同一 Effect 数据格式化。

`BattleSingleOtherHandCardSelectionRules` 统一定义 Vent Heat 与 Burning Pact 的“当前来源之外另一张手牌”候选集合。存在候选时，`PlayCardCommand.SelectedCardIds` 必须精确携带一个合法实例，规则层以 `BattleHandCardSelectionRequest` 暴露 RequiredCount 1 与合法 ID；空选、多个、选中来源或陈旧实例均拒绝。来源是唯一手牌时不创建选择请求，也不伪造自动选择；Burning Pact 仍支付 1 Energy、抽 2 张并弃置来源，这与 Vent Heat 无候选时“不获能”的职业结果保持各自语义。

**原子卡区计划与结算顺序**：`BattlePreparedSelectedHandCardDrawAndPlayedCardDeparture` 在 Prepare 阶段冻结 owner、起始 Layout、洗牌 RNG 前后状态、最终 Layout、全部移动 settlements 与一次性提交标记；Validate 拒绝跨 owner、布局或随机快照漂移及重复提交；Commit 不重新洗牌，只发布一次最终 Layout。权威逻辑顺序固定为 `EnergySpent → optional selected HandToExhaust → optional DiscardPileToDrawPile / reshuffle → DrawPileToHand（至多 2）→ source HandToDiscard`。抽牌容量以“选择牌已离手、来源牌仍在 Hand”的投影计算：初始 Hand 10 时先消耗一张，仍只有一个空位，实际抽 1，来源最后弃置后 Hand 为 9。所有计划在 Energy 首写前完成联合准备和校验；失败保持 Energy、卡区、RNG、Turn 与 settlement 零写入。

**UI、表现与复用边界**：Burning Pact 直接复用 V2V 已验证的 `SelectedCardIds`、`BattleHandCardSelectionRequest`、`HandCardSelectionSession`、候选视觉和确认/取消协议，不增加 Scene/Prefab 或战士专用 UI。表现层消费真实 settlement：`EnergySpent` 没有可见 prelude，随后依次路由 selected→Exhaust、每次 Draw→Hand、source→Discard，并按既有 transient 生命周期清理。共享的是选择规则、命令协议和 CardZones 深事务；Vent Heat 的能量收益仍留在职业适配器，Burning Pact 的选择后抽牌仍由通用 Effect 序列适配，两者没有合并成含职业分支的浅模块。

**影响与验收**：3125 基础态已翻为 `Implemented`，Ironclad 85 张达到 9/76；全项目 168 张为 86/82，Marine 82 张保持 76/6，Effect 增至 13 项。正式工作簿 SHA-256 为 Enums `D0984D35BE585D04C9C1E56B62B5C8AEFBB0F9760A38DBACF9477B3A685D0EC3`、Card `C3025BA774D84E24CAD679DEE057AA79F25A41F81AC83798E6263DDE8FAA22DB`、Effect `0B002B0C97820E7BF3F5DEFB54084F53CF94F1F224E77E15A8E8BCB62CC30173`、i18n `A05411C781FE20D3CFA99F0FD4AAD08F68E34F0A80571E425A5C2772E50B4C37`；Luban、Localization、Sync/Addressables、静态编译和真实 AB 均通过。正式行为 9/9、目录 22/22、聚合 172/172、完整 EditMode 754/754，Console 最终 0 error。升级 Draw 3 仍只是目录/本地化元数据；升级实例、默认 Deck、奖励、Run、多人、Scene/Prefab 与其余 76 张目录卡不在本决定范围内。完整任务 ID、耗时、数据哈希与构建证据见 `06_testing/2026-08-13-sts2-ironclad-burning-pact-runtime.md`。

## CD-104：Not Yet 与战地手术通过共享 Heal 结果和行动结束再生适配接入

**问题**：`Not Yet`（3171）要求普通 Self Effect 在支付后恢复 10 点生命并进入 ExhaustPile；`Field Surgery`（3231）则在出牌时只添加 Regeneration 5 与 Shackle 1，随后于行动结束按当前 Regeneration 层数治疗并减 1 层。两者共享“恢复不得超过战斗生命上限”，但一个是配置 Effect，另一个是职业状态生命周期。若分别直写 Health，会产生两套封顶、快照和结算口径；若把 Regeneration 塞进普通 Effect executor，又会让通用层知道职业状态和回合顺序。来源还明确要求先清 Shackle / LoseStrength 等临时状态，再恢复，然后处理 Bomb 与 Burn；旧行动结束适配器曾把 Heal 放在清理之前。

**共享 Heal 契约**：公共枚举新增 `EffectType.Heal = 6`，Effect 4014 固定为 `Heal / Attribute.None / Value 10`。`BattleHealthRestorationOutcomeResolver` 以 requested、current health 与 max health 纯计算 `RequestedAmount`、`HealthBefore`、`HealthAfter` 和实际 `Amount`，用剩余生命空间封顶以避免加法溢出。普通 Effect executor 和职业 Regeneration 都只经 `BattleCombatantEffectOperations.ApplyPreparedHealthRestoration` 校验旧生命快照并调用 `CombatantData.ApplyHealthRestorationOutcome`；没有公开第二个 Health 写入口。`BattleHealthRestoredSettlement` 即使实际 `Amount = 0` 也保留请求量与前后生命，只有正实际量派生 `HealthRestoredNumber` 与 `+N`，不显示 `+0`。

**两个独立适配器**：3171 保持 `Program.None`，由 `heal:4014` 进入普通有序 Effect 计划；基础态为 2 Energy、Rare Skill、Self、Hand→ExhaustPile。满生命仍提交 Energy、零实际治疗和来源 Exhaust；Heal 后存在缺失 Effect 时，联合预构建在 Energy、Health、卡区、随机流、Turn 与 settlement 首写前整体失败。3231 保持 Program 31 与空 bindings；出牌事务按 `EnergySpent → Regeneration +5 → Shackle +1 → source Exhaust` 排序，不立即治疗，Shackle 溢出也在 Regeneration 或其他事实首写前失败。共享模块没有 3171、3231、名称、职业或回合阶段分支。

**行动结束与状态身份**：Regeneration 追加为第 17 个 `MachineGunnerCombatantStatus`，保留旧枚举数值。玩家行动结束的当前来源顺序固定为 `Shackle 清零 → LoseStrength 清零 → Heal → Regeneration -1 → Bomb → Burn`；Heal 使用行动结束计划冻结的 actor、Health、MaxHealth 与 Regeneration，不在提交时重算。该顺序细化 CD-083 / CD-084，旧实现的相反顺序只保留为 TDD 红灯，不登记成新玩法。Regeneration 进入 3277 / 3278 的私有状态种类集合；本决定只 supersede CD-098、CD-100 及其派生文档中“当前固定 16 种”的数量口径，不改写那些决定的历史文本，也不改变 Shackle 的 Attack 门禁或战术推进授权仍位于枚举之外的语义。

**影响与验收**：3171 与 3231 基础态翻为 `Implemented`，全项目 168 张达到 88/80，Ironclad 85 张为 10/75，Marine 82 张为 77/5（V1 61/3、V2 16/2），Effect 为 14 项。正式工作簿 SHA-256 为 Enums `dc35fc55df7a4223347f81054c09df88ddea3b6eb88da36de41499562dd7618e`、Effect `34eef4012c2b858e43fb0f7cb7c2417e1a3caa34d5afa3dcb46dfbd61c465af0`、Card `7c57c0a024d445d990ee275e7474a5460f7055504b1169f0b74dfd525d3665f3`、i18n `bd37b5660cbd5b1ceff8c07a58410c4f49e124acbdc3b97d893d4754b8551f5e`；Luban、Localization、Sync/Addressables、静态编译和真实 AB 均通过。来源顺序修正后的最终精确行为 `b511f5ddcd2041a9b264c0f982c4b600` 为 9/9，正式目录 `c3e5c7dbcb534cd18a85b635761fb8d7` 为 50/50，治疗视图精确任务 `4d5e4253e93840bd849571512f5f0a43` 为 1/1，含治疗视图与真实 AB 的最终聚合 `818f8283386b4d86aa625c6d95284245` 为 243/243，完整 EditMode `c6a86ba528804a13b1c84fe38c28b48b` 为 766/766。Not Yet 升级 13、Field Surgery 升级 6、AnyAlly / 多玩家、升级实例、默认 Deck、奖励、Run、Scene / Prefab 与其余目录卡不在范围内；完整证据见 `06_testing/2026-08-13-shared-heal-not-yet-field-surgery-runtime.md`。

## CD-105：Sword Boomerang 与幻彩射击通过共享具体重复伤害计划接入

**问题**：`Sword Boomerang`（3116）要求每一击从当前仍存活敌人中独立随机选择，前一击致死必须改变下一击候选；`Prismatic Shot / 幻彩射击`（3279）则固定显式目标，按目标命令起始状态种类展开逻辑段，并让 Stim、IncendiaryAmmo 与 PortableHelper 紧邻每一来源段。两张卡都需要在 Energy、Ammo、HP/Block、状态、卡区、随机流或 settlement 首写前确定完整结果。若通用 Effect 和职业程序各自维护重复循环，会复制目标投影、死亡停止、随机推进与快照校验；若在 Commit 时重新选目标或读取状态，则同一命令会受中途权威写入影响而失去确定性和失败零写入。

**共享 concrete prepared plan**：新增 `BattleRepeatedDamageExecutor`、`BattleRepeatedDamageRequest` 与 `BattlePreparedRepeatedDamagePlan`。目标策略只开放两个真实消费者需要的 `FixedEnemy` 与 `RandomLivingEnemyPerHit`：前者锁定一个显式敌人并在其投影死亡后停止，后者每段只从 Encounter 顺序中当时投影仍存活的敌人选择，没有候选时停止尾段且不再取随机数。Prepare 冻结来源标量、Encounter 全体敌人标量、每段目标、配置值、主伤 outcome、紧邻后效后的目标投影、全部敌方终态、卡牌目标随机流 before/after 及计划 settlement 总数；Validate 拒绝跨 owner、来源/敌人/Encounter/RNG/职业序列快照漂移和重复生命周期；Commit 只提交冻结段并最后一次性推进随机流，不重新选目标、重算公式或读取状态。

`IBattleRepeatedDamageHitSequence` 是 planner 与具体伤害管线之间的窄适配口。通用 `BattleRepeatedDamageEffectAdapter` 只解析普通 `DealDamage / Attribute.None` bindings，并用默认序列复用现有伤害 outcome 与内部写入口；它没有 3116、名称或职业分支。机枪兵 `MachineGunnerRepeatedDamageHitSequence` 只在职业侧冻结并提交每段主伤、IncendiaryAmmo 与 PortableHelper，同时核对 Stim 和 17 种私有状态快照；共享 planner 不知道 Program 79、Ammo、Stim、Burn 或 Helper。这样共享的是目标/投影/随机/计划生命周期，不是把两套不同伤害语义压成一个含职业条件的浅函数。

**随机所有权**：通用卡牌目标随机流的唯一可变 `GameRandom` 归 `BattleTurnController` 所有。`BattleSession` 只携带由战斗种子原样复制的不可变 `CardTargetRandomSeed`，`BattleLifetimeScope` 把种子装配给 Queue/Turn，`BattleCommandQueue.CardTargetRandomState` 仅供只读事务和确定性核对。Sword Boomerang 的 Prepare 在随机副本上演算，成功 Commit 后才把权威状态推进到冻结 after；目标规则、费用、绑定、快照或其他失败都不推进。固定目标幻彩射击不消费该随机域，也没有建立第二个全局或 Unity 随机源。

**两个适配器的精确语义**：3116 基础态为 1 Energy、Common Attack、RandomEnemy、DiscardPile，三条有序绑定 `damage` / `damageRepeat1` / `damageRepeat2` 均指向 4015=`DealDamage / None / 3`，即三次独立随机 3 伤；被击杀目标从后续候选移除，显式 TargetId 在首写前拒绝。升级第 4 次仍只是元数据。3279 基础态为 0 Energy、Rare Attack、显式 Enemy、Program 79、基础 Ammo 1；目标命令起始状态种类 `S` 由 Strength 非零、Vulnerable 正层与 17 种职业私有状态正层各计一次，逻辑段为 `[6, 9 × S]`。Stim 激活时每个逻辑段后立即复制同基础值，整卡 Ammo 为 `1 + logicalCount`，资源门禁全额成功或零写入；每段按 `main Damage → IncendiaryAmmo Burn → PortableHelper` 完成，固定目标死亡后停止且不重定向。升级首段 9、重复段 9 仍仅为元数据。

**复合 settlement 回归修正**：初次广义行为聚合 `14131e7fa23c4f14a3a08e2cad0da556` 完成 250 项但有 16 项失败，最小化后异常为“机枪兵卡区 settlement 顺序不连续”。根因是既有机枪兵复合卡区计划在计算 starting order 时，本地 `settlements` 尚未包含稍后前置的 `EnergySpent` / 可选 `AmmoSpent`。修复把这两类不可变付款记录先加入局部计划序列，再让 Vent Heat、PreparedDraw、离手后抽牌、换手创建及 repeated plan 统一从 `settlements.Count` 取顺序；构造记录本身不写权威资源，失败时仍整体丢弃。该修正恢复既有 Bully、Limit Overload、Machinegun Burst 与 Vent Heat 路径，不改变其玩法语义。

**影响与验收**：3116 与 3279 基础态翻为 `Implemented`，全项目 168 张达到 90/78，Ironclad 85 张为 11/74，Marine 82 张为 78/4（V1 61/3、V2 17/1），Effect 为 15 项。正式工作簿 SHA-256 为 Enums `DC35FC55DF7A4223347F81054C09DF88DDEA3B6EB88DA36DE41499562DD7618E`、Card `EA90C1A34FBDD9C54EBE2832C6CCC796DC4752A6B90C15F6A42BDB8C03A2CDF1`、Effect `35BF163D09E6F8AA6478C134D90A5FBAC304CC3135357D8237909DBC87ECAE64`、i18n `B80CD6EDCD0EAE2F52812B1CFF5DDAD96C1AB0507CD05E012C919DB05122215F`。Luban、Localization Import/Validate、`Sync and Build All`、Addressables 13.962 秒、BuildLayout/物理 bundle 与静态编译均通过；双卡定向 `6932f72f288a477ca5869c21e3ac3996` 为 11/11，正式门禁 `908e5fb8b93e437d89533bb1b727231a` 为 53/53，回归代表集 `6ee679521f4c45d9a69b9984110c51bb` 为 5/5，最终行为聚合 `4ea4eff81b3c4ce786e318d0902c1ed4` 为 243/243，完整 EditMode `3e0a091d891e4f918668b99cb4a20157` 为 776/776（77.7525946 秒）。默认 Deck、奖励、Run、多人、Scene/Prefab、升级实例与其余目录卡不在本决定范围内；完整证据见 `06_testing/2026-08-13-shared-repeated-damage-sword-boomerang-prismatic-shot-runtime.md`。

## CD-106：Body Slam 与二手烟以来源快照动态值和通用 Poison 生命周期接入

**问题**：`Body Slam`（3105）的伤害基础值来自施放者当前 Block，`Secondhand Smoke / 二手烟`（3270）的 Poison 基础值来自施放者当前 Smoke；两者都不能在 Commit 时重新读取可变来源，也不能为卡牌 ID 或职业名称复制伤害 / 状态写入口。Poison 还需要统一适用于玩家与敌人的权威层数、行动开始 tick、致死中止和表现事实。若 Secondhand 先提交 Poison 再按 live 卡区直接移动来源牌，同步 observer 可改写布局并留下半次出牌；若为 Poison 修改 Participant HUD Prefab，又超出本切片授权。

**来源动态值与卡牌适配**：通用 Effect magnitude 支持 `ConfiguredValue` 与 `SourceBlock`。`SourceBlock` 在 Prepare 时冻结来源当前 Block 为普通 `DealDamage` 的 base magnitude，随后继续经过 Strength、目标 Vulnerable、目标 Block / HP 与致死公式，且不消费来源 Block。3105 基础态只使用该通用 Effect；升级仍是 metadata。Program 70 在 Prepare 时冻结来源当前 Smoke，把正值作为 Poison apply 请求交给显式敌方目标，不改变来源 Smoke；Smoke 为 0 时操作为空但出牌仍成功。3270 升级文本描述来源与目标 Smoke 总和，但当前没有升级 `CardInstance`，运行时基础态不得提前读取目标 Smoke。

**通用 Poison 契约与时序**：Poison 是 `CombatantData` 的通用权威非负事实，不属于 17 种职业私有状态。Apply / Tick 都以 Prepare、Validate、Commit 冻结参与者快照与一次性生命周期；tick 绕过 Block，生命损失为 `min(current Poison, current Health)`，随后 Poison `max(0, before - 1)`，致死也减层，零层不写 settlement。敌人在自己的行动开始先 tick：非致死再执行旧行为 / 状态 / intent advance，致死则直接产生 source-not-alive skip、不推进意图并继续 Encounter。玩家在 PlayerRoundStart 先按稳定玩家顺序 tick：非致死才继续 Block / 职业状态 / 资源 / 抽牌，致死则跳过 reset 并把 BattleEnded 延迟到状态机栈退出后提交。

**状态种类、卡区与表现边界**：3277、3278、3279 共用状态种类 helper，当前集合为 Strength、Vulnerable、通用 Poison 和 17 种职业私有状态，最大 20；同一状态只按存在计一次，3277 读来源，3278 / 3279 读目标并在命令起点冻结。Secondhand 只在没有其他卡区深操作时使用 `BattlePreparedPlayedCardDeparture`：Poison 首写前准备 / 校验，末尾按冻结最终布局一次提交；Draw、DrawToHandLimit、Replace、选择等既有深事务继续独占卡区变化。Poison tick 表现只产生 Health loss number，致死追加 death transition，不产生 attack shake 或 block absorbed。没有修改 Scene / Prefab，因此常驻 Poison 图标、层数文本与 pulse 未实现；M9B 的 HUD 结论继续成立。

**已知边界**：玩家 Poison plans 本身可联合准备，但 Poison 之后的完整 round reset 还不是一个跨模块 joint transaction；正常生产 observer 只做展示投影，异常重入写入是后续 P2。敌人当前没有公开 Regeneration 路径；未来开放时，其行动结束治疗计划必须从 Poison 后投影生命准备，不能读取 tick 前生命。升级实例、默认 Deck、奖励、Run、多人和 UI 专属 Poison 资产不在本决定范围。

**影响与验收**：3105 / 3270 基础态已翻为 `Implemented`，全项目 168 张为 92/76，Ironclad 85 张为 12/73，Marine 82 张为 79/3（V1 61/3、V2 18/0），Effect 为 16 项。Body Slam 的正式基础 / 升级文本均为 EN `Deal {damage} damage, equal to your Block.`、ZH `造成 {damage} 点伤害，数值等同于你当前的格挡。`；`{damage}` 保持 validator 绑定且运行时动态显示来源 Block，升级实例仍未实现。正式工作簿 SHA-256 / bytes 为 Enums `48aa59ec32cba63429678f34d2f88d8010d0ba2842865e021d3578b93ce2ef5e` / 10982、Effect `cac78b6069764a037275b3261125e379de9a8f75a358f34c9d430ac98dff6d14` / 4603、Card `01c1613de65ee7e9b6fb49a774fecb4e31c53535c2186cb0b5e9bbac03358be0` / 23197、i18n `0bb37d8ba79bff9c3d8853b95af7c436373893385c0c62055e5400be2fbd8d0b` / 29057。Luban、Localization Import / Validate、Sync/Addressables（50.667 秒）及真实 `AssetBundleProvider` 均通过；BuildLayout / bundle、生成物与 Localization asset hash 见验收页。前置任务前缀 `419c…` 2/2、`b5f…` 8/8、`79a…` 289/289 与 `fd6…` 的预翻表红灯保留；最终权威定向 `88e36d2a5cbb47b7b4a67207dad00856` 为 9/9，完整 EditMode `9ca3d43a79d24b25a917fad7b6166584` 为 793/793，强枚举清理后精确任务 `40af8c25ba4442ffbe9e98451890f01c` 为 1/1。静态 Editor build 0 error / 12 条既有 warning，资产屏障 idle、Console 0 error；常驻 Poison HUD / Prefab 与升级运行时仍未实现。完整证据见 `06_testing/2026-08-14-shared-source-magnitude-poison-body-slam-secondhand-smoke-runtime.md`。

## CD-107：Barricade 与 Garrison 以共享 Block 保留授权接入，手牌保留保持职业侧单行动语义

**问题**：`Barricade`（3157）要求 Block 永久跨玩家回合保留，`Garrison`（3246）则只让 Block 在计时层存续期间跨回合保留，并同时让精确选择的两张其他手牌只跨过一次行动结束弃牌。若两张卡各自在 Turn 中改写清 Block 时机，会复制永久 / 计时判定和层数递减；若把手牌保留也塞进公共 Block 模块，则模块会知道 CardZones 与职业选择规则，接口变浅且难以复用。

**选择**：共享 `BattleBlockRetention` 只拥有参与者的永久授权、计时层数与玩家回合开始计划。`PreparePermanent`、`PrepareTimed` 与 `PreparePlayerRoundStart` 都冻结 owner 和旧快照，经 Validate 后一次 Commit；玩家回合开始以进入该时点的授权决定是否清 Block，然后才让计时层数递减，所以 `2→1`、`1→0` 两次均保留 Block，下一次无授权时才清除。3157 通过普通 Self Effect 4017 建立永久授权并进入 PowerPile；3246 的职业适配器获得 12 Block、建立 2 层 Garrison，并发布对应状态 settlement。共享模块没有卡牌 ID、职业、手牌或 UI 分支。

**Garrison 选择与一次行动保留**：规则与 UI 必须从来源以外的当前 Hand 精确选择 2 个不同实例；候选足够但未选、少选、多选、重复或漂移都在 Energy、Block、状态、卡区与 Turn 首写前失败。UI 会话累积到精确 2 张才提交，来源左键或任意右键取消。成功后职业侧冻结这两个实例，当前一次 `EndPlayerAction` 只弃置未选手牌；随后立即消费该授权，下一次行动结束恢复全手弃置。该状态不改变 Draw、DiscardPile 或永久 Deck。

**边界与验收**：3157 / 3246 基础态翻为 `Implemented`，全项目 94/74、Ironclad 13/72、Marine 80/2（V1 62/2、V2 18/0），Effect 17。升级 Barricade 2 Energy 与 Garrison 15 Block / 选 3 仍仅为 metadata；升级实例、默认 Deck、奖励、Run、多人、Scene / Prefab 不在本决定范围。正式工作簿哈希、分阶段 Sync / Localization / Addressables、02:59:03 BuildLayout、生成前 3/3、最终定向 300/300、完整 EditMode 798/798 与静态 0 error / 12 warning 见 `06_testing/2026-08-14-shared-block-retention-barricade-garrison-runtime.md`；未 commit、未 push。

## CD-108：Havoc 与 Opportunistic Strike 通过 Queue-owned system-token continuation 触发免费出牌

**问题**：Havoc（3108）要从 DrawPile 顶部取牌并免费打出且强制 Exhaust；Opportunistic Strike（3243）要在上一张成功牌为 Attack / Shoot 后，从当前 Hand 随机触发一张 Attack。两者都必须执行完整出牌规则、效果与表现，但若在 Turn 内递归调用出牌，会绕过 Queue 的唯一写入口、表现屏障、fault 与 continuation 顺序。

**选择**：Queue 持有不可伪造的内部 system token，并只在前一命令成功提交后串行消费 frozen continuation。触发牌仍进入正常出牌管线，但费用冻结为 0；Havoc 的来源固定为 DrawPile 顶牌且最终归宿强制 Exhaust，Opportunistic 的候选固定为当前 Hand 中的 Attack，并使用既有确定性随机域。外部仍只能调用 `BattleCommandQueue.Submit`，生产代码不公开第二写入口；无候选、前置牌型不符、快照漂移或触发牌失败均走现有 typed 结果，不以递归半提交污染当前命令。

**边界与验收**：3108 / 3243 基础态翻为 `Implemented`，全项目 96/72、Ironclad 14/71、Marine 81/1（V1 63/1、V2 18/0），Effect 18。Havoc 升级费用与 Opportunistic 升级选择行为仍仅为 metadata；自动选择非 Attack、任意卡区触发、链式无限触发、升级实例、Deck / Run / 多人不在范围。初次 full 暴露强枚举与非展示 Effect 本地化门禁问题，修正后定向 8/8、cleanup 1/1、完整 EditMode 802/802、静态 0 error / 12 warning；数据、AB 与任务证据见 `06_testing/2026-08-14-shared-triggered-play-havoc-opportunistic-strike-runtime.md`。

## CD-109：Juggernaut 与 Unstoppable 共用 settlement-derived trigger 深模块与 Queue 表现屏障

**问题**：`Juggernaut`（3169）要在持有者后续每次实际获得 Block 后伤害随机敌人；`Unstoppable`（3250）要在持有者造成致死或破除正 Block 后随机免费打出一张合法攻击。两者都由已提交 settlement 触发，但子事务不能在 Turn 或职业适配器内递归写状态，也不能越过父命令表现屏障。

**选择**：引入共享 `BattleSettlementTriggerEngine`。Power 注册以 Prepare / Validate / Commit 冻结 owner、trigger kind、action kind / value、候选模板与注册表 revision；父命令提交后，引擎按 settlement 顺序、再按注册顺序冻结 intent batch。`BattleCommandQueue` 持有唯一引擎和独占确定性随机流，并且只在父结果的表现屏障完成后，以内部 system token 串行执行 `ResolveSettlementTriggersCommand`。每个子动作再次 Prepare / Validate / Commit 参与者标量、Encounter 敌人顺序、随机 before/after、伤害 outcome 或临时卡请求；成功首写后才推进随机。共享引擎不知道卡牌 ID、名称、Program 或职业分支。

**消费者语义**：Juggernaut 通过 Effect 4019（raw type 10 / `None` / 6）注册 `BlockGained -> RandomEnemyDamage`；只匹配目标为 owner 且 `Amount > 0` 的格挡 settlement。这个 6 点子伤害不读 Strength / Vulnerable，仍由目标 Block / HP / 致死结果承接。Unstoppable 由 Program 50 注册 `FatalOrBlockBroken -> RandomCardPlay`；职业侧只负责从静态表顺序提供 `Implemented` / Attack / 非 Shoot / 目标可自动解析候选，共享引擎创建唯一临时实例，随后通过 Queue 以 `Waived` 费用完整出牌并强制 Exhaust。当前触发注册 ID 在它自己派生的 settlement 链中抑制，阻止同一 Unstoppable 自递归，其他注册仍可按顺序观察。

**边界与验收**：只实现两张基础态；Juggernaut 升级伤害与 Unstoppable 升级 debuff 触发仍仅为 metadata。本切片不新增 HUD / Prefab / Scene，不开放通用 event bus、任意 action grammar、Deck / 奖励 / Run / 多人或升级实例。正式生成后全项目 168 张为 98/70、Ironclad 15/70、Marine 82/0（V1 64/0、V2 18/0）、Effect 19，强枚举已替代开发期 raw 10。Luban 通过；首次 Sync 正确拒绝缺少 `{triggerDamage}` 的 i18n，单点修复后 Localization / `Sync and Build All` 与 Addressables 15.175 秒成功，BuildLayout SHA-256 为 `429C1CD806275B7095205307B67DAE71F39678C19E53E3C39B574193ACDAA769`。Runtime / Editor 静态编译分别为 0 error / 6 warning 与 0 error / 12 warning；定向 `054b6bcd5d734f729a2f1f95c4e7a80d` 7/7，完整 EditMode `d156b8e2537546ef9e83da0ef5dadd2a` 807/807。作者表、生成物和精确耗时见 `06_testing/2026-08-14-shared-settlement-trigger-juggernaut-unstoppable-runtime.md`。

## CD-110：BattleScene Roadmap 固定归档，Run 阶段使用独立 Roadmap 与递归切片 Grill 门

> **2026-08-24 路由修订：** 本决定中“动态阶段只在 `SESSION_LOG.md` 维护”已被项目知识工作流 V2 取代；当前状态只查 `STATUS.md`，`SESSION_LOG.md` 保留为历史 changelog。Roadmap 与 Grill 门禁语义不变。

**问题**：`ROADMAP.md` 的标题和主体职责一直是 BattleScene MVP（M0～M10），但末尾同时承载 G1～G8 的未来 Run 草案。BattleScene 已形成可验证检查点后，继续在同一文件扩写会让历史验收与当前计划争夺“活跃 Roadmap”职责；若现在一次性 Grill 整个 G1，又会在主菜单、Run 生命周期、战斗进出、战后过渡、奖励与存档尚未拆清时制造一个过大的伪实施计划。

**选择**：`ROADMAP.md` 保留原路径并改为 `archived`，固定保存 BattleScene MVP 的目标、依赖和验收历史，不物理迁移，也不再承载 Run 动态规划。新增同层 `RUN_ROADMAP.md` 作为 G1～G8 的阶段骨架；它只记录阶段结果、依赖和门禁，不构成代码、表格、Scene、Prefab 或构建授权。动态阶段仍只在 `SESSION_LOG.md` 维护。

Run 规划采用递归切片门禁：每个可执行切片先处于 `needs-grill`，只对该最小可观察结果 Grill，确认玩法事实、生命周期、失败语义、数据所有权、UI 边界、验收和排除项后，才在 `plans/` 建立窄计划；计划仍需用户明确授权才能实施。如果实施中拆出具有独立玩法选择、生命周期边界、高影响文件或失败语义的子切片，该子切片重新进入 `needs-grill`，不继承父切片授权。纯机械接线、已冻结契约下的测试补齐和生成物同步不重复 Grill。

**阶段边界**：当前只完成 Roadmap 换轨。G1 尚未 Grill、尚未切片、尚未实施。“恭喜/战后总结”是否独立存在、是否承担奖励事实、何时可跳过等没有冻结来源，只登记为未来 G1/G4 交界处的 Grill 问题，不在本决定中发明功能。BattleScene UI、视觉反馈和动画是功能性基线但非最终品质；它们进入独立表现债务轨道，除非阻塞新切片验收，否则不抢占 Run 本体，最终统一收口仍属于 G8 或单独获批的表现切片。

**检查点与影响**：BattleScene 里程碑 commit 为 `e07e39a`，tag 为 `milestone-battlescene-mvp-2026-08-14`，检查点分支已推送 GitHub；最新完整 Unity EditMode 记录为 807/807 passed。根 `README.md` 对外状态改为 BattleScene MVP complete / Run planning next，并明确当前 UI 与动画仍是 provisional。本决定只改文档职责和规划门禁，不改运行时代码、配置表、生成物、Scene、Prefab 或构建产物。

## CD-111：Battle 进出边界使用 Queue-owned 终局结果与父 Scope 输入来源

> **后续状态：** 本决定当时保持 open 的 `DEP-007` 已由 CD-112、CD-113 与 CD-116 完成并在依赖账本中标记 resolved；本段仍保留为当时的交接事实。

**问题**：交接审计 B2R-101/201 证明胜负原先只由 internal 终局规则即时派生，UI 为终局文案再次读取参与者事实；未来非 UI Run 消费者没有 typed、exactly-once 的读取点。B2R-102/202 又证明 `BattleLifetimeScope` 无条件用 Inspector 常量创建 `BattleSetupOptions`，未来父 Run 生命周期不能替换 hero / encounter / seed。若 UI 回调成为结果来源，或 Run 分别写 seed、HP、牌组，会产生第二事实源；若终局刚进入 `BattleEnded` 就对外发布，又会早于当前表现屏障的稳定结束语义。

**终局结果选择**：新增公开不可变 `BattleResult`，包含 `BattleResultKind`（Victory / Defeat）、产生结果的 `AuthoritySequence`、终局 `RoundNumber`，以及按 `CombatantId` 稳定排序的不可变玩家结算快照 `Players`；每项冻结 `CombatantId / TemplateId / Health / MaxHealth`，并提供派生事实 `IsAlive`。`BattleCommandQueue` 只在首次成功进入 `BattleEnded`，且终局 settlement 与 continuation 已完全冻结后创建一次，保证玩家快照读取的是结算后权威事实；同一对象附着到 `BattleCommandExecutionResult`，表现计划必须把它传给 `BattleOutcome` step，UI 只将 `Kind` 映射为既有本地化键，不再重跑 `BattleTerminalRules`。Queue 的只读 R3 `Result` 在对应表现 completion 真正解除屏障后才发布该同一对象；旧、迟到或重复 completion 不能重复发布，新 Battle Scope 从空结果开始。

**输入选择**：`BattleSetupOptions` 继续是 hero / encounter / seed 的唯一不可变载体。每个 `BattleLifetimeScope` child container 注册一个 singleton：若父链能解析 `IBattleSetupOptionsSource`，只调用一次并冻结其返回对象；若不存在来源，才以当前 Inspector 字段创建默认对象；来源返回空对象直接失败，不静默使用默认值。`BattleSession` 继续从 Hero MaxHealth 与 Deck 模板建立生命和牌组，并由同一个 seed 沿用现有卡区、敌人意图、职业及卡牌目标随机域派生，不增加第二随机源或改变盐。

**边界与后续**：本决定不增加 `Abandoned`，不把 Restart / Exit / 回地图 / 奖励从 HUD 收回流程层，也不把牌组或奖励塞进当前 `BattleResult`。结果已经冻结结算后玩家身份与生命事实；但父 Scope 的生产 Run 注册、Run 根种子与战斗标识、初始 HP / 牌组输入，以及 `BattleResult → RunState` 原子写回仍需 G1 分别 Grill。因此 DEP-007 保持 open，不能宣称完整 Battle 进出契约已经完成。现有 `BattleEnded`、`BattleAlreadyEnded`、M9F 稳定面板与按钮门控不变。

**架构与验收**：这是对 AC-002 已确定替换源的最小接缝实现，并遵守 AC-001/004/007/008/009；没有 reopen Locked 约定，不修改 `ARCHITECTURE_CONVENTIONS.md`。结算快照补强的 compile RED 为 6 个缺失类型 / `Players` 错误，两个精确 GREEN 均为 1/1；QueueM8D 14/14、相关 9 个 fixture 127/127、完整 EditMode 811/811、Runtime / Editor 静态编译均通过。唯一 Unity Editor 中已串行通过 Bootstrap → BattleScene、真实 Queue 胜利、HUD Restart、新 Scope 空结果、真实 Queue 失败、HUD Exit exactly-once guard、2 秒无晚到结果、零 active tween 与 Stop 后 Battle Scope 归零；Editor 未证明 Player OS 进程实际退出，只证明 HUD Exit listener 与重复提交 guard。完整任务号和排除范围见 `06_testing/2026-08-14-battlescene-to-run-seam-corrections.md`。

## CD-112：G1-A 以唯一 RunState、attempt snapshot 与 child-scope Result bridge 贯通首战

> **部分 superseded by CD-116：** `RunBattleSnapshot / RestartBattle`、Defeat 恢复 snapshot 与同节点重开仅是 G1 当时事实。child-scope Result bridge、attempt 身份和唯一 Store/Flow 所有权仍有效。

**问题**：G1-A 需要在 Bootstrap、入口 Scene 与 BattleScene 之间保存一局 Run 的 Hero、生命、牌组、节点和本战随机输入，同时允许失败后恢复进战前状态并用新随机输入重开。若入口 UI、静态单例或 Battle Scene 临时变量各保存一份状态，胜败与重开会产生冲突事实；若 Battle UI 直接判胜或写 Run，会绕过 CD-111 的稳定 `BattleResult` 边界；若 bridge 放在 root，又会让旧 Battle Scope 的迟到回调污染新 attempt。

**选择**：`RunStateStore` 是当前进程内唯一 Run 事实写入所有者，发布完整不可变 `RunState`。每次 `BeginBattle` 先冻结 `RunBattleSnapshot`，再以 `RunId + AttemptSequence` 建立 `RunBattleId` 并签发不可变 `RunBattleInput`；`RestartBattle` 先恢复 snapshot，再递增 attempt。seed 使用正整数空间内与 `int.MaxValue` 互素的固定步进映射，保证有效 attempt 序列内不与另一 attempt 重复。`RunFlowService` 只读取配置、调用 Store 迁移并请求 SceneFlow，不保存第二份 Run 业务状态。

**Battle 输入与结果**：CD-111 的 `BattleSetupOptions` 扩为可选 `PlayerInitialHealth` 与 `DeckTemplateId`，保留 legacy Inspector fallback；Run-managed Battle 把 `RunBattleInput` 的 Hero、Encounter、seed、当前生命和牌组模板一次冻结到 child Scope，`BattleSession` 实际以当前生命/最大生命创建玩家，并用指定牌组和同一 seed 建立全部既有随机域。`BattleResultRunBridge` 只注册为 `BattleLifetimeScope` child entry point，同时冻结该 Scope 的 setup/attempt 身份；只接受当前 attempt 的稳定 Result，胜利写回结算生命并完成节点，失败保留 snapshot 而不写临时生命，Dispose 时解除订阅。`AuthoritySequence` 仍只用于战内排序，不充当跨战斗身份。

**入口与兼容边界**：Bootstrap root 跨 `RunEntryScene` / `BattleScene` 保留 Store、Flow 与 SceneFlow；`RunEntryLifetimeScope` 只拥有 View/Presenter。入口 View 全部使用 TMP + i18n，页面和候选 Hero 只在创建 Run 前是局部 UI 会话；Run 创建后页面完全由 `RunState.NodeStatus` 派生。程序化创建的 `InputSystemUIInputModule` 只依赖自身 `OnEnable` 分配默认 UI Actions，不再次调用 `AssignDefaultActions`，避免禁用 Domain Reload 时破坏包级共享静态状态。Run 模式保留 Battle 终局阻断面板但隐藏旧 Restart/Exit，避免与 bridge 竞争；没有 active Run 的 legacy Battle 继续使用 Inspector setup 和 BattleScene 重载地址。

**边界与验收**：本决定只覆盖单人、单节点、首战胜败和失败重开；不建立存档、奖励、退出 Run、永久死亡、多节点、地图生成或多人。最终完整 EditMode job `55272b6354df42b6a0f351975ab58e71` 为 873/873，`Sync and Build All`、Packed Play Mode 的两名 Hero、胜利 17/30 回图、失败恢复 70/70、attempt seed `768055331 → 261103211` 与新 Session 空弃牌/消耗区均通过；当前 OS CJK 字体的跨平台可携带性仍是独立风险。完整证据见 `06_testing/2026-08-16-g1a-entry-first-battle-run-lifecycle.md`。

## CD-113：G2-A 以稳定态 Save Document、原子单槽 Adapter 与显式恢复编排持久化 Run

> **部分 superseded by CD-116：** 当前写入 schema v1、Defeat 不写盘、失败页同节点重开仅是 G2 当时事实。Bootstrap root、save port、同目录 temp、durable validation、Move/Replace 与失败不覆盖已提交事实仍有效。

**问题**：G2-A 需要跨进程保存最近成功的地图稳定态，同时禁止 Battle 中间态泄漏到磁盘，并在坏档、配置漂移或提交失败时保住旧有效 checkpoint。旧 CD-009 曾前瞻独立 RunScope，但现行 CD-112 和生产 Scene parent seam 已由 Bootstrap root 跨场景持有 Store / Flow；为本片强建新父 Scope 会同时改写两个高影响 Scene，并让“无 active Run 的主菜单如何解析入口 Flow”产生新的生命周期问题。

**选择**：继续以 Bootstrap root 持有 `RunStateStore`、`RunFlowService` 和 `IRunSaveStore`，active Run 生命周期通过 Store 的显式 restore/clear 表达，不修改 Scene parent。Run 领域定义 schema v1 的 `RunSaveDocument`、严格 codec、显式 migration 入口与 save port；Document 只接受无 transient battle facts 的 `Available` / `Completed` 稳定态，并保存 Run/Hero/HP/Deck/Encounter、随机根、节点状态和 attempt 序号。恢复时按当前配置检查 Hero、Deck、Encounter 以及 Hero 最大生命兼容性；任何不兼容都在 hydrate 前类型化失败。

**持久化与失败语义**：唯一生产 Adapter 位于 Infrastructure，以 `Application.persistentDataPath` 构造单槽 versioned JSON。提交先在同目录创建 temp，durable flush 后重新解析、迁移、校验并与输入等价比较；首次提交同卷 Move，已有正式档使用 `File.Replace`，任何平台/IO/校验失败都不得退化为删除旧档后覆盖。Hero 确认提交 S0；胜利只在 BattleResult 已完整结算、清除 transient 并返回地图后提交 S1；BeginBattle、战斗过程、失败和 G1 重开不调用 save port。重试复用缓存的同一 Document，不重取 entropy、不重放 BattleResult；确认退出时清除未提交内存态并恢复上一成功 checkpoint。

**UI 与边界**：启动只探测存档，不自动 hydrate；Continue 是显式用户意图。有效档或不可用档阻止直接新开局并要求确认放弃；坏 JSON、非法 UTF-8、未知 schema、缺失/不兼容配置与 IO 错误禁用 Continue、显示原因，只有确认后才删除。删除有效档失败仍保留 Continue 能力。当前 Completed 节点保留 S1 并显示“节点已清除、后续内容未接入”；CD-112 的失败页、snapshot 与新 seed 重开保持原语义。本决定不授权 G3+、平台 Save Spike、平台 SDK、云存档、多槽、奖励、地图生成或永久死亡。最终完整 EditMode `0004316410dc4b1e9db8d80312499dc4` 为 947/947；Luban、本地 Addressables 构建与唯一 Editor 的 S0 / Continue / 战中不写盘 / S1 / 冷启动恢复 / 确认删除主链通过。完整证据见 `06_testing/2026-08-16-g2a-run-persistence.md`。

## CD-114：RunEntry 视觉使用场景直接依赖、共享纸纹与纯表现一次性时间线

**问题**：`RunEntryScene` 的 Canvas、页面和菜单全部由 `RunEntryView` 在运行时创建，Scene YAML 没有可直接装饰的菜单层级。若另建 Canvas/Prefab 会形成竞争交互树；若视觉组件读取 RunState 或重新实现按钮动作，会改变 CD-112/113 的事实所有权；若把三张彩纸各自做成 1920×1080 位图或独立 Addressable，又会复制相同颗粒并增加无必要的加载生命周期。

**选择**：`RunEntryView` 只新增两个场景序列化资源字段：完整 `ENTRY-BG-002` Sprite 与一张 1024² 中性共享纸纹 Texture2D。它们作为 `RunEntryScene` 的直接依赖进入同一 bundle，不新增 AddressableName、Resources 路径或运行时 release 责任。运行时新增 `EntryPaperStackView` 只创建背景、三张完整 RawImage、V06 响应式几何和私有 DOTween Sequence；三纸共用纹理并以 tint 得到米白/炭黑/砖红，全部 `raycastTarget=false`。`EntryOctagonGraphic` 只绘制透明纸面内芯与细切角边线；真实 Button/TMP、五项文案和 `RunEntryAction` 仍归既有 View/Presenter。

**构图与生命周期**：1920×1080 使用 V06 的 `+17.52°` 与实测边界：米白 top/mid/bottom 约 `576/746.5/916.7`，黑、红中线外边约 `812.5/863.5`；标题和菜单是 `PagesRoot` 的稳定子层，不在旋转 `PaperStackRoot` 下。红/黑/米白从 `0/.12/.24s` 开始，以 0.76s OutCubic 到位；米白 `1.00s` 停稳，内容 `1.10s` 开始淡入。Sequence 使用私有 ID，首次主菜单只播放一次，非主菜单/禁用/销毁只收口或 Kill 自有 Tween，不读写任何 Run、Battle、存档或导航事实。CanvasScaler 继续为 1920×1080、match 0.5；超宽保留左侧基准构图区，窄窗缩放菜单而不扩大米白纸，背景 cover 在窄窗右缘对齐保护主塔。

**边界与验收**：本决定不授权 G3+、地图、FishNet/多人、战斗、存档、GameData、菜单业务重写、ProjectSettings/asmdef 或新包。当前唯一 Unity 6000.5.5f1 Editor 已完成 22/22 定向 EditMode、Local Addressables、BuildLayout 同 bundle/AssetBundleProvider 证明、Bootstrap Packed Play 1920×1080 截图与 Settings 往返；21:9/窄窗已有几何自动化但没有各自截图，也没有 Player/目标平台字体与 DPI 验收。完整来源、importer、job 与风险见 `06_testing/2026-08-17-run-entry-visual-slice.md`。

## CD-115：BattleCommandQueue 隐藏提交预注册协议并以 Queued 生命周期建立 UI pending

**问题**：CD-043/045 为解决同步执行早于 UI pending 登记的问题，引入了正确的 opaque handle 与 Queue-owned lifecycle，但生产调用者必须先向 concrete `BattleCommandSubmissionCoordinator` 以同一命令引用 `PreRegister`，再自行建立 pending、调用 `Queue.Submit`，并在拒绝时回滚。该隐藏顺序协议同时泄漏到 `BattleCommandRuntimeDriver`、`BattleTurnHudView`、`HandCardContainer` 和共享测试扩展；漏一步或换一个命令引用都会失败，使名义上的单一 `Submit` seam 实际需要两个模块协作。

**选择**：`BattleCommandQueue.Submit` 在内部接受判断前签发 handle；调用者只提交不可变 `BattleCommand`。已接受命令仍先发布唯一 `Queued`，生命周期事件同时携带该次原始命令引用，Hand/HUD 在 `Queued` 回调中识别自己关心的 `PlayCardCommand` / `EndPlayerActionCommand` 并建立精确 handle pending；同步普通终态或 fault 随后按同一 handle 清除。结构性拒绝不分配权威序号、不发布生命周期，也不会让 UI 建立需要回滚的 pending。Queue 公开只读 `Lifecycle`；coordinator 的注册、匹配、取消、对账与生命周期源均降为 internal implementation，生产 View 与 runtime driver 不再注入它。

**保留边界**：本决定只 supersede CD-043/045 中“生产调用者必须预注册 concrete coordinator”和“coordinator 是 View 的生命周期入口”两项接口口径。权威序号分配、唯一迭代 drain、callback 非重入、FIFO continuation、system token、普通失败空 settlement、表现屏障、completion 与 fault 诊断继续由既有 Queue/scheduling core 独占，语义和顺序不变。internal scheduling 合同测试继续直接覆盖预注册/拒绝/伪造 token；普通 Queue、UI 与业务测试只调用 `Submit`。没有新增 interface、模块、Scene/Prefab、配置表或包依赖，也没有修改 `BattleCardPlayEvaluation`。

**影响与验收**：三个生产调用点和 13 个旧 `SubmitRegistered` 测试消费者已迁移；共享测试 coordinator registry/扩展已删除。新增 RED 首先证明 lifecycle 缺少原始 Command，随后旧 helper 的重复预注册暴露迁移遗漏；最终 M8B 11/11、相关聚合 116/116、完整 EditMode 953/953 均通过，Unity 编译 Console 0 error，`git diff --check` 通过。未运行 PlayMode、Player build、Addressables 或人工 BattleScene smoke；本次不含资产/配置变更。完整方案与证据见 `plans/2026-08-17-battle-command-submission-interface-deepening.md` 和 `06_testing/2026-08-17-battle-command-submission-interface-deepening.md`。

## CD-116：G3 以冻结 MapDefinition、recipe-only 存档和 Terminal(Defeat) 贯通单 Act 地图

**问题**：G3 需要把 G1 的单节点与失败 snapshot 脚手架替换为一张可规划、可保存、可复现的尖塔式 Act 地图，同时保留 CD-112/113 已验证的 Store / Flow / Scene Scope 与原子单槽边界。若 View 保存可选节点、节点自己把 `Outgoing` 当作唯一合法性、进入节点时才抽 Encounter/Boss，或 Save Document 直接序列化整图和派生集合，都会形成第二事实源并使读档结果依赖调用顺序；若继续沿用 `RunBattleSnapshot`，普通战斗失败又会违背 Hermes 决策 016 的终局规则。

**地图与所有权选择**：创建 Run 时以固定 `ActMapProfile`、独立 map seed 和 generator version 一次生成整张分层 DAG，得到不可变 `MapDefinition`。稳定 `NodeId + Layer + Slot`、边、普通节点 `EncounterId`、本局 Boss 候选子集及每个终点 `BossId` 均属于该整图事实，开局冻结并可投影；构造边界防御性复制节点/边输入，规范化后的 profile/version/seed/nodes/edges 计算小写 SHA-256 `Fingerprint`。`RunStateStore` 是 Map/Run 可变事实的唯一写入所有者，只发布不可变 RunState，并拥有实际路径、当前/已提交节点、attempt、`MapReady / EncounterCommitted / InBattle / BossGateReached / Terminal` 等阶段；`RunFlowService` 只编排 Store、save port 和 SceneFlow，View 只投影与提交 `NodeId` 命令。

**可达性与投影选择**：普通移动的选择集合由纯可达性 module 从 MapDefinition、当前进度和移动模式计算，当前普通模式等价于当前节点直接出边；同一 module 预留 WingBoots 模式，只允许紧邻下一层任意已生成节点。本轮不实现遗物库存、次数、消耗或 UI。完整后继节点/边和可达 Boss 也由 DAG 纯计算，供当前可选节点 hover 高亮完整后半程并弱化会放弃的路线；这些集合、锁定色、hover 与布局均不进入 Store 或存档。Validator 除可达性外，还类型化拒绝 profile/version 漂移、重复 ID/Layer-Slot/边、非稳定 NodeId、错误内容引用、缺失端点、非相邻边和 Boss 出边。投影侧以只读 identity catalog 把冻结内容 ID 映射为名称与程序化锚点：5001 显示 `SLIME PATROL`、首敌本地化名和 Slime silhouette；5002 `SENTRY LINE` 只作为身份判别测试数据；9001/9002/9003 分别为 `BOSS ALPHA/BETA/GAMMA`，使用 Crown/Horns/Eye 三种不同锚点。同一 Boss 的多个终点保持相同身份；名称与锚点不进入 MapDefinition 或存档。

**recipe-only 存档选择**：当前写入 schema v2 在既有 Run/Hero/HP/Deck/random root 事实之外，地图与进度只保存 map seed、generator version、profile/config ID、Map fingerprint、实际 path、稳定 progress phase、可选 committed node 与 terminal reason；不保存 MapDefinition、节点/边副本、可选节点、可达 Boss、动画、hover、`BattleAttemptSequence` 或其他 UI/派生数据。恢复先按 profile/version/seed 重建整图，再运行 validator 并精确比较 fingerprint，之后才校验并恢复路径和阶段；profile、版本、配置引用、fingerprint 或 path 形状漂移均类型化失败。运行时下一 attempt 只由已完成 Combat path 与恢复 phase 推导，存档调用方不能注入第二份计数事实。旧 schema v1 没有 profile/version/map seed/fingerprint/path/Boss 身份，无法把历史单节点状态无歧义映射为本局冻结图，因此明确返回 UnsupportedSchema，不猜默认值、不补随机事实、不静默重掷。

**Boss 门与失败终局选择**：普通战斗胜利只在当前 attempt 的稳定 `BattleResult` 到达后完成已提交 Combat 节点、追加实际路径并回到 `MapReady`；过期结果不能结算新节点。选择 Boss 终点只追加该路径并形成可保存的 `BossGateReached`，不进入 Battle、不发奖励、不产生 RunOutcome。普通战斗失败不完成所选节点，Store 立即形成类型化 `Terminal(Defeat)`；Flow 以同一终局 document 通过既有原子单槽提交，成功后只显示不可 Continue 的失败页。终局提交会先耐久写入并回读校验 `terminal-intent recovery artifact`，再发布通用临时档/正式档；重试相同文档复用既有恢复物，不同文档被拒绝，损坏或非终局恢复物 fail-closed，不能退回旧 live save 提供 Continue。确认离开按 live → intent → temp 顺序删除，若 live 删除失败则保留恢复物；提交失败保持终局内存态并只能重试同一 document，不能回退旧 checkpoint。冷启动直接恢复终局失败页，只有玩家确认离开后才删除终局档。

**Supersede 与保留边界**：本决定只对 G3 Run-managed 流程 supersede CD-112 的 `RunBattleSnapshot / RestartBattle` 和 CD-113 的“当前写入 schema v1、Defeat 不写盘、失败页可同节点重开”口径；G1/G2 验收记录仍作为当时历史事实保留。CD-112 的 child-scope Result bridge、attempt 身份与唯一 Store/Flow 所有权，以及 CD-113 的 Bootstrap root、save port、同目录 temp、durable validation、Move/Replace 和失败不覆盖已提交事实继续有效。真实 Boss 战/Boss 阶段、奖励、Run 胜利、遗物实际效果、非战斗节点、多人/FishNet、云/多槽与战中存档均不在本决定内。

**实施状态与验收门**：当前为 `verified`。静态编译证据为生产 **0 errors / 6 warnings**、Editor **0 errors / 12 warnings**；Mono 定向 runner 为 map+store 25/25、save 21/21、atomic 19/19、flow 22/22、presenter 15/15，Unity View 为 13/13。首次完整 Unity 暴露的 i18n workbook 四格漂移与测试构造参数问题均已修正；最终交互式完整 EditMode job `8e910a98b14f4fe4b4901ba78bf060dc` 为 **993/993 passed**、0 failed、0 skipped、44.1991795 秒。`Sync and Build All` 与 Local Addressables 成功；Packed Play 已实走普通战斗多节点胜利到 `BossGateReached` 并进程级冷启动恢复，以及失败原子终局、冷启动失败页和确认删除两条生产链，各产品检查点 Console Error=0。完整证据见 `06_testing/2026-08-24-g3-deterministic-act-map.md`，方案见 `plans/2026-08-24-g3-deterministic-act-map.md`。
