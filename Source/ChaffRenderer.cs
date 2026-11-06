using System.Collections.Generic;
using UnityEngine;

namespace AeronauticFlares
{
    // Draws every chaff cloud in two meshes: a faint haze, the shadow and sky-light of all those fibres, and the
    // glints of the few whose mirror faces the eye. Both are rebuilt each frame from the clouds' own state.
    sealed class ChaffRenderer
    {
        static readonly string[] AlphaShaders = { "KSP/Particles/Alpha Blended", "Legacy Shaders/Particles/Alpha Blended", "Particles/Alpha Blended" };
        static readonly string[] AdditiveShaders = { "KSP/Particles/Additive", "Legacy Shaders/Particles/Additive", "Particles/Additive" };

        // Aluminium sends on 0.91 of the light that falls on it; the cloud is lit by the sun and the sky
        static readonly Color DayHaze = new Color(0.88f, 0.9f, 0.95f);
        static readonly Color SunLight = new Color(1f, 0.96f, 0.88f);
        const float GlintGain = 3f;
        // The shadow of the fibres is taken a little deeper than the patent's cartridge gives, so the cloud reads as a cloud
        const float HazeBoost = 2.0f;

        readonly List<Vector3> hazeVertices = new List<Vector3>(), glintVertices = new List<Vector3>();
        readonly List<Color> hazeColors = new List<Color>(), glintColors = new List<Color>();
        readonly List<Vector2> hazeUvs = new List<Vector2>(), glintUvs = new List<Vector2>();
        readonly List<int> hazeTriangles = new List<int>(), glintTriangles = new List<int>();

        Mesh hazeMesh, glintMesh;
        GameObject hazeObject, glintObject;

        public bool Build()
        {
            var alpha = Find(AlphaShaders);
            var additive = Find(AdditiveShaders);
            if (alpha == null || additive == null)
            {
                Debug.LogWarning("[AeronauticFlares] no particle shaders for chaff, using BDArmory's");
                return false;
            }
            hazeMesh = NewMesh("AeronauticChaffHaze", alpha, WispAtlas(), out hazeObject);
            glintMesh = NewMesh("AeronauticChaffGlints", additive, StarTexture(32), out glintObject);
            return true;
        }

        public void Destroy()
        {
            if (hazeObject != null) Object.Destroy(hazeObject);
            if (glintObject != null) Object.Destroy(glintObject);
        }

        static Shader Find(string[] names)
        {
            foreach (var name in names)
            {
                var shader = Shader.Find(name);
                if (shader != null) return shader;
            }
            return null;
        }

        static Mesh NewMesh(string name, Shader shader, Texture2D texture, out GameObject holder)
        {
            var material = new Material(shader) { mainTexture = texture };
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            material.renderQueue = 3000;
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            holder = new GameObject(name);
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mesh;
        }

        // Cells of the wisp atlas: four cloud tiles, each a Gaussian broken up by its own noise, and a thin streak
        const int Cell = 96, Columns = 3, Rows = 2;

        static Texture2D WispAtlas()
        {
            var texture = new Texture2D(Cell * Columns, Cell * Rows, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Cell * Columns * Cell * Rows];
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int tile = row * Columns + column;
                    for (int y = 0; y < Cell; y++)
                    {
                        for (int x = 0; x < Cell; x++)
                        {
                            float u = (x + 0.5f) / Cell * 2f - 1f, v = (y + 0.5f) / Cell * 2f - 1f;
                            float value = tile == StreakTile ? Streak(u, v) : Wisp(u, v, tile);
                            byte b = (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
                            pixels[(row * Cell + y) * Cell * Columns + column * Cell + x] = new Color32(255, 255, 255, b);
                        }
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // Tile 4 is the streak; tiles 0 to 3 are clouds (5 is unused)
        public const int StreakTile = 4;

        // A Gaussian out to three sigma at the edge, its density broken up by noise so no edge is a clean curve
        static float Wisp(float x, float y, int seed)
        {
            float r2 = x * x + y * y;
            if (r2 >= 1f) return 0f;
            float body = Mathf.Exp(-4.5f * r2) * (1f - r2 * r2);
            float n = Noise(x * 2.3f + seed * 17.1f, y * 2.3f + seed * 9.7f) * 0.55f
                    + Noise(x * 5.1f + seed * 3.3f, y * 5.1f + seed * 11.9f) * 0.3f
                    + Noise(x * 11.3f + seed * 7.7f, y * 11.3f + seed * 1.3f) * 0.15f;
            return body * Mathf.Clamp01(0.15f + 1.3f * n);
        }

        // Long and thin: soft across, fading to nothing at both ends
        static float Streak(float x, float y)
        {
            float along = Mathf.Clamp01(1f - Mathf.Abs(x));
            return Mathf.Exp(-y * y * 60f) * along * along;
        }

        static float Hash(int x, int y)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        // Value noise
        static float Noise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), fx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), fx), fy);
        }

        // The uv rectangle of a tile in the atlas
        static void Tile(int tile, out Vector2 min, out Vector2 max)
        {
            int column = tile % Columns, row = tile / Columns;
            min = new Vector2((float)column / Columns, (float)row / Rows);
            max = new Vector2((column + 1f) / Columns, (row + 1f) / Rows);
        }

        // A bright point with faint spikes, as a glinting fibre looks
        static Texture2D StarTexture(int size)
        {
            return Fill(size, (x, y) =>
            {
                float core = Mathf.Exp(-(x * x + y * y) * 10f);
                float spikes = 0.22f * (Mathf.Exp(-x * x * 300f - y * y * 9f) + Mathf.Exp(-y * y * 300f - x * x * 9f));
                return Mathf.Clamp01(core + spikes);
            }, false);
        }

        static Texture2D Fill(int size, System.Func<float, float, float> shape, bool whiteColor)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float v = Mathf.Clamp01(shape((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f));
                    byte b = (byte)Mathf.RoundToInt(v * 255f);
                    pixels[y * size + x] = whiteColor ? new Color32(255, 255, 255, b) : new Color32(b, b, b, b);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        public void Draw(List<ChaffCloud> clouds, IList<FlareState> flares, Camera camera)
        {
            hazeVertices.Clear(); hazeColors.Clear(); hazeUvs.Clear(); hazeTriangles.Clear();
            glintVertices.Clear(); glintColors.Clear(); glintUvs.Clear(); glintTriangles.Clear();

            if (camera != null)
            {
                Vector3 eye = camera.transform.position;
                float pixelsPerRadian = FlareEnvironment.ScreenHeight() * 0.5f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                foreach (var cloud in clouds)
                {
                    if (cloud.Dead) continue;
                    DrawCloud(cloud, flares, camera, eye, pixelsPerRadian);
                }
            }
            Upload(hazeMesh, hazeVertices, hazeColors, hazeUvs, hazeTriangles, camera);
            Upload(glintMesh, glintVertices, glintColors, glintUvs, glintTriangles, camera);
        }

        static void Upload(Mesh mesh, List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<int> triangles, Camera camera)
        {
            mesh.Clear();
            if (vertices.Count == 0) return;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(camera.transform.position, Vector3.one * 20000f);
        }

        void DrawCloud(ChaffCloud cloud, IList<FlareState> flares, Camera camera, Vector3 eye, float pixelsPerRadian)
        {
            Vector3 toEye = eye - cloud.Center;
            float distance = Mathf.Max(toEye.magnitude, 1f);
            Vector3 view = -toEye / distance;
            float metresPerPixel = distance / pixelsPerRadian;
            float pixelScale = FlareEnvironment.ScreenHeight() / 1440f;
            float day = FlareEnvironment.Daylight(cloud.Center);

            // What lights it: the sun, the sky, and any flare burning near it, the brightest source by far
            Color haze = DayHaze * Mathf.Lerp(0.02f, 0.95f, day);
            FlareState brightest = null;
            float brightestStrength = 0f;
            // A flare's light is a small part of the sun's, so by day it lights the cloud little
            float flareShare = Mathf.Lerp(1f, FlareSettings.DayLight, day);
            foreach (var flare in flares)
            {
                if (!flare.Lit) continue;
                float d = (flare.Position - cloud.Center).magnitude;
                haze += FlareState.FlameColor * (0.9f * flareShare * flare.CoreAlpha / (1f + d * d / 400f));
                float strength = 1.3f * flareShare * flare.CoreAlpha / (1f + d * d / 900f);
                if (strength > brightestStrength) { brightestStrength = strength; brightest = flare; }
            }
            haze.a = 1f;

            // The haze: a Gaussian whose depth is the fibres' shadow over the cloud's apparent area
            Vector3 along = Vector3.ProjectOnPlane(cloud.Heading, view);
            float dp = along.magnitude;
            Vector3 axisA = dp > 0.05f ? along / dp : Vector3.ProjectOnPlane(camera.transform.up, view).normalized;
            Vector3 axisB = Vector3.Cross(view, axisA).normalized;
            float sigmaA = Mathf.Max(Mathf.Sqrt(Sq(cloud.SigmaAlong * dp) + Sq(cloud.SigmaAcross) * (1f - dp * dp)), 1.2f * metresPerPixel);
            float sigmaB = Mathf.Max(cloud.SigmaAcross, 1.2f * metresPerPixel);
            float tau = HazeBoost * (float)ChaffModel.Rr178.CrossSectionM2 / (2f * Mathf.PI * sigmaA * sigmaB);
            float brightness = cloud.Fade * FlareSettings.ChaffBrightness;
            AddHaze(cloud.Center, axisA * (3f * sigmaA), axisB * (3f * sigmaB), haze, Mathf.Min(1f - Mathf.Exp(-tau), 0.55f) * brightness, 0);
            for (int k = 0; k < ChaffCloud.Lumps; k++)
            {
                float s = cloud.LumpSize[k];
                Vector3 at = cloud.Center + axisA * (cloud.LumpAlong[k] * sigmaA) + axisB * (cloud.LumpAcross[k] * sigmaB);
                float lump = 1f - Mathf.Exp(-tau * cloud.LumpWeight[k] / (s * s));
                AddHaze(at, axisA * (3f * sigmaA * s * cloud.LumpFlipA[k]), axisB * (3f * sigmaB * s * cloud.LumpFlipB[k]), haze,
                    Mathf.Min(lump, 0.4f) * brightness, cloud.LumpTile[k]);
            }

            // Streaks of fibres, mostly along the cloud's heading, thin and faint
            float streakAlpha = 0.28f * brightness * Mathf.Min(1f, tau * 3f);
            for (int k = 0; k < ChaffCloud.Filaments; k++)
            {
                Vector3 at = cloud.Center + cloud.Heading * (cloud.FilamentAlong[k] * cloud.SigmaAlong)
                    + cloud.Side * (cloud.FilamentAcross[k] * cloud.SigmaAcross) + cloud.Up * (cloud.FilamentUp[k] * cloud.SigmaAcross);
                Vector3 direction = (cloud.Heading + cloud.Side * cloud.FilamentYaw[k] + cloud.Up * cloud.FilamentPitch[k]).normalized;
                Vector3 onScreen = Vector3.ProjectOnPlane(direction, view);
                if (onScreen.sqrMagnitude < 1e-6f) continue;
                Vector3 lengthAxis = onScreen.normalized * (0.5f * cloud.FilamentLength[k]);
                Vector3 widthAxis = Vector3.Cross(view, onScreen.normalized) * Mathf.Max(0.04f, 0.9f * metresPerPixel);
                AddHaze(at, lengthAxis, widthAxis, haze, streakAlpha, ChaffRenderer.StreakTile);
            }

            // The glints: only fibres whose axis is square to h, half way between the light and the eye, send it to the eye
            Vector3 toLight;
            Color lightColor;
            float strengthNow;
            if (brightest != null && brightestStrength > day)
            {
                toLight = (brightest.Position - cloud.Center).normalized;
                lightColor = FlareState.FlameColor;
                strengthNow = brightestStrength;
            }
            else
            {
                toLight = FlareEnvironment.ToSun(cloud.Center);
                lightColor = SunLight;
                strengthNow = day;
            }
            strengthNow *= brightness;
            if (strengthNow < 0.02f || cloud.Glints == 0) return;

            Vector3 half = toLight - view;
            half = half.sqrMagnitude < 1e-6f ? cloud.Up : half.normalized;
            Vector3 level1 = Vector3.ProjectOnPlane(cloud.Heading, cloud.Up).normalized;
            Vector3 level2 = Vector3.Cross(cloud.Up, level1);
            float width = FlareSettings.ChaffGlintWidth;
            float inverse = 1f / (2f * width * width);
            Vector3 right = camera.transform.right, up = camera.transform.up;
            for (int i = 0; i < cloud.Glints; i++)
            {
                float turn = cloud.GlintPhase[i] + cloud.GlintRate[i] * cloud.Age;
                Vector3 axis = (level1 * Mathf.Cos(turn) + level2 * Mathf.Sin(turn)) + cloud.Up * cloud.GlintTilt[i];
                float square = Vector3.Dot(axis, half);
                float flash = Mathf.Exp(-square * square * inverse);
                if (flash < 0.05f) continue;

                Vector3 at = cloud.Center + cloud.Heading * (cloud.GlintAlong[i] * 0.8f * cloud.SigmaAlong)
                    + cloud.Side * (cloud.GlintAcross[i] * 0.8f * cloud.SigmaAcross) + cloud.Up * (cloud.GlintUp[i] * 0.8f * cloud.SigmaAcross);
                float intensity = flash * strengthNow * cloud.GlintBrightness[i];
                float radius = (1.8f + 3.2f * flash * cloud.GlintBrightness[i]) * pixelScale * metresPerPixel;
                float g = Mathf.Clamp01(intensity) * GlintGain;
                AddGlint(at, right * radius, up * radius, new Color(lightColor.r * g, lightColor.g * g, lightColor.b * g, Mathf.Clamp01(intensity)));
            }
        }

        static float Sq(float x) { return x * x; }

        void AddHaze(Vector3 center, Vector3 a, Vector3 b, Color color, float alpha, int tile)
        {
            if (alpha < 0.003f) return;
            color.a = alpha;
            Vector2 min, max;
            Tile(tile, out min, out max);
            AddQuad(hazeVertices, hazeColors, hazeUvs, hazeTriangles, center, a, b, color, min, max);
        }

        void AddGlint(Vector3 center, Vector3 a, Vector3 b, Color color)
        {
            AddQuad(glintVertices, glintColors, glintUvs, glintTriangles, center, a, b, color, Vector2.zero, Vector2.one);
        }

        static void AddQuad(List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<int> triangles, Vector3 center, Vector3 a, Vector3 b, Color color, Vector2 uvMin, Vector2 uvMax)
        {
            int first = vertices.Count;
            vertices.Add(center - a - b);
            vertices.Add(center - a + b);
            vertices.Add(center + a + b);
            vertices.Add(center + a - b);
            uvs.Add(new Vector2(uvMin.x, uvMin.y));
            uvs.Add(new Vector2(uvMin.x, uvMax.y));
            uvs.Add(new Vector2(uvMax.x, uvMax.y));
            uvs.Add(new Vector2(uvMax.x, uvMin.y));
            for (int i = 0; i < 4; i++) colors.Add(color);
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }
    }
}
