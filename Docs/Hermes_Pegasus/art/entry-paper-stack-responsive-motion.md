---
title: TinySpire · 入口三张完整纸 · 自适应旋转方案
status: background-v09-confirmed; static-composition-v06-accepted; runtime-implementation-verified
created: 2026-08-16
scope: RunEntryScene main-menu entrance foreground only
supersedes_for_runtime: entry-paper-cutout-animation.md sections 2-5
---

# 入口三张完整纸 · 自适应旋转方案

## 0. 结论

入口前景不再把“暖米 / 灰黑 / 砖红”做成三条窄楔形贴图后单独移动。

那种拆法只能静止时复原参考构图；一旦独立旋转，就会像三根斜飘带，不会读成三张纸。

新的方向是：**三张完整的矩形纸，按同一构成主义纸面语法独立逆时针旋进场；最终通过同角度叠放和小幅横向错位，只露出灰黑、砖红的右侧边角。**

```text
塔内完整底景
└─ PaperStackRoot（只管理三张完整纸的画面几何）
   ├─ BrickRedFullSheet       最底层；只露出最窄的一截
   ├─ CharcoalGrayFullSheet   中层；露出较宽的一截
   └─ WarmIvoryFullSheet      顶层；承载标题和菜单安全区
```

最终静帧仍是：

```text
暖米色大面 → 灰黑斜边 → 窄砖红背边 → 塔内世界
```

区别只在于：这三条边来自**三张完整矩形纸的遮挡关系**，不是来自三张裁到只剩边条的 PNG。

> **2026-08-17 用户校正：** 入口最终静帧必须以用户提供的示意图为构图目标；当前工作只应为该静帧补上一次入场动画，不能用一块覆盖左侧约 60% 的新大纸面重写原构图。三纸的最终覆盖范围、锚点与静态形状需要重新按示意图回推。

### 已确认背景：ENTRY-BG-002

用户确认 `assets/entry/entry-bg-002_ref-v09-right-subject.png` 为入口背景定稿：`1920×1080 RGB`、无 UI/文字/人物；右侧约 40–45% 为近中景塔主体，左侧为连续远景塔群与雾层，不是空白菜单区或近景遮挡。它以既有 TinySpire 封面为唯一风格锚点，使用中性米灰、炭黑和克制砖红。其 Runtime 副本已接入 `TinySpire/Assets/Arts/Runtime/UI/RunEntry/ui_run_entry_background.png`。

### 已接受整体静帧：ENTRY-OVERALL-V06

用户接受 `assets/entry/entry-overall-ref-v06-subtle-labels.png` 作为当前入口的静态视觉目标：细颗粒、低对比的档案纸；米白主纸、炭黑结构纸、深砖红背纸；对称双切角按钮只带约 `2px` 下缘分离和极弱顶边，不做厚侧壁、内框或明显投影。该方向已接入 Unity，并完成 16:9 Packed Play 验收。

## 1. 当前候选的处置

以下三张已拆出的候选仅保留为“最终静帧色彩宽度、压纹和斜边关系”的参考，**不得用作独立旋转的运行时纸片**：

```text
/home/lxxr/tinyspire-concepts/entry/entry-cutout-red-001_ref-v01.png
/home/lxxr/tinyspire-concepts/entry/entry-cutout-gray-001_ref-v01.png
/home/lxxr/tinyspire-concepts/entry/entry-cutout-ivory-001_ref-v01.png
```

它们不是失败的抠图；问题是资产几何不匹配新的动画意图。不能用补 Tween、改 Pivot 或拉伸条带来挽救。

## 2. 已生成的共享纸面材质

已生成并存入项目文档素材区的是一个**无 alpha、无边框、无文字、无折页**的共享纸纹候选：

```text
assets/entry/entry-paper-material-base-001_ref-v01.png
```

它是 `1024×1024` RGB 的低对比中性浅纸面；三张完整纸共用同一张纹理，在 Unity `Image.color`（或等价材质 tint）中分别取暖米、灰黑、克制砖红。它不需要平铺，只拉伸覆盖每张完整矩形纸。

### 共同规格

- 不使用洋红键色：纸面本身是不透明矩形，没有透明背景要抠。
- 不画右侧斜边、黑色描边、红色折页、阴影、标题、按钮或建筑结构。
- 这不是摄影感手工羊皮纸；是平整、低反差、构成主义印刷/压纹纸面。
- 同一源纹理让红、灰、米天然保持相同的颗粒尺度和印刷感；运行时只改色相与明度。
- `assets/entry/entry-paper-material-base-001_ref-v01.png` 是 V05 的保留候选，不是已确认材质；它比示意图纸面更浅、更均匀，不能直接作为正式入口纸。
- `assets/entry/entry-bg-001_ref-v01.png` 也是保留候选，不是已确认背景；它在 V05 中与过大的纸层一起改变了示意图的静态构图。
- 两者均不得复制到 `TinySpire/Assets` 或接入 Addressables，直到按示意图完成重构并经用户复核。

### 运行时几何不烘焙进图片

矩形尺寸、旋转角、露出宽度、阴影和边缘关系由 Unity `RectTransform` / `Image` 组合决定；图片仅提供材质。这样改安全区或适配宽屏时不需要重生图。

## 3. 分辨率适配合同

### 3.1 Canvas 基线

继续沿用入口当前的：

```csharp
CanvasScaler.ScaleMode.ScaleWithScreenSize
referenceResolution = new Vector2(1920f, 1080f)
matchWidthOrHeight = 0.5f
```

在常规 16:9、超宽屏与较窄窗口下，所有纸和标题/按钮走同一 Canvas 缩放规则。不能用 `Screen.width` 写死某套 1920 像素坐标后不重算。

### 3.2 为什么普通“全高矩形”旋转后会露缝

一个刚好等于视口高度的高矩形旋转后，其上下角会缩进可视区域，露出塔内底景的三角缝。

因此纸的高度要按视口**对角线**计算，而不是按当前高度计算：

```text
canvasWidth  = Canvas 根 RectTransform.rect.width
canvasHeight = Canvas 根 RectTransform.rect.height
bleed        = 0.06 × max(canvasWidth, canvasHeight)
sheetHeight  = sqrt(canvasWidth² + canvasHeight²) + 2 × bleed
```

这样即使以入场允许的最大角度旋转，纸也始终跨过上下边界。

主纸宽度不是固定像素，而由“菜单安全区 + 旋转吃掉的水平余量”决定：

```text
maxAngleRad  = 约 18°（换算为弧度）
safeWidth    = 0.46 × canvasWidth
rotationLoss = sheetHeight × abs(sin(maxAngleRad))
sheetWidth   = max(0.62 × canvasWidth, safeWidth + rotationLoss + bleed)
```

这些是首轮参数，不是锁死的美术数值。最终以 16:9、21:9 和最窄支持窗口的截图验收微调。

### 3.3 层间错位必须沿“纸的本地 X 轴”计算

三张纸最终使用同一个旋转角。红、灰、米之间不要在屏幕世界 X 上硬加偏移；应在 `PaperStackRoot` 的局部 X 轴错开：

```text
Red:    local X = 0
Gray:   local X = -(redRevealWidth)
Ivory:  local X = -(redRevealWidth + grayRevealWidth)
```

因为父节点已经旋转，局部 X 自动垂直于那条长斜边；无论宽屏还是窄屏，露出的灰/红边都保持一致的“纸层厚度”关系。

初始建议按视口宽度比例定义：

```text
redRevealWidth  = 0.018 × canvasWidth
grayRevealWidth = 0.045 × canvasWidth
```

最终由静帧截图对照入口概念确定，不能按 Image2 图片中的某个像素绝对值硬编码。

## 4. Unity 层级与职责

```text
RunEntryCanvas
├─ EntryTowerBackgroundView             # 完整塔内底景，最底
├─ EntryPaperStackView                  # 纯视觉组件，无 Run 业务状态
│  └─ PaperStackRoot                    # 全屏/居左的旋转坐标系
│     ├─ BrickRedFullSheet               # UGUI Image / RawImage
│     ├─ CharcoalGrayFullSheet           # UGUI Image / RawImage
│     └─ WarmIvoryFullSheet              # UGUI Image / RawImage
└─ Existing ContentSurface / pages       # TMP 标题和按钮，独立于旋转节点
```

- 三张完整纸的 `raycastTarget = false`；不能遮挡菜单。
- TMP 标题、菜单按钮不挂在 `PaperStackRoot` 下：它们应保持屏幕/安全区的稳定排版，不随纸的旋转抖动。
- `EntryPaperStackView` 不读取 `RunStateStore`、不订阅页面、不能处理按钮。
- `RunEntryPresenter` 继续只负责 ViewModel/页面行为；视觉 Tween 由入口 visual composition root 在首次展示主菜单时调用一次。

## 5. 动画：完整纸旋出，背纸边角漏出

### 5.1 静帧

`PaperStackRoot` 最终旋转保持一个构成主义小角度，例如 `+16°`。三张完整矩形纸都在同一 Root 内，因此同角度平行；通过局部 X 偏移从右侧依次露出灰、红。

在 Unity UI 的局部坐标中，Z 正角为逆时针。这里的角度方向要在 Editor 截图中确认；若实际显示方向反了，改符号即可，不能在文案中掩盖。

### 5.2 已确认入场时序（V03 motion study）

```text
0.00s  三张纸都在左侧画外；PaperStackRoot 的角度为 -8°
0.00s  砖红完整纸开始入场，并向 +16° 逆时针旋转
0.12s  灰黑完整纸开始入场，并向 +16° 逆时针旋转
0.24s  暖米完整纸开始入场，并向 +16° 逆时针旋转
1.00s  米色主纸停稳
1.10s  标题、菜单淡入
```

用户已确认该一秒节奏。
- 每张纸都是真正完整矩形，因此可以有自己的短旋转和横向平移；这才符合“三张纸分离式地转出来”。
- 时差很短，只让观众读出“纸层先后”，不变成三张卡牌飞行秀。
- 若首轮实测发现三张同时转太花，退化方案是：**仅米色主纸逆时针旋入；灰/红完整纸先在其背后就位，随主纸到位后通过很短的 alpha/局部 X reveal 漏出边角。** 这仍是有效且更克制的方案。
- 禁止弹跳、冲拳、呼吸循环、粒子、视差、镜头推拉或持续漂浮。

### 5.3 示意代码（不是现成可粘贴实现）

```csharp
// 每次 Canvas 尺寸变化后重算，所有值都来自当前 canvasRect。
void RebuildGeometry(RectTransform canvasRect)
{
    var size = canvasRect.rect.size;
    float bleed = 0.06f * Mathf.Max(size.x, size.y);
    float height = Mathf.Sqrt(size.x * size.x + size.y * size.y) + 2f * bleed;
    float width = Mathf.Max(
        0.62f * size.x,
        0.46f * size.x + height * Mathf.Sin(18f * Mathf.Deg2Rad) + bleed);

    SetSize(red, width, height);
    SetSize(gray, width, height);
    SetSize(ivory, width, height);

    float redReveal = 0.018f * size.x;
    float grayReveal = 0.045f * size.x;
    red.anchoredPosition = _baseFinal;
    gray.anchoredPosition = _baseFinal + Vector2.left * redReveal;
    ivory.anchoredPosition = _baseFinal + Vector2.left * (redReveal + grayReveal);
}

void PlayEntrance()
{
    KillOwnedTweens();
    RebuildGeometry(_canvasRect);
    SetStartStateOffscreenLeft(rotationZ: -8f);

    _sequence = DOTween.Sequence()
        .Insert(0.00f, PlaySheet(red,   finalZ: 16f, duration: 0.76f))
        .Insert(0.12f, PlaySheet(gray,  finalZ: 16f, duration: 0.76f))
        .Insert(0.24f, PlaySheet(ivory, finalZ: 16f, duration: 0.76f));
    // Fade title/menu from 1.10f after the ivory sheet has settled.
}
```

实际实现要缓存每张纸的最终局部位置，按当前 `canvasRect.rect.width` 推导“画外左侧”起点；不能用 `-1920f`。`OnRectTransformDimensionsChange` 或明确的 Canvas 尺寸变化回调中重建几何，**不在每帧 Update 重算**。

## 6. 资产/实现验收

### 美术

- [x] 静态视觉目标采用细颗粒、低对比的平整档案纸；不使用先前粗颗粒/水波感候选。
- [x] 三纸最终覆盖范围以 `ENTRY-OVERALL-V06` 为当前目标；暖米主纸不扩大为约 60% 的新大面。
- [x] 灰、红露边与按钮区域以 `ENTRY-OVERALL-V06` 为当前目标；按钮仅保留约 `2px` 的克制分离感。

### 自适应与运行时

- [ ] 16:9、21:9、最窄支持窗口各截图一次；纸旋转期间及停稳后不露上下角缝。
- [ ] 同一尺寸下反复进入场景，最终三层相对位置不漂移。
- [ ] 调整窗口尺寸后只重算一次几何，不创建新 Texture/Sprite，不在 Update 分配内存。
- [ ] 标题和菜单不随纸旋转，动画中始终可读且最终可点击。
- [ ] `OnDisable` / `OnDestroy` Kill 本组件拥有的 DOTween；场景重入不残留旧补间。

## 7. 实施结果与候选边界

- `assets/entry/entry-paper-material-base-001_ref-v01.png` 与 `assets/entry/entry-bg-001_ref-v01.png` 保留为未采用的参考候选，不接入 Runtime。
- `assets/entry/entry-bg-002_ref-v09-right-subject.png` 已确认；其 Runtime 副本与 RunEntryScene 同一 Addressables bundle。
- `ENTRY-OVERALL-V06` 已落实为三张完整纸、纸面颜色/纹理、459×99 对称切角按钮和一秒错拍入场；菜单内容不随纸根旋转。
- `EntryPaperStackView` 与 `EntryOctagonGraphic` 已接入 RunEntryScene，并以一次性私有 DOTween Sequence 完成入场。Packed Play 的 1920×1080 手测、交互与 Console Error 0 已通过。
- 当前 16:9 静帧经用户审阅确认；后续改动须作为新的视觉切片，不重开已锁定的入口方向。
