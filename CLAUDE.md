# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this
repository.

---

## Project structure

- **The repo root (this working directory) is the canonical Unity project root.** Confirmed
  via `git rev-parse --show-toplevel`. Unity **6000.3.18f1** (Unity 6), Built-in Render
  Pipeline (confirmed via Project Settings > Graphics: no URP asset assigned).
  `Assets/Scripts/Fishing/` (all fishing game code) lives here. No nested duplication — the
  root contains `Assets/` directly.

## Working in this codebase

Unity-Editor-driven project — no npm/make-style build, lint, or test toolchain.

- Source of truth: `Assets/`, `Packages/manifest.json`, `ProjectSettings/` (within whichever
  project is canonical). `Library/`, `Temp/`, `Logs/`, `obj/` are generated/disposable —
  never edit them.
- Every asset has a paired `.meta` file holding its GUID. When creating/moving/deleting
  assets by hand, keep the `.meta` in sync, or Unity reassigns GUIDs and silently breaks
  scene/prefab references.
- Hand-editing `.unity`/`.prefab`/`.asset` YAML is error-prone (GUID + fileID wiring). Prefer
  doing structural scene/prefab/component changes through the Editor or via Unity MCP calls,
  not raw text edits.
- Match the Editor version when opening a project; opening with a different Unity version
  forces a full asset re-import.
- Rendering pipeline: **confirm which one is live before writing shader/material code** —
  see unresolved item #1. If URP: use URP-compatible shaders/APIs, pipeline assets are under
  `Assets/Settings/` (`URP-Balanced`, `URP-Performant`, `URP-HighFidelity`). If Built-in: use
  `Standard` shader; `Shader.Find` on URP shader names returns null and will produce a
  magenta/crash.

### Running tests (Unity Test Framework)

`com.unity.test-framework` is included. Interactively: **Window > General > Test Runner**.
From the CLI (substitute your installed Editor path):

```bash
"<UnityEditor>" -runTests -batchmode -projectPath . -testPlatform EditMode -testResults TestResults.xml
```

Use `-testPlatform PlayMode` for play-mode tests. Run a single test/group with
`-testFilter "Namespace.Class.Method"` (or a regex).

### Headless build

Once a build script and scenes exist:

```bash
"<UnityEditor>" -batchmode -quit -projectPath . -buildTarget StandaloneWindows64 -executeMethod <YourBuildClass.BuildMethod>
```

---

## Git — this IS a repo now

Repo root is the Unity project root itself (confirmed via `git rev-parse --show-toplevel`). Remote is
pushed to GitHub. `.gitignore` is Unity's standard template plus `.claude/` (Claude Code's
local tool state — not project content, never commit it).

**Before starting any implementation session:**
```bash
git add -A && git commit -m "checkpoint before [feature name]"
```
This is the actual safety net for the hard rules above — not the prompt wording. Any
unwanted change (deletion, a rule violation, a bad judgment call) is a `git diff` /
`git checkout` away from undone, but only back to the most recent commit. Commit often,
specifically right before handing over anything that touches existing working code.

**After a session completes and is verified working:**
```bash
git add -A && git commit -m "[what was built/changed]"
git push
```

If `git status` ever shows `Library/`, `Temp/`, `Logs/`, `obj/`, or a nested `Momentum/`
prefix on paths, stop and check `.gitignore` and folder structure before committing — see
the note above about the duplicate-folder incident.

---

## HARD RULES — do not override under any circumstances

These hold even under time pressure, even if a change seems trivially safe, even at high
confidence, and **especially if the user is away and not responding.**

### 1. Never delete
Do not delete files, GameObjects, components, scenes, prefabs, or blocks of code. Comment
out, disable, or rename with a `_deprecated_` prefix instead, and say what you did and why.
No exceptions.

### 2. Stop and wait on instruction conflicts
If a task requires violating an instruction the user gave — including a "do not modify"
rule on a specific file — **stop and ask. Do not proceed on best judgment.** If the user has
not responded, **wait.** No task is urgent enough to skip this. This holds even when the fix
is one line, provably behavior-preserving, and trivially reversible — an **incomplete task
plus a clear explanation** is the correct outcome, not a completed task plus a violated rule.

### 3. When unsure whether something is off-limits, treat it as off-limits
Ask. The burden of proof is on proceeding, not on stopping.

### 4. No new dependencies
No packages, Asset Store assets, or third-party libraries (e.g. tweening libraries) unless
already present. Use Coroutines, `AnimationCurve`, `LineRenderer`, `Mathf`. If a dependency
seems genuinely necessary, stop and ask.

### 5. No silent edits to protected files
Any change to a file marked protected (see `ARCHITECTURE.md`) must be reported explicitly
with a diff, flagged prominently in the summary — never buried in a paragraph.

---

## WORKFLOW — required for every implementation task

1. **Discovery first.** Read the relevant existing files, report what exists and what will
   need to change, then give a short plan. **Wait for go-ahead before implementing.**
2. **Implement.**
3. **Verify in Play mode.** Confirm each acceptance criterion explicitly. Report console
   errors honestly, including pre-existing ones — do not fix pre-existing issues without
   asking, even if they block an acceptance criterion.
4. **Report.** State what was built, what was changed, what was flagged, what to verify.

---

## ARCHITECTURE

See `ARCHITECTURE.md` in the repo root — read it before starting any task so project
structure doesn't need to be rediscovered by search. Keep it updated (same commit as the
feature) when new scripts or scene objects are added.
