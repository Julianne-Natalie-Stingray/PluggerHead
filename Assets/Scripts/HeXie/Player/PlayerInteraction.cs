using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 接收 Core 输入并向 Env 发出请求。
/// 查询最近目标和按类型分派是示例实现，可由后续玩法规则替换。
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
[DisallowMultipleComponent]
public class PlayerInteraction : MonoBehaviour
{
    public enum OperationMode
    {
        PickUp,
        Select
    }

    [SerializeField] private OperationMode dualTargetMode = OperationMode.PickUp;
    public OperationMode CurrentMode => dualTargetMode;
    [SerializeField, Min(0f)] private float interactionRadius = 2f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    private PlayerInventory inventory;
    private InputManager input;
    private readonly List<Collider2D> hits = new List<Collider2D>();

    private void Awake()
    {
        inventory = GetComponent<PlayerInventory>();
    }

    private void Start()
    {
        CoreFacade core = CoreFacade.Instance;
        if (core == null || core.Input == null || inventory.Environment == null)
        {
            Debug.LogError("PlayerInteraction 需要 Core 输入服务和背包的 Environment 引用。", this);
            enabled = false;
            return;
        }

        input = core.Input;
        input.SecondaryPressed += HandleOperation;
        input.TertiaryPressed += ToggleOperationMode;
    }

    private void OnEnable()
    {
        if (input != null)
        {
            input.SecondaryPressed += HandleOperation;
            input.TertiaryPressed += ToggleOperationMode;
        }
    }

    private void OnDisable()
    {
        if (input != null)
        {
            input.SecondaryPressed -= HandleOperation;
            input.TertiaryPressed -= ToggleOperationMode;
        }
    }

    /// <summary>K 只切换双属性目标的 J 操作，不直接发出 Env 请求。</summary>
    public void ToggleOperationMode()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f)
        {
            return;
        }

        dualTargetMode = dualTargetMode == OperationMode.PickUp
            ? OperationMode.Select
            : OperationMode.PickUp;
    }

    private OperationMode GetOperation(EnvInteractionTarget target)
    {
        if (target.CanPickUp && target.CanSelect)
        {
            return dualTargetMode;
        }

        return target.CanPickUp ? OperationMode.PickUp : OperationMode.Select;
    }

    private void HandleOperation()
    {
        if (Time.timeScale > 0f)
        {
            TryPerformOperation();
        }
    }

    /// <summary>每次只处理最近的一个目标；无目标时不发出请求。</summary>
    public bool TryPerformOperation()
    {
        if (!isActiveAndEnabled)
        {
            return false;
        }

        if (inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }
        EnvFacade environment = inventory.Environment;
        if (environment == null)
        {
            return false;
        }

        EnvInteractionTarget target = FindNearestTarget();
        if (target == null)
        {
            return false;
        }

        switch (GetOperation(target))
        {
            case OperationMode.PickUp:
                return environment.PickUpItem(new PickUpItemData
                {
                    Actor = gameObject,
                    Item = target.gameObject
                });
            case OperationMode.Select:
                environment.Interact(new InteractionData
                {
                    Actor = gameObject,
                    Target = target.gameObject
                });
                return true;
            default:
                return false;
        }
    }

    private EnvInteractionTarget FindNearestTarget()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(interactionLayers);
        filter.useTriggers = true;
        Vector2 origin = transform.position;
        Physics2D.OverlapCircle(origin, Mathf.Max(0f, interactionRadius), filter, hits);

        EnvInteractionTarget nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            EnvInteractionTarget candidate = hit.GetComponentInParent<EnvInteractionTarget>();
            if (candidate == null || !candidate.isActiveAndEnabled ||
                candidate.transform.IsChildOf(transform) ||
                (!candidate.CanPickUp && !candidate.CanSelect) ||
                (GetOperation(candidate) == OperationMode.PickUp &&
                 inventory.Contains(candidate.gameObject)))
            {
                continue;
            }

            // 示例以碰撞体表面到玩家的距离衡量远近；相同距离按实例 ID 稳定选择。
            float distance = (hit.ClosestPoint(origin) - origin).sqrMagnitude;
            if (distance < nearestDistance ||
                (distance == nearestDistance && nearest != null &&
                 candidate.GetInstanceID() < nearest.GetInstanceID()))
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, interactionRadius));
    }
}
