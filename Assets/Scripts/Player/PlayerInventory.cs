using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 持有 Env 返回的拾取实例，并通过实例接口将原场景对象放回关卡。
/// 不复制或销毁道具；源对象被销毁后，在下次访问背包时清理实例。
/// Anchor 不进入背包，由 PlayerInteraction 独立放置与收回。
/// </summary>
[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [SerializeField]
    [Tooltip("该玩家所在关卡的环境服务；未指定时使用当前场景的 EnvironmentFacade。")]
    private EnvironmentFacade environment;

    private readonly List<IPickupInstance> items = new List<IPickupInstance>();
    private IReadOnlyList<IPickupInstance> readOnlyItems;

    public EnvironmentFacade Environment
    {
        get
        {
            if (environment == null || environment.gameObject.scene != gameObject.scene)
            {
                environment = EnvironmentFacade.ForScene(gameObject.scene);
            }

            return environment;
        }
    }

    /// <summary>道具实例集合变化后通知；Sprite 变化不属于背包集合变化。</summary>
    public event Action Changed;

    /// <summary>Env 的真实拾取实例，每个实例保留与原场景对象的绑定。</summary>
    public IReadOnlyList<IPickupInstance> Items
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

    /// <summary>通过环境拾取契约请求拾取，只有收到实例才加入背包。</summary>
    public bool PickUpItem(IEnvironmentPickup pickup)
    {
        Component source = pickup as Component;
        if (!CanOperate() || source == null || source.gameObject.scene != gameObject.scene ||
            !source.gameObject.activeInHierarchy ||
            !pickup.CanPickup || Contains(source.gameObject))
        {
            return false;
        }

        IPickupInstance item = pickup.Pickup(new InteractionDetails(gameObject, source.gameObject));
        if (item == null)
        {
            return false;
        }

        items.Add(item);
        Changed?.Invoke();
        return true;
    }

    public bool Contains(GameObject source)
    {
        RemoveDestroyedItems();
        if (source == null)
        {
            return false;
        }

        return items.Exists(item => item.SourceObject == source);
    }

    /// <summary>放下指定实例；Env 拒绝放置时保留背包内容。</summary>
    public bool DropItem(IPickupInstance item, Vector3 position)
    {
        RemoveDestroyedItems();
        if (!CanOperate() || item == null || !items.Contains(item) || !item.TryDrop(position))
        {
            return false;
        }

        items.Remove(item);
        Changed?.Invoke();
        return true;
    }

    private bool CanOperate()
    {
        PlayerMove movement = GetComponent<PlayerMove>();
        return isActiveAndEnabled && Time.timeScale > 0f &&
            (movement == null || (!movement.IsDead && !movement.IsInputLocked));
    }

    private void RemoveDestroyedItems()
    {
        if (items.RemoveAll(item => item == null || item.SourceObject == null) > 0)
        {
            Changed?.Invoke();
        }
    }
}
