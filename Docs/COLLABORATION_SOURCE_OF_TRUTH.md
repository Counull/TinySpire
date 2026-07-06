---
title: TinySpire Docs · Collaboration Source of Truth
created: 2026-07-06
updated: 2026-07-06
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

- 不把 `Hermes_Pegasus/` 或 `Copilot_Daedalus/` 各自当成独立事实源。
- 不在正文中写死 `E:\...`、`/mnt/e/...`、`\wsl...` 作为协作规范依据。
- 如需说明本机路径，只能放在本地注释、临时日志或个人环境说明里；项目协作规范一律使用相对路径。
- 根目录的本文档优先级最高；子目录文档若与本文档冲突，以本文档为准。

## 2. Agent Roles

| Agent | Directory | Role |
|---|---|---|
| Pegasus / 珀伽索斯 | `Docs/Hermes_Pegasus/` | 策划 / 设计 / 美术方向 / 流程 / 决策整理 |
| Daedalus / 代达罗斯 | `Docs/Copilot_Daedalus/` | 编程实现 / Unity C# / 架构落地 / 测试 / 重构 |
| User / 刘鸿森 | root owner | 制作人 / 主程 / 最终拍板者 |

## 3. Required Reading Order

任何 Agent 开始 TinySpire 工作前，按顺序读取：

```text
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
Docs/Hermes_Pegasus/AGENT_HANDOFF.md
Docs/Hermes_Pegasus/STATUS.md
Docs/Hermes_Pegasus/SYNC_PROTOCOL.md
```

Daedalus 还需要读取：

```text
Docs/Copilot_Daedalus/README.md
Docs/Copilot_Daedalus/SESSION_LOG.md
Docs/Copilot_Daedalus/CODE_DECISIONS.md
```

Pegasus 还需要读取：

```text
Docs/Hermes_Pegasus/art/art-style.md
Docs/Hermes_Pegasus/design/decisions.md
Docs/Hermes_Pegasus/architecture.md
```

## 4. Directory Ownership

```text
E:/Project/                    # unique Git root
  Docs/
    COLLABORATION_SOURCE_OF_TRUTH.md  # highest-priority collaboration fact
    Hermes_Pegasus/                  # design/planning docs owned by Pegasus
    Copilot_Daedalus/                # coding-agent docs owned by Daedalus
```

Ownership means default write authority, not exclusive access.

### Pegasus may edit

```text
Docs/Hermes_Pegasus/**
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
```

Pegasus should not silently rewrite Daedalus implementation notes unless correcting names/paths or merging an explicit request.

### Daedalus may edit

```text
Docs/Copilot_Daedalus/**
Docs/Hermes_Pegasus/local-agent-notes/**
```

Daedalus should not silently rewrite Pegasus design decisions. If implementation needs a design change, write a note/request first.

## 5. Change Protocol

Before editing:

```bash
git status
git diff
```

After editing:

```bash
git add .
git commit -m "short meaningful message"
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
Pegasus defines/designs → Daedalus implements/plans code → User decides/finalizes
```
