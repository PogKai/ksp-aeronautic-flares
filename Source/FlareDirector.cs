using System.Collections.Generic;
using UnityEngine;

namespace AeronauticFlares
{
    // Finds BDArmory's pooled flares and chaff, keeps their visuals, and draws every live flare and chaff cloud
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class FlareDirector : MonoBehaviour
    {
        internal static FlareDirector Instance;

        const int MaxClouds = 40;

        readonly List<FlareVisual> active = new List<FlareVisual>();
        readonly List<FlareState> states = new List<FlareState>();
        readonly List<ChaffCloud> clouds = new List<ChaffCloud>();

        FlareRenderer flareRenderer;
        ChaffRenderer chaffRenderer;
        float nextScan;
        int appliedRevision;

        void Awake()
        {
            FlareSettingsStore.Load();
            if (!FlareSettings.Enabled || !BdaLink.Available)
            {
                enabled = false;
                return;
            }
            KspEnvironment.Install();
            var flares = new FlareRenderer();
            if (!flares.Build())
            {
                enabled = false;
                return;
            }
            flareRenderer = flares;
            Instance = this;
            var chaff = new ChaffRenderer();
            if (chaff.Build()) chaffRenderer = chaff;
            appliedRevision = FlareSettings.Revision;
            GameEvents.onFloatingOriginShift.Add(OnFloatingOriginShift);
        }

        void OnDestroy()
        {
            GameEvents.onFloatingOriginShift.Remove(OnFloatingOriginShift);
            if (Instance == this) Instance = null;
            if (flareRenderer != null) flareRenderer.Destroy();
            if (chaffRenderer != null) chaffRenderer.Destroy();
        }

        internal List<FlareVisual> Active { get { return active; } }

        internal List<ChaffCloud> Clouds { get { return clouds; } }

        // A chaff cartridge BDArmory has thrown: its cloud forms where it was released
        internal void SpawnCloud(Vector3 position, Vector3 velocity)
        {
            if (chaffRenderer == null) return;
            // Over the cap the oldest cloud that is still there thins away rather than vanishing
            int live = 0;
            foreach (var cloud in clouds) if (!cloud.Retired) live++;
            if (live >= MaxClouds)
            {
                foreach (var cloud in clouds)
                {
                    if (cloud.Retired) continue;
                    cloud.Retire(Time.time);
                    break;
                }
            }
            clouds.Add(new ChaffCloud(position, velocity, Time.time));
        }

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
            foreach (var visual in active) visual.State.ShiftTrail(shift);
            foreach (var cloud in clouds) cloud.Origin -= shift;
        }

        void Update()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + 0.2f;
            bool changed = appliedRevision != FlareSettings.Revision;
            appliedRevision = FlareSettings.Revision;
            if (changed && !ChaffVisual.Replacing)
            {
                foreach (var cloud in clouds) cloud.Retire(Time.time);
            }

            var pool = BdaLink.Pool();
            if (pool != null)
            {
                foreach (var pooled in pool)
                {
                    if (pooled == null) continue;
                    var visual = pooled.GetComponent<FlareVisual>();
                    if (visual == null) FlareVisual.Attach(pooled);
                    else if (changed) visual.ApplyMode();
                }
            }
            var chaff = BdaLink.ChaffPool();
            if (chaff != null)
            {
                foreach (var pooled in chaff)
                {
                    if (pooled == null) continue;
                    var visual = pooled.GetComponent<ChaffVisual>();
                    if (visual == null) ChaffVisual.Attach(pooled);
                    else visual.ApplyMode();
                }
            }
        }

        void LateUpdate()
        {
            var camera = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : Camera.main;
            states.Clear();
            if (camera != null)
            {
                foreach (var visual in active)
                {
                    visual.Step(camera);
                    states.Add(visual.State);
                }
            }
            flareRenderer.Draw(states, camera);

            clouds.RemoveAll(cloud => cloud.Dead);
            foreach (var cloud in clouds) cloud.Update(Time.time);
            if (chaffRenderer != null) chaffRenderer.Draw(clouds, states, camera);
        }
    }
}
