# Technical Design Implementation Status

This document records implementation progress and verification evidence without changing the design documents or their checkboxes.

## Step 0 — Project Baseline and Assembly Boundaries

**Status:** Complete  
**Completed:** 2026-08-08  
**Next step:** Steps 1 through 3 are complete; Step 4 has not been started.

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
**Next step:** Steps 2 and 3 are complete; Step 4 has not been started.

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
**Next step:** Step 3 is complete; Step 4 has not been started.

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
**Next step:** Step 4 has not been started.

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
