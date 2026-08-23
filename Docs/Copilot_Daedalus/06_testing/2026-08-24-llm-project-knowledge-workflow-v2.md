---
title: LLM 项目知识工作流 V2 试点验收记录
page_type: testing
lifecycle: completed
date: 2026-08-24
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
- `STATUS.md` 为 **2732 bytes / 39 lines**；`README.md` 为 **40 lines**；`plans/README.md` 与 `06_testing/README.md` 均为 **28 lines**。
- 修改文档相对链接检查通过，`RELATIVE_LINKS_OK files=21`；UTF-8 无 BOM 检查通过；`git diff --check` 未发现空白错误。
- 上位 `COLLABORATION_SOURCE_OF_TRUTH.md` 已把当前实现路由改为 Daedalus `STATUS.md`；Pegasus 旧 `STATUS.md` 明确标为设计/美术同步历史快照。
- 公共 workflow 的 12 份 Markdown 经独立只读审计、链接/锚点、BOM、私有语义边界与 `git diff --check` 复验通过；提交 `310edca` 已推送其 `origin/main`，父仓库只记录 submodule 指针。

结论：V2 试点的短状态路由、历史压缩与离线结构校验已完成。它没有运行 Unity、Luban、Addressables 或运行时测试，也不声称验证这些无关链路。ByteRover 实际链路另见专门验收页。
