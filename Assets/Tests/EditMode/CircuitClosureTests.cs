using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    public sealed class CircuitClosureTests
    {
        [TestCase("Complete", false)]
        [TestCase("Complete", true)]
        [TestCase("MissingInterface", false)]
        [TestCase("WalkOnly", false)]
        [TestCase("NoReturnRestriction", false)]
        [TestCase("NoReturnRestriction", true)]
        public void Circuit_RequiresEverySocketAndKeepsPoweredWire(string scenario, bool neutralFirst)
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckCircuit", scenario, neutralFirst);
        }

        [TestCase("EqualVoltage")]
        [TestCase("VoltageTooHigh")]
        [TestCase("MissingGround")]
        [TestCase("DestroyedWire")]
        public void Circuit_GroundAndVoltageFollowSuccessRules(string scenario)
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckGroundAndVoltage", scenario);
        }

        [TestCase("Dual")]
        [TestCase("Reducer")]
        public void RemoteInteraction_KeepsBothWirePathsAdjacent(string kind)
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckRemoteInteraction", kind);
        }

        [Test]
        public void Outlet_HandoverPinsInheritedRoute()
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckOutletPin");
        }

        [Test]
        public void Examples_HaveValidComponentsAndConfiguration()
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckExamples");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void Interface_RejectsInvalidInitialization(int polarity)
        {
            LogAssert.Expect(LogType.Error,
                "PolaritySocket 'InvalidPole' cannot initialize: expected exactly Live | Neutral or Ground.");
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckInvalidInitialization", polarity);
        }
    }
}
