# ARCHITECTURE.md

> **Save at the ROOT of the Unity repo, next to `CLAUDE.md`.**
>
> **Purpose:** this is a *map*, not documentation. It exists so the agent knows where to look
> instead of searching. Keep it under ~150 lines. If it grows past that, it will go stale and
> become worse than useless. Update it whenever a new script or scene object is added.

---

## Environment

| Thing | Value |
|---|---|
| Render pipeline | **Built-in RP** (`Standard` shader). NOT URP. |
| Input | **Legacy Input Manager**. NOT the new Input System. |
| Active scene | `Sandbox` |
| Namespace | `Momentum.Fishing` |
| Tweening/animation | Coroutines + `AnimationCurve` only. No packages. |

---

## Scene objects (`Sandbox`)

| Object | Type | Notes |
|---|---|---|
| `Ground` | Plane primitive | Shore. Player spawns here. |
| `Water` | Plane primitive | Grey-blue `Standard` material. On the **Water layer** — this layer is how clicks are filtered. |
| `Dock` | Cube primitive (scaled long/flat) | Brown. Walkable, has collider. Extends from shore over water. |
| `Player` | Empty GameObject | Has `CharacterController` (height 2, center y=1), `TopDownController` (active), `FirstPersonController` (**disabled, not removed** — see below), `FishingSpotInteractor`, `FishingCastController`. |
| `Player/CharacterVisual` | Empty pivot @ local `(0,0.9,0)` | The body root that rotates to face movement/cast direction (`TopDownController.bodyVisual`). Also hosts the cast view-model (`FishingCastController.viewModelParent`). |
| `Player/CharacterVisual/BodyCapsule` | Capsule primitive, scale `(1,0.9,1)` → ~1.8 tall | Placeholder character body. **Collider stripped** (never blocks the cast raycast). |
| `Player/CharacterVisual/Nose` | Cube primitive @ local `(0,0.25,0.45)` | Small forward-facing indicator so facing is readable from above. **Collider stripped.** |
| `Main Camera` | Camera | **Unparented (scene root)**, fixed rotation `(55,0,0)` pitched down, positioned behind/above the player. Retains `MainCamera` tag + `AudioListener`. Follows the player via `TopDownCameraFollow`; **rotation never changes at runtime**. The cast view-model is NO LONGER parented here (now on `CharacterVisual`). |

---

## Scripts

### `FishingTensionController.cs` — 🔒 **PROTECTED**
`Assets/Scripts/Fishing/FishingTensionController.cs`

The core minigame. Full-screen `ScreenSpaceOverlay` UI, built in code (`BuildUI()`).
Tension/HP/reel struggle loop with success/fail result panel. **Has no world-space concept** —
it is purely a screen overlay.

**Public API (use these; do not add more without asking):**
- `BeginFight()` — starts an encounter, shows overlay
- `CloseFight()` — hides overlay, fires `OnFightClosed`
- `event Action OnFightClosed` — fired on close
- `bool autoStartOnPlay` — default `false`; keeps overlay hidden until triggered

**Internals worth knowing:**
- `ResetFight()` — **not dead code.** Called by `BeginFight()` and the `autoStartOnPlay` path.
- `state` defaults to `Fighting` (enum 0) and `fish` is null until `ResetFight()` runs — hence
  the null-guard at the top of `Update()`. Do not remove that guard.
- Result panel has a **Done** button → `CloseFight()`. There is deliberately **no Retry
  button** — a new encounter starts by clicking the water again.

---

### `TopDownController.cs` — active player controller
On `Player`. Fixed-angle top-down movement (legacy Input). WASD on screen-relative world
axes derived from the camera (W = away from camera, S = toward, A/D = screen left/right),
gravity kept via the existing `CharacterController`. `bodyVisual` (CharacterVisual) turns to
face the movement direction (exposed `turnSpeed`). **Cursor is visible/unlocked at all times.**
Exposes the same `SetControlEnabled(bool)` lock the old `FirstPersonController` had, plus
`FaceTowards(Vector3)` so the interactor can turn the character toward the cast point.

### `TopDownCameraFollow.cs`
On `Main Camera`. Position-only smooth follow (`SmoothDamp`) of `target` (Player). **Never
touches rotation** — the fixed top-down angle is authored in the scene. `offset`/`smoothTime`
are public for tuning (default offset `(0,13,-7)`).

### `FirstPersonController.cs` — 🚫 **DISABLED (component unticked), NOT deleted**
On `Player`. The old first-person controller (WASD relative to look, mouse-look, cursor
locked, screen-center crosshair aim). Kept in the scene and repo but its component is disabled;
`TopDownController` replaces it. Do not delete — re-enable only by disabling `TopDownController`.

---

### `FishingSpotInteractor.cs`
On `Player`. The entry point for fishing.

On LMB: raycasts **from the mouse cursor** (`cam.ScreenPointToRay(Input.mousePosition)`).
If the **closest** hit is on the **Water layer** (dock/ground correctly occlude water), it:
1. Turns the character to face the clicked point (`player.FaceTowards(hit.point)`)
2. Locks player movement (`player.SetControlEnabled(false)`)
3. Calls `castController.BeginCast(hit.point, fishing.BeginFight)`
4. On `OnFightClosed`, calls `ReturnToRest()` and unlocks movement

`player` is now typed `TopDownController` (was `FirstPersonController`). The center crosshair
(`drawCrosshair`) is disabled in the Inspector since aim is now the mouse cursor.
Auto-wires the cast controller in `Awake()` via `GetComponent`; other refs set in Inspector.

---

### `FishingCastController.cs`
On `Player`. The rod/arm view-model + cast animation. Built entirely in code.

**Re-hosted for top-down:** `viewModelParent` is wired (Inspector) to `CharacterVisual` so the
rig is held at the character's side instead of on the camera. **No code change was needed** —
the windup/release rod pitches are local to the rig root, and the flight is world-space
analytical, so re-parenting is purely a mounting change. The body is turned to face the cast
point first (by the interactor), so the rod flings toward the target.

**View-model** (parented to `viewModelParent`, all colliders stripped so it never blocks the raycast):
- `Arm` — cube, lower-right
- `RodPivot` → `Rod` — thin long cube, animated
- `RodTip` — anchor transform
- `Lure` — blue sphere, non-uniform oval scale
- `String` — `LineRenderer`, drawn in world space tip→lure every `LateUpdate()`

**`BeginCast(Vector3 target, Action onLanded)`:**
1. Windup — rod rotates back, ~0.2s
2. Release — rod snaps forward, ~0.12s; lure detaches to world space
3. Flight — ~0.5s parabola: `Lerp(start, target, u) + arcHeight * 4u(1-u)`.
   **Analytical, not physics-simulated** — lands exactly on target every time.
4. `onLanded()` fires → rod eases to forward hold pose

**`ReturnToRest()`** — re-docks lure to rod tip, returns to idle.

Cast timing and rod poses are **public fields**, exposed for feel tuning without code changes.

---

## Flow, end to end

```
Player clicks water (mouse cursor)
  → FishingSpotInteractor raycast from cursor (Water layer only, closest hit)
  → character turns to face the point → movement locked
  → FishingCastController.BeginCast(hitPoint, callback)
      → windup → release → parabolic flight
      → lands exactly on hitPoint
  → callback → FishingTensionController.BeginFight()
      → overlay shows, tension minigame runs
      → win/lose → result panel → Done button
  → CloseFight() → OnFightClosed
  → ReturnToRest() + movement unlocked
  → player walks; click water again for a new encounter
```

---

## Known constraints

- The minigame overlay is **full-screen and screen-space** — it has no concept of world
  position. "Anchoring" the fight to the cast location is handled by locking the player in
  place, not by any world-space UI.
- Aim is **mouse-cursor based** (cursor visible/unlocked). Click directly on the water; the
  fixed top-down camera keeps the water in view. (The old crosshair path is disabled, not removed.)
