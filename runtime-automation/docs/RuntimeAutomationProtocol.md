# Lilith Runtime Automation Protocol 1.0

协议由 embedded UPM 包 `Packages/com.vocalith.runtime-automation` 实现，Lilith 业务适配位于 `Assets/Scripts/Automation`。包版本为 `0.1.0`，响应 schema 为版本化 `1.0`。

## 响应信封

每个响应至少包含：

- `ok`、`schema_version`、`session`。
- `build.id / mode / git_revision / dirty`。
- `runtime_mode`、`scene_epoch`、`frame`。
- `state_revision`、`event_sequence`。
- `payload` 与局部 `errors`。

Provider 失败不会丢弃其他 Provider 的健康结果；这类响应使用 `payload.partial=true` 并在局部错误中标明 Provider。场景切换递增 `scene_epoch`，旧动态实体 ID 因 epoch 不匹配而失效。大集合按稳定 ID 排序，使用 opaque cursor、字段掩码、页大小与硬上限。

## MCP 主接口

| 工具 | 责任 |
| --- | --- |
| `get_capabilities` | 协议、模式、Provider、动作、视觉与门禁 |
| `query_state` | 按领域和字段掩码查询版本化状态 |
| `query_entities` | 按 ID、kind、tag、距离、Provider、字段和分页查询实体 |
| `list_actions` | 动作参数、前置条件、预期效果和证据等级 |
| `execute_action` | 串行执行原子/语义输入并返回真实回执 |
| `get_events` | 读取 session 内有序事件日志 |
| `wait_for` | 等待结构化 JSONPath 条件，不看截图决策 |
| `capture_visual` | 按屏幕或目标 Bounds 生成 PNG 与结构化 manifest |
| `get_diagnostics` | 输入、日志、服务、缺失引用和性能诊断 |
| `qa_list_mutations` | QA 模式列出白名单底层修改 |
| `qa_execute_mutation` | QA 模式执行白名单修改 |

## 动作回执

动作回执分别记录：

- `accepted`：协议、模式、revision 和参数门禁通过。
- `dispatched`：Provider 已实际发送输入或调用允许的公开 QA API。
- `observed_effect`：结构化状态中观察到预期变化。
- `blocked_reason`：没有观察到效果或前置条件不满足的明确原因。
- `before_revision / after_revision`、前后事件 sequence、产生事件和 Provider 证据。
- `evidence_level`：`atomic_input`、`semantic_input` 或 `direct_qa`。

因此 `dispatched=true, observed_effect=false` 只能报告“已发出但未观察到效果”，不能报告玩法结果已经发生。动作队列有界串行，支持超时、外部取消和 optimistic revision；无论超时、取消还是异常都释放串行 gate。

## Lilith 状态领域

- `system`：场景、语言、分辨率、相机、时间、输入焦点与设备、日志、帧性能。
- `ui`：导航栈、语义控件、对话/教程、文本、Rect、遮挡、溢出和可达性摘要。
- `gameplay`：玩家、敌人、生命、攻击/战斗、Run、拾取、库存/构筑/存档可用摘要。
- `world`：房间/布局摘要、Tilemap 层、门/连接器、四层高度、碰撞体、触发器、建筑与导航摘要。
- `rendering`：Camera、Pixel Perfect、Renderer/Sprite、Animator、材质/Shader、SortingGroup、Bounds、可见性、粒子、灯光、阴影和水面摘要。
- `feedback`：AudioSource/Mixer、粒子/VFX 生命周期、运行时异常和性能反馈。

若下一步无法从结构化状态表达，必须登记 Provider 覆盖缺口；不得退回截图推断玩法或导航状态。

## 输入与生命周期

运行时输入租约为 Keyboard/Mouse 注册独立 layout，强制 layout 的 `canRunInBackground=true`；创建自动化设备前记录当前设备，服务退出时释放全部控制、删除设备、移除 layout 并恢复之前的 current device。`Application.runInBackground` 只在服务生命周期临时启用，退出时恢复。

Editor 环境会根据 Game View 设置覆盖 `InputDevice.canRunInBackground` 的公开读值，因此 EditMode 契约测试验证 layout override；最终后台生效必须在独立 Development Player 验证。

## 视觉产物

`capture_visual` 只输出 PNG。默认 session 目录为 `Temp/RuntimeAutomation/<session>`，每张图旁写 JSON manifest，包含分辨率、语言、相机、frame、目标 Bounds、渲染状态和内容 SHA-256。基线及登记清单位于 `Tests/VisualBaselines/Gameplay`，外部比较器为 `Tools/RuntimeAutomation/visual_compare.py`。
