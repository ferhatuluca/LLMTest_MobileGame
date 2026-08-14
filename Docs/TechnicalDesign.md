# Monsters vs Zombies - Technical Design

## Purpose

The project is a Unity combat sandbox. Pressing Play in `CombatSandbox` starts a Player, a stationary Enemy target, and the scene services needed to spawn and fight Player, Ally, and Enemy units.

The design favors direct Unity composition and readable gameplay code. New interfaces, policies, adapters, or manager layers should be introduced only when two real implementations need the same boundary.

## Gameplay rules

- Player and Ally units can affect Enemy units.
- Enemy units can affect Player and Ally units.
- Health is clamped between zero and maximum health; death happens once per spawn.
- Player weapons cycle through Pistol, GrenadeGun, and SpaceGun.
- AI units acquire hostile targets, chase when needed, and attack in range.
- Stunner applies stun on successful hits 1, 4, 7, and so on.
- Divisible spawns three MiniDivisible units when it dies.
- Units and projectiles are returned to pools and reset before reuse.

## Runtime structure

### Scene services

`CombatSandboxBootstrap` owns startup. Its serialized references initialize:

- `PoolManager` for prefab pools.
- `SpawnManager` for unit and projectile creation/return.
- `InteractionSystem` for faction checks and damage dispatch.
- `UnitRegistry` for currently active units.
- Player camera and HUD bindings.

These are scene-owned services. They do not use service locators or static singleton instances.

### Units

`UnitController` is the shared composition root. The main sibling components remain focused:

- `HealthController` owns current health and death notification.
- `DamageController` receives accepted hit contexts.
- `StatusEffectController` owns stun state.
- `TargetingController` finds and retains hostile targets.
- `AttackController` owns cooldown, windup, impact, and recovery.
- `UnitLifecycleController` coordinates spawn, death, and pool return.
- `PlayerMotor` or `NavMeshUnitMotor` owns movement.
- `AIUnitBrain` makes local idle/chase/attack decisions.

Do not split these components further unless a concrete feature makes the current responsibility unclear.

### Combat flow

1. `TargetingController` selects a hostile active unit.
2. `AttackController` starts an attack when cooldown and range allow it.
3. The selected executor delivers melee, projectile, grenade, or hitscan impact.
4. `InteractionSystem` rejects invalid faction, self, inactive, dead, invulnerable, or duplicate hits.
5. `DamageController` forwards accepted damage to `HealthController` and accepted status effects to `StatusEffectController`.
6. Death is handled by `UnitLifecycleController`; special death behavior runs before pool return.

`FactionRules`, `CombatRangeRules`, `HealthState`, `AttackHitLedger`, `WeaponIndexCycle`, `StunnerHitSchedule`, and `MiniDivisibleSpawnFormation` contain deterministic rules that can be tested without entering Play Mode.

### Data

ScriptableObject definitions hold authored configuration:

- `PlayerUnitDefinition` and `AIUnitDefinition` describe units.
- `AttackDefinition` describes timing, range, damage, and delivery.
- `WeaponDefinition` maps Player weapon selection to attacks.
- `ProjectileDefinition` describes projectile movement and pool identity.
- `UnitCatalog` and `PoolCatalog` map stable IDs to definitions and prefabs.

Runtime state never belongs in these assets.

### Pooling and spawning

Each configured pool owns an inactive stack and an active set. Rent pops an inactive instance or creates one. Return resets the entity, deactivates it, and pushes it back unless the inactive retention limit is full.

`PooledEntity` invokes `IPoolable` callbacks in three phases:

1. Inactive preparation.
2. Activation-dependent completion.
3. Return cleanup.

`SpawnManager` assigns a new `SpawnId` for every unit spawn and returns partial spawns immediately if initialization fails.

## Assets and scenes

- Production scripts: `Assets/Scripts/Runtime`
- EditMode tests: `Assets/Scripts/Tests/EditMode`
- PlayMode tests: `Assets/Scripts/Tests/PlayMode`
- Combat fixtures: `Assets/Tests/Fixtures/Combat`
- Main scene: `Assets/Scenes/CombatSandbox.unity`
- Unit prefabs: `Assets/Prefabs/Units`
- Definitions and catalogs: `Assets/Data`

One-off setup generators and numbered implementation-step verifiers are not part of the maintained project.

## Testing strategy

### EditMode

Use EditMode for fast deterministic behavior:

- faction and planar-range rules;
- health boundaries and reset;
- per-attack duplicate-hit protection;
- weapon cycling;
- Stunner cadence;
- MiniDivisible formation;
- definition, catalog, and pooled-prefab validation.

Tests are grouped by gameplay domain, not by implementation order.

### PlayMode

Use PlayMode only when Unity runtime behavior matters. Load the saved `CombatSandbox` scene and verify:

- bootstrap creates an active Player and Enemy target;
- hostile damage, friendly-fire rejection, and duplicate-hit rejection work through production components;
- returned units respawn with full health and a new spawn identity;
- a spawned Ally acquires an Enemy through production targeting.
- Player weapon selection cycles through all three configured deliveries.
- Divisible death creates exactly three MiniDivisible units.

Avoid tests that merely repeat prefab hierarchy or scene-YAML structure. Validate behavior through the public runtime result whenever possible.

## Acceptance criteria

- `CombatSandbox` compiles and starts without gameplay exceptions.
- Player and Enemy initial units are active and registered.
- hostile damage and friendly-fire rules are enforced centrally.
- pooled units reset health and identity.
- Player weapons, AI targeting, stun cadence, and divide-on-death behavior remain supported.
- all EditMode and PlayMode tests pass.

## Simplicity rules

- Prefer one direct method call over an interface used by one implementation.
- Prefer serialized scene references over discovery frameworks.
- Prefer a small pure rule class when logic is independent of Unity.
- Do not keep implementation generators after their assets exist.
- Do not add a test because a file or implementation step exists; add it because a meaningful behavior can regress.
- Keep diagnostics outside gameplay update paths unless the diagnostic is actively used during normal development.
