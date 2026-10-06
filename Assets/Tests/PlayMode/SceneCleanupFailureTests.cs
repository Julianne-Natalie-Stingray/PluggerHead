using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class SceneCleanupFailureTests
    {
        [TestCase("Timeout")]
        [TestCase("MoveNext")]
        [TestCase("Dispose")]
        [TestCase("Cancel")]
        [TestCase("Aggregate")]
        public void NestedIteratorFailures_AlwaysUnwindAndKeepEvidence(string kind)
        {
            IntegrationCheckBridge.Invoke("SceneCleanupFailureChecks", "CheckIteratorFailure", kind);
        }

        [UnityTest]
        public IEnumerator FloatingFailure_RestoresMotionStateAndRetriesUnload()
        {
            yield return Run("Floating");
        }

        [UnityTest]
        public IEnumerator GameplayUnloadFailure_RestoresCoreAndGlobals()
        {
            yield return Run("Gameplay");
        }

        [UnityTest]
        public IEnumerator MenuUnloadFailure_RestoresCoreAndGlobals()
        {
            yield return Run("Menu");
        }

        [UnityTest]
        public IEnumerator RecoveryUnloadFailure_RestoresHostsAndGlobals()
        {
            yield return Run("Recovery");
        }

        [UnityTest]
        public IEnumerator PendingLoad_KeepsPersonalProgressIsolatedUntilCleanupRetry()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("SceneCleanupFailureChecks", "CheckPendingLoadIsolation");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("SceneCleanupFailureChecks", "Cleanup");
        }

        private static IEnumerator Run(string kind)
        {
            return (IEnumerator)IntegrationCheckBridge.Invoke("SceneCleanupFailureChecks", "CheckFixtureFailure", kind);
        }
    }
}
