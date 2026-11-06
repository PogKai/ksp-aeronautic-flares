// Checks the C# flare model against the assertions in upstream's flare.test.ts and the numbers its README states.
using System;
using AeronauticFlares;

static class Program
{
    static int failures;

    static void Check(string name, bool ok, string detail)
    {
        Console.WriteLine((ok ? "ok    " : "FAIL  ") + name + "  " + detail);
        if (!ok) failures++;
    }

    static int Main()
    {
        var f = FlareModel.Mju7;

        double platinum = Graybody.BlackbodyLuminance(2042) / 6e5;
        Check("2042 K blackbody is the old candela, 60 cd/cm2", Math.Abs(platinum - 1) < 0.05, platinum.ToString("F4"));

        double ratio = Graybody.BandRadiance(1.8e-6, 2.6e-6, 2100) / Graybody.BandRadiance(3e-6, 5e-6, 2100);
        Check("seeker band ratio 1.2 to 1.8", ratio > 1.2 && ratio < 1.8, ratio.ToString("F3"));

        Check("MTV1 density 1906 kg/m3", Math.Abs(f.DensityKgPerM3 - 1906) < 0.5, f.DensityKgPerM3.ToString("F2"));
        Check("grain fits the 8 inch case", f.GrainLength > 0.1 && f.GrainLength < 0.2032, f.GrainLength.ToString("F4") + " m");

        double burn = f.BurnTime;
        Check("burn time 2.93 s, under the 5 s of a flare", Math.Abs(burn - 0.0226 / (2 * 3.86e-3)) < 1e-6 && burn < 5, burn.ToString("F3") + " s");

        var gone = f.GrainAt(burn);
        Check("grain gone at burn time", gone.MassKg == 0 && gone.SurfaceM2 == 0, "mass " + gone.MassKg);

        const int steps = 20000;
        double energy = 0, burnt = 0;
        for (int i = 0; i < steps; i++)
        {
            var g = f.GrainAt((i + 0.5) * burn / steps);
            energy += f.BandIntensity(g) * burn / steps;
            burnt += f.MassRate(g) * burn / steps;
        }
        Check("burns its whole mass", Math.Abs(burnt / f.MassKg - 1) < 5e-4, (burnt / f.MassKg).ToString("F5"));
        Check("radiates its measured energy", Math.Abs(energy / (f.BandEnergyJPerKgSr * f.MassKg) - 1) < 5e-4,
            (energy / (f.BandEnergyJPerKgSr * f.MassKg)).ToString("F5"));

        var light = f.Light(f.GrainAt(0), 0, FlareModel.SeaLevelDensity);
        Check("flame is a few hundred thousand cd", light.IntensityCd > 1e5 && light.IntensityCd < 1e6, light.IntensityCd.ToString("F0") + " cd");
        Check("flame is under a metre across", light.RadiusM > 0.1 && light.RadiusM * 2 < 1, (light.RadiusM * 2).ToString("F3") + " m");

        double face = light.IntensityCd / (Math.PI * light.RadiusM * light.RadiusM);
        double wanted = 0.95 * Graybody.BlackbodyLuminance(2100);
        Check("face as bright as a graybody", Math.Abs(face / wanted - 1) < 5e-7, (face / wanted).ToString("F7"));

        var hot = Graybody.BlackbodyLuminance(2200) / Graybody.BandRadiance(1.8e-6, 2.6e-6, 2200);
        Check("a hotter flame gives the eye more per band watt", hot > f.LuminousPerBand, hot.ToString("F1") + " vs " + f.LuminousPerBand.ToString("F1"));

        double half = f.AirstreamShare(150, FlareModel.SeaLevelDensity);
        Check("light halved at 150 m/s", Math.Abs(half - 0.5) < 1e-9, half.ToString("F4"));
        Check("thin air sweeps less off", f.AirstreamShare(150, 0.3) > half, f.AirstreamShare(150, 0.3).ToString("F3"));

        var fast = f.Light(f.GrainAt(0), 250, FlareModel.SeaLevelDensity);
        Check("trail gets the swept light, dimmer flame", fast.TrailCd > 0 && fast.IntensityCd < light.IntensityCd,
            "flame " + fast.IntensityCd.ToString("F0") + " cd, trail " + fast.TrailCd.ToString("F0") + " cd");

        var table = Graybody.CoolingTemperatures(2100, 3, 24);
        bool cooling = Math.Abs(table[0] - 2100) < 1;
        for (int i = 1; i < table.Length; i++) cooling &= table[i] < table[i - 1];
        Check("trail cools monotonically from 2100 K", cooling, table[0].ToString("F0") + " K to " + table[24].ToString("F0") + " K");

        Console.WriteLine(failures == 0 ? "all checks passed" : failures + " check(s) failed");
        return failures == 0 ? 0 : 1;
    }
}
