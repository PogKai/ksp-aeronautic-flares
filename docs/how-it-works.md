# How it works

## Layout

The drawing is KSP-free. `FlareEnvironment` holds the world inputs (sun, daylight, up, air, screen height) as delegates, `KspEnvironment` fills them from KSP, `FlareState` and `ChaffCloud` are the per-effect state, `FlareRenderer` and `ChaffRenderer` draw. `FlareVisual`, `ChaffVisual`, `FlareDirector`, `FlareGui` and `BdaLink` are the KSP glue. `FlareSettings` is plain fields; `FlareSettingsStore` reads KSP's config files.

## Hook

BDArmory builds static pools of pooled objects: `CMDropper.flarePool` of `CMFlare` and `CMDropper.chaffPool` of `CMChaff`. Flares first. Every 0.2 s the add-on walks it and, on each pooled object, switches off the two `pEmitter2` flame emitters (as objects, because `CMFlare.EnableEmitters()` re-enables the emitters in its own list) and adds a `FlareVisual`. `pEmitter`, the smoke, is left alone. All BDArmory access is reflection (`BdaLink`), so there is no compile-time dependency. Pooled objects are reused, so state resets in `OnEnable`.

## Per flare, each frame

- Age over BDArmory's `lifeTime` (5 s) sets how far the grain has burnt, so the 2.9 s physical burn is stretched over BDArmory's life and the flame ends with the heat.
- Speed is BDArmory's `velocity`; air density is the same `FlightGlobals` lookup BDArmory uses for drag.
- `FlareModel` gives the flame's intensity and radius (`flare.ts`): band energy times mass rate, through the 2100 K graybody to candela, times the airstream share `1 / (1 + v/150 at sea-level density)`.
- Intensity over its starting value drives the flame, halo and light. KSP has no physical exposure, so brightness is relative; `brightness` scales it.
- The flame and halo are sized in screen pixels (`corePixels`, `glarePixels`, scaled to the screen height over 1440), so a flare stays a bright point from afar. The flame's physical radius only wins within about 9 m.
- Past `fullSizeDistance` the pixel size shrinks as 1 / sqrt(distance), as the light reaching the eye falls, down to `farShare`. Without that every flare is drawn the same size at 14 m and at 800 m.
- Halos are added together on screen, so ones that overlap dim each other by how much of them the others cover (`overlapDim`). A salvo seen from afar would otherwise add up to white.
- In daylight the halo, flame size and light shrink to `dayGlare`, `dayCore` and `dayLight`, by the sun's height at the flare.
- The last `fadeSeconds` of the life scale the flame, halo, light and trail down to nothing together. Without it the grain's burning surface drops from about a quarter of its starting size to nothing in one frame.
- The flare's light is set to `lightIntensity` and `lightRange` and forced to pixel lighting, so it lights the nearest parts of the aircraft.

## Trail

Light swept off the grain stays where it was left and cools: an e-fold of brightness every 0.05 s, three e-folds drawn. Points of the flare's path are kept for 0.15 s, shifted with KSP's floating origin, and drawn as a camera-facing ribbon whose colour follows the cooling graybody table (`trail_colors`). Its brightness follows the share the airstream sweeps off, so a slow flare has none.

## Drawing

One mesh for all flares, rebuilt each frame, with KSP's additive particle shader and a generated soft-disc texture. No asset bundles.

## Chaff

`CMChaff` throws one particle with a sprite, moves it with a heavy drag (it falls at about 20 m/s) and switches itself off after the emitter's lifetime. The add-on attaches a `ChaffVisual` to each pooled object, which hides the sprite by disabling its renderers (the emitter code, which keeps the object's life, is left running) and, when the object is activated, reads the release point and the velocity BDArmory set and starts a `ChaffCloud`. The cloud does not follow BDArmory's object after that: real chaff stays where it forms, and the object's 20 m/s fall would take it out of the frame.

- **Shape and spread** (`chaff.ts`, `smoke.ts`): the cloud forms over 0.2 s, laid along the aircraft's path (11 m at 244 m/s, scaled with speed), then sinks at the one-mil fibre's fall speed (Lamb's drag in Oseen's flow, 0.18 m/s at sea level) and spreads as sigma squared = sigma0 squared + epsilon t cubed / 2.
- **Haze:** the fibres' shadow is 1.77 m2, so the cloud's depth through its middle is that over 2 pi sigma-along sigma-across; it is drawn as an alpha-blended Gaussian, with extra off-centre lumps and thin streaks, from a generated atlas of noise-broken tiles so no edge is a clean curve. Its colour is the sky and sun, and any flare burning near it.
- **Glints:** each fibre lies close to level and tumbles at the air's turbulence rate. A mirror cylinder sends a light's rays to the eye only when its axis is square to h, half way between the light and the eye, so each glint flashes by exp(-(axis . h)^2 / 2 w^2). The width `chaffGlintWidth` is wider than the sun's real 0.0047 rad, so a cloud twinkles rather than showing a single flash a minute. The light is the sun by day and the brightest nearby flare at night.
- **Fade:** the cloud's depth falls as it spreads, and over the last `chaffFadeSeconds` (7 of 20) its haze and glints are scaled down to nothing together, as a flare's are. A cloud removed early (over the 40-cloud cap, or by the window's switch) is retired: it fades over 1.5 s, then goes.
- Two meshes carry it all, rebuilt each frame: an alpha-blended one for the haze and streaks, an additive one for the glints.

## The window

`FlareGui` adds a stock toolbar button in flight and an IMGUI window. A change raises `FlareSettings.Revision`; the director's 0.2 s scan then re-applies the mode to every pooled object: for flares it switches the two flame emitters and the lights back to BDArmory's own, for chaff it re-enables the sprite's renderers and clears the clouds. The choices are written to `PluginData/user.cfg` a moment after the last change.
