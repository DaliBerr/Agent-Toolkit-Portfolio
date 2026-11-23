# Runtime 2D Room Enemy AI Implementation Log

本文只记录 `Docs/Runtime2DRoomEnemyAISystemAgentSpec.md` 的实现推进、验证结果和 Blocker。本文不是正式目标规格，不能覆盖或修改正式目标文档的要求。

## Status

- Current phase: Runtime vertical slice, offline bot matrix, production attack evidence, default profile asset, Main scene Spawn Director wiring, AI observability closure, Tank stall fix, and reviewer follow-up fixes implemented; manual Play tuning remains
- Formal spec: `Docs/Runtime2DRoomEnemyAISystemAgentSpec.md`
- Last updated: 2026-07-07 reviewer follow-up fixes for Dash sweep, Elite timer, Ranged kite, and 2D AI registry

## Rules

- 后续实现 agent 可以更新本文记录进度。
- 后续实现 agent 不得修改 `Docs/Runtime2DRoomEnemyAISystemAgentSpec.md`。
- 如果正式目标文档存在无法执行、与仓库事实冲突或需要用户决策的问题，停止实现并在本文 `Blockers` 中记录。
- 每个实现阶段完成后，记录触达文件、验证命令、结果和剩余风险。

## Baseline

- 2026-07-06：创建正式目标文档与实现日志。本文仅作为后续进度记录入口。
- 2026-07-06：根据审阅意见修订正式目标文档，明确 bot 对战模拟是非 PlayMode 离线模型，并锁定生产运行时伤害入口为 `ICombatDamageTarget.TryApplyDamage(...)`；远程 / 弹幕运行时使用 `AttackProjectileEmitter / CharBullet / ProjectileSimulationPlane.XY`。
- 本次落库只新增文档并更新 README 协作文档入口。
- 本次落库不触发 Unity refresh/import，不运行 Unity tests，不进入 PlayMode。
- 本次落库前已创建 prefab safety snapshot：`Backup/PrefabSafetySnapshots/20260706_171704`。
- 本次审阅修订前已创建 prefab safety snapshot：`Backup/PrefabSafetySnapshots/20260706_181858`。

### Current Repo Facts

- `Runtime2DRoomEnemySpawner` 已有 Spawn Director 路径，配置 `spawnDirectorProfile` 时会生成 `Runtime2DSpawnPlan` 并释放 Opening / Main / Climax 敌人。
- `Runtime2DEnemy.prefab` 当前包含 `BaseCharEnemyNorm1`、`Runtime2DChasingEnemy`、`Character2DAnimatorDriver`、`CapsuleCollider2D`、`Rigidbody2D` 和 `Runtime2DEnemyBrain`。
- `CharEnemy.prefab` 当前包含 `EnemyDefinitionBinder`、`EnemyAIController`、旧攻击器和 XZ `CharEnemyMovement`，不作为本目标的 2D 运行时壳。
- `EnemyDefinition` 已有 `Runtime2DSpawnMetadata` 与 `AIProfile` 字段；本目标只复用可安全复用的数据，不把旧普通敌人 AI 改成双平面通用系统。

## Phase Progress

| Phase | Status | Notes |
| --- | --- | --- |
| Phase 1 - 2D 运行时基线与接线 | Implemented vertical slice | 新增 `Runtime2DEnemySpawnContext`；`Runtime2DRoomEnemySpawner` 在 legacy 和 Spawn Director 路径初始化 `Runtime2DEnemyBrain`；`Runtime2DEnemy.prefab` 已接入 Brain；Boss / Start / Reward 不进入本 AI 路径。 |
| Phase 2 - CombatCoordinator、目标系统与 2D 行为上下文 | Implemented vertical slice | 新增 `Runtime2DCombatCoordinator` token 管理、`Runtime2DTargetProvider`、`Runtime2DRoomAIContext`、`Runtime2DEnemyAIContext`；目标优先玩家，保留非玩家目标扩展点。 |
| Phase 3 - 2D 移动、寻路、LOS 与 AttackSpec | Implemented first pass with production evidence | 新增 tile/grid 辅助、hazard 检查、简易 BFS next-cell、LOS 和 `Runtime2DAttackSpecExecutor` 生命周期；远程使用 `AttackProjectileEmitter` / `CharBullet` XY，缺弹体配置时走直接伤害兜底；新增组件测试证明直接伤害走 `ICombatDamageTarget.TryApplyDamage(...)`，远程路径能生成 `ProjectileSimulationPlane.XY` 子弹。 |
| Phase 4 - 六类小兵完整落地 | Implemented first pass | `Swarm / Ranged / Chaser / Dasher / Tank / Control` 已通过 `Runtime2DEnemySpawnMetadata.mainTag` 映射到 2D FSM、移动目标和 AttackSpec；仍需更大样本调参与体感验收。 |
| Phase 5 - 四类精英 Utility AI 完整落地 | Implemented first pass | `EliteRanged / EliteTank / EliteControl / EliteChaser` 已接入 `RoomCombatType.EliteCombat`、Elite token 和 debug reason；本轮不实现护甲系统、破甲、光环和召唤精英能力。 |
| Phase 6 - Debug、Telemetry、离线模拟与验收 | Implemented matrix coverage plus report charts | 新增 `CreateDebugSnapshot()`、C# deterministic tick simulation、event stream 和 Python report renderer；当前 matrix 覆盖 6 Archetype、4 RoomCombatType、6 小兵 main tag、4 精英 kind、5 SpawnRole、3 WavePhase、hazard、ambush、summon limit、non-player target、token grant/deny、summary、metrics、failure CSV、token pressure、elite behavior distribution、room timeline 与 contact sheet。 |

## 2026-07-06 Runtime Implementation Pass

### Touched Files

- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DEnemyAI.cs`
- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DEnemyAISimulation.cs`
- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DCombatCoordinator.cs`
- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DEnemyBrain.cs`
- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DSpawnDirectorProfileData.cs`
- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DRoomEnemySpawner.cs`
- `Assets/Data/Rooms/SpawnDirector/Runtime2DSpawnDirectorProfile_Default.asset`
- `Assets/Prefabs/Enemy/Runtime2DEnemy.prefab`
- `Assets/Prefabs/Player/Test2DCharacterSprite.prefab`
- `Assets/Scenes/Main.unity`
- `Assets/Editor/Test/ProceduralRoomMapTests.cs`
- `Tools/Runtime2DEnemyAISimulation/render_runtime2d_enemy_ai_report.py`
- `.gitignore`
- `Lilith.Kernel.csproj`

### Implementation Notes

- `Runtime2DEnemyBrain` disables legacy `Runtime2DChasingEnemy` only after successful AI initialization, so old fallback paths remain usable.
- `Runtime2DRoomEnemySpawner` may add `Runtime2DCombatCoordinator` to its GameObject and `Runtime2DEnemyBrain` to spawned Combat enemies if absent. This automatic state change is limited to `RoomKind.Combat` spawning.
- `Runtime2DEnemyBrain` and `Runtime2DCombatCoordinator` use same-name partial files so Unity can serialize the prefab / scene component references reliably.
- `Runtime2DSpawnDirectorProfileData` now has a same-name partial script file so Unity can serialize a real `ScriptableObject` asset reference reliably.
- `Runtime2DEnemy.prefab` now has `Runtime2DEnemyBrain` on the root. The initial MCP add left a missing-script block because the Brain class was still in a mismatched file; this was fixed by same-name partial files and the prefab now imports without Console errors.
- Added `Assets/Data/Rooms/SpawnDirector/Runtime2DSpawnDirectorProfile_Default.asset` with the six minion main-tag roles represented by existing enemy definitions.
- `Assets/Scenes/Main.unity` now wires `GeneratedRoomMapRoot`'s `Runtime2DRoomEnemySpawner.spawnDirectorProfile` to `Runtime2DSpawnDirectorProfile_Default.asset`, so manual Runtime 2D Combat entry uses the Spawn Director path instead of the old wave fallback when the scene is played by the user.
- Added explicit `Runtime2DTargetScorer` score breakdown, AI context fields for token availability / pressure / nearby alive friends / target score / attack phase, and Brain debug output for behavior count and telegraph state.
- `Runtime2DCombatCoordinator` now records token wait snapshots, including waiting owner, requested tokens, request reason, and reject reason.
- `Runtime2DAttackSpecExecutor` now exposes telegraph state, phase remaining seconds, locked direction, and interrupt support for interruptible windup attacks.
- Offline bot reports now include `RoomId` and behavior selection counts at report and enemy-summary levels; events also carry `RoomId`.
- Smoke offline output was generated locally at `Reports/Runtime2DEnemyAISimulation/smoke/`. Matrix offline output was generated locally at `Reports/Runtime2DEnemyAISimulation/matrix/`. `Reports/` and `Temp/` remain ignored; the reusable renderer under `Tools/Runtime2DEnemyAISimulation/` is now unignored for source control.
- The deterministic simulator now records `Events` for token grants / denies, summon grants / denies, clear, and failure, plus report-level hazard / ambush / summon / non-player target coverage fields.
- `Runtime2DAttackSpecExecutor` now falls back from `EnemyData.Definition` to `Runtime2DEnemySpawnContext.EnemyDefinition` when resolving ranged projectile configuration, matching the Runtime 2D Spawn Director initialization path.
- The Python renderer now emits `token_pressure.csv`, `elite_behavior_distribution.csv`, `room_timeline.csv`, `charts/token_pressure.png`, `charts/elite_behavior_distribution.png`, `charts/room_timeline_overview.png`, and per-report `room_timelines/*.png`.

### Remaining Risks

- Utility scoring for elites is a first-pass behavior selector with debug reason, not a fully tuned combat brain.
- The offline simulation is deterministic and non-PlayMode by design and now has a 24-report matrix. It still uses lightweight mathematical approximations, so final projectile feel, collision feel, animation timing, and manual combat readability remain user Play-smoke work.
- Real manual Play体感、正式关卡 profile 调参、召唤 / 光环 / 护甲后续扩展尚未执行。

## 2026-07-07 Tank Center Leash / No-Progress Fix

### Touched Files

- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DEnemyAI.cs`
- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DRoomEnemySpawner.cs`
- `Assets/Editor/Test/ProceduralRoomMapTests.cs`

### Implementation Notes

- Manual Play paused inspection showed `Runtime2D Enemy 甲` had non-zero velocity and no 2D contacts, but sat at `distToCenter=1.507` against the old Tank `> 1.5` center switch.
- Tank movement is now role-aware: only `SpawnPointRole.Center` Tank / EliteTank uses Center leash; Frontline / Side / Ambush / Fallback Tank targets the player instead of forcing a Center return.
- Center Tank leash now uses hysteresis: enter return-to-center at distance `> 2.0`, exit at `<= 0.8`, preventing Center / player destination flipping near the old single threshold.
- Spawn Director no-progress detection now treats active budget enemy position changes, Brain state changes, AttackSpec phase changes, and behavior-selection count changes as combat activity in addition to pressure changes.
- Teleport failsafe now clears the activity snapshot and resets the no-progress timer after teleport, giving enemies a fresh observation window before forced cleanup.

### Verification

- Prefab safety snapshot created: `Backup/PrefabSafetySnapshots/20260707_014705`.
- `dotnet build Lilith.Kernel.csproj --no-restore /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- `dotnet build Lilith.Tests.EditMode.csproj --no-restore /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- `git diff --check -- Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DEnemyAI.cs Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DRoomEnemySpawner.cs Assets/Editor/Test/ProceduralRoomMapTests.cs` passed; Git reported LF/CRLF normalization warnings only.
- Added compile-verified EditMode tests:
  - `Runtime2DEnemyBrain_FrontlineTank_ChasesPlayerInsteadOfCenterLeash`
  - `Runtime2DEnemyBrain_CenterTank_UsesLeashHysteresis`
  - `Runtime2DRoomEnemySpawner_NoEffectiveCombatTimeout_IgnoresMovingBudgetEnemies`
- Unity PlayMode was not stopped by the agent. Unity EditMode tests were not run because the user had the Editor paused in PlayMode during the fix.

## 2026-07-07 Reviewer Follow-Up Fixes

### Touched Files

- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DEnemyAI.cs`
- `Assets/Scripts/Kernel/MapGrid/Rooms/Runtime2DRoomEnemySpawner.cs`
- `Assets/Editor/Test/ProceduralRoomMapTests.cs`

### Implementation Notes

- Dash / EliteChaser damage no longer uses the Active first-frame `context.DistanceToTarget <= width` snapshot. Dash Active now sweeps the locked dash path over the configured range and applies damage once through `ICombatDamageTarget.TryApplyDamage(...)`.
- Elite decision timing no longer mutates inside `ResolveEliteAttackSpec()`. `TickAI(deltaTime)` resolves the preferred spec once, refreshes context once, and decrements `eliteDecisionTimer` once per AI frame.
- Ranged / EliteRanged close-range kite destinations now resolve through `Runtime2DRoomAIContext.TryFindKiteDestination(...)`, which samples legal ground / non-hazard / reachable cells before falling back to Backline, MainCombatArea, then current position.
- `Runtime2DCombatCoordinator` now owns an instance-level 2D AI registry for active `Runtime2DEnemyBrain` objects and selectable `Enemy` targets. `Runtime2DEnemyAIContext` and `Runtime2DTargetProvider` use this registry before falling back to `FindObjectsByType`.
- `Runtime2DRoomEnemySpawner.RegisterSummonedEnemy(...)` registers summoned / ally `Enemy` targets with the coordinator so non-player target selection keeps the same faction semantics without per-frame global scans.

### Verification

- Prefab safety snapshot created: `Backup/PrefabSafetySnapshots/20260707_230937`.
- Initial parallel `dotnet build ... --no-restore` attempts were blocked by missing `Temp/obj/*/project.assets.json`; `dotnet restore` was run to regenerate local build cache.
- A parallel build attempt then hit transient `Temp/obj` file locks from concurrent builds / Huorong. Re-running builds sequentially with `/m:1` avoided the lock.
- `dotnet build Lilith.Kernel.csproj --no-restore /m:1 /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- `dotnet build Lilith.Tests.EditMode.csproj --no-restore /m:1 /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- Added compile-verified EditMode tests:
  - `Runtime2DEnemyBrain_DasherSweepDamagesOnceDuringActive`
  - `Runtime2DEnemyBrain_EliteChaserSweepDamagesDuringDash`
  - `Runtime2DEnemyBrain_EliteDecisionTimerTicksOncePerAIFrame`
  - `Runtime2DEnemyBrain_RangedKiteDestinationStaysOnLegalGround`
  - `Runtime2DEnemyAIContext_UsesCoordinatorRegistryForNearbyFriends`
  - `Runtime2DTargetProvider_UsesRegisteredEnemyTargetsForAllyActor`
- Follow-up Unity verification was run after confirming the Editor was not in PlayMode and no Prefab Stage was open.
- Test-time prefab safety snapshot created: `Backup/PrefabSafetySnapshots/20260707_233435`.
- Test-time prefab hash manifests written:
  - `Backup/PrefabSafetySnapshots/20260707_233435_before_unity_tests_hashes.tsv`
  - `Backup/PrefabSafetySnapshots/20260707_233435_after_unity_tests_hashes.tsv`
- Unity refresh/import completed with `refresh_unity(mode=force, scope=all, compile=request)`; Console reported 0 compile/import errors immediately after refresh.
- Targeted Unity EditMode tests passed: 10 total, 10 passed, 0 failed, 0 skipped.
  - `Runtime2DEnemyBrain_DasherSweepDamagesOnceDuringActive`
  - `Runtime2DEnemyBrain_EliteChaserSweepDamagesDuringDash`
  - `Runtime2DEnemyBrain_EliteDecisionTimerTicksOncePerAIFrame`
  - `Runtime2DEnemyBrain_RangedKiteDestinationStaysOnLegalGround`
  - `Runtime2DEnemyAIContext_UsesCoordinatorRegistryForNearbyFriends`
  - `Runtime2DTargetProvider_UsesRegisteredEnemyTargetsForAllyActor`
  - `Runtime2DEnemyBrain_FrontlineTank_ChasesPlayerInsteadOfCenterLeash`
  - `Runtime2DEnemyBrain_CenterTank_UsesLeashHysteresis`
  - `Runtime2DRoomEnemySpawner_NoEffectiveCombatTimeoutClearsStuckBudgetEnemies`
  - `Runtime2DRoomEnemySpawner_NoEffectiveCombatTimeout_IgnoresMovingBudgetEnemies`
- Post-test Editor state was idle, not compiling, not updating, not in PlayMode, and no Prefab Stage was open.
- Post-test Console contained one Unity Test Runner result-path record (`Saving results to: .../TestResults.xml`) reported by MCP as an `Exception` type with no stack trace; the test job itself succeeded.
- Final post-test prefab/meta hash diff: 0 across 118 tracked prefab / prefab meta files.
- PlayMode was not stopped or entered by the agent, and PlayMode tests were not run.

## Blockers

None.

## Verification

Runtime implementation verification:

- Prefab safety snapshots created:
  - `Backup/PrefabSafetySnapshots/20260706_182754`
  - `Backup/PrefabSafetySnapshots/20260706_185648`
  - `Backup/PrefabSafetySnapshots/20260706_190148`
  - `Backup/PrefabSafetySnapshots/20260706_190500`
  - `Backup/PrefabSafetySnapshots/20260706_191008`
  - `Backup/PrefabSafetySnapshots/20260706_193226`
- Unity refresh/import completed after new scripts and prefab wiring; Console error count was 0 after the final refresh.
- `dotnet build Lilith.Kernel.csproj --no-restore /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- `dotnet build Lilith.Tests.EditMode.csproj --no-restore /p:UseSharedCompilation=false` passed with 0 warnings and 0 errors.
- Python renderer syntax check passed with `python -m py_compile Tools/Runtime2DEnemyAISimulation/render_runtime2d_enemy_ai_report.py`.
- Unity EditMode tests passed: 7 total, 7 passed, 0 failed.
  - `Runtime2DEnemyPrefab_HasRuntime2DBrain`
  - `Runtime2DRoomEnemySpawner_LegacyCombatPath_InitializesRuntime2DBrainContext`
  - `Runtime2DRoomEnemySpawner_DirectorPath_InitializesRuntime2DBrainContextFromSocket`
  - `Runtime2DCombatCoordinator_GrantsRejectsReleasesAndExpiresTokens`
  - `Runtime2DEnemyAISimulator_WritesDeterministicJsonReport`
  - `Runtime2DRoomEnemySpawner_WithDirectorProfile_SpawnsOpeningAndQueuesBatches`
  - `Runtime2DRoomEnemySpawner_CombatRoom_SpawnsCappedEnemiesAtSpawnCells`
- Prefab Stage check before final tests: no prefab stage open.
- Final prefab hash diff after tests: 0.
- Offline smoke report generated:
  - `Temp/Runtime2DEnemyAISimulation/smoke/simulation.json`
  - `Reports/Runtime2DEnemyAISimulation/smoke/summary.json`
  - `Reports/Runtime2DEnemyAISimulation/smoke/metrics.csv`
  - `Reports/Runtime2DEnemyAISimulation/smoke/failures.csv`
  - `Reports/Runtime2DEnemyAISimulation/smoke/contact_sheets/overview.png`
- Smoke summary: report count 1, cleared count 1, failure count 0, enemy count 1, death count 1, token grants 2, token denies 0.
- Offline matrix report generated:
  - `Temp/Runtime2DEnemyAISimulation/matrix/*.json`
  - `Reports/Runtime2DEnemyAISimulation/matrix/summary.json`
  - `Reports/Runtime2DEnemyAISimulation/matrix/metrics.csv`
  - `Reports/Runtime2DEnemyAISimulation/matrix/failures.csv`
  - `Reports/Runtime2DEnemyAISimulation/matrix/token_pressure.csv`
  - `Reports/Runtime2DEnemyAISimulation/matrix/elite_behavior_distribution.csv`
  - `Reports/Runtime2DEnemyAISimulation/matrix/room_timeline.csv`
  - `Reports/Runtime2DEnemyAISimulation/matrix/charts/token_pressure.png`
  - `Reports/Runtime2DEnemyAISimulation/matrix/charts/elite_behavior_distribution.png`
  - `Reports/Runtime2DEnemyAISimulation/matrix/charts/room_timeline_overview.png`
  - `Reports/Runtime2DEnemyAISimulation/matrix/room_timelines/*.png`
  - `Reports/Runtime2DEnemyAISimulation/matrix/contact_sheets/overview.png`
- Matrix summary: report count 24, cleared count 24, failure count 0, enemy count 66, death count 66, token grants 495, token denies 123.
- Matrix coverage counts: archetype 6, combat type 4, kind 10, main tag 6, spawn role 5, wave phase 3, elite bool 2.
- Additional matrix assertions covered hazard cells, ambush spawn role, summon grant / summon-limit deny, non-player decoy target, failsafe events, and token deny pressure.
- Unity EditMode tests passed after matrix expansion: 8 total, 8 passed, 0 failed.
  - Includes `Runtime2DEnemyAISimulator_MatrixCoversArchetypesCombatTypesTagsAndWritesReports`.
- Unity EditMode Runtime2D related test suite passed after production attack evidence expansion: 32 total, 32 passed, 0 failed.
  - Includes `Runtime2DEnemyBrain_DirectAttackSpec_AppliesDamageThroughCombatTarget`.
  - Includes `Runtime2DEnemyBrain_RangedAttackSpec_EmitsXYCharBullet`.
- Final post-test prefab/meta hash diff after the 8-test run: 0.
- Final post-test prefab/meta hash diff after the 32-test run: 0.
- Added and verified default profile / Main wiring guard tests:
  - `Runtime2DSpawnDirectorProfile_DefaultAsset_CoversMinionRoles`
  - `MainScene_Runtime2DRoomEnemySpawner_UsesDefaultSpawnDirectorProfile`
- `Assets/Scenes/Main.unity` disk YAML confirms `spawnDirectorProfile: {fileID: 11400000, guid: 7b796e0cf8bee0a4f9271b898b99ee1c, type: 2}`.
- Unity EditMode Runtime2D related test suite passed after default profile / Main wiring: 34 total, 34 passed, 0 failed.
- Final post-test prefab/meta hash diff after the 34-test rerun using current pre-test baseline: 0.
- Added and verified AI observability / lifecycle guard tests:
  - `Runtime2DEnemyAIContext_ExposesTargetScoreTokensPressureAndNearbyFriends`
  - `Runtime2DEnemyBrain_AttackSpec_TracksTelegraphAndInterruptsWindup`
  - `Runtime2DCombatCoordinator_DebugTracksWaitingTokenRequests`
  - Existing `Runtime2DEnemyAISimulator_WritesDeterministicJsonReport` now also verifies `RoomId` and behavior selection counts.
- Unity EditMode Runtime2D related test suite passed after AI observability closure: 37 total, 37 passed, 0 failed.
- Final post-test prefab/meta hash diff after the 37-test rerun using current pre-test baseline: 0.
- PlayMode was not entered and PlayMode tests were not run.

Document landing verification:

- `git diff --check` passed. Git reported the existing README line-ending normalization warning only.
- Confirmed `Docs/Runtime2DRoomEnemyAISystemAgentSpec.md` exists.
- Confirmed `Docs/Runtime2DRoomEnemyAIImplementationLog.md` exists.
- Confirmed the formal spec contains `正式目标规格`, `不得修改本文`, and `ImplementationLog` constraints.
- Confirmed the formal spec has no source citation residue, personalized wording residue, or open-goal residue from the source draft.
- Confirmed README collaboration document table links to the formal spec only.
- Review amendment confirmed: bot simulation is specified as non-PlayMode C# deterministic tick simulation plus Python report rendering; production runtime damage contract is specified as `ICombatDamageTarget.TryApplyDamage(...)`, with `CharBullet` limited to XY ranged / barrage runtime projectiles.
