using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using System;
using System.Linq;

/// <summary>
/// Component pool wrapper. Get activates and Release deactivates; callers own checkout lifetime.
/// poolSize limits inactive retention, while a hard creation limit requires a separate CanReuse check.
/// 组件对象池包装. Get 激活、Release 禁用; 调用方负责借还配对.
/// poolSize 限制闲置保留数量, 创建硬上限需调用方另行检查 CanReuse.
/// </summary>
public sealed class GameObjectPool<TPooled>
    where TPooled : Component
{
    private TPooled pooledGameObject;
    private Transform poolRoot;

    private readonly ObjectPool<TPooled> pool;
    private readonly List<TPooled> activeInPool;

    public int CountActive => pool.CountActive;
    public int CountInactive => pool.CountInactive;
    public int CountAll => pool.CountAll;

    /// <summary>
    /// True when an inactive instance exists OR CountAll is below the caller-supplied creation ceiling.
    /// This is an advisory check, not a promise of reuse or a reservation. Get itself does not enforce it.
    /// An inactive object is allowed even if maxSize is smaller than the current CountAll.
    /// 有闲置对象, 或 CountAll 小于调用方传入的创建上限时返回 true.
    /// 这是建议性检查, 不保证复用, 不预留名额; Get 本身不执行此限制.
    /// 即使 maxSize 小于当前 CountAll, 只要有闲置对象仍返回 true.
    /// </summary>
    public bool CanReuse(int maxSize)
        => CountInactive > 0 || CountAll < maxSize;


    public GameObjectPool(
        TPooled pooled,
        Transform root,
        Action<TPooled> onGet = null,
        Action<TPooled> onRelease = null,
        bool collectionCheck = true,
        int defaultCapacity = 10,
        int poolSize = 100)
    {
        pooledGameObject = pooled;
        poolRoot = root;
        
        pool = new(
            createFunc: Create,
            actionOnGet: item =>
            {
                item.gameObject.SetActive(true);
                onGet?.Invoke(item);
            },
            actionOnRelease: item =>
            {
                onRelease?.Invoke(item);
                item.gameObject.SetActive(false);
            },
            actionOnDestroy: item =>
            {
                UnityEngine.Object.Destroy(item.gameObject);
            },
            collectionCheck: collectionCheck,
            defaultCapacity: defaultCapacity,
            maxSize: poolSize
        );

        activeInPool = new();
    }

    public TPooled Get()
    {
        var instance = pool.Get();
        activeInPool.Add(instance);
        return instance;
    }

    public void Release(TPooled item)
    {
        pool.Release(item);
        activeInPool.Remove(item);
    }

    public void Clear()
    {
        pool.Clear();
        activeInPool.Clear();
    }

    public void Prewarm(int count)
    {
        List<TPooled> instances = new();
        
        for (int i = 0; i < count; i++)
            instances.Add(pool.Get());
        
        foreach (var instance in instances)
            pool.Release(instance);
    }

    public int CountInActive(Func<TPooled, bool> predicate)
        => activeInPool.Count(predicate);
    
    private TPooled Create()
    {
        var instance = UnityEngine.Object.Instantiate(
            pooledGameObject,
            poolRoot);
        instance.gameObject.SetActive(false);
        return instance;
    }
}
