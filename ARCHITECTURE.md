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
| Namespaces | `Momentum.Fishing` (fishing systems, `Assets/Scripts/Fishing/`); `Momentum.Player` (player controllers, `Assets/Scripts/Player/`) |
| Tweening/animation | Coroutines + `AnimationCurve` only. No packages. |

---

## Scene objects (`Sandbox`)

| Object | Type | Notes |
|---|---|---|
| `Ground` | Plane primitive | Shore. Player spawns here. |
| `Water` | Plane primitive | Grey-blue `Standard` material. On the **Water layer** — this layer is how clicks are filtered. |
| `Dock` | Cube primitive (scaled long/flat) | Brown. Walkable, has collider. Extends from shore over water. |
| `Player` | Empty GameObject | Has `CharacterController` (height 2, center y=1), `TopDownController` (active), `FirstPersonController` (**disabled, not removed** — see below), `FishingSpotInteractor`, `FishingCastController`, `FishingBiteController`. |
| `Player/CharacterVisual` | Empty pivot @ local `(0,0.9,0)` | The body root that rotates to face movement/cast direction (`TopDownController.bodyVisual`). Also hosts the cast view-model (`FishingCastController.viewModelParent`). |
| `Player/CharacterVisual/BodyCapsule` | Capsule primitive, scale `(1,0.9,1)` → ~1.8 tall | Placeholder character body. **Collider stripped** (never blocks the cast raycast). |
| `Player/CharacterVisual/Nose` | Cube primitive @ local `(0,0.25,0.45)` | Small forward-facing indicator so facing is readable from above. **Collider stripped.** |
| `Main Camera` | Camera | **Unparented (scene root)**, fixed rotation `(55,0,0)` pitched down, positioned behind/above the player. Retains `MainCamera` tag + `AudioListener`. Follows the player via `TopDownCameraFollow`; **rotation never changes at runtime**. The cast view-model is NO LONGER parented here (now on `CharacterVisual`). |
| `LureShop` | Empty GameObject @ `(0,0.3,13)` | Far (over-water) end of the Dock. Hosts `LureShop`; a trigger `BoxCollider` is added in code. The stand primitives (`ShopCounter`/`ShopPost`/`ShopSign`) + hint/panel canvas are all **built in code at runtime** — not authored in the scene. |

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
- `event Action<FishData, CatfishSpecies> OnFishLanded` — fired the moment a fight is WON
  (inside `Win()`, before the Done button closes the panel), carrying the landed `FishData`
  **and the caught `CatfishSpecies`**. The **species' `rarity`** now drives the coin reward
  (not `FishData.rarity`). Passing the same `caughtSpecies` shown on the result panel makes
  payout-vs-displayed-species mismatch structurally impossible. Added with explicit approval
  (species param added later, also approved); loss path is untouched so losses signal nothing.
- `bool autoStartOnPlay` — default `false`; keeps overlay hidden until triggered
- `bool IsFightVisible` (get) — true while the overlay canvas is on screen (`BeginFight`→
  `CloseFight`), on win **and** loss. Read-only; added so `CoinHud` can hide the top-left
  counter while the overlay is up. No behaviour change.

**Internals worth knowing:**
- `ResetFight()` — **not dead code.** Called by `BeginFight()` and the `autoStartOnPlay` path.
- `state` defaults to `Fighting` (enum 0) and `fish` is null until `ResetFight()` runs — hence
  the null-guard at the top of `Update()`. Do not remove that guard.
- Result panel has a **Done** button → `CloseFight()`. There is deliberately **no Retry
  button** — a new encounter starts by clicking the water again.
- On win, the result panel now shows the caught **catfish species' name + a flat colour
  swatch** (`resultSwatch`, hidden on loss). Species identity is picked in `ResetFight()`
  via `CatfishSpecies.PickRandom()` and is **display-only** — it does not affect fight
  stats (all catfish use the same `FishData`, so difficulty is unchanged). Private fields
  `resultSwatch` / `caughtSpecies`; no public API change.

### `FishData.cs`
`Assets/Scripts/Fishing/FishData.cs`. Plain `[Serializable]` data for the fight mechanics
(`displayName`, `effortFrequency`, `effortIntensity`, `progressPerReel`, `rarity`) plus the
`FishRarity` enum. Drives all tension/HP behaviour; not species content. **`rarity` no longer
drives payout** (species rarity does now — see below) but is retained/unused, not removed.

### `CatfishSpecies.cs`
`Assets/Scripts/Fishing/CatfishSpecies.cs`. Content + reward-tier layer: the 3 catchable
catfish (**Whiskers=Common / Old Tom=Uncommon / Spotmouth=Rare**) as name + swatch colour
+ `FishRarity rarity`. Plain C# (no ScriptableObject). Carries **no fight stats** — every
catfish still fights identically; rarity drives payout only.
- `static Catalogue` — the entries. **Catalogue index order (0=Whiskers, 1=Old Tom,
  2=Spotmouth) is canonical** — every weight table is indexed against it.
- `static float[] ActiveWeights` (private) — current pick odds, one per entry. Default
  **70/25/5** (heavily Whiskers), reproducing the old near-uniform-feel bias.
- `static SetActiveWeights(IReadOnlyList<float>)` — swaps the odds (used by `LureShop.Equip`).
  Null/wrong-length is ignored (odds unchanged). Takes effect on the NEXT fight.
- `static PickWeightedIndex()` / `static PickRandom()` — weighted roll against `ActiveWeights`.
  `PickRandom()` is called once per fight from `FishingTensionController.ResetFight()`.

---

### `PlayerWallet.cs` — coin reward logic
`Assets/Scripts/Fishing/PlayerWallet.cs`. On the **`FishingTension`** GameObject (same object as
the tension controller — `[RequireComponent(FishingTensionController)]`). Session-only coin
balance (no saving/PlayerPrefs; resets to 0 each Play). Auto-subscribes to the controller's
`OnFishLanded` in `OnEnable`, awards `PayoutFor(species.rarity)` off the caught
`CatfishSpecies` (falls back to `fish.rarity` only if no species is supplied). Exposes `int Coins`,
`AddCoins(int)`, `event Action<int,int> OnBalanceChanged (newTotal, delta)`, and the five
per-rarity payout amounts as **public Inspector fields** (defaults: Common 10, Uncommon 25,
Rare 60, Epic 150, Legendary 400). Wins only — losses award nothing.

### `LureShop.cs` — dockside lure shop
`Assets/Scripts/Fishing/LureShop.cs` (namespace `Momentum.Fishing`). On the `LureShop`
GameObject. **Self-contained, all in code**: builds its own stand primitives + trigger volume,
a screen-space hint (`"Press E — Lure Shop"`) and shop panel (own `ScreenSpaceOverlay` canvas,
`sortingOrder = 90` — below `CoinHud` so the balance stays on top), and holds session-only
purchase/equip state (no saving). References (`PlayerWallet`, `TopDownController`,
`FishingCastController`, `FishingSpotInteractor`) **auto-wire** via `FindFirstObjectByType` but
are Inspector-overridable. Lure stock is a **public `LureOption[]`** (name/price/colour/ownedByDefault **+ per-species
weights** `whiskersWeight`/`oldTomWeight`/`spotmouthWeight`) — all Inspector-tunable. Defaults:
Blue (free, owned, default) 70/25/5, Red 50 → 50/40/10, Purple 150 → 30/45/25, Gold 400 →
15/40/45. **Equipping a lure applies its weight table** via `CatfishSpecies.SetActiveWeights`
(`ApplyLureOdds`), shifting species odds toward rarer (higher-paying) fish. Applies to the
NEXT fight; a fight already running is unaffected. Blue's table is applied at `Start()` (the
default-equip path).
- Trigger `OnTriggerEnter/Exit` (fires from the player's CharacterController) → shows the hint
  when in range **and** free (`player.ControlEnabled`). Walking out closes an open shop.
- `E` opens/closes; open locks movement (`SetControlEnabled(false)`) **and disables the
  `FishingSpotInteractor`** so panel clicks can't fall through to a water cast. Opening is
  refused when `!player.ControlEnabled` (mid-cast/fight).
- Buy → `PlayerWallet.TrySpend`, mark owned, auto-equip. Unaffordable → button disabled.
  Equip → `FishingCastController.SetLureColor` (colour persists through cast→fight→return).

### `CoinHud.cs` — coin counter overlay
`Assets/Scripts/Fishing/CoinHud.cs`. Also on **`FishingTension`** (`[RequireComponent(PlayerWallet)]`).
Its own always-visible `ScreenSpaceOverlay` canvas (`sortingOrder = 100`, above the fight
overlay), built in code in the same `BuildUI` style as the tension controller. Top-left
`Coins: N` counter that updates on `OnBalanceChanged`.
- **Hides during a fight:** polls `FishingTensionController.IsFightVisible` each frame and hides
  the top-left counter while the overlay is up (win & loss), so it never overlaps the HP/tension
  bars; re-shows on close with the post-award total.
- **Win-result coin line:** on a positive-delta balance change (award = win; fires while the
  result panel is already up), shows a centered `+N / Total: M` on its own canvas
  (`sortingOrder 100`, above the overlay's `0`), in the result panel's dead space. Loss shows
  none.
- The old floating `+N`-near-the-HUD path (`ShowPopup`/`PopupRoutine`) is **superseded and no
  longer called** (call site commented, methods kept per the never-delete rule).

### `TopDownController.cs` — active player controller
`Assets/Scripts/Player/TopDownController.cs` (namespace `Momentum.Player`).
On `Player`. Fixed-angle top-down movement (legacy Input). WASD on screen-relative world
axes derived from the camera (W = away from camera, S = toward, A/D = screen left/right),
gravity kept via the existing `CharacterController`. `bodyVisual` (CharacterVisual) turns to
face the movement direction (exposed `turnSpeed`). **Cursor is visible/unlocked at all times.**
Exposes the same `SetControlEnabled(bool)` lock the old `FirstPersonController` had, plus
`FaceTowards(Vector3)` so the interactor can turn the character toward the cast point.
**Added for the lure shop (approved edit):** read-only `public bool ControlEnabled` getter — lets
the shop tell when the player is free (not mid-cast/fight) before opening. No behaviour changed.

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
`Assets/Scripts/Player/FishingSpotInteractor.cs` (namespace `Momentum.Player`).
On `Player`. The entry point for fishing.

On LMB: raycasts **from the mouse cursor** (`cam.ScreenPointToRay(Input.mousePosition)`).
If the **closest** hit is on the **Water layer** (dock/ground correctly occlude water), it:
1. Turns the character to face the clicked point (`player.FaceTowards(hit.point)`)
2. Locks player movement (`player.SetControlEnabled(false)`)
3. Calls `castController.BeginCast(hit.point, StartBiteOrFight)`. On landing `StartBiteOrFight`
   hands off to `bite.BeginWait(fishing.BeginFight, HandleNoCatch)` (the **bite-wait phase**), or
   — if no `FishingBiteController` is present — calls `fishing.BeginFight()` directly (old
   behaviour).
4. On `OnFightClosed` **or** the no-catch outcome (`HandleNoCatch`), calls the shared
   `ReleaseFishing()` — clears the re-trigger guard, unlocks movement, and `ReturnToRest()`. The
   no-catch path reuses this **exact** unlock so no fight ever has to start, and casting again
   works immediately.

`bite` (the `FishingBiteController`) auto-wires in `Awake()` via `GetComponent` (same pattern as
`castController`); Inspector-overridable.

`player` is now typed `TopDownController` (was `FirstPersonController`). The center crosshair
(`drawCrosshair`) is disabled in the Inspector since aim is now the mouse cursor.
Auto-wires the cast controller in `Awake()` via `GetComponent`; other refs set in Inspector.

---

### `FishingBiteController.cs`
`Assets/Scripts/Fishing/FishingBiteController.cs` (namespace `Momentum.Fishing`).
On `Player`. The **bite-wait phase** inserted between the lure landing and the fight starting
(Pokémon-encounter style). Built entirely in code; Coroutines + primitives only.

**Public API:**
- `BeginWait(Action onBite, Action onNoCatch)` — called by `FishingSpotInteractor` as the cast's
  `onLanded` callback (instead of `fishing.BeginFight` directly). Rolls the outcome **once** up
  front, then waits, then fires **either** `onBite` (fish → start the fight) **or** `onNoCatch`
  (nothing/junk → return the line, unlock).

**Behaviour:**
- Outcome odds are **public Inspector fields**, normalized in code (need not sum to 100):
  `fishBiteChance` (85), `nothingChance` (10), `junkChance` (5). Junk sub-rolls uniformly over
  `junkMessages` (yarn / scratching post / hairball).
- Wait is `Random.Range(minWait, maxWait)` (public, default 1–5s). Movement stays locked the whole
  time (the interactor locked it before the cast).
- **Fish:** after the wait, a "!" thought bubble pops above the head, holds for `readBeat`
  (public, default 0.7s), hides, **then** `onBite` runs. No input required.
- **Nothing/junk:** waits the **FULL `maxWait`** (not the fish roll), shows a brief screen message
  for `noCatchMessageDuration` (public, default 2s), then `onNoCatch` runs. Junk is **flavor text
  ONLY** — no coins, no inventory, nothing spawned.

**Visuals (built in code):**
- Thought bubble: world-space, parented to `CharacterVisual` at `bubbleLocalOffset` (public,
  default `(0,2.1,0)` — above the head). A white `Quad` backdrop + a `TextMesh` "!" (colliders
  stripped). **Billboarded to `Camera.main` every `LateUpdate`** so it reads from the fixed
  top-down angle. Hidden by default; shown at the bite, hidden before the overlay opens.
- No-catch message: own `ScreenSpaceOverlay` canvas, `sortingOrder = 90` (**below** `CoinHud`'s
  100), same code-built style as `CoinHud`/the tension overlay. Centered-lower panel.

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

**Added for the lure shop (approved edit):** `public void SetLureColor(Color)` — sets the lure
material's colour. Persists through cast/fight/return because `lureMat` is created once and never
reset. No existing behaviour changed.

---

## Flow, end to end

```
Player clicks water (mouse cursor)
  → FishingSpotInteractor raycast from cursor (Water layer only, closest hit)
  → character turns to face the point → movement locked
  → FishingCastController.BeginCast(hitPoint, callback)
      → windup → release → parabolic flight
      → lands exactly on hitPoint
  → callback → FishingBiteController.BeginWait(onBite, onNoCatch)  [bite-wait phase]
      → rolls outcome once (85% fish / 10% nothing / 5% junk), waits 1–5s
      → FISH: "!" bubble pops above head → read beat → onBite:
           → FishingTensionController.BeginFight()
               → overlay shows, tension minigame runs
               → win/lose → result panel → Done button
           → CloseFight() → OnFightClosed → ReleaseFishing()
      → NOTHING/JUNK: full 5s wait → brief screen message → onNoCatch:
           → HandleNoCatch() → ReleaseFishing()  (no fight ever started)
  → ReleaseFishing(): movement unlocked + ReturnToRest()
  → player walks; click water again for a new encounter
```

---

## Known constraints

- The minigame overlay is **full-screen and screen-space** — it has no concept of world
  position. "Anchoring" the fight to the cast location is handled by locking the player in
  place, not by any world-space UI.
- Aim is **mouse-cursor based** (cursor visible/unlocked). Click directly on the water; the
  fixed top-down camera keeps the water in view. (The old crosshair path is disabled, not removed.)
