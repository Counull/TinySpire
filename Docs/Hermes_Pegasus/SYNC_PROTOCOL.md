# TinySpire · Sync Protocol

> Pegasus（WSL/Hermes）与 Windows 本地 Agent 的协作规则。目标：减少上下文丢失、避免互相覆盖、保留决策轨迹。

## 不引入 Jira 的默认结论

现阶段不建议引入 Jira。

原因：

- TinySpire 还在 demo / 垂直切片阶段；
- Jira 的字段、工作流、Issue 类型会比项目本身还重；
- 本地 Agent / Pegasus / 用户三方协作，目前更需要轻量、可读、可 git diff 的 Markdown；
- 真正需要的是“状态同步 + 决策记录 + 变更审计”，Git + Markdown 已经够用。

当前替代方案：

> **Markdown checklist + Git commit = 轻量 Jira。**

如果以后任务膨胀到多人、多模块、多周迭代，再考虑：

1. GitHub Issues / Projects
2. Linear
3. Jira

优先级不要反过来。

## 目录职责

```text
Hermes_Pegasus/
  AGENT_HANDOFF.md      # Agent 入口上下文
  STATUS.md             # 当前状态/checklist
  SYNC_PROTOCOL.md      # 同步协议
  design/decisions.md   # 设计决策记录
  art/art-style.md      # 美术方向
  architecture.md       # 程序架构
  local-agent-notes/    # 本地 Agent 输出，不直接污染主文档
```

## 修改规则

### 1. Pegasus 修改

Pegasus 可以直接修改：

- `AGENT_HANDOFF.md`
- `STATUS.md`
- `SYNC_PROTOCOL.md`
- `art/art-style.md`
- `design/*.md`
- `architecture.md`

修改后必须：

```bash
git status
git diff
git add .
git commit -m "..."
```

### 2. 本地 Agent 修改

本地 Agent 默认只写：

```text
local-agent-notes/
```

例如：

```text
local-agent-notes/2026-07-06-battlescene-plan.md
local-agent-notes/2026-07-06-unity-implementation-notes.md
```

如果本地 Agent 要修改主文档，必须在输出里说明：

- 改了哪个文件；
- 为什么改；
- 是否新增决策；
- 是否需要 Pegasus 合并回 WSL wiki。

### 3. 决策记录

任何会影响未来实现的选择，都写进：

```text
design/decisions.md
```

格式：

```markdown
## 决策 NNN：标题

**问题**：一句话说明矛盾

**选项**
- A：xxx — 解决的问题：yyy
- B：xxx — 解决的问题：yyy

**选择**：xxx

**理由**：xxx

**程序影响**：可选
```

### 4. 状态更新

任务状态只在 `STATUS.md` 里改，不散落在聊天里。

允许状态：

```text
planned
in_progress
blocked
done
cancelled
```

### 5. 资源登记

新增图片、VFX、音频、字体时，在 `STATUS.md` 的资源表登记：

```markdown
| 资源 | 路径 | 用途 | 状态 |
```

资源状态：

```text
candidate
keep
rejected
reference
production
```

## 同步流程

### Pegasus 开始工作前

```bash
git status
git diff
```

如果有未提交改动，先读 diff，不要覆盖。

### Pegasus 完成工作后

```bash
git add .
git commit -m "sync docs: short description"
```

然后必要时把有效内容同步回：

```text
~/.hermes/hermes-wiki/03_projects/card-game/
```

### 本地 Agent 开始工作前

读取：

```text
AGENT_HANDOFF.md
STATUS.md
SYNC_PROTOCOL.md
```

### 本地 Agent 完成工作后

输出到：

```text
local-agent-notes/YYYY-MM-DD-topic.md
```

并 commit。

## 什么时候升级到 Issue 系统

满足以下任意 2 条，再考虑 GitHub Issues / Linear / Jira：

- P0/P1/P2 任务超过 50 个；
- 出现两个以上长期并行模块；
- 本地 Agent 和 Pegasus 经常互相覆盖；
- 需要按 sprint 管理；
- 需要外部协作者；
- 需要 bug triage / priority / assignee。

在那之前，不上 Jira。
