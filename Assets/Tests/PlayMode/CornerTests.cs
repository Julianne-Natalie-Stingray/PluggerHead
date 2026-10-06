using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class CornerTests
    {
        [Test]
        public void MovingTail_EntersWrapsAndReverses_WithoutEndpointOverlap()
        {
            IntegrationCheckBridge.Invoke("CornerIntegrationChecks", "CheckEntryAndReverse");
        }

        [Test]
        public void FastSweep_OrdersMultipleCornersAndUnwindsInReverse()
        {
            IntegrationCheckBridge.Invoke("CornerIntegrationChecks", "CheckSweptEntryAndMultipleCorners");
        }

        [Test]
        public void StationaryAndOutwardMotion_DoNotHook_DisabledColliderReleases()
        {
            IntegrationCheckBridge.Invoke("CornerIntegrationChecks", "CheckStationaryAndMovingAway");
        }

        [Test]
        public void SubToleranceMovements_AccumulateForColliderAndCornerRouting()
        {
            IntegrationCheckBridge.Invoke("CornerIntegrationChecks", "CheckAccumulatedSmallMovements");
        }

        [Test]
        public void WireSwap_PreservesIndependentAnchors_RestartAndDisableCleanUp()
        {
            IntegrationCheckBridge.Invoke("CornerIntegrationChecks", "CheckWireSwapAndCleanup");
        }

        [Test]
        public void ColliderGeometry_MatchesWorldPath_AndDoesNotExtendInteractionRange()
        {
            IntegrationCheckBridge.Invoke("CornerIntegrationChecks", "CheckColliderCoordinatesAndInteractionRange");
        }

        [UnityTest]
        public IEnumerator AutomaticLateUpdate_UsesCornerPrefabAndDestroysReleasedAnchor()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("CornerIntegrationChecks", "CheckAutomaticLateUpdateAndDestroy");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("CornerIntegrationChecks", "Cleanup");
        }
    }
}
