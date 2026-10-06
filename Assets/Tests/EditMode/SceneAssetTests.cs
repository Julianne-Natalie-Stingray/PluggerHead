using NUnit.Framework;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class SceneAssetTests
    {
        [TestCase("Assets/Scenes/Tests/SceneSwitchTarget.unity")]
        [TestCase("Assets/Scenes/Tests/GameplayIntegration.unity")]
        [TestCase("Assets/Scenes/Tests/CircuitDiagnostics.unity")]
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
