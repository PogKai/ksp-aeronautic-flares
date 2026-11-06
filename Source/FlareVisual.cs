using System;
using UnityEngine;

namespace AeronauticFlares
{
    // Sits on one of BDArmory's pooled flares. BDArmory keeps the flight, heat and lifetime; this works out
    // what the flame, its light and its trail look like from the flare's age, speed and the air around it.
    sealed class FlareVisual : MonoBehaviour
    {
        public const int MaxTrailPoints = 24;
        public const float TrailEFolds = 3f;
        public const int ColorSteps = 24;

        static readonly FlareModel Model = FlareModel.Mju7;
        static readonly double ReferenceCd = Model.Light(Model.GrainAt(0), 0, FlareModel.SeaLevelDensity).IntensityCd;
        static readonly double Burn = Model.BurnTime;
        public static readonly Color FlameColor = Blackbody(Model.TemperatureK);
        // Trail colours as the swept products cool, brightest channel at 1
        public static readonly Color[] TrailColors = BuildTrailColors();

        Component flare;
        float startTime, lifeTime;
        Light[] lights;

        public bool Lit;
        public Vector3 Position;
        public float CoreRadius, HaloRadius, CoreAlpha, HaloAlpha, TrailBrightness, MetresPerPixel, HaloPixels, Reach;
        public Vector2 ScreenPosition;
        public readonly Vector3[] TrailPoints = new Vector3[MaxTrailPoints];
        public readonly float[] TrailAges = new float[MaxTrailPoints];
        public int TrailCount;

        static Color Blackbody(double kelvin)
        {
            float r, g, b;
            Graybody.Rgb(kelvin, out r, out g, out b);
            return new Color(r, g, b, 1f);
        }

        static Color[] BuildTrailColors()
        {
            var temperatures = Graybody.CoolingTemperatures(Model.TemperatureK, TrailEFolds, ColorSteps);
            var colors = new Color[temperatures.Length];
            for (int i = 0; i < colors.Length; i++) colors[i] = Blackbody(temperatures[i]);
            return colors;
        }

        // Takes over a pooled flare's look; false when its prefab is not the one this expects
        public static bool Attach(GameObject pooled)
        {
            if (pooled.GetComponent<FlareVisual>() != null) return true;
            var cm = pooled.GetComponent(BdaLink.FlareType);
            if (cm == null) return false;

            // The prefab's two "pEmitter2" emitters are the flame and glow; "pEmitter" is the smoke, which stays.
            // BDArmory re-enables emitters in its own list, so these are switched off as objects instead.
            bool replaced = false;
            foreach (var emitter in pooled.GetComponentsInChildren<KSPParticleEmitter>(true))
            {
                if (emitter.name != "pEmitter2") continue;
                emitter.gameObject.SetActive(false);
                replaced = true;
            }
            if (!replaced) return false;

            var visual = pooled.AddComponent<FlareVisual>();
            visual.flare = cm;
            visual.lights = pooled.GetComponentsInChildren<Light>(true);
            foreach (var light in visual.lights)
            {
                light.renderMode = LightRenderMode.ForcePixel;
                light.shadows = LightShadows.None;
            }
            return true;
        }

        void OnEnable()
        {
            startTime = Time.time;
            lifeTime = BdaLink.LifeTime(flare);
            TrailCount = 0;
            Lit = false;
            if (FlareDirector.Instance != null) FlareDirector.Instance.Add(this);
        }

        void OnDisable()
        {
            Lit = false;
            if (FlareDirector.Instance != null) FlareDirector.Instance.Remove(this);
        }

        // BDArmory moves the flare in its physics step; a floating origin shift moves it and its trail together
        public void ShiftTrail(Vector3 offset)
        {
            for (int i = 0; i < TrailCount; i++) TrailPoints[i] -= offset;
        }

        public void Step(Camera camera)
        {
            float age = Time.time - startTime;
            float burn = age / lifeTime;
            Vector3 velocity = BdaLink.Velocity(flare);
            float ahead = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            Position = transform.position + velocity * ahead;

            // The grain burns out over BDArmory's lifetime, so the visuals end when the heat does
            Lit = burn < 1f;
            if (!Lit)
            {
                TrailCount = 0;
                return;
            }

            var body = FlightGlobals.currentMainBody;
            double density = body == null ? 0 :
                FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(Position), FlightGlobals.getExternalTemperature(), body);
            var light = Model.Light(Model.GrainAt(burn * Burn), velocity.magnitude, density);

            float level = Mathf.Clamp01((float)(light.IntensityCd / ReferenceCd));
            float ignition = Mathf.Clamp01(age / (float)Model.IgnitionDelayS);
            // The last FadeSeconds of the life dim the flame, halo, light and trail together to nothing, so it burns out
            float fade = FlareSettings.FadeSeconds <= 0f ? 1f :
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - burn) * lifeTime / FlareSettings.FadeSeconds));
            float bright = Mathf.Sqrt(level) * ignition * fade * FlareSettings.Brightness;
            float day = Daylight(Position);

            // The flame's physical radius up close; from afar a floor in screen pixels, so it stays a bright point
            float distance = Mathf.Max((camera.transform.position - Position).magnitude, 1f);
            float metresPerPixel = distance / (Screen.height * 0.5f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float pixelScale = Screen.height / 1440f;
            float sqrtLevel = Mathf.Sqrt(level) * Mathf.Lerp(0.6f, 1f, fade);
            // The light reaching the eye falls with the square of the distance, so the glare shrinks as 1 / sqrt(distance)
            // from FullSizeDistance out, to FarShare
            Reach = Mathf.Clamp(Mathf.Sqrt(FlareSettings.FullSizeDistance / distance), FlareSettings.FarShare, 1f);
            float corePixels = FlareSettings.CorePixels * Mathf.Lerp(1f, FlareSettings.DayCore, day) * pixelScale * sqrtLevel * Reach;
            HaloPixels = FlareSettings.GlarePixels * Mathf.Lerp(1f, FlareSettings.DayGlare, day) * pixelScale * sqrtLevel * Reach;
            CoreRadius = Mathf.Max((float)light.RadiusM * FlareSettings.FlameScale, corePixels * metresPerPixel);
            HaloRadius = Mathf.Max(CoreRadius * 2.5f, HaloPixels * metresPerPixel);
            MetresPerPixel = metresPerPixel;
            ScreenPosition = camera.WorldToScreenPoint(Position);
            CoreAlpha = Mathf.Clamp01(bright);
            HaloAlpha = Mathf.Clamp01(0.7f * bright * Mathf.Lerp(1f, FlareSettings.DayGlare, day) * Mathf.Sqrt(Reach));
            TrailBrightness = Mathf.Sqrt(Mathf.Clamp01((float)(light.TrailCd / ReferenceCd))) * ignition * fade * FlareSettings.Brightness;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].color = FlameColor;
                lights[i].intensity = FlareSettings.LightIntensity * bright * Mathf.Lerp(1f, FlareSettings.DayLight, day);
                lights[i].range = FlareSettings.LightRange;
            }

            UpdateTrail();
        }

        // 0 at night to 1 in full daylight, from the sun's height at the flare. Against a sunlit sky and airframe the
        // flare's glare and the light it throws are a small fraction of what they are in the dark.
        static float Daylight(Vector3 position)
        {
            var body = FlightGlobals.currentMainBody;
            var sun = Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
            if (body == null || sun == null) return 0f;
            Vector3d up = (Vector3d)position - body.position;
            Vector3d toSun = sun.position - (Vector3d)position;
            float height = (float)Vector3d.Dot(up.normalized, toSun.normalized);
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((height + 0.05f) / 0.35f));
        }

        void UpdateTrail()
        {
            if (!FlareSettings.Trail)
            {
                TrailCount = 0;
                return;
            }
            float span = (float)Model.GlowTimeS * TrailEFolds;
            for (int i = 0; i < TrailCount; i++) TrailAges[i] += Time.deltaTime;
            while (TrailCount > 0 && TrailAges[TrailCount - 1] > span) TrailCount--;

            if (TrailCount == 0 || (Position - TrailPoints[0]).sqrMagnitude > 0.04f)
            {
                int keep = Math.Min(TrailCount, MaxTrailPoints - 1);
                Array.Copy(TrailPoints, 0, TrailPoints, 1, keep);
                Array.Copy(TrailAges, 0, TrailAges, 1, keep);
                TrailPoints[0] = Position;
                TrailAges[0] = 0f;
                TrailCount = keep + 1;
            }
        }
    }
}
