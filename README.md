# Agent Toolkit Portfolio

我在 Unity 游戏开发中使用编程 Agent（Claude Code、Codex）时搭建的工具、规则和产出，主要服务于 2D 肉鸽构筑游戏《苍灵志》（[Lilith-Portfolio](https://github.com/DaliBerr/Lilith-Portfolio)）。

目标是让 Agent 能安全地参与日常开发：读写编辑器不搞坏资源，测试不越界，多个 Agent 不互相覆盖，游戏能被 Agent 直接驱动验证，改动有审查关卡，玩法系统按冻结的规格实现，美术特效能由 Agent 以可编辑的方式产出。

> 作者口径：规则、协议与流程由我设计，代码大量借助 Agent 编写，并由我审查和验收。

## 目录

| 目录 | 内容 | 对应问题 |
| --- | --- | --- |
| [`unity-editor-ops/`](unity-editor-ops/) | Unity MCP 命令行客户端（Python 标准库，18 个离线测试）及其 Skill | Agent 操作编辑器时的路由、门禁和多 Agent 互斥 |
| [`agent-rules/`](agent-rules/) | 仓库 AGENTS 规则、Unity 通用安全规则、多 Agent 监管流程、记忆规则 | 序列化资源保护、测试边界、单写者、长期记忆分层 |
| [`skills/`](skills/) | 仓库操作 Skill、监管 Skill | 让 Agent 按需加载项目规范和排障索引 |
| [`runtime-automation/`](runtime-automation/) | 运行时自动化协议包（Unity UPM，约 1900 行 C#）、核心测试与协议文档 | 让 Agent 不借助 Computer Use、用结构化状态驱动游戏 |
| [`editor-guards/`](editor-guards/) | Unity Editor 插件 SystemCallBlocker 与带种子的随机数生成器 | 拦截绕过统一随机数和日志模块的直接调用 |
| [`balance-lab/`](balance-lab/) | 离线数值实验的方案概述（代码未公开） | 用模拟筛选数值候选，人工批准后才写入游戏 |
| [`agent-specs/`](agent-specs/) | 敌人 AI 的执行规格与实现日志 | 让负责实现的 Agent 按冻结的目标和验收条件推进 |
| [`agent-art/`](agent-art/) | Agent 用 pixelfx 编写的特效与敌人动画源码、预览 | 让 Agent 产出可编辑、可复现的像素美术 |

## 1. 编辑器操作：unity-editor-ops

起因：多个 Unity 项目并行使用 Codex、Claude Code 开发时，出现过 Agent 按编辑器未刷新的旧缓存保存 Prefab、覆盖了新加节点的问题。这个客户端就是为了让 Agent 的编辑器操作有统一的入口和限制。

编辑器侧使用开源的 [MCP for Unity](https://github.com/CoplayDev/unity-mcp) 服务。`scripts/unity_ops.py` 是我的客户端：

- 只用 Python 标准库实现 Streamable HTTP MCP 客户端，按项目别名路由到不同端口的编辑器实例。
- **门禁**：拒绝进入 PlayMode；Lilith 测试只接受非空的精确测试类/方法名，拒绝空列表、通配、命名空间和已知的房间生成测试；直接调用和 batch 嵌套调用走同一道检查。
- **单写者**：同一个编辑器同一时间只允许一个写入者；需要专用执行者时，用文件队列（pending → claimed → completed/failed）交接一个原子请求，同一端点已有 claimed 时拒绝第二次领取，超时不自动释放或重放。
- 写请求必须声明 lease、Prefab 快照路径、Prefab Stage 状态和 `play_mode=false`，并给出预期读回。

```bash
cd unity-editor-ops
python -m unittest tests/test_unity_ops.py
python -m unittest tests/test_safety_gates.py
```

测试全部离线，不连接真实编辑器。

## 2. 仓库规则与多 Agent 流程：agent-rules / skills

- [`AGENTS.lilith-example.md`](agent-rules/AGENTS.lilith-example.md)：《苍灵志》仓库的 Agent 规则，只放项目硬边界和按任务加载的入口。
- [`Unity_Repo_Common_Agent_Rules.md`](agent-rules/Unity_Repo_Common_Agent_Rules.md)：写序列化资源、刷新导入或跑测试前，先做全量 Prefab/meta 快照；检查 Prefab Stage，脏了就停下来问人；测试前后比对 Prefab 哈希；旧快照只进回收站。
- [`Unity_Supervisor_First_Agent_Workflow.md`](agent-rules/Unity_Supervisor_First_Agent_Workflow.md)：supervisor / explorer / worker / tester 分工，同一 checkout 单写者，委派合同和停止条件。
- [`Memory_Rules.md`](agent-rules/Memory_Rules.md) 与 [`Global_AGENTS.md`](agent-rules/Global_AGENTS.md)：跨会话共享记忆的分层（规则、Skill、项目文档、知识库、归档），每类信息只保留一个主源。
- [`skills/lilith-repo-operator`](skills/lilith-repo-operator/)：按症状直达排障条目的索引，Agent 不必通读全部文档。

这些规则原本存放在一个跨会话共享的记忆库里，文档之间的引用已改成仓库内链接；记忆库其余部分含个人信息，没有公开，引用到的地方标为「未公开」。

## 3. 运行时 Agent 接口（试验性）：runtime-automation

包体内的 MCP 服务来自开源的 [Gameplay MCP](https://github.com/nowsprinting/gameplay-mcp)。`com.vocalith.runtime-automation` 是我在其上做的通用协议层，不依赖具体游戏：

- Agent 通过 `get_capabilities`、`query_state`、`query_entities`、`list_actions`、`execute_action`、`get_events`、`wait_for` 等工具读结构化状态、执行输入，用动作回执和事件日志判断结果，**不靠截图做决策**；截图只用于视觉检查，并附 SHA-256 和清单。
- 每个响应带 `state_revision`、`event_sequence` 和 `scene_epoch`，过期的动作和跨场景的旧实体 ID 会被拒绝。
- 验证包体只允许真实输入；QA 包体额外开放白名单内的条件布置，禁止反射、任意路径和直接改结果。
- 后台输入使用独立的虚拟键鼠设备，流程结束统一释放。

《苍灵志》的适配层（玩家、战斗、地图、UI 的状态和动作提供者，约 4400 行）属于游戏代码，没有收录。当前进度见 [`docs/RuntimeAutomationCoverage.md`](runtime-automation/docs/RuntimeAutomationCoverage.md)：协议核心测试 9/9 通过，两种包体构建通过；教程章节还没有完成一次全程不看截图的独立包体验收。

## 4. 代码审查插件：editor-guards

[`SystemCallBlocker.cs`](editor-guards/SystemCallBlocker.cs) 是《苍灵志》仓库里的 Unity Editor 插件，原样收录：

- **扫描**：每次编译完成后扫描业务脚本，即将进入 Play 模式前再扫描一次。
- **拦截**：直接调用 `System.Random`、`UnityEngine.Random` 或 `UnityEngine.Debug` 时，在控制台报出规则、文件、行号和替换建议；修正前不能进入 Play 模式。
- **随机数**：须改用带种子的 [`Vocalith.Random`](editor-guards/Random.cs)（PCG32），这样 Agent 引入的问题可以按种子复现。
- **日志**：须改用 `Vocalith.Logging.GameDebug`，它的调用在发行包里会被编译器去掉。

它和第 2 节的 Prefab 快照、哈希比对规则一起，构成 Agent 改动后的审查关卡。详见 [`editor-guards/README.md`](editor-guards/README.md)。

## 5. 离线数值实验：balance-lab

目标是让 Demo 达到设定体验：有同类游戏经验的玩家约在第 3–4 次尝试通关，成功一局约 15 分钟。

- **模拟**：实验室模拟两章 13 个战斗房和 2 个 Boss，在上千项可调参数中搜索，筛出少量候选数值方案。
- **写入**：候选只写入隔离目录，经人工批准和 Unity 试运行后才会写入资源。
- **审阅**：方案和实现由两个不同模型的 Agent 交叉审阅。

实验室代码在私有的游戏仓库里，这里只放方案概述：[`balance-lab/README.md`](balance-lab/README.md)。

## 6. 执行规格：agent-specs

刷怪、敌人 AI 和房间生成三个系统，都是先写执行规格，再交给负责实现的 Agent：

- **规格内容**：写明目标、仓库现状、验收条件和禁止事项。
- **不得改规格**：负责实现的 Agent 不得修改规格，只能更新实现日志。
- **遇到冲突**：发现冲突必须停下，写入日志的 Blockers，由我裁决。

这里收录敌人 AI 那份规格和它的实现日志；按规格，六类小兵已经落地。详见 [`agent-specs/README.md`](agent-specs/README.md)。

## 7. Agent 美术：agent-art

游戏中大部分特效和部分素材由 Agent 产出，路线是让 Agent 写代码或操作分层工程，而不是直接生成位图，这样结果可编辑、可复现、能用脚本导入 Unity。

- [`pixelfx-sources/`](agent-art/pixelfx-sources/)：基于 [pixelfx](https://github.com/patch-gg/pixelfx) 编写的特效源码，包括 18 种法术释放、命中、玩家与敌方弹道。
- [`pixelfx-enemies/`](agent-art/pixelfx-enemies/)：两种敌人（异形兽、墨团小鬼）的待机、移动、攻击、受击、死亡动画源码。
- Boss 和部分敌人、草与竹林植被用 [Pixel Loom](https://github.com/yangsir2077/pixel-loom) 的分层工程制作，这里只放预览。

![18 种法术释放特效](agent-art/previews/ink-releases.png)

![Boss 四方向九个动作](agent-art/previews/boss-tanxian-4dir.gif)

pixelfx 与 Sprite Fusion 的原始代码按 MIT 许可使用，见 [`agent-art/PixelFX-MIT.txt`](agent-art/PixelFX-MIT.txt)。

## 开源依赖

| 依赖 | 用途 | 许可 |
| --- | --- | --- |
| [MCP for Unity](https://github.com/CoplayDev/unity-mcp) | 编辑器侧 MCP 服务 | 见原项目 |
| [Gameplay MCP](https://github.com/nowsprinting/gameplay-mcp) | 包体内嵌 MCP 服务 | 见原项目 |
| [pixelfx](https://github.com/patch-gg/pixelfx) | 像素特效库 | MIT |
| [Pixel Loom](https://github.com/yangsir2077/pixel-loom) | 分层像素工程编辑器（经 MCP 由 Agent 操作） | 见原项目 |

## 许可

本仓库以 [MIT](LICENSE) 许可发布。`agent-art/` 中基于 pixelfx 与 Sprite Fusion 改写的文件同时保留原作者的 MIT 版权声明（见 [`agent-art/PixelFX-MIT.txt`](agent-art/PixelFX-MIT.txt) 及各目录内的 LICENSE-PixelFX.txt）。
