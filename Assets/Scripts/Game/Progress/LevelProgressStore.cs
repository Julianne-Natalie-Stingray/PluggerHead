/// <summary>本次运行内的顺序通关进度，不读取或写入存档。</summary>
public sealed class LevelProgressStore
{
    public const int LevelCount = 3;
    public int CompletedLevels { get; private set; }

    public bool IsUnlocked(int levelNumber)
    {
        return levelNumber >= 1 && levelNumber <= LevelCount && levelNumber <= CompletedLevels + 1;
    }

    /// <summary>只接受已解锁关卡的通关；重玩不会倒退或越级解锁。</summary>
    public bool CompleteLevel(int levelNumber)
    {
        if (!IsUnlocked(levelNumber))
        {
            return false;
        }
        if (levelNumber > CompletedLevels)
        {
            CompletedLevels = levelNumber;
        }
        return true;
    }
}
