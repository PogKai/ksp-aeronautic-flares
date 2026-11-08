using UnityEngine;

namespace AeronauticFlares
{
    // Sits on one of BDArmory's pooled flares. BDArmory keeps the flight, heat and lifetime; this hands the flare's
    // age, speed and the air around it to a FlareState, which works out the flame, halo and trail, and sets the light.
    sealed class FlareVisual : MonoBehaviour
    {
        public readonly FlareState State = new FlareState();

        Component flare;
        float startTime, lifeTime;
        Light[] lights;
        GameObject[] emitters;
        float[] lightIntensity, lightRange;
        Color[] lightColor;

        // Takes over a pooled flare's look; false when its prefab is not the one this expects
        public static bool Attach(GameObject pooled)
        {
            if (pooled.GetComponent<FlareVisual>() != null) return true;
            var cm = pooled.GetComponent(BdaLink.FlareType);
            if (cm == null) return false;

            // The prefab's two "pEmitter2" emitters are the flame and glow; "pEmitter" is the smoke, which stays.
            // BDArmory re-enables emitters in its own list, so these are switched off as objects instead.
            var flame = new System.Collections.Generic.List<GameObject>();
            foreach (var emitter in pooled.GetComponentsInChildren<KSPParticleEmitter>(true))
            {
                if (emitter.name == "pEmitter2") flame.Add(emitter.gameObject);
            }
            if (flame.Count == 0) return false;

            var visual = pooled.AddComponent<FlareVisual>();
            visual.flare = cm;
            visual.lights = pooled.GetComponentsInChildren<Light>(true);
            visual.emitters = flame.ToArray();
            visual.lightIntensity = new float[visual.lights.Length];
            visual.lightRange = new float[visual.lights.Length];
            visual.lightColor = new Color[visual.lights.Length];
            for (int i = 0; i < visual.lights.Length; i++)
            {
                visual.lightIntensity[i] = visual.lights[i].intensity;
                visual.lightRange[i] = visual.lights[i].range;
                visual.lightColor[i] = visual.lights[i].color;
                visual.lights[i].renderMode = LightRenderMode.ForcePixel;
                visual.lights[i].shadows = LightShadows.None;
            }
            visual.ApplyMode();
            // A flare already flying (a pool that grew mid-salvo) was enabled before it had its fields: start it now, at its true age
            if (pooled.activeInHierarchy) visual.Begin(BdaLink.StartTime(cm));
            return true;
        }

        // BDArmory's own flame and light back when the window asks for the legacy effect, this one's otherwise
        public void ApplyMode()
        {
            bool legacy = FlareSettings.LegacyFlares;
            foreach (var emitter in emitters)
            {
                if (emitter != null && emitter.activeSelf != legacy) emitter.SetActive(legacy);
            }
            if (!legacy) return;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].intensity = lightIntensity[i];
                lights[i].range = lightRange[i];
                lights[i].color = lightColor[i];
            }
        }

        void OnEnable()
        {
            // AddComponent enables a flying flare before Attach has set its fields; Attach starts it afterwards
            if (flare != null) Begin(Time.time);
        }

        void Begin(float start)
        {
            startTime = start;
            lifeTime = BdaLink.LifeTime(flare);
            State.Reset();
            if (FlareDirector.Instance != null) FlareDirector.Instance.Add(this);
        }

        void OnDisable()
        {
            State.Lit = false;
            if (FlareDirector.Instance != null) FlareDirector.Instance.Remove(this);
        }

        public void Step(Camera camera)
        {
            if (FlareSettings.LegacyFlares)
            {
                State.Reset();
                return;
            }
            Vector3 velocity = BdaLink.Velocity(flare);
            float ahead = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            Vector3 position = transform.position + velocity * ahead;
            double density = FlareEnvironment.Air(position).y;
            State.Step(Time.time - startTime, lifeTime, position, velocity, density, camera, Time.deltaTime);

            if (!State.Lit) return;
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].color = FlareState.FlameColor;
                lights[i].intensity = FlareSettings.LightIntensity * State.LightLevel;
                lights[i].range = FlareSettings.LightRange;
            }
        }
    }
}
