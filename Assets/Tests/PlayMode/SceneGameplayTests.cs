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
        [UnityTest]
        public IEnumerator AuthoredGameplayScene_PickupRouteSwapCloseAndRestart()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("SceneIntegrationChecks", "CheckGameplay");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("SceneIntegrationChecks", "CleanupGameplay");
        }
    }
}
