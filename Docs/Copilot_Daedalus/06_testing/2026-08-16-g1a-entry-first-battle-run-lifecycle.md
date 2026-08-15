---
title: G1-A 基础入口到首战 Run 生命周期验收
page_type: testing
lifecycle: active
date: 2026-08-16
scope: G1-A entry-first-battle minimal Run lifecycle
status_source: ../SESSION_LOG.md
source: Docs/Hermes_Pegasus/design/2026-08-15-g1a-entry-first-battle-grill.md
---

# G1-A 基础入口到首战 Run 生命周期验收

## 1. 结论

G1-A 已在唯一 Unity 6000.5.5f1 Editor 中完成 TDD、生成/本地内容构建、Packed Play Mode（Use Existing Build）和完整 EditMode 验收。已验证的最小链为：

```text
BootstrapScene → RunEntryScene → BattleScene
  ├─ Victory → RunEntryScene / 节点 Completed / 写回结算生命
  └─ Defeat  → RunEntryScene / Failed / snapshot 恢复
                    └─ Restart → BattleScene / 新 attempt 与新 seed
```

本记录只证明当前进程内的 G1-A 竖切；没有实现存档、继续游戏、多节点、奖励、退出 Run、永久死亡或多人队伍。

## 2. 主要契约

- `RunStateStore` 是跨场景 Run 业务事实的唯一写入所有者；`RunFlowService` 只创建/编排 Run、Battle 输入和场景切换。
- 每次入战先冻结 `RunBattleSnapshot`，再签发含 `RunBattleId`、Hero、当前生命、最大生命、牌组模板、Encounter 与 seed 的 `RunBattleInput`。
- `BattleLifetimeScope` 仍只经 `IBattleSetupOptionsSource` / `BattleSetupOptions` 取得上游输入；`BattleSession` 实际使用其中的当前生命、牌组模板和 seed。
- `BattleResultRunBridge` 只订阅当前 child Scope 的稳定 `BattleCommandQueue.Result`，以当前 attempt 身份回写；`Dispose` 后解除订阅。Run 模式不再由 Battle HUD 的 Restart/Exit 判断或改写 Run。
- 胜利只写回 `BattleResult.Players` 中对应 Hero 的结算生命并把唯一节点标为 `Completed`；失败不写入临时生命，保留进战前 snapshot，并由重开签发新 attempt。
- `RunEntryScene` 内的主菜单、角色选择、设置、图鉴、统计、地图和失败页由同一个 TMP/i18n View 切换；选角前的当前页和候选 Hero 只是 UI 会话状态，不是第二份 RunState。

## 3. RED → GREEN 证据

以下均由当前唯一 Editor 的 Unity MCP Test Runner 执行；数字来自本轮任务返回值，不沿用旧里程碑数量。

| 阶段 | RED 证据 | GREEN 证据 |
|---|---|---|
| Run 状态与 seed | job `a485304ebea64e6db91602c7003ec5e6`：具体碰撞用例 0/1，root `123456789` 的 attempt 50549/63342 都得到 `1984921321` | 改为正整数空间内互素步进映射后，job `5b7636eafd8443afb8a72ceb699500a9`：`RunStateStoreTests` 8/8 passed |
| RunEntry Scope 接线 | job `ac2a0f0484ed44efbfc83e9eccfb4cb6`：生产 Scope 缺少 View interface / Presenter entry point，0/1 | job `3345ffd256bf434aa648ffd3f23e5edb`：场景合约 6/6 passed |
| legacy Battle 兼容 | job `b3dd0f0cdc8644af873890f2101bffd8`：3 total / 1 passed / 2 failed；idle RunFlow 在 setup 与 bridge 绑定处抛 `No active Run battle input exists` | job `94bbb04ee3a5443d82894a54122736a5`：BattleSession / Bridge / HUD 24/24 passed |
| Battle result / HUD 隔离 | 先出现 5 个编译 RED：bridge overload、旧 flow feedback 签名和缺失 View 配置 | job `1e084a556be445878ee4108db87622a3`：Adapter / Bridge / HUD 32/32 passed |
| 本地化 | 首轮 21 项中 20 passed / 1 failed，缺少或 Smart 标记错误的入口字符串被门禁拒绝 | 最终 `RunEntryLocalizationG1ATests` 21/21 passed；最终完整集合再次覆盖 |
| 生产 DI | Packed Play 首次进入 RunEntry 时真实 RED：`VContainerException`，Presenter 构造器被误选并尝试解析 `Func<cfg.Tables>` | 生产构造器增加明确 `[Inject]` 后，job `e13f2ffd49a941af8071103fbeb2c766`：Presenter / Scene / View 19/19 passed，随后 Packed Play 正常启动 |
| Input System 重入 | 禁用 Domain Reload 的连续任务中，job `9ec0bfe1d6cc4186ae214c309dcda06d` 为 873 total / 870 passed / 3 failed；三个 View 用例都在重复 `AssignDefaultActions` 时报告 Point action 已脱离 asset | 删除程序化组件 `OnEnable` 后的重复分配，并断言 point / click / move / submit / cancel 均属于同一 asset；job `98ecb4a2dc264d018104f551fff5129b` 3/3 passed，完整套件后再跑 job `cf17bdf3eea14283a2ae6f5df34a0958` 仍为 3/3 passed |
| G1-A 聚合 | 中间聚合 job `28591c7bf29642c98e5e2ad2319bdf6d`：95/95 passed | 最终完整 EditMode job `55272b6354df42b6a0f351975ab58e71`：873/873 passed，0 failed，0 skipped，24.2229056 秒 |

补充最终定向结果：`RunStateStoreTests` 8/8；`BattleSessionTests`、`BattleResultRunBridgeTests` 与 Battle terminal 兼容集合 24/24；G1-A 命名 fixture 聚合 95/95。最终完整 873 项在生产 DI、Input System 重入修复与全部手测之后重新运行。

## 4. 数据、Localization 与 Addressables

通过当前 Editor 读取菜单资源后执行：

```text
TinySpire/Build/Sync and Build All
```

原始完成行：

```text
Json build layout written to Library/com.unity.addressables/buildlayout.json
Addressable content successfully built (duration : 0:00:15.018)
TinySpire Addressables content built: Library/com.unity.addressables/aa/Windows/settings.json
TinySpire sync and local content build completed successfully.
```

最新 BuildLayout：

- `BuildStart = 2026/08/16 02:47:10`
- `BuildError = ""`
- 总时长 `15.1231673` 秒
- `Assets/Scenes/RunEntryScene.unity` 位于独立 scene bundle，`BuildStatus = 0`
- Provider 为 `UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider`
- Result type 为 `IAssetBundleResource`

`i18n.xlsx` 新增 18 个 `run.entry.*` key，并把 Hero 1001 的既有名称 key 同步为 `Warrior / 战士`；只有 `run.entry.map.health` 使用 Smart 格式。生成的 Shared Data 与 en/zh-CN StringTable 已同步。入口 UI 全部使用 TMP；当前 Windows Editor 以 Microsoft YaHei UI 动态 Font Asset 覆盖所需中文字形，字形门禁和 Packed Play 截图均通过。

## 5. Unity MCP Packed Play Mode 手测

Play Mode 使用 Addressables `PackedPlayMode` / Use Existing Build。操作通过实际 `Button.onClick` 与既有 Battle command queue 完成，不直接写胜负结果。

### 5.1 入口、页面与两名 Hero

- Bootstrap 启动后只有 `RunEntryScene`，可见 `TinySpire / 开始游戏 / 设置 / 图鉴 / 统计`。
- 设置页显示 `设置布局占位` 并可返回；图鉴、统计各显示 `开发中` 并可返回。
- 角色选择页同时显示战士、机枪兵和未来队伍槽位；1001 与 1002 的选择高亮都实际切换，确认按钮只在选择后可用。
- Hero 1001 创建的 Run 为 30/30、Deck 1001；Hero 1002 创建的 Run 为 70/70、Deck 1002。

### 5.2 战士胜利回图

- attempt 1 输入：Hero 1001、HP 30、Deck 1001、seed `1143371176`。
- Battle child Scope 解析值与 `BattleSession` 实际值一致：30/30、10 张牌、Hero/Deck/seed 均来自 Run。
- 通过真实 `PlayCardCommand`、`EndPlayerActionCommand` 和 Queue 表现屏障打完战斗；玩家结算生命为 17/30。
- `BattleResult` 发布后自动回 `RunEntryScene`；Run 节点为 `Completed`、当前生命 17/30、`ActiveBattle = null`。节点显示“已完成”。

### 5.3 机枪兵失败与重开

- attempt 1 输入：Hero 1002、HP 70、Deck 1002、seed `768055331`。
- 首战初始卡区：12 张牌、Hand 5、Draw 7、Discard 0、Exhaust 0；Battle setup 与 session 都为 70/70。
- 连续通过真实结束回合命令让敌人结算至玩家 0 HP；失败页返回后 Run 为 `Failed`，但当前生命和 snapshot 都恢复为 70/70，没有继承失败战临时生命。
- 点击“重开本关”后签发 attempt 2，seed 为 `261103211`，与 attempt 1 不同；输入仍为 HP 70、Deck 1002。
- 新 `BattleSession` 为 70/70、12 张牌、Hand 5、Draw 7、Discard 0、Exhaust 0、Power 0；新起手顺序与 attempt 1 不同，没有继承失败战手牌或弃牌堆。

两条运行链在各自清空 Console 后查询 `error` 均得到 `Retrieved 0 log entries.`。完整 EditMode 后 Console 仅出现测试自身的“本地化校验通过”和“保存 TestResults.xml”记录，没有产品运行时错误。

本地临时截图已通过视觉检查：`TinySpire/Temp/G1A_Evidence/` 下的主菜单、胜利地图、失败页和重开战斗；该目录是未版本化的本机验收证据，不是正式入口美术。

## 6. 未覆盖与风险

- 没有执行 Player build；Packed Play 证明的是当前 Windows Editor 经真实 AssetBundleProvider 加载。
- CJK 字体当前来自操作系统字体族动态创建，当前 Windows 机器字形与 Console 均通过，但仓库尚未携带许可明确的 CJK TMP 字体资产；换到不含候选字体的平台需补独立资源切片。
- `RunEntrySceneBuildTools` 的失败重试原子性与 Addressables scene group 的 stale entry 清理仍是 Editor 工具 P2，不影响当前已生成且 exact-set 通过的三场景内容。
- 自动化尚未把 child LifetimeScope 的真实销毁、View/Scope 的 enabled/autoRun，以及 Addressables GUID 到目标 SceneAsset 的映射全部做成独立断言；当前生产释放链静态成立，Scene YAML/组映射正确，且 Packed Play 已走过真实场景卸载。
- 本切片没有新增存档、继续、退出 Run、奖励、多节点或任何 G2+ 状态。
