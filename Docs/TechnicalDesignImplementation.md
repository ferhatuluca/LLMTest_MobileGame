# Monsters vs Zombies - Implementation Guide

This guide describes how to change the maintained project. It is intentionally organized by feature responsibility rather than numbered construction steps.

## Before changing C#

Read:

- `Docs/CodeNameConventions.md`
- `Docs/CodingPreferences.md`

Keep serialized prefab and scene compatibility in mind before deleting or renaming a `MonoBehaviour`.

## Change workflow

1. Identify the gameplay behavior and its current owner.
2. Put deterministic math or rules in a plain C# type when Unity APIs are not needed.
3. Change the smallest runtime component that owns the behavior.
4. Add or update a focused EditMode test for pure behavior.
5. Add or update a PlayMode test only when scene, physics, lifecycle, input, NavMesh, or pooling behavior is involved.
6. Compile the project, run EditMode, then run PlayMode.
7. Verify `CombatSandbox` still starts successfully.

## Adding a unit

1. Reuse an existing attack delivery when possible.
2. Create the unit definition and any required attack/projectile definitions.
3. Create a concrete prefab variant from the appropriate Player, Ally, or Enemy base.
4. Add the unit definition to `UC_CombatSandbox`.
5. Add the prefab pool to the pool catalog.
6. Add a developer spawn path if the unit needs manual sandbox testing.
7. Test only new rules and one representative runtime flow.

Do not create a new controller, policy interface, setup generator, or test fixture solely because the unit is new.

## Adding attack behavior

Use the existing attack flow:

- timing stays in `AttackController`;
- delivery stays in an `IAttackExecutor` implementation;
- legality stays in `InteractionSystem`;
- target-side changes stay in `DamageController`, `HealthController`, or `StatusEffectController`.

Add another executor only for a genuinely different delivery mechanism. Balance differences belong in definitions.

## Pooling checklist

Every pooled component must clear per-spawn state on return and restore required state during preparation/completion. Important reset state includes:

- health and death flags;
- current target and attack timing;
- status effects;
- AI path and state;
- projectile payload, motion, ledger, and lifetime;
- special-unit counters and death-spawn flags.

Use the PlayMode pool-reuse test as the baseline regression check.

## Test placement

- `EditMode/Combat`: deterministic combat rules and state.
- `EditMode/Data`: authored configuration and catalog validity.
- `PlayMode`: saved-scene runtime integration.
- `Assets/Tests/Fixtures/Combat`: only assets that are required by the sandbox or a runtime integration test.

Never name a test after an implementation step. Test names should state an action and expected outcome.

Do not add lifecycle relays, executor binding tables, policy interfaces, or spawn-context receivers for a single concrete behavior. Add a shared boundary only after a second real implementation needs it.

## Verification commands

Run Unity Test Framework EditMode and PlayMode suites. In automated environments use an isolated project copy when the project is already open in the Editor.

The required green baseline is currently:

- 29 EditMode tests.
- 6 PlayMode tests.
