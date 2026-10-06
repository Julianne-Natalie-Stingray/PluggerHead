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
        public void StandingOnOppositePolarity_KillsOnce()
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckOppositePolarities");
        }

        [Test]
        public void SafePolaritiesAndNonSupportingContacts_PreservePlayer()
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckSafeContacts");
        }

        [Test]
        public void SwappingWireWhileStandingAndInputLocked_RechecksPolarity()
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckWireSwapWhileStanding");
        }

        [UnityTest]
        public IEnumerator AutomaticPhysicsCallback_OnOppositeGround_KillsLockedPlayerOnce()
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
