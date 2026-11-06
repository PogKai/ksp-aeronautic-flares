namespace AeronauticFlares
{
    // The settings, with their defaults. In KSP they are read from GameData/AeronauticFlares/Settings.cfg and the
    // in-game window's PluginData/user.cfg (FlareSettingsStore); the offline harness sets them directly.
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
        // How far the white-hot core streams back from the grain in the airstream, in core lengths per unit of the airstream's share
        // of the flame it sweeps off (0: a round core). Daylight stretches it further, as there is no glare to round it
        public static float FlameStreak = 3f;
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

        // Chaff: a faint cloud of fibres that glints where they catch the light, not a sprite
        public static bool Chaff = true;
        // Multiplies the cloud's haze and its glints
        public static float ChaffBrightness = 1f;
        // How long a cloud hangs in the air, and how many glints it has
        public static float ChaffSeconds = 20f;
        // Over the last this many seconds of its life a cloud thins away to nothing
        public static float ChaffFadeSeconds = 7f;
        public static int ChaffGlints = 240;
        // How near square to the sun's mirror direction a fibre glints, in radians: wider than the sun is, so a cloud twinkles
        public static float ChaffGlintWidth = 0.05f;
        // The air's turbulence, which spreads a cloud, m2/s3
        public static float Turbulence = 3e-3f;
        // Draw BDArmory's own sprites instead (set in the in-game window)
        public static bool LegacyFlares, LegacyChaff;
        // Raised each time a setting changes in the window, so pooled objects pick it up
        public static int Revision;
    }
}
