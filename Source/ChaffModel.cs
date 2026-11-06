// Ported from aeronautic's packages/countermeasures/src/chaff.ts and smoke.ts (MIT, see THIRD-PARTY-NOTICES.md).
using System;

namespace AeronauticFlares
{
    // A chaff cartridge: millions of aluminised glass fibres that the airstream tears into a faint cloud. No Unity types.
    public sealed class ChaffModel
    {
        public const double G0 = 9.80665;

        // RR-178 class, 117 g of one-mil dipoles (US patent 4,653,403)
        public static readonly ChaffModel Rr178 = new ChaffModel();

        public readonly double DipoleLengthM = 88775, DiameterM = 25.4e-6, CutLengthM = 0.024;
        public readonly double DensityKgPerM3 = 2600, Reflectance = 0.91;
        // The cloud forms in BloomTimeS, this wide and long (plus and minus two sigma) at BloomAtMPerS
        public readonly double BloomTimeS = 0.2, BloomWidthM = 1.75, BloomLengthM = 11, BloomAtMPerS = 244;

        public double MassKg { get { return Math.PI * DiameterM * DiameterM / 4 * DipoleLengthM * DensityKgPerM3; } }

        public double DipoleCount { get { return DipoleLengthM / CutLengthM; } }

        // The light all the dipoles stop: their shadow, a quarter of their surface over every orientation, m2
        public double CrossSectionM2 { get { return Math.PI * DiameterM * DipoleLengthM / 4; } }

        // Sutherland's law, Pa s
        public static double AirViscosity(double temperatureK)
        {
            return 1.458e-6 * Math.Pow(temperatureK, 1.5) / (temperatureK + 110.4);
        }

        // How fast a dipole falls once the air has it: Lamb's drag on a cylinder in Oseen's flow against its weight
        public double FallSpeed(double temperatureK, double densityKgPerM3)
        {
            double d = DiameterM, mu = AirViscosity(temperatureK);
            double weight = DensityKgPerM3 * G0 * d * d / (16 * mu);
            double speed = weight;
            for (int step = 0; step < 16; step++)
            {
                double reynolds = Math.Max(densityKgPerM3 * speed * d / mu, 1e-9);
                speed = weight * Math.Max(0.5 - 0.5772 + Math.Log(8 / reynolds), 1);
            }
            return speed;
        }

        // How fast the air's turbulence turns a dipole, rad/s
        public double TumbleRate(double turbulenceM2PerS3)
        {
            return Math.Pow(turbulenceM2PerS3 / (CutLengthM * CutLengthM), 1.0 / 3);
        }

        // How far the cloud has bloomed, 0 to 1
        public double Bloom(double ageS)
        {
            double left = 1 - Math.Min(Math.Max(ageS / BloomTimeS, 0), 1);
            return 1 - left * left;
        }

        // How wide a cloud has spread, Richardson's law: sigma^2 = sigma0^2 + epsilon t^3 / 2
        public static double Sigma(double sigma0M, double turbulenceM2PerS3, double ageS)
        {
            return Math.Sqrt(sigma0M * sigma0M + 0.5 * Math.Max(turbulenceM2PerS3, 0) * Math.Pow(Math.Max(ageS, 0), 3));
        }
    }
}
