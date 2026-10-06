using NUnit.Framework;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class SceneAssetTests
    {
        [TestCase("Assets/Scenes/Tests/JillTestScene.unity")]
        [TestCase("Assets/Scenes/Tests/JillTestSceneSwitch.unity")]
        [TestCase("Assets/Scenes/Tests/HeXieTestScene.unity")]
        [TestCase("Assets/Scenes/Tests/JillTestWireScene.unity")]
        public void BuildScene_HasValidScriptsPrefabsAndWireMaterials(string scenePath)
        {
            IntegrationCheckBridge.Invoke("SceneIntegrationChecks", "CheckSceneAsset", scenePath);
        }
    }
}
