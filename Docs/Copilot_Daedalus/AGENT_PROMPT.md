---
title: Daedalus · Agent Prompt 模板
role: coding-agent
name: Daedalus
project: TinySpire
page_type: template
lifecycle: draft
created: 2026-07-08
updated: 2026-08-24
---

# Daedalus · Agent Prompt 模板

> 这是调用适配器，不是事实源。它只传递任务差异；身份、规则、状态和历史继续由原文拥有，避免复制后漂移。

## 可复制模板

```text
Daedalus，请处理下面的 TinySpire 任务。

前置读取：
1. 根 AGENTS.md 与 Docs/Copilot_Daedalus/AGENTS.md
2. Docs/Copilot_Daedalus/README.md → STATUS.md
3. 若涉及实现、运行时或架构，再读 ARCHITECTURE_CONVENTIONS.md
4. 再按 README 路由只读一份直接相关页；证据缺失或冲突时才继续下钻

任务目标：<一句话可观察结果>
所属阶段 / 切片：<编号，或“独立维护任务”>
事实来源：<精确项目相对路径；没有则写“未提供”>
允许范围：<允许修改的文件或目录>
明确排除：<不得修改的内容>
已锁定决定：<CD / 设计决定；没有则写“无”>
交付与验证：<评审 / 计划 / 代码 / 测试 / 手测>
Git 权限：<只修改 / 可 commit / 可 push>

执行合同：
- 保护现有工作区，只精确暂存本任务文件。
- Roadmap、计划和历史授权不产生新权限；不要把 proposal 当事实。
- 缺失信息只有在会改变语义、范围或高影响文件时才提问；否则采用最小、可回滚假设继续。
- 结果中说明：改了什么、没改什么、验证结果、仍需用户处理什么。
```

## 使用原则

- 不要把 Profile、Roadmap、完整决策集或历史日志粘贴进 Prompt；给精确路径即可。
- 只读评审把 `Git 权限` 写成“无写入”；需要 commit / push 时分别明确。
- 出现 Locked 冲突、新玩法选择、第三方依赖或高影响 Scene / Prefab / asmdef / ProjectSettings 变更时，停止该分支并请求裁决。
