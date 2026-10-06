using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class GroundPolarityTests
    {
        [Test]
        public void StandingWithoutMatchingPolarity_KillsOnce()
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckNonMatchingPolarities");
        }

        [Test]
        public void SafePolaritiesAndNonSupportingContacts_PreservePlayer()
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckSafeContacts");
        }

        [Test]
        public void MissingOrReleasedWire_DoesNotProtectPlayer()
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckMissingOrReleasedWire");
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void GroundTerrain_RequiresCarriedGroundWire(bool keepPoweredWire, bool releaseGroundWire)
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckCarriedGroundWire", keepPoweredWire, releaseGroundWire);
        }

        [Test]
        public void ReleasingMatchingWireWhileStanding_KillsLockedPlayer()
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckReleaseWhileStanding");
        }

        [Test]
        public void SwappingWireWhileStandingAndInputLocked_RechecksPolarity()
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckWireSwapWhileStanding");
        }

        [UnityTest]
        public IEnumerator AutomaticPhysicsCallback_WithoutHeldWire_KillsLockedPlayerOnce()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckAutomaticPhysicsCallback");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "Cleanup");
        }
    }
}
