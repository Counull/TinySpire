---
title: BattleScene 手牌 UI（杀戮尖塔式扇形/悬停/拖拽视觉）
page_type: plan
lifecycle: proposal
date: 2026-07-29
scope: BattleScene MVP · UI
source: 与用户的 grilling 交互式确认（本会话），基于 2026-07-12 手牌 UI 计划演进
status_source: ../SESSION_LOG.md
supersedes: 2026-07-12-battlescene-card-ui.md 中的交互模型部分（CD-002）
---

# BattleScene 手牌 UI（杀戮尖塔式）

## 目标

在不实现出牌判定 / 目标选择 / 卡牌数据运行时业务逻辑的前提下，把现有静态手牌 UI（CD-002）升级为杀戮尖塔式的表现层：扇形排布 + 悬停抬起 + 拖拽跟手视觉。**本轮只做 UI 表现，不做出牌判定。**

## 影响层

- 计算层：不影响。
- 状态层：不影响；手牌数量来自临时占位字段，不接入真实业务状态。
- 时序层：新增 DOTween 驱动的补位 / 悬停过渡动画。
- UI 层：替换现有 `Toggle` + `ToggleGroup` 单选交互，改为扇形布局 + hover + 拖拽跟手表现。

## 前置事实

- `TinySpire/Assets/Arts/Runtime/Card/Prefab/CardView.prefab` 已存在，每张卡自带独立 `Canvas`（`m_SortingOrder` 可直接设置）+ `CanvasScaler`，本轮继续复用、不修改预制体本体。
- DOTween（含 DOTweenPro）已由用户导入到 `TinySpire/Assets/Plugins/Demigiant/`，本轮可直接使用，无需再评估依赖引入。
- 现有 `BattleScene` 手牌实现见 CD-002：固定 5 张卡，`Toggle`+`ToggleGroup` 单选高亮，无拖拽、无扇形、无动态数量。

## 设计决策（本轮已与用户逐条确认）

1. **交互模型**：悬停抬起 + 扇形排布 + 拖拽跟手视觉；不做出牌判定/合法目标判断（留给后续切片）。
2. **手牌数量**：布局算法写成接受任意张数的纯函数（输入总张数 → 输出每张卡的位置/旋转/层级），用 mock 验证 3~10 张，为后续接入真实牌库做准备。
3. **扇形曲线参数化**：对每张卡的归一化位置 `t ∈ [-1, 1]`（0 为正中）：
   - 水平偏移 `x`：等间距（受第 4 条压缩策略影响）
   - 旋转角 `= t × 最大边缘角度`（可调参数，建议默认 15°）
   - 纵向下沉 `y = -k × t²`（`k` 可调参数，形成下垂弧线）
   - 三个常量（基础间距、最大角度、`k`）做成可调字段，不硬编码。
4. **溢出压缩**：手牌区总宽度设上限；实际间距取 `min(基础间距, 总宽度上限 / (count - 1))`，卡本身尺寸不缩放，只压缩间距（卡多时自然重叠增多）。
5. **悬停表现**：抬高位移 + 旋转归零 + 轻微放大（约 1.1~1.2 倍）+ 该卡 `Canvas.sortingOrder` 临时提升到高于所有手牌卡；退出 hover 恢复基线值。
6. **层级基线**：非 hover 时 `sortingOrder = 手牌索引`（右边的卡覆盖左边的卡，与 STS 一致）。
7. **拖拽**：
   - 触发用 UGUI 标准 `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`，`IPointerEnterHandler`/`IPointerExitHandler` 驱动 hover，不自建阈值判定。
   - 拖起后，其余卡按新的（减少 1 张的）布局重新排列填补空位；被拖起的卡直接跟随鼠标屏幕坐标，不受布局函数约束。
   - 松手且不触发出牌（本轮固定行为，因为不做判定）：手牌顺序不变，回弹到该卡原 index 对应的重新计算位置。
8. **补位动画技术选型**：使用 DOTween。
   - 悬停抬起/落下：约 0.15s，`Ease.OutBack`。
   - 手牌重排补位（含拖拽释放回弹）：约 0.2~0.25s，`Ease.OutQuad` 或 `Ease.OutCubic`。
   - 每张卡自行维护自己的 Tween 引用，新动作触发前先 `Kill` 旧 Tween，避免叠加。
9. **手牌数量来源**：本轮不引入数据源接口抽象（评估后判断会增加不必要的复杂度）。直接用 Inspector 可调的 `int` 字段表示当前手牌数量，**用清晰注释标记为临时占位**，说明未来会替换为 Luban 表格驱动的真实数据源；替换时预期只改这一个字段的取值来源，不改布局 / hover / 拖拽逻辑本身。
10. **旧方案处理**：移除 `BattleScene` 中现有的 `Toggle` + `ToggleGroup` 组件与选中面板视觉，替换为新的 hover/drag 驱动脚本；在 `CODE_DECISIONS.md` 新增一条决策记录本次替换，不静默覆盖 CD-002。

## 实现范围建议（供实现者细化，非强制架构）

- 布局计算：一个不依赖 `MonoBehaviour` 状态的纯函数/纯类（输入卡数 → 输出每张卡的 `anchoredPosition`/`rotation`/`sortingOrder` 基线），便于未来单元测试。
- 手牌容器组件：持有当前 `CardView` 实例列表、临时占位的手牌数量字段、驱动布局函数、响应 hover/drag 事件、调用 DOTween 播放过渡。
- 单卡交互组件（可挂在 `CardView` 实例的根物体或外部包装物体上）：实现 `IPointerEnterHandler`/`IPointerExitHandler`/`IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`，把手势事件转发给手牌容器组件，自身不保存业务状态。
- 新脚本建议落在 `TinySpire/Assets/Scripts/UI/`（参考现有 `UI/Loading/` 惯例新建子目录，例如 `UI/Battle/Hand/`），具体文件划分由实现阶段决定。

## 边界与非目标（本轮明确不做）

- 不做出牌判定、合法目标高亮、出牌线/弃牌线检测。
- 不接入真实卡牌数据、ScriptableObject、Luban 表格、运行时业务状态。
- 不修改 `CardView.prefab` 本体。
- 不实现弃牌/抽牌等牌库操作，只验证手牌 UI 表现本身。
- 手牌数量变化仅通过 Inspector 字段手动调整验证，不做运行时快捷键/按钮。

## 验收点

- Game View 中，手牌能以扇形（旋转 + 下沉曲线）排布，中间卡居中、两侧卡对称倾斜。
- 将 Inspector 手牌数量字段调整为 3、5、10 等不同值并重新进入 Play Mode 后，布局都能正确扇开且不超出安全区（10 张时间距应可见压缩）。
- 鼠标悬停任意一张卡：该卡抬起、旋转归零、轻微放大，且显示在其余卡之上；移出后恢复原位。
- 按住任意一张卡拖动：其余卡实时重排填补空位，被拖起的卡跟随鼠标；松开鼠标后该卡按原顺序回弹到重新计算的位置，不触发任何出牌效果。
- 场景中不再存在 `Toggle`/`ToggleGroup` 相关组件或选中高亮面板。
- Unity Console 无新增错误或警告。
- `CODE_DECISIONS.md` 已补充 DOTween 引入记录与 CD-002 交互模型替换记录。

## 后续（明确不在本轮范围内，留给下一切片）

- 出牌判定（拖出安全区/出牌线触发 play）。
- 手牌数量真正接入 Luban 数据源，替换当前的 Inspector 占位字段。
- 卡牌可玩性反馈（不可用卡的置灰/无法拖拽表现）。
