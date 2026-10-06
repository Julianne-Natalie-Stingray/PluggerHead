using NUnit.Framework;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class LevelProgressTests
    {
        [Test]
        public void Progress_PersistsOnlyLevelAndHandlesMissingCorruptAndFailedSaves()
        {
            IntegrationCheckBridge.Invoke("MainMenuIntegrationChecks", "CheckProgressStorage");
        }
    }
}
