# Runtime 2D Room Enemy AI System Agent Spec

> 状态：Frozen（冻结规格）。仅在当前任务明确涉及本专题时作为设计约束；验收命令先服从 repo AGENTS 的现行安全与测试禁令。历史阶段步骤不构成重新执行授权；新任务的产品范围变化由用户决定。

版本：v1.0
状态：正式目标规格
范围：2D 随机 `RoomKind.Combat` 房间的小兵与精英敌人 AI。
进度日志：`Docs/Runtime2DRoomEnemyAIImplementationLog.md`

## 文档权限

本文是后续 Agent 推进 2D 随机房间敌人 AI 的正式目标文档。后续实现 agent 不得修改本文。

如果实现过程中发现本文存在规格缺陷、仓库事实冲突或无法执行的验收条件，实现 agent 必须停止继续实现，把问题写入 `Docs/Runtime2DRoomEnemyAIImplementationLog.md` 的 `Blockers` 区域，并询问用户。实现 agent 只能更新 ImplementationLog，不能修改本文或另起目标规格文档。

本文不替代：

- `Docs/EnemySpawningSystemAgentSpec.md`：2D Room Spawn Director 的预算、批次、压力、召唤和 hard limits 仍以该文为准。
- `Docs/SemanticRoomGenerationAgentSpec.md`：2D 房间语义 layout、SpawnSocket、hazard、A/B 与离线房间验收仍以该文为准。
- `Docs/EnemyContentLayerPlan.md`：旧普通敌人内容层调参文档继续保留，不作为本目标的实现主线。
- `Docs/EnemyBossAIPlan.md`：Boss AI 保持独立，不迁入本目标。

## 0. 当前仓库事实

本目标必须从当前仓库事实出发，不得假设 2D 敌人 AI 已经完整存在。

- `Runtime2DRoomEnemySpawner` 已具备 2D Spawn Director 路径，能生成 `Opening / Main / Climax` 计划并按压力释放敌人。
- `Runtime2DSpawnPlanGenerator` 已消费 `Runtime2DEnemySpawnMetadata`、`SpawnMainTag`、`SpawnPointRole`、`RoomCombatType` 和语义 SpawnSocket。
- `Runtime2DEnemy.prefab` 当前是 2D 运行时敌人壳，包含 `BaseCharEnemyNorm1`、`Runtime2DChasingEnemy`、2D collider / rigidbody 和 2D 动画驱动，但不具备完整攻击 AI。
- `CharEnemy.prefab`、`EnemyDefinitionBinder`、`EnemyAIController`、`CharEnemyMovement` 和旧攻击器主要服务旧普通敌人内容层，使用 XZ 平面、`PlayerPlaneMovement` 和旧 prefab 合同。
- 本目标不直接把 `CharEnemy.prefab` 迁入 2D 房间，也不把旧普通敌人 AI 改成双平面通用系统。
- 本目标采用 `Runtime2DEnemy` 专用 AI 壳：新增 2D 专用 brain、context、movement、AttackSpec lifecycle、CombatCoordinator、debug 和模拟链路；复用 `EnemyDefinition`、`Runtime2DEnemySpawnMetadata`、Spawn Director、SpawnSocket、语义 metadata、`ICombatDamageTarget.TryApplyDamage(...)` 与可安全复用的数据。

## 1. 目标与边界

目标是让 2D 随机 Combat 房中的小兵与精英敌人稳定、清晰、可读、可复现、可调参、可解释，并且能消费房间语义生成与 Spawn Director 的下游数据。

最终运行结构固定为：

```text
Runtime2DRoomEnemySpawner
    -> Runtime2DSpawnPlanEntry
    -> Runtime2DEnemySpawnContext
    -> Runtime2DEnemyBrain
    -> 小兵 FSM / 精英 Utility
    -> Runtime2DCombatCoordinator token
    -> Runtime2DAttackSpec / Runtime2DSpecialAbility
    -> Debug / Telemetry / Offline Bot Report
```

本目标必须实现：

- 六类小兵：`Swarm / Ranged / Chaser / Dasher / Tank / Control`。
- 四类精英：远程精英、坦克精英、控制精英、追身精英。
- 房间级 `Runtime2DCombatCoordinator`。
- 2D `AttackSpec` 生命周期：`Windup -> Active -> Recovery -> Cooldown`。
- Telegraph、LOS、方向锁定、2D tilemap 寻路、hazard 规避、SpawnSocketRole、WavePhase、RoomContext、目标系统、Debug/Telemetry、完整 bot 对战模拟和真实 Spawn Director 接入。

本目标明确不做：

- 不设计 Boss AI，不改 Boss 房专属战斗逻辑。
- 不处理 Start 房或 Reward 房敌人逻辑。
- 不迁移旧 3D / XZ `WaveManager` 波次战斗。
- 不把旧普通敌人 `EnemyAIController` 改成 2D / XZ 双平面通用系统。
- 不新增完整护甲系统，不实现破甲、护甲 UI 或护甲资源。
- 不改变整张地图路线生成。

`EliteCombat` 只表示 Spawn Director 的 `RoomCombatType.EliteCombat`。它不是新的 `RoomKind`，也不是新的地图节点类型。本文所有精英规则都只发生在 `RoomKind.Combat` 内。

## 2. 核心数据与接口

### 2.1 Runtime2DEnemySpawnContext

每个由 2D Spawn Director 生成的敌人必须在初始化时获得 `Runtime2DEnemySpawnContext`。

该 context 固定包含：

```text
EnemyDefinition
Runtime2DEnemySpawnMetadata
Runtime2DSpawnPlanEntry
Runtime2DSpawnBatchKind
SpawnPointRole
SpawnSocketId
RoomCombatType
RoomResolvedLayout
RoomSemanticLayoutMetadata
Tilemap groundTilemap
Player2DMovementController player
Runtime2DCombatCoordinator coordinator
roomSeed / spawnIndex
```

若某个敌人来自 legacy fallback 而没有完整 plan entry，必须生成 fallback context：

```text
BatchKind = Opening
SpawnPointRole = Fallback
RoomCombatType = NormalCombat
SpawnSocketId = empty
```

### 2.2 Runtime2DEnemyAIContext

AI 决策只能从 `Runtime2DEnemyAIContext` 读取运行态输入，不得在行为模块中散落查找房间、玩家、socket 或 coordinator。

该 context 固定暴露：

```text
self position / cell
target transform / damage target
distance to target
target alive
target in attack range
line of sight
can move / can act
health ratio
SpawnPointRole
Runtime2DSpawnBatchKind
RoomCombatType
current semantic area
nearby alive friend count
nearby damaged friend count
current pressure
hazard at self / target / path
available tokens
current AttackSpec phase
```

### 2.3 Target System

目标系统固定分两层：

```text
Runtime2DTargetProvider
Runtime2DTargetScorer
```

第一版必须支持：

- 默认目标：玩家。
- 扩展目标：召唤物、诱饵、敌对单位、任务目标。
- 目标失效、死亡或不可达时重新选择。

默认评分规则：

- `Swarm / Chaser / Dasher` 优先最近可攻击目标。
- `Ranged` 优先有 LOS 的可射击目标。
- `Tank` 优先能阻挡玩家推进或保护后排的位置目标。
- `Control` 优先主战斗区、玩家停留区或高价值目标附近区域。
- 精英使用 Utility 分数综合目标价值、距离、LOS、房间区域、hazard 和当前行为。

### 2.4 Room Context

2D AI 必须通过一个房间上下文读取空间信息。

该上下文固定提供：

```text
ground cells
wall / blocked cells
hazard cells
EntrySafeArea
DoorBufferArea
MainCombatArea
BacklineZone
SideZone
AmbushZone
ReservedCells
SpawnSockets
cell <-> world conversion
reachability
line of fire
path query
anchor query by role / area
```

### 2.5 伤害与投射物契约

生产运行时伤害入口固定为 `ICombatDamageTarget.TryApplyDamage(float damage, out float remainingHealth, out bool isDead)`。

固定规则：

- 玩家目标使用 `PlayerHealth : ICombatDamageTarget`。
- 敌人目标使用 `Enemy : ICombatDamageTarget`；当前 `Runtime2DEnemy` 上的 `BaseCharEnemyNorm1` 继承 `Enemy`。
- 近战、冲刺、区域控制、爆炸和直接命中类 `Runtime2DAttackSpec` 通过 `ICombatDamageTarget.TryApplyDamage(...)` 结算伤害。
- 远程 / 弹幕类 `Runtime2DAttackSpec` 在生产运行时通过 `AttackProjectileEmitter` 与 `CharBullet` 发射，`ProjectileSimulationPlane` 固定为 `XY`，命中后仍落到现有 `ICombatDamageTarget` 伤害链路。
- 本目标不新增 `IHitable`、`IDamageable` 或第二套伤害接口。
- `CharBullet` 是远程 / 弹幕表现与命中载体，不是本 AI 系统的通用伤害抽象。
- 离线 bot 模拟不直接驱动 `CharBullet`、`Rigidbody2D` 或 Unity 物理碰撞；离线模拟使用独立的 damage ledger 复刻 `TryApplyDamage` 的血量语义。

完成本目标时必须分别验证：

- 生产运行时组件测试证明 `Runtime2DAttackSpec` 能正确调用 `ICombatDamageTarget.TryApplyDamage(...)`。
- 生产运行时组件测试证明远程 AttackSpec 能用 `AttackProjectileEmitter / CharBullet / ProjectileSimulationPlane.XY` 发射。
- 离线 bot 模拟报告证明 AI 决策、token、攻击命中近似、血量和清场统计可复现。

## 3. CombatCoordinator

`Runtime2DCombatCoordinator` 是房间级压力协调器。它不决定刷什么敌人；它只决定当前哪些敌人可以释放高压行为。

Token 类型固定为：

| Token | 用途 |
| --- | --- |
| `AttackToken` | 高威胁普通攻击、近战重击、需要节奏控制的普通弹幕 |
| `BarrageToken` | 高密度弹幕、多发远程、持续扫射 |
| `DashToken` | 突进、冲撞、追身爆发 |
| `ControlToken` | 区域控制、减速区、束缚区、封路技能 |
| `EliteToken` | 精英特殊行为、精英高影响行为 |

Token 使用规则：

- 小兵和精英都必须接入 coordinator。
- 低威胁普通攻击可以不申请 token。
- 所有高威胁攻击、突进、区域控制、高密弹幕和精英特殊行为必须申请 token。
- 精英不绕过 coordinator。
- `RoomCombatType.EliteCombat` 必须至少允许一个 `EliteToken` 被合法授予。
- token 必须有持有者、过期时间、释放原因和 debug 状态。
- 行为结束、敌人死亡、房间清理和对象销毁时必须释放 token。

Debug 必须显示：

```text
当前 token 占用
等待 token 的敌人
最近 token 授予 / 拒绝原因
当前房间压力
```

## 4. 小兵 FSM

所有小兵共享基础 FSM：

```text
SpawnIntro
AcquireTarget
Move / Reposition
RequestAttack
Windup
Active
Recovery
Cooldown
Dead
```

FSM 规则：

- `SpawnIntro` 必须有可读预警；Ambush 可延迟激活，但不能无提示直接造成伤害。
- `AcquireTarget` 通过目标系统选目标，不写死玩家唯一目标。
- `Move / Reposition` 可以追击、保持距离、回到 socket 角色区域、靠近房间目标点或避开 hazard。
- `RequestAttack` 必须检查目标、LOS、冷却、AttackSpec 条件和 token。
- `Windup` 是主要预警窗口；大多数小兵可被打断，核心小兵和精英可提高抗打断。
- `Active` 只执行 AttackSpec 声明的伤害、投射物、控制区或冲刺结算。
- `Recovery` 限制连续压制；精英和高威胁行为 recovery 更明显。
- `Cooldown` 可以移动或换位，但不能重复释放同一攻击。

六类小兵固定职责：

| 类型 | 角色 | 必须具备的行为 |
| --- | --- | --- |
| `Swarm` | 低费近战填充 | 成群压迫、短前摇近战、受 token 影响较轻 |
| `Ranged` | 远程弹幕 | 找 LOS、保持距离、Backline / Side 换位、远程 AttackSpec |
| `Chaser` | 追身压力 | 持续追击、短窗口攻击、迫使玩家移动 |
| `Dasher` | 突进切入 | Windup 预警、DashToken、方向锁定、冲刺后 recovery |
| `Tank` | 阻挡承压 | 占据 Center / Frontline、保护后排、不得永久堵死唯一通路 |
| `Control` | 区域限制 | 放置短时区域控制、封锁玩家站位、第一版不做长时间硬控 |

每类小兵完成标准：

- 能由真实 Spawn Director 选出并生成。
- 能读取 SpawnSocketRole 与 WavePhase。
- 能移动、攻击、死亡并计入房间完成逻辑。
- Debug 能显示当前状态、目标、socket role、wave phase 和最近攻击阶段。

## 5. 精英 Utility AI

精英是房间压力锚点，不是 Boss。

精英必须：

- 比小兵更灵活。
- 具有可解释的行为评分。
- 与小兵形成组合压力。
- 受 `Runtime2DCombatCoordinator` 约束。
- 不拥有复杂多阶段 Boss 流程。
- 不长时间无敌转阶段。

四类精英固定为：

| 类型 | 核心体验 | 必须具备的行为池 |
| --- | --- | --- |
| 远程精英 | 高价值远程压力点 | 保持距离、高威胁远程、Backline / Side 换位、高密弹幕 |
| 坦克精英 | 中心锚点和保护者 | 占位、重击、保护后排、短距离压迫 |
| 控制精英 | 区域控制核心 | 区域控制、封路、重新选区域、低伤害高空间压力 |
| 追身精英 | 高机动追击者 | 追身、突进、短爆发、失败后撤或换位 |

Utility 评分节奏固定为：

- 固定间隔评分。
- 当前行为结束时评分。
- 目标失效、目标变化、受击、token 状态变化时允许立即评分。
- 行为有最短锁定时间。
- Windup / Active 阶段通常不切换行为。
- 分数差距不足时维持当前行为，避免抖动。

评分因子固定包含：

```text
distance
cooldown
line of sight
target position
current token state
semantic area
hazard
health ratio
simple health phase
WavePhase
SpawnSocketRole
nearby minion count
current pressure
```

精英血量阶段规则：

- 第一版只允许一个简单阈值，例如低于 50% 改变行为权重或解锁一个行为。
- 不做多阶段 Boss 化。
- 不做长时间无敌、剧情演出或房间级 phase。

精英特殊规则：

- 精英普通攻击按威胁申请普通 token。
- 精英高影响行为申请 `EliteToken`。
- 远程精英的高密弹幕申请 `BarrageToken + EliteToken`。
- 控制精英的区域控制申请 `ControlToken + EliteToken`。
- 追身精英的高威胁突进申请 `DashToken + EliteToken`。
- 坦克精英的重击申请 `AttackToken + EliteToken`。

护甲相关内容固定为未来扩展：

- 本目标不新增护甲系统。
- 本目标不新增破甲事件、护甲 UI 或护甲资源。
- `damageReductionPercent`、`DamageShieldController` 和现有破甲描述只作为未来扩展参考。
- 如果未来实施精英护甲，必须先单独更新正式规格或创建新的目标规格，并确认与现有伤害减免、护盾和控制系统不冲突。

## 6. AttackSpec、Telegraph 与公平性

2D `Runtime2DAttackSpec` 用于数据化小兵攻击，并供精英部分普通攻击复用。

字段固定包含：

```text
attackId
attackKind
windupSeconds
activeSeconds
recoverySeconds
cooldownSeconds
range
radius / width
damage
projectileCount
projectileSpeed
requiresLineOfSight
lockDirectionAtWindupRatio
canBeInterrupted
requiredTokens
telegraphKind
telegraphDuration
```

攻击生命周期固定为：

```text
Windup -> Active -> Recovery -> Cooldown
```

Telegraph 标准：

- 近战：敌人朝向、短范围提示或动画提示。
- 远程：发射方向、弹道或命中区域提示。
- 突进：起点、方向、宽度和预计路径提示。
- 区域控制：地面区域提示，Active 前必须有反应窗口。
- 精英技能：提示更清楚、持续更长、不能突然无预警结算。

方向锁定规则：

- 默认 Windup 前半段可追踪目标。
- 默认 Windup 后半段锁定方向。
- Active 全程锁定方向。
- 精英技能可以更强，但必须可读。

碰撞与公平性：

- 敌人之间允许轻量碰撞或避让，不得互相永久堵死。
- 敌人与玩家可以发生阻挡，但 `Tank` 和精英不得永久堵死唯一通道。
- SpawnIntro 阶段禁用或弱化敌人对玩家的阻挡。
- 远程攻击必须检查墙体和障碍 LOS。
- 敌人是否避开 hazard 由 metadata / 类型决定；精英默认更积极避开高威胁 hazard。

## 7. SpawnSocket、WavePhase 与房间语义

敌人出生后必须记住自己的 `SpawnPointRole` 和 socket 来源，并持续影响 AI。

固定影响：

| SpawnPointRole | 行为倾向 |
| --- | --- |
| `Frontline` | 更早接触玩家，近战、Tank、Swarm 更积极 |
| `Backline` | Ranged、Control、Support 保持距离和 LOS |
| `Side` | Dasher、Chaser、Ranged 更倾向侧翼和换位 |
| `Center` | Tank、Elite、Control 作为房间压力锚点 |
| `Ambush` | 延迟激活、SpawnIntro 预警、触发后快速切入 |
| `Fallback` | 只使用保守追击和基础攻击 |

WavePhase 固定影响：

| Batch | 行为倾向 |
| --- | --- |
| `Opening` | SpawnIntro 较清楚，攻击频率较低，引导玩家识别敌人 |
| `Main` | 标准行为权重 |
| `Climax` | 更主动，精英特殊行为可用，但仍受 token 控制 |

房间级目标点固定用于 `Ranged / Tank / Control / Elite`：

- Ranged 找 Backline / Side 可射击点。
- Tank 找 Center / Frontline 阻挡点。
- Control 找 MainCombatArea、通道边缘和玩家停留区。
- Elite 根据行为池选择攻击点、控制点、换位点或保命点。

## 8. 召唤、光环与控制抗性

召唤规则：

- 召唤物不扣 Spawn Director 房间预算。
- 召唤物必须登记到 `Runtime2DRoomEnemySpawner`，并受房间 summon hard limit 控制。
- 召唤行为必须有 cooldown、数量上限、房间总上限和 debug 记录。
- 召唤不应在 Opening 阶段频繁出现。
- 召唤物不得无限拖慢清场；房间完成逻辑按 Spawn Director 当前召唤清理规则收束。

光环规则：

- 第一版只允许一个测试光环：护盾光环。
- 光环必须有半径、持续、冷却、可见提示和上限。
- 光环不能让普通敌人永久不死。
- 玩家必须能通过击杀精英或离开范围处理。

控制抗性规则：

- 精英不能被永久控死。
- 精英控制抗性表现为控制时长缩短和连续控制递减。
- 该规则只接入已有控制 / 状态系统的可安全入口；不得新增完整护甲或破甲系统。

## 9. Debug、Telemetry 与离线模拟

Debug 必须仅在 Editor 或 Development build 中启用。

Runtime debug overlay 固定显示：

```text
enemy id
minion / elite type
current FSM state or Utility behavior
current target
SpawnSocketRole
WavePhase
AttackSpec phase
held tokens
waiting token reason
```

Utility debug 固定显示：

```text
top scored behaviors
selected behavior
score inputs
cooldown blockers
token blockers
LOS blocker
hazard blocker
lock timer
```

Telemetry 导出固定包含：

```text
room id
seed
RoomCombatType
archetype
enemy type
spawn role
wave phase
attack count
token granted / denied count
behavior selection count
damage dealt / taken
death time
room clear time
failsafe events
```

### 9.1 非 PlayMode 离线模拟执行模型

完整 bot 对战模拟固定为非 PlayMode 离线模拟，不得进入 PlayMode，不得运行 PlayMode tests。

离线模拟由两层组成：

```text
C# EditMode deterministic tick simulator
    -> 输出 JSON / CSV simulation snapshots

Python report renderer
    -> 读取 snapshots
    -> 输出 summary / metrics / failure log / charts / contact sheets
```

C# EditMode simulator 固定职责：

- 按固定 tick 推进 bot、敌人 AI、token、AttackSpec 阶段、路径近似、LOS、hazard、召唤、血量和清场。
- 使用 `RoomResolvedLayout`、`RoomSemanticLayoutMetadata` 和 Spawn Director 计划作为输入。
- 使用 cell / world position 的轻量数学近似，不依赖 Unity PlayMode、不依赖 `Rigidbody2D` 自动推进、不依赖 `Physics2D.Simulate` 作为验收主路径。
- 使用 simulation damage ledger 记录 `ICombatDamageTarget.TryApplyDamage(...)` 等价语义。
- 输出每个房间、每个 tick、每个敌人、每次攻击、每次 token 授予 / 拒绝、每次伤害、每次死亡和每次 failsafe 的结构化记录。

Python report renderer 固定职责：

- 不重新实现 AI 决策。
- 不改变模拟结果。
- 只读取 C# simulator 输出的 JSON / CSV。
- 生成 `summary.json`、`metrics.csv`、`failures.csv`、token pressure 图、elite behavior distribution 图、room timeline 图和 grid-state contact sheet。

本文中的 representative screenshots 指离线模拟网格状态图、room timeline 图或 contact sheet，不指 PlayMode 截图。

完整 bot 对战模拟必须覆盖：

- 六个房间 Archetype。
- `NormalCombat / PressureCombat / EliteCombat / ReliefCombat`。
- 六类小兵。
- 四类精英。
- SpawnSocketRole。
- WavePhase。
- hazard。
- Ambush。
- 召唤上限。
- CombatCoordinator token。
- 目标不止玩家的场景。

Bot Player 规则：

- 能移动。
- 能避开明显 hazard。
- 能攻击最近或最高价值目标。
- 能在被堵住时尝试绕行。
- 不要求模拟真实玩家最优操作；它的目标是压力统计。

离线报告固定输出：

```text
summary.json
metrics.csv
failure log
token pressure report
elite behavior distribution
grid-state images or contact sheet
```

## 10. 实施阶段

后续实现必须按以下 6 个阶段执行。每个阶段完成后必须更新 `Docs/Runtime2DRoomEnemyAIImplementationLog.md`。不得新增接入计划文档，不得修改本文。

### Phase 1：2D 运行时基线与接线

目标：

建立 2D 敌人 AI 的真实运行入口，让 Spawn Director 生成的敌人进入新的 2D AI 初始化路径。

必须完成：

- 只读确认 `Runtime2DRoomEnemySpawner`、`Runtime2DChasingEnemy`、`Runtime2DEnemy.prefab`、Spawn Director、语义 layout、Main/调试场景接线。
- 创建 `Runtime2DEnemySpawnContext`。
- 在 Spawn Director 路径和 legacy fallback 路径都为敌人提供 context。
- 为 `Runtime2DEnemy` prefab 增加 2D AI 所需组件。
- 创建或更新正式 `Runtime2DSpawnDirectorProfileData` 资产。
- 完成 Main 或调试链路接线，使真实 2D Combat 房能启动新的 AI 链路。

完成标准：

- `RoomKind.Combat` 中由 Spawn Director 生成的敌人进入新的 2D AI 初始化路径。
- 没有 Spawn Director profile 的旧 fallback 仍可生成基础敌人，不抛异常。
- `Start / Reward / Boss` 不进入本 AI 链路。
- 实现日志记录 prefab、scene、profile 接线状态。

### Phase 2：CombatCoordinator、目标系统与 2D 行为上下文

目标：

建立房间级压力协调、目标选择和 AI 决策输入真源。

必须完成：

- 实现 `Runtime2DCombatCoordinator`。
- 实现 Attack / Barrage / Dash / Control / Elite token。
- 实现 token 授予、拒绝、释放、过期、死亡清理和房间清理。
- 实现 `Runtime2DTargetProvider` 与 `Runtime2DTargetScorer`。
- 实现 `Runtime2DEnemyAIContext`。
- 将 SpawnSocketRole、WavePhase、RoomCombatType、semantic area、hazard、LOS、pressure 输入统一接入 context。

完成标准：

- 后续行为模块只通过 context 与 coordinator 读取决策输入。
- token debug 能解释授予和拒绝原因。
- 玩家仍是默认目标，召唤物、诱饵和敌对单位扩展点存在并可测试。

### Phase 3：2D 移动、寻路、LOS 与 AttackSpec

目标：

替换单纯中心追踪，提供 2D 房间专用移动、寻路、LOS 和攻击生命周期。

必须完成：

- 实现追击、保持距离、侧翼、后撤、冲刺、回到 socket 附近和 hazard 规避。
- 基于 `RoomResolvedLayout` 与 Tilemap 实现可达性、简易路径和 line of fire。
- 实现 `Runtime2DAttackSpec` 和执行器。
- 实现 Telegraph、方向锁定、可打断窗口和 cooldown。
- 至少落地近战、远程、突进、区域控制四种 AttackSpec。

完成标准：

- 墙体阻挡远程 LOS。
- 敌人能绕墙到达目标附近或目标区域。
- Tank 和精英不会永久堵死唯一通道。
- 近战、远程、突进、区域控制都能按统一生命周期执行。

### Phase 4：六类小兵完整落地

目标：

完成 `Swarm / Ranged / Chaser / Dasher / Tank / Control` 的 FSM + 行为模块。

必须完成：

- 六类小兵都使用共享 FSM。
- 六类小兵都消费 SpawnSocketRole 和 WavePhase。
- Control 小兵只做区域控制和空间限制，不做长时间硬控。
- 每类小兵都有 debug 状态和测试覆盖。

完成标准：

- 六类小兵均可由真实 Spawn Director 选出、生成、行动、攻击、死亡并计入房间完成逻辑。
- 六类小兵在测试房间中有可感知差异。
- 同屏压力受 coordinator 控制，不出现全场高压行为同时爆发。

### Phase 5：四类精英 Utility AI 完整落地

目标：

完成远程、坦克、控制、追身四类精英，形成区别于小兵但不 Boss 化的压力锚点。

必须完成：

- 实现精英 Utility AI 基础框架。
- 实现四类精英行为池。
- 接入 EliteToken、行为锁定、评分惯性和简单血量阶段。
- 接入控制抗性、召唤规则和一个测试护盾光环。
- 护甲、破甲、护甲 UI 只保留未来扩展说明，不实现。

完成标准：

- `RoomCombatType.EliteCombat` 至少稳定生成并驱动一个精英。
- 四类精英行为明显不同。
- 精英不绕过 coordinator。
- Debug 能解释每个精英当前行为选择和主要分数来源。

### Phase 6：Debug、Telemetry、离线模拟与验收

目标：

建立可解释、可统计、可复现的最终验收链路。

必须完成：

- Editor / Development-only debug overlay。
- token、FSM、Utility、AttackSpec、target、socket、phase 的 debug snapshot。
- C# EditMode deterministic tick simulator。
- Python report renderer。
- 完整非 PlayMode bot 对战模拟。
- 覆盖六种 Archetype、所有支持的 RoomCombatType、六类小兵、四类精英、hazard、ambush、召唤上限和失败保护。
- 输出 summary、metrics、failure log、token report、elite behavior distribution 和 grid-state contact sheet。

完成标准：

- 不进入 PlayMode 的自动验证能证明 2D AI 决策、AttackSpec 生命周期、token 压力、离线命中近似、血量、清场统计和配置覆盖完整。
- 真实物理碰撞、动画表现、投射物最终手感和玩家体感由用户手动 Play 验收。
- 用户手动 Play 只负责体感确认。
- 实现日志记录所有验证命令、输出路径、失败数和剩余风险。

## 11. 验收标准

功能验收：

- 六类小兵可运行。
- 四类精英可运行。
- 小兵使用 FSM + 行为模块。
- 精英使用 Utility AI。
- AttackSpec 生命周期完整。
- CombatCoordinator token 正常。
- SpawnSocketRole 持续影响行为。
- WavePhase 持续影响行为。
- 目标系统不写死玩家。
- 召唤不计预算但受 hard limit 与清理规则控制。
- `RoomCombatType.EliteCombat` 只作为 `RoomKind.Combat` 的战斗内容类型。

体验验收：

- 小兵行为可读。
- 精英行为有变化但不是 Boss。
- 同屏压力稳定。
- 远程敌人不会因墙体长期失效。
- Dasher / Chaser 有预警和反应窗口。
- Tank / Elite 不永久堵死唯一通路。
- Control 不让玩家长期失去操作。

Debug 验收：

- 可以看到小兵 FSM 状态。
- 可以看到精英 Utility 行为。
- 可以看到 token 占用。
- 可以看到目标、socket role、WavePhase。
- 可以解释精英为何选择当前行为。

模拟验收：

- 六种 Archetype 覆盖。
- 普通、压力、精英和缓冲战斗覆盖。
- AI 目标不止玩家的场景可测试。
- 能统计攻击次数、token 拒绝、精英行为分布、清场时间和失败保护。
- 离线模拟不依赖 PlayMode、`Rigidbody2D` 自动推进或 Unity 物理真实碰撞。
- Python 报告只渲染 C# simulator 输出，不重新实现 AI 决策。

## 12. 风险与固定应对

| 风险 | 固定应对 |
| --- | --- |
| Utility AI 抖动 | 最短行为锁定、评分惯性、分数差距不足维持当前行为 |
| Token 长期饱和 | token 超时、死亡释放、房间清理释放、拒绝原因 debug |
| 远程敌人被墙体影响后失效 | LOS 失败时换位到 Backline / Side / 可射击点，失败后使用保守追击 |
| 敌人阻挡玩家过强 | SpawnIntro 弱碰撞，Tank / Elite 检查唯一通路，不在 DoorBufferArea 永久驻留 |
| 召唤不计预算导致压力失控 | 房间召唤总上限、单次召唤上限、cooldown、清理 grace、debug 统计 |
| 精英 Boss 化 | 只允许简单血量阈值，不做多阶段流程、长无敌、剧情演出或 Boss 房机制 |
| 护甲系统冲突 | 本目标不实现护甲，只记录未来扩展说明 |

## 13. 验证命令

每个实现阶段至少执行与阶段相关的最小验证。最终验收必须执行：

```powershell
dotnet build Lilith.Kernel.csproj --no-restore /p:UseSharedCompilation=false
dotnet build Lilith.Tests.EditMode.csproj --no-restore /p:UseSharedCompilation=false
```

Unity 验证规则：

- 修改 Unity C#、asmdef、packages 或编译相关资产后，必须 Unity refresh/import 并等待编译 / domain reload。
- 运行 Unity tests 前必须完成 prefab safety snapshot、Prefab Stage 检查和测试前 prefab hash 清单。
- 测试结束后必须立刻对全部 prefab 做 hash 对比。
- Agent 不进入 PlayMode，不运行 PlayMode tests。
- 用户手动 Play 验收结果只写入 ImplementationLog。

文档落库验证：

```powershell
git diff --check
rg -n "EliteCombat" Docs/Runtime2DRoomEnemyAISystemAgentSpec.md
```

同时必须静态确认本文不含源文件引用残留、个人化表述或开放目标字样。`EliteCombat` 搜索用于确认所有出现位置均解释为 `RoomCombatType`，不是 `RoomKind`。
