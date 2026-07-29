---
title: Codex 实施 Prompt — BattleScene 手牌 UI（杀戮尖塔式）
page_type: handoff
lifecycle: proposal
date: 2026-07-29
companion_plan: 2026-07-29-battlescene-hand-ui-sts-style.md
note: 本文件供直接复制给 Codex 或其他外部实现 Agent 使用，非 Daedalus 自身使用格式。
---

# 发给 Codex 的实施 Prompt（可直接复制）

```text
你在为 Unity 6.5 项目 TinySpire 实现一个 BattleScene 手牌 UI 切片。请严格按下面的计划文档执行，不要扩大范围。

必读计划文档（唯一实现依据）：
Docs/Copilot_Daedalus/plans/2026-07-29-battlescene-hand-ui-sts-style.md

项目背景事实（不要重新决策，直接当作已确认前提）：
- 引擎/语言：Unity 6.5，C#。
- 已存在并必须复用、不得修改本体的预制体：TinySpire/Assets/Arts/Runtime/Card/Prefab/CardView.prefab
  （每张卡自带独立 Canvas，Canvas.sortingOrder 可直接设置）。
- DOTween / DOTweenPro 已经导入到 TinySpire/Assets/Plugins/Demigiant/，可直接使用，不要再引入其他补间库。
- 目标场景：TinySpire/Assets/Scenes/BattleScene.unity，当前手牌是 Toggle + ToggleGroup 单选高亮
  （需要被本次改动替换掉）。

本轮范围（只做这些）：
1. 扇形排布：纯函数/纯类，输入手牌张数，输出每张卡的位置(x)/旋转/纵向下沉(y)/层级基线，
   基于归一化位置 t∈[-1,1]：rotation = t × 最大角度；y = -k × t²；三个常量可调、不硬编码。
2. 溢出压缩：手牌总宽度设上限，实际间距 = min(基础间距, 总宽度上限/(count-1))，卡本身不缩放。
3. 悬停：IPointerEnterHandler/IPointerExitHandler 驱动——抬起位移 + 旋转归零 + 放大约1.1~1.2倍 +
   Canvas.sortingOrder 临时提升到高于所有手牌卡；退出恢复基线（基线 sortingOrder = 手牌索引，
   右边的卡覆盖左边的卡）。用 DOTween，约 0.15s，Ease.OutBack。
4. 拖拽跟手视觉：IBeginDragHandler/IDragHandler/IEndDragHandler 驱动——拖起后其余卡按“减少一张”
   的布局重新排列填补空位（DOTween 补位约 0.2~0.25s，Ease.OutQuad 或 OutCubic）；被拖起的卡直接
   跟随鼠标屏幕坐标；松手后不做任何出牌判定，直接按原 index 顺序回弹到重新计算的位置。
5. 手牌数量来源：用 Inspector 可调的 int 字段（例如 handCount），运行时据此实例化/更新对应数量的
   CardView。必须在字段声明处用清晰注释标记：这是临时占位，未来会被 Luban 表格驱动的真实数据源
   替换，替换时不应改动布局/悬停/拖拽逻辑本身。不要引入接口或数据源抽象层——评估后认为当前引入
   会增加不必要的复杂度。
6. 移除 BattleScene 中现有的 Toggle / ToggleGroup 组件与选中高亮面板视觉。
7. 每张卡自行维护自己的 DOTween Tween 引用，新动作触发前必须先 Kill 旧 Tween，避免动画叠加冲突。

明确不要做（这些是本轮的边界，越界视为超出范围）：
- 不要实现出牌判定、出牌线/弃牌线检测、合法目标高亮或任何“打出”这张卡的效果。
- 不要接入真实卡牌数据、ScriptableObject、Luban 表格或运行时业务状态。
- 不要修改 CardView.prefab 本体。
- 不要实现抽牌/弃牌等牌库操作。
- 不要新增除 DOTween 以外的第三方依赖。
- 不要执行 git commit / git push，也不要修改 Docs/Hermes_Pegasus/** 或 Docs/Gemini_Calliope/**
  下的任何文件。

建议的文件组织（可按需微调，但不要偏离层次划分）：
- 新脚本放在 TinySpire/Assets/Scripts/UI/Battle/Hand/ 下（参考现有 UI/Loading/ 目录惯例）。
- 布局计算逻辑做成不依赖 MonoBehaviour 状态的纯类，方便未来写 NUnit 测试。
- 手牌容器组件持有 CardView 实例列表 + 临时占位手牌数量字段，驱动布局与动画。
- 单卡交互组件只转发 hover/drag 事件给容器组件，不保存业务状态。

验收标准（照抄自计划文档，逐条自查）：
- Game View 中手牌以扇形（旋转 + 下沉曲线）排布，居中对称。
- Inspector 手牌数量字段改成 3 / 5 / 10 后重新进入 Play Mode，布局都能正确扇开且不超出安全区
  （10 张时间距应可见压缩）。
- 悬停任意一张卡：该卡抬起、旋转归零、轻微放大，显示在其余卡之上；移出后恢复原位。
- 拖动任意一张卡：其余卡实时重排填补空位，被拖起的卡跟随鼠标；松开后按原顺序回弹，不触发任何
  出牌效果。
- 场景中不再存在 Toggle / ToggleGroup 相关组件或选中高亮面板。
- Unity Console 无新增错误或警告。

完成后请给出：
1. 改动的文件清单（新增/修改/删除）。
2. 是否有偏离计划文档任何一条决策的地方，以及原因。
3. 是否新增了计划之外的依赖或修改了计划之外的文件。
```

## 使用说明

- 把上方 ```text``` 代码块里的内容整段复制给 Codex，与 `2026-07-29-battlescene-hand-ui-sts-style.md` 一起给它访问权限（或直接把计划文档内容一并贴过去）。
- Codex 产出代码后，建议回到 TinySpire Unity 工程里实际进入 Play Mode 验证验收点，而不要只看代码就认定完成。
- 验证通过后，回来让 Daedalus 补一条 `Docs/Copilot_Daedalus/06_testing/` 验证记录，并确认 `CODE_DECISIONS.md` 的 CD-003/CD-004 是否需要根据实际实现细节补充。
- 提交前仍需按 `Docs/AI_COLLABORATION_RULES.md` §3 展示审查包并等待 Theseus 批准，不要让 Codex 自行 commit / push。
