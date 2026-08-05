---
title: DOTween Pro 仓库净化与免费版独立验证
page_type: testing
lifecycle: active
updated: 2026-08-05
---

# DOTween Pro 仓库净化与免费版独立验证

## 范围与结论

本次只处理 DOTween 的公开分发边界：免费 DOTween、必要 DemiLib 与官方 `DOTween/readme.txt` 继续跟踪；DOTween Pro 源码、DLL、示例、目录 Meta 与 Pro readme 已从当前树和本地准备的新 `main` 可达历史移除，并由精确忽略规则保持为持证开发者本地内容。GitHub 因五个非 Pro LFS 对象缺失而拒绝 force-push，故远端公开历史切换尚未完成；远端仍保持原 SHA。没有修改业务代码、Scene、Prefab、ProjectSettings、asmdef、HybridCLR、DataTables、Localization、Addressables 配置或 `.gitattributes`，也没有清理或上传 Git LFS 对象、处理既有 Fork/Clone。

## 可观察失败基线

- 清理前 `HEAD` 为 `64596d248ab25323af98e60215824eb4db9c2550`，`origin/main` 为 `3e7b8e5100015686a3c12260155e9b7076456a26`，本地 `main` 领先远端 2 个提交，工作区干净。
- 六个目标路径合计有 **46 个已跟踪项**：`DOTweenPro/` 27 个、`DOTweenPro.meta` 1 个、`DOTweenPro Examples/` 15 个、示例目录 Meta 1 个、`readme_DOTweenPro.txt` 与其 Meta 各 1 个。
- 首次引入提交为 `fc6441d8cbfdc0fad21849738c6a5d27f021e7e9`（`chore:Dotween`，2026-07-29）；该提交属于 `origin/main` 的可达历史，因此只删除当前树不能停止公开历史继续分发。
- Pro 的两个 DLL 与示例 logo 使用 LFS 指针，其余 Pro 源码和 Editor 代码是普通 Git blob；LFS 与 `.gitattributes` 不能替代路径历史净化。

## 当前树、忽略与本地恢复

- `git rm --cached` 只从索引移除了六个目标路径；随后 `git ls-files` 对这些路径为 0，免费 `DOTween/` 与 `DemiLib/` 仍有 **307 个跟踪项**。
- `TinySpire/.gitignore` 对六个目标逐项生效；没有忽略整个 `Demigiant/`，不会误删免费版。
- 清理前在 `TinySpire/.codex_work/dotweenpro-backup-20260805/` 创建了本地文件备份、`sha256.csv` 与完整 Git bundle；bundle 已通过 `git bundle verify`。该目录已被仓库规则忽略，不会进入提交或远端。
- 为验证无 Pro 环境，六个本地路径曾临时移动到已忽略的验证目录；验证后全部恢复到原 Unity 路径。恢复后的 **50 个物理文件 SHA-256 与备份完全一致（0 mismatch）**。

## 无 Pro Unity 验证

验证只使用当前唯一的 Unity 6000.5.5f1 Editor，没有启动批处理或第二个 Editor，也没有清理 Library/Temp。

- 移出 Pro 后强制刷新并全量重编译，Editor 日志记录 `Tundra build success`，耗时 69.15 秒；域重载完成后 Console 为 0 Error / 0 Warning。
- 完整 EditMode 任务 `5b817700afff40f1a4928b2e78f01a25` 为 **459/459 passed、0 failed、0 skipped**，用时 39.512084 秒。
- 紧接完整测试任务第一次进入 Play Mode 时，测试生命周期遗留的已销毁 UI target 触发 4 条 DOTween Safe Mode warning；退出、清空 Console 后独立重新从 BootstrapScene 启动，成功进入 BattleScene，存在唯一 `BattleLifetimeScope`，Console 为 **0 Error / 0 Warning**。前述测试后污染未作为正常启动证据，也未伪报为产品零告警。
- 恢复本地 Pro 后再次全量编译与域重载成功，Editor 回到 BootstrapScene、非 Play Mode、idle，Console 为 0 Error / 0 Warning。

上述证据证明当前代码、场景序列化与自动回归不要求 Pro，运行时使用的是保留的免费 DOTween API；本地 Pro 可以存在，但不会成为干净 Clone 的隐含依赖。

## 历史与远端验证

- 清理提交旧 SHA `bec2f892c8f38f995046e8f11f088e0921b5c2e2` 经过滤后映射为 `3c831013046e9f5fb30097701533b66c80abeb0e`；改写前后 tip tree 均为 `f8003ddb36b14f79fc5c2e68ddfbd0f937043887`，证明历史过滤没有改变最终非 Pro 内容。
- 过滤器先生成本地新 `main`，随后在 `TinySpire/.codex_work/dotweenpro-history-rewrite-20260805.git` 独立重放；两次都得到相同 HEAD 与 tree。镜像只有 `refs/heads/main`、无 Tag，共 81 个提交，`git fsck --full --no-reflogs` 通过。
- `git rev-list --objects main` 对六个目标路径为 0，目标路径的 `git log` 为 0；免费 DOTween/DemiLib 当前树仍为 307 个跟踪项。旧远端提交 `3e7b8e5100015686a3c12260155e9b7076456a26` 映射为 `8798f1576374676690a35e3fb63e80a684acf490`。
- 使用 `--force-with-lease=refs/heads/main:3e7b8e5100015686a3c12260155e9b7076456a26` 尝试只更新 `main` 时，GitHub 以 `GH008` 拒绝：新历史引用了远端尚无的五个 LFS 对象。回读确认远端仍为 `3e7b8e5100015686a3c12260155e9b7076456a26`，没有发生部分更新。
- 五个对象均来自清理前本地领先远端的两个提交，且均非 Pro：`m10d-bootstrap-default.png`，三张 `Docs/Hermes_Pegasus/art/assets/art-style/scenes/candidates/` 场景候选图，以及 `TinySpire/Assets/Arts/Runtime/UI/Battle/Candidates/ui_battle_end_turn_button_ref_v01.png`。Pro 的 DLL、Editor DLL 与示例 logo 哈希均不在该缺失集合，也不在新历史中。
- 用户明确要求不修改 LFS，因此没有执行 GitHub 建议的 `git lfs push --all`，没有上传上述五个对象。完成远端切换需要新的明确选择：允许只上传这五个既有非 Pro LFS 对象，或另行授权改写/排除包含它们的本地提交；在此之前不得声称远端历史已净化。

## 许可证与 NOTICE

- `THIRD-PARTY-NOTICES.md` 已把免费 DOTween 的官方自定义许可与 Pro 的每席位专有边界分开，不再把全部组件误写为 MIT。
- NOTICE 同时列出 Luban 二进制内含依赖、UPM/NuGet、Unity 专用许可、Git 子模块与模板来源；`Tools/Luban/NOTICE.md` 指向根清单。
- 免费 DOTween 的版权、免责声明和原始 `DOTween/readme.txt` 保留；Pro 不由 TinySpire 再许可。依据见 <https://dotween.demigiant.com/license.php> 与 <https://unity.com/legal/as-terms>。

## 恢复与排除项

- 历史改写前恢复点为本地 bundle；物理 Pro 文件另有逐文件哈希备份。恢复这些备份只能用于本地持证环境，不得再次推送到公开仓库。
- 本次不删除或上传 GitHub LFS 对象、不修改 `.gitattributes`、不召回既有 Clone/Fork、不联系 GitHub Support；这些排除项是用户明确边界。
- 历史改写会改变引入 Pro 之后的提交 ID。协作者不得把旧分支直接推回新历史；应重新 Clone，或显式迁移自己的提交。
