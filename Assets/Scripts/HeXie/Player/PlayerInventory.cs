using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 保存 Env 道具的原始场景对象引用，不复制或销毁道具。
/// 场景道具被销毁后，对应引用会在下次访问背包时清理。
/// 当前背包不限制容量，不实现堆叠；最终物品模型和消耗规则由 Jill 决定。
/// </summary>
[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [SerializeField]
    [Tooltip("处理该玩家拾取和使用请求的 Env。")]
    private EnvFacade environment;

    private readonly List<GameObject> items = new List<GameObject>();
    private IReadOnlyList<GameObject> readOnlyItems;

    public EnvFacade Environment => environment;

    /// <summary>道具引用集合变化后通知；Sprite 变化不属于背包集合变化。</summary>
    public event Action Changed;

    /// <summary>只读的道具引用列表；引用本身仍指向 Env 中的原对象。</summary>
    public IReadOnlyList<GameObject> Items
    {
        get
        {
            RemoveDestroyedItems();
            if (readOnlyItems == null)
            {
                readOnlyItems = items.AsReadOnly();
            }

            return readOnlyItems;
        }
    }

    public int Count => Items.Count;

    /// <summary>通过 Inspector 指定的 Env 请求拾取，成功时保存原物品引用。</summary>
    public bool PickUpItem(GameObject item)
    {
        return environment != null && environment.PickUpItem(new PickUpItemData
        {
            Actor = gameObject,
            Item = item
        });
    }

    /// <summary>请求使用已持有的物品；返回 true 仅表示请求已发出。</summary>
    public bool UseItem(GameObject item, GameObject target = null)
    {
        return environment != null && environment.UseItem(new UseItemData
        {
            Actor = gameObject,
            Item = item,
            Target = target
        });
    }

    public bool Contains(GameObject item)
    {
        RemoveDestroyedItems();
        return item != null && items.Contains(item);
    }

    /// <summary>由 Env 在拾取时加入引用，拒绝空对象及重复引用。</summary>
    internal bool TryAddItem(GameObject item)
    {
        if (item == null || Contains(item))
        {
            return false;
        }

        items.Add(item);
        Changed?.Invoke();
        return true;
    }

    /// <summary>供消耗或丢弃逻辑移除引用；不销毁、不移动物品对象。</summary>
    public bool RemoveItem(GameObject item)
    {
        RemoveDestroyedItems();
        if (item == null || !items.Remove(item))
        {
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    private void RemoveDestroyedItems()
    {
        if (items.RemoveAll(item => item == null) > 0)
        {
            Changed?.Invoke();
        }
    }
}
