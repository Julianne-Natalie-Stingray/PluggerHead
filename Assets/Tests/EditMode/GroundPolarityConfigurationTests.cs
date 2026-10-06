using NUnit.Framework;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class GroundPolarityConfigurationTests
    {
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(0, false)]
        [TestCase(4, false)]
        [TestCase(3, false)]
        [TestCase(5, false)]
        [TestCase(6, false)]
        [TestCase(7, false)]
        [TestCase(8, false)]
        [TestCase(-1, false)]
        public void GroundPolarity_RejectsInvalidConfiguration(int polarity, bool valid)
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckConfiguration", polarity, valid);
        }

        [Test]
        public void Inspector_OffersOnlyLiveAndNeutral()
        {
            IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "CheckInspectorOptions");
        }
    }
}
