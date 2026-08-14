---
title: Marine Game 机枪兵 MG3 目标与卡牌随机流验收
page_type: testing
lifecycle: active
created: 2026-08-07
status_source: ../SESSION_LOG.md
related_plan: ../plans/2026-08-07-marine-game-card-only-integration.md
---

# Marine Game 机枪兵 MG3 目标与卡牌随机流验收

## 范围

本记录只验收 Hero 1002 的会话私有目标选择器与职业卡牌随机事务。它不翻转任何新增 `CatalogOnly` 卡，也不引入奖励、地图、Run、场景或 UI 选择流程。

## 实现结论

- 目标选择器只从当前存活敌人的 Encounter 顺序派生显式、最近、最远、全体、随机和自身目标；第二近等后续可选目标复用同一快照索引。
- 随机选择器使用调用方传入的随机副本。机枪兵运行时仅在整张卡的资源、效果与 Hand→Discard 都成功后，才提交候选随机状态。
- 伪造随机目标、无存活敌人或后续提交失败不推进随机流；默认 Hero 1001 和通用战斗规则不读取该随机流。

## Unity 验证

使用当前已连接的单一 Unity 6000.5.5f1 Editor，未启动第二实例、未操作 Game View。

| 项目 | 结果 |
| --- | --- |
| 编译刷新 | Console 未出现产品编译错误；仅保留 MCP WebSocket 已知 warning |
| EditMode 任务 | `c9c735c3070342d6879a1d4d1d01b462` |
| 汇总 | **9/9 passed，0 failed，0 skipped** |
| 覆盖 | 初始牌队列、Encounter 最近/最远/全体、显式目标、固定种子重放、伪造随机输入、无活敌零推进 |

## 未包含

- Weakness、Smoke、Burn、Oil、Armor、Power、延迟效果、X 费、手牌上限与临时卡；
- 驻防/排气散热等权威待决选择；
- DataTables/Localization/Addressables 的新增同步（本切片未修改作者表或可寻址内容）；
- 场景、Prefab、默认 Hero、角色选择和受保护美术路径。
