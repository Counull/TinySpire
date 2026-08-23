---
title: LLM 项目知识工作流 V2 试点验收记录
page_type: testing
lifecycle: completed
date: 2026-08-24
updated: 2026-08-24
scope: Docs/Copilot_Daedalus local instance only
status_source: ../STATUS.md
source: plans/2026-08-24-llm-project-knowledge-workflow-v2.md
---

# LLM 项目知识工作流 V2 试点验收记录

## 验收合同

本记录只验证 Markdown 路由和离线校验器：不构成 Unity、Luban、Addressables、测试或运行时功能的证据。

| 检查 | 通过条件 |
|---|---|
| 当前状态源 | `STATUS.md` 存在、为 `page_type: status`、不超过 5 KiB，且主入口与活动 G3 文档指向它。 |
| 默认读取集 | `README.md` 默认集包含 `STATUS.md`，不包含 `SESSION_LOG.md` 或 `CODE_DECISIONS.md` 的全文。 |
| 历史可追溯 | `SESSION_LOG.md` 标记为 `changelog`；G3 计划原路径归档，底层决策、计划和验收文件保持原位。 |
| 可选检索边界 | ByteRover adapter 明确 `non-authoritative locator/cache` 与精确项目相对来源核验。 |
| 可重复校验 | `pwsh -File Docs/Copilot_Daedalus/08_tools/Test-LLMKnowledgeWorkflow.ps1` 与 `git diff --check` 均成功。 |

## 结果

- `pwsh -NoProfile -File Docs/Copilot_Daedalus/08_tools/Test-LLMKnowledgeWorkflow.ps1` 通过：确认状态页、主要入口、G3 归档计划/验收、压缩索引、changelog 与 ByteRover 非权威合同。
- `STATUS.md` 为 **2785 bytes / 39 lines**；`README.md` 为 **40 lines**；`plans/README.md` 与 `06_testing/README.md` 分别为 **27 / 28 lines**。
- Daedalus 全目录相对链接检查为 0 个断链；本轮 23 份 Markdown/PowerShell 文件通过严格 UTF-8 无 BOM 检查；`git diff --check` 未发现空白错误。
- 上位 `COLLABORATION_SOURCE_OF_TRUTH.md` 已把当前实现路由改为 Daedalus `STATUS.md`；Pegasus 旧 `STATUS.md` 明确标为设计/美术同步历史快照。
- 先前提交 `310edca` 改动的 12 份公共 Markdown 已通过独立只读审计并推送。此次复核又对公共仓库全部 18 份 Markdown 执行链接/锚点、UTF-8 BOM、私有语义边界与 `git diff --check` 检查；9 份改动通过独立复审后以 `4491bb4` 推送到公共仓库 `origin/main`。

## 第二轮维护结果

- 授权 seam 收紧为“目录责任不等于常驻写权限”；旧状态、Roadmap、计划或 owner 字段都不能替代当前用户授权，commit/push 仍是独立权限。
- Daedalus 的 `AGENTS.md`、`AGENT_PROFILE.md`、`AGENT_PROMPT.md` 收敛为入口、职责与调用 adapter，不再各自复制状态和路由规则。
- 清除 2 个失效相对链接与 1 份重复验收/归档文档；3 份已验证计划统一归档，`DEP-012` 按现有代码与 I4 RED→GREEN 证据标记 resolved。
- 离线脚本新增全目录相对链接、授权语义、归档唯一性、计划 lifecycle 与入口契约检查；授权接口、历史迁移、公共 workflow 三路独立复核均为 PASS。
- 公共 workflow 补齐 testing route、上下文压缩后继续同一 active work、检索不可用时回退 Fast Path，以及模板分组一致性；没有写入 TinySpire、ByteRover 或机器路径等私有语义。

结论：V2 试点的短状态路由、历史压缩与离线结构校验已完成。它没有运行 Unity、Luban、Addressables 或运行时测试，也不声称验证这些无关链路。ByteRover 实际链路另见专门验收页。
