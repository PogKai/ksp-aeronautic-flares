using UnityEngine;

namespace AeronauticFlares
{
    // Read from an AERONAUTIC_FLARES node in GameData/AeronauticFlares/Settings.cfg
    static class FlareSettings
    {
        public const string Version = "1.0.0";

        public static bool Enabled = true;
        public static bool Trail = true;
        // Multiplies the flame's brightness, its halo and the flare's light
        public static float Brightness = 1f;
        // Multiplies the flame's physical size
        public static float FlameScale = 1f;
        // Radius in pixels at 1440p that the halo and the flame keep at full brightness, however far away
        public static float GlarePixels = 420f;
        public static float CorePixels = 44f;
        // Multiplies the colour of everything drawn, over 1 so the flame saturates to white at its heart
        public static float Gain = 4f;
        // Narrowest the trail is drawn, pixels at 1440p
        public static float TrailPixels = 5f;
        // The flare's light on the aircraft and ground around it: intensity at full brightness, and reach in metres
        public static float LightIntensity = 6f;
        public static float LightRange = 80f;
        // What the halo, the flame's size and the light shrink to in full daylight, as a share of their night values
        // Out to this distance a flare keeps its full pixel size; past it the glare shrinks as 1/sqrt(distance), down to FarShare
        public static float FullSizeDistance = 25f;
        public static float FarShare = 0.5f;
        // How much overlapping halos on screen dim each other, so a salvo seen from afar does not add up to white
        public static float OverlapDim = 0.5f;
        // Seconds before the end of its life over which a flare burns out, dimming to nothing (0: cuts off)
        public static float FadeSeconds = 0.8f;
        public static float DayGlare = 0.45f;
        public static float DayCore = 0.8f;
        public static float DayLight = 0.1f;

        public static void Load()
        {
            var nodes = GameDatabase.Instance.GetConfigNodes("AERONAUTIC_FLARES");
            if (nodes.Length == 0) return;
            var n = nodes[0];
            Read(n, "enabled", ref Enabled);
            Read(n, "trail", ref Trail);
            Read(n, "brightness", ref Brightness);
            Read(n, "flameScale", ref FlameScale);
            Read(n, "glarePixels", ref GlarePixels);
            Read(n, "corePixels", ref CorePixels);
            Read(n, "gain", ref Gain);
            Read(n, "trailPixels", ref TrailPixels);
            Read(n, "lightIntensity", ref LightIntensity);
            Read(n, "lightRange", ref LightRange);
            Read(n, "fullSizeDistance", ref FullSizeDistance);
            Read(n, "farShare", ref FarShare);
            Read(n, "overlapDim", ref OverlapDim);
            Read(n, "fadeSeconds", ref FadeSeconds);
            Read(n, "dayGlare", ref DayGlare);
            Read(n, "dayCore", ref DayCore);
            Read(n, "dayLight", ref DayLight);
        }

        static void Read(ConfigNode node, string key, ref bool value)
        {
            if (node.HasValue(key)) bool.TryParse(node.GetValue(key), out value);
        }

        static void Read(ConfigNode node, string key, ref float value)
        {
            float parsed;
            if (node.HasValue(key) && float.TryParse(node.GetValue(key), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out parsed))
                value = parsed;
        }
    }
}
