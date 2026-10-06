using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class TilemapTests
    {
        [Test]
        public void TilePath_TracksLengthAndRetractsByCell()
        {
            IntegrationCheckBridge.Invoke("TilemapIntegrationChecks", "CheckPathAndLength");
        }

        [Test]
        public void FastMovement_TraversesAndRetracesAllQuadrants()
        {
            IntegrationCheckBridge.Invoke("TilemapIntegrationChecks", "CheckFastAndDiagonalRetrace");
        }

        [Test]
        public void SameCellMotion_DoesNotExtendPath_MissingTileRejectsAnchor()
        {
            IntegrationCheckBridge.Invoke("TilemapIntegrationChecks", "CheckSameCellAndMissingTile");
        }

        [Test]
        public void Anchor_PinsEarlierRoute_ReclaimRestoresRetraction()
        {
            IntegrationCheckBridge.Invoke("TilemapIntegrationChecks", "CheckAnchorPinAndRelease");
        }

        [Test]
        public void WireSwap_PreservesIndependentPaths_RestartResetsPins()
        {
            IntegrationCheckBridge.Invoke("TilemapIntegrationChecks", "CheckWireSwapAndRestart");
        }

        [Test]
        public void ColliderGeometry_MatchesWorldPath_AndDoesNotExtendInteractionRange()
        {
            IntegrationCheckBridge.Invoke("TilemapIntegrationChecks", "CheckTransformedGeometryAndInteractionRange");
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void WireRendering_PreservesInheritedColor_AndNewSegmentsCoverOlderOnes(bool neutralFirst, bool returnAtOutlet)
        {
            IntegrationCheckBridge.Invoke("TilemapIntegrationChecks", "CheckWireRendering", neutralFirst, returnAtOutlet);
        }

        [UnityTest]
        public IEnumerator AutomaticLateUpdate_RecordsTilesAndHonorsAnchorLifetime()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("TilemapIntegrationChecks", "CheckAutomaticLateUpdate");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("TilemapIntegrationChecks", "Cleanup");
        }
    }
}
