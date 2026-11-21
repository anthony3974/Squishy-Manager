# Deep-Sea Station

A portrait mobile management game about keeping an underwater research station alive. You see the station side-on, as a cutaway sunk in dark water. You balance oxygen and power by sending three crew to walk between rooms, and every failure is visible and recoverable.

This is **version 0.1.0**: Phases 1 and 2 plus a casual pass. Phase 1 answered "is keeping the station balanced fun for 5 minutes?" (yes). Phase 2 added failure and recovery: machines break down, crew repair them, and emergency oxygen tanks buy time. The casual pass made it relaxed: crew have standing jobs and rest on their own, everything runs about 2.5x slower, running out of air makes crew faint instead of ending the run, and the crew have traits, thoughts and small celebrations. See [PLAN.md](PLAN.md) for the full roadmap and the visual direction.

## Running it

- Unity **6000.0.85f1** (Unity 6), 2D URP template
- Open the project, open `Assets/Scenes/SampleScene`, and press Play
- Set the Game view to a portrait resolution such as 1080×1920

The station builds itself at startup (`StationBootstrap`), so the scene needs no setup, prefabs or imported art. Every shape is generated in code.

## How to play

- Tap a crew member, then tap a room to give them a **job** there. They walk over, climbing the ladder if needed, and keep that job until you change it. A small badge over their head shows the job: a rotor for the Generator, bubbles for the Scrubber, a moon for Quarters (off duty).
- Tapping a room with nobody selected picks whoever is in it, and tapping it again picks the next person. If two crew already have a job there, one of them swaps to the sender's old job.
- Drag (or use the mouse wheel) to scroll up and down.

### Auto-rest

- When a crew member's stamina drops to 20, they leave their job and rest in Living Quarters on their own. They finish a repair first unless they're completely exhausted.
- Once stamina is full, they walk back to their job.
- A machine whose crew is resting shows "Juno resting, back in ~12s". It turns amber only if its resource is low, and red only when nobody has that job at all.
- Quarters has 2 beds. If a third crew member needs rest, they keep working (at half speed once exhausted) and Quarters shows **BEDS FULL**. The most tired crew member gets the next free bed.
- Sending a resting crew member to a room pulls them out of bed early. If they're still tired, they work until exhausted before resting again, so your order isn't undone straight away.

| Room | Effect (per crew, up to 2) |
| --- | --- |
| Generator | +1.2 power/s |
| Oxygen Scrubber | +1.7 O2/s, uses 1 power/s |
| Living Quarters | Crew regain stamina, each one uses 0.2 power/s |

- Each crew member breathes 0.3 O2/s wherever they are.
- Crew work about 80 s before auto-rest sends them to bed, and rest about 32 s before heading back.
- At zero stamina, crew keep working at half speed instead of stopping. An amber dot means tired; a flashing red dot means exhausted.
- A room flashes a red outline when it's failing: no power, or no crew while its resource is low.

### Breakdowns and repairs

- The Generator and Scrubber wear out while crew work them, and break after 150–250 s of running time. Idle machines don't wear.
- For the last 10 s before a breakdown the machine rattles, shakes and puffs smoke, and the room says **RATTLING**.
- A broken machine stops, smokes and sparks, and its meter turns white to show repair progress.
- Crew in a broken room repair it on their own. One crew member takes about 8 s, two take about 4 s, and exhausted crew go at half speed. Repairing drains stamina a quarter faster than normal work.
- After finishing a repair, a crew member can't repair again for 12 s. A wrench and a shrinking bar above their head show the wait while they're in the room they fixed or at a broken machine.
- The room outline is red only when a broken machine has nobody on the way. Once crew are repairing it, or walking there, it turns amber.

### Emergency tanks

- Two oxygen canisters hang under the station, and pips beside OXYGEN in the top bar show them too.
- When oxygen hits 0, the fullest tank vents automatically, adding 40 O2 over 8 s.
- Tanks refill one at a time by drawing 0.4 O2/s from the air while oxygen is above 90%. The top bar's oxygen rate includes this.
- Only once every tank is empty does running out of oxygen start a 20 s countdown (**OUT OF AIR** in the top bar).

### Fainting (there is no game over)

- When the countdown ends, every crew member faints where they stand. Little stars circle their heads, and a bar above them fills as they come round.
- Fainted crew can't walk, work, repair or rest for 30 s. They keep their job and stamina, and the rooms they're in say **CREW FAINTED** with the cause and a wake-up time.
- When they wake, they get a fresh 20 s before fainting again. That's enough for the scrubber's crew to get air flowing. If nobody has the scrubber job, they faint again, so it's a setback that repeats until you fix it.
- While the air is out, the hint at the bottom names the fix: nobody on the scrubber, its crew member resting (send them early), the scrubber broken, or no power.

### The crew

| Crew | Trait |
| --- | --- |
| Mara | Tireless: tires 25% slower |
| Theo | Tinkerer: repairs 50% faster |
| Juno | Quick napper: rests 40% faster |

- Selecting a crew member shows their name under their feet and their trait in the bottom hint.
- Every 20–30 s or so, one crew member has a thought bubble. While something's wrong, thoughts come more often and usually hint at it ("air feels thin…", "that rattle doesn't sound good", "beds are full… so sleepy"). Otherwise it's small talk. All lines are in `CrewThoughts.cs`.
- Crew who aren't working idle: they breathe, tap a foot, glance around and stretch. That's off-duty crew who are fully rested (they get up and stand by their bunk), crew waiting at a broken machine, and crew at a scrubber with no power.

### Small rewards

- A finished repair throws a burst of sparkles from the machine, a "Fixed!" floats up, and the repairer is pleased with themselves.
- A short toast under the top bar marks minutes survived (2, 5, 10, 15, 20, 30, 45, 60, then every 30), an emergency tank refilling, and the air coming back after a crisis. Toasts sit below the warning line, so they never cover a warning.

## Building for Android

- With Unity closed, run `Tools/android-build.sh`. It builds `Builds/Android/DeepSeaStation.apk` (about 3 minutes) and writes a log to `Logs/android-build.log`. Add `--install` to install and launch it on a phone connected over USB.
- With Unity open, use **Tools > Android > Build APK** instead.
- The build script (`Assets/Editor/AndroidBuild.cs`) sets the Android settings every time: package `com.Tony3974.DeepSeaStation`, name "Deep-Sea Station", portrait only, IL2CPP, ARM64.
- To release, copy the APK to `~/APKs/` and run `~/fdroid/release.sh`, which publishes it to fdroid.tonycad.com.
- Before each new release, raise the version code (Player Settings > Android > Bundle Version Code). F-Droid only offers an update when the code goes up. 0.1.0 shipped as code 1.

## Tuning

All balance numbers are in [`StationTuning.cs`](Assets/Scripts/Station/StationTuning.cs). The **Pacing** block at the top holds everything that sets how fast the station runs (drains, breakdowns, production), with the old Phase 2 values in each tooltip. During Play, select **Deep-Sea Station** in the Hierarchy to change them live.

## Code layout

All code is in `Assets/Scripts/Station/`:

| File | What it does |
| --- | --- |
| `Station.cs` | The simulation: resources, jobs and auto-rest, walking, stamina, wear and repairs, emergency tanks, room statuses. No rendering. |
| `StationModel.cs` | `Room`, `CrewMember` and `EmergencyTank` data |
| `StationLayout.cs` | World geometry: room boxes, crew slots, ladder paths |
| `StationTuning.cs` | Every balance number, with the Pacing block at the top |
| `CrewThoughts.cs` | Thought bubble lines: hints about problems, trait lines, small talk |
| `StationView.cs` | Draws the ocean, critters, rooms, crew (poses, job badges, fainting, thought bubbles), breakdown effects, repair sparkles and the tank rack, and handles taps |
| `StationCamera.cs` | Fits the camera to portrait, handles drag scrolling, and turns taps into world positions |
| `StationHud.cs` | The slim resource bar with tank pips, warning line, milestone toast and hint |
| `UIKit.cs` | Generated sprites, UI helpers, safe-area fitter |
| `StationBootstrap.cs` | Spawns everything when you press Play |

The Android build script is separate, in `Assets/Editor/AndroidBuild.cs`, with a shell wrapper in `Tools/android-build.sh`.
