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
        public IEnumerator Tilemap_NullUnloadStillRestoresAndCleansOtherScenes() => Run("TilemapIntegrationChecks", "Null");

        [UnityTest]
        public IEnumerator Tilemap_ThrowingUnloadPreservesErrorAndRestores() => Run("TilemapIntegrationChecks", "Exception");

        [UnityTest]
        public IEnumerator Tilemap_TimeoutRetainsOwnershipAndRestores() => Run("TilemapIntegrationChecks", "Timeout");

        [UnityTest]
        public IEnumerator Tilemap_DisposalRetainsOwnershipAndRestores() => Run("TilemapIntegrationChecks", "Dispose");

        [UnityTest]
        public IEnumerator Tilemap_UnloadedSceneRetainsUnconfirmedHandle() => Run("TilemapIntegrationChecks", "UnloadedPending");

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
        public IEnumerator Tilemap_CompletedHandleCannotHideLoadedScene() => Run("TilemapIntegrationChecks", "CompletedLoaded");

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
