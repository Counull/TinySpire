---
created: 2026-07-06
updated: 2026-07-30
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
