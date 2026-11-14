---
name: unity-editor-ops
description: 无模型、无 daemon 的 Python CLI，用于本机 Unity MCP 路由、读取、受保护的 Editor 操作和按需 broker 队列；不用于 PlayMode。
---
# Unity Editor Ops

`scripts/unity_ops.py` 是标准库 Streamable HTTP 客户端，不是子代理。普通磁盘工作不用它；Editor 操作先读共享 Unity Common 与 Unity_MCP_Multi_Project，按操作风险完成路由/快照/Prefab Stage/lease 检查。

## 路径与读取

```powershell
$UnityOps = '<skills-root>\unity-editor-ops\scripts\unity_ops.py'
python $UnityOps doctor
python $UnityOps route lilith
python $UnityOps status lilith
python $UnityOps tools-list lilith
python $UnityOps resource-read lilith mcpforunity://editor/state
python $UnityOps console lilith --arguments '{"limit":50}'
```

`doctor/route` 不联网；status 返回实例和 Editor 状态，由调用者核对真实项目/场景。可用项目以 route/doctor 输出为准，expected_namespace 不是直接工具入口。只读查询不因读取建立快照。

## 风险操作

完成共享安全前置条件后才执行：

```powershell
python $UnityOps refresh lilith --arguments '{"compile":"request"}'
# 示例类名仍须在当前源码中核实；检查 SetUp/TearDown、基类及调用链不触发房间生成。
python $UnityOps tests lilith --mode EditMode --arguments '{"test_names":["RuntimeAutomationCoreTests"]}' --poll
```

- Lilith 必须提供非空精确 *Tests 类/方法名；空列表、通配符、命名空间、程序集/分类筛选及已知房间生成测试被拒绝。只接受 mode/test_names；服务筛选 schema 改变时先核查再调整客户端。
- 同一门禁用于 tests、tool-call run_tests 和嵌套 batch。它不能证明夹具的间接副作用，调用者仍须审核源码；任意 execute_code/菜单不能用于绕过测试禁令。
- CLI 拒绝明确 PlayMode 请求。通用 `tool-call PROJECT TOOL --arguments JSON` 的业务授权、路由和安全证据由 lease owner 核实；CLI 不会自动授予权限。
- `--poll` 有界等待测试 job；超时不表示测试已停止，不立即重跑。单次网络 timeout 1–120000 ms，默认 10000；poll 默认 30000 ms。

## 按需 broker

常规操作由 lease owner 直接用 CLI。只有具体操作确实不适用时，先 submit，再让 root 用 request_id + project 唤醒 broker。

```powershell
python $UnityOps broker submit lilith --requester-role tester --kind tool-call --name read_console --arguments '{"limit":50}'
python $UnityOps broker claim lilith --request-id REQUEST_ID
python $UnityOps broker complete REQUEST_ID --result '{"ok":true}'
python $UnityOps broker fail REQUEST_ID --error '操作未完成，已核实执行者停止'
```

写请求还必须传 `--mode write --lease-id ID --preconditions JSON --expected-readback JSON`。只读操作采用显式允许清单，未知工具/写入/batch 中的写入不能标为 read。
同 endpoint 已有 claimed 时不领取第二个请求；complete/fail 才释放占用，超时不自动释放。异常恢复先确认原执行者停止和真实结果。直接 CLI/MCP 与 broker 仍须遵守同一 lease，不并行操作同一 Editor。

存储、写请求示例和恢复说明见 [references/broker-protocol.md](references/broker-protocol.md)。
