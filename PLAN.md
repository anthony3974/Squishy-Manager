# Deep-Sea Station: Build Plan

**Core idea:** Expand a research station downward, room by room. Three resources (oxygen, power, heat), visible failures, and slow fallbacks so one mistake never snowballs.

**Current status:** Phases 1 and 2 are built and playtested (removed from this file; see `git log -p PLAN.md`). The casual pass and the first part of Phase 3 (crew-operated building/digging, 2-crew repairs, and floor-by-floor depth hazards) are built as version 0.2.0 (code 2) and awaiting playtest. Heat as the 3rd resource and playtesting Phase 3 are next.

## Casual pass (between Phases 2 and 3 — built, awaiting playtest)

The loop worked but was too stressful: constantly rotating crew to keep two meters up is plate-spinning. This pass makes the game handle the boring upkeep so the player only makes interesting choices. It changes behavior, not visuals, and adds no new floors, hazards or resources.

1. **Auto-rest** ✅ built. Tired crew (stamina 20) walk to Quarters on their own and go back to their job once rested.
2. **Set-a-job assignment** ✅ built. A tap sets a standing job that lasts through rests and repairs. A badge over each crew member's head shows it.
3. **Slower pacing** ✅ built. A Pacing block at the top of `StationTuning.cs`: drains, breakdowns and production are all about 2.5x slower, so the balance between rooms is unchanged. In a rough model, the starting jobs stay healthy untouched for 20 minutes (oxygen never below about 58), while a station with nobody on the scrubber takes about 3 minutes to run out of air instead of about 1.
4. **Softer failure** ✅ built. No game over and no "station lost" restart. With both tanks empty at 0 O2, a 20 s countdown runs, then every crew member faints for 30 s (circling stars, a wake-up bar, **CREW FAINTED** on the room) and wakes with a fresh 20 s grace period. The bottom hint names the cause.
5. **Crew feel like people** ✅ built. Each crew member has one trait with a small, shown effect (Mara Tireless, Theo Tinkerer, Juno Quick napper). One thought bubble at a time, mostly hints while something's wrong and small talk otherwise. Crew who aren't working idle (foot tap, glance, stretch), and fully rested off-duty crew get up out of bed.
6. **Small rewards** ✅ built. Sparkle burst and a floating "Fixed!" on every repair. Calm toasts for minutes survived, tanks refilled, and air restored. No menus.

## Phase 3: Expansion and depth (in progress)

- Build new floors downward, each costing power; **crew must walk to the site to build and dig** ✅ built
- **Team repairs:** broken machines require a second crew member to help repair, so breakdowns ask for a real player decision instead of self-healing ✅ built
- Each deeper floor adds one new hazard ✅ built:
  - Floor 3+: **pressure leaks** (drains O2 until sealed)
  - Floor 4+: **heat vents** (rapid fatigue and machine wear until cooled)
  - Floor 5+: **light-attracted creatures** (clings to lit windows and drains power until driven off)
- Add heat as the third resource once the first two feel solid
- **Maybe plan (active goal room):** add a progress room (e.g., Research Lab, Core Drill, or Dive Pod) that competes for the 3rd crew member—keeping survival calm while making progress requires active shuffling between more rooms/tasks than crew.

## Phase 4: Mobile polish

- Offline progress: the station keeps running gently while you're away, and damage is capped so closing the app never punishes you
- Save and load, short-session pacing, and a clear "what needs attention" summary on open

## Phase 5: Content

- More room types, more crew traits (the first three came in the casual pass), random events
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
