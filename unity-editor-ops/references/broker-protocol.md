# Unity MCP Broker Protocol

队列是本地交接记录，不执行 Unity，也不替代路由和 lease 审查。默认位于 `%LOCALAPPDATA%/OpenAI/Codex/unity-mcp-broker`，不可用时回退临时目录；调用者应使用输出的同一个 state_root。

## 生命周期

`pending → claimed → completed|failed`。JSON 同目录临时写入后原子替换；同 endpoint 的领取和结束在文件锁下串行。
`claim PROJECT --request-id ID` 只领取指定请求，找不到返回 empty，不领取其他请求。旧的无 ID claim 仅供显式 FIFO 使用。
同 endpoint 有 claimed 时拒绝后续 claim；claimed 是执行期占用记录，complete/fail 后才能继续。不同 endpoint 可以独立领取。request_id 在任何状态已存在都不可重用。

## 写请求

```powershell
python $UnityOps broker submit lilith --requester-role worker --kind tool-call --name refresh_unity --arguments '{"compile":"request"}' --mode write --lease-id WORK-LEASE-ID --preconditions '{"snapshot":"ABSOLUTE_SNAPSHOT_PATH","prefab_stage":{"is_open":false,"is_dirty":false},"play_mode":false}' --expected-readback '{"kind":"resource-read","uri":"mcpforunity://editor/state"}'
```

请求 schema_version=2，包含 project/route、operation(kind/name/arguments)、mode、timeout_ms、lease_id、requester/requester_role、preconditions、expected_readback；CLI 添加 ID 和时间。

- kind 仅 tool-call/resource-read/tools-list。read 只接受明确只读工具；未知和写工具必须 write；batch 递归检查。
- write 检查非空 lease_id、snapshot、prefab_stage.is_dirty=false、play_mode=false。字段的真实性、快照完整性和当前 Editor 状态仍由 lease owner/broker 读回核实，不能把声明当证明。
- 操作参数固定，不允许 broker 猜业务、补参数或自行编写代码。execute_code 只执行请求中完整代码。
- Lilith 测试必须精确且已审核无房间生成；CLI 同时检查直接/批量测试，不能用脚本或菜单绕过禁令。

## 恢复与边界

root 先 submit，再用 request_id + project 唤醒 broker；broker 精确 claim、执行与读回，然后记录 complete/fail 一次，无请求立即结束。
请求超时不自动释放或重试。执行者异常退出时，主线先确认进程/任务已停、操作是否生效和 Editor 状态，再 fail 原请求；不能靠等候到期假定无人写入。遗留文件锁也不得在未确认原持有者停止前移除。

`status` 读取状态，`wait` 有界等待，`cleanup` 只移除到期 completed/failed JSON，不清理 pending/claimed。任一异常保留证据，不自动重放。
队列互斥不覆盖另一个 state_root、直接 CLI 或原始 MCP；这些通道必须由主线同一 lease 串行管理。
