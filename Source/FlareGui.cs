using KSP.UI.Screens;
using UnityEngine;

namespace AeronauticFlares
{
    // A window from a button on the stock toolbar in flight, to switch back to BDArmory's own flare and chaff
    // effects or tune the brightness. Changes apply at once and are kept in PluginData/user.cfg.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class FlareGui : MonoBehaviour
    {
        const int WindowId = 0x41465231;

        ApplicationLauncherButton button;
        Texture2D icon;
        Rect window = new Rect(160, 160, 330, 10);
        bool shown;
        float saveAt = -1f;

        void Start()
        {
            GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
            AddButton();
        }

        void OnDestroy()
        {
            GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
            if (button != null && ApplicationLauncher.Instance != null) ApplicationLauncher.Instance.RemoveModApplication(button);
            if (saveAt >= 0f) FlareSettingsStore.Save();
        }

        void AddButton()
        {
            if (button != null || !ApplicationLauncher.Ready) return;
            if (icon == null) icon = Icon(38);
            button = ApplicationLauncher.Instance.AddModApplication(() => shown = true, () => shown = false,
                null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, icon);
        }

        void Update()
        {
            if (saveAt >= 0f && Time.unscaledTime >= saveAt)
            {
                saveAt = -1f;
                FlareSettingsStore.Save();
            }
        }

        void OnGUI()
        {
            if (!shown) return;
            GUI.skin = HighLogic.Skin;
            window = GUILayout.Window(WindowId, window, Draw, "Aeronautic flares " + FlareSettings.Version, GUILayout.Width(330));
        }

        void Draw(int id)
        {
            bool changed = false;
            GUILayout.Label("Flares and chaff are drawn by this add-on instead of BDArmory's own sprites. " +
                            "Switch either back here; it applies at once.");
            if (!BdaLink.Available) GUILayout.Label("BDArmory was not found: nothing to replace.");

            GUILayout.Space(4);
            changed |= Toggle(ref FlareSettings.LegacyFlares, "Flares: BDArmory's legacy effect");
            changed |= Toggle(ref FlareSettings.LegacyChaff, "Chaff: BDArmory's legacy effect");

            GUILayout.Space(4);
            changed |= Slider("Flare brightness", ref FlareSettings.Brightness);
            changed |= Slider("Chaff brightness", ref FlareSettings.ChaffBrightness);

            GUILayout.Space(4);
            if (GUILayout.Button("Reset to defaults"))
            {
                FlareSettings.LegacyFlares = FlareSettings.LegacyChaff = false;
                FlareSettings.Brightness = FlareSettings.ChaffBrightness = 1f;
                changed = true;
            }
            if (changed)
            {
                FlareSettings.Revision++;
                saveAt = Time.unscaledTime + 0.6f;
            }
            GUI.DragWindow();
        }

        static bool Toggle(ref bool value, string label)
        {
            bool now = GUILayout.Toggle(value, label);
            bool changed = now != value;
            value = now;
            return changed;
        }

        static bool Slider(string label, ref float value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(120));
            float now = GUILayout.HorizontalSlider(value, 0.2f, 2f);
            GUILayout.Label(now.ToString("0.00"), GUILayout.Width(36));
            GUILayout.EndHorizontal();
            bool changed = Mathf.Abs(now - value) > 0.001f;
            value = now;
            return changed;
        }

        // A dark disc with a flare falling in it: a white-hot head with a glow at the bottom, and a flame curving up behind it.
        // Drawn here so there is no image file to ship.
        static Texture2D Icon(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            const float headX = 0.22f, headY = -0.40f;
            const int steps = 24;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    float disc = Mathf.Clamp01((0.95f - Mathf.Sqrt(u * u + v * v)) * 12f);
                    float dx = u - headX, dy = v - headY, d2 = dx * dx + dy * dy;
                    float glow = Mathf.Exp(-d2 * 11f), core = Mathf.Exp(-d2 * 130f);
                    // The nearest point on the curved path behind the head, and how far back along it that is
                    float nearest = 9f, back = 0f;
                    for (int i = 0; i <= steps; i++)
                    {
                        float t = (float)i / steps;
                        float px = headX - 0.95f * t * t, py = headY + 1.45f * t;
                        float d = (u - px) * (u - px) + (v - py) * (v - py);
                        if (d < nearest) { nearest = d; back = t; }
                    }
                    float width = 0.045f + 0.06f * back;
                    float streak = Mathf.Exp(-nearest / (width * width)) * Mathf.Exp(-back * 1.7f) * Mathf.Min(1f, back * 12f);
                    pixels[y * size + x] = new Color(0.08f + glow * 0.9f + streak + core, 0.08f + glow * 0.45f + streak * 0.5f + core, 0.1f + glow * 0.12f + streak * 0.12f + core, disc);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
