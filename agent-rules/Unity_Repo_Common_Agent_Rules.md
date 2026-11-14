<!-- Memory Vault Managed: 2026-07-31 Memory Architecture Refactor -->
# Unity Repo Common Agent Rules

本文件是三个登记 Unity 仓库的通用安全主源。repo-local `AGENTS.md` 只保存项目差异和本文件的触发入口；冲突时以 repo-local 硬规则为准。

## When To Read

C# 实现、UI 绑定和迁移按需读取 Coding Contract；普通磁盘任务不连接 Unity。序列化写入、refresh/import、Unity tests 或 Editor 状态变更前读取本文件安全章节。

## Serialized Asset Safety

- 纯磁盘读取 `.prefab`、`.unity`、`.asset`，以及不会触发 refresh/import 或状态变更的只读 Editor 查询，不需要创建 Prefab 快照。
- 首次准备写入 Unity 序列化资产、触发 refresh/import、改变 Editor 状态或运行 Unity tests 前，备份全部 `Assets/**/*.prefab` 与对应 `.prefab.meta` 到 `Backup/PrefabSafetySnapshots/<yyyyMMdd_HHmmss>/`，保留相对路径。
- 快照目录只保留最近 5 份；清理旧快照前必须验证目标位于 `Backup/PrefabSafetySnapshots` 内，并通过 Windows 回收站移除（PowerShell 可使用 `Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(..., RecycleOption.SendToRecycleBin)`；禁止使用 `Remove-Item`、`rm` 等方式永久删除）。清理后读回确认活动目录仍保留最近 5 份。
- 用户刚手动修改 prefab、即将写 prefab、运行大组测试、`external_changes_dirty=true` 或用户明确要求时，在对应风险边界重新建快照。

## Prefab Stage And Tests

- 序列化写入、refresh/import、状态变更或测试前检查 Prefab Stage。若打开且 `isDirty=true`，停止并询问用户；agent 不保存、不关闭，也不执行依赖该状态的写入、刷新或测试。
- 若打开且 `isDirty=false`，关闭后重新确认目标 prefab hash 与关键层级。
- 关闭 Prefab Stage 后记录全部 prefab/meta hash；测试后立即比较，并按风险抽查关键节点。
- 发现意外 prefab 改写、回滚、丢节点或未知差异时停止。只有能证明差异属于本轮、没有并发写入且恢复不会覆盖任务外改动时，才可从本轮快照恢复。
- Agent 默认不进入 PlayMode，也不运行 PlayMode tests；不得从历史会话推定本次例外授权。
- 用户遗留 PlayMode 允许安全停止；停止属于状态变更，先核实路由和保护条件。无法确认安全时询问。

## Editor Route

- 普通代码和静态任务不连接 Unity。
- 需要 Scene、Prefab、GameObject、Component、Console、Test、Build、Package 或 Screenshot truth 时，先用 `$unity-editor-ops` 确认项目、endpoint、实例和场景。
- route、status、resource、Console、refresh、EditMode 和参数明确的普通调用由当前 lease owner 直接使用 CLI。
- 只有具体操作无法由 CLI 有效承载、会显著污染主线上下文或需要专用 MCP 配置时，才按 Supervisor Workflow 创建 `unity_mcp` broker。
- 项目别名与 endpoint 以 [Unity_MCP_Multi_Project](Unity_MCP_Multi_Project.md) 为准；角色、写权和 lease 以 [Unity_Supervisor_First_Agent_Workflow](Unity_Supervisor_First_Agent_Workflow.md) 为准。

## Refresh And Compilation

- 修改 C#、asmdef、packages 或编译相关资产后，检查 Console、场景或测试前执行全量 refresh/import，并等待 compile/domain reload。
- 新脚本、字段或 `MenuItem` 在全量刷新后仍不可见时，再 reimport 相关脚本并等待 reload。
- 不要为绕过刷新把新类型塞入无关旧脚本。

## Coding Contract

- 核心绑定使用显式序列化引用、组件/接口契约、配置或上层 wiring，不依赖对象名称、层级和兄弟顺序。
- UI prefab 是 authored source of truth。绑定断裂明确报错，禁止运行时兜底创建另一套容器、按钮、格子、文本或布局。MCP 与磁盘均找不到用户描述的结构时询问，不自行补建。
- 只改任务所需；迁移默认切换到新实现，除存档、联机、外部接口或用户要求外不长期保留双路径。
- 运行时自动状态变化最终说明对象、字段、触发时机和影响；新文字 UI 默认 TMP。
- Windows 小步修改，保留用户 dirty。单例、持久化与依赖方向遵守 repo-local 硬规则。

## Focus And Ignore

- 优先：运行时/Editor C#、asmdef、packages、相关 ProjectSettings，以及任务直接相关的 scene/prefab/data。
- 默认忽略：Library、Temp、Obj、Logs、UserSettings、build output、IDE/VCS 噪音、生成工程文件、大体量素材和 `.meta`。
- 只有 GUID 丢失、Missing Script/Reference、merge 损坏或用户明确要求时主动读取 `.meta`。

## Documentation

记忆结构与收尾以 [Memory_Rules](Memory_Rules.md) 为准。本文件不复制 Supervisor、MCP 或项目特定协议。
