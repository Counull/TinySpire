---
title: M9 代码结构与实现质量审查
owner: Daedalus
page_type: code_review
lifecycle: active
created: 2026-08-05
scope: M9 BattleScene 表现层、命令队列、回合控制、UI 视图、手牌交互
baseline: M9G 全量验证通过（423 EditMode、0 Console Error/Warning）
---

# M9 代码结构与实现质量审查

> 只读审查，未修改任何文件。审查范围：`TinySpire/Assets/Scripts/Battle/` 和 `TinySpire/Assets/Scripts/UI/Battle/` 中与 M9 相关的全部 C# 文件。

---

## 审查方法

每项发现包含：

- **优先级**：P1（阻碍后续扩展）/ P2（显著影响可维护性）/ P3（小改进）
- **证据**：具体文件路径、行级引用和代码模式
- **为什么是问题**：对 AC 约定、单一职责、依赖方向或生命周期正确性的违反
- **推荐调整**：最小可行修改方案
- **预计文件范围**：会被修改的文件列表
- **行为变化与兼容风险**：是否改变运行时行为，是否影响现有测试
- **最小迁移步骤**：可分步执行的实施顺序
- **所需回归测试**：验证修改正确性的最低测试集

分级：

- **A** — 可随当前 Bug 修复完成的局部重构
- **B** — 无行为变化但应单独提交的重构
- **C** — 需要独立计划或架构决策的大型重构

---

## A. 可随当前 Bug 修复完成的局部重构

### A1. `HandCardContainer.Start()` 初始化顺序脆弱

- **优先级**：P1
- **分级**：A

**证据**

```text
文件: HandCardContainer.cs, Start() 方法
```

当前 `async void Start()` 的执行顺序：

1. 校验 `cardViewPrefab` 和 DI 依赖 → 失败则 `enabled = false`
2. 解析 `_cardZones` 和 `_player`
3. 创建 `_cardPlayRules`
4. 准备 targeting arrow 为 ScreenOverlay
5. `await LoadCardIllustrationsAsync()` → 异常则 `ReleaseCardIllustrations()` 后 `return`（`enabled` 保持 `true`）
6. 若 `_isDestroyed` → `return`（不建立订阅）
7. **才建立所有 R3 订阅**（Layout、Strength、Locale、Turn、Queue、Lifecycle、Health × N）
8. `RebuildCards(immediate: true)`

问题：步骤 5 或 6 的 `return` 会导致容器保持 `enabled = true` 但没有建立任何 R3 订阅，后续手牌变化不会触发 UI 刷新，表现为"战斗开始后手牌无响应"。

另外：步骤 5 的 `catch` 块中 `ReleaseCardIllustrations()` 之后 `if (!_isDestroyed)` 才 `Debug.LogException`，但 `_isDestroyed` 分支没有释放插图句柄 —— 不过此时对象即将销毁，资源会随场景卸载释放，不是泄漏。

**为什么是问题**

- 违反 AC-003（未决细节必须登记依赖项 ID）：这里没有 TODO(DEP-xxx) 标记这个半初始化状态的后果。
- 插图加载是纯表现资源，不应阻塞核心交互链路（订阅 + 首帧渲染）的建立。
- `async void` 的异常会静默丢失（Unity 会 log，但调用方无法 catch），让问题难以排查。

**推荐调整**

将初始化拆为两阶段，保证核心链路不受插图加载影响：

```csharp
private async void Start()
{
    // 阶段 1：同步建立所有核心依赖和订阅（不依赖任何异步资源）
    if (!TrySetupCoreDependencies()) return;
    SetupSubscriptions();
    RebuildCards(immediate: true);

    // 阶段 2：异步加载插图（失败时用占位 Sprite 降级，不阻塞交互）
    await LoadCardIllustrationsWithFallbackAsync();
}

private bool TrySetupCoreDependencies()
{
    // 校验 cardViewPrefab 和 DI 依赖
    // 解析 _cardZones, _player, _cardPlayRules
    // 准备 targeting arrow
    // 返回 false 时设置 enabled = false
}

private void SetupSubscriptions()
{
    // 建立所有 R3 订阅
    // 始终执行，不受插图加载影响
}
```

**预计文件范围**

- `HandCardContainer.cs`

**行为变化与兼容风险**

- **行为变化**：插图加载失败时，卡牌将以无插图（或占位 Sprite）正常显示和交互，而非静默禁用整个手牌。这是更正确的降级行为。
- **测试影响**：现有测试中如果有依赖"插图加载失败导致容器 disabled"的用例需要更新；从 SESSION_LOG 来看，M9E/M9G 的测试都使用正式 Addressables 加载，不涉及该路径。
- **兼容风险**：低。不会改变正常路径（插图加载成功）的任何行为。

**最小迁移步骤**

1. 提取 `TrySetupCoreDependencies()` 方法，包含依赖校验和同步初始化。
2. 提取 `SetupSubscriptions()` 方法，包含所有 R3 订阅。
3. 修改 `LoadCardIllustrationsAsync()` 为 `LoadCardIllustrationsWithFallbackAsync()`，失败时创建白色占位 Sprite 而非 return。
4. 删除原 `Start()` 中的三个 `return` 分支（插图异常、_isDestroyed、插图加载后_isDestroyed）。

**所需回归测试**

- Bootstrap → BattleScene 正常进入，手牌 5 张完整显示并可交互。
- M9E 卡区运动测试（88 聚焦 + 166 回归）全部通过。
- M9D 目标选择测试（98）全部通过。
- （可选新增）模拟插图加载失败的 EditMode 测试，确认容器仍建立订阅并可重建手牌。

---

### A2. `HandCardVisual` 的 Tween 生命周期管理分散

- **优先级**：P2
- **分级**：A

**证据**

```text
文件: HandCardVisual.cs
```

当前 `HandCardVisual` 手工管理四类互不重叠的 Tween：

| 字段 | 用途 | Kill 方式 |
|---|---|---|
| `_activeTween` | 姿态/Hover/BasePose 补间 | `KillActiveTween()` → `_activeTween.Kill()` |
| `_feedbackTween` | 打出反馈 | `KillFeedbackTween()` → `_feedbackTween.Kill()` |
| `_targetFocusTransitionTween` | 聚焦位移动画 | `CancelTargetFocus()` → `DOTween.Kill(id)` |
| `_targetFocusBreathTween` | 聚焦呼吸动画 | `CancelTargetFocus()` → `DOTween.Kill(id)` |

每个调用点都需要知道"此刻该 Kill 哪个"：

- `SetBasePoseImmediately()`: `CancelTargetFocus()` + `KillActiveTween()`
- `PlayBasePose()`: `CancelTargetFocus()` + `KillActiveTween()`
- `PlayHover()`: `CancelTargetFocus()` + `KillActiveTween()`
- `BeginDrag()`: `CancelTargetFocus()` + `KillActiveTween()` + `SetDragPlayFeedback(false)`
- `PrepareAsTransient()`: `CancelTargetFocus()` + `KillActiveTween()` + `KillFeedbackTween()`

**为什么是问题**

- 新增第五类 Tween 时需要修改 5+ 个调用点。
- Kill 遗漏会导致两个 Tween 同时修改同一 Transform（DOTween 会报冲突警告）。
- `_targetFocusTransitionTween` 和 `_targetFocusBreathTween` 使用 `object` ID 管理（而非 `_activeTween` 的直接引用），方式不一致。

**推荐调整**

提取内部 `CardTweenScope` 结构，统一拥有该卡的所有 Tween：

```csharp
private struct CardTweenScope
{
    private Tween _pose;
    private Tween _feedback;
    private Tween _focusTransition;
    private Tween _focusBreath;

    public void ReplacePose(Tween tween) { _pose?.Kill(); _pose = tween; }
    public void ReplaceFeedback(Tween tween) { _feedback?.Kill(); _feedback = tween; }
    public void ReplaceFocus(Tween transition, Tween breath) { ... }
    public void KillAll() { _pose?.Kill(); _feedback?.Kill(); ... }
    public void KillFocus() { ... }
}
```

所有调用点只需 `_tweens.KillFocus()` 或 `_tweens.ReplacePose(newTween)`，不再关心内部有哪些子类。

**预计文件范围**

- `HandCardVisual.cs`（新增嵌套 `CardTweenScope`，替换四个 Tween 字段和相关 Kill 方法）

**行为变化与兼容风险**

- **行为变化**：无。纯粹的内部重构。
- **测试影响**：M9C 红灯测试（Runner 12/12 Tween 清理）依赖 `HandCardVisual` 对外暴露的 Tween 清理时机，需要在重构后重新确认 `active=0 / playing=0`。
- **兼容风险**：极低。`CardTweenScope` 是 `private` 内部类型。

**最小迁移步骤**

1. 新增 `private CardTweenScope _tweens` 字段。
2. 逐一替换四个 Tween 字段的读写为 `_tweens.Replace*/Kill*`。
3. 删除旧的 `KillActiveTween()`、`KillFeedbackTween()`、`CancelTargetFocus()` 方法体，改为委托给 `_tweens`。
4. 运行 M9C 聚焦测试（96）确认 Tween 清理行为不变。

**所需回归测试**

- M9C 聚焦测试（96 passed）
- M9D 目标聚焦测试（98 passed）
- M9E 卡区运动测试（88 passed）

---

## B. 无行为变化但应单独提交的重构

### B1. `BattleCommandPresentationAdapter` 构造函数过多 + 工厂创建耦合在内部

- **优先级**：P1
- **分级**：B

**证据**

```text
文件: BattleCommandPresentationAdapter.cs
```

当前有 **7 个构造函数**（1 个 `[Inject]` 生产构造 + 6 个 `internal` 测试构造），以及 5 个 `private static` 工厂创建方法：

```
CreateCombatFeedbackFactory()
CreateFlowFeedbackFactory()
CreateProductionFlowFeedbackFactory()
CreateHandCardContainerProvider()
ConfigureProductionFlowFeedbackView()
```

生产构造路径：

1. `[Inject]` 构造 → 调用 6 参数私有构造
2. 私有构造内部调用 `CreateCombatFeedbackFactory(participantPresenter)` 创建战斗反馈工厂
3. 私有构造内部调用 `CreateProductionFlowFeedbackFactory(resolver)` 创建流程反馈工厂
4. `CreateProductionFlowFeedbackFactory` 内部又通过 `IObjectResolver` 延迟解析 `BattleTurnHudView`、`LocalizationService`、`SceneFlowService`、`GameStartupOptions`
5. 私有构造内部又通过条件判断 `hasAllCardMotionViews` 决定是否创建 `BattleCardMotionTweenFactory`
6. `BattleCardMotionTweenFactory` 的构造函数接收 `CreateCardMotionTween` 回调，该回调是 adapter 的私有方法

**为什么是问题**

- 违反 AC-002（不做投机性抽象）的精神：这里不是"太多抽象"，而是"没有在正确的边界抽象"。三个工厂的创建逻辑全部 inline 在 adapter 构造里。
- 每次新增测试场景（如 M9B 只需要 participant、M9E 需要 hand + pile、M9F 只需要 flow feedback）就要加一个新的 `internal` 构造函数。
- `CreateProductionFlowFeedbackFactory` 中的 `configuredView` 闭包缓存是防御性代码：它假设 `IObjectResolver.Resolve<BattleTurnHudView>()` 可能在不同调用中返回不同实例（实际上 Singleton 不会），增加了不必要的认知负担。
- `_completeZeroDurationFallbackOnTick` 标志直接改变 `Tick()` 行为，是 fragile base class 的味道。

**推荐调整**

将三个子工厂的创建从 adapter 移到 `BattleLifetimeScope.Configure()`：

```csharp
// BattleLifetimeScope 中
builder.Register<BattleCombatFeedbackTweenFactory>(Lifetime.Singleton);
builder.Register<BattleFlowFeedbackTweenFactory>(Lifetime.Singleton);
builder.Register<BattleCardMotionTweenFactory>(Lifetime.Singleton);
```

adapter 只接收已创建的工厂：

```csharp
[Inject]
public BattleCommandPresentationAdapter(
    BattleCombatFeedbackTweenFactory combatFactory,
    BattleFlowFeedbackTweenFactory flowFactory,
    BattleCardMotionTweenFactory motionFactory,
    /* 其他依赖 */)
```

测试用 adapter 通过构造注入测试替身工厂（fake/mock），不再需要多个 `internal` 构造函数。

**预计文件范围**

- `BattleCommandPresentationAdapter.cs`（删除 5 个 `internal` 构造和 5 个 `static` 工厂方法，只保留 1 个 `[Inject]` 构造和 1 个测试用公开构造）
- `BattleLifetimeScope.cs`（新增三个工厂的注册）
- `BattleCombatFeedbackTweenFactory.cs`（可能需要微调使其可 DI 构造）
- `BattleFlowFeedbackTweenFactory.cs`（同上）
- `BattleCardMotionTweenFactory.cs`（同上）
- 测试文件（将 `new BattleCommandPresentationAdapter(participant, deltaTime)` 改为注入 fake 工厂）

**行为变化与兼容风险**

- **行为变化**：无。DI 注册的生命周期（Singleton）与当前"adapter 持有工厂实例"完全等价。
- **测试影响**：需要修改测试中 adapter 的构造方式，从"选一个 internal 构造"变为"注入测试替身"。
- **兼容风险**：中等。涉及 DI 注册变更和测试重写。

**最小迁移步骤**

1. 为三个工厂创建可 DI 的构造函数（接收 `Func<>` 回调）。
2. 在 `BattleLifetimeScope` 中注册。
3. 修改 adapter 的 `[Inject]` 构造接收工厂而非自行创建。
4. 保留 1 个测试用公开构造（接收工厂 + `Func<float>`），删除其余 5 个 `internal` 构造。
5. 逐一迁移测试。

**所需回归测试**

- M9G 全量 EditMode（423 passed）
- Bootstrap → BattleScene 生产链，Console Error/Warning 0/0

---

### B2. 三个 Tween Factory 的 `TryCreate` 接口不一致

- **优先级**：P2
- **分级**：B

**证据**

```text
文件:
- BattleCombatFeedbackTweenFactory.cs  →  TryCreate(step, out tween)                          // 只有 step
- BattleCardMotionTweenFactory.cs      →  TryCreate(prelude, out tween) + TryCreate(step, out tween)  // prelude + step
- BattleFlowFeedbackTweenFactory.cs    →  TryCreate(prelude, out tween) + TryCreate(step, out tween)  // prelude + step
```

在 `BattleCommandPresentationAdapter` 中的消费方式：

```csharp
// CreatePreludeCueTween
if (_flowFeedbackFactory != null && _flowFeedbackFactory.TryCreate(prelude, out tween))
    return tween;
if (_cardMotionFactory != null && _cardMotionFactory.TryCreate(prelude, out tween))
    return tween;
return CreateCueTween(); // fallback

// CreateSettlementCueTween
if (_flowFeedbackFactory != null && _flowFeedbackFactory.TryCreate(step, out tween))
    return tween;
if (_combatFeedbackFactory != null && _combatFeedbackFactory.TryCreate(step, out tween))
    return tween;
if (_cardMotionFactory != null && _cardMotionFactory.TryCreate(step, out tween))
    return tween;
return CreateCueTween(); // fallback
```

**为什么是问题**

- `BattleCombatFeedbackTweenFactory` 没有 prelude 入口（因为战斗反馈只来自 settlement），但 adapter 仍然需要知道"哪些工厂响应 prelude、哪些响应 step"。
- 新增第四类反馈（例如 G5 遗物触发表现）时，需要在 adapter 的两个方法中各加一行，容易遗漏。
- 当前每个工厂的 null 检查是手动做的（`_flowFeedbackFactory != null &&`），而不是统一遍历。

**推荐调整**

定义一个统一接口，让 adapter 以列表遍历：

```csharp
internal interface IBattlePresentationCueFactory
{
    bool TryCreatePrelude(BattleCommandPrelude prelude, out BattleCommandPresentationTween tween);
    bool TryCreateStep(BattleCommandPresentationStep step, out BattleCommandPresentationTween tween);
}
```

三个工厂实现该接口（`BattleCombatFeedbackTweenFactory.TryCreatePrelude` 始终返回 `false`）。Adapter 持有 `IReadOnlyList<IBattlePresentationCueFactory>` 并按序遍历。

**预计文件范围**

- 新增 `IBattlePresentationCueFactory.cs`（或放在现有 `IBattleCommandPresentation.cs` 同文件）
- `BattleCombatFeedbackTweenFactory.cs`（实现接口）
- `BattleCardMotionTweenFactory.cs`（实现接口）
- `BattleFlowFeedbackTweenFactory.cs`（实现接口）
- `BattleCommandPresentationAdapter.cs`（用列表遍历替代硬编码链）

**行为变化与兼容风险**

- **行为变化**：无。遍历顺序保持当前优先级（flow → combat/motion 按注册顺序）。
- **测试影响**：无。接口提取不改变任何测试可见行为。
- **兼容风险**：极低。纯内部重构。

**最小迁移步骤**

1. 定义 `IBattlePresentationCueFactory` 接口。
2. 三个工厂各自实现。
3. Adapter 构造函数接收 `IReadOnlyList<IBattlePresentationCueFactory>`。
4. 删除 adapter 中的三个具体工厂字段和硬编码 `TryCreate` 链。

**所需回归测试**

- M9G 全量 EditMode（423 passed）

---

### B3. 动画时长常量分散在 4 个文件中

- **优先级**：P3
- **分级**：B

**证据**

```text
BattleCommandPresentationAdapter.cs:
  PlayCardPreludeDurationSeconds = 0.18f
  CardZoneMotionDurationSeconds = 0.22f
  ReshuffleMotionDurationSeconds = 0.32f

BattleTurnHudView.cs:
  BattleStartFadeDurationSeconds = 0.12f
  BattleStartHoldDurationSeconds = 0.36f
  TurnBannerFadeDurationSeconds = 0.1f
  TurnBannerHoldDurationSeconds = 0.3f
  BattleOutcomeRevealDurationSeconds = 0.22f

BattleFloatingNumberView.cs:
  _durationSeconds = 0.45f（SerializeField）
  _riseDistance = 48f（SerializeField）

HandCardContainer.cs:
  hoverDuration = 0.15f（SerializeField）
  reflowDuration = 0.22f（SerializeField）
  _targetFocusDuration = 0.2f（SerializeField）
```

部分值已经是 `[SerializeField]`（可以在 Inspector 调整），部分是硬编码 `const`。

**为什么是问题**

- M10 对标阶段必然需要统一调参（如"所有卡牌运动加速 1.5×"），当前需要修改 4 个文件。
- `CardZoneMotionDurationSeconds` 和 `reflowDuration` 都是 0.22f（巧合？还是故意对齐？），分散后容易漂移。
- `BattleFloatingNumberView._durationSeconds` 是 `SerializeField` 但 `_riseDistance` 也是——两个值共同决定视觉节奏，应该有一个统一配置入口。

**推荐调整**

在当前架构约定下，M10 对标是集中这些值的正确时机。建议方案：

1. 新增 `BattlePresentationTimingConfig` ScriptableObject，包含分类的时长字段（`CardMotion`、`CombatFeedback`、`FlowFeedback`）。
2. 在 `BattleLifetimeScope` 中注册为 Singleton。
3. 各 View/Factory 通过 DI 接收配置，替代硬编码常量。

**预计文件范围**

- 新增 `BattlePresentationTimingConfig.cs` + `.asset`
- `BattleCommandPresentationAdapter.cs`（接收配置替代常量）
- `BattleTurnHudView.cs`（接收配置替代常量）
- `BattleFloatingNumberView.cs`（接收配置替代 SerializeField）
- `HandCardContainer.cs`（接收配置替代 SerializeField）
- `BattleLifetimeScope.cs`（注册配置）

**行为变化与兼容风险**

- **行为变化**：如果 ScriptableObject 的默认值与当前硬编码一致，则无行为变化。
- **测试影响**：测试中可能需要创建 `BattlePresentationTimingConfig` 实例（或使用 `ScriptableObject.CreateInstance`）。
- **兼容风险**：低。但改变了多个 View 的构造方式，建议作为独立 PR。

**最小迁移步骤**

1. 创建 `BattlePresentationTimingConfig` 和默认 `.asset`。
2. 在 `BattleLifetimeScope` 中注册。
3. 逐文件替换硬编码为配置引用。
4. 删除旧常量。

**所需回归测试**

- M9 全部聚焦测试（M9A 83 + M9B 17 + M9C 96 + M9D 98 + M9E 88 + M9F 111 + M9G 423）

---

### B4. `Update()` / `LateUpdate()` 每帧无效空检查

- **优先级**：P3
- **分级**：B

**证据**

```text
文件: HandCardContainer.cs
```

```csharp
private void Update()
{
    if (_cardPlayRules == null) return;  // 行 ~340
    bool presentationReady = IsParticipantPresentationReady();
    if (presentationReady == _lastParticipantPresentationReady) return;
    // ...
}

private void LateUpdate()
{
    if (_dragPhase != HandCardDragPhase.EnemyTargeting  // 行 ~355
        || _draggingCard == null
        || !_hasPointerScreenPosition) return;
    // ...
}
```

- `_cardPlayRules` 在 `Start()` 的同步校验阶段被赋值，之后永远不为 null。但在 `Update()` 中每帧检查。
- `LateUpdate()` 中的三个条件在 99% 的帧中都是 false（只有拖动瞄准敌人时才是 true）。

**为什么是问题**

- 每帧空检查虽然开销可忽略（一条 null 比较），但暗示了"初始化可能失败"的不确定性——实际上 `_cardPlayRules` 为 null 时 `enabled` 已经被设为 false，`Update()` 根本不会调用。
- 对阅读者来说，`if (_cardPlayRules == null) return;` 让人以为这是一个"可能尚未初始化"的防御，增加认知负担。

**推荐调整**

```csharp
private bool _isCoreReady;

// Start() 末尾
_isCoreReady = true;

private void Update()
{
    if (!_isCoreReady) return;
    // ...
}
```

或者更简单地：因为 `_cardPlayRules` 在 `Start()` 同步阶段赋值后永远非 null，直接删除 `Update()` 中的 null 检查。

如果 `_isDestroyed` 也是 `Update` 需要提前退出的条件，应该用一个统一的早期返回：

```csharp
private void Update()
{
    if (_isDestroyed || _cardPlayRules == null) return;
    // ...
}
```

**预计文件范围**

- `HandCardContainer.cs`

**行为变化与兼容风险**

- **行为变化**：无。
- **测试影响**：无。
- **兼容风险**：无。

**最小迁移步骤**

1. 将 `Update()` 中的 `_cardPlayRules == null` 替换为 `_isDestroyed || _cardPlayRules == null`。
2. （可选）在 `Start()` 成功路径末尾设置 `_isCoreReady = true`。

**所需回归测试**

- M9D 目标选择测试（98 passed）
- M9E 卡区运动测试（88 passed）

---

### B5. `BattleCommandQueue.Execute()` 命令类型分支过长

- **优先级**：P3
- **分级**：B

**证据**

```text
文件: BattleCommandQueue.cs, Execute() 方法（约 140 行）
```

当前 `Execute()` 方法内有一长串 `if-else`：

```csharp
if (entry.Command is StartBattleCommand)
    operationResult = _turnController.TryStartBattle();
else if ((entry.Command is PlayCardCommand || entry.Command is EndPlayerActionCommand) &&
         entry.SubmittedRoundNumber != turnBefore.RoundNumber)
    operationResult = BattleTurnOperationResult.Failed(...);
else if (entry.Command is PlayCardCommand playCardCommand)
    operationResult = _turnController.TryPlayCard(playCardCommand);
else if (entry.Command is EndPlayerActionCommand endPlayerActionCommand)
    operationResult = _turnController.TryEndPlayerAction(endPlayerActionCommand);
else if (entry.Command is CompleteEnemyActionCommand completeEnemyActionCommand)
{
    // ~60 行嵌套逻辑：Validate → enemyExecutor.Execute → 合并 settlement → AdvanceAfterValidatedEnemyAction → 冷冻 continuation
}
else
    operationResult = BattleTurnOperationResult.Failed(UnsupportedCommand);
```

`CompleteEnemyActionCommand` 分支内部有 ~60 行嵌套逻辑，包括 enemy executor 调用、terminal outcome 评估、settlement 合并和 continuation 冷冻。

**为什么是问题**

- 当前只有 4 种命令类型，分支可管理。但 ROADMAP G5（遗物触发时机）会引入新命令类型，分支将继续膨胀。
- `CompleteEnemyActionCommand` 分支的逻辑复杂度远高于其他分支，却没有提取为独立方法。
- 新增命令类型时需要修改 `Execute()` 内的 if-else 链和 `CreateTurnContinuation()`，两个位置可能不一致。

**推荐调整**

**当前阶段（M9→M10）不需要大改**。只需将 `CompleteEnemyActionCommand` 分支提取为 `ExecuteCompleteEnemyAction()` 私有方法。未来 G5 引入 ≥3 种新命令类型时再考虑策略模式。

**预计文件范围**

- `BattleCommandQueue.cs`

**行为变化与兼容风险**

- **行为变化**：无（纯提取方法）。
- **测试影响**：无。
- **兼容风险**：无。

**最小迁移步骤**

1. 提取 `ExecuteCompleteEnemyAction(entry, turnBefore)` 方法。
2. 在 `Execute()` 中调用。

**所需回归测试**

- M8D 敌人循环测试（11 + 12 passed）
- M9G 全量 EditMode（423 passed）

---

### B6. `BattleTurnHudView` 职责积累

- **优先级**：P2
- **分级**：B

**证据**

```text
文件: BattleTurnHudView.cs
```

当前 View 包含：

| 职责 | 对应 UI 元素 |
|---|---|
| 能量/轮次/阶段文本 | `_energyFill`, `_energyText`, `_roundText`, `_phaseText` |
| 命令状态文本 | `_commandStatusText` |
| 结束行动按钮 | `_endActionButton` |
| 战斗开始覆盖层 | `_battleStartOverlay`, `_battleStartText` |
| 回合横幅 | `_turnBannerGroup`, `_turnBannerText`, `_playerTurnBanner` |
| 胜负面板 | `_battleOutcomePanel`, `_battleOutcomeText` |
| 重开/退出按钮 | `_restartButton`, `_exitButton` |
| 流程反馈 Tween 创建 | `CreateBattleStartOverlayTween`, `CreateTurnBannerTween`, `CreateBattleOutcomeTween` |
| 重开/退出回调 | `_restartBattle` (Func\<UniTask\>), `_quitApplication` (Action) |

这些都是通过 `ConfigureFlowFeedback()` 方法配置的，该方法在 `BattleCommandPresentationAdapter` 的私有构造路径中被调用。

**为什么是问题**

- M9A 用横幅、M9F 用胜负面板时，这些功能是逐步叠加到同一个 View 上的。G1（主菜单）会让重开/退出逻辑变得更复杂（"退出到主菜单" vs "退出应用"）。
- Tween 创建逻辑（`CreateBattleStartOverlayTween` 等）混在 View 中，使 View 同时承担"持有 UI 引用"和"编排动画"两个职责。
- `ConfigureFlowFeedback` 不是通过 DI 注入，而是由 adapter 主动调用的配置方法，这违反了"View 只消费、不持有流程知识"的原则。

**推荐调整**

当前阶段建议保守拆分：只把"胜负面板 + 重开/退出"提取为独立的 `BattleOutcomeView`：

```text
BattleTurnHudView — 保留：能量、轮次、阶段、命令状态、结束行动、横幅、战斗开始覆盖层
BattleOutcomeView — 新增：胜负文本、重开按钮、退出按钮
```

两个 View 各自持有自己的 Tween 创建逻辑。G1 引入主菜单时，`BattleOutcomeView` 的退出行为可以替换为"返回主菜单"而不影响 `BattleTurnHudView`。

**预计文件范围**

- 新增 `BattleOutcomeView.cs` + `.prefab`
- `BattleTurnHudView.cs`（删除胜负面板相关代码）
- `BattleCommandPresentationAdapter.cs`（`ConfigureProductionFlowFeedbackView` 改为配置两个 View）
- `BattleScene.unity`（添加 `BattleOutcomeView` prefab）

**行为变化与兼容风险**

- **行为变化**：无。只是拆分 prefab 层级。
- **测试影响**：M9F/M9G 的 Bootstrap 测试需要确认新 prefab 层级不影响 Addressables 构建。
- **兼容风险**：低。但涉及 prefab 变更，需要重建 Addressables Local Content。

**最小迁移步骤**

1. 创建 `BattleOutcomeView` prefab 和脚本。
2. 从 `BattleTurnHudView` 中移除胜负面板代码和序列化引用。
3. 在 `BattleLifetimeScope` 中注册 `BattleOutcomeView`。
4. 修改 adapter 的 `ConfigureProductionFlowFeedbackView` 为同时配置两个 View。
5. 在 `BattleScene.unity` 中挂载新 prefab。
6. 重建 Addressables Local Content。

**所需回归测试**

- Bootstrap → BattleScene 正常进入，胜利/失败面板正确显示
- M9F 终局测试（111 passed）
- M9G 全量验证（423 passed）

---

## C. 需要独立计划或架构决策的大型重构

### C1. `HandCardContainer` 拆分 — 当前 God Object

- **优先级**：P1
- **分级**：C

**证据**

```text
文件: HandCardContainer.cs（当前约 850 行）
```

`HandCardContainer` 当前承担的全部职责：

| 职责 | 相关代码区域 | 行数估计 |
|---|---|---|
| 卡牌实例化/销毁生命周期 | `RebuildCards()`, `DestroyAllCards()`, transient 管理 | ~150 |
| 扇形布局计算 | `LayoutCards()`, `CalculateFanPose()` | ~60 |
| 悬停/拖拽交互状态机 | `HandlePointerEnter/Exit`, `HandleBeginDrag/Drag/EndDrag` | ~200 |
| 越线出牌判定 | `IsAbovePlayLine()`, `SubmitPlayCard()` | ~50 |
| 箭头瞄准 + 目标聚焦 | `UpdateEnemyTargeting()`, `PlayTargetFocus()` | ~80 |
| 瞬时卡牌运动 | `CreateTransientCardMotionTween()`, `CreateIncomingCardMotionTween()`, `TryFastForwardIncomingCardMotion()` | ~100 |
| 卡牌插图 Addressables 加载 | `LoadCardIllustrationsAsync()`, `ReleaseCardIllustrations()` | ~60 |
| R3 订阅管理 | `Start()` 中的 7 条 Subscribe + Health × N | ~40 |
| Update/LateUpdate 轮询 | `Update()`, `LateUpdate()` | ~30 |
| 可交互性即时派生 | `CanInteractWithCard()`, `RefreshCardPlayPresentation()`, `IsCardMotionReady` | ~50 |

**为什么是问题**

- 违反 AC-001（最小状态聚合）：容器自己持有 `_cards`、`_transientCards`、`_pendingPlayCards`、`_draggingCard`、`_dragPhase`、`_lastPointerScreenPosition` 等多个可变集合和状态字段。这些字段之间有时序依赖（如 `_dragPhase` 决定 `_draggingCard` 的解释方式），但没有显式的状态机保护。
- 任何改动（如新增一个卡牌反馈步骤）都需要理解和修改这个 850 行的类。
- G1（Run 生命周期）引入后，`HandCardContainer` 的"当前唯一玩家"假设会松动（ROADMAP 明确提到 `DEP-008` 多人玩家），届时这个类需要同时处理多套卡区。

**推荐调整**

拆分为 4 个独立组件，由容器协调：

```text
HandCardLayoutManager
  职责：扇形排布计算、重排补间、基础姿态
  输入：Hand 有序列表（CardZoneLayoutData.Hand）
  输出：每张卡的 HandCardPose
  生命周期：随 HandCardContainer

HandCardDragController
  职责：拖拽状态机（Idle → Dragging → EnemyTargeting → 提交/取消）
  输入：PointerEventData、HandCardVisual、合法目标列表
  输出：PlayCardCommand?（提交时）或 null（取消时）
  生命周期：随 HandCardContainer

HandCardTargetingController
  职责：箭头管理、目标聚焦、合法目标高亮
  输入：拖拽卡牌、指针位置、BattleParticipantPresenter
  输出：CombatantId?（命中时）
  生命周期：随 HandCardContainer

HandCardMotionController
  职责：transient 卡创建/运动/清理、入场牌 fast-forward
  输入：BattleCardMotionCue、pile screen anchors
  输出：BattleCommandPresentationTween
  生命周期：随 HandCardContainer
```

容器只保留：
- R3 订阅协调
- 4 个组件的装配和生命周期
- `IsCardMotionReady`（需要跨组件查询）

**预计文件范围**

- 新增 4 个文件：`HandCardLayoutManager.cs`、`HandCardDragController.cs`、`HandCardTargetingController.cs`、`HandCardMotionController.cs`
- 修改 `HandCardContainer.cs`（从 ~850 行缩减到 ~200 行）
- 修改 `BattleCommandPresentationAdapter.cs`（`CreateCardMotionTween` 改为调用 `HandCardMotionController` 而非 `HandCardContainer`）
- 测试文件（拆分测试以匹配组件边界）

**行为变化与兼容风险**

- **行为变化**：无（纯重构）。
- **测试影响**：大量测试需要重写。当前 HandCardContainer 的测试直接访问其内部方法（如 `HandleBeginDrag`），拆分后这些方法移到子组件中。
- **兼容风险**：高。需要仔细迁移每一段逻辑，确保拖拽状态机、运动租约和 transient 生命周期保持一致。

**最小迁移步骤**

1. 提取 `HandCardLayoutManager`（纯计算，最容易独立）。
2. 提取 `HandCardMotionController`（接口已通过 `BattleCommandPresentationAdapter` 间接定义）。
3. 提取 `HandCardTargetingController`。
4. 提取 `HandCardDragController`（最复杂，依赖前三个）。
5. 清理 `HandCardContainer` 为纯协调器。

每一步独立提交并通过对应测试。

**所需回归测试**

- M6D 目标选择全量（53 passed）
- M9C 结算反馈（96 passed）
- M9D 目标聚焦（98 passed）
- M9E 卡区运动（88 passed）
- M9G 全量（423 passed）

---

### C2. 呈现层工厂 DI 化

- **优先级**：P2
- **分级**：C

**证据**

与 B1 相关但范围更大。当前呈现层的数据流：

```text
BattleCommandQueue.Execute()
  → BattleCommandPresentationAdapter.Present(result, completion)
    → BattleCommandPresentationPlan.Create(result)
    → _runner.Play(plan, completion)
      → CreatePreludeCueTween(prelude)   // 硬编码工厂链
      → CreateSettlementCueTween(step)   // 硬编码工厂链
        → _combatFeedbackFactory.TryCreate(step)
        → _cardMotionFactory.TryCreate(step)
        → _flowFeedbackFactory.TryCreate(step)
        → CreateCueTween()  // 测试占位 fallback
```

问题在于：
1. 工厂链的遍历顺序是硬编码的。
2. 工厂的创建（含延迟解析）全部在 adapter 内部。
3. 产品的 `CreateProductionFlowFeedbackFactory` 中通过 `IObjectResolver` 直接解析 `BattleTurnHudView`、`LocalizationService` 和 `SceneFlowService`，绕过了 VContainer 的依赖图。

**推荐调整**

结合 B1 和 B2：

1. 三个工厂实现 `IBattlePresentationCueFactory`。
2. 工厂通过 DI 接收它们需要的 View/Service（不再通过 `IObjectResolver` 延迟解析）。
3. Adapter 通过 DI 接收 `IEnumerable<IBattlePresentationCueFactory>` 并按注册顺序遍历。

**预计文件范围**

- 新增 `IBattlePresentationCueFactory.cs`
- `BattleCombatFeedbackTweenFactory.cs`、`BattleCardMotionTweenFactory.cs`、`BattleFlowFeedbackTweenFactory.cs`
- `BattleCommandPresentationAdapter.cs`
- `BattleLifetimeScope.cs`

**行为变化与兼容风险**

- **行为变化**：无。
- **测试影响**：测试需要构造工厂实例并注入 adapter。
- **兼容风险**：中等。涉及 DI 注册顺序（决定了工厂遍历优先级）。

**最小迁移步骤**

1. B1 + B2 的迁移步骤合并执行。
2. 确认 DI 注册顺序与当前硬编码链的优先级一致。

**所需回归测试**

- M9G 全量 EditMode（423 passed）
- Bootstrap → BattleScene 生产链

---

## 架构亮点（不予修改）

以下是设计中值得保留的部分，不建议重构：

### 1. `BattleCommandPresentationRunner` 的 ManualUpdate + DOTween 模式

手动推进不受 `Time.timeScale` 影响，且 `CompleteImmediately` 可以安全跳帧。`SetSpeed()` 允许 M10 实现动画加速/跳过而不改变 cue 顺序。

### 2. `PresentationCompletion` 的 arm/complete 边界

`Present()` 成功返回前同步 completion 被缓存，返回后才 arm，抛错时 cancel。精确保护了 fault 诊断现场，避免了"表现层抛错 → completion 仍触发 → 队列推进到下一个命令"的级联故障。

### 3. `BattleCommandPresentationPlan.Create()` 的 settlement Order 连续性校验

拒绝在 UI 层重排 settlement，拒绝 `BattleEnded` 不出现在末尾，把排序权威完全锁在 Queue 层。这是正确的"表现层只消费、不重排"设计。

### 4. `BattleFlowFeedbackTweenFactory` 的 `outcomeLocalizationKeyProvider` (Func) 延迟求值

终局文案只在 `BattleOutcome` 步骤实际播放时才调用 `BattleTerminalRules.Evaluate()`。这避免了 Plan 创建时冻结可能过时的终局结果，也避免了在 Plan 中保存 outcome 镜像。

### 5. `BattleCardZonesData` 的四区互斥 + `CardZoneLayoutData` 原子发布

UI 订阅 `Layout`（`ReactiveProperty<CardZoneLayoutData>`）一次拿到完整手牌快照，不会看到半次移动。这是 AC-004 的正确落实。

---

## 发现汇总

| ID | 分级 | 优先级 | 标题 |
|---|---|---|---|
| A1 | A | P1 | `HandCardContainer.Start()` 初始化顺序脆弱 |
| A2 | A | P2 | `HandCardVisual` Tween 生命周期分散 |
| B1 | B | P1 | `BattleCommandPresentationAdapter` 构造函数过多 + 工厂耦合 |
| B2 | B | P2 | 三个 Tween Factory TryCreate 接口不一致 |
| B3 | B | P3 | 动画时长常量分散 |
| B4 | B | P3 | `Update()/LateUpdate()` 每帧无效空检查 |
| B5 | B | P3 | `BattleCommandQueue.Execute()` 命令分支过长 |
| B6 | B | P2 | `BattleTurnHudView` 职责积累 |
| C1 | C | P1 | `HandCardContainer` 拆分（God Object） |
| C2 | C | P2 | 呈现层工厂 DI 化 |

## 建议处理顺序

```text
Phase 1（M10 对标前）:
  A1 → A2 → B4 → B5

Phase 2（M10 对标中或独立 PR）:
  B1 → B2 → B3 → B6

Phase 3（G1 Run 生命周期时）:
  C1 → C2
```

C1/C2 建议在 G1（主菜单 + Run 生命周期）开始前完成，因为 `HandCardContainer` 的"当前唯一玩家"假设会在 G1 中被 `DEP-008` 打破，拆分是必要的前置工作。
