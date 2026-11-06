# TODO

Not in priority order unless noted.

## Next: space world (in progress)
- [x] Beat: engine rings ripple-flare (written, compiles, not yet seen running)
- [ ] Accent: thruster burst or satellite pass
- [ ] Build: star streaks + camera push-in
- [ ] Drop: impact-frame flash, warp, nebula reveal
- [ ] Progression: planets / escort ships / galaxies that persist until Reset
- [x] URP + bloom, procedural nebula sky, orbiting planets/moons/sub-moons, satellites, shooting stars
- [x] Warp: toggle with W (blast, star streaks, fisheye, chromatic aberration)
- [ ] Swap primitive ship and satellites for real models (Kenney Space Kit CC0, NASA 3D, or TRELLIS-generated)
- [ ] Real nebula/planet textures (NASA/ESA public-domain imagery) instead of procedural
- [ ] Tie warp ramp to Build/Drop controls; beat-lock the star streak pulses

## Later: jellyfish world (not a priority)

Lots of jellyfish swimming, mostly on the beat but not all of them.

**Stroke schedule.** Each jellyfish gets one of these when it spawns:
- ~45% beats 1 and 3 (every other beat, offset 0)
- ~45% beats 2 and 4 (every other beat, offset 1)
- ~10% off-beat: own free-running tempo and random phase, so the school feels natural
- Optional later variants: every beat, or once per bar on the downbeat

Each jellyfish listens to the shared beat clock (`BeatInBar`, 0-3) and fires a stroke when its pattern matches. Off-beat ones ignore the clock. Add a little jitter (tens of ms) to the on-beat ones so they are not machine-perfect.

**Stroke motion.** A stroke is a fast bell contraction followed by a slow relaxation, with a forward surge that decays between strokes and a slight drift/sink in between. Tentacles trail and lag behind.

**Assets.** A search found no free CC0 animated 3D jellyfish for Unity. What turned up is paid or uses custom licenses:
- [Low-poly animated jellyfish (CGTrader)](https://www.cgtrader.com/3d-models/animal/fish/low-poly-animated-jellyfish): about $22, rigged with several swim clips, royalty-free licence
- [Jellyfish Pack (Super Hive Market)](https://superhivemarket.com/products/jellyfish-pack-4): paid
- [Compass Jellyfish (Sketchfab)](https://sketchfab.com/3d-models/jellyfish-compass-red-unreal-unity-asset-fb80656f608a4bde91934353d4510ea4): Unity asset with 10 clips including a pulse; licence not confirmed
- [Primary Jellyfishes (itch.io, rapidpunches)](https://rapidpunches.itch.io/primary-jellyfishes/purchase): pay-what-you-want, 3D contents unconfirmed
- Not found: a Quaternius jellyfish. The [Quaternius animated fish pack](https://quaternius.itch.io/lowpoly-animated-fish) is CC0 but I did not confirm it contains one.

**Recommendation: build them procedurally.** Baked swim clips play at a fixed length, which fights per-jellyfish beat timing. A procedural jellyfish (a bell mesh squashed by a stroke curve, plus trailing tentacles via line renderers or a vertex shader) lets us trigger each stroke exactly on a beat and scale to dozens of them cheaply. It also needs no downloads. Use a purchased or downloaded model only if a hand-animated look is wanted, and then retime its clip to the BPM.

Open questions: glowing bioluminescent look (emissive, dark water) vs. bright stylised; how many jellyfish on screen.
