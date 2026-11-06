using System.Collections.Generic;
using UnityEngine;

namespace AeronauticFlares
{
    // Draws every live flare's flame, halo and trail in one additive mesh, rebuilt each frame. No KSP types.
    sealed class FlareRenderer
    {
        static readonly string[] ShaderNames = { "KSP/Particles/Additive", "Legacy Shaders/Particles/Additive", "Particles/Additive" };

        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        readonly Vector3[] ribbon = new Vector3[FlareState.MaxTrailPoints + 1];
        readonly float[] ribbonAges = new float[FlareState.MaxTrailPoints + 1];

        Mesh mesh;
        GameObject drawer;

        public bool Build()
        {
            Shader shader = null;
            foreach (var name in ShaderNames)
            {
                shader = Shader.Find(name);
                if (shader != null) break;
            }
            if (shader == null)
            {
                Debug.LogWarning("[AeronauticFlares] no additive particle shader found, idle");
                return false;
            }

            var material = new Material(shader) { mainTexture = FlameAtlas() };
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            material.renderQueue = 3000;

            mesh = new Mesh { name = "AeronauticFlares" };
            mesh.MarkDynamic();
            drawer = new GameObject("AeronauticFlares");
            drawer.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = drawer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return true;
        }

        public void Destroy()
        {
            if (drawer != null) Object.Destroy(drawer);
        }

        // Two cells side by side: a white centre fading to nothing at the edge (the glow and the head of the flame), and
        // a streak that fades from the head backwards, ragged at its edges (the flame swept back by the airstream)
        const int Cell = 64;

        static Texture2D FlameAtlas()
        {
            var texture = new Texture2D(Cell * 2, Cell, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Cell * 2 * Cell];
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    float u = (x + 0.5f) / Cell, v = (y + 0.5f) / Cell;
                    float dx = u * 2f - 1f, dy = v * 2f - 1f;
                    float r2 = Mathf.Min(dx * dx + dy * dy, 1f);
                    float disc = Mathf.Pow(1f - r2, 3f);
                    // The streak: u runs from the head (0) to the tip (1), v across
                    float width = 0.34f * (1f - 0.65f * u) + 0.04f;
                    float ragged = 0.65f + 0.35f * Mathf.PerlinNoise(u * 7f + 3.1f, dy * 2.5f + 1.7f);
                    float streak = Mathf.Exp(-dy * dy / (width * width)) * Mathf.Pow(1f - u, 1.4f) * ragged;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(disc) * 255f), b = (byte)Mathf.RoundToInt(Mathf.Clamp01(streak) * 255f);
                    pixels[y * Cell * 2 + x] = new Color32(a, a, a, a);
                    pixels[y * Cell * 2 + Cell + x] = new Color32(b, b, b, b);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static readonly Vector2 DiscMin = new Vector2(0f, 0f), DiscMax = new Vector2(0.5f, 1f);
        static readonly Vector2 StreakMin = new Vector2(0.5f, 0f), StreakMax = new Vector2(1f, 1f);

        public void Draw(IList<FlareState> flares, Camera camera)
        {
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            triangles.Clear();

            if (camera != null && flares.Count > 0)
            {
                Vector3 eye = camera.transform.position;
                DimOverlaps(flares);
                foreach (var flare in flares)
                {
                    if (!flare.Lit) continue;
                    if (FlareSettings.Trail && flare.TrailCount > 0) AddTrail(flare, eye);
                    AddQuad(flare.Position, flare.HaloRadius, Tint(FlareState.FlameColor, flare.HaloAlpha), camera);
                    Color hot = Color.Lerp(FlareState.FlameColor, Color.white, 0.85f);
                    AddFlameStreak(flare, hot, eye);
                    AddCore(flare, hot, eye, camera);
                }
            }

            mesh.Clear();
            if (vertices.Count == 0) return;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.bounds = new Bounds(camera.transform.position, Vector3.one * 20000f);
        }

        // Halos are added together, so ones that overlap on screen dim each other by how much of them the others cover
        static void DimOverlaps(IList<FlareState> flares)
        {
            for (int i = 0; i < flares.Count; i++)
            {
                var a = flares[i];
                if (!a.Lit) continue;
                float covered = 0f;
                for (int j = 0; j < flares.Count; j++)
                {
                    var b = flares[j];
                    if (i == j || !b.Lit) continue;
                    float reach = (a.HaloPixels + b.HaloPixels) * 0.5f;
                    if (reach < 1f) continue;
                    covered += Mathf.Clamp01(1f - (a.ScreenPosition - b.ScreenPosition).magnitude / reach);
                }
                a.HaloAlpha /= 1f + FlareSettings.OverlapDim * covered;
            }
        }

        static Color Tint(Color color, float alpha)
        {
            float g = alpha * FlareSettings.Gain;
            return new Color(color.r * g, color.g * g, color.b * g, Mathf.Min(alpha, 1f));
        }

        // The flame swept back from the grain: a streak along the flow, as long as the core is stretched, flickering
        void AddFlameStreak(FlareState flare, Color color, Vector3 eye)
        {
            if (flare.CoreStretch <= 1.05f) return;
            Vector3 view = (flare.Position - eye).normalized;
            Vector3 tail = Vector3.ProjectOnPlane(-flare.Velocity, view);
            if (tail.sqrMagnitude < 1e-6f) return;
            tail.Normalize();
            float length = flare.CoreRadius * 2.2f * (flare.CoreStretch - 1f) * flare.Flicker;
            float width = flare.CoreRadius * 0.9f;
            Vector3 along = tail * (0.5f * length), across = Vector3.Cross(view, tail).normalized * width;
            AddQuadUv(flare.Position + along, along, across, color, StreakMin, StreakMax);
        }

        // The white-hot core: round with no airstream, a teardrop pointing back along the flow with it
        void AddCore(FlareState flare, Color hot, Vector3 eye, Camera camera)
        {
            Color color = Tint(hot, flare.CoreAlpha);
            Vector3 view = (flare.Position - eye).normalized;
            Vector3 tail = Vector3.ProjectOnPlane(-flare.Velocity, view);
            if (flare.CoreStretch <= 1.05f || tail.sqrMagnitude < 1e-6f)
            {
                AddQuad(flare.Position, flare.CoreRadius, color, camera);
                return;
            }
            tail.Normalize();
            float r = flare.CoreRadius, stretch = Mathf.Min(flare.CoreStretch, 4f);
            Vector3 along = tail * (r * (0.6f + 0.5f * stretch)), across = Vector3.Cross(view, tail).normalized * (r * 0.6f);
            AddQuadUv(flare.Position + tail * (r * 0.5f * (stretch - 1f) * 0.5f), along, across, color, DiscMin, DiscMax);
        }

        void AddQuad(Vector3 center, float radius, Color color, Camera camera)
        {
            AddQuadUv(center, camera.transform.right * radius, camera.transform.up * radius, color, DiscMin, DiscMax);
        }

        // A quad round a centre with half extents a and b; its texture runs from uvMin at -a-b to uvMax at +a+b
        void AddQuadUv(Vector3 center, Vector3 right, Vector3 up, Color color, Vector2 uvMin, Vector2 uvMax)
        {
            int first = vertices.Count;
            vertices.Add(center - right - up);
            vertices.Add(center - right + up);
            vertices.Add(center + right + up);
            vertices.Add(center + right - up);
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

        // A ribbon facing the camera along the flare's path through the air, cooling and fading with age
        void AddTrail(FlareState flare, Vector3 eye)
        {
            int count = flare.TrailCount + 1;
            ribbon[0] = flare.Position;
            ribbonAges[0] = 0f;
            for (int i = 0; i < flare.TrailCount; i++)
            {
                ribbon[i + 1] = flare.TrailPoints[i];
                ribbonAges[i + 1] = flare.TrailAges[i];
            }

            float glow = (float)FlareModel.Mju7.GlowTimeS;
            float pixelScale = FlareEnvironment.ScreenHeight() / 1440f;
            int first = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 along = ribbon[Mathf.Max(i - 1, 0)] - ribbon[Mathf.Min(i + 1, count - 1)];
                Vector3 side = Vector3.Cross(along, ribbon[i] - eye);
                float fold = ribbonAges[i] / glow;
                float width = Mathf.Max(flare.CoreRadius * 0.6f, FlareSettings.TrailPixels * pixelScale * flare.MetresPerPixel) * (1f - 0.5f * fold / FlareState.TrailEFolds);
                side = side.sqrMagnitude > 1e-12f ? side.normalized * width : Vector3.zero;

                int step = Mathf.Clamp(Mathf.RoundToInt(fold / FlareState.TrailEFolds * FlareState.ColorSteps), 0, FlareState.ColorSteps);
                Color color = Tint(FlareState.TrailColors[step], flare.TrailBrightness * Mathf.Exp(-fold));

                vertices.Add(ribbon[i] - side);
                vertices.Add(ribbon[i] + side);
                uvs.Add(new Vector2(0f, 0.5f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                colors.Add(color);
                colors.Add(color);
            }
            for (int i = 0; i < count - 1; i++)
            {
                int a = first + i * 2;
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(a + 2);
                triangles.Add(a + 1);
                triangles.Add(a + 3);
                triangles.Add(a + 2);
            }
        }
    }
}
