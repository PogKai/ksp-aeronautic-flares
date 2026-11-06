// Ported from aeronautic's packages/countermeasures/src/flare.ts (MIT, see THIRD-PARTY-NOTICES.md).
using System;

namespace AeronauticFlares
{
    public struct Grain
    {
        public double MassKg, SurfaceM2;
    }

    public struct FlareLight
    {
        public double IntensityCd, TrailCd, RadiusM;
    }

    // A decoy flare: an MTV grain burning from every face, and the flame it gives the eye. No Unity types.
    public sealed class FlareModel
    {
        public const double SeaLevelDensity = 1.225;

        // MJU-7A/B class, 253 g of MTV1 (Debnath et al., CEJEM 22(2), 2025)
        public static readonly FlareModel Mju7 = new FlareModel();

        public readonly double Side0M = 0.0226, Side1M = 0.0498, MassKg = 0.253;
        public readonly double BurnRateMPerS = 3.86e-3, BandEnergyJPerKgSr = 179e3;
        public readonly double BandLowM = 1.8e-6, BandHighM = 2.6e-6;
        public readonly double TemperatureK = 2100, Emissivity = 0.95;
        public readonly double HalfLightMPerS = 150, GlowTimeS = 0.05, TrailShare = 0.5, IgnitionDelayS = 0.1;
        public readonly double DensityKgPerM3;
        public readonly double LuminousPerBand;

        FlareModel()
        {
            // MTV1 by mass: magnesium 0.5, Teflon 0.45, Viton 0.05, at their densities
            DensityKgPerM3 = 1.0 / (0.5 / 1740 + 0.45 / 2140 + 0.05 / 1850);
            LuminousPerBand = Graybody.BlackbodyLuminance(TemperatureK) /
                              Graybody.BandRadiance(BandLowM, BandHighM, TemperatureK);
        }

        public double GrainLength { get { return MassKg / (DensityKgPerM3 * Side0M * Side1M); } }

        // Until the thinnest side is gone
        public double BurnTime
        {
            get { return Math.Min(Math.Min(Side0M, Side1M), GrainLength) / (2 * BurnRateMPerS); }
        }

        public Grain GrainAt(double ageS)
        {
            double burnt = 2 * BurnRateMPerS * Math.Max(ageS, 0);
            double a = Math.Max(Side0M - burnt, 0), b = Math.Max(Side1M - burnt, 0), c = Math.Max(GrainLength - burnt, 0);
            bool left = a > 0 && b > 0 && c > 0;
            return new Grain
            {
                MassKg = left ? DensityKgPerM3 * a * b * c : 0,
                SurfaceM2 = left ? 2 * (a * b + b * c + c * a) : 0,
            };
        }

        public double MassRate(Grain grain) { return DensityKgPerM3 * BurnRateMPerS * grain.SurfaceM2; }

        // W/sr in the seeker band
        public double BandIntensity(Grain grain) { return MassRate(grain) * BandEnergyJPerKgSr; }

        // Share of the flame's light that stays at the grain, 1 / (1 + v/v_half); v_half grows as the air thins
        public double AirstreamShare(double speedMPerS, double densityKgPerM3)
        {
            return 1 / (1 + Math.Max(speedMPerS, 0) * densityKgPerM3 / (HalfLightMPerS * SeaLevelDensity));
        }

        public FlareLight Light(Grain grain, double speedMPerS, double densityKgPerM3)
        {
            double still = BandIntensity(grain);
            double share = AirstreamShare(speedMPerS, densityKgPerM3);
            double band = still * share;
            double radiance = Emissivity * Graybody.BandRadiance(BandLowM, BandHighM, TemperatureK);
            return new FlareLight
            {
                IntensityCd = band * LuminousPerBand,
                TrailCd = still * (1 - share) * TrailShare * LuminousPerBand,
                RadiusM = Math.Sqrt(band / (radiance * Math.PI)),
            };
        }
    }
}
