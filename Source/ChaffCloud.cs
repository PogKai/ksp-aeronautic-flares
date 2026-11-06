using System;
using UnityEngine;

namespace AeronauticFlares
{
    // One chaff cartridge's cloud. The airstream tears the packet open in a fifth of a second, laid along the path
    // the aircraft flew, and the fibres then stay where they formed: sinking slowly and spreading in the turbulence.
    // BDArmory keeps the cartridge's flight; the cloud only takes its release point and velocity.
    sealed class ChaffCloud
    {
        static readonly ChaffModel Model = ChaffModel.Rr178;

        public readonly Vector3 Heading, Up, Side;
        public readonly float Speed, Born, Fall;
        readonly float sigmaAcross0, sigmaAlong0;
        public Vector3 Origin;
        public bool Dead;

        // Where its centre is once it has bloomed, for aiming a camera before it has
        public Vector3 BloomedCenter { get { return Origin + Heading * (2f * sigmaAlong0); } }

        // Where it is now and how wide, written by Update
        public Vector3 Center;
        public float SigmaAlong, SigmaAcross, Age, Fade;

        // Each glint's place in the cloud, in sigmas, and its fibre's turn
        public readonly int Glints;
        public readonly float[] GlintAlong, GlintAcross, GlintUp, GlintPhase, GlintRate, GlintTilt, GlintBrightness;
        // The haze's lumps: off-centre, smaller, fainter blobs that stop the cloud being a perfect ellipse
        public const int Lumps = 7;
        public readonly float[] LumpAlong = new float[Lumps], LumpAcross = new float[Lumps], LumpSize = new float[Lumps], LumpWeight = new float[Lumps];
        // Which noise tile each lump uses, and whether it is mirrored, so no two look alike
        public readonly int[] LumpTile = new int[Lumps];
        public readonly float[] LumpFlipA = new float[Lumps], LumpFlipB = new float[Lumps];
        // Thin streaks of fibres: where in the cloud (in sigmas), their length, and how far off the cloud's heading they lie
        public const int Filaments = 30;
        public readonly float[] FilamentAlong = new float[Filaments], FilamentAcross = new float[Filaments], FilamentUp = new float[Filaments];
        public readonly float[] FilamentLength = new float[Filaments], FilamentYaw = new float[Filaments], FilamentPitch = new float[Filaments];

        public ChaffCloud(Vector3 origin, Vector3 velocity, float now)
        {
            Origin = origin;
            Born = now;
            Speed = velocity.magnitude;
            Up = FlareEnvironment.Up(origin);
            Heading = Speed > 1f ? velocity / Speed : Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere, Up).normalized;
            Side = Vector3.Cross(Up, Heading).normalized;

            // The patent's cloud at 244 m/s is laid over the path, so a slower aircraft lays a shorter one
            float lengthScale = Mathf.Clamp((float)(Speed / Model.BloomAtMPerS), 0.3f, 1.6f);
            sigmaAlong0 = (float)Model.BloomLengthM * lengthScale / 4f;
            sigmaAcross0 = (float)Model.BloomWidthM / 4f;

            Vector2 air = FlareEnvironment.Air(origin);
            Fall = (float)Model.FallSpeed(Math.Max(air.x, 150.0), Math.Max(air.y, 0.02));

            var random = new System.Random(unchecked((int)(now * 1000f) + (int)(origin.x * 13f) + (int)(origin.z * 7f)));
            Glints = FlareSettings.ChaffGlints;
            GlintAlong = new float[Glints];
            GlintAcross = new float[Glints];
            GlintUp = new float[Glints];
            GlintPhase = new float[Glints];
            GlintRate = new float[Glints];
            GlintTilt = new float[Glints];
            GlintBrightness = new float[Glints];
            float tumble = (float)Model.TumbleRate(FlareSettings.Turbulence);
            for (int i = 0; i < Glints; i++)
            {
                GlintAlong[i] = Normal(random);
                GlintAcross[i] = Normal(random);
                GlintUp[i] = Normal(random);
                GlintPhase[i] = (float)(random.NextDouble() * Math.PI * 2);
                GlintRate[i] = tumble * (0.5f + (float)random.NextDouble()) * (random.Next(2) == 0 ? -1f : 1f);
                GlintTilt[i] = Normal(random) * 0.05f;
                // Fibres differ in size and in how true a mirror they are: most glints are faint, a few are bright
                GlintBrightness[i] = 0.25f + 0.75f * (float)Math.Pow(random.NextDouble(), 2.0);
            }
            for (int i = 0; i < Lumps; i++)
            {
                LumpAlong[i] = Normal(random) * 0.8f;
                LumpAcross[i] = Normal(random) * 0.7f;
                LumpSize[i] = 0.3f + (float)random.NextDouble() * 0.45f;
                LumpWeight[i] = 0.18f + (float)random.NextDouble() * 0.2f;
                LumpTile[i] = random.Next(4);
                LumpFlipA[i] = random.Next(2) == 0 ? -1f : 1f;
                LumpFlipB[i] = random.Next(2) == 0 ? -1f : 1f;
            }
            for (int i = 0; i < Filaments; i++)
            {
                FilamentAlong[i] = Normal(random) * 0.9f;
                FilamentAcross[i] = Normal(random) * 0.9f;
                FilamentUp[i] = Normal(random) * 0.9f;
                FilamentLength[i] = 0.5f + (float)random.NextDouble() * 1.3f;
                FilamentYaw[i] = Normal(random) * 0.22f;
                FilamentPitch[i] = Normal(random) * 0.15f;
            }
            Update(now);
        }

        // Box and Muller's normal
        static float Normal(System.Random random)
        {
            double a = 1.0 - random.NextDouble(), b = random.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(a)) * Math.Cos(2.0 * Math.PI * b));
        }

        // Set when the cloud is taken out early (a drop over the cap, the window's switch): when it has faded away
        float retiredAt = -1f;
        const float RetireSeconds = 1.5f;

        // Fades the cloud out over a moment and then removes it, so it never vanishes in a frame
        public void Retire(float now)
        {
            if (retiredAt < 0f) retiredAt = now;
        }

        public bool Retired { get { return retiredAt >= 0f; } }

        public void Update(float now)
        {
            Age = now - Born;
            float life = FlareSettings.ChaffSeconds;
            if (Age > life || (retiredAt >= 0f && now - retiredAt > RetireSeconds))
            {
                Dead = true;
                return;
            }
            float bloom = (float)Model.Bloom(Age);
            double turbulence = FlareSettings.Turbulence;
            SigmaAlong = Math.Max((float)ChaffModel.Sigma(sigmaAlong0 * bloom, turbulence, Age), 0.08f);
            SigmaAcross = Math.Max((float)ChaffModel.Sigma(sigmaAcross0 * bloom, turbulence, Age), 0.08f);
            // Laid along the path from the release point, then sinking
            Center = Origin + Heading * (2f * sigmaAlong0 * bloom) - Up * (Fall * Mathf.Max(Age - (float)Model.BloomTimeS, 0f));
            // The last part of its life thins it away to nothing, as a flare burns out; a retired cloud fades faster
            float fadeTime = Mathf.Min(FlareSettings.ChaffFadeSeconds, life * 0.5f);
            Fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((life - Age) / Mathf.Max(fadeTime, 0.01f)));
            if (retiredAt >= 0f) Fade = Mathf.Min(Fade, Mathf.SmoothStep(0f, 1f, 1f - (now - retiredAt) / RetireSeconds));
        }
    }
}
