using System.Collections;
using UnityEngine;

/// <summary>
/// Coroutine host used internally by Timer.
/// Expected to live on the persistent GameCore GameObject.
/// </summary>
[DisallowMultipleComponent]
public sealed class TimerRunner : MonoBehaviour
{
    public static TimerRunner Instance { get; private set; }

    private void Awake()
    {
        if (Instance && Instance != this)
        {
            GameLog.Warning(this)
                .Subsystem(nameof(Timer))
                .Name(LogName.ClassAndGameObject)
                .Issue(LogIssue.Specify("Another TimerRunner already exists. "))
                .Action(LogAction.DisableComponent)
                .Write();

            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    internal Coroutine StartTimer(IEnumerator coro)
        => StartCoroutine(coro);

    internal void StopTimer(Coroutine coro)
        => StopCoroutine(coro);
}
