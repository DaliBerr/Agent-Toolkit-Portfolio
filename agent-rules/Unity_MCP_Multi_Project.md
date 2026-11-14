<!-- Memory Vault Managed: 2026-05-22 Multi-Repo AGENTS Sync -->
# Unity MCP Multi Project Routing

本机同时存在多个 Unity MCP 端点。端点表只供 `$unity-editor-ops` CLI 和 `unity_mcp` broker 路由；全局配置不注册 Unity MCP，plain `unityMCP` 不是可调用入口。

## Endpoint Map

| Repo | CLI project | Path | Endpoint | Typical Scene Context |
| --- | --- | --- | --- | --- |
| Unity Project 4 | `UnityProject4` | `<local path>` | `http://127.0.0.1:8080/mcp` | `Assets/Scenes/RefactorScene.unity` |
| Colo-3D | `Colo-3D` | `<local path>` | `http://127.0.0.1:8081/mcp` | `Assets/Scenes/StartPage.unity` or `Assets/Scenes/Main.unity` |
| Lilith | `Lilith` | `<local path>` | `http://127.0.0.1:8082/mcp` | `Assets/Scenes/StartUp.unity` or `Assets/Scenes/Main.unity` |
| ArtTest (Lilith art test) | `arttest` | `<local path>` | `http://127.0.0.1:8083/mcp` | `Assets/Scenes/SampleScene.unity` |

## Required Check
1. 普通代码和静态任务不连接 Unity；需要 Editor truth 时先执行 `$unity-editor-ops route <project>`，再用其 `status`、`tools-list`、`resource-read` 或有界工具调用。
2. 用 `status` 的实例和 Editor 状态确认项目、端点和场景上下文，再进行 scene、prefab、component、console、screenshot 或 test 操作。
3. CLI 是无模型 Python MCP 客户端，直接连接表中 endpoint；它不是小 agent，也没有再启动一层 MCP server。route、status、resource、Console、refresh、EditMode 和参数明确的 tool-call 由当前 lease owner 直接执行。
4. 只有具体操作无法由 CLI 有效承载、会显著污染主线上下文或需要专用 MCP 配置时，root 才创建 broker。lease owner 先用 `$unity-editor-ops broker submit` 写入完整原子请求，root 再只用 `request_id + project` 通过 `local_agents` 唤醒；`max_depth=1` 子代理不得自行创建。调用者等待终态，同 endpoint 在 claimed 到 complete/fail 期间互斥；直接 CLI/MCP 仍由主线 lease 串行，不与活动 broker 并行。
5. broker 没有业务设计或仓库文件所有权，但可在有效 lease 下机械执行参数已冻结的 Unity MCP 写操作；不得猜测接线、补参数或接收未入队的自然语言写目标。无请求时立即结束，不轮询。
6. 如果目标项目不能确认、当前 lease 不匹配或 Editor 前置条件缺失，不得修改 Unity 状态。
7. ArtTest 是 Lilith 邻接美术协作测试仓库，不独立治理；仅在明确需要检查或迁移 ArtTest 内容时路由到它，不能作为 Lilith Editor truth。

## Lilith 8082 Manual Recovery
- 仅当 CLI / broker 已报告 8082 connection refused 且用户尚未恢复服务时，才可在单独窗口用 `uvx` 启动与项目包版本匹配的 project-scoped `mcp-for-unity` server（`--transport http --http-url http://127.0.0.1:8082 --project-scoped-tools`）。
- 版本先核对当前 `com.coplaydev.unity-mcp/package.json`，不要沿用旧版本命令；不要为了测试命令而重启正在工作的 server。恢复后用 `$unity-editor-ops status Lilith` 再确认实例和 Editor state。
- CLI 负责 Streamable HTTP 初始化、`mcp-session-id` 和 SSE 响应处理；不要重建临时 PowerShell 协议调用。

## Prefab Stage Caution
- Unity MCP scene hierarchy/search tools can surface the current prefab stage even while `get_loaded_scenes` and `editor_state` report the intended scene loaded and active. If hierarchy/search returns an unexpected prefab root, do not mutate through that result; target the loaded scene explicitly, for example with `EditorSceneManager.GetSceneByPath(...)` via `execute_code`, then save only the intended scene.

## After Script Edits

遵守 [Unity_Repo_Common_Agent_Rules § Refresh And Compilation](Unity_Repo_Common_Agent_Rules.md#refresh-and-compilation)，不在路由文档重复刷新流程。

## Execute Code Roslyn Backend
- If `execute_code` reports `Roslyn (Microsoft.CodeAnalysis) is not available`, MCPForUnity expects Roslyn DLLs under the target Unity repo at `Assets/Plugins/Roslyn/`.
- MCPForUnity's built-in installer downloads these NuGet DLLs: `Microsoft.CodeAnalysis.dll` and `Microsoft.CodeAnalysis.CSharp.dll` 4.12.0, `System.Collections.Immutable.dll` and `System.Reflection.Metadata.dll` 8.0.0, and `System.Runtime.CompilerServices.Unsafe.dll` 6.0.0.
- In Lilith on 2026-06-02, direct installation to `Assets/Plugins/Roslyn/` plus Unity refresh/import made `execute_code` with `compiler=roslyn` succeed; Console stayed at 0 errors and `dotnet build Lilith.Kernel.csproj --no-restore` stayed 0 warning / 0 error.
