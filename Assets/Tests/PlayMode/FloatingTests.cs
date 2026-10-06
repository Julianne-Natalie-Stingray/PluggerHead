using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class FloatingTests
    {
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("FloatingIntegrationChecks", "Cleanup");
        }

        [UnityTest]
        public IEnumerator Floating_RootRotationPreservesPositionAndPauses()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("FloatingIntegrationChecks", "CheckMotion", false);
        }

        [UnityTest]
        public IEnumerator Floating_TransformedParentRotationPreservesPositionAndPauses()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("FloatingIntegrationChecks", "CheckMotion", true);
        }
    }
}
