using System.Collections.Generic;
using UnityEngine;

namespace AeronauticFlares
{
    // Finds BDArmory's pooled flares and draws every live flare's flame and trail in one mesh
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class FlareDirector : MonoBehaviour
    {
        internal static FlareDirector Instance;

        static readonly string[] ShaderNames = { "KSP/Particles/Additive", "Legacy Shaders/Particles/Additive", "Particles/Additive" };

        readonly List<FlareVisual> active = new List<FlareVisual>();
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();
        readonly Vector3[] ribbon = new Vector3[FlareVisual.MaxTrailPoints + 1];
        readonly float[] ribbonAges = new float[FlareVisual.MaxTrailPoints + 1];

        Mesh mesh;
        GameObject drawer;
        float nextScan;

        void Awake()
        {
            FlareSettings.Load();
            if (!FlareSettings.Enabled || !BdaLink.Available || !BuildDrawer())
            {
                enabled = false;
                return;
            }
            Instance = this;
            GameEvents.onFloatingOriginShift.Add(OnFloatingOriginShift);
        }

        void OnDestroy()
        {
            GameEvents.onFloatingOriginShift.Remove(OnFloatingOriginShift);
            if (Instance == this) Instance = null;
            if (drawer != null) Destroy(drawer);
        }

        internal List<FlareVisual> Active { get { return active; } }

        internal void Add(FlareVisual visual)
        {
            if (!active.Contains(visual)) active.Add(visual);
        }

        internal void Remove(FlareVisual visual)
        {
            active.Remove(visual);
        }

        void OnFloatingOriginShift(Vector3d offset, Vector3d nonKrakensbane)
        {
            var shift = (Vector3)nonKrakensbane;
            foreach (var visual in active) visual.ShiftTrail(shift);
        }

        bool BuildDrawer()
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

            var material = new Material(shader) { mainTexture = SoftDisc(64) };
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

        // White centre fading to nothing at the edge, so a quad reads as a round glow
        static Texture2D SoftDisc(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float r2 = Mathf.Min(dx * dx + dy * dy, 1f);
                    float v = Mathf.Pow(1f - r2, 3f);
                    byte b = (byte)Mathf.RoundToInt(v * 255f);
                    pixels[y * size + x] = new Color32(b, b, b, b);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void Update()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + 0.2f;
            var pool = BdaLink.Pool();
            if (pool == null) return;
            foreach (var pooled in pool)
            {
                if (pooled != null && pooled.GetComponent<FlareVisual>() == null) FlareVisual.Attach(pooled);
            }
        }

        void LateUpdate()
        {
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            triangles.Clear();

            var camera = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : Camera.main;
            if (camera != null && active.Count > 0)
            {
                Vector3 eye = camera.transform.position;
                foreach (var visual in active) visual.Step(camera);
                DimOverlaps();
                foreach (var visual in active)
                {
                    if (!visual.Lit) continue;
                    if (FlareSettings.Trail && visual.TrailCount > 0) AddTrail(visual, eye);
                    AddQuad(visual.Position, visual.HaloRadius, Tint(FlareVisual.FlameColor, visual.HaloAlpha), camera);
                    AddQuad(visual.Position, visual.CoreRadius, Tint(Color.Lerp(FlareVisual.FlameColor, Color.white, 0.85f), visual.CoreAlpha), camera);
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
        void DimOverlaps()
        {
            for (int i = 0; i < active.Count; i++)
            {
                var a = active[i];
                if (!a.Lit) continue;
                float covered = 0f;
                for (int j = 0; j < active.Count; j++)
                {
                    var b = active[j];
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

        void AddQuad(Vector3 center, float radius, Color color, Camera camera)
        {
            Vector3 right = camera.transform.right * radius, up = camera.transform.up * radius;
            int first = vertices.Count;
            vertices.Add(center - right - up);
            vertices.Add(center - right + up);
            vertices.Add(center + right + up);
            vertices.Add(center + right - up);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            for (int i = 0; i < 4; i++) colors.Add(color);
            AddQuadIndices(first);
        }

        void AddQuadIndices(int first)
        {
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
            triangles.Add(first);
            triangles.Add(first + 2);
            triangles.Add(first + 3);
        }

        // A ribbon facing the camera along the flare's path through the air, cooling and fading with age
        void AddTrail(FlareVisual visual, Vector3 eye)
        {
            int count = visual.TrailCount + 1;
            ribbon[0] = visual.Position;
            ribbonAges[0] = 0f;
            for (int i = 0; i < visual.TrailCount; i++)
            {
                ribbon[i + 1] = visual.TrailPoints[i];
                ribbonAges[i + 1] = visual.TrailAges[i];
            }

            float glow = (float)FlareModel.Mju7.GlowTimeS;
            float pixelScale = Screen.height / 1440f;
            int first = vertices.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 along = ribbon[Mathf.Max(i - 1, 0)] - ribbon[Mathf.Min(i + 1, count - 1)];
                Vector3 side = Vector3.Cross(along, ribbon[i] - eye);
                float fold = ribbonAges[i] / glow;
                float width = Mathf.Max(visual.CoreRadius * 0.6f, FlareSettings.TrailPixels * pixelScale * visual.MetresPerPixel) * (1f - 0.5f * fold / FlareVisual.TrailEFolds);
                side = side.sqrMagnitude > 1e-12f ? side.normalized * width : Vector3.zero;

                int step = Mathf.Clamp(Mathf.RoundToInt(fold / FlareVisual.TrailEFolds * FlareVisual.ColorSteps), 0, FlareVisual.ColorSteps);
                Color color = Tint(FlareVisual.TrailColors[step], visual.TrailBrightness * Mathf.Exp(-fold));

                vertices.Add(ribbon[i] - side);
                vertices.Add(ribbon[i] + side);
                uvs.Add(new Vector2(0f, 0.5f));
                uvs.Add(new Vector2(1f, 0.5f));
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
