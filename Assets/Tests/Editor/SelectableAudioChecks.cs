using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

public static class SelectableAudioChecks
{
    public static void CheckVariants()
    {
        var data = ScriptableObject.CreateInstance<AudioClipData>();
        var primary = AudioClip.Create("Primary", 100, 1, 1000, false);
        var alternate = AudioClip.Create("Alternate", 100, 1, 1000, false);
        UnityEngine.Random.State random = UnityEngine.Random.state;
        try
        {
            Set(data, "clip", primary);
            Require(data.SelectClip() == primary, "No variants must use the primary clip.");
            Set(data, "variants", new AudioClip[] { null, alternate, null });
            UnityEngine.Random.InitState(1729);
            bool sawPrimary = false;
            bool sawAlternate = false;
            for (int i = 0; i < 100; i++)
            {
                AudioClip selected = data.SelectClip();
                Require(selected == primary || selected == alternate, "Only configured non-null clips may be selected.");
                sawPrimary |= selected == primary;
                sawAlternate |= selected == alternate;
            }
            Require(sawPrimary && sawAlternate, "Both configured clips must be reachable.");
            Set(data, "variants", new AudioClip[] { null });
            Require(data.SelectClip() == primary, "Empty slots must fall back to the primary clip.");
        }
        finally
        {
            UnityEngine.Random.state = random;
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(primary);
            Object.DestroyImmediate(alternate);
        }
    }

    public static void CheckUI(AudioManager manager, Transform parent)
    {
        var events = new GameObject("UI audio test events", typeof(EventSystem));
        events.transform.SetParent(parent);
        EventSystem system = events.GetComponent<EventSystem>();
        var root = new GameObject("UI audio button", typeof(RectTransform));
        root.transform.SetParent(parent);
        root.SetActive(false);
        var button = root.AddComponent<UnityEngine.UI.Button>();
        // Register the business callback first, as authored persistent callbacks run first.
        button.onClick.AddListener(() => root.SetActive(false));
        var audio = root.AddComponent<SelectableAudio>();
        root.SetActive(true);
        int count = manager.Registry.Count;
        ExecuteEvents.Execute(root, new BaseEventData(system), ExecuteEvents.submitHandler);
        Require(!root.activeSelf && manager.Registry.Count == count + 1,
            "Accepted keyboard submit must play once even when the business callback hides the panel.");

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(GameStateManager.EnterLoading);
        root.SetActive(true);
        count = manager.Registry.Count;
        ExecuteEvents.Execute(root, new BaseEventData(system), ExecuteEvents.submitHandler);
        Require(GameStateManager.Current == GameState.Loading && manager.Registry.Count == count + 1,
            "Accepted submit must complete feedback after entering Loading.");
        GameStateManager.ExitLoading();

        root.SetActive(false);
        button.onClick.RemoveAllListeners();
        root.SetActive(true);
        var pointer = new PointerEventData(system) { button = PointerEventData.InputButton.Left, pointerId = 7 };
        count = manager.Registry.Count;
        ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerClickHandler);
        Require(manager.Registry.Count == count + 2, "Pointer activation must play down/up once without duplicate click audio.");
        pointer.button = PointerEventData.InputButton.Right;
        audio.OnPointerDown(pointer);
        audio.OnPointerUp(pointer);
        Require(manager.Registry.Count == count + 2, "Right click must be silent.");
        pointer.button = PointerEventData.InputButton.Left;
        button.interactable = false;
        audio.OnPointerDown(pointer);
        audio.OnPointerUp(pointer);
        Require(manager.Registry.Count == count + 2, "Disabled controls must be silent.");
        button.interactable = true;
        GameStateManager.EnterLoading();
        audio.OnPointerDown(pointer);
        audio.OnPointerUp(pointer);
        ExecuteEvents.Execute(root, pointer, ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(root, new BaseEventData(system), ExecuteEvents.submitHandler);
        Require(manager.Registry.Count == count + 2, "Complete pointer and submit events already in Loading must be silent.");
        GameStateManager.ExitLoading();

        GameStateManager.Freeze();
        count = manager.Registry.Count;
        audio.OnPointerDown(pointer);
        audio.OnPointerUp(pointer);
        Require(manager.Registry.Count == count + 2, "Paused menu feedback must play.");

        var sliderRoot = new GameObject("UI audio slider", typeof(RectTransform));
        sliderRoot.transform.SetParent(parent);
        sliderRoot.SetActive(false);
        var slider = sliderRoot.AddComponent<UnityEngine.UI.Slider>();
        var sliderAudio = sliderRoot.AddComponent<SelectableAudio>();
        sliderRoot.SetActive(true);
        count = manager.Registry.Count;
        slider.SetValueWithoutNotify(0.2f);
        Require(manager.Registry.Count == count, "Loading volume values must be silent.");
        slider.value = 0.4f;
        slider.value = 0.6f;
        Require(manager.Registry.Count == count + 1, "Slider changes must be throttled while paused.");
        sliderAudio.enabled = false;
        slider.value = 0.8f;
        Require(manager.Registry.Count == count + 1, "Disabled component must unsubscribe.");
        sliderAudio.enabled = true;
        slider.value = 0.3f;
        Require(manager.Registry.Count == count + 2, "Re-enable must restore one subscription.");

        var instance = typeof(CoreFacade).GetProperty("Instance");
        object core = instance.GetValue(null);
        try
        {
            instance.SetValue(null, null);
            audio.OnPointerDown(pointer);
            audio.OnPointerUp(pointer);
        }
        finally
        {
            instance.SetValue(null, core);
        }
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
