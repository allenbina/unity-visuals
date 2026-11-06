# Unity DJ visuals

Proof of concept: a beat-synced visual world cropped into three virtual panels on a second monitor, played against a separately running track. Full spec: [Unity-DJ-Visuals-Handoff.md](Unity-DJ-Visuals-Handoff.md).

Current world: **space** (`SpaceWorld.cs`), on **URP with HDR bloom**. Wired controls: BPM, Sync, Beat (engine rings ripple-flare on the beat) and **Warp** (W). The other controls are hidden for now. Switch `UseSpace` in `Bootstrap.cs` to run the rain world instead.

Always moving, no controls needed: a gas giant with a ring, a moon with two sub-moons, a second moon, an ice planet with a ring and moon, satellites crossing the frame, shooting stars, a drifting procedural nebula sky, and a sustained engine flame.

**Warp (W):** charge, white flash, star streaks, fisheye bend, chromatic aberration, big sustained blast; press again to drop out.

Everything is generated in code (primitives, particles, procedural shaders in `Assets/Shaders`). No third-party assets.

## Status

Compiles and renders on Unity **6000.0.85f2** (URP 17.0.4). Verified headless by screenshot, not yet by ear: the beat sync and feel against a real track still need a human test. Use `DjVisuals > Ensure URP` if the pipeline assets are ever missing.

Headless screenshot test (writes PNGs of the wide render through a scripted beat + warp timeline):
`Unity.exe -batchmode -projectPath . -executeMethod DjVisuals.EditorTools.CaptureTool.Run -djcapture <outDir>`

## Running it

1. Install Unity Hub, sign in, activate a free Personal license, install Unity 6000.0.85f2.
2. Unity Hub > Add > select this folder. Choose the editor version when prompted. (No template is needed; the project is code-only. If Unity asks, the project uses the Built-in render pipeline.)
3. Open it (first open creates the URP assets) and press Play in any empty scene. `Bootstrap.cs` builds the whole experiment at runtime.
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
| W | Warp (space) | Toggle warp speed. |
| D | Drop | Sheet of water sweeps across the panels; Build then releases. |
| P | Progression | Adds persistent runoff (puddle + stream). Stays until Reset. |
| R | Reset | Back to the opening scene. |

The beat indicator (four cells, downbeat brighter, plus a phase bar) shows drift.

## Layout

- `Assets/Scripts/BeatClock.cs` internal clock: BPM sets rate, Sync sets phase.
- `Assets/Scripts/VisualActions.cs` the one set of scene actions and events.
- `Assets/Scripts/SpaceWorld.cs` the space world; `RainWorld.cs` the rain world.
- `Assets/Scripts/PostFx.cs` bloom, lens distortion, chromatic aberration (built in code).
- `Assets/Shaders/` procedural nebula sky, planet, ring, additive glow shaders.
- `Assets/Editor/` URP auto-setup and the headless capture tool.
- `Assets/Scripts/ControlStrip.cs` panels and controls (IMGUI).
- `Assets/Scripts/Bootstrap.cs` assembles everything; `PanelCount` there sets the panel count.

## Asset dependencies

None. Shaders are found by name; for a standalone build add them to Always Included Shaders. Future worlds (see handoff) may use Kenney, Quaternius, or Synty Simple FX; download into `Assets/ThirdParty/` (git-ignored) and list them here.
