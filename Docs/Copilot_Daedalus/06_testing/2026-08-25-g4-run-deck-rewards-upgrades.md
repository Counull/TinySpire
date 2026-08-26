---
title: G4 RunDeck、普通战斗奖励与多级升级验收记录
page_type: testing
lifecycle: active
date: 2026-08-25
updated: 2026-08-26
scope: G4 only
status_source: ../STATUS.md
source: ../plans/2026-08-25-g4-run-deck-rewards-upgrades.md
implementation_status: verified
---

# G4 RunDeck、普通战斗奖励与多级升级验收记录

## 1. 当前结论

G4-A～D 已完成并在本轮取得完整证据，G4 整体标记为 `verified`。本轮停在 G4，没有进入 G5，也没有修改 Scene、Prefab、asmdef、ProjectSettings、HybridCLR 或 DI 架构。

已交付的玩家闭环是：新 Run 只展开一次有序 RunDeck；普通战斗胜利冻结三张 Hero 专属奖励；选择一张会追加新的独立 RunCard 实例，跳过不改牌组；读档、语言切换、场景重建和冷启动不会重抽；下一场 Battle 继续保留实例身份、顺序与升级等级。四张生产卡同时覆盖有限与无限升级轨道，并由同一等级投影驱动文本、费用、归宿和真实规则执行。

最终门结果：

- Unity EditMode job `7cad4b02d38248f298227ea06804c949`：**1093/1093 passed，0 failed，0 skipped，16.2256886s**。
- Rider build session `07b40384-6749-4cfa-ac8c-b5f8bd4f9cee`：`Completed / buildIsSuccess=true / 0 problems`；errors-only 项目检查为 0。
- `TinySpire/Build/Sync and Build All` 成功；最近一次 Addressables 构建 15.457 秒，输出 `Library/com.unity.addressables/aa/Windows/settings.json`。
- 最新 BuildLayout `BuildError` 为空；G4 GameData、Localization 依赖与 RunEntry/Battle/Loading 场景均由 `AssetBundleProvider` 打入 bundle。
- Packed Play 双 Hero 产品链完成，所有产品检查点 Console Error=0，且没有 `InvalidKey` 或配置初始化失败。

## 2. 串行停止点结果

### G4-A · RunDeck、实例身份与迁移

- `RunCardInstanceId` 与 battle-local `CardInstanceId` 明确分离；同模板副本拥有独立跨战身份。
- RunCard 只保存实例 ID、TemplateId 与 UpgradeLevel；费用、文本、伤害与归宿不进入 Run save。
- schema v4 canonical 保存 ordered RunCards；v2 经合法 deck template 展开一次，v3 无损迁移，v1 继续 fail-fast。
- legacy Continue 会在发布 active Run 前先耐久 canonicalize，避免 legacy live 与首战 reward intent 在崩溃恢复时冲突。
- Battle setup 防御性复制 RunDeck；卡区、临时卡、敌人、队列、动画与 BattleSession 不回写 RunDeck 或 Run save。
- G4-A 最终聚合 job `c210e4b045aa454780e22a38d02e9445`：**120/120**。

### G4-B · 双 Hero 奖励池与冻结 Pending

- Hero 1001/1002 分别配置 12/76 张互不共享的 Implemented、非 Basic、Common/Uncommon/Rare 候选。
- 构建期拒绝少于三张、重复模板、Basic、非法 rarity、CatalogOnly、缺失模板和跨 Hero 内容。
- Common/Uncommon/Rare 使用无状态 `60/37/3` 权重；每个奖励独立，不保存或更新保底计数。
- Reward seed 由独立随机域纯派生，generator 每次使用局部随机流，不推进 Map 或 Battle 随机状态。
- 同页三张模板不同；RewardId、顺序和候选只生成一次并写入 schema v4 Pending。
- 最终定向 job `29bb1f63a33f432695d7ef6833a1c0f9`：**30/30**。

### G4-C · 选择/跳过与原子闭环

- RunEntry 奖励页只渲染 Pending projection，并提交稳定 RewardId 与候选 TemplateId；重复 Render 不叠加监听。
- 选择和跳过均采用 prepare → durable save → publish；失败保持同一 Pending 与同一后继，重试不重复生成实例。
- 选择追加 `max(instanceId)+1` 的新实例；跳过逐卡保持原牌组；重复、过期或伪造命令均零写入。
- reward intent 能在中断后恢复，且 live/intent 的实例、顺序、等级与候选必须完全一致。
- 下一战以 `OriginRunCardInstanceId` 证明选择所得实例进入真实 Battle 牌组，而不是只证明同模板出现。
- 最终定向 job `4ba5196358a94341901a228f7ad61ec2`：**35/35**。

### G4-D · 有限/无限生产升级

| Hero | 生产卡 | 轨道 | 已验证投影与执行 |
|---|---|---|---|
| 1001 | Strike 3002（Basic） | 有限 | L0 伤害 6 → 唯一 L1 伤害 9，费用 1；通用 Effect 实际执行 9。 |
| 1001 | Bludgeon 3123（Uncommon） | 无限 | 伤害 `32 + 10 × level`，L1=42、L2=52；同模板不同实例各按自身等级执行。 |
| 1002 | Shoot 3201（Basic） | 无限 | 程序伤害 `6 + 3 × level`；MachineGunner runtime 读取当前 Run 实例等级。 |
| 1002 | OutputAdjust 3207（Uncommon） | 有限 | 唯一 L1 费用 0；Power 规则与 PowerPile 归宿保持不变并真实执行。 |

- 有限轨道按 `CardId + NextUpgradeLevel` 查询连续显式配表；无限轨道只允许一条类型化 DamageValue 增量，没有有限后接无限尾巴。
- Store 每次只把一个合法实例升一级；有限升满、无轨道、溢出和伪造实例均拒绝且零写入。
- 同一 `BattleCardLevelProjection` 驱动文本参数、费用、归宿、合法性、通用 Effect 与 MachineGunner Program。
- Implemented 状态和 effect 引用先于等级投影验证；缺失/非法基础 effect 仍返回稳定的 typed failure，不会被升级投影掩盖成 Queue fault。
- 最终定向 job `d9d5a6efa72348df8cfb1a52d5bea13a`：**258/258**。

## 3. RED → GREEN 原始证据

| 范围 | RED | GREEN / 回归 |
|---|---|---|
| G4-A RunDeck 与 schema | `d41f395b94b343469e1363ce7c198d06` 证明旧 schema 仍为 2；`01cc53dbf5914e05bb99fc700186c9c3` 证明 v2 尚无迁移；`2b353e13f9224f10b76bd44db5b980f1` 证明 legacy deck 缺卡会漏过恢复 | `919001193a5b43a8afc6613ce2d281d9` 1/1、`d93535162ef64e38965389f2c0942482` 3/3、`2470f01dddc9430bb0a209deef2fd3f1` 1/1；最终 `c210e4b045aa454780e22a38d02e9445` 120/120 |
| schema v4 / Pending 持久化 | job `1259e1260b864e7a80c0cc986f6714d7`：新增的 8 个期望用例全部按缺失行为失败 | job `cad36e470501464799568147ce945ae5`：8/8 |
| 升级内容角色门禁 | job `803b22d77f42487cb394b0c7a779f3d5`：一个生产角色组合按预期失败 | job `316776abfe0c43218e166792b5b11767`：5/5 |
| Effect 引用与等级投影顺序 | job `e4b3401376ff4d868f17672adb832038`：有限升级卡缺失 effect 时预期 typed failure，实际为 Faulted | job `6284f3df572746c5ba6c8f106ebf96ee`：相关 8/8；D 聚合 258/258 |
| i18n 等级文本 | 首次完整回归暴露 Strike 升级说明仍固化旧值 | 修正四张生产卡的参数化文本后，job `e5079d5bab01494b83483bc9eabf639d`：1/1 |

G4-B/C/D 的最终停止点分别是 30/30、35/35、258/258；每一站都在进入下一站前取得 Unity 原生定向结果，没有把失败积到最后。

## 4. 生产 GameData 与产品闭环

### 自动化生产数据闭环

`RunG4ProductionAcceptanceTests.BothProductionHeroes_ColdRestoreFrozenRewardAndDrawSelectedInstanceNextBattle` 使用生成后的两名 Hero 与生产卡表依次验证：新 Run、首战 setup/bind、胜利、三张不同且属于当前 Hero 的候选、新 Store/Flow 恢复同一 RewardId/顺序、选择新增实例及下一战按 origin 抽到。job `614adafdcec0456088074214dbc85f98`：**1/1**。

该测试是生产 GameData 驱动的 Editor 领域证据；真实 Addressables、场景、UI 和磁盘存档证据由下面的 Packed Play 补足。

### Hero 1001 · 选择路径

- Packed Play 从 Bootstrap 经 Addressables 进入 RunEntry，UI 创建 Hero 1001 新 Run并选择首个普通战斗节点。
- 胜利由生产 `BattleCommandQueue`、真实 BattleSession、卡区与 Effect 链产生，没有直接伪造 BattleResult。
- 奖励冻结为 RewardId `d0b28ccce6d24e369e22ad90f808d99a:1:L01-S00`，候选顺序 `3116,3108,3125`，三张不同，初始牌组为 10 张。
- 语言从 `zh-CN` 切到 `en` 后 RewardId/候选不变；停止 Play、重新从 Bootstrap 启动并点击 Continue 后仍是同一 RewardId 与顺序，证明语言刷新、场景重建、读档和冷启动没有重抽。
- UI 点击候选 0 后牌组 10→11，只新增 `RunCardInstanceId=11 / TemplateId=3116 / L0`。
- 下一场真实 BattleSession 有且仅有一个 origin=11 的实例；它先位于 DrawPile，调用实际卡区 Draw 后唯一进入 Hand，模板和等级仍为 `3116 / L0`。

### Hero 1002 · 跳过路径

- 使用同一 Packed Play 产品链创建 Hero 1002 新 Run并完成真实普通战斗。
- 奖励冻结为 RewardId `74c0d24ca0c649f89b04f3bc0882b924:1:L01-S00`，候选顺序 `3218,3213,3274`，三张不同，初始牌组为 12 张。
- 停止 Play、重新从 Bootstrap 启动并点击 Continue 后 RewardId 与顺序完全一致。
- UI 点击 Skip 后回到 MapReady；12 张 RunCard 的实例 ID、模板、等级和顺序逐项相同。
- 下一场 BattleSession 的 12 张卡与 RunDeck 顺序完全一致，12 个 origin 全部唯一；跳过没有生成隐藏实例或补偿。

以上 UI 点击均通过运行中的实际 UGUI Button 触发 Presenter command；地图和场景切换走生产 SceneFlow。两个产品链的 Console Error、`InvalidKey` 与配置失败查询均为 0。

## 5. 配表、生成与 Addressables

- Spreadsheets 流程修改并回读了 `battle.hero.xlsx`、`battle.card.xlsx`、`battle.card_upgrade_level.xlsx`、`__tables__.xlsx`、`__enums__.xlsx` 与 `i18n.xlsx`；公式错误扫描为 0，预览确认既有样式未被破坏。
- i18n 的 Strike、Bludgeon、Shoot、OutputAdjust 升级说明统一改为参数化生产文本，避免无限 L2+ 被固定 L1 文案覆盖。
- Luban 生成更新 `battle_tbhero.json`、`battle_tbcard.json`、`battle_tbcardupgradelevel.json` 及对应生成代码；业务卡牌字段继续使用既有短键契约。
- `TinySpire/Build/Sync and Build All` 完成配置门禁、Localization 同步和 Local Addressables 重建；Editor.log 记录 `TinySpire sync and local content build completed successfully`。
- 最新 BuildLayout：`BuildStartTime=2026-08-26 04:32:41`、`BuildError` 为空、`BuildScript=Default Build Script`。`battle_tbhero/card/deck/cardupgradelevel.json` 和 RunEntry/Battle/Loading 场景均位于物理 bundle，Provider 为 `UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider`。
- Packed Play 临时使用 `BuildScriptPackedPlayMode`，只改 ignored 的 `Library/AddressablesConfig.dat`；结束后恢复 Fast Mode。tracked `AddressableAssetSettings.asset` 在切换前后 SHA-256 均为 `879257C892A035284B272AA19BB97A2E98F29264DC2E86B312BD1055423B400C`。

## 6. 完整回归与环境说明

- Rider 最终 build session `07b40384-6749-4cfa-ac8c-b5f8bd4f9cee` 成功；`get_project_problems(severity=Error)` 为 0。
- 新生产验收用例首次 job `614adafdcec0456088074214dbc85f98` 为 1/1。
- Packed Play 后第一次完整 job `527b953aa0344a6ea492e4f59eec9c81` 在第 440 项旧 Localization UnityTest 等待静态初始化并最终超时；它不作为通过证据。没有结束 Unity 进程或清理 Library，只调用 Unity 的 `RequestScriptReload` 复位脚本域。
- 复位后原挂起用例 job `b690bfdb15dd458f979aefea1eba85f2` 为 1/1；最终完整 job `7cad4b02d38248f298227ea06804c949` 为 **1093/1093，0 failed，0 skipped**。
- 最终产品检查点和最终 Editor 静态检查点均重新查询 Console Error=0。

## 7. 工作区与边界审计

- 修改前基线 HEAD 为 `6cfdaca99eb565254983532f0c0d04afa8ff90b8`。
- 用户已有 `TinySpire/ProjectSettings/ProjectSettings.asset` 与 `TinySpire/.codex/` WIP 全部保留；未执行 `git add`、`commit`、`push`、`reset` 或 `clean`。
- Packed Play 前把用户原 `run-save.json` 复制到工作区临时目录；验收后恢复并验证 SHA-256 为 `419058435D82A48EA08DBF3121F6127417EAC700D302388BFFFA4586DFEE54B9`，随后删除临时副本。
- 最终 Unity 状态复查为 `ActivePlayModeDataBuilderIndex=0`、BootstrapScene `sceneDirty=false`、`isPlaying=false`、`isCompiling=false`；刷新最后两份生成 JSON 后 Console Error 仍为 0。
- `git diff --check` 通过；`Docs/Copilot_Daedalus/08_tools/Test-LLMKnowledgeWorkflow.ps1` 输出 `LLM knowledge workflow V2 checks passed.`，当前文档状态链与相对链接通过离线校验。
- 本轮没有修改 Scene、Prefab、asmdef、ProjectSettings、HybridCLR 或 DI 架构，也没有实现 G5 遗物/药水、金币、商店、事件、宝箱、真实 Boss、RunOutcome、云/多槽/战中存档、多人、广告或商业化。
- G4 已 verified，授权在此闭环；下一步必须等待新的窄 Goal，不能顺手进入 G5。
