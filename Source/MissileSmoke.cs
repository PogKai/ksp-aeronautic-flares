using System.Collections.Generic;
using UnityEngine;

namespace AeronauticFlares
{
    // A smoke trail behind every fired BDArmory missile: cloud-textured puffs that spread and thin with age and hang in the air
    // (anchored to the planet, so KSP's scene shifts do not drag them along). Each puff is lit by the sky, the sun's colour and
    // height, terrain in the sun's way and any flare burning nearby. Cosmetic: BDArmory keeps the missile's flight and guidance.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class MissileSmoke : MonoBehaviour
    {
        sealed class Trail
        {
            public GameObject Object;
            public ParticleSystem Particles;
            public Vector3 Last;
            public bool HasLast;
            public float Age, CruiseAge, NextHide;
            public readonly List<Renderer> Hidden = new List<Renderer>();
            public readonly HashSet<Renderer> Seen = new HashSet<Renderer>();
            public readonly List<Transform> Nozzles = new List<Transform>();
            public bool Ignited;
        }

        const int MaxParticles = 6000;
        const int LightStride = 3, RaysPerFrame = 40, MaxTrails = 12;
        const float BoostSeconds = 3.5f, CruiseFadeSeconds = 4f, Spacing = 0.6f, GlowRange = 45f;

        readonly Dictionary<Vessel, Trail> trails = new Dictionary<Vessel, Trail>();
        readonly List<KeyValuePair<Trail, float>> fading = new List<KeyValuePair<Trail, float>>();
        readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[MaxParticles + 2000];
        struct ShadowEntry { public float Value; public int Frame; }
        readonly Dictionary<uint, ShadowEntry> shadowCache = new Dictionary<uint, ShadowEntry>();
        readonly List<Vessel> gone = new List<Vessel>();
        int raysLeft;
        readonly List<Vector4> glows = new List<Vector4>();
        Material material;
        Matrix4x4 previousBody, shift = Matrix4x4.identity;
        bool havePrevious;
        float nextScan;
        int frame;

        void Awake()
        {
            FlareSettingsStore.Load();
            if (!BdaLink.MissileAvailable)
            {
                enabled = false;
                return;
            }
            var shader = Shader.Find("KSP/Particles/Alpha Blended");
            if (shader == null)
            {
                enabled = false;
                return;
            }
            material = new Material(shader);
            material.SetTexture("_MainTex", Puff());
        }

        void OnDestroy()
        {
            foreach (var trail in trails.Values) if (trail.Object != null) Destroy(trail.Object);
            foreach (var pair in fading) if (pair.Key.Object != null) Destroy(pair.Key.Object);
            if (material != null) Destroy(material);
        }

        void LateUpdate()
        {
            frame++;
            raysLeft = RaysPerFrame;
            TrackPlanet();
            if (FlareSettings.MissileSmoke && Time.time >= nextScan)
            {
                nextScan = Time.time + 0.1f;
                foreach (var vessel in FlightGlobals.VesselsLoaded)
                    if (trails.Count < MaxTrails && !trails.ContainsKey(vessel) && BdaLink.IsFiredMissile(vessel))
                    {
                        trails[vessel] = Create();
                        Debug.Log("[AeronauticFlares] missile smoke on " + vessel.vesselName);
                    }
            }
            if (trails.Count + fading.Count == 0) return;
            if (frame % 6 == 0) RefreshGlows();

            gone.Clear();
            foreach (var pair in trails)
            {
                var vessel = pair.Key;
                var trail = pair.Value;
                if (vessel == null || !vessel.loaded) { gone.Add(vessel); continue; }
                if (FlareSettings.MissileSmoke)
                {
                    HideStockSmoke(vessel, trail);
                    Emit(vessel, trail);
                }
                else ShowStockSmoke(trail);
                Relight(trail);
            }
            foreach (var vessel in gone)
            {
                var trail = trails[vessel];
                trails.Remove(vessel);
                fading.Add(new KeyValuePair<Trail, float>(trail, Time.time + FlareSettings.SmokeSeconds + 1f));
            }
            for (int i = fading.Count - 1; i >= 0; i--)
            {
                var trail = fading[i].Key;
                if (Time.time > fading[i].Value || trail.Object == null)
                {
                    if (trail.Object != null) Destroy(trail.Object);
                    fading.RemoveAt(i);
                }
                else Relight(trail);
            }
        }

        // KSP shifts the whole scene (floating origin, Krakensbane) and the planet turns: the puffs move with the planet's frame
        void TrackPlanet()
        {
            var body = FlightGlobals.currentMainBody;
            if (body == null || body.bodyTransform == null) return;
            Matrix4x4 now = body.bodyTransform.localToWorldMatrix;
            shift = havePrevious ? now * previousBody.inverse : Matrix4x4.identity;
            previousBody = now;
            havePrevious = true;
            foreach (var trail in trails.Values) trail.Last = shift.MultiplyPoint3x4(trail.Last);
        }

        Trail Create()
        {
            var go = new GameObject("AeronauticFlares missile smoke");
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = FlareSettings.SmokeSeconds;
            main.startSpeed = 0.4f;
            main.maxParticles = MaxParticles;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.9f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.2f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            // Fast spread at first, slowing as the smoke diffuses
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.10f), new Keyframe(0.08f, 0.32f), new Keyframe(0.35f, 0.66f), new Keyframe(1f, 1f)));
            // Colour comes from the lighting; over life only the opacity changes, dense and bright at first
            var colour = ps.colorOverLifetime;
            colour.enabled = true;
            var gradient = new Gradient();
            gradient.colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            gradient.alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.2f), new GradientAlphaKey(0.45f, 0.55f), new GradientAlphaKey(0f, 1f) };
            colour.color = gradient;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingFudge = 2f;
            ps.Play();
            return new Trail { Object = go, Particles = ps };
        }

        // The missile's own plume smoke (emitters drawn with a smoke texture) is hidden while this trail runs; the flame stays.
        // Renderers are switched off rather than the emitters, since BDArmory re-enables emitters in its own lists.
        void HideStockSmoke(Vessel vessel, Trail trail)
        {
            if (Time.time < trail.NextHide) return;
            trail.NextHide = Time.time + 0.25f;
            foreach (var emitter in vessel.GetComponentsInChildren<KSPParticleEmitter>(true))
            {
                var renderer = emitter.GetComponent<Renderer>();
                if (renderer == null || !trail.Seen.Add(renderer)) continue;
                trail.Nozzles.Add(emitter.transform);
                var texture = renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;
                bool smoke = (texture != null && texture.name.IndexOf("smoke", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    || emitter.name.IndexOf("smoke", System.StringComparison.OrdinalIgnoreCase) >= 0;
                Debug.Log("[AeronauticFlares] missile emitter " + emitter.name + " texture " + (texture != null ? texture.name : "none") + (smoke ? " hidden" : " kept"));
                if (smoke) trail.Hidden.Add(renderer);
            }
            foreach (var renderer in trail.Hidden) if (renderer != null) renderer.enabled = false;
        }

        // Smoke switched off in the window while a missile flies: its own plume comes back
        static void ShowStockSmoke(Trail trail)
        {
            foreach (var renderer in trail.Hidden) if (renderer != null) renderer.enabled = true;
            trail.Hidden.Clear();
            trail.Seen.Clear();
            trail.Nozzles.Clear();
        }

        // The rearmost of the missile's exhaust emitters, so puffs start at the nozzle; the missile's centre if it has none
        static Vector3 NozzlePosition(Vessel vessel, Trail trail, Vector3 direction)
        {
            Vector3 best = vessel.GetWorldPos3D() - direction * 0.9f;
            float rearmost = float.MinValue;
            foreach (var nozzle in trail.Nozzles)
            {
                if (nozzle == null || !nozzle.gameObject.activeInHierarchy) continue;
                float behind = -Vector3.Dot(nozzle.position, direction);
                if (behind <= rearmost) continue;
                rearmost = behind;
                best = nozzle.position;
            }
            return best;
        }

        // New puffs along the path the missile flew since the last frame. Smoke only while the motor burns: full during boost,
        // fading out over CruiseFadeSeconds once the booster is spent, none before ignition (a delayed motor) or after burnout.
        void Emit(Vessel vessel, Trail trail)
        {
            string motor = BdaLink.MotorState(vessel);
            if (motor != null && motor != "Boost" && motor != "Cruise")
            {
                trail.HasLast = false;
                return;
            }
            float strength = 1f;
            if (motor == "Cruise")
            {
                trail.CruiseAge += Time.deltaTime;
                strength = 1f - trail.CruiseAge / CruiseFadeSeconds;
                if (strength <= 0f)
                {
                    trail.HasLast = false;
                    return;
                }
            }
            else if (motor == null)
            {
                // State unreadable: smoke throughout, thinner after the usual boost time
                trail.Age += Time.deltaTime;
                if (trail.Age > BoostSeconds) strength = 0.55f;
            }
            if (!trail.Ignited)
            {
                trail.Ignited = true;
                Debug.Log("[AeronauticFlares] missile smoke starts at ignition on " + vessel.vesselName);
            }
            Vector3 velocity = vessel.srf_velocity;
            Vector3 direction = velocity.sqrMagnitude > 1f ? velocity.normalized : vessel.transform.forward;
            Vector3 position = NozzlePosition(vessel, trail, direction);
            if (trail.HasLast)
            {
                float distance = (position - trail.Last).magnitude;
                int count = Mathf.Clamp(Mathf.CeilToInt(distance / Spacing), 1, 60);
                float width = FlareSettings.SmokeWidth * FlareSettings.SmokeGrowth * (0.5f + 0.5f * strength);
                float alpha = Mathf.Clamp01(0.8f * FlareSettings.SmokeDensity * strength);
                float life = FlareSettings.SmokeSeconds * (0.6f + 0.4f * strength);
                for (int i = 1; i <= count; i++)
                {
                    var emit = new ParticleSystem.EmitParams();
                    emit.position = Vector3.Lerp(trail.Last, position, (float)i / count);
                    emit.velocity = Random.insideUnitSphere * 0.5f;
                    emit.startSize = width;
                    emit.startLifetime = life;
                    emit.startColor = new Color(1f, 1f, 1f, alpha);
                    trail.Particles.Emit(emit, 1);
                }
            }
            trail.Last = position;
            trail.HasLast = true;
        }

        // Burning flares near the trail light the smoke warm
        void RefreshGlows()
        {
            glows.Clear();
            var director = FlareDirector.Instance;
            if (director == null) return;
            foreach (var visual in director.Active)
            {
                var state = visual.State;
                if (!state.Lit) continue;
                float night = 1f - 0.7f * FlareEnvironment.Daylight(state.Position);
                glows.Add(new Vector4(state.Position.x, state.Position.y, state.Position.z, 1.4f * night * FlareSettings.Brightness));
            }
        }

        // Each puff takes its colour from what lights it: sky, sun (colour, height, terrain in the way) and nearby flares.
        // Every puff moves with the planet each frame, but its colour is refreshed only every LightStride frames (new puffs at once),
        // and the sun terms, the same along a trail, are worked out once per trail.
        void Relight(Trail trail)
        {
            int n = trail.Particles.GetParticles(buffer);
            if (n == 0) return;
            for (int i = 0; i < n; i++) buffer[i].position = shift.MultiplyPoint3x4(buffer[i].position);

            Vector3 head = buffer[0].position;
            Vector3 sun = FlareEnvironment.ToSun(head);
            float height = Vector3.Dot(sun, FlareEnvironment.Up(head));
            float day = Smooth(-0.12f, 0.25f, height);
            float direct = Smooth(-0.03f, 0.30f, height);
            Color sunColour = Color.Lerp(new Color(1f, 0.80f, 0.62f), new Color(1f, 0.97f, 0.92f), Smooth(0.02f, 0.45f, height));
            Color sky = new Color(0.50f, 0.52f, 0.56f) * (0.45f + 0.55f * day);
            Color sunLight = sunColour * (direct * 0.85f);
            uint turn = (uint)(frame % LightStride);
            float range2 = GlowRange * GlowRange;

            for (int i = 0; i < n; i++)
            {
                Color32 old = buffer[i].startColor;
                bool fresh = old.r == 255 && old.g == 255 && old.b == 255;
                uint seed = buffer[i].randomSeed;
                if (!fresh && (seed + turn) % LightStride != 0) continue;
                Vector3 position = buffer[i].position;
                Color lit = sky + sunLight * (direct > 0f ? Shadow(seed, position, sun) : 0f);
                lit *= 0.90f + 0.10f * ((seed * 2654435761u >> 8) % 1000 / 1000f);
                for (int g = 0; g < glows.Count; g++)
                {
                    Vector4 glow = glows[g];
                    float dx = glow.x - position.x, dy = glow.y - position.y, dz = glow.z - position.z;
                    float d2 = dx * dx + dy * dy + dz * dz;
                    if (d2 >= range2) continue;
                    float f = 1f - Mathf.Sqrt(d2) / GlowRange;
                    lit += FlareState.FlameColor * (f * f * glow.w * 0.9f);
                }
                buffer[i].startColor = new Color32((byte)(Mathf.Clamp01(lit.r) * 255), (byte)(Mathf.Clamp01(lit.g) * 255), (byte)(Mathf.Clamp01(lit.b) * 255), old.a);
            }
            trail.Particles.SetParticles(buffer, n);
            if (shadowCache.Count > 12000) shadowCache.Clear();
        }

        // 1 in the sun, less where terrain or an aircraft stands between a puff and the sun. Cached per puff for 30 frames, and at
        // most RaysPerFrame rays a frame across all trails; a puff without a ray to spare keeps its last value (full sun if none)
        float Shadow(uint id, Vector3 position, Vector3 sun)
        {
            ShadowEntry entry;
            bool known = shadowCache.TryGetValue(id, out entry);
            if (known && (frame - entry.Frame < 30 || raysLeft <= 0)) return entry.Value;
            if (!known && raysLeft <= 0) return 1f;
            raysLeft--;
            RaycastHit hit;
            entry.Value = Physics.Raycast(position, sun, out hit, 4000f) && hit.distance > 6f ? 0.55f : 1f;
            entry.Frame = frame;
            shadowCache[id] = entry;
            return entry.Value;
        }

        static float Smooth(float from, float to, float x)
        {
            x = Mathf.Clamp01((x - from) / (to - from));
            return x * x * (3f - 2f * x);
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), fx), Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), fx), fy);
        }

        // A cloud-like puff: a soft round falloff modulated by fractal noise, so the trail billows rather than showing smooth discs
        static Texture2D Puff()
        {
            const int n = 128;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f), r = Mathf.Sqrt(dx * dx + dy * dy);
                    float f = 0f, amplitude = 0.55f, frequency = 3.2f;
                    for (int octave = 0; octave < 4; octave++)
                    {
                        f += amplitude * ValueNoise(x / (float)n * frequency + 11f * octave, y / (float)n * frequency + 5f * octave);
                        amplitude *= 0.5f;
                        frequency *= 2f;
                    }
                    float edge = Mathf.Clamp01(1f - r / (0.92f + 0.25f * (f - 0.5f)));
                    edge = edge * edge * (3f - 2f * edge);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, edge * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(f * 1.25f))));
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
