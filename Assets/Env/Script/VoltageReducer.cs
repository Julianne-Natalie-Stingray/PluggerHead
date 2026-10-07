using System;
using UnityEngine;

/// <summary>A voltage reducer with one electrical anchor at its transform.
/// 降压器：Transform 是电气锚点；每个已连接的降压器只扣减一次电压。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class VoltageReducer : MonoBehaviour, IEnvironmentInteractable
{
    [SerializeField, Min(0f)]
    private float voltageDrop = 10f;

    public float VoltageDrop => voltageDrop;
    public bool IsConfigurationValid => !float.IsNaN(voltageDrop) &&
        !float.IsInfinity(voltageDrop) && voltageDrop >= 0f;
    public bool CanInteract => isActiveAndEnabled && IsConfigurationValid;
    public event Action<IEnvironmentInteractable> OnInteracted;

    public void Interact(InteractionDetails details)
    {
        if (CanInteract)
        {
            OnInteracted?.Invoke(this);
        }
    }
}
