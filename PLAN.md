# Deep-Sea Station: Build Plan

**Core idea:** Expand a research station downward, room by room. Three resources (oxygen, power, heat), visible failures, and slow fallbacks so one mistake never snowballs.

**Current status:** Phase 1 built, being playtested. Nothing past Phase 1 gets built until the core loop feels good.

## Phase 1: Prototype (the fun test) ✅ built

- Unity, portrait mode, tested on a phone along the way
- A grid of rooms with 3 types: Living Quarters, Scrubber (oxygen), Generator (power)
- 3 crew you tap to assign to rooms
- Oxygen and power tick up and down; a red warning shows which room is failing
- **Goal:** is keeping the station balanced fun for 5 minutes?

What was built:

- 3 floors × 2 rooms: 2 Quarters, 2 Scrubbers, 2 Generators
- Crew stamina drains while working and refills in Quarters, so the 3 crew have to rotate
- Exhausted crew keep working at half speed instead of stopping
- Running out of oxygen starts a 20 s countdown, so you can still recover before the run ends
- Every rate is shown on screen: per room, per resource bar, and "empty in Ns"

## Phase 2: Failure and recovery

- Breakdowns on a timer, each with a clear red indicator
- Emergency tanks buy time instead of instant death
- Crew can repair rooms, with a short cooldown
- **Goal:** a mistake costs you something but never ends the run

## Phase 3: Expansion and depth

- Build new floors downward, each costing resources
- Each floor adds one new hazard: pressure leaks, then heat vents, then light-attracted creatures
- Add heat as the third resource once the first two feel solid

## Phase 4: Mobile polish

- Offline progress: the station keeps running gently while you're away, and damage is capped so closing the app never punishes you
- Save and load, short-session pacing, and a clear "what needs attention" summary on open

## Phase 5: Content

- More room types, crew traits, random events
- A goal or ending so runs feel like they're going somewhere

## Guardrails (fixing the Oxygen Not Included problems)

- Never more than 3 resources
- Every failure has a visible cause and a slow fallback
- No hidden systems, so you can always see why something broke
