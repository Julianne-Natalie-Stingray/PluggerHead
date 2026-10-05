using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using System;
using System.Linq;

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
    /// True when Get() will reuse a pooled instance rather than create a new one.
    /// Implementation approach: compares against the pool's own capacity before delegating, because Unity's
    /// ObjectPool creates a fresh instance whenever it is empty instead of refusing, so a caller that needs a
    /// hard ceiling must ask this first. Reuse is preferred even when the pool is empty, so an empty pool
    /// below its ceiling still reports true and is allowed to grow into it.
    /// Get() 会复用池化实例而不是新建时返回 true.
    /// 实现思路: 在委托之前先与池自身容量比较, 因为 Unity 的 ObjectPool 在池空时总是新建实例而不拒绝,
    /// 因此需要硬上限的调用方必须先问这里. 即使池为空也优先复用之下的增长:
    /// 空池只要未达上限仍返回 true, 允许它增长到上限.
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
