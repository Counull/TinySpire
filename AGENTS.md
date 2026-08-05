# 项目协作规则

本文件适用于整个仓库。除非用户在当前请求中明确覆盖，所有 Agent 都必须遵守以下规则。

## 修改范围

- 用户提出问题、讨论方案、询问可行性或权衡时，只进行分析和回答，不得直接修改文件。
- 只有用户明确要求“实现、修改、添加、删除、迁移、重构”等操作时，才可以改动项目。
- 未经用户明确确认，不得进行大规模重构、程序集重新划分、目录整体迁移、启动流程替换、依赖注入体系调整、热更新架构改造或构建管线重写。
- 如果实现范围明显超出用户原始描述，必须先说明预计影响的文件、风险和回滚方式，并等待用户确认。
- 不得把“未来可能需要”当作当前实施的授权。优先解决已经存在的需求。

## 工作方式

- 优先使用 Unity MCP 进行 Meta 生成和最终验证。
- 大任务必须拆成可独立验证的小步骤；每完成一步再继续下一步。
- 修改前先检查当前工作区状态，保留用户已有改动，不覆盖、不清理无关文件。
- 代码改动完成后，如果产生了新的口径、实现决策、流程约定或验证结论，必须依据 `Docs/_external/llm-workflow/LLM_WORKFLOW.md` 的分层与读取规则维护 `Docs/Copilot_Daedalus/`：更新 `SESSION_LOG.md` 记录本次变化与后续动作；代码决策写入 `CODE_DECISIONS.md`，实现方案写入 `plans/`，测试与验收结果写入 `06_testing/`。不得只把新口径留在聊天记录或代码注释中。
- `Docs/Copilot_Daedalus/` 的维护必须使用项目相对路径，并遵守该目录现有的 index、status source、decision source 约定；不得把 TinySpire 私有语义回写到 `Docs/_external/llm-workflow/`。
- 用户说“暂停、停止、先别做”时，立即停止所有写入和外部操作，只汇报当前状态。
- 遇到不确定的架构选择时，优先询问，不自行替用户作重大决策。
  每个函数至少有中文注释说明。

## Unity 项目规则

- 不得在未明确告知用户的情况下启动多个 Unity Editor 或批处理实例。
- 不得擅自结束用户的 Unity 进程、删除项目锁文件或清理 Library、Temp 等目录。
- 场景、Prefab、ProjectSettings、asmdef 和 HybridCLR 设置属于高影响文件；修改前必须确认确有必要，并保持改动最小。
- 使用批处理 Unity 验证前，先确认没有其他 Unity 实例占用项目。验证结束后确认批处理进程已经退出。
- 讨论 HybridCLR、AOT/热更新边界或配置热更方案时，默认只给设计建议；只有用户明确要求实施后才能修改程序集结构。

## 配置表与资源包流程

- 只要修改 `DataTables/Datas/` 下的表格或表定义，交付前必须运行 Luban 生成（`DataTables/gen.bat` 或等价命令），更新生成代码与 `TinySpire/Assets/GameData/` 内的 JSON。
- `DataTables` 中凡是引用 Unity 素材的业务字段，统一保存无目录、无扩展名且大小写精确匹配文件名的短键，并使用 `*_key` 命名；禁止把 `Assets/...`、反斜杠路径或文件扩展名写入这类字段。场景地址、`Assets/GameData/*.json` 等 Addressables 基础设施清单项不是业务素材字段，可以继续使用完整 `Assets/...` 稳定地址。
- 每个配置素材域必须具有专用素材目录、唯一 Addressables 逻辑地址前缀、运行时短键转换函数和构建期解析器。构建期必须拒绝空键、首尾空白、目录、扩展名、忽略大小写后的重名、大小写漂移、缺失素材和不符合该素材域导入/组件契约的资源；专用 Addressables Group 必须与配置实际引用集合精确同步并删除陈旧条目。
- 运行时只能把短键转换为逻辑地址后调用 Addressables API；不得用 `AssetDatabase`、`Resources.Load`、文件系统路径或配置中的 `Assets/...` 业务素材路径绕过 AssetBundle。`AssetDatabase` 和真实 `Assets/...` 路径只允许存在于 Editor 构建期的索引、校验与组装逻辑中。
- Luban 生成、Localization 资源变更或其他方式改动可寻址内容后，必须执行 `TinySpire/Build/Sync and Build All`，完成生成、同步与本地 Addressables 内容重建；仅刷新 Unity 资源数据库不算完成。只有明确确认没有表格生成需求时，才可单独执行 `TinySpire/Addressables/Build Local Content`。
- 验收至少确认：目标 JSON 位于 `Assets/GameData` 且业务素材字段只含短键；专用 Group 只暴露逻辑地址；本地 Addressables 内容已重建；启动加载链路未出现 InvalidKey/资源地址无效错误。新增素材域或修改其地址/加载实现时，还必须用最新 BuildLayout 证明目标资产由 `AssetBundleProvider` 打入物理 bundle，并在 `Use Existing Build`（Packed Play Mode）或 Player 中完成一次真实加载；`Use Asset Database`（Fast Mode）通过不能作为 AB 包加载证据。

## 验证与交付

- 修改后只运行与改动规模相称的检查，不为了验证而扩展修改范围。
- 如果验证被编辑器占用、权限或外部环境阻塞，应停止并说明，不得通过强制结束进程等高风险方式绕过。
- 交付前检查本次代码改动是否带来了需要沉淀的新口径；如有，先完成 `Docs/Copilot_Daedalus/` 的 LLM Wiki 同步，再汇报结果。
- 最终回复应明确说明：改了什么、哪些内容没有改、验证结果，以及仍需用户处理的事项。

## 默认原则

当“立即实施”和“先确认范围”之间存在疑问时，选择先确认范围。
