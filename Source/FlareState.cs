using System;
using UnityEngine;

namespace AeronauticFlares
{
    // How one flare looks: its flame, halo and trail, from its age, speed and the air around it. No KSP types, so the
    // offline harness runs the same code as the game. BDArmory keeps the flight, heat and lifetime.
    sealed class FlareState
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

        public bool Lit;
        public Vector3 Position, Velocity;
        // How many times longer than wide the core is, and a flicker on its length
        public float CoreStretch = 1f, Flicker = 1f;
        public readonly float Seed = NextSeed();
        static int seeds;

        static float NextSeed()
        {
            return (seeds++ * 1.618f) % 6.2832f;
        }
        public float CoreRadius, HaloRadius, CoreAlpha, HaloAlpha, TrailBrightness, MetresPerPixel, HaloPixels, Reach;
        // The brightness the light on the aircraft is set to, over its night value
        public float LightLevel;
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

        public void Reset()
        {
            TrailCount = 0;
            Lit = false;
        }

        // A floating origin shift moves the flare and its trail together
        public void ShiftTrail(Vector3 offset)
        {
            for (int i = 0; i < TrailCount; i++) TrailPoints[i] -= offset;
        }

        // The look at an age (seconds) of a life (seconds), at a position moving at a velocity through air of a density
        public void Step(float age, float life, Vector3 position, Vector3 velocity, double density, Camera camera, float deltaTime)
        {
            Position = position;
            Velocity = velocity;
            float burn = age / life;

            // The grain burns out over BDArmory's lifetime, so the visuals end when the heat does
            Lit = burn < 1f;
            if (!Lit)
            {
                TrailCount = 0;
                return;
            }

            var light = Model.Light(Model.GrainAt(burn * Burn), velocity.magnitude, density);

            float level = Mathf.Clamp01((float)(light.IntensityCd / ReferenceCd));
            float ignition = Mathf.Clamp01(age / (float)Model.IgnitionDelayS);
            // The last FadeSeconds of the life dim the flame, halo, light and trail together to nothing, so it burns out
            float fade = FlareSettings.FadeSeconds <= 0f ? 1f :
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - burn) * life / FlareSettings.FadeSeconds));
            float bright = Mathf.Sqrt(level) * ignition * fade * FlareSettings.Brightness;
            float day = FlareEnvironment.Daylight(position);

            // The flame's physical radius up close; from afar a floor in screen pixels, so it stays a bright point
            float screenHeight = FlareEnvironment.ScreenHeight();
            float distance = Mathf.Max((camera.transform.position - position).magnitude, 1f);
            float metresPerPixel = distance / (screenHeight * 0.5f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
            float pixelScale = screenHeight / 1440f;
            float sqrtLevel = Mathf.Sqrt(level) * Mathf.Lerp(0.6f, 1f, fade);
            // The light reaching the eye falls with the square of the distance, so the glare shrinks as 1 / sqrt(distance)
            // from FullSizeDistance out, to FarShare
            Reach = Mathf.Clamp(Mathf.Sqrt(FlareSettings.FullSizeDistance / distance), FlareSettings.FarShare, 1f);
            float corePixels = FlareSettings.CorePixels * Mathf.Lerp(1f, FlareSettings.DayCore, day) * pixelScale * sqrtLevel * Reach;
            HaloPixels = FlareSettings.GlarePixels * Mathf.Lerp(1f, FlareSettings.DayGlare, day) * pixelScale * sqrtLevel * Reach;
            CoreRadius = Mathf.Max((float)light.RadiusM * FlareSettings.FlameScale, corePixels * metresPerPixel);
            HaloRadius = Mathf.Max(CoreRadius * 2.5f, HaloPixels * metresPerPixel);
            MetresPerPixel = metresPerPixel;
            ScreenPosition = camera.WorldToScreenPoint(position);
            CoreAlpha = Mathf.Clamp01(bright);
            HaloAlpha = Mathf.Clamp01(0.7f * bright * Mathf.Lerp(1f, FlareSettings.DayGlare, day) * Mathf.Sqrt(Reach));
            TrailBrightness = Mathf.Sqrt(Mathf.Clamp01((float)(light.TrailCd / ReferenceCd))) * ignition * fade * FlareSettings.Brightness;
            LightLevel = bright * Mathf.Lerp(1f, FlareSettings.DayLight, day);

            // In the airstream the flame is swept back from the grain into a streak, and by day, with no glare round it,
            // that streak is what is seen. Its length flickers as the burn does.
            float swept = 1f - (float)Model.AirstreamShare(velocity.magnitude, density);
            CoreStretch = 1f + FlareSettings.FlameStreak * swept * (1f + day);
            Flicker = 1f + 0.18f * Mathf.Sin(age * 41f + Seed * 7f) + 0.1f * Mathf.Sin(age * 97f + Seed * 13f) + 0.06f * Mathf.Sin(age * 173f + Seed * 3f);

            UpdateTrail(deltaTime);
        }

        void UpdateTrail(float deltaTime)
        {
            if (!FlareSettings.Trail)
            {
                TrailCount = 0;
                return;
            }
            float span = (float)Model.GlowTimeS * TrailEFolds;
            for (int i = 0; i < TrailCount; i++) TrailAges[i] += deltaTime;
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
