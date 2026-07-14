# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## The project lives in `./Momentum/`

The actual Unity game project is the **`./Momentum/` subfolder**, not the repo root. Open, build, and develop against `./Momentum/`.

- **`./Momentum/`** — the canonical project. Unity **2022.3.62f1** (LTS), Universal Render Pipeline (URP 14.0.12). Currently a fresh URP template: one scene (`Assets/Scenes/SampleScene.unity`) and only the template `TutorialInfo` Readme scripts — i.e. no gameplay code written yet.
- **Repo root (`.`)** — scaffolding only. It is a *separate, empty* Unity 6.3 (6000.3.18f1) project shell with the Unity MCP package. Do not put game code here. When in doubt, work inside `./Momentum/`.

Not a git repository.

## Working in this codebase

This is a Unity project driven through the Unity Editor — there is no npm/make-style build, lint, or test toolchain. Core facts:

- The source of truth is `Momentum/Assets/`, `Momentum/Packages/manifest.json`, and `Momentum/ProjectSettings/`. `Library/`, `Temp/`, `Logs/`, `obj/` are generated and disposable — never edit them.
- Every asset has a paired `.meta` file holding its GUID. When creating/moving/deleting assets by hand, keep the `.meta` in sync, or Unity reassigns GUIDs and silently breaks references in scenes/prefabs.
- Hand-editing `.unity`/`.prefab`/`.asset` YAML is error-prone (GUID + fileID wiring). Prefer doing structural scene/prefab/component changes through the Editor.
- Match the Editor version (2022.3.62f1) when opening `./Momentum/`; opening with a different Unity version forces a full asset re-import.
- Rendering uses URP — use URP-compatible shaders/APIs (not the built-in pipeline). Pipeline assets are under `Momentum/Assets/Settings/` (`URP-Balanced`, `URP-Performant`, `URP-HighFidelity`).

### Running tests (Unity Test Framework)

`com.unity.test-framework` is included. Interactively: **Window > General > Test Runner**. From the CLI (substitute your installed 2022.3.62f1 Editor path):

```bash
"<UnityEditor>" -runTests -batchmode -projectPath ./Momentum -testPlatform EditMode -testResults TestResults.xml
```

Use `-testPlatform PlayMode` for play-mode tests. Run a single test/group with `-testFilter "Namespace.Class.Method"` (or a regex).

### Headless build

Once a build script and scenes exist:

```bash
"<UnityEditor>" -batchmode -quit -projectPath ./Momentum -buildTarget StandaloneWindows64 -executeMethod <YourBuildClass.BuildMethod>
```
