# Unity DJ visuals proof of concept

## Goal

Build a small, playable visual world in Unity Personal on the Windows HP Victus. Think of a game scene that responds to the performer and the music. The immediate experiment is on a second monitor with three virtual display panels, using free assets or Unity primitives. The measure of success is whether the visuals feel fun to play against a track.

Étienne de Crécy’s Beats ’n’ Cubes is the visual reference. Three panels are the working layout for this experiment. Keep the scene geometry, cues, and animation independent of panel count so a 3×3 layout can be explored later. Start with one wide camera view cropped into three windows. Leave alternate viewpoints and performer placement open.

## Controls for the first version

Put a small clickable control strip under the panels in Unity’s Game view and give the actions keyboard shortcuts. Play a track separately and enter its BPM manually. The keys and on-screen controls should call the same scene actions so MIDI or another input source can drive them later.

| Control | Behavior |
|---|---|
| **BPM** | Numeric input, including decimals. Unity emits a steady beat at this tempo once synced. At 120 BPM the interval is half a second. |
| **Sync / downbeat** | Press on the track’s first beat of a bar to start or re-align the visual beat clock. Show a small beat indicator so drift is easy to see. Manual BPM sets the rate, while Sync sets the phase. |
| **Beat** | Runs from the clock. A separate manual pulse is useful for auditioning an effect. |
| **Accent** | Fires a one-shot event on demand, such as a splash. Immediate playback is enough for the first test. A next-beat option can come later. |
| **Build / Drop** | A continuous Build slider controls tension. A separate Drop button triggers the payoff when the performer chooses. These are two controls for one musical gesture. |
| **Progression** | Advances the scene by adding something persistent. Try it every four or eight beats by hand. |
| **Reset** | Restores the opening scene so the same cues can be tested again. |

Start with one internal beat clock and preserve phase between frames. Manual sync is enough for a steady track. Tempo changes, live beat detection, deck metadata, and automatic musical phrasing are separate follow-up experiments.

## First scene and alternatives

**First test — rain.** A dark scene, visible droplets, and one blue virtual uplight are enough. On the beat, briefly swell the light so the droplets catch it. Accent fires a splash. Build thickens the rain. Drop sends a short sheet of water across the panels. Progression adds a layer that stays, such as runoff. Use basic particles and simple geometry. The first useful milestone is seeing a beat, a one-shot accent, and a persistent change behave differently.

| World | Beat | Accent | Build and drop | Progression |
|---|---|---|---|---|
| **Space** | Pulse the ship’s engine rings | Fire a thruster or send a satellite past | Stretch stars into streaks, jump across all three panels, and emerge in a colorful nebula | Add planets, satellites, escort ships, or distant galaxies |
| **Underwater** | Send a shimmer across the seabed | Release a burst of bubbles | Build toward a reveal of a luminous reef | Add schools of fish and larger silhouettes |
| **Waterslide** | Send a ripple through the water or open a roof segment | Throw a sheet of spray | Plunge over the big drop | Open adjacent lanes and add riders |
| **Stylized garden** | Ripple petals | Puff pollen or mushroom spores | Sweep color across the field | Add flowers, then rain, then insects |
| **Torchlit corridor** | Pulse the torchlight | Throw sparks | Open into a larger cavern | Add lit sconces or figures ahead |
| **Retro worlds** | Advance one maze tile or illuminate highway markers | Flip a tile or send a car past | Open into a checkerboard room or neon city | Unlock corridors or add traffic and skyline layers |
| **Heartbeat** | Pulse a stylized heart | Send a burst of red particles through a vessel | Reveal a wider network | Add vessel branches and flowing particles |

Space and underwater are the strongest next candidates. Space gets a big payoff from simple motion. Underwater has ready-made swim loops. A first-person torch view is simpler than syncing visible footfalls and stride length to tempo. A stylized heart is easier than a detailed anatomical flythrough.

## Free starting assets

- [Synty Simple FX](https://assetstore.unity.com/packages/vfx/particles/simple-fx-cartoon-particles-67834) provides fire, smoke, splashes, and explosions. Unity primitives and basic particles are sufficient for the rain test.
- [Kenney Space Kit](https://kenney.nl/assets/space-kit) supplies modular space props and ships.
- [Quaternius Animated Fish](https://quaternius.com/packs/animatedfish.html) includes swim clips. Unity still moves each fish through the scene. [Animated Cute Fish](https://quaternius.com/packs/cutefish.html) is a more cartoonish option.
- [Quaternius LowPoly Animated Monsters](https://quaternius.itch.io/lowpoly-animated-monsters) supplies flying, walking, and jumping clips. [Modular Dungeon](https://quaternius.com/packs/medievaldungeon.html) supplies torch props and walls. [Mixamo](https://www.mixamo.com/) is free with an Adobe account for humanoid motion.
- [Stylized Nature MegaKit](https://quaternius.itch.io/stylized-nature-megakit) has a free standard download with matching plants, flowers, and trees. The Unity URP source edition is paid.
- [Windows 95-style 3D Maze](https://github.com/bsimser/3DMaze) is a Unity reference project from the Unity 2021 era. [Kenney Racing Kit](https://kenney.nl/assets/racing-kit) and [Retro Urban Kit](https://kenney.nl/assets/retro-urban-kit) are other starting assets.
- [First Person Interaction Toolkit](https://github.com/stevenharmongames/Unity-FirstPersonInteractionToolkit) demonstrates a switch and a wheel with a continuous value. [Karbon](https://github.com/keijiro/Karbon) is a Unity 6.3 live music visual project using a camera feed and Launchpad controls. Both are references for later experiments.

## Later experiments

Keep Unity in charge of the scene. Live 3D objects, shaders, and particles can carry the action. Transparent sprites or pre-rendered video can add anime-style flashes, energy bursts, and other layers where useful. Live camera feeds are another possible visual input.

For hardware input, try [Minis](https://github.com/keijiro/Minis) with a USB MIDI fader or the DDJ-GRV6. [AlphaTheta documents the GRV6 MIDI output](https://pioneer-support.zendesk.com/hc/en-us/articles/37284057102489-DDJ-GRV6-MIDI-compatible-software). Test standalone before assuming it can be shared with rekordbox. Rekordbox beat or track data, Ableton cues, audio-reactive inputs, and a Teensy foot pedal or pulley can each be explored separately.

A later Unity build can put the control view on the laptop and a clean 1080p image on the second monitor. If physical lighting becomes interesting, reuse the virtual fixture’s color and intensity values as the starting point for DMX or Art-Net. One blue uplight can be enough. Projectors, mapping, moving heads, LED bars, and Depence are outside the first experiment.

## First implementation milestone

One editable Unity project opens in Play mode. Three panels show the same rain world. Set BPM, press Sync, verify the blue light follows the beat, then try Accent, Build, Drop, Progression, and Reset with a track playing separately. Include a short README with the tested Unity version, controls, and any asset dependencies. If the rain test feels playable, try the same controls in space or underwater.
