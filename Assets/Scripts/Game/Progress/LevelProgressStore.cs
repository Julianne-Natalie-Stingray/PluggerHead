using System;
using System.IO;
using UnityEngine;

/// <summary>Only the last level is persisted. Scene objects always come from the scene asset.
/// 进度文件只保存关卡标识，不保存位置、背包、电路或其他关卡内状态。</summary>
public sealed class LevelProgressStore
{
    [Serializable]
    private sealed class Data
    {
        public int level = -1;
    }

    private readonly string path;
    private Data data = new Data();

    public LevelProgressStore(string path)
    {
        this.path = path;
        Reload();
    }

    public bool TryGetLevel(out SceneId level)
    {
        level = (SceneId)data.level;
        return data.level >= 0 && Enum.IsDefined(typeof(SceneId), level);
    }

    public void Reload()
    {
        data = new Data();
        try
        {
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    JsonUtility.FromJsonOverwrite(json, data);
                }
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            data = new Data();
            Debug.LogWarning($"Cannot load level progress: {exception.Message}");
        }
    }

    public bool SaveLevel(SceneId level)
    {
        Data next = new Data { level = (int)level };
        string temporaryPath = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(next));
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }

            data = next;
            return true;
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
        {
            Debug.LogWarning($"Cannot save level progress: {exception.Message}");
            return false;
        }
    }
}
