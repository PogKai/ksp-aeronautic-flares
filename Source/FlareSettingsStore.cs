using UnityEngine;

namespace AeronauticFlares
{
    // Reads the settings from KSP's config files and keeps the in-game window's choices between sessions
    static class FlareSettingsStore
    {
        static string UserPath { get { return KSPUtil.ApplicationRootPath + "GameData/AeronauticFlares/PluginData/user.cfg"; } }

        public static void Load()
        {
            var nodes = GameDatabase.Instance.GetConfigNodes("AERONAUTIC_FLARES");
            if (nodes.Length > 0) ReadShared(nodes[0]);
            if (System.IO.File.Exists(UserPath))
            {
                var user = ConfigNode.Load(UserPath);
                if (user != null && user.HasNode("AERONAUTIC_FLARES_USER"))
                {
                    var u = user.GetNode("AERONAUTIC_FLARES_USER");
                    Read(u, "legacyFlares", ref FlareSettings.LegacyFlares);
                    Read(u, "legacyChaff", ref FlareSettings.LegacyChaff);
                    Read(u, "brightness", ref FlareSettings.Brightness);
                    Read(u, "chaffBrightness", ref FlareSettings.ChaffBrightness);
                }
            }
        }

        // What the in-game window changed, kept between sessions
        public static void Save()
        {
            var root = new ConfigNode();
            var u = root.AddNode("AERONAUTIC_FLARES_USER");
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            u.AddValue("legacyFlares", FlareSettings.LegacyFlares);
            u.AddValue("legacyChaff", FlareSettings.LegacyChaff);
            u.AddValue("brightness", FlareSettings.Brightness.ToString(ci));
            u.AddValue("chaffBrightness", FlareSettings.ChaffBrightness.ToString(ci));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(UserPath));
            root.Save(UserPath);
            FlareSettings.Revision++;
        }

        static void ReadShared(ConfigNode n)
        {
            Read(n, "enabled", ref FlareSettings.Enabled);
            Read(n, "trail", ref FlareSettings.Trail);
            Read(n, "brightness", ref FlareSettings.Brightness);
            Read(n, "flameScale", ref FlareSettings.FlameScale);
            Read(n, "glarePixels", ref FlareSettings.GlarePixels);
            Read(n, "corePixels", ref FlareSettings.CorePixels);
            Read(n, "gain", ref FlareSettings.Gain);
            Read(n, "flameStreak", ref FlareSettings.FlameStreak);
            Read(n, "trailPixels", ref FlareSettings.TrailPixels);
            Read(n, "lightIntensity", ref FlareSettings.LightIntensity);
            Read(n, "lightRange", ref FlareSettings.LightRange);
            Read(n, "fullSizeDistance", ref FlareSettings.FullSizeDistance);
            Read(n, "farShare", ref FlareSettings.FarShare);
            Read(n, "overlapDim", ref FlareSettings.OverlapDim);
            Read(n, "fadeSeconds", ref FlareSettings.FadeSeconds);
            Read(n, "dayGlare", ref FlareSettings.DayGlare);
            Read(n, "dayCore", ref FlareSettings.DayCore);
            Read(n, "dayLight", ref FlareSettings.DayLight);
            Read(n, "chaff", ref FlareSettings.Chaff);
            Read(n, "chaffBrightness", ref FlareSettings.ChaffBrightness);
            Read(n, "chaffSeconds", ref FlareSettings.ChaffSeconds);
            Read(n, "chaffFadeSeconds", ref FlareSettings.ChaffFadeSeconds);
            Read(n, "chaffGlintWidth", ref FlareSettings.ChaffGlintWidth);
            Read(n, "turbulence", ref FlareSettings.Turbulence);
            float glints = FlareSettings.ChaffGlints;
            Read(n, "chaffGlints", ref glints);
            FlareSettings.ChaffGlints = Mathf.Clamp((int)glints, 0, 600);
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
