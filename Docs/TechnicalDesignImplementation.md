# Monsters vs Zombies - Technical Design Implementation Order

## 1. Purpose

This document is the ordered implementation checklist for [TechnicalDesign.md](TechnicalDesign.md). It defines what to create first, what depends on it, and what must work before moving to the next step.

Use [GameDesign.md](GameDesign.md) for gameplay requirements and `TechnicalDesign.md` for system responsibilities. This file should remain focused on implementation order rather than repeating the full architecture.

All C# implementation must follow [CodeNameConventions.md](CodeNameConventions.md) and [CodingPreferences.md](CodingPreferences.md).

## 2. Rules for Following This Order

- Complete the exit check for a step before starting the next step.
- Add tests in the same step as the behavior they protect. Do not leave all tests until the end.
- Do not create production unit prefabs before their required components and lifecycle contracts compile and pass tests.
- Create ScriptableObject class definitions before creating assets that use them.
- Create a concrete prefab, its definition asset, its pool entry, and its catalog entry together. Do not leave partially registered content.
- Use placeholder meshes and animations until all behavior works. Visual polish must not block system testing.
- Keep `SampleScene` unchanged until `CombatSandbox` satisfies the final acceptance check.
- Do not add a global GameState system. The sandbox starts directly from `Awake` and `Start`.
- Do not continue while the Console has compilation errors or recurring exceptions.

Production unit prefab work begins at Step 9. Steps 0 through 8 establish the code and tests that those prefabs need.

## 3. Order Summary

| Step | Deliverable | Depends on |
| --- | --- | --- |
| 0 | Project folders, assemblies, tests, and required layers | Existing Unity project |
| 1 | Shared identifiers and pure gameplay rules | Step 0 |
| 2 | ScriptableObject definition and catalog classes | Step 1 |
| 3 | Core unit health, damage, status, and lifecycle components | Steps 1-2 |
| 4 | Unit registry and interaction system | Step 3 |
| 5 | Pool manager and pool lifecycle | Steps 2-4 |
| 6 | Spawn manager and spawn requests | Step 5 |
| 7 | Targeting and attack timing | Steps 4 and 6 |
| 8 | Melee, projectile, grenade, and hitscan delivery | Step 7 |
| 9 | Common unit prefab and minimum combat sandbox | Steps 3-8 |
| 10 | Player movement, weapons, and Player prefab | Step 9 |
| 11 | AI movement, behavior, and faction base prefabs | Step 9 |
| 12 | Regular Ally and Enemy concrete prefabs | Steps 8 and 11 |
| 13 | Stunner, Divisible, and MiniDivisible | Step 12 |
| 14 | Debug spawn controls, HUD, and gizmos | Steps 10-13 |
| 15 | Full validation and automated test coverage | Steps 0-14 |
| 16 | Stress profiling and final sandbox acceptance | Step 15 |

## Step 0: Prepare the Project Structure

### Create

- [ ] Confirm the project opens in Unity `6000.5.5f1` without compilation errors.
- [ ] Confirm Input System, AI Navigation, Unity Test Framework, and URP packages resolve correctly.
- [ ] Create the runtime folders described in `TechnicalDesign.md` under `Assets/Scripts/Runtime`.
- [ ] Create `Assets/Scripts/Editor`.
- [ ] Create `Assets/Scripts/Tests/EditMode` and `Assets/Scripts/Tests/PlayMode`.
- [ ] Create data, prefab, and test-fixture folders under `Assets`.
- [ ] Create `MonstersVsZombies.Runtime.asmdef`.
- [ ] Create `MonstersVsZombies.Editor.asmdef` referencing Runtime and restricted to the Editor.
- [ ] Create Edit Mode and Play Mode test assembly definitions referencing Runtime and Unity test assemblies.
- [ ] Add the `World`, `UnitBody`, `UnitTarget`, and `Projectile` physics layers.
- [ ] Confirm the project uses the new Input System in Project Settings.

### Verify

- [ ] Runtime code can be referenced by both test assemblies.
- [ ] Editor code is excluded from player builds.
- [ ] An empty Edit Mode test and an empty Play Mode test both run successfully.
- [ ] Unity recompiles with no warnings caused by assembly references.

### Exit check

The folder and assembly structure is stable, and both test modes can run before gameplay code is added.

## Step 1: Create Shared Identifiers and Pure Rules

### Create

- [ ] Create `UnitFaction` with Player, Ally, and Enemy values.
- [ ] Create the stable unit, attack, pool, and per-spawn identity types required by the technical design.
- [ ] Create result and reason types for interactions, damage, pooling, and spawning.
- [ ] Create immutable `DamagePayload`, `HitContext`, `DamageResult`, and `InteractionResult` value types.
- [ ] Create `FactionRules` as the only faction-hostility rule implementation.
- [ ] Create the plain C# `HealthState` model.
- [ ] Create a plain weapon-index cycling rule for previous and next wrapping.
- [ ] Create a plain Stunner hit schedule that identifies hits 1, 4, 7, and onward.

### Test now

- [ ] Test all Player, Ally, and Enemy attacker-target combinations.
- [ ] Test health initialization, damage, healing, clamping, overkill, death once, and reset.
- [ ] Test previous and next weapon wrapping.
- [ ] Test Stunner cadence, rejected hits, misses, and reset.
- [ ] Test equality and uniqueness behavior for IDs used in duplicate-hit protection.

### Exit check

All shared rules are independent of scenes and prefabs, and all Edit Mode tests pass.

## Step 2: Create Configuration and Catalog Scripts

Create the ScriptableObject classes in this step, but do not build all concrete game assets yet.

### Create

- [ ] Create `UnitDefinition`.
- [ ] Create `AttackDefinition`.
- [ ] Create `WeaponDefinition`.
- [ ] Create `ProjectileDefinition`.
- [ ] Create `PoolCatalog` and its pool-entry data type.
- [ ] Create `UnitCatalog` and its unit-entry data type.
- [ ] Create `SandboxSpawnConfiguration`.
- [ ] Add `OnValidate` checks that do not require prefab inspection.
- [ ] Create reusable validation results so the future Editor validation command and tests use the same rules.

### Test now

- [ ] Test duplicate unit IDs and pool IDs.
- [ ] Test missing required references.
- [ ] Test zero or negative health, damage, range, speed, lifetime, and pool sizes.
- [ ] Test AI attack range greater than chase range.
- [ ] Test projectile attacks without projectile definitions.

### Exit check

Definition scripts compile and invalid data produces clear validation results. Only small test assets should exist at this point.

## Step 3: Create the Core Unit Components

Implement the shared unit state before interaction, attacks, AI, Player input, or production prefabs.

### Create in this order

- [ ] Create `UnitController` for definition, faction, spawn identity, active state, and cached component references.
- [ ] Cache and validate only the core sibling components implemented in this step; do not create temporary targeting or attack component placeholders.
- [ ] Create `HealthController` as the Unity adapter around `HealthState`.
- [ ] Create `StatusEffectController` with the stun state and action-block properties.
- [ ] Create `DamageController` as the target-side damage entry point.
- [ ] Create `UnitLifecycleController` for inactive, active, dying, and pool-return transitions.
- [ ] Define `IUnitMotor` so Player and AI movement implementations share the same stop, resume, move, and facing boundary.
- [ ] Define the pool and spawn lifecycle interfaces needed by these components without depending on a concrete `PoolManager` yet.
- [ ] Add health, damage, death, status, spawn, and despawn events.
- [ ] Ensure external code can read state through properties but cannot mutate health or lifecycle fields directly.

### Test now

- [ ] Construct units from GameObjects inside tests rather than creating production prefabs.
- [ ] Verify damage reaches `HealthController` only through `DamageController`.
- [ ] Verify death fires once and changes lifecycle state.
- [ ] Verify dead units reject later damage and status effects.
- [ ] Verify stun blocks move and attack permissions, refreshes correctly, and expires.
- [ ] Verify spawn reset restores health, alive state, timers, and event state.

### Exit check

A programmatically created unit can initialize, take damage, become stunned, die once, and reset without needing a prefab, pool, scene manager, or AI.

## Step 4: Create Unit Registration and Interaction

### Create in this order

- [ ] Create `UnitRegistry` with registration, removal, faction counts, and spawn-ID lookup.
- [ ] Register units only when their current spawn becomes logically active.
- [ ] Remove units from the registry before they become targetable as another pooled spawn.
- [ ] Create `DamageTargetProxy` for hurtbox colliders.
- [ ] Create `InteractionSystem` faction, self-hit, active-state, alive-state, and duplicate-hit validation.
- [ ] Add reusable non-allocating target and area query buffers.
- [ ] Add deterministic nearest-target tie handling using spawn IDs.
- [ ] Return a specific `InteractionResult` for every rejection and accepted hit.

### Test now

- [ ] Verify Player and Ally can damage Enemy.
- [ ] Verify Enemy can damage Player and Ally.
- [ ] Verify every friendly or same-faction interaction is rejected.
- [ ] Verify self-hits and duplicate hits from one attack sequence are rejected.
- [ ] Verify a later attack sequence can hit the same target again.
- [ ] Verify dead, dying, inactive, and pooled units cannot be targeted.
- [ ] Verify units register and unregister exactly once per spawn.

### Exit check

Two stationary test units can resolve legal and illegal hits through `InteractionSystem`, and no caller except `DamageController` mutates health.

## Step 5: Create PoolManager

### Create in this order

- [ ] Create `IPoolable` and `PooledEntity`.
- [ ] Create the internal pool wrapper around Unity `ObjectPool<T>`.
- [ ] Create `PoolManager` initialization and pool lookup by stable pool ID.
- [ ] Implement prewarming while instances remain inactive.
- [ ] Implement the exact spawn and return order from `TechnicalDesign.md`.
- [ ] Implement collection checks for Editor and Development builds.
- [ ] Add created, active, inactive, peak-active, and failed-rent diagnostics.
- [ ] Add a controlled response for missing pool IDs and invalid pooled prefabs.
- [ ] Keep the pool reset contract generic. Each unit, Rigidbody, NavMesh, particle, and trail component added later owns its own reset implementation; `PoolManager` must not search for concrete component types.

### Test now

- [ ] Use a programmatically created or test-only pooled fixture; do not create the production unit hierarchy yet.
- [ ] Verify prewarm counts.
- [ ] Verify rent, activate, return, and reuse order.
- [ ] Verify a returned object is inactive.
- [ ] Verify the same object can be reused with a new spawn ID and clean state.
- [ ] Verify double-return and unknown-pool errors are detected.
- [ ] Verify maximum retained count behavior.

### Exit check

Pool reuse is proven independently of final unit and projectile prefabs.

## Step 6: Create SpawnManager and Spawn Requests

### Create in this order

- [ ] Create `UnitSpawnRequest` and projectile spawn request types.
- [ ] Create `SpawnManager` using `PoolManager`; it must not instantiate an unpooled fallback.
- [ ] Assign a new spawn ID for every unit spawn.
- [ ] Apply pose and spawn context before activation.
- [ ] Register units only after initialization succeeds.
- [ ] Create `SpawnPointGroup` with deterministic and round-robin selection.
- [ ] Add NavMesh position validation as an optional path used later by AI units.
- [ ] Define the `InitialSandboxSpawner`, `DebugUnitSpawner`, and death-spawn entry points, but defer their UI and concrete-unit wiring.

### Test now

- [ ] Verify a spawn request selects the expected pool.
- [ ] Verify an invalid pool or definition returns a clear failure.
- [ ] Verify position, rotation, definition, faction, and spawn ID are assigned before activation.
- [ ] Verify unit registry counts change on spawn and return.
- [ ] Verify round-robin spawn points are deterministic.
- [ ] Verify a returned instance receives a different spawn ID when spawned again.

### Exit check

Tests can request, observe, return, and respawn a generic unit through `SpawnManager` and `PoolManager` without direct instantiation.

## Step 7: Create Targeting and Attack Timing

### Create in this order

- [ ] Create `TargetingController` using `InteractionSystem` target-query rules.
- [ ] Implement current-target validation, nearest-target acquisition, target loss, and target events.
- [ ] Support AI chase-range queries and Player attack-range-only queries.
- [ ] Stagger full target scans and use cheap current-target checks between scans.
- [ ] Define `IAttackExecutor`.
- [ ] Create `AttackController` with cooldown, windup, impact, recovery, cancellation, and sequence IDs.
- [ ] Create `AttackAnimationEventRelay` so placeholder timing and later animation events call the same impact method.
- [ ] Feed successful interaction results back to attack policies.
- [ ] Update `UnitController` component caching and validation to include the completed targeting and attack components.

### Test now

- [ ] Verify nearest hostile target selection.
- [ ] Verify dead, inactive, friendly, and out-of-range candidates are ignored.
- [ ] Verify deterministic selection at equal distance.
- [ ] Verify Player targeting never requests movement.
- [ ] Verify one attack sequence creates only one impact per target.
- [ ] Verify cooldown and cancellation on stun, target loss, death, and despawn.
- [ ] Verify a melee-style impact rechecks range at impact time.

### Exit check

A stationary attacker can acquire a stationary hostile target, run attack timing, and generate one validated impact through a test executor.

## Step 8: Create Attack Delivery and Projectile Systems

Create each delivery type separately and route all of them through `InteractionSystem`.

### Create in this order

- [ ] Create `MeleeAttackExecutor`.
- [ ] Create the common projectile lifecycle and captured-payload initialization.
- [ ] Create kinematic swept movement for bullets and fireballs.
- [ ] Create `ProjectileAttackExecutor`.
- [ ] Create Rigidbody-based grenade movement, fuse, collision, and area resolution.
- [ ] Create `GrenadeAttackExecutor`.
- [ ] Create `HitscanAttackExecutor` for the SpaceGun.
- [ ] Add a pooled laser-beam presentation placeholder.
- [ ] Add projectile lifetime expiry and pool return.
- [ ] Add per-explosion target deduplication for multiple hurtboxes.

### Create the first concrete non-unit assets

- [ ] Create bullet, fireball, grenade, and laser-beam placeholder prefabs only after their scripts compile.
- [ ] Create their `ProjectileDefinition` assets.
- [ ] Add their pool entries and prewarm values.
- [ ] Create initial attack definitions for melee, bullet, fireball, grenade, and hitscan tests.

### Test now

- [ ] Verify melee applies one interaction at the impact point.
- [ ] Verify bullet and fireball hit or expire and return to their pools.
- [ ] Verify grenade area damage applies once per unit even with multiple hurtboxes.
- [ ] Verify SpaceGun hits only the intended first valid target.
- [ ] Verify projectiles use the payload captured at fire time.
- [ ] Verify a projectile from a dead or recycled source cannot read the source's new state.
- [ ] Verify friendly fire is rejected for every delivery type.

### Exit check

All four delivery types work against stationary test units, are pooled, and share the same interaction and damage path.

## Step 9: Create the Common Unit Prefab and Minimum Sandbox

This is the first step that creates production unit prefab structure. All required common scripts now exist and have tests.

### Create `PF_Unit_Base`

- [ ] Add `UnitController`.
- [ ] Add `HealthController`.
- [ ] Add `DamageController`.
- [ ] Add `StatusEffectController`.
- [ ] Add `TargetingController`.
- [ ] Add `AttackController`.
- [ ] Add `UnitLifecycleController`.
- [ ] Add `PooledEntity`.
- [ ] Add the `Hurtbox` child on the `UnitTarget` layer with its trigger collider and `DamageTargetProxy`.
- [ ] Add `VisualRoot`, `Sockets`, `UIAnchor`, and Editor-only debug roots.
- [ ] Add AttackOrigin, WeaponSocket, RightHandSocket, and MouthSocket transforms.
- [ ] Validate required components and child references.

### Create the minimum `CombatSandbox` scene

- [ ] Create `Assets/Scenes/CombatSandbox.unity`.
- [ ] Add `__Systems`, Environment, SpawnPoints, CameraRig, UI, and Lighting roots.
- [ ] Add ground and basic obstacles on the World layer.
- [ ] Add and bake a `NavMeshSurface` even though AI is implemented later.
- [ ] Add `PoolManager`, `SpawnManager`, `InteractionSystem`, `UnitRegistry`, and `CombatSandboxBootstrap` scene objects.
- [ ] Add Player, Ally, and Enemy spawn-point groups.
- [ ] Create a test-only stationary unit variant for sandbox verification; do not use it as a gameplay prefab.
- [ ] Add required pool and unit catalog entries for the test fixture.

### Verify

- [ ] Pressing Play initializes services in the documented order.
- [ ] The stationary fixture can spawn, take damage, die, return, and respawn with clean state.
- [ ] No system uses `FindObjectOfType` or an unrelated singleton for dependency discovery.
- [ ] `SampleScene` remains unchanged.

### Exit check

The scene starts cleanly, the common prefab is stable, and the full health-to-pool lifecycle works through production scene services.

## Step 10: Create the Player Feature Slice

### Create input and movement first

- [ ] Replace the template gameplay action map with Move, PreviousWeapon, NextWeapon, and optional DebugAttack actions.
- [ ] Bind Move to WASD, arrow keys, and left-stick input.
- [ ] Bind PreviousWeapon to Q and NextWeapon to E.
- [ ] Remove or disable conflicting template bindings, especially E on Interact and 1/2 on Previous/Next.
- [ ] Create `PlayerInputReader` using `InputActionReference` fields.
- [ ] Create `PlayerMotor` using `CharacterController` and camera-relative XZ movement.
- [ ] Create `CameraFollowController`.
- [ ] Add an Input System `OnScreenStick` feeding the same Move action through left-stick input.

### Create Player combat

- [ ] Create `PlayerWeaponController` and ordered Pistol, GrenadeGun, and SpaceGun cycling.
- [ ] Create the Player auto-target and auto-attack behavior.
- [ ] Create weapon definition assets for all three weapons.
- [ ] Create placeholder nested weapon visual prefabs.
- [ ] Connect each weapon to the already tested projectile, grenade, or hitscan delivery path.

### Create prefabs and assets

- [ ] Create `PF_Unit_Player_Base` as a variant of `PF_Unit_Base`.
- [ ] Add `CharacterController`, Player input, Player motor, Player weapon, and Player combat components.
- [ ] Create `PF_Player` as the concrete Player variant.
- [ ] Create the Player `UnitDefinition`.
- [ ] Add the Player prefab and definition to pool and unit catalogs.
- [ ] Configure `InitialSandboxSpawner` to spawn the Player on Play.
- [ ] Bind the camera and minimum health/weapon HUD to the spawned Player.

### Test now

- [ ] Test weapon-cycle wrap in both directions through the Input System.
- [ ] Test that each weapon updates target range and attack definition.
- [ ] Test that Player targeting does not chase.
- [ ] Manually verify keyboard and on-screen stick movement.
- [ ] Manually verify Pistol, GrenadeGun, and SpaceGun against stationary Enemy test targets.
- [ ] Kill and reset the Player and verify input and weapon state are restored correctly.

### Exit check

Pressing Play immediately creates a controllable Player that can switch and use all three weapons against stationary test targets.

## Step 11: Create AI Movement and Faction Base Prefabs

### Create AI scripts first

- [ ] Implement the existing `IUnitMotor` contract for NavMesh movement.
- [ ] Create `NavMeshUnitMotor` as the only component that controls `NavMeshAgent` movement.
- [ ] Create `AIUnitBrain` with Idle, Chase, Attack, and Disabled local states.
- [ ] Implement chase-range and attack-range transitions.
- [ ] Limit destination refresh frequency and skip insignificant target movement.
- [ ] Reset paths on attack, stun, death, and pool return.
- [ ] Use `NavMeshAgent.Warp` during spawn positioning.
- [ ] Vary avoidance priority using spawn ID.

### Create prefab branches

- [ ] Create `PF_Unit_AI_Base` as a variant of `PF_Unit_Base`.
- [ ] Add `NavMeshAgent`, `NavMeshUnitMotor`, and `AIUnitBrain`.
- [ ] Create `PF_Unit_Ally_Base` as a variant of `PF_Unit_AI_Base`.
- [ ] Create `PF_Unit_Enemy_Base` as a variant of `PF_Unit_AI_Base`.
- [ ] Configure only faction defaults and debug presentation on faction bases; keep balance values in definitions.

### Create one thin test pair

- [ ] Create `PF_Test_AI_Ally` and `PF_Test_AI_Enemy` fixtures under the test assets folder using placeholder visuals.
- [ ] Create test-only definitions and catalog/pool entries for the fixtures.
- [ ] Do not treat these fixtures as the production Classic Melee prefabs created in Step 12.

### Test now

- [ ] Verify Ally targets only Enemy.
- [ ] Verify Enemy targets Player and Ally.
- [ ] Verify Idle to Chase, Chase to Attack, Attack to Chase, and target-loss transitions.
- [ ] Verify stun enters Disabled and expiry returns the AI to normal acquisition.
- [ ] Verify death and pool return clear path, target, and AI state.
- [ ] Verify a respawned agent is on the NavMesh and has no stale path.

### Exit check

The Player, one Ally, and one Enemy can move and fight correctly in the sandbox using base prefab variants.

## Step 12: Create Regular Concrete Unit Prefabs

Build one combat family at a time. Complete the repeated content checklist for each concrete unit before creating the next one.

### Repeated content checklist

- [ ] Create or reuse the nested visual prefab.
- [ ] Create the `UnitDefinition`.
- [ ] Create the concrete prefab variant from the correct faction base.
- [ ] Assign the correct attack executor, definition, model, collider size, and socket.
- [ ] Add the prefab to `PoolCatalog` with an initial prewarm value.
- [ ] Add the unit to `UnitCatalog`.
- [ ] Add one direct sandbox spawn path for immediate manual testing.
- [ ] Spawn, fight, die, return, and respawn the unit before moving on.

### Create in this order

1. [ ] Complete Ally Classic Melee and Enemy Classic Melee.
2. [ ] Create Ally Classic Range and Enemy Classic Range using bullet delivery.
3. [ ] Create Ally Dragon and Enemy Dragon using the shared Dragon visual and fireball delivery.
4. [ ] Create Ally DoubleHead using melee delivery and its wrist attack socket.

### Test after each family

- [ ] Melee units must close to melee attack range.
- [ ] Ranged units must stop at their longer effective range.
- [ ] Dragon fireballs must originate from MouthSocket.
- [ ] DoubleHead must use the Ally faction rules.
- [ ] Ally and Enemy versions of shared kinds must not damage the same faction.
- [ ] All concrete variants must reset correctly after pool reuse.

### Exit check

All non-special Ally and Enemy kinds can be spawned and fight with the correct range, delivery type, faction, and pooling behavior.

## Step 13: Create Special Enemy Units

### Create Stunner first

- [ ] Create `StunnerHitPolicy` using the tested hit schedule.
- [ ] Add stun payload creation only for the next required successful hit.
- [ ] Advance the counter only after an Applied interaction result.
- [ ] Create the hammer nested visual and configure RightHandSocket.
- [ ] Create the Stunner unit and attack definitions.
- [ ] Create `PF_Enemy_Stunner` from `PF_Unit_Enemy_Base`.
- [ ] Add pool, unit catalog, and sandbox spawn entries.
- [ ] Test hits 1, 4, 7, misses, rejected hits, death, and pool reset.

### Create MiniDivisible before Divisible

- [ ] Create the shared Divisible visual prefab.
- [ ] Create the MiniDivisible unit and melee attack definitions.
- [ ] Create `PF_Enemy_MiniDivisible` directly from `PF_Unit_Enemy_Base`.
- [ ] Apply the smaller scale and collider values.
- [ ] Confirm it has no divide-on-death component.
- [ ] Add its pool and unit catalog entries.
- [ ] Test MiniDivisible as a normal independent Enemy before using it as a death spawn.

### Create Divisible last

- [ ] Create `SpawnUnitsOnDeath`.
- [ ] Create and test the three radial spawn-position calculation.
- [ ] Add NavMesh sampling and a clear failed-position result.
- [ ] Create the Divisible unit and melee attack definitions.
- [ ] Create `PF_Enemy_Divisible` from `PF_Unit_Enemy_Base`.
- [ ] Add `SpawnUnitsOnDeath` pointing to MiniDivisible.
- [ ] Add its pool, unit catalog, and sandbox spawn entries.
- [ ] Verify one Divisible death produces exactly three MiniDivisibles once.
- [ ] Verify clearing, killing, and reusing Divisible never repeats a previous death spawn.

### Exit check

Stunner cadence and Divisible death spawning pass Edit Mode, Play Mode, and manual pool-reuse checks.

## Step 14: Complete Developer Controls and Diagnostics

### Create debug input

- [ ] Create the separate `SandboxDebug` input action map.
- [ ] Add F1 panel toggle.
- [ ] Add keys 1 through 9 for all concrete units in the order documented in `TechnicalDesign.md`.
- [ ] Add Backspace to clear non-Player units and active projectiles.
- [ ] Keep Q and E owned only by Player weapon switching.
- [ ] Enable debug actions only in the Editor or Development builds.

### Create the sandbox panel

- [ ] Add one spawn button per concrete unit.
- [ ] Add Spawn 10 controls.
- [ ] Add clear non-Player units and projectiles.
- [ ] Add reset Player.
- [ ] Add pause/resume AI decisions.
- [ ] Add spawn-at-cursor if it does not delay the required controls.
- [ ] Display current Player health and weapon.
- [ ] Display active units by faction.
- [ ] Display active, inactive, created, and peak pool counts.
- [ ] Display the last interaction result.

### Create diagnostics

- [ ] Add chase-range, attack-range, current-target, spawn-point, and faction gizmos.
- [ ] Add a target-query buffer-full warning.
- [ ] Add missing-pool, invalid-spawn-position, and invalid-definition diagnostics.
- [ ] Create the Editor validation command that scans concrete definitions and prefabs.

### Verify

- [ ] Every key and panel button spawns the expected unit.
- [ ] Spawn 10 uses normal `SpawnManager` requests.
- [ ] Clear returns objects through `PoolManager`; it does not destroy them.
- [ ] Debug controls and gizmos are absent from release builds.

### Exit check

A developer can exercise every unit and interaction without changing the scene or Inspector during Play Mode.

## Step 15: Complete Automated and Manual Validation

Most tests already exist from earlier steps. This step closes gaps and runs the complete system together.

### Complete Edit Mode coverage

- [ ] Faction rules
- [ ] Health state
- [ ] Damage rejection and results
- [ ] Weapon cycling
- [ ] Stunner cadence
- [ ] Target selection and deterministic ties
- [ ] Attack-sequence duplicate protection
- [ ] MiniDivisible spawn formation
- [ ] Catalog and definition validation

### Complete Play Mode coverage

- [ ] Ally-versus-Enemy accepted damage
- [ ] Friendly-fire rejection for every delivery type
- [ ] Full AI range transition behavior
- [ ] Stun stop and resume
- [ ] Divisible creates exactly three MiniDivisibles
- [ ] Pooled Stunner and Divisible reset correctly
- [ ] Projectile impact and lifetime pool return
- [ ] Grenade deduplication with multiple hurtboxes
- [ ] In-flight projectile remains safe after source death and reuse
- [ ] Q/E weapon switching and wrap
- [ ] Bootstrap initialization and immediate Player spawn

### Run manual acceptance

- [ ] Complete every item in `TechnicalDesign.md` section 17.3.
- [ ] Test each concrete unit individually.
- [ ] Test mixed groups of all factions and attack types.
- [ ] Test repeated clear, reset, respawn, stun, and death cycles.
- [ ] Confirm the Console remains free of exceptions and repeated warnings.
- [ ] Run the prefab and definition validation command with no unresolved errors.

### Exit check

All automated tests pass and the complete manual combat checklist has no known interaction, lifecycle, or pool-reset defects.

## Step 16: Profile and Finish the Sandbox Milestone

### Profile in this order

1. [ ] Run 10 Allies versus 10 Enemies.
2. [ ] Run 50 Allies versus 50 Enemies.
3. [ ] Run 100 Allies versus 100 Enemies as a diagnostic load.
4. [ ] Repeat a representative load in a Development build on the intended mobile device.

### Inspect and adjust

- [ ] Confirm steady combat has no recurring managed allocations from targeting, attacks, projectile movement, or pooling.
- [ ] Confirm prewarmed scenarios do not repeatedly instantiate or destroy gameplay objects.
- [ ] Inspect target-query buffer saturation.
- [ ] Inspect pool growth and peak projectile counts.
- [ ] Inspect NavMesh path and destination-update cost.
- [ ] Stagger target scans or adjust scan intervals if needed.
- [ ] Adjust pool prewarm counts from observed peaks.
- [ ] Record the profiling scene, unit counts, platform, and results.

### Final project setup

- [ ] Confirm `CombatSandbox.unity` starts immediately when it is the active Editor scene.
- [ ] Add `CombatSandbox.unity` to Build Settings for Development testing.
- [ ] Confirm there is no menu, loading gate, or global GameState dependency.
- [ ] Confirm keyboard and on-screen movement both remain functional.
- [ ] Confirm all spawn shortcuts, panel buttons, weapons, units, and special interactions remain functional after performance changes.
- [ ] Keep or remove `SampleScene` only after the team decides that `CombatSandbox` is the project starting scene.

### Exit check

Every acceptance criterion in `TechnicalDesign.md` section 19 is satisfied, the performance baseline is recorded, and the combat sandbox is ready for the next gameplay milestone.

## Adding Future Unit Kinds

When adding any new unit kind after this milestone, follow this smaller repeatable order:

1. Define or reuse its attack and projectile behavior.
2. Add pure rules and tests for any special behavior.
3. Implement the focused runtime behavior component.
4. Test the component without a production prefab where practical.
5. Create the unit and attack definition assets.
6. Create the concrete prefab variant from the correct faction base.
7. Add pool and unit catalog entries.
8. Add a sandbox spawn button and key only if one is available.
9. Add Edit Mode and Play Mode tests.
10. Spawn, fight, kill, return, and respawn it manually.

This order prevents new content from bypassing interaction rules, lifecycle reset, catalogs, or pooling.
