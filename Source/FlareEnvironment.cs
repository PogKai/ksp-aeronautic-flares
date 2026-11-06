using System;
using UnityEngine;

namespace AeronauticFlares
{
    // What the drawing code asks of the world: the sun, the air, which way is up and how big the screen is. In KSP
    // these come from KSP (KspEnvironment); anywhere else (the offline harness) they are set to whatever the scene is.
    static class FlareEnvironment
    {
        // 0 at night to 1 in full daylight, at a world position
        public static Func<Vector3, float> Daylight = position => 1f;

        // The unit vector pointing up, away from the planet's centre, at a position
        public static Func<Vector3, Vector3> Up = position => Vector3.up;

        // The unit vector from a position to the sun
        public static Func<Vector3, Vector3> ToSun = position => new Vector3(0.3f, 0.8f, 0.5f).normalized;

        // The air at a position: temperature in kelvin (x) and density in kg/m3 (y)
        public static Func<Vector3, Vector2> Air = position => new Vector2(288.15f, 1.225f);

        // The height in pixels of what is being drawn to
        public static Func<float> ScreenHeight = () => Screen.height;
    }
}
