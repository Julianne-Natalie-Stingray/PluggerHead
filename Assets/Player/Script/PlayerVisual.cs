using UnityEngine;

/// <summary>Applies accepted movement input to the sprite without changing physics or attachment transforms.
/// 根据已接受的移动输入更新精灵朝向，不改变碰撞体或挂点的 Transform。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public sealed class PlayerVisual : MonoBehaviour
{
    [SerializeField, Tooltip("未翻转的原始精灵是否朝右。")]
    private bool spriteFacesRight = true;

    [SerializeField, Tooltip("可选的朝向标记，必须是 Visual 的子对象。")]
    private Transform facingMarker;

    [SerializeField] private Sprite wireMarkerSprite;
    [SerializeField] private Vector3 wireMarkerOffset = new Vector3(0f, 0.6f, 0f);

    private SpriteRenderer spriteRenderer;
    private Vector3 markerPosition;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (facingMarker != null && facingMarker.IsChildOf(transform) && facingMarker != transform)
        {
            markerPosition = facingMarker.localPosition;
        }
        else
        {
            facingMarker = null;
        }
    }

    public void ApplyMovementInput(float horizontal)
    {
        if (!isActiveAndEnabled || Mathf.Abs(horizontal) <= 0.01f)
        {
            return;
        }

        spriteRenderer.flipX = (horizontal > 0f) != spriteFacesRight;
        if (facingMarker != null)
        {
            facingMarker.localPosition = new Vector3(
                spriteRenderer.flipX ? -markerPosition.x : markerPosition.x, markerPosition.y, markerPosition.z);
        }
    }

    /// <summary>交互动画首帧显示操作后的持线类型；标记独立于玩家移动。</summary>
    public void SpawnWireMarker()
    {
        if (!isActiveAndEnabled || wireMarkerSprite == null)
        {
            return;
        }

        EnvironmentFacade environment = EnvironmentFacade.ForScene(gameObject.scene);
        Wire wire = environment != null ? environment.HeldWire : null;
        Color color = Color.black;
        if (wire != null && wire.IsHeld)
        {
            if (wire.Polarity == WirePolarity.Live)
            {
                color = Color.red;
            }
            else if (wire.Polarity == WirePolarity.Neutral)
            {
                color = Color.blue;
            }
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
        GameObject marker = new GameObject("WireMarker");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marker, gameObject.scene);
        marker.layer = gameObject.layer;
        marker.transform.position = transform.position + wireMarkerOffset;
        SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = wireMarkerSprite;
        markerRenderer.color = color;
        markerRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        markerRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
        marker.AddComponent<WireMarker>();
    }
}
