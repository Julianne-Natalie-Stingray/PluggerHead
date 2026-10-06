using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class MainMenuTests
    {
        [SetUp]
        public void Setup()
        {
            IntegrationCheckBridge.Invoke("MainMenuIntegrationChecks", "BeginProgressIsolation");
        }

        [UnityTest]
        public IEnumerator MainMenu_NewGameSettingsReturnAndContinueFromDefaultState()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("MainMenuIntegrationChecks", "CheckMenuFlow");
        }

        [UnityTest]
        public IEnumerator LevelMenu_RestartRetriesReloadsOwnSceneAndPreservesDeathRestart()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("MainMenuIntegrationChecks", "CheckMenuRestart");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                yield return (IEnumerator)IntegrationCheckBridge.Invoke("MainMenuIntegrationChecks", "CleanupMenuFlow");
            }
            finally
            {
                IntegrationCheckBridge.Invoke("MainMenuIntegrationChecks", "EndProgressIsolation");
            }
        }
    }
}
