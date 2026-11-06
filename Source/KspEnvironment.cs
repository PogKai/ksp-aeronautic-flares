using UnityEngine;

namespace AeronauticFlares
{
    // Gives the drawing code KSP's sun, air and "up"
    static class KspEnvironment
    {
        public static void Install()
        {
            FlareEnvironment.Daylight = Daylight;
            FlareEnvironment.Up = Up;
            FlareEnvironment.ToSun = ToSun;
            FlareEnvironment.Air = Air;
        }

        static Vector3 Up(Vector3 position)
        {
            var body = FlightGlobals.currentMainBody;
            return body == null ? Vector3.up : (Vector3)((Vector3d)position - body.position).normalized;
        }

        static Vector3 ToSun(Vector3 position)
        {
            var sun = Planetarium.fetch != null ? Planetarium.fetch.Sun : null;
            return sun == null ? Vector3.up : ((Vector3)(sun.position - (Vector3d)position)).normalized;
        }

        static Vector2 Air(Vector3 position)
        {
            var body = FlightGlobals.currentMainBody;
            if (body == null) return new Vector2(288.15f, 1.225f);
            double temperature = FlightGlobals.getExternalTemperature();
            double density = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(position), temperature, body);
            return new Vector2((float)temperature, (float)density);
        }

        // 0 at night to 1 in full daylight, from the sun's height at a position. Against a sunlit sky and airframe a
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
    }
}
