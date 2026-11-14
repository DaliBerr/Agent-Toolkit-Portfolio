# Gameplay MCP：Lilith Runtime Automation Player 通道

本通道只驱动独立的 Windows Development Player，不进入 Unity Editor PlayMode。常规流程必须先查询结构化状态，再执行动作并读取动作回执；截图只用于构图、排版、遮挡和像素差等视觉判断。

## 构建配置

- `Tools > Lilith > Gameplay MCP > Build Verification Player...`：只包含 Build Settings 中正式启用的场景，不暴露 QA 工具。
- `Tools > Lilith > Gameplay MCP > Build QA Player...`：额外包含 `Assets/Scenes/Automation/RuntimeQaGallery.unity`，并在构建前重建 `Assets/Automation/RuntimeAutomationQaContentCatalog.asset`。
- 两种配置都会临时启用 `INCLUDE_COM_NOWSPRINTING_TEST_HELPER`，先构建并校验 Addressables，再构建 Development Player，最后恢复原 define。
- 构建输出根目录会生成 `runtime-automation-manifest.json`，记录协议版本、构建 ID、模式、Unity/Git/dirty 状态、场景、Addressables settings hash 和 QA 目录信息；该文件不写回工程资源。

## 启动

Verification：

```powershell
& '<build-dir>\Lilith-RuntimeAutomation-Verification\Lilith.exe' --enable-lilith-gameplay-mcp
```

QA：

```powershell
& '<build-dir>\Lilith-RuntimeAutomation-QA\Lilith.exe' --enable-lilith-gameplay-mcp --lilith-runtime-automation-qa
```

可选参数：

- `--lilith-gameplay-mcp-port 8010`：修改 loopback 端口。
- `--lilith-runtime-automation-artifact-root <绝对路径>`：修改本次 session 的视觉/诊断产物根目录。

未传 `--enable-lilith-gameplay-mcp`、不是 Development Build 或是 Release Player 时，服务不会启动。QA 权限只在进程启动时读取，不能运行中提升。QA 模式将存档根切到 session 目录的 `QaSave`，不会污染正常存档。

## Codex 连接

仓库本机 `.codex/config.toml` 将 `http://127.0.0.1:8010/mcp` 声明为可选服务，并显式允许通用协议工具与旧兼容工具。Player 启动后需要重开 Codex 任务或重启 Codex，才能重新完成工具握手。

连接后的最小结构化流程：

1. `lilith.get_capabilities`：确认 session、build、运行模式、Provider、动作和视觉能力。
2. `lilith.query_state`：读取所需领域；不要先截图猜状态。
3. `lilith.query_entities`：取得稳定语义 ID、位置、Bounds、组件摘要与局部分页结果。
4. `lilith.list_actions` 后调用 `lilith.execute_action`。
5. 只有 `observed_effect=true` 才能报告动作已生效；仅有 `dispatched=true` 代表输入已经发出。
6. 用 `lilith.get_events` 或 `lilith.wait_for` 等待结构化条件。
7. 视觉节点再调用 `lilith.capture_visual`；结束前执行 `atomic.release_all`。

旧的 `get_runtime_state`、`get_combat_state`、移动、瞄准、攻击和 `wait_until` 仍保留为兼容包装器。Gameplay MCP 原生 GameObject/UI/截图工具只用于低层排障。

## QA 展厅与内容目录

- `Assets/Scenes/Automation/RuntimeQaGallery.unity` 是空白组合场景，只包含相机、四层高度根节点和 QA 内容目录引用，不包含房间生成器或完整进度夹具。
- 可组合 Prefab 只能放入 `Assets/Automation/RuntimeQaContent`。构建器按相对路径生成 `qa.content.<path>` 稳定 ID。
- `qa.spawn_content` 只能实例化目录中精确登记的 Prefab；`qa.destroy_spawned_content` 只能销毁本 session 由该接口生成的对象。
- 任意反射、私有字段写入、任意资产路径和结果型作弊命令均不提供。

## 安全边界

- 只监听 `127.0.0.1`；不使用通配或远程地址。
- Verification 模式隐藏并硬拒绝 `qa_list_mutations` 与 `qa_execute_mutation`。
- QA 修改使用已登记实体与公开运行时 API，回执证据等级固定为 `direct_qa`。
- Agent 不得进入 Editor PlayMode、运行 PlayMode Test Runner、运行房间生成测试或通过宽泛 EditMode 组间接触发房间生成。
- Editor tests 只允许精确筛选 `RuntimeAutomationCoreTests` 等确认安全的测试类。
