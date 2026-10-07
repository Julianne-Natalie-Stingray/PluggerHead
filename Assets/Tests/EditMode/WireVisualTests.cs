using NUnit.Framework;

namespace PluggerHead.Tests
{
    public sealed class WireVisualTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void Initialize_SelectsPolarityMaterial(int polarity)
        {
            IntegrationCheckBridge.Invoke("WireVisualIntegrationChecks", "CheckSelection", polarity);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(4)]
        public void Continuation_SelectsNewTypeWithoutChangingSource(int polarity)
        {
            IntegrationCheckBridge.Invoke("WireVisualIntegrationChecks", "CheckContinuation", polarity);
        }

        [Test]
        public void MissingConfiguration_PreservesAuthoredMaterial_EmptySlotClearsStaleMaterial()
        {
            IntegrationCheckBridge.Invoke("WireVisualIntegrationChecks", "CheckMissingConfiguration");
        }

        [Test]
        public void SuffixRendering_AndHandoverOrder_PreserveMaterial()
        {
            IntegrationCheckBridge.Invoke("WireVisualIntegrationChecks", "CheckPathAndOrder");
        }

        [TestCase("PowerSocket", 1)]
        [TestCase("PowerSocket", 2)]
        [TestCase("DualSocket", 1)]
        [TestCase("DualSocket", 2)]
        public void SocketConnections_ReachAnchorsAndRenderAboveSocket(string socketName, int polarity)
        {
            IntegrationCheckBridge.Invoke("WireVisualIntegrationChecks", "CheckSocketConnections", socketName, polarity);
        }

        [Test]
        public void ProductionAssets_HaveCompletePolarityMaterials()
        {
            IntegrationCheckBridge.Invoke("WireVisualIntegrationChecks", "CheckAssetReferences");
        }
    }
}
