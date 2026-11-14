---
name: lilith-repo-operator
description: Lilith 仓库操作入口，用于 Lilith Unity 仓库的代码、文档、Editor truth、排障、持久化和本地化工作。
---
# Lilith Repo Operator

先执行 repo AGENTS 的按需启动链，已读且未变化的内容不重复；压缩后复用来源/章节摘要。本技能保存步骤，硬边界以 repo AGENTS、详细政策以共享主题主源为准。

- 快捷键、字段、文件位置等只读查询：直接从相关模块源码收窄，不强制读项目快照或症状索引。
- 实现/架构与入口：README.md 和相关专题；代码操作细节：references/unity-workflow.md。项目进度/交接才读共享 Project note。
- 新事实放哪里：references/document-boundaries.md；数据破坏风险、可复用根因或正式入口变化补主源/必要索引，普通查询不写记录。
- 症状排障：先搜索 references/troubleshooting-index.md，直达命中的共享 Knowledge 专题与标题，不通读聚合索引；未命中则查真实调用链。
- C# 实现或 UI 绑定先读共享 Unity Common 的 Coding Contract；只有 Editor 风险操作才加载完整安全流程。
- Editor truth 用 unity-editor-ops；已授权的独立 Player 验证用 repo Docs/RuntimeAutomationAgentWorkflow.md。
- 需要监管时查共享 Supervisor；出现具体 CLI 不适用操作才落盘 broker 请求。

日常偏好和聊天回顾由 Codex 内置记忆辅助；明确规则和当前源码优先，历史目录默认不读。

从真实调用链收窄目标，保持最小改动并做最窄验证。禁止 PlayMode 和房间生成测试等仓库硬规则始终适用。
