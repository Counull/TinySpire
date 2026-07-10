# TinySpire · Autonomous Brainstorm

这个目录专门给 Pegasus 定时自主发散用。

## 定位

- 这里不是事实源。
- 这里不是决策记录。
- 这里不是实现计划的最终版本。
- 这里是“想法草稿 / 反事实推演 / 设计变体 / 风险提醒 / 下一步提案”的收集区。

## 硬边界

定时任务只允许写入本目录：

```text
Docs/Hermes_Pegasus/brainstorm-autonomous/
```

不允许自动修改：

```text
Docs/Hermes_Pegasus/design/project-definition.md
Docs/Hermes_Pegasus/design/decision-locks.md
Docs/Hermes_Pegasus/design/decisions.md
Docs/Hermes_Pegasus/design/baseline.md
Docs/Hermes_Pegasus/STATUS.md
Docs/COLLABORATION_SOURCE_OF_TRUTH.md
```

不允许自动：

- commit
- push
- 改 Locked 决策
- 把 Open Question 写成已决定
- 添加图片、zip、大文件
- 修改 Unity 工程代码

## 文件命名

每次定时运行生成一个独立 Markdown 文件：

```text
YYYYMMDD-HHMM-topic.md
```

## 内容要求

每篇必须明确标注：

- `status: proposal`
- `source: autonomous-brainstorm`
- `not_decision: true`

任何可执行建议都必须写成“待用户拍板”，不能伪装成项目决定。
