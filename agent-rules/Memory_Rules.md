# Memory Rules

只在写入长期知识或明确维护记忆时读取。共享库保存工程规则与关键经验，Codex 内置记忆负责日常回忆；不手工维护其生成文件。

## Ownership

- AGENTS：必须执行的规则和按任务入口；Skills：可复用步骤；项目 README/Docs：当前实现、验证证据与验收细项。
- Knowledge：有证据的安全风险、可复用根因及防止复发的原则；每条只保留症状、原因、处理边界和主源链接。过时实现参数、试验流水进入 Archive。
- Project note：项目导航、证据日期和跨任务风险；TODO：唯一待办/验收清单，按主题读取。每项只留一处完成状态，不再拆出 Acceptance 或全局重复待办。
- Codex 内置记忆：一般偏好、最近工作和历史线索；自动回忆不能覆盖明确规则、源码、证据或充当操作授权。
- 交接/补充记录：仅确有跨任务需求且现有主源无法承载时写入 Archive 的明确目录。普通任务不生成日记，不重建 Dashboard、Preferences、Sessions、Decisions 等退役顶层目录。
- Archive：完整历史和压缩前原文；保留可追溯性，默认搜索与读取排除。旧链接只在追溯时使用。

## Hot-Path Budgets

- Project：建议 4–6 KiB，硬上限 60 行 / 12 KiB；只保留 Status / Current State / Next Steps / Risks / Links，Status 最多 6 行，Next Steps 最多 8 条。
- TODO：100 行 / 16 KiB；Active 每项含续行最多 400 字符；Done 最多 10 项，无需为了凑数保留旧流水。
- README：简短架构与专题导航，Lilith 建议不超过 10 KiB。排障按专题分文件，只读命中条目，不把完整索引列为启动必读。
- Index：只做导航和少量跨项目未决事项；不建立平行看板。历史文件与元数据归档不意味着要读取历史正文。
- 压缩或迁移前完整备份并核对原文；不因局部任务扩展成全库治理。

## Memory Consistency Pass

1. 先核对目标现状和证据，保留已有改动；无长期新信息不写。自动维护跳过他人已改文件，交互整理保留原文并检查并发变化。
2. 只更新事实所属的一个主源及必要指针；历史不覆盖现行规则。项目已有 Latest handoff 时指向最近正式归档；没有交接的项目无需制造历史。
3. 修正风险或根因时同步症状入口；已修复的事故保留预防原则，不继续写成未修复现状。未验证结果明确保留边界。
4. 写后读回、核对 diff 和直接链接；规则/模板维护运行结构检查与相关离线回归。交互任务不自动提交。

## Scheduled Maintenance

每日/月度操作、时间回执、自动化会话归档与定向提交统一见 [[Knowledge/References/Memory_Maintenance_Operations]]；调度提示词见 [[Knowledge/References/Memory_Vault_Daily_Monthly_Agent_Prompts]]。开发任务不加载这些流程。
