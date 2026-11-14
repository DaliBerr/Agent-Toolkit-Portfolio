# Unity Supervisor-First Agent Workflow

适用于 Lilith、UnityProject4、Colo-3D；ArtTest 仅为 Lilith 邻接美术仓库。模型只放 agent profile。

## Activation Gate

默认主线。显式调用 unity-supervisor-agents 时启用监管；其他情况须符合当前会话委派授权，且任务很复杂、存在明确独立工作。仅多文件不足以委派，边界不清继续主线。用户禁用子代理时不自动启用。
只创建实际有工作的角色；不固定要求 explorer/tester/worker 全部出现，不要求每个任务都测试先行。

## Roles

- supervisor：冻结目标、路径、公开/序列化契约和验收，仲裁并最终审查；不可安全拆分的部分留主线。
- explorer：只读入口、调用链、接线和证据；不写文件、不 refresh、不运行测试。
- worker：按冻结契约实现授权生产文件；不改 tester 拥有的测试，不自行改变架构、存档或公开契约。
- tester：独占授权测试文件和验证输出；需要复现/回归时才测试先行；生产失败交回主线。
- unity_mcp：具体 CLI 不适用请求的执行代理，不拥有设计/仓库文件写权。按冻结参数机械执行单个请求，不补参数、不自行编写 execute_code。

## Single-Writer And Unity Leases

- 同一 checkout 同时仅一名写者；worker、tester 与主线串行交接写权。只读探索可并行。
- 普通磁盘工作不连接 Unity、不建快照。Editor 只读先验路由，不因读取建快照；风险操作按 [Unity_Repo_Common_Agent_Rules](Unity_Repo_Common_Agent_Rules.md) 执行。
- 主线记录当前 lease owner、项目、操作范围与安全证据；只有实际使用 Editor 风险操作时才需要 Unity lease，不为纯文档伪造 lease。
- 常规 route/status/resource/Console/refresh/精确 EditMode 由 lease owner 用 unity-editor-ops；broker 不产生第二份 lease。
- profile sandbox 可能被宿主权限覆盖，不能作为唯一保护；仍需路径边界、单写者和最终 diff。Agent 不进入 PlayMode，不运行 PlayMode tests，项目额外测试禁令同样适用。

## Supervised Execution Loop

1. 主线明确任务与已有改动；按需选择独立探索、实现或验证工作。
2. 给实际需要的角色发窄合同，串行授予/归还文件写权；已有回归足够时直接实现再验证。
3. 实现后运行最窄验证；按失败归属修复，只有实际交接才更新所有权记录。
4. 主线审查结果与边界，按 [Memory_Rules § Memory Consistency Pass](Memory_Rules.md#memory-consistency-pass) 同步长期新事实，人工验收明确列出。

## Broker Requests

仅当一个具体操作不适合 CLI 时：lease owner submit 完整原子请求 → root 用 request_id + project 唤醒 broker → broker `claim PROJECT --request-id ID` → 执行/读回 → complete 或 fail。

- 不预创建空闲 broker，不运行 daemon，不无限轮询，不自行创建子代理。
- 请求含精确 operation/arguments、mode、timeout 和 expected readback；write 还需 lease_id、snapshot、Prefab Stage 与 play_mode=false。
- CLI 只把明确只读操作允许为 read；未知操作和写操作要求 write。测试门禁检查直调与 batch，但源码/夹具是否间接生成房间仍由调用者确认。
- 同 endpoint 已有 claimed 时拒绝第二次 claim，直到 complete/fail；不会因超时自动释放或重放。异常退出由主线先确认执行者已停、实际结果和 Editor 状态，再决定 fail/后续请求。
- 队列互斥只覆盖使用同一 state root 的 broker；直接 MCP/CLI 仍依赖主线 lease，禁止与活动 broker 并行操作同一 Editor。

## Delegation Contract

必需：task_id/role、目标与非目标、基准状态、allowed/forbidden paths、保持的契约、验收与停止条件。仅涉及 Unity 时补路由、lease 和安全证据，不复制完整安全正文。
返回：status、summary、evidence(file:line)、files_touched、commands_and_tests、risks/blockers；没有的信息不填空模板。

## Role Routing Invariant

角色由 agent_type 选择，task_name 只是标签。角色专用输入默认 fork_turns="none"；仅兼容且必要时扩大上下文。
客户端/profile 改变后按需验证运行时元数据，不把子代理自报或任务名当作路由证明；既有有效证据可复用，不每次重跑烟测。
路由无法证明时不冒充指定角色；主线可完成的工作继续，确实依赖指定角色的部分报告限制。排障见 `Codex_Custom_Agent_Profile_Routing`（未公开）。

## Stop Conditions

路径/写权冲突、未知用户改动、契约歧义、Prefab Stage dirty、路由/lease 不明或意外资产差异时停止受影响部分并交回主线。不要因无关问题停止全部独立工作。
