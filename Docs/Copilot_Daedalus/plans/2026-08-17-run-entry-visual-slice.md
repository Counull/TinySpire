---
title: RunEntryScene 主入口视觉切片实施计划
page_type: plan
lifecycle: archived
date: 2026-08-17
updated: 2026-08-24
scope: RunEntry presentation only
source: Docs/Hermes_Pegasus/art/entry-paper-stack-responsive-motion.md
status_source: ../STATUS.md
implementation_status: verified
---

# RunEntryScene 主入口视觉切片

## 1. 授权范围

本片只把已确认的 `ENTRY-BG-002`、三张完整纸、既有主菜单控件与一次性入场动画接入 `RunEntryScene`。不修改 `RunStateStore`、`RunFlowService`、Presenter 动作语义、战斗、存档、地图生成、GameData、FishNet、多人或 G3+；不新增菜单文案、页面、导航规则或软件包。

## 2. Seam audit

- `RunEntryScene.unity` 只序列化 Camera、Light、Scope 与 `RunEntryView`；Canvas、页面和菜单由 `RunEntryView.Awake` 运行时建立，因此视觉必须接入现有运行时组装，不能另建竞争 Canvas。
- 既有 Canvas 已使用 `ScaleWithScreenSize / 1920×1080 / match 0.5`；布局读取 Canvas `RectTransform`，不读取 Run 状态，也不写死 `Screen.width` 或 `-1920`。
- DOTween 已存在且 UI 模块启用；动画使用私有 Sequence/ID 与 `CanvasGroup.DOFade`，不引入新依赖。
- 两项美术资源由 `RunEntryView` 场景序列化字段直接引用，作为 `RunEntryScene` bundle 的依赖释放；不建立 `Resources.Load`、新 Addressables 地址或额外生命周期。

## 3. 资产与构图

1. 背景字节级复制到 `TinySpire/Assets/Arts/Runtime/UI/RunEntry/ui_run_entry_background.png`，基线 1920×1080 不裁切、不重定位。
2. 三纸共用 `ui_run_entry_paper_grain.png`。它从外部临时源 `paper-ivory-fineprint-v06.png` 的 `(448, 28, 1024, 1024)` 区域提取共同细颗粒残差并归一为中性浅灰；三层只用 tint 得到米白、炭黑、砖红，不导入三张 1920×1080 彩色纹理。
3. V06 基线角度为 `+17.52°`；米白边 top/mid/bottom 为约 `576/746.5/916.7 px`，黑色中线外边约 `812.5 px`，红色约 `863.5 px`。米白中线覆盖 `38.88%`，总纸叠约 `44.97%`，不得退化为 60% 大面。
4. 三纸都是完整 `RawImage`、共用纹理且 `raycastTarget=false`。标题与菜单位于独立 `PagesRoot/MainMenuContent`，不挂在旋转的 `PaperStackRoot` 下。
5. 保留五个既有按钮及原动作。按钮为 `459×99`、27 px 对称切角、透明纸面内芯、细描边和克制下缘；文字继续由 TMP 渲染。

## 4. 动画与响应式

- 红纸 `0.00s`、黑纸 `0.12s`、米白纸 `0.24s` 开始；每张用 `0.76s OutCubic`，米白在 `1.00s` 停稳。
- 内容 `1.10s` 开始用 `0.22s OutQuad` 淡入；淡入前 CanvasGroup 不交互、不挡 raycast。无循环、粒子、视差、镜头运动、漂浮或弹跳。
- 入场只播放一次；离开主菜单或销毁/禁用组件时只 Kill 本组件私有 Tween。返回主菜单直接保持最终稳定态。
- 21:9 保留左侧 1920×1080 物理构图区并向右暴露更多背景；背景 cover 会裁上下。窄窗按宽度等比收纸边、露边和菜单内容，背景右缘对齐以保留主塔，左侧远景被裁；不靠扩大米白纸保证菜单安全。

## 5. 验收与回滚

验收包括 Unity 编译、视觉/场景/importer 定向 EditMode、Local Addressables、BuildLayout 物理 bundle 证据、Bootstrap Packed Play 1920×1080 截图及设置页往返。回滚只撤销本计划列出的运行时视觉代码、Editor 重建接线、场景两项引用、两项资产、测试与文档；不得 reset、clean、unstage 或触碰既有 GameData/Hermes WIP。

实施结果与剩余风险见 `../06_testing/2026-08-17-run-entry-visual-slice.md`。
