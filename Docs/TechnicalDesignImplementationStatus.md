# Technical Design Implementation Status

This document records implementation progress and verification evidence without changing the design documents or their checkboxes.

## Step 0 — Project Baseline and Assembly Boundaries

**Status:** Complete  
**Completed:** 2026-08-08  
**Next step:** Steps 1 through 8 are complete; Step 9 has not been started.

### Implemented

- Created the prescribed runtime folder tree under `Assets/Scripts/Runtime`.
- Created `Assets/Scripts/Editor/Validation`, `Assets/Scripts/Editor/Sandbox`, `Assets/Scripts/Tests/EditMode`, and `Assets/Scripts/Tests/PlayMode`.
- Created the data folders under `Assets/Data` and prefab folders under `Assets/Prefabs`.
- Created the test-fixture folder at `Assets/Tests/Fixtures`.
- Added the four assembly definitions:
  - `MonstersVsZombies.Runtime`
  - `MonstersVsZombies.Editor`
  - `MonstersVsZombies.Tests.EditMode`
  - `MonstersVsZombies.Tests.PlayMode`
- Restricted `MonstersVsZombies.Editor` to the Editor platform so it is excluded from player builds.
- Referenced `MonstersVsZombies.Runtime` from the Editor and both test assemblies.
- Added a runtime assembly marker and Edit Mode/Play Mode smoke tests that verify the runtime reference resolves to `MonstersVsZombies.Runtime`.
- Added an Editor setup command at `Tools/Monsters vs Zombies/Step 0/Apply and Verify`.
- Used that Editor command to add the required layers through Unity's serialized project settings API:
  - Layer 8: `World`
  - Layer 9: `UnitBody`
  - Layer 10: `UnitTarget`
  - Layer 11: `Projectile`
- The setup command also verifies the Unity version, registered packages, active input backend, and runtime assembly reference.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Unity version | `ProjectVersion.txt` and the running Editor report `6000.5.5f1`. | Pass |
| Package resolution | The running Editor registered Input System `1.19.0`, AI Navigation `2.0.14`, Test Framework `1.7.0`, and URP `17.5.0`. | Pass |
| Input backend | `activeInputHandler` is `1` (new Input System only), and the Editor setup verification succeeded. | Pass |
| Required layers | The Editor setup command created and then verified `World`, `UnitBody`, `UnitTarget`, and `Projectile` at indices 8–11. | Pass |
| Runtime/editor boundaries | All four assemblies compiled; the Editor assembly is restricted to `Editor`, and the Editor verification resolved `RuntimeAssemblyMarker` from `MonstersVsZombies.Runtime`. | Pass |
| Final live-project compilation | Unity reported Tundra build success with exit code 0 and successfully reloaded the assemblies after the final C# batch. | Pass |
| Edit Mode smoke test | 1 total, 1 passed, 0 failed, 0 skipped, 0 inconclusive; Unity test-run exit code 0. | Pass |
| Play Mode smoke test | 1 total, 1 passed, 0 failed, 0 skipped, 0 inconclusive; Unity test-run exit code 0. | Pass |
| Compiler/assembly diagnostics | Final live compilation and both test-run logs contain no C# compiler errors, C# compiler warnings, script-compilation failures, or assembly-reference warnings. | Pass |
| Scope audit | `GameDesign.md`, `TechnicalDesign.md`, `TechnicalDesignImplementation.md`, and `SampleScene.unity` are unchanged. | Pass |

The smoke tests were executed by Unity `6000.5.5f1` in batch mode against an isolated source copy of the workspace's `Assets`, `Packages`, and `ProjectSettings`. Edit Mode was selected with `-testPlatform EditMode -assemblyNames MonstersVsZombies.Tests.EditMode`; Play Mode was selected with `-testPlatform PlayMode -assemblyNames MonstersVsZombies.Tests.PlayMode`. The generated NUnit result XML supplied the totals above, each Unity process returned exit code 0, and all setup/test logs were scanned for compiler and assembly diagnostics before the temporary copy was removed.

The first compilation exposed an ambiguous Unity `PackageInfo` type reference. It was corrected with an explicit package-manager alias, followed by a clean compilation and both passing test runs. A broad asset-save call was also narrowed to save only `TagManager.asset` before the final clean compilation.

### Explicit Structural Choice

The implementation document requests a test-fixture/test-assets folder but does not prescribe its exact path. `Assets/Tests/Fixtures` was used as the conventional project-local location. No gameplay behavior, balance value, or other missing design decision was introduced.

## Step 1 — Shared Identifiers and Pure Rules

**Status:** Complete  
**Completed:** 2026-08-08  
**Commit summary:** `Step 1 completed: add shared identifiers and pure rules`  
**Next step:** Steps 2 through 8 are complete; Step 9 has not been started.

### Implemented

- Added `UnitFaction` with Player, Ally, and Enemy values and an invalid default serialized value.
- Added `AttackDeliveryType` with an explicit `Unspecified` default plus Melee, Projectile, Grenade, and Hitscan.
- Added stable `UnitId`, `AttackId`, `PoolId`, `SpawnId`, and `AttackSequenceId` value types.
- Added composite `AttackKey` identity using source spawn ID plus source-local attack sequence ID.
- Added explicit immutable interaction, damage, pool, and spawn result/reason types with safe default states.
- Added immutable `DamagePayload`, `HitContext`, `DamageResult`, `InteractionResult`, and status-effect payload values with defensive copies for effect collections.
- Added the single `FactionRules` hostility matrix.
- Added the plain C# `HealthState` model with initialization, damage, healing, clamping, single death transition, finite-value validation, and reset.
- Added plain previous/next weapon-index wrapping.
- Added the stateful Stunner schedule for successful damaging hits 1, 4, 7, and onward; rejected interactions and misses do not advance it.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Live-project compilation | Final Unity Tundra build succeeded, followed by a successful assembly reload. | Pass |
| Compiler/assembly diagnostics | Final code-batch scan found 0 C# compiler errors, C# compiler warnings, script-compilation failures, or unresolved-assembly diagnostics. | Pass |
| Edit Mode suite | 39 total, 39 passed, 0 failed, 0 skipped, 0 inconclusive; Unity process exit code 0. This includes 38 Step 1 cases and the Step 0 smoke test. | Pass |
| Faction matrix | All nine Player/Ally/Enemy attacker-target combinations are covered. | Pass |
| Health rules | Initialization, ordinary damage, healing, upper/lower clamping, exact lethal damage, overkill, death once, reset, invalid values, NaN, and infinity are covered. | Pass |
| Weapon wrapping | Previous/next wrapping and the single-weapon case are covered. | Pass |
| Stunner cadence | Hits 1/4/7, rejected interactions, misses, and reset are covered. | Pass |
| Identity behavior | Equality, hashing, collection uniqueness, local-sequence changes, and sequence reuse under a different source spawn are covered. | Pass |
| Immutable result invariants | Default results cannot report success; applied interaction requires applied positive finite damage; payload/effect inputs are defensively copied. | Pass |
| Scope audit | No scene, prefab, concrete definition asset, balance value, or design-document checkbox was changed. | Pass |

The tests ran in Unity `6000.5.5f1` batch mode against the isolated source copy used for verification. The NUnit XML supplied the totals above, and its Unity log was scanned before the temporary copy was removed.

### Explicit Sequencing Resolutions

- `TechnicalDesign.md` requires `HitContext` to contain a `DamageController`, but Step 1 requires `HitContext` before `DamageController` is created in Step 3. Step 1 therefore uses the explicitly documented `IDamageReceiver` compile-time boundary. `DamageController` will be its only production implementation, and Step 3 will close the public hit target to the concrete controller boundary once that type exists. This changes no interaction rule or gameplay behavior.
- The design requires a damage category in `DamagePayload` but names no category values. An opaque `DamageCategoryId` with explicit validity is present, while no concrete category string or balance meaning was invented. Concrete attacks remain uncategorized until an authoritative category is supplied or required by a later design revision.

## Step 2 — Configuration and Catalog Scripts

**Status:** Complete
**Completed:** 2026-08-08
**Commit summary:** `Step 2 completed: add configuration and catalog validation`
**Next step:** Steps 3 through 8 are complete; Step 9 has not been started.

### Implemented

- Added the abstract `UnitDefinition` and concrete `PlayerUnitDefinition` and `AIUnitDefinition` ScriptableObjects, keeping Player free of chase and AI fields.
- Added `AttackDefinition`, `WeaponDefinition`, and `ProjectileDefinition` with explicit delivery compatibility and contextual validation.
- Added optional accepted-hit effect configuration without defining any unapproved damage categories or gameplay balance values.
- Added the typed `WeaponId` required by the technical design rather than using a raw string or overloading another identifier.
- Added `PoolCatalog` and `PoolCatalogEntry` with separate initial prewarm, maximum inactive retained, capacity policy, maximum active, and collection-check settings.
- Added `UnitCatalog` and its definition-backed entry type so each unit ID has one source of truth.
- Added `SandboxSpawnConfiguration` with a required Player definition and optional counted AI definitions.
- Added shared ordered validation results and finite-number validation used by every definition, catalog, configuration, `OnValidate` path, and Edit Mode test.
- Kept validation at the asset/reference level; no validator inspects prefab components before the components exist.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Live-project compilation | Final Unity Tundra build succeeded at `Editor.log` line 29954 and the runtime/test assemblies reloaded successfully at line 29967. | Pass |
| Compiler/assembly diagnostics | The final live-project code-batch scan found 0 C# compiler errors, C# compiler warnings, script-compilation failures, or unresolved-assembly diagnostics. | Pass |
| Edit Mode suite | 56 total, 56 passed, 0 failed, 0 skipped, 0 inconclusive; Unity process exit code 0. This includes 17 Step 2 cases and all 39 earlier cases. | Pass |
| Catalog rules | Duplicate unit IDs, duplicate pool IDs, null entries, missing definitions, missing prefabs, and pool-capacity invariants are covered. | Pass |
| Unit-definition rules | Player faction/no-chase shape, AI factions, required default attack, positive chase range, and attack-range-versus-chase-range are covered. | Pass |
| Attack/projectile rules | Unspecified, missing, and incompatible delivery configurations; Melee/Hitscan exclusions; Projectile/Grenade contextual requirements; and accepted-hit effect durations are covered. | Pass |
| Numeric rules | Required damage/range/cooldown values and optional windup/recovery/gravity/radius/prewarm values are independently covered for zero, negative, NaN, or infinity as appropriate. | Pass |
| Asset scope | No persistent concrete definition, catalog, projectile, weapon, prefab, scene, or balance asset was created; test objects exist only in memory. | Pass |
| Scope audit | The three design documents, their checkboxes, `SampleScene.unity`, and existing input/URP assets are unchanged. | Pass |

The definitive suite ran in Unity `6000.5.5f1` batch mode against a fresh isolated source copy. NUnit XML supplied the totals, and the Unity log was scanned for compiler and assembly diagnostics before cleanup.

### Explicit Structural Choices

- The technical design requires a Weapon ID but Step 1 did not list a wrapper for it. `WeaponId` follows the same stable, ordinal, case-sensitive value semantics as the other authored identifiers; no identifier value was invented.
- Projectile definitions store an explicit compatible delivery type so Projectile and Grenade compatibility is validated without inferring behavior from tuning values. Hitscan has no projectile definition.
- `DamageCategoryId` remains optional and unset in attack definitions because the design names no concrete damage categories.
- No concrete ScriptableObject test asset was required: in-memory test instances provide the smallest test surface and leave `Assets/Tests/Fixtures` empty for later fixture assets that genuinely need serialization.

## Step 3 — Core Unit Components

**Status:** Complete
**Completed:** 2026-08-08
**Commit summary:** `Step 3 completed: add core unit components`
**Next step:** Steps 4 through 8 are complete; Step 9 has not been started.

### Implemented

- Added `UnitController` as the composition root for definition, faction, spawn identity, logical activity, required core siblings, and an optional `IUnitMotor` capability.
- Added `HealthController` as the sole Unity-facing health adapter around `HealthState`; its damage and healing mutation methods remain internal and its state is externally read-only.
- Added `DamageController` as the concrete target-side damage boundary and changed `HitContext.Target` from the temporary Step 1 interface to `DamageController`.
- Added `StatusEffectController` with deterministic stun refresh/expiry, movement/chase/attack blocking, and death/return reset behavior.
- Added `UnitLifecycleController` with observable Inactive, Active, Dying, and PoolReturn transitions; logical spawn/despawn events; synchronous pool callbacks; and a return-request boundary above the pool callback.
- Added `IPoolable` with activation-independent setup, activation-dependent setup, and return phases without depending on a pool service.
- Added `IUnitMotor` with shared stop, resume, world-position move, and facing commands for later Player and NavMesh implementations.
- Added tracked per-spawn unsubscribe actions while keeping permanent Health-to-Lifecycle sibling wiring until destruction; publishers never blanket-clear their events.
- Added runtime assembly visibility only for the two project test assemblies so tests can prove internal mutation boundaries without exposing them publicly.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Live-project compilation | Final Unity Tundra build succeeded at `Editor.log` line 31800 and assemblies reloaded successfully at line 31812. | Pass |
| Compiler/assembly diagnostics | The final live-project batch and isolated test log contain 0 C# errors, C# warnings, script-compilation failures, or unresolved-assembly diagnostics. | Pass |
| Edit Mode suite | 83 total, 83 passed, 0 failed, 0 skipped, 0 inconclusive; Unity process exit code 0. This includes 27 Step 3 cases and all 56 earlier cases. | Pass |
| Health/damage boundary | Reflection and integration tests prove `HealthController` has no public health mutator or state escape and accepted hits reach it through concrete `DamageController`. | Pass |
| Damage/death behavior | Exact applied amounts, overkill clamping, invulnerability, invalid amounts, event ordering, lethal-effect rejection, death once, and later dead-target rejection are covered. | Pass |
| Status behavior | Immediate action blocking, maximum-duration refresh, deterministic expiry once, direct dead-target rejection, and coherent clear-on-death state are covered. | Pass |
| Lifecycle behavior | Two-phase setup plus final logical activation, Active/Dying/PoolReturn/Inactive transitions, early-return guards, immediate death-return requests, logical despawn once, and partial setup cleanup are covered. | Pass |
| Pool subscription behavior | Two death/return/respawn cycles prove permanent sibling wiring survives without duplication and tracked current-spawn listeners are selectively removed. | Pass |
| Programmatic fixture scope | All component tests construct GameObjects and ScriptableObjects in memory; no production prefab, scene object, concrete balance asset, or persistent test asset was created. | Pass |
| Independent review | Read-only semantic audit passed after lifecycle reentrancy, event ordering, and activation-return regressions were closed. | Pass |
| Scope audit | The design documents, their checkboxes, `SampleScene.unity`, and existing input/URP assets are unchanged. | Pass |

The definitive suite used the isolated Unity `6000.5.5f1` validation copy synchronized with the final runtime and test sources. NUnit XML supplied the totals, and the Unity log was scanned for compiler and assembly diagnostics before cleanup.

### Explicit Structural Choices

- Damage rejection checks a dead target before inactive state, so a Dying unit reports `TargetDead`; the design does not prescribe precedence, and this preserves the most specific diagnostic without changing acceptance rules.
- `DamageResolved` publishes every applied or rejected result for diagnostics; listeners are observation-only and cannot alter the returned result.
- `IUnitMotor.MoveTo` and `FaceTowards` use world positions. This resolves the unspecified signature in a form that both direct Player movement and later NavMesh destination movement can implement without weakening stop/resume semantics.
- A return requested during Dying is published through `PoolReturnRequested` only after lifecycle callbacks finish, before any future `PoolManager` begins its synchronous `IPoolable.PrepareForReturn` sequence. Return requests during logical activation finalization are rejected so activation cannot report success for an already-returned unit.
- Logical `Despawned` publishes when a unit becomes non-participating at Dying or PoolReturn; the later physical return exposes PoolReturn and Inactive state transitions without publishing a second logical despawn.

## Step 4 - Unit Registration and Interaction

**Status:** Complete
**Completed:** 2026-08-08
**Commit summary:** `Step 4 completed: add unit registration and interaction`
**Next step:** Steps 5 through 8 are complete; Step 9 has not been started.

### Implemented

- Added `UnitRegistry` with active-spawn registration, expected-unit removal, spawn-ID lookup, faction counts, immutable event snapshots, and deterministic snapshot copies.
- Wired registry removal to logical `Despawned`, so an entry is removed once while its old identity is still available and before pooled reuse can assign a new spawn.
- Added `DamageTargetProxy` to cache a hurtbox collider's owning `UnitController` and `DamageController` while exposing the owner's current pooled spawn identity.
- Added reusable delivery-owned `AttackHitLedger` storage keyed by composite `AttackKey`; reset clears bounded per-delivery history and no scene service retains completed attacks.
- Added `InteractionSystem` validation for payload/ledger, current target identity, self-hit, faction, alive/active state, invulnerability, and duplicate-hit rejection before dispatching only to `DamageController`.
- Kept impact legality independent of a live or registered source object by using only the captured source spawn ID and faction in `DamagePayload`.
- Added fixed-capacity `TargetQueryBuffer` and `AreaQueryBuffer` implementations that query the configured `UnitTarget` layer with trigger collision enabled, filter current hostile active/alive targets, deduplicate by current spawn ID, and report conservative saturation.
- Added deterministic nearest-candidate comparison with the lowest valid spawn ID winning exact equal-distance ties.
- Added a project-local Editor verification command that runs the Edit Mode test assembly synchronously and writes auditable totals under `Logs` when MCP test routing is unavailable.
- Added programmatic Edit Mode fixtures for registry lifecycle, all faction interactions, duplicate/reused attack identity, captured in-flight payloads, inactive/dead/pooled targets, reentrant damage callbacks, hurtbox caching, non-allocating query reuse, and tie handling.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Correct-project compilation | Unity `6000.5.5f1` compiled the target project; the final live-Editor Tundra build succeeded in `Editor.log` at line 1324 and completed its domain reload. | Pass |
| Compiler/assembly diagnostics | The final code-batch log contains 0 C# errors, C# warnings, script-compilation failures, unhandled exceptions, or NUnit assertion exceptions. | Pass |
| Edit Mode suite | 125 total, 125 passed, 0 failed, 0 skipped, 0 inconclusive. This includes 42 Step 4 cases and all 83 earlier cases. | Pass |
| Registry lifecycle | Active-only registration, duplicate rejection, lookup/count accuracy, immutable event identity, death/return removal once, respawn with a new identity/faction, stale-ID protection, and sorted snapshot isolation are covered. | Pass |
| Interaction legality | All nine faction combinations pass through `InteractionSystem`; self, invalid, inactive, dead/Dying, pooled, invulnerable, and duplicate outcomes are asserted with no illegal health mutation. | Pass |
| Attack identity and reuse | Same-target duplicate rejection, multiple targets under one key, later sequence reuse, same sequence under a new source spawn, separate ledgers, reset, and reentrant duplicate prevention are covered. | Pass |
| Captured impact source | Tests prove a captured payload remains valid after source unregister, after source-object reuse with another faction, and without any source registration. | Pass |
| Query/proxy behavior | Cached owner references, current pooled identity, exact physics layer, explicit trigger inclusion, faction/state filtering, multiple-hurtbox deduplication, saturation, reuse without stale candidates, and independent target/area buffers are covered. | Pass |
| Deterministic tie | Exact equal distances choose the lower valid spawn ID; closer candidates still win and invalid distances are rejected. | Pass |
| Independent review | Two read-only semantic audits passed after the hurtbox owner-cache Awake-order regression was closed. | Pass |
| Scope audit | No scene, prefab, ScriptableObject asset, input asset, balance value, package, or design-document checkbox was changed. | Pass |

The final suite ran through the project-local verification command in the verified correct target Editor after Unity generated metadata for the new scripts. `Logs/ImplementationEditModeSummary.txt` supplied the totals, and the final Unity log was scanned for compiler, assembly, unhandled-exception, and assertion diagnostics.

### Explicit Structural Choices

- Rejection precedence is invalid payload/ledger, invalid current target identity, self-hit, invalid faction, dead, inactive, invulnerable, duplicate, then applied. A configured but inactive unit reports `TargetInactive`, Dying reports `TargetDead`, and a returned identity-cleared unit reports `InvalidTarget`.
- Registry and faction snapshots are sorted by ascending spawn ID. Exact equal-distance target ties also select the lower valid spawn ID; the design requires determinism but does not prescribe the direction.
- Query capacities remain constructor inputs owned by later targeting/delivery systems. Step 4 introduces no production capacity or warning-throttle balance value.
- The ledger reserves a target immediately before damage dispatch to block a reentrant copy of the same hit. If the downstream damage boundary unexpectedly rejects the dispatch, that reservation is removed so only accepted hits remain recorded.
- Filling a non-allocating physics buffer sets `WasSaturated` conservatively because Unity cannot distinguish an exactly full result from omitted overflow without allocating or issuing another query.

## Step 5 - PoolManager

**Status:** Complete
**Completed:** 2026-08-09
**Commit summary:** `Step 5 completed: add pooled entity and pool manager`
**Next step:** Steps 6 through 8 are complete; Step 9 has not been started.

### Implemented

- Added `PooledEntity` as the root aggregator for activation-independent, activation-dependent, and return callbacks on generic `IPoolable` components.
- Cached only callbacks owned by the nearest pooled root, so nested pooled hierarchies retain their own lifecycle boundary without concrete component searches.
- Added an internal `RuntimeObjectPool` wrapper around Unity `ObjectPool<PooledEntity>` and a scene-level `PoolManager` with validated `PoolCatalog` initialization and stable `PoolId` lookup.
- Added inactive prewarming and an inactive creation root so prefab construction cannot accidentally run activation-dependent behavior before rent and spawn setup.
- Implemented Expandable and HardActiveLimit policies while using Unity `ObjectPool<T>.maxSize` only for maximum inactive retention.
- Implemented generic rent and return boundaries: rent always yields an inactive root; return clears rented participation before callbacks, runs reset callbacks, deactivates the root, and releases it.
- Added Editor/Development-only collection checks from catalog configuration.
- Added immutable per-pool diagnostics for cumulative created, current rented-active, current inactive, peak rented-active, failed-rent, capacity-reached, overflow-destroy, and effective collection-check state.
- Added manager-level accounting for failed unknown-pool rents, which cannot belong to a valid per-pool diagnostic record.
- Added controlled failures for missing catalogs, invalid catalog entries, pooled prefabs without a root `PooledEntity`, unknown IDs, capacity limits, foreign returns, and double returns.
- Added a runtime-compatible, non-production test-fixture assembly under `Assets/Tests/Fixtures` so Unity can attach the programmatic `IPoolable` probe without creating a scene or production prefab.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Correct-project compilation | Unity `6000.5.5f1` compiled the target project; the final Tundra build succeeded in `Editor.log` at line 4131 and assemblies reloaded at line 4144. | Pass |
| Compiler/assembly diagnostics | The final code-batch scan after line 4131 found 0 C# errors, C# warnings, script-compilation failures, unhandled exceptions, or NUnit assertion exceptions. | Pass |
| Edit Mode suite | 141 total, 141 passed, 0 failed, 0 skipped, 0 inconclusive. This includes 16 Step 5 cases and all 125 earlier cases. | Pass |
| Prewarm and inactivity | Configured prewarm counts, created/inactive/active/peak diagnostics, inactive roots, and zero `OnEnable` calls during prewarm are asserted. | Pass |
| Spawn phases | Tests prove context is assigned while inactive, activation-independent reset runs inactive, activation-dependent setup runs after GameObject activation but before logical activation/registration, and both failure phases can return safely without logical activation. | Pass |
| Return and reuse | The same instance is reused with a new `SpawnId`; prior identity, logical/registration state, and transient state are reset, and returned objects are inactive. | Pass |
| Capacity policies | Expandable grows to three simultaneous rents without a failed rent; HardActiveLimit rejects before `ObjectPool.Get`; maximum inactive retention destroys overflow and increments its diagnostic. | Pass |
| Controlled errors | Missing/invalid catalogs, missing root aggregator, unknown rent, foreign return, and double return outcomes are asserted. | Pass |
| Generic aggregation | Nested pooled roots own their callbacks independently, and the pool code contains no search for unit, physics, navigation, particle, or trail component types. | Pass |
| Scope audit | No scene, production prefab, ScriptableObject asset, input asset, balance value, package, or design-document checkbox was changed. | Pass |

The definitive suite ran through the project-local verification command in the verified correct target Editor. `Logs/ImplementationEditModeSummary.txt` supplied the final totals, and the final Unity log segment was scanned for compiler, assembly, unhandled-exception, and assertion diagnostics.

### Explicit Structural Choices

- Pool diagnostics use "active" to mean currently rented from the pool. Logical gameplay participation remains a later `SpawnManager`/lifecycle decision and occurs only after both pooled setup phases succeed.
- New instances are cloned beneath a temporary inactive manager child, set inactive, then reparented to the manager. This prevents an authored active prefab from receiving `OnEnable` during creation or prewarm without changing the prefab itself.
- Callback aggregation follows Unity's stable hierarchy/component enumeration and excludes any component whose nearest `PooledEntity` is a nested root. No callback priority system or component-specific reset order was invented.
- Unknown-pool rent failures increment `PoolManager.UnknownPoolFailedRentCount` and the manager total because there is no valid pool record to own that failure; known-pool failures remain in their per-pool diagnostics.
- The test fixture assembly is runtime-compatible but not auto-referenced. The Edit Mode test assembly references it explicitly, allowing programmatic `MonoBehaviour` fixtures while keeping them outside production runtime assemblies and assets.

## Step 6 - SpawnManager and Spawn Requests

**Status:** Complete
**Completed:** 2026-08-09
**Commit summary:** `Step 6 completed: add spawn orchestration and requests`
**Next step:** Steps 7 and 8 are complete; Step 9 has not been started.

### Implemented

- Added immutable `UnitSpawnRequest` and `ProjectileSpawnRequest` values with validated definitions, payloads, finite poses, optional source unit identity, and explicit spawn reasons.
- Added `UnitSpawnContext` plus root-level unit-context and projectile-lifecycle extension contracts so later features can receive per-spawn metadata without expanding `UnitController` or introducing projectile movement early.
- Added `SpawnManager` as the only request-to-live-instance path. It resolves the definition's stable pool ID through `PoolManager` and never instantiates an unpooled fallback.
- Added monotonically increasing unit `SpawnId` assignment; a rented spawn attempt consumes its identity even if a later phase fails, so an identity is never reused.
- Applied resolved pose, definition, faction, spawn ID, reason, optional source identity, and projectile payload while the pooled root is inactive.
- Ran activation-independent pooled reset while inactive, activated the GameObject, ran activation-dependent setup, then performed logical unit activation/registration or projectile start.
- Returned failed partial unit and projectile spawns through `PoolManager` without registry membership or projectile start.
- Connected lifecycle pool-return requests back to `SpawnManager`, preserving registry removal and generic pool cleanup.
- Added deterministic indexed and repeatable round-robin `SpawnPointGroup` selection with explicit reset and controlled invalid-point results.
- Added optional `ISpawnPositionValidator` orchestration and a `NavMeshSpawnPositionValidator` that requires caller-supplied sample distance and area mask instead of inventing scene values.
- Added `InitialSandboxSpawner`, `DebugUnitSpawner`, and an explicit source-identified death-spawn method without automatic `Start`, UI, input, concrete definition, or death-effect wiring.
- Added runtime-compatible programmatic unit and projectile probes under `Assets/Tests/Fixtures`; no production hierarchy, prefab, definition asset, or scene object was created.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Correct-project compilation | Unity `6000.5.5f1` compiled the target project; the final Tundra build succeeded in `Editor.log` at line 6714 and assemblies reloaded at line 6726. | Pass |
| Compiler/assembly diagnostics | The final code-batch scan after line 6714 found 0 C# errors, C# warnings, script-compilation failures, unhandled exceptions, or NUnit assertion exceptions. | Pass |
| Edit Mode suite | 163 total, 163 passed, 0 failed, 0 skipped, 0 inconclusive. This includes 22 Step 6 cases and all 141 earlier cases. | Pass |
| Pool selection and failures | Definition-owned pool selection, missing definitions, invalid poses, unknown pools, invalid projectile payloads, missing projectile lifecycle, and both activation-phase failures are covered. | Pass |
| Unit spawn order | Tests prove pose, definition, faction, spawn ID, reason, and source identity are assigned inactive; activation-dependent setup observes an active GameObject while the unit is logically inactive and absent from the registry. | Pass |
| Projectile spawn order | Tests prove captured payload/configuration and reset occur inactive, activation-dependent setup observes active/not-started state, and start occurs only afterward. Start failure returns the object inactive. | Pass |
| Registry and return | Successful spawn adds exactly one faction entry; explicit return and lifecycle-requested return remove it and deactivate the pooled instance. | Pass |
| Reuse identity | A returned instance is reused from the same pool with a strictly newer spawn ID, updated pose, clean context, and no stale registration. | Pass |
| Position validation | Rejection happens before rent; an accepted resolved position is applied before context/setup; NavMesh validation requires explicit sampling configuration. | Pass |
| Spawn points | Indexed selection follows authored order; round-robin cycles deterministically and reset restarts at the first point. | Pass |
| Entry points | Initial, Debug, and source-identified DeathEffect request reasons are asserted without UI or concrete-unit wiring. | Pass |
| Scope audit | `SpawnManager` contains no instantiate fallback. No scene, production prefab, ScriptableObject asset, input asset, balance value, package, or design-document checkbox was changed. | Pass |

The definitive suite ran through the project-local verification command in the verified correct target Editor. `Logs/ImplementationEditModeSummary.txt` supplied the final totals, and the final Unity log segment was scanned for compiler, assembly, unhandled-exception, and assertion diagnostics.

### Explicit Structural Choices

- Existing `SpawnFailureReason` values are reused: missing manager readiness maps to `RentFailed`, invalid request metadata maps to `InvalidDefinition`, phase/context failures map to their activation-independent or activation-dependent values, and pool unknown/capacity failures retain their specific values.
- `IUnitSpawnContextReceiver` delivers reason and optional source identity to interested root modules while inactive. `UnitController` remains limited to definition, faction, generated spawn identity, activity, and cached capabilities.
- `IProjectileSpawnLifecycle` is only the Step 6 orchestration seam for inactive request capture and final start. Step 8 still owns the common projectile controller, movement, collision, damage, lifetime, and return behavior.
- Optional position validation is selected explicitly through the `SpawnManager` overload. `NavMeshSpawnPositionValidator` accepts caller-supplied sample distance and area mask; Step 6 introduces no sampling radius, area, or other balance default. Final `NavMeshAgent.Warp` and on-NavMesh confirmation remain activation-dependent responsibilities for Step 11.
- Spawn context and projectile lifecycle receivers are required on the pooled root. The documented production prefab hierarchy places lifecycle/combat capabilities on that root, and nested pooled roots remain independent.
- Initial and Debug entry-point components are manual request adapters only. Automatic initial spawning, input/UI commands, concrete unit references, and `SpawnUnitsOnDeath` behavior remain assigned to their later numbered steps.

## Step 7 - Targeting and Attack Timing

**Status:** Complete
**Completed:** 2026-08-09
**Commit summary:** `Step 7 completed: add targeting and attack timing`
**Next step:** Step 8 is complete; Step 9 has not been started.

### Implemented

- Added shared `CombatRangeRules` for finite, inclusive, squared XZ-plane distance checks used by target acquisition, target retention, attack gating, and melee impact revalidation.
- Added `TargetingController` with explicit caller-supplied query capacity and staggered scan schedule, cheap current-target checks between scans, nearest-hostile selection, target identity retention, and immediate target-loss events on target despawn or source death.
- Reused `TargetQueryBuffer` filtering and spawn-ID deduplication so multiple hurtboxes resolve to one logical candidate before deterministic nearest-target selection.
- Added separate targeting modes: AI units query their authored chase range; Player units query only the currently selected attack's authored attack range and never request movement.
- Added `IAttackExecutor`, immutable execution/timing/impact contracts, explicit delivery bindings, and successful-interaction result policies.
- Added `AttackController` with start-to-start cooldown, windup, one impact gate, recovery, cancellation, composite `AttackKey`, and reusable per-sequence `AttackHitLedger`.
- Preserved committed cooldown while clearing an interrupted windup and its ledger; death and pool return reset all timing and transient attack identity.
- Added explicit fixed-AI single-executor validation and Player Projectile/Grenade/Hitscan executor switching without replacing the `AttackController`.
- Added `AttackAnimationEventRelay`, with placeholder timing and later animation events both entering the same guarded impact method.
- Updated `UnitController` caching and completed-gameplay validation for `TargetingController` and `AttackController` while retaining the earlier core-only validation boundary used by generic pooled fixtures.
- Added runtime-compatible attack executor, result-policy, and motor probes plus Edit Mode tests for targeting, range, timing, cancellation, binding, policy, and pooled-state behavior.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Correct-project compilation | Unity `6000.5.5f1` compiled the target project; the final Tundra build succeeded in `Editor.log` at line 8170 and completed its assembly reload. | Pass |
| Compiler/assembly diagnostics | The final code-batch log segment from line 8170 contains 0 C# errors, C# warnings, script-compilation failures, unhandled exceptions, assertion exceptions, or assembly-resolution failures. | Pass |
| Edit Mode suite | 187 total, 187 passed, 0 failed, 0 skipped, 0 inconclusive. This includes 24 Step 7 cases and all 163 earlier cases. | Pass |
| Target selection | Nearest hostile selection, all invalid candidate states/factions, exact-distance deterministic ties, and multiple-hurtbox spawn-ID deduplication are asserted. | Pass |
| Range contract | Acquisition, retention, Player attack range, AI chase range, attack start, and melee impact use the shared root-position XZ rule; vertical separation and inclusive boundaries are covered. | Pass |
| Scan and lifecycle behavior | Explicit stagger offsets, cheap between-scan target loss, target despawn/death, source death, and subscription cleanup are covered. | Pass |
| Attack timing | Start-to-start cooldown, windup, one-impact gating, recovery, ledger reuse, successful-result policy feedback, and animation-relay impact routing are covered. | Pass |
| Cancellation and pooling | Stun, range loss, target loss/despawn, attacker death, and pool return clear active attack state; ordinary cancellation retains committed cooldown while death/return resets it. | Pass |
| Executor bindings | Missing, duplicate, incompatible, fixed-AI multi-binding, and Player Projectile/Grenade/Hitscan switching cases are asserted without controller replacement. | Pass |
| Scope audit | No scene, production prefab, ScriptableObject asset, input asset, balance value, package, or design-document checkbox was changed. | Pass |

The definitive suite ran through the project-local verification command in the verified correct target Editor. `Logs/ImplementationEditModeSummary.txt` supplied the final totals, and the final Unity log segment was scanned for compiler, assembly, unhandled-exception, and assertion diagnostics.

### Explicit Structural Choices

- Scan capacity, interval, and initial stagger offset are mandatory caller-supplied configuration; Step 7 adds no production scan or balance defaults.
- Equal-distance candidates retain the Step 4 deterministic rule: the lower valid spawn ID wins.
- A unit without an `AttackDefinition` is explicitly non-attacking and requires no executor. Fixed AI units with an attack bind exactly one executor; the Player may bind each supported selectable delivery once.
- Player definition switching is transactional: an invalid or unbound definition leaves the prior definition and executor intact.
- Attack sequences restart their local sequence counter after pool reset, while the composite source `SpawnId` makes the resulting `AttackKey` unique across spawns.
- Cooldown advances while an active unit is stunned, but a stun cancels any current windup and does not refund the already committed cooldown.
- A melee target that leaves attack range at impact produces no executor call and enters recovery. Actual melee, projectile, grenade, and hitscan delivery behavior remains Step 8 scope.

## Step 8 - Attack Delivery and Projectile Systems

**Status:** Complete
**Completed:** 2026-08-09
**Commit summary:** `Step 8 completed: add attack delivery and projectiles`
**Next step:** Step 9 is complete; Step 10 has not been started.

### Implemented

- Added `AttackPayloadFactory` so every delivery snapshots the source spawn ID, faction, attack sequence, damage, damage category, and optional accepted-hit effect from one immutable attack context.
- Added `MeleeAttackExecutor`, with target-specific hit position/normal diagnostics and all damage routed through the existing `InteractionSystem` and current attack ledger.
- Added `ProjectileController` as the common pooled projectile lifecycle, captured-request owner, timer, reusable per-projectile ledger, interaction boundary, termination event, and return-to-`SpawnManager` path.
- Added `KinematicProjectileMovement` for bullet/fireball swept-sphere motion against only `World` and `UnitTarget`, including nearest relevant contact selection, explicit trigger inclusion, saturation diagnostics, lifetime expiry, and pool return.
- Added `ProjectileAttackExecutor` and `GrenadeAttackExecutor`, both spawning only through `SpawnManager` with an explicitly supplied `InteractionSystem` runtime context.
- Added `GrenadeProjectileMovement` with Rigidbody launch/reset, authored gravity scale, fuse timing, classified World collision, classified hostile trigger contact, non-allocating area resolution, spawn-ID hurtbox deduplication, and one shared explosion ledger.
- Added `HitscanAttackExecutor` with non-allocating first-relevant-hit selection, World obstruction, captured-payload interaction, source/friendly/inactive/dead filtering, and a pooled beam placeholder.
- Added `LaserBeamPresentationController` with inactive configuration, pooled setup, authored `0.12 s` lifetime, visual span placement, expiry, and return.
- Extended `SpawnManager` with an overload that supplies explicit runtime projectile services before inactive configuration while preserving the Step 6 request-only overload and test lifecycle seam.
- Added Unity Editor automation that idempotently creates or updates Bullet, Fireball, Grenade, and LaserBeam prefabs; three projectile definitions; five attack definitions; and four Expandable pool entries while preserving later catalog entries.
- Used the exact temporary sandbox speeds, radii, lifetimes/fuse, explosion radius, damage/range/timing values, and prewarm/retention baselines from the design table.
- Added Edit Mode delivery, pooling, physics-policy, captured-source, concrete-asset, and asset-tuning tests.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Correct-project compilation | Unity `6000.5.5f1` compiled the final code batch; the Tundra build succeeded in `Editor.log` at line 11558 and completed its assembly reload. | Pass |
| Compiler/assembly diagnostics | The final log segment from line 11558 contains 0 C# errors, C# warnings, script-compilation failures, unhandled exceptions, assertion exceptions, or assembly-resolution failures. | Pass |
| Unity asset automation | The rerunnable Step 8 Editor command created/updated and verified all definitions, prefabs, and pool entries at `Editor.log` line 11686 without hand-authored YAML or GUIDs. | Pass |
| Edit Mode suite | 212 total, 212 passed, 0 failed, 0 skipped, 0 inconclusive. This includes 25 Step 8 cases and all 187 earlier cases. | Pass |
| Melee and direct projectiles | Melee applies once at impact; Bullet and Fireball hostile hits, expiry, pool return, source overlap, friendly pass-through, and World obstruction are asserted. | Pass |
| Grenade behavior | Rigidbody velocity reset, source/friendly trigger ignore, hostile trigger detonation, World collision, fuse expiry, area damage, and multiple-hurtbox deduplication are asserted. | Pass |
| Hitscan and beam | Friendly pass-through, first-hostile-only resolution, World obstruction, single interaction, beam rent/configuration, and timed beam return are asserted. | Pass |
| Captured source state | Tests prove in-flight damage remains valid after source death and after the source object receives a new spawn ID and faction, without reading that object's new state. | Pass |
| Friendly-fire matrix | Melee and Hitscan reject friendly impact; Bullet and Fireball ignore friendly hurtboxes without consumption; Grenade area/contact rules ignore friendlies. | Pass |
| Authored asset data | All projectile, attack, and pool values are loaded back through `AssetDatabase` and compared with the temporary sandbox table; required pooled component structures are inspected. | Pass |
| Scope audit | No scene, unit prefab, input asset, package, or design-document checkbox was changed. | Pass |

The definitive suite ran after a second successful idempotent asset-automation pass in the verified correct target Editor. `Logs/ImplementationEditModeSummary.txt` supplied the totals, and the final Unity log segment was scanned for compiler, assembly, unhandled-exception, and assertion diagnostics.

### Explicit Structural Choices

- A successful Projectile or Grenade executor returns the default `InteractionResult` (`None`) because firing has not yet produced a target interaction. The pooled delivery publishes its real applied/rejected result only at impact through `InteractionSystem`; immediate spawn failure returns `InvalidPayload`.
- Projectile runtime services are passed explicitly by the executor through a `SpawnManager` overload. They are not serialized into reusable prefab assets and are cleared on pool return.
- Fixed non-allocating safeguards are explicit implementation capacities rather than combat tuning: Bullet/Fireball sweeps and Hitscan casts hold 32 contacts; Grenade area queries hold 64 colliders. Each system exposes saturation for diagnostics.
- Exact-distance collision ties prefer World over a hostile target, ensuring an obstruction is never bypassed by nondeterministic physics-hit ordering.
- Source, friendly, inactive, and dead contacts are ignored. An active/alive hostile remains a consuming direct contact even if `InteractionSystem` later rejects damage for invulnerability.
- Grenade Rigidbody velocity and angular velocity are cleared both before a spawn and during pool return. Authored gravity scale `1` uses Unity gravity directly; non-unit values remain definition-owned.
- Beam-presentation pool failure does not roll back an already resolved Hitscan interaction. The placeholder uses a non-gameplay `0.04` visual thickness and the authored `0.12 s` lifetime.
- The Step 8 catalog automation replaces its own four entries on rerun but preserves entries owned by later steps, so future unit/effect pools are not erased.

## Step 9 - Common Unit Prefab and Combat Sandbox

**Status:** Complete
**Completed:** 2026-08-09
**Commit summary:** `Step 9 completed: add common unit prefab and sandbox`
**Next step:** Step 10 has not been started.

### Implemented

- Added `PF_Unit_Base` as the shared unit composition prefab with the required unit, health, damage, status-effect, targeting, attack, lifecycle, and pooled-entity components.
- Added the `VisualRoot`, `Sockets`, `UIAnchor`, and `Debug` hierarchy, including `Muzzle`, `WeaponGrip`, `CastOrigin`, and `HeadTop` sockets.
- Added a child `Hurtbox` trigger on the `UnitTarget` layer with `DamageTargetProxy`, plus a non-gameplay root body collider on `UnitBody`.
- Persisted explicit targeting query capacity and scan scheduling on the prefab and made inactive pooled initialization reconstruct the runtime query buffer before activation.
- Added a stationary, non-attacking test-only prefab variant and Player definition under `Assets/Tests/Fixtures/StepNine`.
- Added a lifecycle adapter for the placeholder fixture that requests pooled return immediately after death without coupling health, damage, or lifecycle code to `PoolManager`.
- Added `UC_CombatSandbox` and extended the existing pool catalog with the stationary fixture while preserving all Step 8 projectile/beam entries.
- Added `CombatSandboxBootstrap` with explicit serialized references and fail-closed validation for catalogs, services, spawn groups, fixture membership, and required physics layers.
- Added Unity Editor automation that idempotently creates and validates the common prefab, prefab variant, fixture definition, catalogs, scene hierarchy, and baked NavMesh without hand-authored Unity YAML or GUIDs.
- Added `CombatSandbox` with the required `__Systems`, `Environment`, `SpawnPoints`, `CameraRig`, `UI`, and `Lighting` roots; World-layer ground and obstacles; baked `NavMeshSurface`; pool/spawn/interaction/registry/bootstrap services; and Player/Ally/Enemy spawn groups.
- Added Edit Mode coverage for prefab capability and hierarchy matrices, persisted targeting data, variant inheritance, catalogs, scene structure/NavMesh, fail-closed bootstrap, and the full stationary spawn/damage/death/return/respawn cycle through production services.

### Verification Evidence

| Check | Evidence | Result |
| --- | --- | --- |
| Correct-project compilation | Unity `6000.5.5f1` compiled the final code batch; the Tundra build succeeded in `Editor.log` at line 14015 and assemblies reloaded successfully. | Pass |
| Compiler/assembly diagnostics | The final compile and Play Mode log segments contain 0 C# errors, C# warnings, script-compilation failures, unhandled exceptions, or missing-reference exceptions. | Pass |
| Unity asset automation | The Step 9 Editor command created and verified the prefabs, definition, catalogs, `CombatSandbox`, and baked NavMesh in the correct target project. | Pass |
| Edit Mode suite | 220 total, 220 passed, 0 failed, 0 skipped, 0 inconclusive. This includes 8 Step 9 cases and all 212 earlier cases. | Pass |
| Actual Play Mode startup | Entering Play Mode with `CombatSandbox` active initialized the pool and spawn services and spawned the stationary fixture; the bootstrap success signal is recorded at `Editor.log` line 14384. | Pass |
| Production lifecycle | A stationary unit spawns from its pool, takes lethal damage through `InteractionSystem`, unregisters and returns immediately, then reuses the same object with a new spawn ID, full health, and cleared status/target/attack state. | Pass |
| Prefab and scene matrix | Tests load the saved Unity assets and assert all required components, hierarchy nodes, layers, sockets, scene roots, services, spawn groups, World geometry, and baked NavMesh data. | Pass |
| Dependency audit | Runtime code has no scene-wide object discovery and no manager/service singleton. The only static `Instance` is a private stateless registry comparer. | Pass |
| Scope audit | `SampleScene` is unchanged. No input asset, package, design document, or design-document checkbox was modified. | Pass |

The definitive Edit Mode suite ran through the project-local verification command in the verified correct target Editor. Actual Play Mode was entered and exited through the live Editor with `CombatSandbox` active; its successful service initialization and fixture spawn were confirmed from the target project's Editor log.

### Explicit Structural Choices

- The Step 9 fixture is deliberately a prefab variant of `PF_Unit_Base`, so the common capability matrix remains inherited while the test-only death-return adapter and placeholder capsule remain outside the production base prefab.
- The common prefab includes no motor and no attack executor at this step. Those are concrete-unit responsibilities introduced in Steps 10 through 13; a null attack definition is the explicit stationary/non-attacking state.
- Bootstrap dependencies are wired explicitly in the saved scene. Failure of any required reference, catalog, definition, spawn group, layer, pool initialization, or initial spawn disables gameplay as one coherent sandbox failure rather than allowing partial startup.
- Targeting query capacity `32`, scan interval `0.25 s`, and initial offset `0 s` are the temporary sandbox values already specified by the implementation design, not newly invented balance values.
- The fixture pool uses one prewarmed instance and one retained inactive instance so the required reuse cycle can be verified without adding a concrete Player/Ally/Enemy unit before their numbered steps.
