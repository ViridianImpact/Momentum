# 00_VISION.md — Catfishing (working title)

> **This document is CLOSED as of 2026-07-17.** It is the referee for every scope
> decision. Changing it requires a deliberate revision, not drift. The guidance agent
> classifies all feature requests against this page.

---

## Identity

**It's a game where you are a fishercat trying her luck at catching catfish.**

- Cozy, pastel, happy. Cult of the Lamb's presentation model (3D world, billboarded 2D
  sprite characters) with the opposite mood.
- References: **Webfishing** (loop, economy, catalogue-completion objective),
  **Stardew Valley** (NPC favor chains gating unlocks — no calendar, no birthdays).
- Feel curve: zen baseline → collection pull with time → rare mastery spikes on
  Epic/Legendary fights.

## Player & release

- **Player #1: the developer and his wife.** She is the quality referee.
  "Wife-complete" is the definition of done for the *game*.
- **Steam, paid (~$5), is the definition of done for the *product*.** Ships under the
  studio brand — never a personal name, no face content, ever.
- **Studio: LizardNucleus** (decided 2026-07-17; verify Steam-publisher/domain
  availability). **Game: "Catfishing" — working title.** Known SEO/connotation risk
  (scam/MTV associations own the search term); acceptable, discovery runs via tags.
  Eliminated by collision check: "Catfish" (Steam ×2), "FisherCat" (The Fishercat,
  LoadComplete 2018 — same concept space). Final name locked before M12; blocks nothing
  until then. Do not reopen casually.
- **Self-funded. No crowdfunding.** Total 1.0 outlay (~$2k art + $100 Steam) is covered
  by existing cash. Marketing = Steam page wishlists + a Next Fest demo under the
  LizardNucleus brand — the product speaks; the developer does not appear.

## Core loop

Talk to NPC (❗) → learn their favorite fish → find its habitat → gear up → cast (mouse-
aim, analytical parabola) → tension-minigame fight → collect/sell → NPC unlocks new
region or gear → repeat. Endless fishing; finite unlocks.

- **Fight:** existing tension minigame is FINAL, expanded once ("Fight 2.0"): positional
  QTE dodges on Epic/Legendary only (miss = dunked, sad wet cat), reel stats, line-
  tension visual. Screen-space overlay stays screen-space; the dunk is presentation
  after the fight, not world simulation during it.
- **Loss cost:** time only at 1.0. Knot-retying trace minigame = post-1.0.

## Content

- **50 catfish species.** Fight stats from 5 rarity-tier templates + small per-species
  offsets. Per-catch quality/size roll drives value; gear shifts it deterministically.
- **4 regions at 1.0: Lake, River, Pond, Ocean.** One scene, one walkable map, mostly-
  exclusive species pools, no per-region mechanics. Frozen lake + hole saw = seasonal
  update. Fly-rod-only sub-pools in Lake/Pond/River (different cast trail, no new
  technique).
- **Secret discoverable spots** (e.g. behind the waterfall) — found, not unlocked.

## Gear — six categories, one verb each

| Category | Verb |
|---|---|
| Rods | Unlock pools/cast types + cool factor |
| Lures | Shift rarity odds (implemented, needs refactor to scale) |
| Bait | Consumable modifier bundles (target species, quality bias, easier fight) |
| Reels | Modify the fight (speed, pull reduction); tier-locked to rod tier |
| Tools | Key items that open gates (net→pier, claws→lake, tent→teleport home) |
| Transport | TukTuk fast travel — coins or catnip |

- **Gear-tier eligibility on fish** (the "too good to catch guppies" mechanic) is core.
- **Catnip:** dual-use item (monster-fish trigger + TukTuk payment). Inventory item,
  finite, sourced — design its source deliberately; it is a second currency.

## Progression & persistence

- Full save: coins, gear, unlocks, NPC chains, **lifetime per-species career counts**.
- Local JSONL telemetry from day one; balancing (esp. Legendary rates, tuned in
  days-of-play) is data-driven off the developers' own sessions.
- Knowledge-as-progression: map mastery, spots, routes. Costs level design, not code.
- No end state. Climax: the rare fish she stopped believing existed. Then victory laps.

## Presentation

- **Commission-primary art plan (~$2k covers a complete 1.0 pass at bulk-simple spec):**
  animated hero fishercat (~$300–800), 4–6 mostly-static NPCs (~$50–150 ea), ~20 bulk
  fish bodies at 48–64px (~$400–900 as a batch set), palette swaps extending to 50
  species. Wife participation on bulk sprites is a welcome **upside if she opts in**
  (with the real asset count on the table) — never a dependency. DIY fallback exists
  (Aseprite ~$20; fish are the easiest subject in pixel art). Sourcing via transactional
  channels (r/gameDevClassifieds, VGen) — zero personal-brand surface.
- 3-asset pipeline test (deliberately ugly placeholders are fine) precedes any
  commission — it proves billboarded sprites read at the fixed 55° camera, nothing else.
- Full SFX + cozy ambient soundtrack. **Commercial licenses verified at acquisition;
  LICENSES.md in repo from the first sound.**
- Floor: title screen, pause, settings (volume, resolution), quit.

## Release sentence (binary)

**1.0 ships when:** a fresh install on a clean machine lets a new player complete every
NPC unlock chain, catch all 50 species, and quit/resume with full progression intact,
with no missing art or sound — **and** the wife-complete evening has actually happened.

## Anti-goals (NEVER at 1.0)

Multiplayer/networking of any kind (spectral co-op = possible 2.0) · Mobile port ·
Procedural generation · Multiple save slots/instances · First-person mode ·
Leaderboards/online services · **Crowdfunding campaigns** (self-funded; a campaign is
a marketing job that buys nothing at this budget scale).

## Budget

~2 hrs/day average, delivered in honest bursts. **Hard date: 2027-07-17 — ship what
exists.** Wife-complete milestone precedes any Steam work.
