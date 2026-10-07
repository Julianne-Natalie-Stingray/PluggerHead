using NUnit.Framework;

namespace PluggerHead.Tests
{
    public sealed class AudioVariantTests
    {
        [Test]
        public void Selection_UsesPrimaryAndNonNullVariants()
        {
            IntegrationCheckBridge.Invoke("SelectableAudioChecks", "CheckVariants");
        }
    }
}
