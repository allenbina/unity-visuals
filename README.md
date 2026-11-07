# Unity DJ visuals

Proof of concept: a beat-synced visual world cropped into three virtual panels on a second monitor, played against a separately running track. Full spec: [Unity-DJ-Visuals-Handoff.md](Unity-DJ-Visuals-Handoff.md).

Three worlds, switched with **1 / 2 / 3** or the buttons in the strip: **Space** and **Underwater**. Space is described next; Underwater is described after it.

Space world: **space** (`SpaceWorld.cs`), on **URP with HDR bloom**. Wired controls: BPM, Sync, Beat (engine nozzles ripple-flare on the beat), Warp, Build, Drop, Accent, Reset. Progress is rain-only for now. Switch `UseSpace` in `Bootstrap.cs` to run the rain world instead.

Always moving, no controls needed: a gas giant with a ring, a moon with two sub-moons, a second moon, an ice planet with a ring and moon, satellites crossing the frame, shooting stars, a drifting procedural nebula sky, and a sustained engine flame.

**Warp (W):** charge, white flash, star streaks, fisheye bend, chromatic aberration, big sustained blast. Dropping out of a real jump lands in the **next destination** (cycle: Home System > Ember Nebula > Twin Suns > Shard Field > Event Horizon), shown in the control strip. The transition is layered: the old region accelerates away and every object in it is **stretched along the direction of travel in proportion to its speed**; the warp tunnel runs empty; the next region comes in from far ahead as long streaks that relax into shape as it decelerates; an **iris** vignette closes and opens; a soft **light sweep** crosses all three panels; dark **foreground shards** whip past close to the camera for depth; and the arrival brightness swells in gently with no hard flash. Idea references: the layered 2.5D reveals in Sundered: Eldritch Edition (multiplane parallax, light reveals), used loosely, not copied.

**Build / Drop / Accent / Reset (space):** Build (Up/Down or slider) pre-stretches the stars and swells the engines without jumping. Drop (D) jumps to the next destination, then Build eases back to 0 (if you are in warp, Drop is the same as leaving warp). Accent (A) fires shooting stars and an engine surge, **moves the ship into a different panel (left / center / right) and swings the camera to a new angle** (cycles through 5 angles). Reset (R) returns to the Home System.

Models: the hero ship is Quaternius's **Executioner** (CC0) and the crossing satellites are **NASA** models (Aqua, CALIPSO, CubeSat, GOES, ISS, JWST, Juno). Sources and licences are in `Assets/Resources/Models/LICENSES.md`. Planets, nebula, stars, rings and all effects are generated in code (procedural shaders in `Assets/Shaders`). If the models are missing the code falls back to the old primitive ship and satellites.

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
- `Assets/Shaders/` procedural nebula sky, planet, ring, additive glow, accretion disc and photon ring shaders.
- `Assets/Editor/` URP auto-setup and the headless capture tool.
- `Assets/Scripts/ControlStrip.cs` panels and controls (IMGUI).
- `Assets/Scripts/Bootstrap.cs` assembles everything; `PanelCount` there sets the panel count.

## Asset dependencies

Packages: URP, glTFast + Draco for Unity (the NASA models are Draco-compressed GLB). Models are committed under `Assets/Resources/Models`. Shaders are found by name; for a standalone build add them to Always Included Shaders. Future worlds (see handoff) may use Kenney, Quaternius, or Synty Simple FX; download into `Assets/ThirdParty/` (git-ignored) and list them here.


## Underwater world (key 2)

No ship or vehicle: just the water. One art style throughout: **faceted low-poly** (flat-shaded facets from screen-space derivatives, bloom for the glow). Two scenes share the same geometry and swap looks: the dark **deep reef** and a bright, colorful **coral reef**.

- **Beat:** jellyfish stroke on the beat. Each one is assigned when it spawns: ~45% stroke on beats 1 and 3, ~45% on beats 2 and 4, ~10% free-run on their own tempo, with a few tens of milliseconds of jitter. Fish tails (and the manta's wings) are locked to the beat clock (some one flap per beat, some one per two beats, ~10% keep their own tempo) and the schools kick forward on every beat. Caustics on the seabed and the light shafts flash on the beat.
- **Accent (A):** a burst of faceted bubbles at a random spot on the seabed; the schools dart; a big animal glides through (whale, shark or manta in the deep, dolphin or manta on the reef).
- **Build (Up/Down or slider):** a rising current: schools speed up, more bubbles and marine snow, stronger light shafts.
- **Drop (D):** a **wall of bubbles** rises from the bottom of all three screens. Behind it the world swaps scenes (deep <-> coral reef). Arriving in the deep, a ring of light sweeps the seabed and lights the bioluminescent coral.
- **Reset (R):** back to the dark deep reef.

**Creatures:** the CC0 Quaternius animated fish pack (clownfish, angelfish, tuna-like fish, dolphin, shark, whale, manta ray; see `third_party/quaternius-fish/`) plus four reef fish generated in the same faceted style (blue tang, yellow tang, needlefish, pufferfish). `python tools/convert_fish.py` turns them into flat-colored meshes in `Assets/Resources/Models/Fish/` (nose +Z, 2 units long, wag coordinates in uv0). The tail swing and wing flap run in the vertex shader from the beat clock. Coral (branching, dome, table, sea fan, tube) and rocks are generated in code (`ReefMeshes.cs`).

Files: `Assets/Scripts/UnderwaterWorld.cs`, `Creatures.cs`, `ReefMeshes.cs`, `BubbleWall.cs` and the `DjFacet`, `DjBubble`, `DjSeabed`, `DjJelly`, `DjWaterSky`, `DjFog` shaders. The rain world still exists in the code but is no longer selectable.

## Cube world (key 3): a three-screen take on Beats 'n' Cubes

A copy-in-spirit of the 2007 Etienne de Crecy show (see `docs/beats-n-cubes-research.md` and `docs/beats-n-cubes-scenes.md`). Kept deliberately separate from the Space and Underwater worlds: it shares only the plumbing (beat clock, controls, post-processing).

**Exactly one cube per screen, always.** Each screen has its own camera looking straight at its own cube, so all three are seen identically. Each cube is a bold outer frame divided into a 3x3x3 grid of thinner glowing boxes, with bright dots at the joints and an open center column where the DJ stood, in a black void. The cubes turn, tilt, tumble and zoom in place; the cameras never move. The outer two cubes echo the center one a beat late.

- **Progress (P):** next phase. The 13 phases follow the 13 scenes of the reference video: dim red, red flood and a single white line, trace, reveal, look down, tumble, cells play, slide, extrude, dissolve, build, breathe, subdivide. Reset (R) goes back to phase 1.
- **Drop (D):** floods the cubes white. **Accent (A):** a bar of light sweeps across the three screens. **Build:** brighter lines and more camera drift.
- Everything is a function of the beat clock, so it stays in time at any BPM.

Files: `Assets/Scripts/CubeWorld.cs` and `Assets/Shaders/DjBoxEdge.shader`.
