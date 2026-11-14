# Runtime Automation Agent 工作流

状态：Active。适用于已授权的独立 Windows Development Player 验证，非 Editor PlayMode。连接/构建/存档边界见 GameplayMcpValidation.md；最终构建和产品验收仍按 README 的用户边界。Verification 用真实输入，QA 仅布置条件并隔离存档。

1. 确认目标是 Verification 还是 QA；玩法真实性使用 Verification，布置无关前置条件或美术展厅使用 QA。
2. 调用 `get_capabilities`，记录 session、build、mode、scene epoch、revision 与 event sequence。
3. 用 `query_state` 和 `query_entities` 获取下一步所需结构；字段不足就停止依赖截图，登记 Provider 缺口。
4. 从 `list_actions` 选择最接近验收目标的证据等级：输入时序用 `atomic_input`，流程闭环用 `semantic_input`，无关条件布置才用 `direct_qa`。
5. 执行动作时传当前 `expected_revision`。只在 `observed_effect=true` 时报告生效；否则记录 `blocked_reason` 并查询新状态。
6. 使用 `get_events` 或 `wait_for` 等待状态，不使用截图决定路线、目标、教程页或战斗结果。
7. 视觉节点调用 `capture_visual`，记录 manifest 与 hash；登记基线的静态内容再运行外部比较器。
8. 流程结束执行 `atomic.release_all`，读取 `get_diagnostics`，确认没有未释放输入和新的运行时异常。

QA 报告必须逐项标明使用过的 `direct_qa` 修改及其目的。QA 修改只能布置条件，不能代替需要验收的玩法结果。
