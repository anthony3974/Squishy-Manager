# Deep-Sea Station

A portrait mobile management game about keeping an underwater research station alive. You see the station side-on, as a cutaway sunk in dark water. You balance oxygen and power by sending three crew to walk between rooms, and every failure is visible and recoverable.

This is the **Phase 1 prototype**. It answers one question: is keeping the station balanced fun for 5 minutes? See [PLAN.md](PLAN.md) for the full roadmap and the visual direction.

## Running it

- Unity **6000.0.85f1** (Unity 6), 2D URP template
- Open the project, open `Assets/Scenes/SampleScene`, and press Play
- Set the Game view to a portrait resolution such as 1080×1920

The station builds itself at startup (`StationBootstrap`), so the scene needs no setup, prefabs or imported art. Every shape is generated in code.

## How to play

- Tap a crew member, then tap a room. They walk there, climbing the ladder if needed, and get to work.
- Tapping a room with nobody selected picks whoever is in it. Tapping a full room swaps the two crew.
- Drag (or use the mouse wheel) to scroll up and down.

| Room | Effect (per crew, up to 2) |
| --- | --- |
| Generator | +3 power/s |
| Oxygen Scrubber | +4 O2/s, uses 2.5 power/s |
| Living Quarters | Crew regain stamina, each one uses 0.5 power/s |

- Each crew member breathes 0.8 O2/s wherever they are.
- Working drains stamina in about 33 s, and resting refills it in about 25 s, so crew must rotate.
- At zero stamina, crew keep working at half speed instead of stopping. An amber dot means tired; a flashing red dot means exhausted.
- A room flashes a red outline when it's failing: no power, or no crew while its resource is low.
- If oxygen runs out, a 20 s countdown starts. Get the scrubber running before it ends or the station is lost, and a new dive starts.

## Tuning

All balance numbers are in [`StationTuning.cs`](Assets/Scripts/Station/StationTuning.cs). During Play, select **Deep-Sea Station** in the Hierarchy to change them live.

## Code layout

All code is in `Assets/Scripts/Station/`:

| File | What it does |
| --- | --- |
| `Station.cs` | The simulation: resources, walking, stamina, room statuses. No rendering. |
| `StationModel.cs` | `Room` and `CrewMember` data |
| `StationLayout.cs` | World geometry: room boxes, crew slots, ladder paths |
| `StationTuning.cs` | Every balance number |
| `StationView.cs` | Draws the ocean, critters, rooms and crew, and handles taps |
| `StationCamera.cs` | Fits the camera to portrait, handles drag scrolling, and turns taps into world positions |
| `StationHud.cs` | The slim resource bar, warning line and hint |
| `UIKit.cs` | Generated sprites, UI helpers, safe-area fitter |
| `StationBootstrap.cs` | Spawns everything when you press Play |
