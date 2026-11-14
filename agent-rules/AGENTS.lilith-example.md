# AGENTS.md - Lilith Repo Rules

始终使用中文，进度说明简单直接。

## 轻量启动链

先读本文件 → `<shared-memory>/AGENTS.md`。快捷键、字段、文件位置等窄范围只读查询随后直接查相关源码，不必读项目快照或完整排障索引。
功能实现按需读短 README，再直达命中的专题/源码，不展开全部链接；涉及项目进度或跨任务衔接时读 `<shared-memory>/Projects\Lilith\Lilith.md`。
同一任务已读且未变化的正文不重读；压缩时保留已读来源/章节摘要，必要时先核对修改时间。共享库不可访问时说明限制，按本文件与 lilith-repo-operator 继续。
共享库默认排除历史目录；仅追溯时定向读取。Codex 内置记忆辅助回忆，不能覆盖本文件、当前专题和源码，也不构成历史授权的延续。

## 按任务加载

- 架构和入口：`README.md`；C#、UI 绑定或迁移：共享 `Knowledge\References\Unity_Repo_Common_Agent_Rules.md` 的 Coding Contract。
- Editor truth：unity-editor-ops 与共享 `Knowledge\Tools\Unity_MCP_Multi_Project.md`。
- 序列化写入、refresh/import、Unity tests 或 Editor 状态变更：共享 Unity Common 全文。
- 已授权的独立 Player 验证：`Docs/RuntimeAutomationAgentWorkflow.md` 与 `Docs/GameplayMcpValidation.md`。
- 监管角色与 lease：共享 `Knowledge\References\Unity_Supervisor_First_Agent_Workflow.md`。
- 长期记忆写入或显式维护：共享 `Memory_Rules.md`；排障先搜索 lilith-repo-operator 症状索引，只读命中条目。搜索先缩小模块、列路径和少量命中，再展开正文。

权威顺序：当前会话指令 → 本文件项目硬边界 → 共享主题主源 → skill 步骤 → README / Project / Preferences。历史规格不覆盖现行禁令。

## Unity 安全

- 普通代码、Markdown、Git、静态分析和纯磁盘读取不连接 Unity。Editor 只读查询需要确认路由，但不建立 Prefab 快照。
- 任何序列化写入、refresh/import、Unity tests 或 Editor 状态变更前，按 Unity Common 建立全量 Prefab/meta 快照并执行 Prefab Stage/hash 安全检查。
- 快照清理只保留最近 5 份；必须验证目标在 `Backup/PrefabSafetySnapshots` 内并通过 Windows 回收站移除，禁止永久删除。
- Editor 操作先核实 Lilith 路由、实例与场景；通常由 lease owner 直接用 CLI，具体请求确实不适用时才启用 broker。
- Agent 禁止进入 PlayMode 或运行 PlayMode 测试。
- 禁止任何房间生成相关测试，包括 `RoomGenerationTests` 和间接触发房间生成的夹具；禁止完整 EditMode 与宽泛筛选。只运行已核查夹具的精确测试清单。

## 仓库硬边界

- 优先代码、Editor、asmdef、Packages 与相关 ProjectSettings；Scene/Prefab/ScriptableObject 只处理任务相关部分。
- `Assets/Scripts/Vocalith/**` 不新增 `Kernel.*` 引用；游戏语义放 Kernel adapter/bridge，通用抽象可下沉。
- 生产存档走 `Kernel/Save` 与 `Vocalith/Persistence`，不新增 `Vocalith.Scribe`、`Scribe_*`、`ISaveItem`、`IExposable` 生产引用。
- 触碰 CharBullet、BackPackUIScreen、RuntimeSaveService 等集中类时，优先窄协作者，保持 public API、Prefab 契约与测试语义。
- 运行时 `public static Instance` 同步维护 SubsystemRegistration reset 和 OnDestroy 清理；不新增公开可变 static collection。
- 生产本地化走 Unity Localization String Table；静态 TMP 用 LocalizeStringEvent，动态文本用显式 key 与 UnityLocalization.GetString(...)。新文字默认 TMP，除兼容修复不新增 UnityEngine.UI.Text。
- 引入运行时自动状态变化，最终说明对象、字段、触发时机和影响。

## 文档收尾

README/专题保存当前实现，Project note 保存导航、证据日期和待验收风险，memory.md 只做离线索引。数据破坏风险、可复用根因进入共享 Knowledge；正式入口变化更新 README/专题及必要指针，不复制多份状态。只同步本次变化；普通查询不写 Session/Handoff，审查不自动整改任务外文档。
