using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

/// <summary>
/// The whitelist of switchable scenes: which SceneId resolves to which scene name.
/// Subsystem: Core (SceneSwitch).
/// Where it lives: nowhere. It is an asset referenced by SceneSwitchManager, which is the only reader.
/// Responsibility: hold the SceneId to scene-name mapping, answer lookups for it, and keep itself free of
/// duplicates and blank names.
/// Does NOT own: whether the named scene is registered for loading. That is Unity's Build Settings, and
/// SceneSwitchManager checks it separately, so a whitelist entry pointing at an unregistered scene is reported
/// rather than silently trusted.
/// Lifetime: an asset; it lives as long as the project does. It is never created or destroyed at runtime.
/// Data overview: one flat list of entries, each pairing a SceneId with a scene name. The name must equal the
/// scene file name without its extension, which is what Unity's build-index lookup accepts.
/// Paradigms: none. It is a data lookup.
/// 可切换场景的白名单: 哪个 SceneId 解析到哪个场景名.
/// Subsystem 归属: Core (SceneSwitch).
/// 存在位置: 无. 它是被 SceneSwitchManager 引用的资产, 而后者是它唯一的读者.
/// 职能: 持有 SceneId 到场景名的映射, 为它提供查找, 并保持自身无重复、无空名.
/// 不负责: 被命名的场景是否已注册可加载. 那属于 Unity 的 Build Settings, 由 SceneSwitchManager 另行检查,
/// 因此指向未注册场景的白名单条目会被报告出来, 而不是被静默信任.
/// 生命周期: 资产; 与工程同寿. 运行时既不创建也不销毁.
/// 数据概览: 一张扁平的条目列表, 每条把一个 SceneId 与一个场景名配对. 名字必须等于场景文件名(不含扩展名),
/// 因为 Unity 的构建索引查找接受的就是它.
/// 使用范式: 无. 它是数据查找.
/// </summary>
[CreateAssetMenu(
    fileName = MenuTool.Game.BasicSettings.SceneSwitchConfigs.FileName,
    menuName = MenuTool.Game.BasicSettings.SceneSwitchConfigs.Path)]
public class SceneSwitchConfigs : ScriptableObject
{
    [SerializeField] private List<Entry> scenes = new();

    #region APIs

    /// <summary>
    /// Single entry point for resolving a key to the scene name it stands for.
    /// Implementation approach: a linear scan over a list whose length is the number of switchable scenes, and
    /// it runs once per switch request rather than per frame, so a lookup index would add a second source of
    /// truth for no measurable gain.
    /// 把键解析为它所代表的场景名的单一入口.
    /// 实现思路: 对一张长度等于可切换场景数的列表做线性扫描, 且每次切换请求只运行一次而非每帧运行,
    /// 因此建查找索引只会多出一个真相来源, 而换不来可度量的收益.
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
    /// One mapping from a switchable scene's key to its scene name.
    /// It is nested because it has no meaning outside this list: a whitelist entry is not a concept anything
    /// else in the project should be able to name.
    /// 一条从可切换场景的键到其场景名的映射.
    /// 它之所以嵌套, 是因为它在这张列表之外没有意义: 白名单条目不是一个工程中其他东西应当能够命名的概念.
    /// </summary>
    [System.Serializable]
    private struct Entry
    {
        [SerializeField] private SceneId id;
        [SerializeField] private string sceneName;

        public SceneId Id => id;
        public string SceneName => sceneName;
    }
}
