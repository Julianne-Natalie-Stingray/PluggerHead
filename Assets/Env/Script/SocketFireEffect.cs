using UnityEngine;

/// <summary>Plays the socket Fire sprite sequence once and removes the instance.
/// 插座 Fire 序列帧仅播放一次，结束后销毁实例。</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class SocketFireEffect : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.01f)] private float framesPerSecond = 8f;

    private SpriteRenderer spriteRenderer;
    private float elapsed;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (frames == null || frames.Length == 0 || framesPerSecond <= 0f ||
            float.IsNaN(framesPerSecond) || float.IsInfinity(framesPerSecond))
        {
            Destroy(gameObject);
            return;
        }
        spriteRenderer.sprite = frames[0];
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        int frame = Mathf.FloorToInt(elapsed * framesPerSecond);
        if (frames == null || frame >= frames.Length)
        {
            Destroy(gameObject);
            return;
        }
        spriteRenderer.sprite = frames[frame];
    }
}
