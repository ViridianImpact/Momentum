# 06_STATE_AND_ROADMAP.md — Honest State Assessment & Milestone Playbook

> Companion to `00_VISION.md`. Written 2026-07-17. The percentages below are against
> the **1.0 Steam release** defined in the vision. Re-baseline only if the vision is
> deliberately revised.

---

## Completion: **~20% overall** (wife-complete milestone: ~15%)

Not a discouraging number — a clarifying one. The 20% that exists is the *hard-to-fake*
20%: a working, feel-tested core loop end-to-end plus persistence and telemetry (M1).
Most of the remaining 80% splits into (a) systems code the AI agent builds cheaply and
reliably, and (b) content/art/balancing that costs developer-and-wife time, not tokens.
The plan below is ordered so that (a) never blocks (b).

| System | State | % |
|---|---|---|
| Core fight (tension minigame) | Working, protected. Needs Fight 2.0 (QTEs, reel stats, tension visual, per-tier stats) | 60% |
| Cast & aim | Working, feel-tunable via Inspector. Fly-rod trail variant pending | 80% |
| Catch/loot system | 3-species weighted lure tables work but the design cannot scale (see M2) | 15% |
| Economy & shop | Wallet, payouts, lure shop live. 1 of 6 gear categories; no inventory/loadout | 15% |
| World/map | One sandbox plane + dock vs. a 4-region walkable map | 5% |
| Species content | 3/50 as data; 0/50 art; no quality/size roll | 5% |
| NPC/quest system | Nothing exists | 0% |
| Persistence (save/load) | JSON save/load (coins, lure loadout, schema v1); auto-saves on change. Grows with each new system. | 95% |
| Telemetry | JSONL event log (7 event types); append-only, fire-and-forget. Grows with new systems. | 90% |
| Audio | Nothing exists | 0% |
| Menus/settings/pause | Nothing exists | 0% |
| Art pipeline (sprite-in-3D) | Direction chosen; nothing built or tested | 0% |
| Steam packaging/store | Nothing exists (deliberately last) | 0% |

---

## Milestone playbook

Each milestone = one or a few prompts via `04_PROMPT_TEMPLATE.md`, its own commit(s),
its own `/clear`. Order matters — it is dependency-driven, not preference-driven.

**M0 — Housekeeping (this week, no agent needed)**
✅ Studio: **LizardNucleus** (pending a 5-min publisher/domain availability check).
✅ Game name: **"Catfishing"** as working title; "Catfish" and "FisherCat" eliminated
by collision check; final name due before M12.
⬜ Create `LICENSES.md`. ⬜ Decide save path (`Application.persistentDataPath`, JSON).

**M1 — Save system + telemetry** ✅ *(shipped 2026-07)*
JSON save/load service (`SaveService`, `momentum_save.json` — coins, owned lures,
equipped lure, schema v1) + JSONL event log (`TelemetryService`,
`momentum_telemetry.jsonl` — 7 event types: `session_start`, `cast`, `bite_outcome`,
`fight_start`, `fight_end`, `coins`, `loadout`). Additive hooks on `PlayerWallet`,
`LureShop`, `FishingSpotInteractor`, and `FishingBiteController` — protected file
untouched. Auto-saves on every meaningful change; restore path with zero-delta
(no UI flash). Both services live on the `SaveService` GameObject in `Sandbox`.

**M2 — Catch-table refactor** *(AI-strong, the central data system, ~3 prompts)*
Species registry (50-capable): rarity tier, tier stat-template + offsets, region pool
membership, rod requirement, gear-tier eligibility band, bait tags, quality roll params.
Selection becomes `Pick(region, rod, lure, bait)`; lures become rarity-tier modifiers,
not per-species fields; the static `ActiveWeights` global and named
`whiskersWeight/oldTomWeight/spotmouthWeight` fields are superseded (kept, per the
never-delete rule). Touches `ResetFight()`'s call site in the protected file:
full ceremony — spec first, branch, tight discovery step.

**M3 — Fight 2.0** *(the ONE planned opening of the protected file)*
Batched deliberately: per-tier fight stats consumed from M2 · positional QTE dodges on
Epic/Legendary · reel stat hooks · line-tension visual. Screen shake ships separately
and earlier (it lives in `TopDownCameraFollow`, not the protected file — the drafted
prompt must be checked for this before it is sent). Fully specced before any prompt.

**M4 — Inventory & loadout** *(AI-strong)*
Equip model for rod/reel/lure/bait, tier-lock rules (reel ≤ rod tier), shop expansion
from 1 category to 6, bait as consumable stack. All persisted via M1.

**M5 — Greybox map** *(AI-WEAK — this is developer editor time)*
Four regions blocked out with primitives in one scene: distinct water bodies on the
Water layer, walkable paths, gate positions (pier, vines), secret-spot geometry,
TukTuk stops, tent spawn. No art. The point is walking distance, sightlines, and
route-knowledge as progression. Expect this to be hands-on Unity work with MCP used
for wiring, not sculpting. It is also probably the most *fun* milestone — it is
Minecraft-adjacent building, not exposure work.

**M6 — NPC & quest system** *(AI-strong systems + developer writing)*
Interactable NPCs (❗ indicator), dialogue panels in the established code-built UI
style, favor chains as data (fish + count → unlock), gate integration with M5.
The "clever trap" chains (the 10,000-guppies gag) are content entries, not code.

**M7 — Menus & settings floor** *(AI-strong, unglamorous, scheduled so it exists)*
Title, pause, volume, resolution, quit, save-slot bootstrapping.

**M8 — Art pipeline test, then production** *(parallel track from here)*
3-asset test: cat + one fish + one prop, billboarded in-engine, camera-angle checked
at the fixed 55° pitch. Only after the test passes does anyone commit to 50 species.
Hero assets commissioned; bulk species by wife against the hero style bar.

**M9 — Audio pass** *(wiring is 1–2 prompts; sourcing is developer time)*
SFX first (cast, splash, reel, win/loss sting — disproportionate feel value), ambient
loop second. Every file enters through `LICENSES.md`.

**M10 — Content fill & balancing** *(data entry + telemetry-driven tuning)*
Species 4→50, NPC chains, gear tables, payout curves. Legendary rates tuned in
days-of-play using M1 telemetry from the developers' own sessions.

**M11 — Wife-complete** *(the real finish line)*
The D3 evening happens for real. Nothing proceeds to M12 until it does.

**M12 — Steam commercialization pass**
Studio account ($100), store page, capsule art, screenshots, trailer, builds/depots,
pricing. All brand-name surfaces. Deliberately last and deliberately separate.

---

## Top risks & mitigations

**1. Art pipeline (downgraded from highest risk — now a scheduling risk, not an
existential one).** Plan is commission-primary and fully inside the ~$2k budget at
bulk-simple spec; wife participation is upside, not dependency; DIY (Aseprite) is the
documented fallback. Every branch of the cascade ends fully arted at or under budget.
*Remaining mitigations:* the M8 three-asset test before any commission; hero/bulk
quality split; palette swaps for species 30–50; no artist browsing before M8.

**2. The map is AI-weak terrain.** Scene/terrain authoring is where the agent workflow
is weakest and where GUID/YAML breakage lives. *Mitigations:* greybox early (M5) so
every later system develops against real geography; developer does the sculpting
by hand; MCP for component wiring only; commit before every scene session.

**3. Protected-file surgery.** M2 touches its call site; M3 opens it. *Mitigations:*
batching all fight changes into one milestone; full spec before prompting; dedicated
branch; the existing discovery-step + stop-and-ask regime, which has a perfect record.

**4. Bursty effort + the judgment cliff at M12.** The pattern on record: strong sprints,
quiet weeks, and a known aversion to exposure surfaces exactly where Steam begins.
*Mitigations:* the hard 2027-07-17 ship-what-exists date; wife-complete (M11) as the
emotionally real finish line so M12 is logistics, not identity; every public surface
pre-decided as brand-name in M0 so no exposure decision remains to stall on.

**5. Procrastination-via-building watchlist** (pre-named so they can be cited later):
a metrics dashboard before M2 ships · any editor tooling "to make content entry
easier" before there is content · the knot minigame before 1.0 · spectral co-op ·
anything for the frozen lake · **a crowdfunding campaign** (anti-goal; the legitimate
audience play is the M12 Steam page + Next Fest demo, product-led, brand-name only).

---

## Process improvements adopted during this exercise

1. `ARCHITECTURE.md` is being updated in step with builds — verified this session
   (the lure-weight system was documented before the guidance side knew it existed).
   Keep the ≤150-line discipline as it grows; prune superseded detail into git history.
2. Economy/balancing work is frozen until M1+M2 exist; numbers tuned before
   persistence and the real catch model are numbers tuned twice.
3. Vision drift now has a referee: feature requests get classified against
   `00_VISION.md`, and "vividly designed" is no longer confusable with "shipped" —
   the doc and the repo are the only two sources of truth.
