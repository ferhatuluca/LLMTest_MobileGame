# Technical Design Implementation Status

Last updated: 2026-08-14

## Current state

The combat sandbox is implemented and the codebase has been simplified from its step-generated form. A second structural pass reduced the runtime from 84 C# files / 12,432 lines to 78 files / about 10,160 lines.

### Removed

- All one-off Step 0-16 Editor setup and live-verification scripts.
- Three runtime scenario controllers used only by numbered verification steps.
- Custom per-frame allocation tracking and the standalone profiling runner.
- The extra Unity `ObjectPool` wrapper layer inside each runtime pool; pools now use a direct inactive stack.
- Physics query-buffer inheritance and saturation diagnostics; targeting and grenades use direct non-allocating Unity queries.
- Target acquire/lost event plumbing and AI state-change events; controllers validate current state in their normal update loops.
- Attack policy interfaces and serialized executor binding tables; executors are discovered from sibling components and Stunner behavior is explicit.
- Generic unit/projectile spawn-context receiver interfaces and the one-line initial-spawner component.
- Per-spawn subscription registration and one-off immediate-death-return components; lifecycle now unsubscribes directly and returns dead units automatically.
- The faction-guard component and stress-preset controller.
- Pool metrics that existed only for profiling; the developer panel keeps active, inactive, and created counts.
- All Step-named EditMode and PlayMode tests and their test-only probe scripts.
- Numbered fixture folders and the now-empty fixture test assembly.

### Retained

- The Player, Ally, Enemy, attack delivery, status, special-unit, spawning, pooling, basic diagnostics, HUD, and developer spawn controls.
- Existing prefab and ScriptableObject GUIDs used by catalogs and scenes.
- The saved `CombatSandbox` startup and production service graph.

### Test structure

EditMode tests now cover:

- faction and range rules;
- health behavior and pool reset state;
- hit-ledger duplicate protection;
- weapon cycling, Stunner cadence, and MiniDivisible formation;
- catalogs, definitions, and pool-prefab configuration.

PlayMode tests now cover:

- saved-scene bootstrap;
- hostile damage, friendly-fire rejection, and duplicate-hit rejection;
- return/respawn health and identity reset;
- Ally-to-Enemy target acquisition.
- Player weapon selection and executor-definition binding.
- Divisible death spawning exactly three MiniDivisible units.

## Verification evidence

- Unity batch compile: success, exit code 0.
- EditMode: 29 total, 29 passed, 0 failed, 0 skipped.
- PlayMode: 6 total, 6 passed, 0 failed, 0 skipped.

The retained logs are:

- `Logs/Simplify3Compile3.log`
- `Logs/Simplify3EditMode.log`
- `Logs/Simplify3EditMode.xml`
- `Logs/Simplify3PlayModeClean.log`
- `Logs/Simplify3PlayModeClean.xml`

## Maintenance direction

Future work should follow `TechnicalDesignImplementation.md`. Keep tests organized by behavior and use new abstractions only when current concrete code has more than one real implementation need.
