using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class SceneGameplayTests
    {
        [SetUp]
        public void Setup()
        {
            IntegrationCheckBridge.Invoke("MainMenuIntegrationChecks", "BeginProgressIsolation");
        }

        [UnityTest]
        public IEnumerator AuthoredGameplayScene_PickupRouteSwapCloseAndRestart()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("SceneIntegrationChecks", "CheckGameplay");
        }

        [UnityTest]
        public IEnumerator DiagnosticScene_RealPlayerLandsPlacesAnchorAndSwapsWire()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("SceneIntegrationChecks", "CheckDiagnosticsPlayer");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                yield return (IEnumerator)IntegrationCheckBridge.Invoke("SceneIntegrationChecks", "CleanupGameplay");
            }
            finally
            {
                IntegrationCheckBridge.Invoke("MainMenuIntegrationChecks", "EndProgressIsolation");
            }
        }
    }
}
