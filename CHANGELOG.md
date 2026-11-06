# Changelog

## 1.0.0

First release. Redraws BDArmory's flares with a physically based flame. Built against BDArmory 1.13.1.0 for KSP 1.12.5.

- Flame, halo, light and an airstream trail from aeronautic's MJU-7 flare model, driven by BDArmory's own flare age, speed and air density.
- Sizes in screen pixels, so a flare stays a bright point from far away, with glare that shrinks with distance and overlapping flares that dim each other.
- Daylight dimming of the glare and light, from the sun's height at the flare.
- A burn-out fade over the last 0.8 s of the flare's life.
- A light strong enough to light the nearest parts of the aircraft at night.
- Settings in `Settings.cfg`. No shaders or asset bundles.
