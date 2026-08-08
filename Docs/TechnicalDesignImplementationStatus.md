# Technical Design Implementation Status

This document records implementation progress and verification evidence without changing the design documents or their checkboxes.

## Step 0 — Project Baseline and Assembly Boundaries

**Status:** Complete  
**Completed:** 2026-08-08  
**Next step:** Step 1 has not been started.

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
