---
title: TinySpire Docs · Collaboration Source of Truth
created: 2026-07-06
updated: 2026-08-24
status: active
---

# TinySpire Docs · Collaboration Source of Truth

> **唯一事实：`E:/Project` 是 TinySpire 的唯一 Git root；`Docs/` 是唯一文档根；所有文档协作路径都必须以 `Docs/` 为相对根目录，不在文档正文中依赖机器相关绝对路径。**

## 1. Root Contract

本文档所在目录就是唯一文档根目录：

```text
Docs/
```

它所在的父级项目目录是唯一 Git root：

```text
../
```

规则：

- 不把 `Hermes_Pegasus/`、`Gemini_Calliope/` 或 `Copilot_Daedalus/` 各自当成独立事实源。
- 不在正文中写死 `E:\...`、`/mnt/e/...`、`\\wsl...` 作为协作规范依据。
- 如需说明本机路径，只能放在本地注释、临时日志或个人环境说明里；项目协作规范一律使用相对路径。
- 根目录的本文档优先级最高；子目录文档若与本文档冲突，以本文档为准。

## 1.1 Current roadmap entry

For future TinySpire progression work, **“roadmap” means the active Run roadmap**:

```text
Docs/Copilot_Daedalus/RUN_ROADMAP.md
```

`Docs/Copilot_Daedalus/ROADMAP.md` is the frozen BattleScene MVP archive. Read it
only when tracing M0–M10 history or its acceptance evidence; do not treat it as
the current implementation entrypoint. Current implementation status, authorization,
blockers, and next action live only in `Docs/Copilot_Daedalus/STATUS.md`.

## 2. Agent Roles

| Agent | Directory | Role |
|---|---|---|
| Theseus / 忒修斯 | root owner | 项目所有者 / 主程 / 最终拍板者 |
| Calliope / 卡利俄佩 | `Docs/Gemini_Calliope/` | 创意 / 文本包装 / 剧情 / 美术概念 / 脑暴发散 |
| Pegasus / 珀伽索斯 | `Docs/Hermes_Pegasus/` | 数值策划 / 系统机制设计 / 文档整理 / 决策记录 |
| Daedalus / 代达罗斯 | `Docs/Copilot_Daedalus/` | 编程实现 / Unity C# / 架构落地 / 测试 / 重构 |

## 3. Required Reading Order

任何 Agent 开始 TinySpire 工作前，先读取两份全局规则：

```text
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
Docs/AI_COLLABORATION_RULES.md
```

随后只进入本次任务所属角色的入口与当前状态，再按任务下钻至多一份直接相关的计划、决策或验收页；不得把完整历史、整本决策集或所有角色目录作为默认上下文。

Daedalus 默认读取：

```text
Docs/Copilot_Daedalus/README.md
Docs/Copilot_Daedalus/STATUS.md
```

涉及实现、运行时或架构时，再读取：

```text
Docs/Copilot_Daedalus/ARCHITECTURE_CONVENTIONS.md
```

上述全局规则与实现约定属于规则预检，不占“一份任务知识页”的额度；只有缺失证据或发现冲突时，才允许从该任务页继续下钻。

设计、创意或美术任务先进入对应角色索引，再读取直接相关的设计决定、来源或资源页：

```text
Docs/Hermes_Pegasus/AGENT_PROFILE.md
Docs/Gemini_Calliope/README.md
```

## 4. Directory Responsibility

```text
E:/Project/                    # unique Git root
  Docs/
    COLLABORATION_SOURCE_OF_TRUTH.md  # highest-priority collaboration fact
    AI_COLLABORATION_RULES.md         # global AI rules
    Gemini_Calliope/                 # creative/text/concept docs owned by Calliope
    Hermes_Pegasus/                  # design/planning/docs owned by Pegasus
    Copilot_Daedalus/                # coding-agent docs owned by Daedalus
```

Ownership 只表示责任归属，以及当前任务已获用户明确写入授权后的允许落点；它本身不授予修改、commit 或 push 权限，也不限制其他 Agent 的只读访问。

### Calliope 的授权写入落点

```text
Docs/Gemini_Calliope/**
```

Calliope should not silently promote brainstorms, flavor text, or visual concepts into locked design decisions. Confirmed concepts should be handed to Pegasus for system/design integration.

### Pegasus 的授权写入落点

```text
Docs/Hermes_Pegasus/**
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
Docs/AI_COLLABORATION_RULES.md
```

Pegasus should not silently rewrite Daedalus implementation notes unless correcting names/paths or merging an explicit request.

### Daedalus 的授权写入落点

```text
Docs/Copilot_Daedalus/**
Docs/Hermes_Pegasus/local-agent-notes/**
```

Daedalus should not silently rewrite Pegasus design decisions. If implementation needs a design change, write a note/request first.

## 5. Change Protocol

Git 提交规范见：

```text
Docs/AI_COLLABORATION_RULES.md
```

Before editing，先确认当前用户请求明确包含写入；讨论、评审或可行性分析保持只读。获得写入授权后仍须先检查：

```bash
git status
git diff
```

Commit / push 也需要当前任务的明确用户授权。若用户已在当前任务中给出“完成后提交并 push”之类的清晰指令，不需要对同一已审查范围重复索取授权；否则必须先展示目标文件、diff stat、排除项与 commit message，再等待确认。任何授权都不扩展到无关文件、force push 或 broad stage；存在未跟踪文件或生成资源时不得使用 `git add .`。

After approval:

```bash
git add <explicit paths>
git commit -m "<message following Docs/AI_COLLABORATION_RULES.md>"
```

If an Agent sees uncommitted changes it did not create, it must inspect them before writing.

## 6. Decision Protocol

Gameplay/design decisions go here:

```text
Docs/Hermes_Pegasus/design/decisions.md
```

Code-level decisions go here:

```text
Docs/Copilot_Daedalus/CODE_DECISIONS.md
```

Current implementation status, authorization, blockers, and next action go here:

```text
Docs/Copilot_Daedalus/STATUS.md
```

Pegasus gameplay/design decisions remain in `Docs/Hermes_Pegasus/design/decisions.md` and `decision-locks.md`. `Docs/Hermes_Pegasus/STATUS.md` is a legacy design-sync checklist; it does not override the current implementation status.

## 7. Path Policy

Use relative paths in project docs.

Correct:

```text
Docs/Hermes_Pegasus/AGENT_HANDOFF.md
Docs/Copilot_Daedalus/README.md
```

Avoid in durable docs:

```text
E:\Project\...          # absolute machine path
/mnt/e/Project/...      # WSL mount path
\\wsl...                # WSL UNC path
```

Machine-specific absolute paths may appear only in temporary troubleshooting notes or local environment files.

## 8. Current Project Focus Route

当前 phase、active slice、授权、阻塞、最新证据与下一步只查：

```text
Docs/Copilot_Daedalus/STATUS.md
```

本节不复制可变项目快照，避免全局协作规则与实现状态形成两个维护点。

Current collaboration model:

```text
Calliope brainstorms concepts/text → Pegasus defines systems/math/docs → Daedalus implements code → Theseus decides/finalizes
```

## 9. Current Work Tracking Workflow

This is the current project-wide workflow decision. Agents must read and follow it instead of inferring a task system from their own defaults.

```text
Docs/                         detailed design, decisions, status, and evidence
GitHub Issue                  one phase-level entry for a vertical slice
Git                           code and document history
```

Rules:

- Do not create a GitHub Issue for every small implementation step.
- Do not use Hermes Kanban for TinySpire at the current project scale.
- A phase-level Issue links to the relevant Docs pages; it does not duplicate the full design.
- `Docs/Copilot_Daedalus/STATUS.md` is the current implementation task/status entry; design decisions remain with Pegasus.
- When this workflow changes, update this section first. Other Agent prompts must point back here rather than copying a second version.
