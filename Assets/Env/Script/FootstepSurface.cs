using UnityEngine;

/// <summary>Explicit audio surface classification; unmarked supporting colliders use ground footsteps.</summary>
[DisallowMultipleComponent]
public sealed class FootstepSurface : MonoBehaviour
{
    [SerializeField] private bool metal = true;
    public bool IsMetal => metal;
}
