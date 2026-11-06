# Unity DJ visuals

Proof of concept: a beat-synced visual world cropped into three virtual panels on a second monitor, played against a separately running track. Full spec: [Unity-DJ-Visuals-Handoff.md](Unity-DJ-Visuals-Handoff.md).

Current world: **rain** (dark scene, one blue uplight). No third-party assets; everything is Unity primitives plus code-built particle systems.

## Status

Code written but **not yet opened in Unity**. Expect small compile or tuning fixes on first run. Tested Unity version: _fill in after first run_ (target: Unity 6 LTS, Built-in render pipeline, Unity Personal).

## Running it

1. Install Unity Hub, sign in, activate a free Personal license, install a Unity 6 LTS editor.
2. Unity Hub > Add > select this folder. Choose the editor version when prompted. (No template is needed; the project is code-only. If Unity asks, the project uses the Built-in render pipeline.)
3. Open it and press Play in any empty scene. `Bootstrap.cs` builds the whole experiment at runtime.
4. Maximize the Game view on the second monitor (16:9, e.g. 1920x1080).

## Controls

Buttons and keys call the same methods in `VisualActions`, so MIDI or other input can drive them later.

| Key | Control | What it does |
|---|---|---|
| (field) | BPM | Type a tempo (decimals OK), Enter. Phase is preserved when the rate changes. |
| Space | Sync | Press on the track's downbeat to start or re-align the clock. |
| B | Beat | Manual pulse (the clock fires the same event automatically). |
| A | Accent | One-shot splash. |
| Up / Down | Build | Thickens and speeds the rain. Also a slider. |
| D | Drop | Sheet of water sweeps across the panels; Build then releases. |
| P | Progression | Adds persistent runoff (puddle + stream). Stays until Reset. |
| R | Reset | Back to the opening scene. |

The beat indicator (four cells, downbeat brighter, plus a phase bar) shows drift.

## Layout

- `Assets/Scripts/BeatClock.cs` internal clock: BPM sets rate, Sync sets phase.
- `Assets/Scripts/VisualActions.cs` the one set of scene actions and events.
- `Assets/Scripts/RainWorld.cs` the rain world; reacts to the actions.
- `Assets/Scripts/ControlStrip.cs` panels and controls (IMGUI).
- `Assets/Scripts/Bootstrap.cs` assembles everything; `PanelCount` there sets the panel count.

## Asset dependencies

None yet. Future worlds (see handoff) may use Kenney, Quaternius, or Synty Simple FX; download into `Assets/ThirdParty/` (git-ignored) and list them here.
