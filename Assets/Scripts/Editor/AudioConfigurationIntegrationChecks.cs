using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>Validates pool configuration using isolated, unsaved ScriptableObjects.</summary>
public static class AudioConfigurationIntegrationChecks
{
    public static void CheckMaxPoolSize(int serializedValue)
    {
        var configs = ScriptableObject.CreateInstance<AudioManagerConfigs>();
        try
        {
            FieldInfo field = typeof(AudioManagerConfigs).GetField("maxPoolSize", BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(configs, serializedValue);
            int expected = Math.Max(1, serializedValue);
            Require(configs.MaxPoolSize == expected, "Runtime property must return a positive pool capacity.");
            Require((int)field.GetValue(configs) == serializedValue, "Reading runtime capacity must not rewrite the asset.");
            // Construct the actual Unity pool through the same configuration property used by AudioManager.
            using (var pool = new ObjectPool<object>(() => new object(),
                collectionCheck: configs.CollectionCheck, defaultCapacity: configs.DefaultCapacity, maxSize: configs.MaxPoolSize))
            {
                object instance = pool.Get();
                pool.Release(instance);
                Require(pool.CountInactive == 1, "The normalized capacity must support creating and retaining an object.");
            }
            int defaultCapacity = configs.DefaultCapacity;
            int prewarmAmount = configs.PrewarmAmount;
            int maxSoundInstance = configs.MaxSoundInstance;
            typeof(AudioManagerConfigs).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(configs, null);
            Require((int)field.GetValue(configs) == expected, "OnValidate must persist the positive normalized pool size.");
            Require(configs.DefaultCapacity == defaultCapacity && configs.PrewarmAmount == prewarmAmount &&
                configs.MaxSoundInstance == maxSoundInstance, "Pool size validation must not adjust the other valid pool settings.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(configs);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
