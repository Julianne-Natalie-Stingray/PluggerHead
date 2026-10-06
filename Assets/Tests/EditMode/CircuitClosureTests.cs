using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    public sealed class CircuitClosureTests
    {
        [TestCase("Complete", false)]
        [TestCase("Complete", true)]
        [TestCase("IndependentReturns", false)]
        [TestCase("MissingInterface", false)]
        [TestCase("WalkOnly", false)]
        [TestCase("WrongOutlet", false)]
        [TestCase("IndependentTermination", false)]
        [TestCase("DestroyedJunction", false)]
        public void Circuit_RequiresConnectedSourceLoopAndEveryInterface(string scenario, bool neutralFirst)
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckCircuit", scenario, neutralFirst);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Circuit_MultipleWiresRequireOppositeSourcePoles(bool samePolarityReturn)
        {
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckMultipleWires", samePolarityReturn);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        [TestCase(6)]
        public void Interface_RejectsInvalidInitialization(int polarity)
        {
            LogAssert.Expect(LogType.Error,
                "PolaritySocket 'InvalidPole' cannot initialize: powered interfaces must include both Live and Neutral; None is invalid.");
            IntegrationCheckBridge.Invoke("CircuitClosureIntegrationChecks", "CheckInvalidInitialization", polarity);
        }
    }
}
