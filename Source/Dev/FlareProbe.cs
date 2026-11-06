using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AeronauticFlares
{
    // Dev only. With GameData/AeronauticFlares/PluginData/probe.txt present it logs
    // each live flare's distance and on-screen size every half second, and moves the clock to night if the file says "night".
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    sealed class FlareProbe : MonoBehaviour
    {
        const double NightElevationDeg = -25;

        // "cam" in the marker: once the first flare lights, a still camera takes over, to one side of the drop and
        // looking at the live flares until the last one has burnt out
        bool cam, camActive;
        GameObject rig;
        Vector3 rigPosition, lookAt;
        float lastFlare;

        IEnumerator Start()
        {
            string marker = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/AeronauticFlares/PluginData/probe.txt");
            if (!File.Exists(marker))
            {
                Destroy(this);
                yield break;
            }
            // "night" in the file moves the clock to night; any other text only logs
            string text = File.ReadAllText(marker);
            bool night = text.Contains("night");
            cam = text.Contains("cam");
            GameEvents.onFloatingOriginShift.Add(OnShift);
            while (!FlightGlobals.ready || FlightGlobals.ActiveVessel == null) yield return null;
            yield return new WaitForSeconds(2f);

            while (true)
            {
                var vessel = FlightGlobals.ActiveVessel;
                if (night && vessel != null && SunElevation(vessel) > NightElevationDeg + 10) SetNight(vessel);
                LogFlares();
                yield return new WaitForSeconds(0.5f);
            }
        }

        void OnShift(Vector3d offset, Vector3d nonKrakensbane)
        {
            var shift = (Vector3)nonKrakensbane;
            rigPosition -= shift;
            lookAt -= shift;
        }

        void OnDestroy()
        {
            GameEvents.onFloatingOriginShift.Remove(OnShift);
        }

        void LateUpdate()
        {
            if (!cam) return;
            var director = FlareDirector.Instance;
            if (director == null) return;

            Vector3 sum = Vector3.zero;
            int lit = 0;
            FlareVisual first = null;
            foreach (var flare in director.Active)
            {
                if (!flare.Lit) continue;
                sum += flare.Position;
                lit++;
                if (first == null) first = flare;
            }

            if (!camActive)
            {
                if (lit == 0) return;
                TakeOver(first);
            }
            if (lit > 0)
            {
                lastFlare = Time.time;
                lookAt = Vector3.Lerp(lookAt, sum / lit, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f));
            }
            rig.transform.position = rigPosition;
            rig.transform.rotation = Quaternion.LookRotation(lookAt - rigPosition, ((Vector3d)rigPosition - FlightGlobals.currentMainBody.position).normalized);
            FlightCamera.fetch.transform.localPosition = Vector3.zero;
            FlightCamera.fetch.transform.localRotation = Quaternion.identity;
        }

        void TakeOver(FlareVisual first)
        {
            // CameraTools, which the autotest drives, lets go of the camera
            foreach (var behaviour in FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour.GetType().Name != "CamTools") continue;
                var stop = behaviour.GetType().GetMethod("CameraDeactivate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (stop != null) stop.Invoke(behaviour, null);
            }
            var up = ((Vector3d)first.Position - FlightGlobals.currentMainBody.position).normalized;
            Vector3 heading = BdaLink.Velocity(first.GetComponent(BdaLink.FlareType)).normalized;
            Vector3 side = Vector3.Cross(up, heading).normalized;
            // Flares are thrown some 400 m on along the heading and fall about 200 m, so the camera waits ahead and below,
            // on whichever side looks away from the sun
            Vector3 centre = first.Position + heading * 200f;
            Vector3 toSun = (Planetarium.fetch.Sun.position - (Vector3d)first.Position).normalized;
            Vector3 best = Vector3.zero;
            float least = float.MaxValue;
            foreach (float sign in new[] { 1f, -1f })
            {
                Vector3 position = first.Position + heading * 360f + side * (110f * sign) - (Vector3)up * 80f;
                float facing = Vector3.Dot((centre - position).normalized, toSun);
                if (facing < least) { least = facing; best = position; }
            }
            rigPosition = best;
            lookAt = first.Position + heading * 200f;
            rig = new GameObject("FlareCamRig");
            var fc = FlightCamera.fetch;
            fc.SetTargetNone();
            fc.DeactivateUpdate();
            fc.transform.parent = rig.transform;
            fc.SetFoV(60f);
            camActive = true;
            lastFlare = Time.time;
            Debug.Log("[FlareProbe] cam takeover at " + Time.time.ToString("F2"));
        }

        static double SunElevation(Vessel vessel)
        {
            var up = (vessel.CoMD - vessel.mainBody.position).normalized;
            return Math.Asin(Vector3d.Dot((Planetarium.fetch.Sun.position - vessel.CoMD).normalized, up)) * 180 / Math.PI;
        }

        // Turns the day until the sun is NightElevationDeg, the same hour-angle algebra as the Wingtip Vortex autotest
        static void SetNight(Vessel vessel)
        {
            var body = vessel.mainBody;
            Vector3d axis = body.RotationAxis.normalized;
            Vector3d up = (vessel.CoMD - body.position).normalized;
            Vector3d toSun = (Planetarium.fetch.Sun.position - body.position).normalized;
            Vector3d upFlat = Vector3d.Exclude(axis, up), sunFlat = Vector3d.Exclude(axis, toSun);
            double uA = Vector3d.Dot(up, axis), sA = Vector3d.Dot(toSun, axis);
            double flat = upFlat.magnitude * sunFlat.magnitude;
            if (flat < 1e-6) return;
            upFlat = upFlat.normalized;
            sunFlat = sunFlat.normalized;

            double hour = -Math.Atan2(Vector3d.Dot(Vector3d.Cross(sunFlat, upFlat), axis), Vector3d.Dot(sunFlat, upFlat)) * 180 / Math.PI;
            double cosH = (Math.Sin(NightElevationDeg * Math.PI / 180) - uA * sA) / flat;
            double h = Math.Acos(Math.Max(-1, Math.Min(1, cosH))) * 180 / Math.PI;
            double day = body.solarDayLength > 0 ? body.solarDayLength : body.rotationPeriod;
            double ahead = ((h - hour) % 360 + 360) % 360, behind = ((-h - hour) % 360 + 360) % 360;
            Planetarium.SetUniversalTime(Planetarium.GetUniversalTime() + Math.Min(ahead, behind) / 360 * day);
            Debug.Log("[FlareProbe] night: sun now " + SunElevation(vessel).ToString("F1") + " deg");
        }

        void LogFlares()
        {
            var director = FlareDirector.Instance;
            var camera = FlightCamera.fetch != null ? FlightCamera.fetch.mainCamera : Camera.main;
            if (director == null || camera == null) return;

            float pixelsPerRadian = Screen.height * 0.5f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            foreach (var flare in director.Active)
            {
                if (!flare.Lit) continue;
                float distance = (flare.Position - camera.transform.position).magnitude;
                Debug.Log(string.Format("[FlareProbe] dist {0:F0} m  core {1:F2} m = {2:F1} px  halo {3:F2} m = {4:F1} px  core alpha {5:F2}  trail {6:F2} x{7}",
                    distance, flare.CoreRadius, flare.CoreRadius / distance * pixelsPerRadian,
                    flare.HaloRadius, flare.HaloRadius / distance * pixelsPerRadian, flare.CoreAlpha, flare.TrailBrightness, flare.TrailCount));
            }
        }
    }
}
