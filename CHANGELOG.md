# Changelog

## Unreleased

- Missile smoke: every fired BDArmory missile leaves a thick white smoke trail. Cloud-textured puffs spread fast at first, then slowly, thin out with age and hang in the air (anchored to the planet, so KSP's scene shifts do not drag them along). Each puff is lit by the sky, the sun's colour and height, terrain in the sun's way and nearby burning flares. The trail starts at the nozzle and only once the motor ignites (a delayed ignition waits), and is full during boost, fading out over 4 s once the booster is spent (BDArmory's Boost to Cruise change), with no smoke after. The missile's own plume smoke is hidden while this trail is on (its flame stays). Drawn only; the missile's flight and guidance are BDArmory's.
- Missile smoke is cheap to run: colour refreshed every third frame, sun terms once per trail, shadow rays capped at 40 a frame, at most 12 trails at once.
- In-flight window: a "Missile smoke trails" toggle and a "Smoke density" slider, saved to `PluginData/user.cfg` with the other choices.
- Settings `missileSmoke`, `smokeDensity`, `smokeWidth`, `smokeGrowth` and `smokeSeconds` in `Settings.cfg`.

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

### Known issues

* **Chaff stutter while accelerating:** a chaff cloud can stutter as it falls away while the aircraft is accelerating. Chaff dropped at steady speed is smooth. The cause is under investigation
* **Tested on one setup:** KSP 1.12.5 with BDArmory 1.13.1.0. Other versions are not tested
