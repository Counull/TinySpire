---
title: 10_communication · 跨 Agent 沟通
page_type: communication
lifecycle: active
updated: 2026-08-14
---

# 10_communication · 跨 Agent 沟通

- 角色：对 Pegasus / Calliope / Theseus 的 hand-off、确认问题、外发措辞草稿。
- **非默认工程上下文**：只在做沟通任务时读。
- 已确认结论迁往对应事实源或实现计划。

## 代码阶段交接与审计

- [BattleScene → Run 阶段交接只读审计任务书](2026-08-14-battlescene-to-run-audit-brief.md)：固定基线、双轴范围、证据标准、排除项与结果回收规则。
- [DeepSeek Harness · BattleScene → Run 只读审计 Prompt](2026-08-14-battlescene-to-run-audit.deepseek-harness-prompt.md)：可直接外发的薄 Prompt；只读审计，不授权修改代码。

## 可外发素材说明

- [STS2 战士卡牌缺图清单](2026-08-06-sts2-ironclad-card-art-checklist.md)：I3 冻结快照中 82 张缺图卡的中英名、建议短键与文件名；Agent 不生成或下载卡图，未交付素材时继续使用 `art_placeholder`。
- [BattleScene UI 美术素材需求说明](2026-07-30-battle-ui-art-brief.md)：供图像生成模型或美术协作者使用的缺失 UI 素材清单、用途、规格与提示词。
- [BattleScene 出牌聚焦与弃牌过渡需求说明](2026-08-02-battle-card-motion-feedback-brief.md)：M6C 人工审阅提出的目标选择聚焦、可选素材与结束行动弃牌过渡；限定在 M9，不改变 M6 权威规则。
