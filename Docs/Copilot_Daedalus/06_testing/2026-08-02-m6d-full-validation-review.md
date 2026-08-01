---
title: M6D 全量验证、双轴复审与文档收口
page_type: testing
lifecycle: active
date: 2026-08-02
status: passed
scope: M6A～M6C 全量回归、Addressables、Bootstrap、真实 Game View、回顾意见分流与 Standards / Spec 复审
plan: ../plans/2026-08-01-m6-card-play-legality-target-selection.md
status_source: ../SESSION_LOG.md
---

# M6D 全量验证、双轴复审与文档收口

## 当前结论

M6D 已通过。最终自动验证、Addressables 与 Bootstrap 冒烟全部成功；真实 Game View 由 M6C 的 Self/左右 Enemy/无效释放/多分辨率/下一轮物理序列，以及最终 transition 代码上的费用不足复测共同覆盖。Spec 首轮为 0 finding，Standards 的唯一硬 finding（M6C 验收页残留过期状态）已修正；两个判断性气味均按深模块与里程碑边界完成处置，没有为消除表面重复新增浅 helper 或提前改造 M8/M9。

## EditMode、队首失效夹具与静态构建

| 检查 | 结果 |
|---|---|
| M6 规则、队列、presentation、目标交互、屏幕命中与 Prefab 合约定向 EditMode | **53/53 passed，0 failed，0 skipped**；任务 `a69e61887bc8441ea49cb38f371bde7a` |
| 全量 EditMode | **122/122 passed，0 failed，0 skipped**；任务 `d3335969ef184c5a9b56682b335931de` |
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | **0 error，12 warnings**；warning 均为既有 Unity/R3/UniTask 依赖程序集版本冲突 |
| `git diff --check` | 自动验证后与文档收口后均通过 |

队首失效使用生产相同的公开 seam：`BattleCommandQueueTests.QueuedPlayCard_WhenEnemyTargetDiesBeforeHead_FailsWithoutMutation` 先让 Enemy 目标在展示等待期间死亡，再让命令到达队首。结果为 `TargetNotAlive`；Turn、卡区、目标 Health 只读对象和死亡后的值均保持原状，能量不扣、卡牌不移动、队列可继续完成。该夹具只写测试内纯运行时事实，没有写回 Scene、Prefab 或配置资产。

## Addressables 本地内容

- 通过已读取的 Unity 菜单项执行 `TinySpire/Addressables/Build Local Content`，构建成功；Editor 日志记录内容构建耗时约 `18.303s`。
- 最新报告：`TinySpire/Library/com.unity.addressables/BuildReports/buildlayout_2026.08.02.01.52.06.json`。
- 报告 `BuildError` 为空，`BuildResultHash=2f21014862b879079e277deb7b7d1cbb`，`Duration=18.4565022s`。
- 报告 SHA-256 为 `DAB7E00D14D845EE805622A2357D13B8E07428AF7D1B01FCB48430C930EAA4C0`；最终 `aa/Windows/settings.json` SHA-256 为 `2BF90AD4013288D5EC0B22D779D5C205B5D12F87513A130855BBBB25D0857FEB`。
- `BattleScene` 继续以完整稳定地址 `Assets/Scenes/BattleScene.unity` 进入本地 catalog。`ParticipantHudView.prefab` 作为该场景 bundle 的静态依赖存在；BattleHand 与目标箭头保持既有静态 Prefab 依赖，不新增独立 Addressables 地址接口。
- 本阶段没有修改 `DataTables/Datas/`、生成 JSON 或 Localization，因此没有运行 Luban 或本地化导入。

## Bootstrap 冒烟

1. 唯一 Unity 6000.5.5f1 Editor 从 `Assets/Scenes/BootstrapScene.unity` 进入 Play Mode，经生产启动链加载 `BattleScene`。
2. 运行时层级包含 5 个 `HandCardVisual`、1 个玩家世界 View、2 个敌人世界 View、3 个 `ParticipantHudView` 与独立 `BattleTargetingArrow`。
3. Console 只记录 `game-config.json 已加载。`，没有 Error、InvalidKey、VContainer 或资源地址无效错误。
4. 本次未启动第二个 Editor、未结束 Unity 进程、未删除锁文件或清理 Library/Temp。

## 真实 Game View 物理验收

M6C 已记录 Self、左右 Enemy、空白/玩家无效释放、16:7/16:9/16:10/16:11/16:14、结束行动/下一轮清理的物理结果；费用不足 transition 修订后，用户又单独确认红色卡可跟手但不进入反馈、瞄准、resolver 或 Submit，释放回弹且权威事实不变。

M6D 复用同一最终生产代码上的累计物理证据，不要求重复一遍已经通过的动作：

- [√] Self 成功提交并按权威结果扣能量、移牌与重排。
- [√] Enemy 箭头端点跟随物理指针，左右合法/悬停高亮对齐并分别完成命中审阅。
- [√] 空白或玩家区域释放不产生提交，卡牌回弹且箭头/高亮清理。
- [√] 结束行动后进入下一轮，手牌/能量恢复且无残留目标表现。
- [√] 16:7、16:9、16:10、16:11、16:14 下箭头端点、目标矩形和高亮不偏移。
- [√] 最终 transition 代码上的费用不足卡仍为红色且可跟手，但不进入反馈、瞄准、resolver 或 Submit，释放回弹且权威事实不变。
- [√] 物理序列与最终 Bootstrap 冒烟的 Console 均无 Error、InvalidKey 或 VContainer 错误。

## Standards / Spec 双轴复审

固定基线为 M5 commit `bbfb650ce9643c470fa59345cba91be26b82420a`。M6 没有 commit，因此复审读取 `git diff --no-ext-diff <base> --` 的 tracked 变化，并逐一读取 `git ls-files --others --exclude-standard` 中的全部 M6 新文件；启动前已有的脏文档只保护，不因其存在本身报 finding。

### Standards 首轮

- **Hard/P2 · 已修正**：M6C 验收页一处费用不足状态仍指向未来复测，与同页已通过结论矛盾。已改为“并已通过下方物理复测”。
- **Judgement/P2 · 不在 M6 抽浅 helper**：`HandCardContainer` 仍同时承载资源、布局、拖拽、目标与 pending。M5 回顾建议的真正提交协作者需要同时收敛 Hand、Turn HUD、Presentation/Queue 的 `Submit → pending → PublishQueued → feedback` 协议；只抽 Hand 会留下第二套协议。该债务已明确排入 ROADMAP M8，与事件驱动、表现描述及 `Queued` 裁决一起处理。
- **Judgement/P3 · 保留局部扫描**：规则、Presenter 与松手 resolver 各自有一段短线性目标成员判断，但三处分别守住规则快照、View 映射与 UI 预览边界。当前没有共享策略或第二消费者，抽通用集合 helper 只会增加浅接口。

### Spec 首轮

**0 finding**。复审确认 `TargetId` 显式迁移、唯一 `Submit` seam、UI/队首共享 `BattleCardPlayRules`、队首目标失效零写入、Self/Enemy、费用不足仅视觉拖动、稳定命中/清理、Prefab 非 Raycast 契约及全部排除项均符合唯一计划；未发现 M7～M9、Scene、CardView、角色 Prefab或 ProjectSettings 越界。

最终文档与物理证据回填后，原两个只读审查者完成收口复核：**Standards 0 finding，Spec 0 finding**，均无 P1/P2。

## M5 回顾意见的谨慎采纳与后期归属

回顾页明确是建议来源而非验收/决策事实源，并要求按里程碑到期清偿。M6 只采纳与当前变更重叠且能在计划内独立验证的部分：

- `BattleCardPlayRules.Evaluate` 已成为 UI 预览与 `TryPlayCard` 队首的唯一出牌校验链，关闭“第三份校验链”风险。
- `TargetId` 的执行期失败承诺已经兑现。
- `HandCardDragTransitionPolicy` 把 `CardZones → Turn` 的同步发布顺序收敛为一份纯 transition 结果，并由真实容器回调消费；这是对 Container 复杂度的窄而可验证的深化，不建立第二份响应式状态。
- 目标命中继续复用 Presenter 唯一 View/HUD 映射，没有新增 Collider、Physics 或第二套参与者注册表。

其余建议按既有后期里程碑处理：M7 承接结算记录、出牌事务、抽牌时序、效果公式、`ApplyDamage` 与 Effect ID；M8 承接队列错误态、事件驱动、表现槽/时长、提交/pending/Queued 协议、阶段屏障与重入；M3E/M9 承接 HUD 绑定、Prefab 契约、装配失败、加载并发及最终反馈；M10 承接配置 fail-fast、表清单和 i18n 构建前校验；G1 承接 Session 的唯一玩家/卡区装配出口。没有提前实施这些建议。

## 范围与工作区保护

- 未修改 `BattleScene.unity`、`CardView.prefab`、角色 Prefab、ProjectSettings、Physics、asmdef、HybridCLR、Luban、Localization、Run 生命周期、网络或启动流程。
- 未执行 `effect_bindings`，未写伤害、格挡、力量、状态、死亡、胜负、奖励或最终动画。
- 只保留计划内功能性费用颜色、箭头、高亮、命中与回弹；LXX-6 已完成独立美术资源交付，最终切片/缩放契约、轨迹和生产接线仍由 M9 承接。
- Linear 回复已验证四张 PNG 的文件名、尺寸、RGBA、透明中心与交付一致性并把 Issue 标记为 Done；Unity 后续为工作区文件生成了未跟踪 Meta。本次没有把这些资源接入 Prefab、纳入 Addressables/物理验收或 M6 提交，避免把 M9 资源契约提前固化到 M6。
- 未 commit、未 push，也未清理或覆盖 Goal 启动前已有改动。
