using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 接收 Core 输入，寻找最近的环境组件并调用真实交互契约。
/// K 放置不限数量的 Anchor，J 收回最近的 Anchor 或操作其他环境目标。
/// </summary>
[DisallowMultipleComponent]
public class PlayerInteraction : MonoBehaviour
{
    [SerializeField] private Anchor anchorPrefab;
    [SerializeField, Min(0f)] private float interactionRadius = 2f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    private InputManager input;
    private PlayerMove playerMove;
    private readonly List<Collider2D> hits = new List<Collider2D>();

    private void Awake()
    {
        playerMove = GetComponent<PlayerMove>();
    }

    private void Start()
    {
        CoreFacade core = CoreFacade.Instance;
        if (core == null || core.Input == null || EnvironmentFacade.ForScene(gameObject.scene) == null)
        {
            Debug.LogError("PlayerInteraction 需要 Core 输入服务和所在关卡的 EnvironmentFacade。", this);
            enabled = false;
            return;
        }

        input = core.Input;
        BindInput();
    }

    private void OnEnable()
    {
        BindInput();
    }

    private void BindInput()
    {
        UnbindInput();
        if (input != null)
        {
            input.SecondaryPressed += HandleOperation;
            input.TertiaryPressed += HandlePlaceAnchor;
        }
    }

    private void OnDisable()
    {
        UnbindInput();
    }

    private void UnbindInput()
    {
        if (input != null)
        {
            input.SecondaryPressed -= HandleOperation;
            input.TertiaryPressed -= HandlePlaceAnchor;
        }
    }

    private void HandlePlaceAnchor()
    {
        TryPlaceAnchor();
    }

    /// <summary>在玩家当前位置放置新的锚点；持线时自动绕线，不消耗背包或数量。</summary>
    public bool TryPlaceAnchor()
    {
        if (!CanOperate() || anchorPrefab == null)
        {
            return false;
        }

        EnvironmentFacade environment = EnvironmentFacade.ForScene(gameObject.scene);
        Vector3 position = transform.position;
        if (environment == null || !IsFinite(position.x) || !IsFinite(position.y) || !IsFinite(position.z))
        {
            return false;
        }

        if (!environment.TryGetTilePosition(position, out position))
        {
            return false;
        }
        Anchor anchor = Instantiate(anchorPrefab, position, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(anchor.gameObject, gameObject.scene);
        anchor.gameObject.SetActive(true);
        environment.RegisterAnchor(anchor);
        if (environment.HeldWire != null)
        {
            anchor.Interact(new InteractionDetails(gameObject, anchor.gameObject));
        }
        EnvironmentFacade.PlayInteractionAudio(AudioId.Activated, position);
        return true;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void HandleOperation()
    {
        TryPerformOperation();
    }

    private bool CanOperate()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        return isActiveAndEnabled && Time.timeScale > 0f &&
            (playerMove == null || (!playerMove.IsDead && !playerMove.IsInputLocked));
    }

    /// <summary>每次只处理最近的一个目标；无目标时不发出请求。</summary>
    public bool TryPerformOperation()
    {
        if (!CanOperate())
        {
            return false;
        }

        if (EnvironmentFacade.ForScene(gameObject.scene) == null)
        {
            return false;
        }

        MonoBehaviour target = FindNearestTarget();
        if (target == null)
        {
            return false;
        }

        bool performed = PerformOperation(target);
        if (performed && playerMove != null)
        {
            playerMove.TryStartInteractionAnimation();
        }

        return performed;
    }

    private bool PerformOperation(MonoBehaviour target)
    {
        if (target is Anchor anchor)
        {
            Vector3 position = anchor.transform.position;
            bool reclaimed = anchor.TryReclaim(new InteractionDetails(gameObject, anchor.gameObject));
            if (reclaimed)
            {
                EnvironmentFacade.PlayInteractionAudio(AudioId.PlugOut, position);
            }
            return reclaimed;
        }

        IEnvironmentInteractable interactable = target as IEnvironmentInteractable;
        if (interactable == null || (!interactable.CanInteract &&
            !(target is Portal portal && portal.CanShowLockedFeedback)))
        {
            return false;
        }

        InteractionDetails details = new InteractionDetails(gameObject, target.gameObject);
        bool performed = false;
        System.Action<IEnvironmentInteractable> onInteracted = node => performed = true;
        interactable.OnInteracted += onInteracted;
        try
        {
            interactable.Interact(details);
        }
        finally
        {
            interactable.OnInteracted -= onInteracted;
        }
        return performed;
    }

    private MonoBehaviour FindNearestTarget()
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(interactionLayers);
        filter.useTriggers = true;
        Vector2 origin = transform.position;
        gameObject.scene.GetPhysicsScene2D().OverlapCircle(origin, Mathf.Max(0f, interactionRadius), filter, hits);

        MonoBehaviour nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider2D hit in hits)
        {
            if (hit == null || hit.gameObject.scene != gameObject.scene || hit.transform.IsChildOf(transform) ||
                hit.GetComponent<Wire>() != null)
            {
                continue;
            }

            foreach (MonoBehaviour candidate in hit.GetComponentsInParent<MonoBehaviour>())
            {
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    candidate.transform.IsChildOf(transform))
                {
                    continue;
                }

                bool canInteract = candidate is Anchor anchor
                    ? anchor.CanReclaim
                    : (candidate is IEnvironmentInteractable interactable && interactable.CanInteract) ||
                        (candidate is Portal portal && portal.CanShowLockedFeedback);
                if (!canInteract)
                {
                    continue;
                }

                // 以碰撞体表面到玩家的距离衡量远近；相同距离按实例 ID 稳定选择。
                float distance = (hit.ClosestPoint(origin) - origin).sqrMagnitude;
                if (distance < nearestDistance ||
                    (distance == nearestDistance && nearest != null &&
                     candidate.GetInstanceID() < nearest.GetInstanceID()))
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
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
