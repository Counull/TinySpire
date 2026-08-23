---
title: ByteRover · TinySpire 项目检索适配
page_type: tool
lifecycle: active
updated: 2026-08-24
status_source: ../STATUS.md
scope: TinySpire private instance only
---

# ByteRover · TinySpire 项目检索适配

ByteRover is a **non-authoritative locator/cache**。它帮助定位项目知识，但不拥有项目事实、不解决文档冲突，也不授予读取、写入、实现、curate、commit 或 push 权限。

## Seed 集

只 seed 小型路由骨架，避免把长历史塞进默认检索：

1. [项目入口](../README.md)
2. [唯一当前状态](../STATUS.md)
3. [Run Roadmap](../RUN_ROADMAP.md)
4. [实现架构约定](../ARCHITECTURE_CONVENTIONS.md)
5. [验收索引](../06_testing/README.md)

## Query 合同

1. 先 query，再要求返回 **exact repository-relative source paths**。
2. 在形成事实、计划或动作前打开并核对原文；缓存回答与原文冲突时，以项目 canonical source 为准并显式报告冲突。
3. 没有来源的结果只当检索线索，不能当作确认事实。
4. ByteRover 不可用时直接走 `README.md → STATUS.md → 至多一份相关页`，项目工作不得因此停摆。

## Curate 合同

- 只 curate 已确认且耐久的路由、当前里程碑、决策或真实验证；临时推测、聊天摘要、密钥、个人信息和完整历史目录不得进入 context tree。
- 一次只提交与主题直接相关的少量原文件。新增知识后必须 query 回查，并验证返回的精确原路径与原文。
- `.brv/` 是 ByteRover 自己管理的本地 context tree，不进入 TinySpire Git；云端账号或 Space 只影响 BRV 同步，不改变项目事实源。

本次关联证据见 [ByteRover 项目知识关联验收](../06_testing/2026-08-24-byterover-project-context.md)。
