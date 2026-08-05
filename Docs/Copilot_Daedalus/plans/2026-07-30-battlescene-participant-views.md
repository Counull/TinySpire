---
title: BattleScene M3A 参与者视图与生命 HUD
page_type: plan
lifecycle: superseded
date: 2026-07-30
updated: 2026-08-05
scope: battle.Hero、battle.Enemy、i18n.xlsx、Addressables、BattleScene 参与者视图
source: 用户确认的 M3 · BattleScene 主 HUD 与参与者视图 grilling
status_source: ../SESSION_LOG.md
---

# BattleScene M3A 参与者视图与生命 HUD

> 本计划保留 M3A 当时采用 `view_prefab_address` 与完整 `Assets/...prefab` 的历史事实，已不再是当前实施口径。角色配置现使用 `view_prefab_key` → `character-view/{key}`，构建期与 Packed 验收规则见 CD-055、根 `AGENTS.md` 和 `../06_testing/2026-08-05-config-asset-logical-keys.md`。

## 当前结论

M3 不一次性制造完整 HUD，而是按运行时事实的依赖拆分。当前开始的是 M3A：把已有的 `BattleSession.Combatants` 可视化为一个玩家和一至三名敌人，并展示名称、生命和力量。它不增加格挡、状态、意图、能量、回合或胜败的占位状态。

`CombatantData` 已经是参与者的唯一运行时事实：`CombatantId`、`TemplateId`、`CurrentHealth`、`MaxHealth` 与只读 R3 的 `Health` / `Strength`。View 只保存自己的 `CombatantId`，按 ID 查询或订阅事实，不能复制数值到自己的可变字段。

## 静态数据与资源

`battle.Hero` 与 `battle.Enemy` 均新增：

```text
name_i18n_key       // 静态名称的 i18n key
view_prefab_address // 完整、稳定的 Addressables Prefab 地址
```

现有裸 `name` 文本迁移为 `name_i18n_key`。首批条目使用：

| 模板 | 名称 key | Prefab Addressables 地址 |
|---|---|---|
| Hero 1001 | `battle.hero.test_warrior.name` | `Assets/Arts/Runtime/Character/Prefabs/pfb_char_player.prefab` |
| Enemy 2001 | `battle.enemy.test_slime.name` | `Assets/Arts/Runtime/Character/Prefabs/pfb_char_enemy.prefab` |

名称翻译进入既有 `DataTables/Datas/i18n.xlsx` 的 `i18n` sheet，并复用当前 Unity Localization String Table。`smart` 为 `false`。运行时仍经 `LocalizationService` 读取 Unity Localization；Excel 只是在编辑期导入，不成为第二条运行时加载链路。

`pfb_character` 是角色共用基础 Prefab。`pfb_char_player` 沿用其当前的默认 `t_char_sisypjus1_trans` Sprite；`pfb_char_enemy` 使用当前 Warden Sprite。Prefab 及其依赖资源必须编入本地 Addressables 内容。以后角色需要不同贴图、动画或结构时，新增/替换对应 Prefab 并更新表中的地址，不在 UI 代码内按模板 ID 写 `switch`。

## 场景生成与释放

新增场景组件 `BattleParticipantPresenter`，由 `BattleLifetimeScope` 注入 `BattleSession`、`ConfigService` 与本地化服务。组件在 `Start` 后：

1. 从 `BattleSession.Combatants.All` 获得参与者；玩家和敌人的筛选仅在生成时按运行时类型派生，不能成为新的领域集合事实。
2. 依据 `TemplateId` 查询 Hero / Enemy 模板的 `view_prefab_address`。
3. 以 `Addressables.InstantiateAsync` 加载并实例化 Prefab：玩家放到 `PlayerAnchor`；敌人放到 `EnemyAnchor`。
4. 组件只维护 `CombatantId -> 场景 View` 的生命周期映射，不保存生命、力量或阵营的镜像数据。
5. 在场景/组件销毁时，对每个实例调用 `Addressables.ReleaseInstance`；R3 订阅随各 HUD View 销毁。

任一配置地址为空、Addressables 条目缺失、加载失败或 Prefab 缺少 `SpriteRenderer` 时，都直接抛出包含 `CombatantId`、模板 ID 与地址的异常。M3A 不提供占位角色、重试、静默跳过或随机回退；未来统一错误呈现/遥测系统可以接收该异常。

M3A 明确只支持一名玩家和一至三名敌人。敌人顺序来自 `Encounter.enemy_template_ids` 的配置顺序，而非 `Dictionary.Values` 的枚举顺序；在 `EnemyAnchor` 下自右向左等距排列。超过三名敌人是明确的配置错误。

## HUD 与绑定

角色 Prefab 是世界空间 `SpriteRenderer`；HUD 是现有 Screen Space - Camera Canvas 下的 UGUI 面板。每个参与者具有一个 HUD View：

- 名称在角色头顶；由 `name_i18n_key` 和当前 locale 派生，订阅 locale 变化后刷新。
- 生命条及 `当前生命 / 最大生命` 在角色脚下；订阅 `Health`。
- 力量在非零时显示于生命条旁；订阅 `Strength`，值为零时隐藏。
- HUD 在 `LateUpdate` 将世界角色位置投影到 Canvas，因此后续角色移动不改变数据绑定方式。

生命变为零时，本切片只把数值刷新为 `0 / MaxHealth`，角色和 HUD 都保留。不根据生命差值猜测死亡、受击数字或其他一次性结算事件；死亡动画、隐藏/销毁、输入锁定与胜败覆盖层等待明确的结算记录和后续阶段。

## 任务拆分与验收

### M3A-1：模板、翻译与资源包

- 修改 Hero / Enemy Luban 定义、Excel 数据和 `i18n.xlsx`。
- 让本地化导入/校验覆盖参与者名称 key。
- 将两份角色 Prefab 标记为 Addressables，运行 `TinySpire/Build/Sync and Build All`。
- 验收：生成 JSON 中包含两字段；两种 locale 都能读取名称；本地 catalog 含 Prefab，且无无效地址。

### M3A-2：参与者 Prefab 工厂与布局

- 实现 `BattleParticipantPresenter`、Prefab 实例化、稳定的敌人布局与 `ReleaseInstance`。
- `BattleSession` 显式保存 `EnemyCombatantIdsInEncounterOrder`：它由 `Encounter.enemy_template_ids` 在实例化时产生，不能从参与者字典的枚举顺序反推；它只记录布局/后续敌方行动需要的既定遭遇顺序，不镜像参与者数值。
- 为地址/加载失败、敌人稳定排序和三人上限写定向测试。
- 验收：修改遭遇表为两名敌人后，不改场景层级即可生成两个独立 Prefab，位置与配置顺序稳定。

### M3A-3：参与者 HUD

- 制作 HUD View Prefab/组件，绑定名称、生命和力量并投影跟随世界对象。
- 为 Health、Strength 与 locale 的刷新写定向测试。
- 验收：启动 BattleScene 后，玩家和敌人的名称、生命条/数值正确；力量为零时隐藏；修改运行时生命后只有对应 HUD 更新。

### M3A-4：场景接入与人工验收

- 注册场景组件，完成 Bootstrap -> BattleScene 实跑。
- 验收：Addressables 本地内容重建后，运行时从配置加载玩家与敌人，Console 无 InvalidKey、无资源地址错误；组件销毁后不残留实例或订阅。

## M3 后续切片

| 切片 | 内容 | 前置事实 |
|---|---|---|
| M3B | 抽牌堆/弃牌堆计数 HUD | M2 的卡区布局 |
| M3C | 能量与结束回合操作区 | M4 的回合数据 |
| M3D | 敌人意图 HUD | M5 的意图与行为数据 |
| M3E | 格挡、状态、死亡表现、回合提示、胜败覆盖层 | M7-M9 的效果与结算记录 |

这些切片均不得预先在 M3A 建立可变的 UI 状态或空槽数据。
