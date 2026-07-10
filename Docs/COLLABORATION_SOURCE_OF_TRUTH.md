---
title: TinySpire Docs · Collaboration Source of Truth
created: 2026-07-06
updated: 2026-07-08
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

## 2. Agent Roles

| Agent | Directory | Role |
|---|---|---|
| Theseus / 忒修斯 | root owner | 项目所有者 / 主程 / 最终拍板者 |
| Calliope / 卡利俄佩 | `Docs/Gemini_Calliope/` | 创意 / 文本包装 / 剧情 / 美术概念 / 脑暴发散 |
| Pegasus / 珀伽索斯 | `Docs/Hermes_Pegasus/` | 数值策划 / 系统机制设计 / 文档整理 / 决策记录 |
| Daedalus / 代达罗斯 | `Docs/Copilot_Daedalus/` | 编程实现 / Unity C# / 架构落地 / 测试 / 重构 |

## 3. Required Reading Order

任何 Agent 开始 TinySpire 工作前，按顺序读取：

```text
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
Docs/AI_COLLABORATION_RULES.md
Docs/Gemini_Calliope/README.md
Docs/Hermes_Pegasus/AGENT_PROFILE.md
Docs/Hermes_Pegasus/AGENT_HANDOFF.md
Docs/Hermes_Pegasus/STATUS.md
Docs/Hermes_Pegasus/SYNC_PROTOCOL.md
```

Calliope 还需要读取：

```text
Docs/Gemini_Calliope/README.md
```

Pegasus 还需要读取：

```text
Docs/Hermes_Pegasus/design/project-definition.md
Docs/Hermes_Pegasus/design/decision-locks.md
Docs/Hermes_Pegasus/design/decisions.md
Docs/Hermes_Pegasus/art/art-style.md
Docs/Hermes_Pegasus/architecture.md
```

Daedalus 还需要读取：

```text
Docs/Copilot_Daedalus/README.md
Docs/Copilot_Daedalus/SESSION_LOG.md
Docs/Copilot_Daedalus/CODE_DECISIONS.md
```

## 4. Directory Ownership

```text
E:/Project/                    # unique Git root
  Docs/
    COLLABORATION_SOURCE_OF_TRUTH.md  # highest-priority collaboration fact
    AI_COLLABORATION_RULES.md         # global AI rules
    Gemini_Calliope/                 # creative/text/concept docs owned by Calliope
    Hermes_Pegasus/                  # design/planning/docs owned by Pegasus
    Copilot_Daedalus/                # coding-agent docs owned by Daedalus
```

Ownership means default write authority, not exclusive access.

### Calliope may edit

```text
Docs/Gemini_Calliope/**
```

Calliope should not silently promote brainstorms, flavor text, or visual concepts into locked design decisions. Confirmed concepts should be handed to Pegasus for system/design integration.

### Pegasus may edit

```text
Docs/Hermes_Pegasus/**
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
Docs/AI_COLLABORATION_RULES.md
```

Pegasus should not silently rewrite Daedalus implementation notes unless correcting names/paths or merging an explicit request.

### Daedalus may edit

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

Before editing:

```bash
git status
git diff
```

Before committing or pushing, every AI must show the intended file list, diff stat, excluded/unrelated files, and commit message, then wait for explicit user approval. Do not use broad `git add .` when untracked files or generated assets are present.

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

Current status/checklist goes here:

```text
Docs/Hermes_Pegasus/STATUS.md
```

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

## 8. Current Project Focus

Current phase:

```text
BattleScene MVP planning / early implementation
```

Primary target:

```text
Card → Effect → BattleState → UI → Feedback
```

Current collaboration model:

```text
Calliope brainstorms concepts/text → Pegasus defines systems/math/docs → Daedalus implements code → Theseus decides/finalizes
```
