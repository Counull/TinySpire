---
title: 机枪兵 MG1 Hero 资源档案验收
page_type: testing
lifecycle: active
created: 2026-08-06
updated: 2026-08-06
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-06-machine-gunner-card-pool-integration.md
---

# 机枪兵 MG1 Hero 资源档案验收

## 范围与固定口径

本记录只验收 MG1：每 Hero 的静态 Energy/Ammo 档案、每玩家只读资源事实、回合开始补充、上限裁剪和构建门禁。用户确认的规则是：首回合 Energy 为 3、不额外叠加 `+3`；资源上限降低时当前值立即裁剪为 `min(current, max)`；默认抽牌继续复用共享“补至 5”规则。

当前生产 Hero `1001` 生成结果为 Energy `3/3/+3`、Ammo `0/0/+0`。本记录不把未来机枪兵的 `3/5/+3`、`5/5/+1` 形状误报为已接入 Hero；本切片没有新增 Hero、Deck、卡牌、状态、UI、Prefab、Scene、素材或奖励/Run 功能。

## 自动化证据

| 验证 | 结果 | 覆盖 |
|---|---:|---|
| `dotnet build TinySpire/TinySpire.sln --no-restore -m:1` | 0 error；12 条既有程序集版本冲突 warning | 新/改 C# 的 solution 编译 |
| Unity EditMode job `1c80844033914880ab4bd43ecd7067b9` | 8/8 passed | 首回合清 Block → Energy → Ammo、后续 capped 补充、低上限即时裁剪、非法档案拒绝、共享补至 5、构建期 JSON 门禁 |
| Unity EditMode job `63d42d2baaf2459fb3735017f8a65a67` | 93/93 passed | Session 装配、Queue/Turn 回归、结算契约、表现计划、敌人联合快照和 M10 默认内容基线 |
| Unity EditMode job `b1488ed9add3427b986e8444439f36f1` | 27/27 passed | 因生成 Hero 反序列化字段扩展而更新的参与者反馈与目标聚焦 fixture |
| `TinySpire/Build/Sync and Build All` | success | Luban 生成、配置清单/资源档案/卡牌目录门禁、本地化导入和本地 Addressables 内容构建；Console 记录 `TinySpire sync and local content build completed successfully.` |

首次相关类集运行暴露了新增快照测试夹具的无效比较：在 `max_ammo = 0` 时构造 `ammo = 1` 会按已确认的即时裁剪规则归零，因而不是实际漂移。夹具改为启用 Ammo 上限的独立快照基线后，单项任务 `53a10734140b4e6da1eb6584f05ab464` 为 1/1 passed，最终相关类集为 93/93 passed。这是测试输入修正，不是生产规则放宽。

## 边界与未覆盖项

- 本次没有可选择或可游玩的机枪兵，因此没有宣称 Game View 玩法验收。
- Ammo settlement 被事实层和未来机制保留，表现层目前显式忽略；没有 Ammo HUD、动画或交互验收。
- `Sync and Build All` 已完成本地内容构建；MG1 未新增业务素材域或 Addressables 资源，因而不需要新增素材的 Packed/Player `AssetBundleProvider` 真加载证据。
- `weakness`、`Vulnerable`、Burn/Oil/Smoke/Armor、Power、目标选择、卡牌随机流和奖励流程仍不在 MG1 范围内。
