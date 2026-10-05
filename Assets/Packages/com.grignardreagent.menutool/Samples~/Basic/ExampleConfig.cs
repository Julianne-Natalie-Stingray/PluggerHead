using UnityEngine;

namespace MenuToolSample
{
    [CreateAssetMenu(
        fileName = MenuTool.Game.Configs.FileName,
        menuName = MenuTool.Game.Configs.Path)]
    public sealed class ExampleConfig : ScriptableObject
    {
        [SerializeField] private string exampleValue = "Hello Menu Tool";
    }
}
