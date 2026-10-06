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
        public void Circuit_RequiresEverySocketWithoutReusingEndpoints(string scenario, bool neutralFirst)
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

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Circuit_ValidatesNeededVoltage(float neededVoltage)
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckNeededVoltage", neededVoltage);
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Socket_RejectsSelfLoopsAndRepeatedEndpointUse(bool powerSocket, bool ground)
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckSingleUse", powerSocket, ground);
        }

        [Test]
        public void GroundWire_CanRouteAndConnectAfterPoweredWireIsReleased()
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckGroundWithoutPoweredWire");
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
