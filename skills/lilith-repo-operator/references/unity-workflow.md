# Lilith Unity Workflow

- 先从 README 与实际调用链定位。优先 C#、Editor、asmdef、Packages；资源只处理任务相关部分。
- C# 绑定/迁移遵守共享 Unity Common 的 Coding Contract。Editor 读取先用 unity-editor-ops 核实 Lilith；写入/刷新/测试执行共享安全流程，不复制协议。
- 集中类优先窄协作者，一次移动一项职责，保留 public API、序列化字段和 prefab contract。
- 游戏存档语义留 Kernel；通用路径、JSON、原子写入和损坏隔离留 Vocalith.Persistence。
- 本地化资源位于 Assets/Data/Localization/Official；不新增 legacy JSON StringTables/JsonPatches 或旧 helper 生产引用。
- 运行时单例维护静态 reset 与 OnDestroy，优先实例事件。完整硬边界查 repo AGENTS。
- 搜索先 rg 并缩小目录，先列路径和少量命中再展开正文；不要为窄查询扫描全部 Assets/Packages/Docs 并输出全文。Windows 小范围顺序补丁；编译/Editor 证据按实际需要获取，已有验证足够时不扩测。
- 无长期新事实不写记忆；已知排障直达 references/troubleshooting-index.md。
