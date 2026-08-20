---
title: RunEntryScene 主入口视觉切片验收
page_type: testing
lifecycle: active
date: 2026-08-17
scope: RunEntry presentation only
status_source: ../SESSION_LOG.md
source: Docs/Hermes_Pegasus/art/entry-paper-stack-responsive-motion.md
---

# RunEntryScene 主入口视觉切片验收

## 1. 结论

本片在当前唯一 Unity 6000.5.5f1 Editor 中通过编译、22/22 定向 EditMode、Local Addressables、BuildLayout 和 Bootstrap Packed Play 1920×1080 验收。最终静帧的背景、纸边位置、层间露边与色彩复现 V06；五个既有菜单按钮可点，设置页往返后不重播入场。

本记录只证明入口表现：没有修改 Run 状态链、战斗、存档、地图、GameData、菜单动作语义、FishNet/多人、ProjectSettings、asmdef 或包依赖，也没有执行 Player build 或 G3+ 工作。

## 2. 资产来源与 importer

| 资产 | 来源与 SHA-256 | Unity importer |
|---|---|---|
| `ui_run_entry_background.png` | `Docs/Hermes_Pegasus/art/assets/entry/entry-bg-002_ref-v09-right-subject.png` 的字节级复制；两者均为 `2A08469E5384BC7F9E65CD218C26C040B7B1B403DA0BF603A0CAC904E69870DA` | Sprite 2D/UI、Single、Full Rect、sRGB、1920×1080、max 2048、mipmap off、Read/Write off、Alpha None、Clamp、Bilinear、Uncompressed |
| `ui_run_entry_paper_grain.png` | 外部 `\\wsl.localhost\Ubuntu\home\lxxr\tinyspire-concepts\entry\reference-paper-material-v06\paper-ivory-fineprint-v06.png`（源 SHA `643AFBFD8EA308D52A92BF50D887768E51A33F23E2DC4F5DD0E354594662483C`）；裁取 `(448,28)` 起的 1024²，共同残差归一为中性细颗粒；产物 SHA `055BC424CF391335DD0CB1A7D30555874E617A6CA3283877BD3C4DF3BCA9AA21` | Texture2D/Default、sRGB、1024×1024、max 1024、mipmap off、Read/Write off、Alpha None、Clamp、Bilinear、Uncompressed |

纸纹生产选择一张共享、无损的 1024² RGB 纹理；三张 1920×1080 彩色临时源没有进入生产目录。Unity 生成全部 `.meta`，没有手写或复制 `.meta`。

## 3. 自动化与时序

最终定向 EditMode job `c42339825cab4bc684c7df34b549e45e`：**22/22 passed**、0 failed、0 skipped、1.739 秒。覆盖：

- V06 的 1920×1080 米白 `576/746.5/916.7`、黑 `812.5`、红 `863.5` 边界与完整纸画外起点；
- 21:9 构图宽上限、窄窗等比内容缩放和“不扩大到 60%”门禁；
- 三纸共享纹理、全部不接收 raycast，菜单不在旋转根下；
- 五个 `459×99` 八边形按钮继续使用真实 `Button + TMP`；
- 私有 Sequence 只播放一次，并按下表确定性推进与清理；
- RunEntry 旧 View/Presenter 意图、场景 Scope、三场景 Addressables 组和 Bootstrap 入口回归。

| 合同点 | 实现/验证结果 |
|---|---|
| 红纸开始 | `0.00s` |
| 黑纸开始 | `0.12s` |
| 米白纸开始 | `0.24s` |
| 米白停稳 | `1.00s`（0.76s OutCubic） |
| 标题/菜单淡入 | `1.10s` 开始，0.22s OutQuad；此前不交互、不挡 raycast |
| 重播/清理 | 同一场景只播放一次；离开主菜单、禁用或销毁只 Kill 私有 ID |

## 4. Addressables 与真实运行

- `TinySpire/Addressables/Build Local Content` 成功，Unity 日志记录构建 20.569 秒，`BuildError` 为空。
- 最新 `Library/com.unity.addressables/buildlayout.json` 为 140,495 bytes。背景与纸纹均为 `BuildLayout/DataFromOtherAsset`，唯一 owner 是 `Assets/Scenes/RunEntryScene.unity`；两者和 RunEntryScene 同处 `TinySpire Scenes` 的 `tinyspirescenes_scenes_assets_scenes_runentryscene...bundle`，Provider 为 `AssetBundleProvider`，没有新增 AddressableName。
- Packed Play 从 `BootstrapScene` 实际加载 `RunEntryScene`。运行时读取为 `Screen/Canvas = 1920×1080`、内容 alpha 1 且可交互；三纸共用纹理且不挡点击，最终旋转均为 `17.52°`。
- 实际调用现有 Settings Button 后 `SettingsPage` 打开，再调用现有 Back Button 返回主菜单；返回后 alpha 1、可交互，`EntrancePlayCount=1` 且没有 active Sequence。
- Play 结束后的 Console error 查询为 0。

实际截图：[1920×1080 RunEntry 最终静帧](evidence/2026-08-17-run-entry-visual/run-entry-1920x1080.png)。

## 5. 响应式行为与剩余风险

- 16:9：完整显示原始 1920×1080 背景；V06 纸边和菜单尺寸逐项对齐。
- 21:9：CanvasScaler 与视觉布局把左侧视觉构图区限制为相当于 1920×1080 的宽度，右侧显示更多背景；aspect-cover 会从背景上、下各裁约 12.5%，不变形。
- 4:3/窄窗：纸边与露边按宽度缩放，菜单再按同一构图比例缩小；背景按高度 cover 并右缘对齐，保留右侧主塔而裁掉左侧约 25% 远景。更窄窗口仍优先保证菜单可读可点，不把米白纸扩到 60%。
- 21:9 与窄窗由 Unity 几何测试覆盖，未各自留实际截图；当前唯一实拍证据是 16:9。没有 Player build、目标移动平台、DPI/安全区或不同 OS 字体验收；动态 CJK 字体的跨平台风险沿用既有 G1-A 记录。
