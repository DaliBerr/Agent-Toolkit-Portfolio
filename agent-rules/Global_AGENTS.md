# Global AGENTS

始终使用中文，进度说明简单直接。

## Lightweight Bootstrap

进入登记仓库先读 repo-local AGENTS → 本文件。窄范围只读查询随后直接查相关源码；功能实现按需读 README/专题，仅涉及项目进度、跨任务衔接时读对应 Project note。
同一任务已读且未变化的正文不重读；上下文压缩保留已读来源/章节摘要，必要时先检查修改时间。搜索先定位路径和少量命中，再展开正文。
Index 是按需目录；默认聚焦 Lilith，其他项目仅在任务涉及时读取。
默认搜索遵守 `.ignore`，不读 Archive；旧会话、交接与退役决策统一归档；只有用户要求追溯或当前证据不足时，才用明确路径定向读取历史，不以 `rg -uuu` 全库绕过。
Codex 内置记忆辅助回忆偏好和历史线索；必须执行的规则、当前实现和授权仍以当前指令及项目主源为准。日常聊天和 Git 路径计数不在此重复记账。

## Authority

- 系统、developer 和用户当前指令优先；已有授权不反复确认。
- 项目硬边界以 repo-local AGENTS 为准；详细政策以主题主源为准；Skill 保存步骤；必须执行的偏好并入本文件或项目规则，决策理由进入对应 Knowledge，历史原文只在 Archive。
- 同一协议只保留一份正文；历史记录不构成当前操作授权。

## Common Behavior

- 先查本地事实。已授权范围内可逆的实现细节自行判断；无法从事实消除、且会显著改变目标、范围、外部影响或不可逆结果的不确定，集中问用户。
- 等待回答时只暂停依赖答案的部分，继续独立工作；根据当前客户端选择可用的提问方式。
- 只改任务需要的内容，保留用户已有改动。用最窄的静态检查、编译或允许的测试验证；错误明确报告，不静默吞掉。
- 默认主线；只有用户显式调用监管技能，或符合当前会话授权且复杂任务有明确委派收益时，按 Supervisor 主源选择实际需要的角色。边界不清继续主线。

- 外部工具写入超时后先读回确认，不立即重试；共享文档小步串行修改。
- 审查先列可操作的 bug、风险和缺失验证，附文件/行号并按严重程度排列；不把无关重构混入必修项。没有发现时明确说明验证边界。
- 匹配现有代码风格，优先最窄有意义的复现/回归；视觉体感不强制测试先行。最终只报告实际变化、验证与未解决事项，不列固定记忆收尾清单。

## Topic Triggers

- C# 实现、绑定、UI 或迁移：[[Knowledge/References/Unity_Repo_Common_Agent_Rules#Coding Contract]]；不因读取编码规则连接 Unity。
- Editor 读取：[[Knowledge/Tools/Unity_MCP_Multi_Project]] 与 unity-editor-ops；需要路由，不因只读建立快照。
- 序列化写入、refresh/import、Unity 测试或 Editor 状态变更：[[Knowledge/References/Unity_Repo_Common_Agent_Rules]]。
- 监管角色、写权、lease：[[Knowledge/References/Unity_Supervisor_First_Agent_Workflow]]。
- 有长期新信息需要写入、归档或显式记忆维护：[[Memory_Rules]]。普通只读问答、审查不自动重写被读取的记忆。
- 其他主题按需查 [[Index]]；Agent 默认不进入 Editor PlayMode 或运行 PlayMode tests。

## Closeout

只同步本次变化影响的事实；数据破坏风险、可复用根因或正式入口变化写对应主源并补必要索引。普通查询不写记忆；需要写入时按 [[Memory_Rules#Memory Consistency Pass]] 检查本次修改范围。
