---
name: unity-supervisor-agents
description: 在用户显式调用或当前会话允许且复杂 Unity 任务有明确委派收益时，按职责组织独立探索、实现和验证。普通任务继续主线。
---
# Unity Supervisor Agents

详细契约唯一主源：`<shared-memory>/Knowledge\References\Unity_Supervisor_First_Agent_Workflow.md`。

- 显式调用本技能时启用监管；其他情况先遵守当前会话的委派权限，边界不清继续主线。
- 只创建实际有工作的角色，不固定走 tester → worker → tester；同 checkout 单写者，Editor 单 lease。
- 使用 agent_type 选择角色、task_name 命名任务，专用角色默认 fork_turns="none"。只传窄目标、路径、契约和验收证据。
- 磁盘工作不连接 Unity；Editor 风险操作遵守共享 Unity Common，普通读取不建快照。
- 只有具体 CLI 不适用请求已落盘时才创建 unity_mcp；用 request_id + project 唤醒，并精确 claim 该 ID。不预建、不常驻。
- 主线负责最终审查；模型留在 profile，记忆只记录长期新事实。
