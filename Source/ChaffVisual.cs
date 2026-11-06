using UnityEngine;

namespace AeronauticFlares
{
    // Sits on one of BDArmory's pooled chaff objects. BDArmory keeps the cartridge's flight and the radar's chaff;
    // this hides the sprite BDArmory draws and starts a cloud at the release point instead.
    sealed class ChaffVisual : MonoBehaviour
    {
        Component chaff;
        Renderer[] renderers;

        public static bool Attach(GameObject pooled)
        {
            if (pooled.GetComponent<ChaffVisual>() != null) return true;
            var cm = pooled.GetComponent(BdaLink.ChaffType);
            if (cm == null) return false;
            var visual = pooled.AddComponent<ChaffVisual>();
            visual.chaff = cm;
            visual.renderers = pooled.GetComponentsInChildren<Renderer>(true);
            visual.ApplyMode();
            return true;
        }

        // The cloud replaces the sprite unless the window (or the settings) ask for BDArmory's own
        public static bool Replacing { get { return FlareSettings.Chaff && !FlareSettings.LegacyChaff; } }

        // The sprite is a particle renderer: switched off as a renderer, so BDArmory's own emitter code runs untouched
        public void ApplyMode()
        {
            bool show = !Replacing;
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.enabled != show) renderer.enabled = show;
            }
        }

        void OnEnable()
        {
            if (!Replacing || FlareDirector.Instance == null || chaff == null) return;
            ApplyMode();
            FlareDirector.Instance.SpawnCloud(transform.position, BdaLink.ChaffVelocity(chaff));
        }
    }
}
