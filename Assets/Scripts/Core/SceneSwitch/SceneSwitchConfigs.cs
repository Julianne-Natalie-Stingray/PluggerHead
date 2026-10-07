using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// The whitelist of switchable scenes: which SceneId resolves to which scene name.
/// Subsystem: Core (SceneSwitch).
/// Where it lives: an asset read by SceneSwitchManager and the hub Portal components.
/// Responsibility: hold scene mappings and gameplay flags, answer lookups, warn about blank names on validation,
/// and offer a manual button to remove duplicate IDs. Runtime queries do not sanitize the list.
/// Does NOT own: whether the named scene is registered for loading. That is Unity's Build Settings, and
/// SceneSwitchManager checks it separately, so a whitelist entry pointing at an unregistered scene is reported
/// rather than silently trusted.
/// Lifetime: the project supplies a shared configuration asset; this type has no automatic creation logic.
/// Data overview: a flat list of SceneId, scene name and gameplay flag. This project uses file names without
/// extensions, also matching the exact Scene.name comparison used for progress tracking.
/// Paradigms: none. It is a data lookup.
/// 可切换场景的白名单: 哪个 SceneId 解析到哪个场景名.
/// Subsystem 归属: Core (SceneSwitch).
/// 存在位置: 由 SceneSwitchManager 和大厅 Portal 组件读取的资产.
/// 职能: 持有场景映射与玩法标记、提供查找、校验时警告空名, 并提供手动按键移除重复 ID.
/// 运行时查询不会清理列表.
/// 不负责: 被命名的场景是否已注册可加载. 那属于 Unity 的 Build Settings, 由 SceneSwitchManager 另行检查,
/// 因此指向未注册场景的白名单条目会被报告出来, 而不是被静默信任.
/// 生命周期: 项目提供共享配置资产; 类型自身没有自动创建逻辑.
/// 数据概览: 扁平的 SceneId、场景名与玩法标记列表. 本项目使用不带扩展名的场景文件名,
/// 也与进度追踪按 Scene.name 精确比较的约定一致.
/// 使用范式: 无. 它是数据查找.
/// </summary>
[CreateAssetMenu(
    fileName = MenuTool.Game.BasicSettings.SceneSwitchConfigs.FileName,
    menuName = MenuTool.Game.BasicSettings.SceneSwitchConfigs.Path)]
public class SceneSwitchConfigs : ScriptableObject
{
    [SerializeField] private List<Entry> scenes = new();

    #region APIs

    public bool IsGameplayLevel(SceneId id)
    {
        return scenes.Exists(entry => entry.Id == id && entry.IsGameplayLevel);
    }

    public bool TryGetGameplayLevel(string sceneName, out SceneId id)
    {
        foreach (Entry entry in scenes)
        {
            if (entry.IsGameplayLevel && entry.SceneName == sceneName)
            {
                id = entry.Id;
                return true;
            }
        }

        id = default;
        return false;
    }

    /// <summary>
    /// Single entry point for resolving a key to the scene name it stands for.
    /// Implementation approach: a linear scan returning the first matching ID, even if its name is blank.
    /// Called by switch requests and by the main menu's per-frame availability refresh.
    /// 把键解析为它所代表的场景名的单一入口.
    /// 实现思路: 线性扫描并返回首个匹配 ID, 即使其名字为空也返回 true.
    /// 切换请求和主菜单每帧的可用性刷新都会调用.
    /// </summary>
    public bool TryGetSceneName(SceneId requested, out string sceneName)
    {
        foreach (Entry entry in scenes)
        {
            if (entry.Id != requested)
                continue;

            sceneName = entry.SceneName;
            return true;
        }

        sceneName = null;
        return false;
    }

    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        ReportBlankNames();
    }

    [Button("Remove Duplicates")]
    private void RemoveDuplicates()
    {
        if (scenes == null || scenes.Count == 0)
            return;

        HashSet<SceneId> seen = new();
        int originalCount = scenes.Count;

        scenes.RemoveAll(entry => !seen.Add(entry.Id));

        int removed = originalCount - scenes.Count;

        if (removed <= 0)
            return;

        GameLog.Warning(this)
            .Subsystem("Core")
            .Name(LogName.Class)
            .Issue(LogIssue.Specify($"{nameof(scenes)} contained duplicate SceneIds. "))
            .Action(LogAction.Specify($"Removed {removed} duplicate(s), keeping the earliest occurrence. "))
            .Write();
    }

    private void ReportBlankNames()
    {
        if (scenes == null)
            return;

        foreach (Entry entry in scenes)
        {
            if (!string.IsNullOrWhiteSpace(entry.SceneName))
                continue;

            GameLog.Warning(this)
                .Subsystem("Core")
                .Name(LogName.Class)
                .Issue(LogIssue.Invalid(nameof(entry.SceneName)))
                .Action(LogAction.Specify($"Fill in the scene name for {entry.Id}. "))
                .Write();
        }
    }
#endif

    /// <summary>
    /// Internal serialized mapping with a gameplay flag for menu availability and reverse progress lookup.
    /// Callers query through the configuration API rather than depending on the entry representation.
    /// 内部序列化映射, 含用于菜单可用性及进度反向查询的玩法标记.
    /// 调用方通过配置 API 查询, 不依赖条目的内部表示.
    /// </summary>
    [System.Serializable]
    private struct Entry
    {
        [SerializeField] private SceneId id;
        [SerializeField] private string sceneName;
        [SerializeField] private bool isGameplayLevel;

        public SceneId Id => id;
        public string SceneName => sceneName;
        public bool IsGameplayLevel => isGameplayLevel;
    }
}
