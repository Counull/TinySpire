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

当前采用轻量方案：

> **GitHub Issue 只作为阶段级入口；详细设计仍在 `Docs/`，不使用 Hermes Kanban。**

一个 BattleScene 垂直切片对应一个 Issue，不为每个小任务单独建 Issue。Issue 只链接目标、验收标准和相关 Docs，不复制完整设计。

暂不引入 GitHub Projects、Linear 或 Jira。

## 目录职责

`Docs/` 是唯一文档根。Agent 目录按职责分工，不互相复制同一类事实文档：

```text
Docs/
  COLLABORATION_SOURCE_OF_TRUTH.md  # 最高优先级：根目录、角色、事实源
  AI_COLLABORATION_RULES.md         # 全局协作与提交规则
  Hermes_Pegasus/                    # 设计、系统、数值、美术方向
  Copilot_Daedalus/                 # Unity 实现计划、代码决策、测试记录
  Gemini_Calliope/                   # 创意、文本、美术概念脑暴
```

Pegasus 目录内：

```text
Hermes_Pegasus/
  STATUS.md             # 当前阶段与任务状态的唯一入口
  design/               # 项目定义、玩法决策、决策锁定表
  art/                  # 美术方向与已登记资产
  architecture.md       # 程序架构约束
  brainstorm-autonomous/ # 未确认的提案，不是事实源
```

`local-agent-notes/` 是早期兼容目录。新产生的 Daedalus 实现计划、代码决策、测试记录统一写入 `Docs/Copilot_Daedalus/`；只有明确需要 Pegasus 转交的短消息才放入 `local-agent-notes/`，不在两个目录重复维护同一文档。

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
```

提交前必须先向 Theseus 展示明确文件清单、diff stat、排除项和 commit message，等待明确批准；批准后只使用显式路径 `git add <paths>`。不得使用 `git add .`。

### 2. Daedalus / 本地 coding agent 修改

Daedalus 默认写入自己的职责目录：

```text
Docs/Copilot_Daedalus/
```

具体落点：

- `plans/`：实现计划，属于 proposal，经 Theseus 确认后执行；
- `CODE_DECISIONS.md`：代码级决策；
- `06_testing/`：真实运行/测试验证记录；
- `SESSION_LOG.md`：会话摘要、已完成工作和阻塞项。

如果需要给 Pegasus 发送尚未整理的短消息，可以写入：

```text
Docs/Hermes_Pegasus/local-agent-notes/
```

但不能把同一份计划或测试记录同时复制到两个目录。若实现需要改变玩法或系统设计，Daedalus 应先写请求/冲突说明，由 Pegasus 整理到设计决策流程，不能静默改 `design/`。

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
git status
git diff --stat
```

提交前先展示提交范围并等待 Theseus 批准；批准后使用显式路径 `git add <paths>` 和 Conventional Commit。Pegasus 不自动 commit。

项目协作文档的事实源是项目内 `Docs/`。不要再把同一份 TinySpire 文档自动复制到 `~/.hermes/hermes-wiki/03_projects/card-game/`；该路径属于历史遗留位置，不作为本项目同步目标。

### 本地 Agent 开始工作前

读取：

```text
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
Docs/AI_COLLABORATION_RULES.md
Docs/Hermes_Pegasus/AGENT_HANDOFF.md
Docs/Hermes_Pegasus/STATUS.md
Docs/Hermes_Pegasus/SYNC_PROTOCOL.md
Docs/Copilot_Daedalus/README.md
Docs/Copilot_Daedalus/SESSION_LOG.md
Docs/Copilot_Daedalus/CODE_DECISIONS.md
```

### 本地 Agent 完成工作后

输出到：

```text
Docs/Copilot_Daedalus/plans/YYYY-MM-DD-topic.md
Docs/Copilot_Daedalus/06_testing/YYYY-MM-DD-topic.md
```

提交前必须展示文件清单、diff stat、排除项和 commit message，等待 Theseus 明确批准；批准后再使用显式路径提交。

## 什么时候增加任务管理层

当前不使用 Kanban、GitHub Projects、Linear 或 Jira。只有在出现以下至少两项时，才重新评估是否需要更重的任务管理层：

- P0/P1/P2 任务超过 50 个；
- 出现两个以上长期并行模块；
- 多个 Agent 经常互相覆盖；
- 需要 sprint、assignee 或正式 bug triage；
- 出现外部协作者。

在那之前，使用 `Docs/Hermes_Pegasus/STATUS.md`、阶段级 GitHub Issue 和 Git 历史即可。
