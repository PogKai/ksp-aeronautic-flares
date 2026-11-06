# Changelog

## 1.0.0

First release. Redraws BDArmory's flares and chaff with physically based models ported from aeronautic's countermeasures module. Built against BDArmory 1.13.1.0 for KSP 1.12.5.

- Flare flame, halo, light and an airstream trail, driven by BDArmory's own flare age, speed and air density. The white-hot core streams back along the airstream (`flameStreak`), more by day.
- Sizes in screen pixels, so a flare stays a bright point from far away, with glare that shrinks with distance and overlapping flares that dim each other.
- Daylight dimming of the glare and light, from the sun's height at the flare.
- A burn-out fade over the last 0.8 s of the flare's life.
- A light strong enough to light the nearest parts of the aircraft at night.
- Chaff. BDArmory's chaff sprite is replaced by a faint, wispy cloud: it blooms open in a fifth of a second along the path the aircraft flew, then hangs where it formed, sinking and spreading in the turbulence. Fibres glint where they catch the sun or a nearby flare. It fades to nothing over the last 7 s of its 20, and a cloud taken out early fades over 1.5 s.
- An in-flight window, from a button on the stock toolbar, to switch flares or chaff back to BDArmory's own effects at once, and to set their brightness. Choices are kept in `PluginData/user.cfg`.
- Settings in `Settings.cfg`. No shaders or asset bundles.
