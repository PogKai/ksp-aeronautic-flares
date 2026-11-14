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
        static Type flareType, dropperType, chaffType, missileType;
        static FieldInfo firedField;
        static PropertyInfo firedProperty, stateProperty;
        static FieldInfo stateField;
        static FieldInfo poolField, chaffPoolField, poolListField, velocityField, lifeTimeField, startTimeField, chaffVelocityField;

        public static Type FlareType { get { Resolve(); return flareType; } }

        public static Type ChaffType { get { Resolve(); return chaffType; } }

        public static bool ChaffAvailable { get { Resolve(); return chaffType != null && chaffPoolField != null && chaffVelocityField != null; } }

        public static bool MissileAvailable { get { Resolve(); return missileType != null && (firedField != null || firedProperty != null); } }

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
                chaffType = loaded.assembly.GetType("BDArmory.CounterMeasure.CMChaff");
                FindMissile(loaded.assembly);
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
            startTimeField = flareType.GetField("startTime", Any);
            if (chaffType != null && dropperType != null)
            {
                chaffPoolField = dropperType.GetField("chaffPool", Any);
                chaffVelocityField = chaffType.GetField("velocity", Any);
            }
            if (poolField == null || velocityField == null)
            {
                Debug.LogWarning("[AeronauticFlares] BDArmory's flare classes differ from 1.13.1, idle");
                poolField = null;
            }
        }

        static void FindMissile(Assembly assembly)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name != "MissileBase") continue;
                    missileType = type;
                    firedField = type.GetField("HasFired", Any);
                    firedProperty = type.GetProperty("HasFired", Any);
                    stateField = type.GetField("MissileState", Any);
                    stateProperty = type.GetProperty("MissileState", Any);
                    return;
                }
            }
            catch (ReflectionTypeLoadException) { }
        }

        // True for a one-part vessel that is a BDArmory missile in flight
        public static bool IsFiredMissile(Vessel vessel)
        {
            if (!MissileAvailable || vessel == null || !vessel.loaded || vessel.parts.Count != 1) return false;
            foreach (PartModule module in vessel.parts[0].Modules)
            {
                if (!missileType.IsInstanceOfType(module)) continue;
                object fired = firedField != null ? firedField.GetValue(module) : firedProperty.GetValue(module, null);
                return fired is bool && (bool)fired;
            }
            return false;
        }

        // BDArmory's state name for a fired missile ("Drop", "Boost", "Cruise", "PostThrust"...), or null when it cannot be read
        public static string MotorState(Vessel vessel)
        {
            if (missileType == null || vessel == null || vessel.parts.Count != 1) return null;
            foreach (PartModule module in vessel.parts[0].Modules)
            {
                if (!missileType.IsInstanceOfType(module)) continue;
                try
                {
                    object state = stateField != null ? stateField.GetValue(module) : stateProperty != null ? stateProperty.GetValue(module, null) : null;
                    return state != null ? state.ToString() : null;
                }
                catch (Exception) { return null; }
            }
            return null;
        }

        // The pooled flare objects, or null before BDArmory has built its pool
        public static List<GameObject> Pool()
        {
            return Available ? PoolOf(poolField) : null;
        }

        // The pooled chaff objects, or null before BDArmory has built its pool
        public static List<GameObject> ChaffPool()
        {
            return ChaffAvailable ? PoolOf(chaffPoolField) : null;
        }

        static List<GameObject> PoolOf(FieldInfo field)
        {
            var pool = field.GetValue(null) as Component;
            if (pool == null) return null;
            if (poolListField == null) poolListField = pool.GetType().GetField("pool", Any);
            return poolListField == null ? null : poolListField.GetValue(pool) as List<GameObject>;
        }

        // The velocity BDArmory threw a chaff cartridge with: set before it activates the object
        public static Vector3 ChaffVelocity(Component chaff)
        {
            return (Vector3)chaffVelocityField.GetValue(chaff);
        }

        public static Vector3 Velocity(Component flare)
        {
            return (Vector3)velocityField.GetValue(flare);
        }

        // When BDArmory lit the flare (its own clock), or now if it will not say
        public static float StartTime(Component flare)
        {
            return startTimeField == null ? Time.time : (float)startTimeField.GetValue(flare);
        }

        // BDArmory's flare lifetime in seconds
        public static float LifeTime(Component flare)
        {
            if (lifeTimeField == null) return 5f;
            return Mathf.Max((float)lifeTimeField.GetValue(flare), 0.5f);
        }
    }
}
