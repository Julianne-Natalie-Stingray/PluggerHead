using NUnit.Framework;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class SceneAssetTests
    {
        [TestCase("Assets/Tests/Scenes/SceneSwitchTarget.unity")]
        [TestCase("Assets/Tests/Scenes/GameplayIntegration.unity")]
        [TestCase("Assets/Tests/Scenes/CircuitDiagnostics.unity")]
        [TestCase("Assets/Scenes/MainMenuScene.unity")]
        [TestCase("Assets/Scenes/FinalScene.unity")]
        public void BuildScene_HasValidScriptsPrefabsAndWireMaterials(string scenePath)
        {
            IntegrationCheckBridge.Invoke("SceneIntegrationChecks", "CheckSceneAsset", scenePath);
        }

        [Test]
        public void SceneRegistry_MapsEveryFunctionalSceneToAnEnabledBuildScene()
        {
            IntegrationCheckBridge.Invoke("SceneIntegrationChecks", "CheckSceneRegistry");
        }
    }
}
