---
title: STS2 Ironclad I4 成功归宿与 Tremble
page_type: testing
lifecycle: active
created: 2026-08-06
updated: 2026-08-06
scope: STS2 v0.107.1 Ironclad 单人卡池 I4
status_source: ../SESSION_LOG.md
---

# STS2 Ironclad I4 成功归宿与 Tremble

## 验收结论

I4 已完成并停在 I5 之前。Tremble（3118）现在是基础可玩的真实 Exhaust 卡：1 费、目标为一个敌人、施加 3 层易伤，成功结算后从 Hand 移入 ExhaustPile。默认 Deck 未加入 Tremble；当前 BattleScene 不会自然抽到该卡。

出牌成功归宿只读取 Card 的基础 `PlayDestination`。Turn 在首次权威写入前把 Discard / Exhaust 解析为本次命令的冻结选择，随后保持既有顺序：EnergySpent → 全部 Effect settlement → CardMoved。未知或 Power 归宿在写入前 fail-fast；升级实例与 Power 分别留在 I9、I11。

## 精确红灯与最小实现

| seam | 红灯 | 最小实现 | 绿灯 |
|---|---|---|---|
| 真实命令归宿 | `60927f80b92b432d95c7681759fa1e82`：期望 ExhaustPile，实际 DiscardPile | `BattleTurnController.TryPlayCard` 在写入前冻结基础归宿，结算后调用既有 `ExhaustFromHand` 或 `DiscardFromHand` | `f68c558591014dc285d6a1888fa8717b`：1/1 |
| Tremble 生成数据 | `97b377dce0c34921938064049ef89c0f`：期望 Implemented，实际 CatalogOnly | Card 翻为 Implemented，绑定 `vulnerable:4006`；新增 ApplyVulnerable 3 | `4892bc7cad4c42769aaef7ff4a349837`：相关三项 3/3 |
| 可玩身份门禁 | `bab167110fba448ea937e4e961092946`：旧门禁只返回 3/82 数量口径 | 门禁锁定 BASH / DEFEND_IRONCLAD / STRIKE_IRONCLAD / TREMBLE，并校验 4/81 | `90bff86aca1d4225a3ad0715ae2c5297`：1/1 |

生产实现没有按卡 ID、卡名或本地化文本分支，没有新增写入口、settlement 类型、卡区事实或动画队列。

## 配置、双语与资源

- `battle.card.xlsx`：Tremble 为 `Implemented`，绑定 `vulnerable:4006`，基础/升级目录归宿均保持 `ExhaustPile`；升级程序尚未实现。
- `battle.card_effect.xlsx`：新增 `4006 / ApplyVulnerable / None / 3`。
- `i18n.xlsx`：基础说明改为 en/zh-CN Smart String，业务参数仅 `{vulnerable}`，并复用 `{keywordVulnerable}`。
- Tremble 继续使用 `art_placeholder`，稳定逻辑地址为 `card-art/art_placeholder`；没有生成、下载或引用新美术。
- 三份作者工作簿均经候选导出、公式错误扫描、渲染目视、写回后重导入与再次渲染。Luban validation、C#/JSON 生成成功。
- 当前唯一 Unity Editor 的 `TinySpire/Build/Sync and Build All` 已成功同步 Localization 并重建 Local Addressables；Console 记录构建约 29.244 秒及完成日志。

## 自动验证

| 层级 | 结果 | 证据 |
|---|---:|---|
| I4 窄回归 | 3/3 | `4892bc7cad4c42769aaef7ff4a349837`：命令顺序、生成 Tremble、身份漂移 |
| Turn / CardZones / Queue / 展示边界 / 构建门禁 | 61/61 | `5ba43fb9daed4a9c9e5467dfb3e69762`；第一次任务只发生 Test Runner 初始化超时，0 项开始，重试后全绿 |
| 完整 EditMode | 482/482 | `65ba008cb21947f3bfb2da54539912af`，0 failed、0 skipped；包含真实 Addressables Sprite 加载 |
| Solution build | 通过 | `dotnet build TinySpire/TinySpire.sln --no-restore -m:1`：0 error、12 条既有程序集版本冲突 warning |
| Console | 通过 | 同步构建后、测试前 error 过滤为 0；最终保留 3 条“Localization validation passed”和 3 条保存 TestResults.xml 的 Test Runner 捕获记录，InvalidKey 为 0，无产品编译或运行时错误 |

本切片没有声称真实 Game View 已展示 Tremble：默认 Deck 未改变，项目当前也没有 Deck Builder。I4 的权威事实由公开 Queue 提交与只读 Energy、Combatant、CardZones、settlement 取证；逐卡真实 BattleScene 验收留到 I14。

## 表现与停止边界

- ExhaustPile 事实与牌堆 HUD 计数会正确更新。
- 既有表现计划不为 Hand→Exhaust 创建运动 cue；本切片按用户确认明确不包含 Exhaust 飞行动画。
- 未修改 `BattleCommandQueue`、settlement、CardZones 公共契约、默认 Deck、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、DI 或启动流程。
- I5 需要扩展每步独立目标并修改 Turn/settlement 边界，必须取得新的明确确认后从独立红灯开始。
- 未暂存、提交或推送 I0-I4 工作区改动。
