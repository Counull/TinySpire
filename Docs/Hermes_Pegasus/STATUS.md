# TinySpire · Status / Sync Checklist

> 用于 Pegasus（WSL/Hermes）与 Windows 本地 Agent 同步新增、修改、决策和待办。

## 当前阶段

```text
阶段：美术方向 + BattleScene MVP 前置规划
主目标：卡牌 → 效果 → 状态 → UI → 反馈 的最小垂直切片
状态：planning
```

## 同步原则

1. **文档变更先写这里，再落到具体文档。**
2. **新增决策必须写入 Decision Log。**
3. **新增资源必须登记路径、用途、是否采用。**
4. **本地 Agent 改动后先 commit，不要静默覆盖 Pegasus wiki。**
5. **Pegasus 同步前先看 `git status` / `git diff`。**

## 文档状态

| 文档 | 用途 | 状态 | 最近动作 |
|---|---|---|---|
| `AGENT_HANDOFF.md` | 本地 Agent 入口 | active | 已生成 |
| `index.md` | 项目总览 | active | 已同步 |
| `architecture.md` | 程序架构 | active | 已同步 |
| `design/baseline.md` | 基础设定 | active | 已同步 |
| `design/decisions.md` | 玩法决策 | active | 已同步 |
| `art/art-style.md` | 美术方向 / AI 资源管线 | active | 已同步 |
| `STATUS.md` | 双方同步状态 | active | 新增 |
| `SYNC_PROTOCOL.md` | 同步规则 | active | 新增 |
| `agent-code/` | 编程 Agent 工作区（Copilot） | migrated | 已迁至 `../Copilot_Daedalus/` |

> **编程 Agent（Daedalus，运行在 GitHub Copilot 上）工作区为 `Docs/Copilot_Daedalus/`**，与 Pegasus 设计文档同级独立。

## 当前美术结论

- Demo / 第一层主题：构成主义奇幻 + 欧洲/沙俄/瘟疫医生/宗教仪式感。
- 不锁死完整独立游戏世界观。
- 角色表现方向：纸片角色 + Tween + 构成主义 VFX。
- 暂不优先 Spine / Q 版。

## 当前资源状态

| 资源 | 路径 | 用途 | 状态 |
|---|---|---|---|
| 构成主义战斗背景 | `art/assets/art-style/tinyspire-constructivist-battle-bg-concept.png` | Demo 背景概念 | keep |
| 初版塔内执行员 | `art/assets/art-style/tinyspire-tower-executor-character-concept.png` | 早期角色概念 | reference |
| 初版秩序守卫 | `art/assets/art-style/tinyspire-order-warden-enemy-concept.png` | 早期怪物概念 | reference |
| 鸟嘴面具执行员 | `art/assets/art-style/tinyspire-plague-doctor-executor-concept.png` | 当前更贴近方向的玩家概念 | candidate |
| 教皇式怪物 | `art/assets/art-style/tinyspire-pontiff-monster-concept.png` | 当前更贴近方向的敌人概念 | candidate |

## 当前待办

### P0 — BattleScene MVP

- [ ] 定义 BattleScene 屏幕布局安全区
- [ ] 定义卡牌数据最小结构
- [ ] 定义 Effect 执行链：Card → Effect → BattleState
- [ ] 定义 UI 绑定方式：BattleState → ViewModel → View
- [ ] 实现一张 `力量+3` 测试牌
- [ ] 实现一个敌人静态 PNG + hit shake
- [ ] 实现基础 VFX：BuffStamp / AttackSlash

### P1 — 美术资源

- [ ] 选定玩家概念图版本
- [ ] 选定敌人概念图版本
- [ ] 生成透明 PNG 立绘版本
- [ ] 生成 VFX sprite atlas：斜线 / 纸片 / 盖章 / 墨点
- [ ] 生成卡牌背景/卡框视觉参考

### P2 — 同步/流程

- [ ] 本地 Agent 读取 `AGENT_HANDOFF.md`
- [ ] 本地 Agent 输出实现计划到 `local-agent-notes/`
- [ ] 每次修改后 git commit
- [ ] Pegasus 定期把有效结论回写 WSL wiki

## Open Questions

- [ ] Unity 项目真实路径是什么？
- [ ] 是否使用 UI Toolkit 还是 UGUI 做第一版战斗 UI？
- [ ] 卡牌数据第一版用 ScriptableObject、JSON，还是纯 C# mock？
- [ ] 是否引入 DOTween？
- [ ] 是否引入 VContainer/R3/UniTask 从第一天开始？

## Change Log

### 2026-07-06

- 创建文档协作区：`Docs/Hermes_Pegasus/`
- 新增 `STATUS.md`
- 新增 `SYNC_PROTOCOL.md`
