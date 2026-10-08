using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 开场 CG：播放 CG.png 内含的多个分镜。
/// 每格分镜上覆盖一张黑色图片，每次点击将一个黑色遮罩的 alpha 从 1 渐变为 0（遮罩淡出、分镜淡入）。
/// 全部分镜淡入后，切换到 HubScene。
/// Subsystem: UI (CG)。
/// 存在位置：CGScene 的一个 GameObject 上。
/// 职能：运行时自建 Canvas 与整张 CG 图、按排版铺设 5 个黑色遮罩、处理点击推进与最终切场景。
/// 不负责：CG.png 的切片（本实现直接使用整张贴图 + 遮罩），也不负责游戏内其它 UI。
/// </summary>
[DisallowMultipleComponent]
public sealed class CgPlayer : MonoBehaviour
{
    [SerializeField, Tooltip("整张 CG 贴图（含全部分镜）。需为 Sprite。")]
    private Sprite cgSprite;

    [SerializeField, Tooltip("单个分镜淡入时长（秒）。")]
    private float fadeDuration = 0.5f;

    [SerializeField, Tooltip("最后一个分镜淡入完成后、自动切场景前的等待（秒）。")]
    private float holdAfterLastPanel = 0.8f;

    /// <summary>
    /// 5 个分镜在整张 CG 内的归一化矩形（anchorMin/anchorMax 四元组，y 从下往上）。
    /// 排版：1,2 / 3,4 / 5 —— 前两行左右各一，末行整行。
    /// </summary>
    private static readonly Vector4[] PanelAnchors =
    {
        new Vector4(0f,   2f / 3f, 0.5f, 1f),        // 1 左上
        new Vector4(0.5f, 2f / 3f, 1f,   1f),        // 2 右上
        new Vector4(0f,   1f / 3f, 0.5f, 2f / 3f),   // 3 左中
        new Vector4(0.5f, 1f / 3f, 1f,   2f / 3f),   // 4 右中
        new Vector4(0f,   0f,      1f,   1f / 3f),   // 5 底部整行
    };

    private Image[] overlays;
    private int revealed;
    private bool finished;
    private Sprite whiteSprite;

    private void Start()
    {
        BuildUi();
        revealed = 0;
    }

    private void Update()
    {
        if (finished)
        {
            return;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Advance();
        }
    }

    private void Advance()
    {
        if (revealed >= PanelAnchors.Length)
        {
            return;
        }

        Image overlay = overlays[revealed];
        revealed++;

        if (overlay != null)
        {
            StartCoroutine(FadeOut(overlay));
        }

        if (revealed >= PanelAnchors.Length)
        {
            StartCoroutine(Finish());
        }
    }

    private IEnumerator FadeOut(Image overlay)
    {
        Color from = overlay.color;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;
            Color color = from;
            color.a = Mathf.Lerp(1f, 0f, t);
            overlay.color = color;
            yield return null;
        }
    }

    private IEnumerator Finish()
    {
        yield return new WaitForSecondsRealtime(holdAfterLastPanel);

        if (finished)
        {
            yield break;
        }
        finished = true;

        CoreFacade core = CoreFacade.Instance;
        if (core && core.SceneSwitch)
        {
            core.SceneSwitch.RequestSwitch(SceneId.HubScene);
        }
    }

    private void BuildUi()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
        }
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(432f, 240f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        whiteSprite = CreateWhiteSprite();

        // 整张 CG，铺满画布。
        Image cg = CreateImage("CG", transform);
        cg.sprite = cgSprite;
        cg.color = Color.white;
        Stretch(cg.rectTransform);

        // 每个分镜上覆盖黑色遮罩。
        overlays = new Image[PanelAnchors.Length];
        for (int i = 0; i < PanelAnchors.Length; i++)
        {
            Image overlay = CreateImage($"Overlay{i + 1}", transform);
            overlay.sprite = whiteSprite;
            overlay.color = Color.black;
            overlay.raycastTarget = false;
            Anchor(overlay.rectTransform, PanelAnchors[i]);
            overlays[i] = overlay;
        }
    }

    private static Sprite CreateWhiteSprite()
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
    }

    private static Image CreateImage(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<Image>();
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rect, Vector4 anchors)
    {
        rect.anchorMin = new Vector2(anchors.x, anchors.y);
        rect.anchorMax = new Vector2(anchors.z, anchors.w);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
