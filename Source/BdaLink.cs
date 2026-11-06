using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AeronauticFlares
{
    // Reaches BDArmory's flare classes by reflection, so the plugin has no compile-time dependency on it
    static class BdaLink
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static bool resolved;
        static Type flareType, dropperType;
        static FieldInfo poolField, poolListField, velocityField, lifeTimeField;

        public static Type FlareType { get { Resolve(); return flareType; } }

        public static bool Available { get { Resolve(); return flareType != null && poolField != null; } }

        static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            foreach (var loaded in AssemblyLoader.loadedAssemblies)
            {
                if (loaded.assembly == null || loaded.assembly.GetName().Name != "BDArmory") continue;
                flareType = loaded.assembly.GetType("BDArmory.CounterMeasure.CMFlare");
                dropperType = loaded.assembly.GetType("BDArmory.CounterMeasure.CMDropper");
                break;
            }
            if (flareType == null || dropperType == null)
            {
                Debug.Log("[AeronauticFlares] BDArmory not found, idle");
                return;
            }
            poolField = dropperType.GetField("flarePool", Any);
            velocityField = flareType.GetField("velocity", Any);
            lifeTimeField = flareType.GetField("lifeTime", Any);
            if (poolField == null || velocityField == null)
            {
                Debug.LogWarning("[AeronauticFlares] BDArmory's flare classes differ from 1.13.1, idle");
                poolField = null;
            }
        }

        // The pooled flare objects, or null before BDArmory has built its pool
        public static List<GameObject> Pool()
        {
            if (!Available) return null;
            var pool = poolField.GetValue(null) as Component;
            if (pool == null) return null;
            if (poolListField == null) poolListField = pool.GetType().GetField("pool", Any);
            return poolListField == null ? null : poolListField.GetValue(pool) as List<GameObject>;
        }

        public static Vector3 Velocity(Component flare)
        {
            return (Vector3)velocityField.GetValue(flare);
        }

        // BDArmory's flare lifetime in seconds
        public static float LifeTime(Component flare)
        {
            if (lifeTimeField == null) return 5f;
            return Mathf.Max((float)lifeTimeField.GetValue(flare), 0.5f);
        }
    }
}
