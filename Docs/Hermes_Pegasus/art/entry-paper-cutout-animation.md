---
title: TinySpire · 主入口斜切纸片 · 资产与动画实现记录
status: superseded-for-runtime
delivery_status: not-ready
superseded_by: entry-paper-stack-responsive-motion.md
created: 2026-08-16
scope: RunEntryScene entrance art only
source:
  - Docs/Hermes_Pegasus/art/entry-ui-asset-checklist.md
  - user-approved compositional reference in current art review
  - /home/lxxr/tinyspire-concepts/entry/entry-paper-cutout-002_ref-v01_rgba.png
---

# 主入口斜切纸片 · 资产与动画实现记录

## 0. 决策摘要

主入口不做多层景深、粒子、循环视差或视频背景。

运行时只有两组视觉对象：

```text
完整塔内底景
└─ 左侧纸片组件（一次性的入场动画）
   ├─ 砖红背纸
   ├─ 灰黑结构纸
   └─ 暖米色主纸
      └─ TMP 标题、主菜单、后续入口 UI
```

三张纸不是三层背景；它们是同一个“从左侧切出菜单安全区”的前景组件。最终静止画面要读成：

```text
暖米色压纹大面 → 厚灰黑斜梁 → 窄砖红背层 → 塔内底景
```

## 0.1 本记录不是正式美术交付包

本页目前只保存已确认的视觉关系、资产生产方式和未来实现边界，避免素材迭代时方向漂移。`delivery_status: not-ready` 表示程序 Agent 不应据此提前接入正式美术，也不应把候选图放入 Unity 资源目录。

正式的“入口美术交付文档”必须等当前入口阶段所有实际导入项都已逐张生成、用户确认、正式化并完成素材 QA 后再生成。那份交付文档只写最终事实：文件路径、版本/哈希、画布尺寸、alpha/导入设置、节点层级、Z 顺序、最终位置、入场时序、点击穿透、验收截图和回退关系；不再夹带候选、未决构图或开放设计问题。

左侧浅色区域不能被实现为普通卡片、白色矩形、菜单面板或孤立的干净 SVG。它应是与塔内视觉同源的、满高的压纹纸面，并与灰梁/红背层共同构成一条长斜向边界。

## 1. 视觉关系：什么是对的，什么是错的

### 对的

- 米色主纸铺满左侧，并超出画面的左、上、下边缘；观众只看见其右侧一条从上方偏中向右下方延伸的长斜切。
- 灰黑层不是描边，而是一条有宽度的斜向结构纸，紧贴米色切边。
- 红层不是折角装饰，而是灰黑结构纸后方露出的窄背纸；它只作为连续边界的一小段色彩深度。
- 米、灰、红三层均使用同源的磨砂/压纹/丝网印刷纸质，而非真实手工羊皮纸、纸纤维、卷边或撕裂边。
- 最终状态与已确认的入口概念在色相、长斜边、结构宽度关系上可直接对照。

### 错的

- 一张带黑色描边、阶梯折边、红色折页的独立“纸片 UI”。
- 米色纯平矩形或纸张纹理与背景完全断裂。
- 将灰梁简化成一像素阴影，或把红背纸做成随机点缀。
- 用三张独立生图的素材硬拼：各自构图/纹理不同会导致边界无法贴合。
- 让三张纸持续漂移、制造景深，或加入粒子与循环动画。

## 2. 资产合同

### 2.1 最终交付物

最终运行时需要四张同画布、同坐标原点的 RGBA PNG：

```text
entry-bg-tower-001.png          # 1920×1080，完整塔内底景，无菜单纸片
entry-cutout-red-001.png        # 1920×1080，透明外部；窄砖红背纸
entry-cutout-gray-001.png       # 1920×1080，透明外部；宽灰黑结构纸
entry-cutout-ivory-001.png      # 1920×1080，透明外部；暖米色主纸
```

- 四张均采用 `1920×1080` 设计画布；在 `CanvasScaler` 中以同一参考分辨率显示，确保最终边界无需逐分辨率调坐标。
- 红、灰、米三张的透明区域必须是真 alpha，不接受洋红底、棋盘格烘焙图、黑底或带文字的位图。
- 三张纸的透明画布可以全屏，但各自只保留自己的可见色面；这样可在 Unity 保持相同锚点并独立运动。
- 标题、菜单、按钮、数字和本地化文字均不是图片资产，统一使用 TMP / Unity UI。
- 当前 `entry-paper-cutout-002_ref-v01_rgba.png` 是经过确认的**合并形状与材质参考**，不是最终运行时单层资产。

### 2.1.1 已生成的三张候选（2026-08-16）

已从同一张合并参考按颜色归属拆出三个同坐标的透明候选，均留在仓库外，尚未提升到 Unity 正式资源：

```text
/home/lxxr/tinyspire-concepts/entry/entry-cutout-red-001_ref-v01.png
/home/lxxr/tinyspire-concepts/entry/entry-cutout-gray-001_ref-v01.png
/home/lxxr/tinyspire-concepts/entry/entry-cutout-ivory-001_ref-v01.png
```

实测事实：三张均为 `1920×1080`、RGBA、alpha 范围 `0–255`。它们以相同画布坐标叠回后，已输出技术 QA 图 `entry-paper-cutout-001_ref-v01_recomposed-qa.png`，视觉关系为米色大面 → 灰黑斜梁 → 窄砖红背层；键色残留与小型孤立噪点已在候选导出阶段清理。

这证明三张纸可以独立移动且最终无缝重合，但它们仍须和最终完整塔内底景合成审阅后，才能成为正式交付资产。

### 2.2 推荐的制作管线：一张主参考 + 三张精确掩膜

不要为红、灰、米分别重新提示 Image2。模型无法保证它们的斜切位置、压纹尺度和纸张结构一致。

推荐管线：

1. **锁一张主合成参考。**
   - 用 Image2 参考已确认入口概念，生成“米色主面 + 灰梁 + 红背层”的完整左侧切片。
   - 背景为均匀洋红 `#FF00FF`；主合成参考必须保留原始洋红源文件。
   - 当前的 `entry-paper-cutout-002_ref-v01_key-source.png` / RGBA 候选可作为这一阶段的方向参考，未自动提升为最终生产资产。

2. **边界连接抠图。**
   - 只从画布边缘洪泛删除接近洋红的连通背景；禁止“全图绿/红色高就删”的阈值抠图。
   - 对斜切边缘残留的洋红反锯齿做保守去色，不能伤害砖红背纸。
   - 核验：RGBA、alpha extrema 为 `0..255`、透明区域无洋红边、主体无洞。

3. **用人工定义的同坐标 SVG mask 拆出三张纸。**
   - 以主合成参考的实际 1920×1080 构图为准，手绘三张简单的多边形 mask：`ivory`、`gray`、`red`。
   - Mask 的职责只是定义色面归属和长斜边位置；材质像素仍来自同一张主合成参考，因此三层的颗粒尺度、色调和印刷感天然一致。
   - 不用自动色彩分割替代 mask：阴影、磨损和压纹会让“按颜色抠”切坏边缘。

4. **由主参考按 mask 导出三张全画布 RGBA。**
   - 每层保持原始坐标；透明区域置 alpha 0，透明像素的 RGB 归零。
   - 将三层在本地按最终坐标叠回时，应在肉眼上回到主合成参考的形状关系。
   - 本地叠图只用于资产 QA，不得把它伪称为新的 AI 融合图。

5. **生成完整底景。**
   - 以同一主合成参考为 Image2 编辑参考，移除左侧纸片组件并重建被遮住的塔内空间，得到 `entry-bg-tower-001.png`。
   - 画面静止时，将“底景 + 红 + 灰 + 米”叠回，必须接近主合成参考；差异若集中在红/灰边界，优先修 mask / 参考图，而不是在 Unity 写补丁遮盖。

### 2.3 文件保留与正式化

- 原始带键色图、透明候选、mask、QA 合成图都先保留在 `/home/lxxr/tinyspire-concepts/entry/`，使用 `ref_vNN` 命名。
- 用户明确确认每一张最终 PNG 后，再复制到 Unity 正式 art 路径并登记采用关系；不得批量提升候选。
- 导入 Unity 后使用 `Sprite (2D and UI)`，保留 alpha，关闭不必要的 mipmap；具体压缩设置以目标平台实测为准。

## 3. Unity 组件结构

当前 `RunEntryView` 以 `1920×1080` 的 `CanvasScaler.ScaleWithScreenSize` 动态建立入口 Canvas。正式美术接入时，入口艺术不应把状态/导航混进 `RunEntryView`，而应新增一个只负责可视层的协作者。

建议结构：

```text
RunEntryCanvas
├─ EntryTowerBackgroundView       # Full-screen Image/RawImage：entry-bg-tower-001
├─ EntryPaperCutoutView           # 只管理三张纸与一次入口动画
│  ├─ RedBackingImage             # entry-cutout-red-001
│  ├─ GrayStructureImage          # entry-cutout-gray-001
│  └─ IvorySurfaceImage           # entry-cutout-ivory-001
└─ Existing ContentSurface / pages
   └─ TMP 标题、菜单、角色页、地图、失败页
```

### `EntryPaperCutoutView` 的边界

它只需要暴露很小的接口：

```csharp
public interface IEntryPaperCutoutView
{
    void PlayEntrance();
    void SetFinalState();
}
```

- 不读取 `RunStateStore`，不订阅 `RunEntryPage`，不处理按钮，不持有本地化文本。
- `RunEntryPresenter` 仍只负责页面投影和 UI 意图；它不控制 Tween。
- Scene 的 visual composition root 在入口首次建好后调用一次 `PlayEntrance()`；场景重入或测试可用 `SetFinalState()` 直接跳到最终位置。
- 三个 `Image.raycastTarget = false`，不能挡住标题、菜单或将来的按钮。
- 三张 Image 使用同一全屏 `RectTransform` 尺寸、同一锚点/基准分辨率；它们只因自身 PNG 的 alpha 不同而露出不同区域。

## 4. 动画合同

### 4.1 运动原则

“斜着切出来”指的是**可见边界的斜向结构**，不是让每张纸沿对角线飞行。

三张纸仍以水平方向从左侧画外移动到同一最终坐标：这能保持底部、顶部、灰梁和红背层的精确对齐。若让它们同时带明显 Y 位移，最终会破坏那条连续长斜边，并显得像卡牌飞入。

### 4.2 顺序与默认参数

```text
0.00s  砖红背纸开始从左进入
0.08s  灰黑结构纸开始进入
0.16s  暖米色主纸开始进入
0.58s  米色纸到位后，标题/菜单淡入
```

- 每张纸使用相同的短 `DOAnchorPosX` 入场 Tween，默认 `0.42f`、`Ease.OutCubic`。
- 不做弹跳、冲拳、呼吸循环、抖动、粒子或二次回弹。
- 可在 Tween 前将三张纸放到各自 `RectTransform` 的“完整位移至左侧画外”起点；不要写死 1920 像素，按当前 Canvas Rect 的实际宽度计算。
- 正式状态三张纸都回到同一 `anchoredPosition`；时间差只是入场节奏，不是最终位置差。
- 背景最多做一次非常轻的 alpha/位置就位；若手测不增益可删除，不能为了“有动画”保留。
- `OnDisable` / `OnDestroy` 必须 Kill 自己拥有的 DOTween sequence，避免场景重入后旧 Tween 继续写 RectTransform。

### 4.3 示意伪代码

```csharp
public void PlayEntrance()
{
    KillSequence();
    SetOffscreenLeft(red, gray, ivory);

    _sequence = DOTween.Sequence()
        .Append(red.DOAnchorPosX(0f, 0.42f).SetEase(Ease.OutCubic))
        .Insert(0.08f, gray.DOAnchorPosX(0f, 0.42f).SetEase(Ease.OutCubic))
        .Insert(0.16f, ivory.DOAnchorPosX(0f, 0.42f).SetEase(Ease.OutCubic));
}
```

这里的 `0f` 表示每层预先保存的最终 anchored X，而不是假定 Canvas 左边就是 0；实际代码应保存 final positions 并按 Canvas 宽度推导 off-screen 起点。

## 5. 验证清单

### 资产 QA

- [ ] 四张最终 PNG 均为 `1920×1080`、RGBA；纸片三张 alpha 同时包含 0 与 255。
- [ ] 抠图只删除连通键色背景；没有洋红/绿色边，没有黑底，没有棋盘格烘焙。
- [ ] 红、灰、米三张在最终坐标合成后，与锁定的主合成参考边界一致。
- [ ] 米色/灰色/红色的压纹尺度与色调一致；红背层连续且窄，不被误读为装饰条。
- [ ] 底景 + 三张纸的最终叠加没有漏缝、重影或错误露出。

### Unity 手测

- [ ] 16:9 下，三张纸依次从左切入，最终边界连续。
- [ ] 非 16:9 窗口下，CanvasScaler 后依然不露出黑缝或错位。
- [ ] 动画期间和结束后，主菜单按钮均能点击。
- [ ] 页面切换不重播入口切纸；重新进 RunEntryScene 才允许重播。
- [ ] 多次进出场景不会产生重复 Tween、空引用或 Console Error。
- [ ] `SetFinalState()` 能在自动化/测试环境无动画地直接展示最终构图。

## 6. 停止条件与后续

本记录只定义主入口“塔内底景 + 三张斜切纸”的资产与一次性入场动效。

角色头像、标题最终摆位、菜单按钮底板、地图节点、失败页、音效、设置功能和其它 Run 页面艺术均不随本记录实现。每项仍按“逐张生成 → 用户审阅 → 保存/正式化”的节奏另行处理。
