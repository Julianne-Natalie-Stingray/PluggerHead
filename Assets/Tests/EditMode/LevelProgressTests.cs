using NUnit.Framework;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class LevelProgressTests
    {
        [Test]
        public void Progress_UnlocksSequentiallyAllowsReplayAndResetsOnNewRun()
        {
            IntegrationCheckBridge.Invoke("MainMenuIntegrationChecks", "CheckProgressStorage");
        }
    }
}
