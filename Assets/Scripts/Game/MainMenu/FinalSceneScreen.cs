using UnityEngine;

/// <summary>Exit action for the standalone congratulations and credits screen.</summary>
[DisallowMultipleComponent]
public sealed class FinalSceneScreen : MonoBehaviour
{
    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
