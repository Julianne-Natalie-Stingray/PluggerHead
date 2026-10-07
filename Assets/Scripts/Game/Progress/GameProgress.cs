using UnityEngine;

/// <summary>运行期间跨场景共享通关进度；每次启动清空，与音量偏好无关。</summary>
public static class GameProgress
{
    private static LevelProgressStore store;
    public static LevelProgressStore Store => store ?? (store = new LevelProgressStore());

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Initialize()
    {
        store = null;
    }
}
