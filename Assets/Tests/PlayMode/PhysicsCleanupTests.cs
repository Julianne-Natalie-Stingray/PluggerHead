using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class PhysicsCleanupTests
    {
        [UnityTest]
        public IEnumerator Corner_NullUnloadStillRestoresAndCleansOtherScenes() => Run("CornerIntegrationChecks", "Null");

        [UnityTest]
        public IEnumerator Corner_ThrowingUnloadPreservesErrorAndRestores() => Run("CornerIntegrationChecks", "Exception");

        [UnityTest]
        public IEnumerator Corner_TimeoutRetainsOwnershipAndRestores() => Run("CornerIntegrationChecks", "Timeout");

        [UnityTest]
        public IEnumerator Corner_DisposalRetainsOwnershipAndRestores() => Run("CornerIntegrationChecks", "Dispose");

        [UnityTest]
        public IEnumerator Corner_UnloadedSceneRetainsUnconfirmedHandle() => Run("CornerIntegrationChecks", "UnloadedPending");

        [UnityTest]
        public IEnumerator Ground_NullUnloadStillRestoresAndCleansOtherScenes() => Run("GroundPolarityIntegrationChecks", "Null");

        [UnityTest]
        public IEnumerator Ground_ThrowingUnloadPreservesErrorAndRestores() => Run("GroundPolarityIntegrationChecks", "Exception");

        [UnityTest]
        public IEnumerator Ground_TimeoutRetainsOwnershipAndRestores() => Run("GroundPolarityIntegrationChecks", "Timeout");

        [UnityTest]
        public IEnumerator Ground_DisposalRetainsOwnershipAndRestores() => Run("GroundPolarityIntegrationChecks", "Dispose");

        [UnityTest]
        public IEnumerator Ground_UnloadedSceneRetainsUnconfirmedHandle() => Run("GroundPolarityIntegrationChecks", "UnloadedPending");

        [UnityTest]
        public IEnumerator Corner_CompletedHandleCannotHideLoadedScene() => Run("CornerIntegrationChecks", "CompletedLoaded");

        [UnityTest]
        public IEnumerator Ground_CompletedHandleCannotHideLoadedScene() => Run("GroundPolarityIntegrationChecks", "CompletedLoaded");

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("PhysicsCleanupIntegrationChecks", "Cleanup");
        }

        private static IEnumerator Run(string owner, string failure)
        {
            IEnumerator routine = (IEnumerator)IntegrationCheckBridge.Invoke("PhysicsCleanupIntegrationChecks", "Run", owner, failure);
            try
            {
                while (routine.MoveNext())
                {
                    yield return routine.Current;
                }
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
            }
        }
    }
}
