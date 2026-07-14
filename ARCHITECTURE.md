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
| `Player` | Empty GameObject | Has `CharacterController`, `FirstPersonController`, `FishingSpotInteractor`, `FishingCastController`. |
| `Main Camera` | Camera | Reparented under `Player` at eye height. Retains `MainCamera` tag + `AudioListener`. View-model is parented to this. |

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

### `FirstPersonController.cs`
On `Player`. WASD relative to look direction, gravity, mouse-look, cursor locked.
Aim is via **screen-center crosshair**, not a free mouse cursor.
Exposes a control lock used during casting/fighting.

---

### `FishingSpotInteractor.cs`
On `Player`. The entry point for fishing.

On LMB: raycasts from screen center. If the **closest** hit is on the **Water layer**
(dock/ground correctly occlude water), it:
1. Locks player movement
2. Calls `castController.BeginCast(hit.point, fishing.BeginFight)`
3. On `OnFightClosed`, calls `ReturnToRest()` and unlocks movement

Auto-wires to the other controllers in `Awake()` via `GetComponent`. No Inspector wiring.
Falls back to calling `BeginFight()` instantly if no cast controller is present.

---

### `FishingCastController.cs`
On `Player`. The first-person view-model + cast animation. Built entirely in code.

**View-model** (parented to camera, all colliders stripped so it never blocks the raycast):
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
Player clicks water
  → FishingSpotInteractor raycast (Water layer only)
  → movement locked
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
- Aim is crosshair-based (cursor locked). Clicking water requires looking down at it.
