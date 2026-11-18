# Deep-Sea Station: Build Plan

**Core idea:** Expand a research station downward, room by room. Three resources (oxygen, power, heat), visible failures, and slow fallbacks so one mistake never snowballs.

**Current status:** Phase 1 built, being playtested. Nothing past Phase 1 gets built until the core loop feels good.

## Phase 1: Prototype (the fun test) ✅ built

- Unity, portrait mode, tested on a phone along the way
- A grid of rooms with 3 types: Living Quarters, Scrubber (oxygen), Generator (power)
- 3 crew you tap to assign to rooms
- Oxygen and power tick up and down; a red warning shows which room is failing
- **Goal:** is keeping the station balanced fun for 5 minutes?

What was built (side-on cutaway version):

- Side-on cutaway of the station in dark teal water, with drifting bioluminescent critters. Drag to scroll up and down.
- 3 rooms on 2 floors linked by a ladder shaft: Living Quarters and Oxygen Scrubber on top, Generator below. Each room holds 2 crew.
- Crew walk along the floors and climb the ladder. They lean into the machines while working and lie in the bunks while resting.
- Each room shows a meter and its live rate above it, and flashes a red outline when it's failing.
- Stamina drains while working and refills in Quarters, so the 3 crew have to rotate. Exhausted crew keep working at half speed instead of stopping.
- Running out of oxygen starts a 20 s countdown, so you can still recover. If it runs out, a new dive starts automatically. There are no menus.

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

## Visual direction

- Side-on cutaway like an ant farm: a vertical stack of floors, rooms side by side, each a visible box with floor, walls and a glowing window
- Portrait, 2D, flat simple shapes. The camera scrolls vertically, and new floors appear below.
- Ocean outside gets darker and bluer with depth, with faint drifting bioluminescent creatures
- Crew are small visible characters who walk the floors and use ladders between levels, and look busy while working
- Each room shows its status on itself: a small meter above it and a red flashing outline when failing
- Oxygen and power only, in a slim bar at the top. No resource sliders.
- Deep blue and teal outside, warm amber light spilling from the rooms, red only for warnings
- No menus or upgrade screens until asked for

## Guardrails (fixing the Oxygen Not Included problems)

- Never more than 3 resources
- Every failure has a visible cause and a slow fallback
- No hidden systems, so you can always see why something broke
