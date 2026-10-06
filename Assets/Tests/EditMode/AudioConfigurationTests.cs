using NUnit.Framework;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class AudioConfigurationTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        [TestCase(1)]
        [TestCase(30)]
        public void MaxPoolSize_ProtectsRuntimePoolConstructionAndEditorValidation(int serializedValue)
        {
            IntegrationCheckBridge.Invoke("AudioConfigurationIntegrationChecks", "CheckMaxPoolSize", serializedValue);
        }
    }
}
