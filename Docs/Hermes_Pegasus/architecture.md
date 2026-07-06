---
title: 卡牌游戏 · 程序架构
created: 2026-06-04
updated: 2026-06-04
type: constraint
tags: [code-architecture, pattern]
sources: [20260604_cardgame_arch]
confidence: medium
attribution: |
  来源分层：
  - 用户自述：【用户】想在卡牌项目里摸 DI/响应式/MVVM/效果系统/异步流程，跑通一个场景就是胜利
  - 模型框架化：【合成:DeepSeek】三层架构模型（计算/状态/时序）、动画耦合无害论、MC 经验迁移陷阱
---

# 卡牌游戏 · 程序架构

> 杀戮尖塔 like 卡牌肉鸽的工程架构——以学习为目标的轻量技术选型。

## 当前结论

卡牌项目定位为**技术学习实验室**——用户在 MC 项目没碰过的 DI、响应式、MVVM、异步流程在这里从零摸。架构分三层：计算（纯 C#）→ 状态（R3 响应式）→ 时序（UniTask 命令 + 动画队列），各层只管自己。**动画耦合不需要解耦**——耦合的后果是否扩散是唯一判断标准。

## 技术选型

| 层 | 工具 | 学习目标 |
|---|---|---|
| DI | VContainer（比 Zenject 轻，Unity 原生支持） | 消除全局单例耦合 |
| 响应式 | R3（ReactiveProperty，async/await 原生支持） | buff/debuff 自动刷新 |
| MVVM | CardView ↔ CardViewModel（R3 属性绑定） | UI 不看逻辑，只看 ViewModel |
| 异步 | UniTask | 命令流程不靠协程手写 |
| 序列化 | JsonUtility / Odin / MemoryPack | Run 中断恢复——比 MC 存档简单 |
| 数据驱动 | JSON/SO 定义卡牌效果 | 何时硬编码 vs 数据驱动 |
| 测试 | NUnit（逻辑核纯 C# 不依赖 Unity） | 单元测试——MC 最缺的一块 |

## 三层架构

```
计算层（纯 C#）     → 伤害多少、buff 多久、谁死了
状态层（R3）       → 计算结果存在哪、谁关心（ReactiveProperty）
时序层（UniTask）  → 播什么动画、等多久、下一步是什么
```

**三层分开，每层只管自己那件事。**

### 动画命令队列

计算层瞬间跑完结果 → 推到响应式状态 → 动画队列接管，把状态变化翻译成时序化的动画命令：

```
AnimationCommandQueue
  ├─ DamageAnim(mobA, -12)
  ├─ WaitForHpBar(mobA)
  ├─ BuffIconPop(player, 力量+2)
  ├─ DeathCheck(mobA)
  ├─ DeathAnim(mobA)
  └─ AllDone → 允许下一张牌
```

命令是纯数据：`{ type: "damage", targetId: 3, value: -12 }`。执行层按 ID 找到对应组件触发动画，组件自己播自己的。

## 关键教训：动画耦合无害

用户在设计动画队列时出现了过度工程化倾向——hashId 注册表、事件总线、命令队列全是为一个还没出现的耦合准备的疫苗。

**但动画耦合和数据耦合本质不同。**

| | 数据耦合（MC Map 模块） | 动画耦合（卡牌项目） |
|---|---|---|
| 后果扩散 | ScriptBus 的改动 → 不知道谁订阅了 → 不敢改 → 越积越毒 | DamagePopup 引了怪物 Transform，怪物没了判 null 跳过 |
| 传染性 | 网络包和 UI 搅一起 → 飘字 bug 导致客户端服务端数据不一致 | 改一张卡的动画只改那个协程，不碰逻辑/数据/其他系统 |
| 该不该解耦 | 该——耦合代价由不在场的人承担 | 不该——耦合了又怎样？不值得建一座桥 |

**核心判断线：耦合的东西是否会在别处产生后果。** 会的，解耦。不会的，让它烂着。

用户的过度工程化习惯来自 MC 环境——那里耦合确实会炸（ScriptBus 牵一发、网络包乱发、God Class 拆不开）。但把这个敏锐带到动画上，敌人不存在。

## 最小跑通目标

选一张"力量+3"的牌 → 打出去 → buff 挂上 → 攻击力自动变 → UI 自动亮。不需要多张牌、不需要回合系统。就一张。

## 相关

- [[card-game/index]] — 项目总览
- [[battledata-architecture]] — MC 战斗数据架构（类似的三层问题）
- [[map-module-constraints]] — MC Map 模块约束（过度解耦的来源环境）
