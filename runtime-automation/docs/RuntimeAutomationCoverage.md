# Runtime Automation 覆盖矩阵

本表区分“代码/契约已接入”和“独立 Player 已验收”。前者不能替代后者。

| 范围 | 结构化接入 | 操作/视觉接入 | 当前验证 |
| --- | --- | --- | --- |
| 通用协议核心 | Provider 隔离、分页、事件、revision、错误信封 | 串行动作、超时、取消、QA 门禁 | 定向 EditMode 9/9 通过 |
| 后台输入 | 独立 Keyboard/Mouse layout 与设备诊断 | 原子移动、瞄准、按键、释放 | layout/清理契约通过；Verification Player 非前台时 `atomic.aim_screen` 已返回设备状态证据 |
| UI/教程/对话 | route、控件、文本、Rect、遮挡、溢出、教程/对话摘要 | 单页推进、交互、区域/屏幕截图 | 编译通过；Chapter 0 全流程待 Player 验收 |
| 玩家/战斗 | 玩家、敌人、生命、AI、攻击与 Run 摘要 | move_to、interact_with、attack_target | 编译通过；正式场景闭环待 Player 验收 |
| 世界/地图/建筑 | Tilemap、门、连接器、四层高度、碰撞/触发、建筑/导航摘要 | 语义查询、目标取景、QA 传送/场景/内容组合 | QA Gallery 已由白名单 mutation 加载，scene epoch 2→3；Main 全量内容矩阵仍待逐项验收 |
| 渲染/美术 | Camera、Renderer、Sprite、Animator、材质/Shader、排序、Bounds、灯光/阴影/水面 | target capture、相机/动画/粒子 QA 修改 | Camera 实体已验证跨场景旧 ID 失效、新 ID 生效；正式基线尚未登记 |
| 音频/VFX/性能 | AudioSource、粒子/VFX、异常与帧性能摘要 | 粒子控制、诊断、截图 manifest | QA Player 空 kind 查询已覆盖 Audio/Camera/UI；具体 VFX 动态证据待登记 |
| QA 安全 | 启动时模式锁定、隔离存档、精确实体/内容白名单 | 无反射/任意路径/结果命令 | Verification 不暴露 QA 工具；QA 隔离存档目录已生成；任意场景路径和未登记内容均被拒绝 |
| 构建 | Verification/QA 两 profile 与 build-only manifest | QA 场景/目录只由 QA profile 引用 | 2026-08-29 双 Player 构建通过：Verification `89658ac4…`，QA `16d925d1…` |
| 视觉比较 | 基线目录与 manifest 约定 | PNG 像素差、容差差、热图、JSON 报告 | 4×4 容差 smoke 通过；Verification Player 已产出 2560×1440 PNG、SHA-256 和结构化 manifest |

## 已知覆盖缺口登记

- Chapter 0 尚未在新协议上完成一次“零截图决策”的独立 Player 全流程，不能宣称教程验收完成。
- 背包、法术图、构筑和存档当前提供通用运行态/UI 摘要；若实际流程需要更细的业务字段，应增加专用 Provider 字段，不能靠控件文本猜测。
- `Assets/Automation/RuntimeQaContent` 当前只有接入说明，生成的 QA 内容目录为空；必须把经过审核的角色、建筑、地图片段或 VFX Prefab 放入该目录并重建 catalog 后，`qa.spawn_content` 才会列入可用内容。未登记资产继续保持拒绝。
- 静态地图、建筑、角色定帧与正式 UI 的中英文、16:9、超宽屏基线尚未登记；动态粒子/战斗默认只采结构和截图，不强制像素基线。
- Editor 通道按既定计划后置；Player 全域验收前不实现 Editor 资产修改复用。

每次新增系统、正式 UI route、建筑/地图层、角色/敌人类型、VFX 或视觉基线时，都必须更新本表。结构化字段不足时先登记缺口并补 Provider，再编写 Agent 流程。
