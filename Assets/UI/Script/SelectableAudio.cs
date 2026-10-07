using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>UI feedback through the shared SFX mixer, including paused menus.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.UI.Selectable))]
public sealed class SelectableAudio : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private bool closesPanel;

    private UnityEngine.UI.Selectable selectable;
    private UnityEngine.UI.Slider slider;
    private UnityEngine.UI.Button button;
    private int? pressedPointer;
    private int pointerReleaseFrame = -1;
    private float nextSliderSound;

    private void Awake()
    {
        selectable = GetComponent<UnityEngine.UI.Selectable>();
        slider = selectable as UnityEngine.UI.Slider;
        button = selectable as UnityEngine.UI.Button;
    }

    private void OnEnable()
    {
        nextSliderSound = 0f;
        if (slider != null)
        {
            slider.onValueChanged.AddListener(OnSliderChanged);
        }
        GameStateManager.Changed += OnStateChanged;
        OnStateChanged(GameStateManager.Current);
    }

    private void OnDisable()
    {
        pressedPointer = null;
        GameStateManager.Changed -= OnStateChanged;
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
        }
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClicked);
        }
    }

    private void OnStateChanged(GameState state)
    {
        if (button == null)
        {
            return;
        }
        button.onClick.RemoveListener(OnButtonClicked);
        if (state != GameState.Loading)
        {
            button.onClick.AddListener(OnButtonClicked);
        }
        else
        {
            pressedPointer = null;
        }
    }

    private bool CanPlay => isActiveAndEnabled && selectable != null &&
        selectable.IsInteractable() && GameStateManager.Current != GameState.Loading;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanPlay || slider != null || eventData.button != PointerEventData.InputButton.Left ||
            pressedPointer.HasValue)
        {
            return;
        }
        pressedPointer = eventData.pointerId;
        Play(closesPanel ? AudioId.ButtonPressClose : AudioId.ButtonPressOpen);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || pressedPointer != eventData.pointerId)
        {
            return;
        }
        pressedPointer = null;
        if (CanPlay)
        {
            pointerReleaseFrame = Time.frameCount;
            Play(closesPanel ? AudioId.ButtonReleaseClose : AudioId.ButtonReleaseOpen);
        }
    }

    private void OnButtonClicked()
    {
        // Button has already accepted the click/submit. Its business callback may have
        // hidden this panel or entered Loading; still complete this accepted feedback.
        // UnityEvent keeps the invocation snapshot even if OnDisable removes the listener.
        if (pointerReleaseFrame == Time.frameCount)
        {
            pointerReleaseFrame = -1;
            return;
        }
        Play(closesPanel ? AudioId.ButtonReleaseClose : AudioId.ButtonReleaseOpen);
    }

    private void OnSliderChanged(float value)
    {
        if (!CanPlay || Time.unscaledTime < nextSliderSound)
        {
            return;
        }
        nextSliderSound = Time.unscaledTime + 0.1f;
        Play(AudioId.Switch);
    }

    private static void Play(AudioId id)
    {
        CoreFacade core = CoreFacade.Instance;
        if (core != null && core.Audio != null)
        {
            core.Audio.CreateBuilder().WithAllowWhileFrozen(true).WithSurviveFreeze(true).Play(id);
        }
    }
}
