# Deep-Sea Station

A portrait mobile management game about keeping an underwater research station alive. You balance oxygen and power by moving three crew between rooms, and every failure is visible and recoverable.

This is the **Phase 1 prototype**. It answers one question: is keeping the station balanced fun for 5 minutes? See [PLAN.md](PLAN.md) for the full roadmap.

## Running it

- Unity **6000.0.85f1** (Unity 6), 2D URP template
- Open the project, open `Assets/Scenes/SampleScene`, and press Play
- Set the Game view to a portrait resolution such as 1080×1920

The station builds itself at startup (`StationBootstrap`), so the scene needs no setup and no prefabs.

## How to play

1. Tap a crew member (their token in a room or their card at the bottom).
2. Tap a room to send them there. They take about a second to walk over.
3. Tapping a full room swaps the two crew.

| Room | Effect |
| --- | --- |
| Generator | +3 power/s while staffed |
| Scrubber | +4 O2/s while staffed, uses 2.5 power/s |
| Quarters | Crew regain stamina, each one uses 0.5 power/s |

- Each crew member breathes 0.8 O2/s wherever they are.
- Working drains stamina in about 33 s, and resting refills it in about 25 s, so crew must rotate.
- At zero stamina, crew keep working at half speed instead of stopping.
- A room flashes red when it's failing: a scrubber with no power, or an empty scrubber or generator while its resource is low.
- If oxygen runs out, a 20 s countdown starts. Get a scrubber running before it ends or the station is lost.

## Tuning

All balance numbers are in [`StationTuning.cs`](Assets/Scripts/Station/StationTuning.cs). During Play, select **Deep-Sea Station** in the Hierarchy to change them live.

## Code layout

All code is in `Assets/Scripts/Station/`:

| File | What it does |
| --- | --- |
| `Station.cs` | The simulation: resources, rooms, crew, statuses. No UI code. |
| `StationModel.cs` | `Room` and `CrewMember` data |
| `StationTuning.cs` | Every balance number |
| `StationUI.cs` | Builds and updates the whole uGUI screen from code |
| `UIKit.cs` | Generated sprites, UI helpers, safe-area fitter |
| `StationBootstrap.cs` | Spawns the station, UI, and EventSystem when you press Play |
