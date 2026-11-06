# How it works

## Hook

BDArmory builds a static `CMDropper.flarePool` of pooled `CMFlare` objects. Every 0.2 s the add-on walks it and, on each pooled object, switches off the two `pEmitter2` flame emitters (as objects, because `CMFlare.EnableEmitters()` re-enables the emitters in its own list) and adds a `FlareVisual`. `pEmitter`, the smoke, is left alone. All BDArmory access is reflection (`BdaLink`), so there is no compile-time dependency. Pooled objects are reused, so state resets in `OnEnable`.

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

## Testing

`dotnet build -c Dev` adds `Source/Dev/FlareProbe.cs`. With `GameData/AeronauticFlares/PluginData/probe.txt` present it logs each live flare's distance and on-screen size every half second. The word `night` in the file moves the clock to night, and `cam` takes over the flight camera at the first flare: a still camera ahead of and below the drop, on the side away from the sun, to watch the flares burn out. A flare is thrown some 400 m on along its heading and falls about 200 m, so cameras that ride with the aircraft lose it within seconds.
