using System.IO;
using UnityEngine;

/// <summary>Session access to the level-only save, separate from player preferences.</summary>
public static class GameProgress
{
    private static LevelProgressStore store;
    public static LevelProgressStore Store => store ?? (store = new LevelProgressStore(
        Path.Combine(Application.persistentDataPath, "LevelProgress.json")));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        store = null;
    }
}
