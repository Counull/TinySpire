---
title: 机枪兵单场战斗内容接入
page_type: plan
lifecycle: superseded
created: 2026-08-06
updated: 2026-08-06
scope: 机枪兵作为新增候选 Hero 的单场战斗规则、卡池与配置接入；不含 Run 或奖励界面
status: superseded-by-2026-08-07-marine-game-card-only-integration
status_source: ../SESSION_LOG.md
source: ../00_inbox/卡牌设计-机枪兵.json
superseded_by: 2026-08-07-marine-game-card-only-integration.md
depends_on:
  - 2026-08-06-sts2-v01071-ironclad-card-pool.md (I5-I11 shared capabilities)
---

# 机枪兵单场战斗内容接入

> 本文是实施 proposal，不是已通过的玩法规格、更不是运行时证据。只有用户逐切片确认后才可实施；当前状态以 `../SESSION_LOG.md` 为准。

## 1. 目标、来源与硬边界

目标是在不破坏默认战士和 M10 黄金基线的前提下，把机枪兵做成一个**新增的、可独立装配的单场战斗 Hero**：拥有自己的基础数值、12 张初始牌组、战斗内资源与状态，并按已确认的通用机制逐张翻转为 `Implemented`。

唯一输入来源是 [`../00_inbox/卡牌设计-机枪兵.json`](../00_inbox/卡牌设计-机枪兵.json)。它是对话产出的 `source-only` 设计稿，不能直接作为运行时 JSON 加载，也不能因本计划被视为已确认的表格、数值或美术事实。

本计划默认采用以下边界；若策划选择不同产品形态，须先回到 MG0 更新本计划：

- 机枪兵是新增 Hero，不替换当前默认 Hero `1001`；M10 的 3 能量、5 手牌、默认战士及其卡组保持不变。
- 只覆盖一场 BattleScene 内的战斗。`reward_pool_rules` 仅记录未来候选池，奖励生成、选牌、跳过、存档与跨局升级属于路线图 G4，未纳入本计划的实施范围。
- 共享权威写入仍只经过 `BattleCommandQueue.Submit`。`Queue`、`Turn`、`BattleSession` 与 `CardZones` 不增加公开写入口或第二份镜像事实。
- 卡牌程序必须在首次权威写入前完整解析、投影与校验；失败时能量、弹药、卡区、参与者和随机流都不得发生写入。
- 运行时不得按卡牌 ID、`external_key`、卡名、本地化文本或原始 JSON 字段名分支；不能把策划 JSON 当作解释器脚本。
- 不实施 Run、主菜单、角色选择、地图、奖励 UI、存档、多人、网络或命令执行中途选择；`DEP-008`、`DEP-010`、`DEP-011` 的边界保持不变。
- 不触碰 `TinySpire/Assets/Arts/Runtime/UI/Battle/Targeting/`、`Docs/Hermes_Pegasus/art/.../candidates/`、`TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates/` 及其 Meta。场景、Prefab、ProjectSettings、asmdef、HybridCLR、DI/启动流程也不属于 MG0--MG6 的默认改动范围。

## 2. 已知事实与不能混淆的口径

### 2.1 设计稿明示的内容（source-stated）

| 项目 | 设计稿内容 | 当前处理 |
|---|---|---|
| Hero 基础 | 70 HP；能量上限 5、每回合 +3、跨回合保留；弹药上限 5、首回合满、每回合 +1、跨回合保留；回合开始清 Block；每回合抽 5 | 需要 MG0 冻结精确公式，不能直接覆盖全局 `GameConfig` |
| 初始牌组 | 5 个模板、12 张实例：`shoot`×4、`elbow`×1、`block`×5、`reload`×1、`stim`×1 | 仅在五张都真实 `Implemented` 后才可接入正式 Deck |
| 奖励卡 | 23 张：7 Power、10 Skill、6 Attack；奖励池排除 starter | 先作为目录/需求清单；奖励流程留给 G4 |
| 状态 | `strength`、`weakness`、`burn`、`oil`、`smoke`、`armor`、`stim` | 除 Strength 外均缺少当前可执行的完整模型 |
| 升级 | 仅明示 `core_expansion`、`output_adjust`、`mag_expansion`、`spray`、`gas_pump` 五张 | 其余 18 张奖励卡及所有 starter 的升级/无升级策略未给出 |
| 特殊规则 | `incomplete_combustion.effect` 只有自然语言；奖励池只给 pool IDs | 都不能直接转为程序，须在 MG0 结构化补齐 |

设计稿的 `damage_pipeline` 是 `weakness_percent → smoke_flat → block → hp`。它尚未说明如何与项目既有 Strength 和 Vulnerable 合并，不能在实现时自行补出顺序。

### 2.2 当前代码/配置观察到的事实（code-observed）

- 当前默认战斗使用全局的每回合 3 能量；Hero 没有角色专属能量/弹药档案，也不支持能量上限、保留或弹药。
- 当前参与者事实仅覆盖 Health、Strength、Block、Vulnerable；现有效果仅覆盖 Strength、伤害、Block、Vulnerable。HUD 也没有弹药及其余机枪兵状态的权威投影。
- 当前 `PlayCardCommand` 只有一个可选 `TargetId`。虽然 `TargetRule` 已声明 `AllEnemies` / `RandomEnemy`，`BattleCardPlayRules` 实际只接受 Self / Enemy；Power 归宿当前在首次写入前失败。
- 洗牌随机流与敌人意图随机流已隔离。随机射击需要独立的“卡牌执行”随机域，不能借用前两者或 Unity 全局随机。
- 构建校验禁止 Deck 引用 `CatalogOnly` 卡，因此不能先把未实现初始牌塞入新的正式 Hero/Deck。

**虚弱不是易伤。** 机枪兵的 `weakness` 是“造成攻击伤害 -25%，再结算格挡；每回合 -1 层”，不能映射到现有 `ApplyVulnerable`。现有 `Vulnerable/易伤` 是目标承受攻击伤害 ×1.5 的独立机制；两者的共存顺序、取整及最小值必须在 MG0 明确。

## 3. 设计归一化必须先补齐的事项

MG0 的产物是一份可追溯的“卡牌—机制原语—时机—未决项”需求矩阵，而不是 C#、Luban 表或可播放卡牌。以下问题任一未决时，不得开始生产实现：

1. 机枪兵是否确定为新增非默认 Hero；Hero/Deck 的稳定表 ID、大小写精确的 `external_key`、初始牌与现有目录的冲突检查规则是什么？源内 `block` 等 ID 只是源局部标识，不可直接复用。
2. 首回合能量的初始值；能量/弹药在上限升降时是否裁剪当前值；“每回合 +3/+1”发生在何时；`energy_persists_across_turns` 的准确含义。
3. `draw_per_turn: 5` 是“抽 5 张”还是“补到 5 张”；与初始抽牌、手牌上限、抽牌堆重洗、死亡和满手的关系。
4. 最近、第二近、随机目标、全体目标的稳定排序；随机命中是否可重复；目标死亡后剩余段数如何跳过；AOE 的快照/逐目标结算顺序。
5. Strength、Weakness、Smoke、Vulnerable、Block、HP 的完整攻击伤害管线、每步取整和最小值；Debuff 伤害、主动失血是否绕过 Smoke/Block。
6. Burn 的双方触发顺序与死亡中止；Oil 对 Burn 的快照、减半、一次触发；Napalm“同时施加”以及不充分爆燃“不触发浸油”的精确定义。
7. Armor 的“破防攻击每段 -1”定义；Stim 对多段、随机与 X 费射击的触发次数、弹药不足与免费额外射击的顺序。
8. `overload`、`retreat`、`wild_rampage` 的延迟效果、免费段、X 值冻结、零资源、终结回合和表现屏障口径。
9. 所有未声明升级的卡明确写为“无升级”或给出升级值；中英文名称、说明、关键词文本与 28 张项目自有插画短键/交付状态。
10. 奖励生成时机、稀有度权重、候选数量、重复、跳过、保存与升级实例语义。这些只为未来 G4 预留，不在机枪兵单战斗切片中伪造。

## 4. 共享机制依赖与内部接口提案

机枪兵与 [`2026-08-06-sts2-v01071-ironclad-card-pool.md`](2026-08-06-sts2-v01071-ironclad-card-pool.md) 是不同来源、不同卡池的计划，但应复用同一套通用能力，禁止为机枪兵复制执行器或写链。

| 机枪兵需求 | 共享前置 | 计划中的接入原则 |
|---|---|---|
| 一次完整出牌事务、每步目标、原子失败 | Ironclad I5 | 在同一个深卡牌执行 module 中完成“解析 → 投影/预构建 → 校验 → 提交” |
| 最近/第二近/全体/随机/重复命中 | Ironclad I6 | 分离玩家提交目标与程序步骤选择器；保持确定性随机域 |
| 抽牌、满手、区域选择 | Ironclad I7 | 复用唯一 CardZones 事实，不保存第二份 Hand |
| X 费、能量增减、临时/延迟资源变化 | Ironclad I8 | 用强类型资源规则表达 Energy/Ammo，不开字符串字典或卡牌特例 |
| 升级实例 | Ironclad I9 | 升级属于实例事实，不能回写静态模板 |
| Power、Modifier、命令内触发 | Ironclad I11 | 活动 Power 必须有唯一归属和稳定顺序，不建立全局事件总线或第二动画队列 |

MG0 必须把下列接口提案交由确认后再实施：

- `BattleCardProgramExecutor` 仅由现有 Turn/Queue 路径调用，负责整张牌的预构建与一次性提交；旧 `effect_bindings` 也应适配到同一条执行路径，不能长期并存两套卡牌写链。
- “玩家输入目标”只表示 `SubmittedEnemy` / Self / 无输入；`NearestEnemy`、`SecondNearestEnemy`、`AllLivingEnemies`、`RandomLivingEnemy` 是程序步骤选择器。两者不得互相伪装。
- `ResourceKind(Energy, Ammo)`、状态事实和状态时机是通用的强类型规则，而非机枪兵专属字段。UI 只读投影，不保存资源或状态副本。
- 若 Power 需要停留，应进入唯一的 Power 区或等价的单一归属模型；“活动 Power”只能从该归属和静态程序派生，不能另存一份可变列表。

## 5. 串行实施切片

每片都要从自己的精确红灯开始。任何片失败、需求变更或计划外影响时，停止在该片，不推进下一片；回滚只回滚本片明确审核的源/表/测试/文档，不触碰无关工作区改动。

| 切片 | 独立交付物 | 依赖 | 红灯 / 验收 seam | 明确停止范围 |
|---|---|---|---|---|
| MG0（已完成） | 设计归一化与需求矩阵；见 [`../01_requirements/2026-08-06-machine-gunner-card-design-digest.md`](../01_requirements/2026-08-06-machine-gunner-card-design-digest.md) | 无 | 28 个源模板均能映射到“机制原语 / 目标 / 时机 / 升级 / 文本 / 素材 / 未决项”；任何未决项不得伪报为默认规则 | 只写文档；不改表、代码、Hero/Deck 或 `ImplementationStatus` |
| MG1（已完成） | 新增 Hero 的战斗资源档案 | MG0 已确认资源公式 | Hero 静态档案、每玩家资源事实、首回合初值、后续 capped 补充、降低上限即时裁剪与 Hero 1001 的 3 能量/补至 5 回归均已由权威战斗路径验证 | 未接卡、未新增默认或可选 Hero、未改 UI/Prefab，未新增公开写入口 |
| MG2 | 单一深卡牌执行 module | Ironclad I5；MG1 | 任何后续步骤不能预构建时，能量、弹药、卡区、参与者和卡牌随机流零写入；固定费用/自身/已提交单体步骤按 Queue 提交 | 不准 `switch(cardId/name)`、不准第二 Effect 链；初始牌还不因本片自动入 Deck |
| MG3 | 自动、多目标与随机步骤选择器 | Ironclad I6；MG2 | 最近/第二近遵循 Encounter 稳定顺序；AOE 顺序稳定；同种子随机结果一致；预构建失败不推进卡牌随机流 | 不做命令中途选择；不复用洗牌或敌人意图随机流 |
| MG4 | 状态、攻击伤害管线与时机 | MG0 的状态口径；MG2--MG3 | Weakness → Smoke → Block → HP 的已确认攻击链可复现；Oil/Burn 单次触发；Napalm 同时施加不触发 Oil；Armor 仅在真实破防段后消耗 | 保留 Strength/Vulnerable 的旧行为并有回归；状态判断不能散落进具体卡牌分支 |
| MG5 | 抽牌、X 费、延迟效果与结束行动 | Ironclad I7/I8；MG1--MG4 | Stim 抽牌/持续、X 值冻结且仅支付一次、Retreat 同一出牌事务结束行动；没有递归 `Submit` | 不实现奖励、升级选择或全局资源写入口 |
| MG6 | Power 区、Modifier 与命令内触发 | Ironclad I11；MG4--MG5 | Power 的归属、顺序、叠层、战斗结束清理和触发记录稳定；Stim、燃烧弹药、Armor 等走同一触发路径 | 不建立全局事件总线、第二命令队列或第二动画队列 |
| MG7 | 按能力分批翻转卡牌与完整初始牌组 | MG1--MG6 的对应能力 | 每张 `Implemented` 卡都有 Queue、只读事实、settlement、文本、素材/Addressables 和真实 Game View 证据；五张 starter 均通过后才接机枪兵正式 Deck | 奖励卡可继续 `CatalogOnly`；不把占位素材称为最终美术 |
| MG8 | 升级与奖励流程的后续门槛 | Ironclad I9；路线图 G4；MG7 | 升级实例、候选生成、重复/跳过/保存各有独立规格、随机域与验收 | 本计划不实施 RunState、地图、奖励 UI、存档或角色选择 |

### MG7 的内容分批清单

下表只描述“应由哪类机制解锁”，不是把卡牌一次性设为 `Implemented` 的授权。

| 能力组 | 源卡牌 ID |
|---|---|
| 基础资源/自身/单体 | `shoot`、`block`、`reload`、`quick_elbow`、`bayonet_parry`、`tumble_reload` |
| 最近、第二近、全体、随机与重复 | `elbow`、`stun_grenade`、`gas_pump`、`napalm`、`knockback_shot`、`spray` |
| Burn / Oil / Smoke / Weakness / Armor | `molotov`、`smoke_bomb`、`blast_shield`、`incendiary_ammo`、`smoke_persist`、`incomplete_combustion` |
| 抽牌、X 费、延迟与结束行动 | `stim`、`overload`、`retreat`、`hold_line`、`wild_rampage`、`hurricane_elbow` |
| Power / Modifier | `core_expansion`、`output_adjust`、`mag_expansion`、`kungfu_mech`，以及上表中有持续触发的 Power |

`incomplete_combustion` 在 MG0 把自然语言效果拆成可验证步骤前不得翻转；`wild_rampage` 的“免费兴奋剂射击”也必须先有已确认的触发/资源顺序。

## 6. 预期文件范围与生成规则

实施获准后，每片只审查其所需的最小集合。预期候选范围包括：

- 规则与测试：`TinySpire/Assets/Scripts/Battle/Commands/`、`Turn/`、`Effects/`、`BattleCardZonesData.cs` 及相邻的 EditMode/PlayMode 测试；所有共享写入仍走 `BattleCommandQueue.Submit`。
- 静态内容：`DataTables/Datas/battle.*.xlsx`、Luban 生成的 C#、`TinySpire/Assets/GameData/`、本地化作者表和项目自有卡图短键。修改作者表后必须运行 `DataTables/gen.bat`，再运行 `TinySpire/Build/Sync and Build All`。
- 素材：只接受项目自有或用户明确授权的素材；业务字段使用无目录、无扩展名、大小写精确的 `*_key`，并通过专用 Addressables 逻辑地址加载。新增或修改素材域时还需 Packed/Player 的 AssetBundle 真实加载证据。
- 文档：每片完成后更新 `SESSION_LOG.md`、相应 `CODE_DECISIONS.md`、验收页和本计划；若机制方案发生变化，先更新本计划再实现。

不在上述范围内的 Scene、Prefab、ProjectSettings、asmdef、HybridCLR、启动/DI、受保护 Targeting/Candidates/Hermes 美术，必须由后续独立切片和明确授权处理。

## 7. 验收矩阵与完成定义

| 层次 | 每个实施切片必须提供的证据 | 不能替代的内容 |
|---|---|---|
| 纯规则 | 精确红灯、最小实现后的单元/编辑器测试；失败零写入、顺序、取整、随机复现、死亡跳过 | 不能只看 UI 或日志 |
| 配置/构建 | 表格变更后的 Luban、`Sync and Build All`、卡牌目录/Deck/文本/素材门禁 | 不能只刷新 AssetDatabase |
| 资源 | 使用最新 BuildLayout 证明短键映射和 AssetBundleProvider；Packed/Player 真加载且无 InvalidKey | Fast Mode 不能作为 AB 加载证据 |
| 集成 | `BattleCommandQueue.Submit`、只读 Queue/Turn/CardZones、settlement 与单场 BattleScene 冒烟 | 不能以第二份测试状态绕过权威链 |
| 可见玩法 | 每张翻转卡牌的真实 Game View、控制台、输入/目标/结束行动路径证据 | 主观观感不是规则或性能通过 |
| 文档 | 状态日志、决策、计划、验收页及相对链接校验 | 文档检查不等于 Unity/Luban/运行时验收 |

“录入”与“可玩”必须分开报告：

- 录入：卡牌仅具有稳定目录身份、双语 key、素材 key、升级声明和结构化程序；若机制未完成则必须保持 `CatalogOnly`。
- 可玩：该卡涉及的资源、选择器、状态、触发器和归宿都已经通过上表的完整证据，才可翻为 `Implemented` 并进入正式 Deck。
- 机枪兵单场战斗完成：五张 starter 可独立完成真实 BattleScene 战斗，所有已承诺的奖励卡机制、升级声明和素材状态均有可追溯证据；奖励获取和跨局内容仍不因此宣称完成。

## 8. 当前停止点

MG0 已完成并产出 [`../01_requirements/2026-08-06-machine-gunner-card-design-digest.md`](../01_requirements/2026-08-06-machine-gunner-card-design-digest.md)。用户随后确认 R1：首回合 Energy 为 3；R2：资源上限降低时当前值立即取 `min(current, max)`；默认抽牌数仍为 5，机枪兵复用当前“补至 5”基线。

MG1 已完成：`battle.hero.xlsx` 增加 Energy/Ammo 的初值、上限和后续回合增量字段；Hero `1001` 明确为 Energy `3/3/+3`、Ammo `0/0/+0`。生产队列由 `BattleSession` 装配冻结的每玩家档案，首回合使用 `initial_*`，后续回合按当前事实的 `min(current + gain, max)` 补充；`PlayerTurnData` 在重建时立即裁剪降低后的上限。回合顺序保持清 Block → Energy → Ammo → 补至 5；`GameConfig.InitialHandCount` 继续是共享抽牌事实，现有 HUD 在只运行 Hero `1001` 时仍正确。详细证据见 [`../06_testing/2026-08-06-machine-gunner-mg1-hero-resource-profile.md`](../06_testing/2026-08-06-machine-gunner-mg1-hero-resource-profile.md)。

MG1 没有新增 Hero `1002`、Deck、卡牌、状态、UI、Prefab、素材或 Addressables 业务资源；机枪兵尚不可选择或游玩。下一次生产实施只能在单独授权后进入 **MG2**，并仍须遵守其卡牌事务原子性和不按卡牌 ID/名称分支的停止范围。
