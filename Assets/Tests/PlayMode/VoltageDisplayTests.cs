using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    public sealed class VoltageDisplayTests
    {
        [UnityTest]
        public IEnumerator VoltageDisplay_TracksRealReductionAndRestart()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("VoltageDisplayIntegrationChecks", "CheckReduction");
        }

        [UnityTest]
        public IEnumerator VoltageDisplay_HandlesVisibilityAndSceneIsolation()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("VoltageDisplayIntegrationChecks", "CheckVisibility");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("VoltageDisplayIntegrationChecks", "Cleanup");
        }
    }
}
