// Ported from aeronautic's packages/countermeasures/src/graybody.ts (MIT, see THIRD-PARTY-NOTICES.md).
using System;

namespace AeronauticFlares
{
    // Planck radiance, photopic luminance and a display colour for a temperature. No Unity types.
    public static class Graybody
    {
        const double PlanckH = 6.62607015e-34;
        const double LightC = 2.99792458e8;
        const double BoltzmannK = 1.380649e-23;
        const double Km = 683;
        const double PhotopicStartM = 380e-9;
        const double PhotopicStepM = 10e-9;

        // CIE 1924 V(lambda), 380 to 780 nm every 10 nm
        static readonly double[] Photopic =
        {
            0.000039, 0.00012, 0.000396, 0.00121, 0.004, 0.0116, 0.023, 0.038, 0.06,
            0.09098, 0.13902, 0.20802, 0.323, 0.503, 0.71, 0.862, 0.954, 0.99495, 0.995,
            0.952, 0.87, 0.757, 0.631, 0.503, 0.381, 0.265, 0.175, 0.107, 0.061, 0.032,
            0.017, 0.00821, 0.004102, 0.002091, 0.001047, 0.00052, 0.000249, 0.00012,
            0.00006, 0.00003, 0.000015,
        };

        public static double Planck(double wavelengthM, double temperatureK)
        {
            return 2 * PlanckH * LightC * LightC / Math.Pow(wavelengthM, 5) /
                   (Math.Exp(PlanckH * LightC / (wavelengthM * BoltzmannK * temperatureK)) - 1);
        }

        public static double BandRadiance(double lowM, double highM, double temperatureK)
        {
            const int steps = 256;
            double step = (highM - lowM) / steps, sum = 0;
            for (int i = 0; i < steps; i++) sum += Planck(lowM + (i + 0.5) * step, temperatureK);
            return sum * step;
        }

        // cd/m2
        public static double BlackbodyLuminance(double temperatureK)
        {
            double sum = 0;
            for (int i = 0; i < Photopic.Length; i++)
            {
                double weight = i == 0 || i == Photopic.Length - 1 ? 0.5 : 1;
                sum += weight * Photopic[i] * Planck(PhotopicStartM + i * PhotopicStepM, temperatureK);
            }
            return Km * sum * PhotopicStepM;
        }

        // Display colour of a blackbody, 0 to 1 per channel (Tanner Helland's curve fit)
        public static void Rgb(double temperatureK, out float r, out float g, out float b)
        {
            double t = Math.Max(1000, Math.Min(40000, temperatureK)) / 100;
            double red = t <= 66 ? 255 : 329.698727446 * Math.Pow(t - 60, -0.1332047592);
            double green = t <= 66 ? 99.4708025861 * Math.Log(t) - 161.1195681661
                                   : 288.1221695283 * Math.Pow(t - 60, -0.0755148492);
            double blue = t >= 66 ? 255 : t <= 19 ? 0 : 138.5177312231 * Math.Log(t - 10) - 305.0447927307;
            r = (float)(Math.Max(0, Math.Min(255, red)) / 255);
            g = (float)(Math.Max(0, Math.Min(255, green)) / 255);
            b = (float)(Math.Max(0, Math.Min(255, blue)) / 255);
        }

        // Temperatures whose luminance is the hot flame's times e^-f, for f over [0, eFolds] in steps
        public static double[] CoolingTemperatures(double hotK, double eFolds, int steps)
        {
            var table = new double[steps + 1];
            double hot = BlackbodyLuminance(hotK);
            for (int step = 0; step <= steps; step++)
            {
                double wanted = hot * Math.Exp(-eFolds * step / steps);
                double low = 300, high = hotK;
                for (int i = 0; i < 40; i++)
                {
                    double mid = (low + high) / 2;
                    if (BlackbodyLuminance(mid) > wanted) high = mid; else low = mid;
                }
                table[step] = (low + high) / 2;
            }
            return table;
        }
    }
}
