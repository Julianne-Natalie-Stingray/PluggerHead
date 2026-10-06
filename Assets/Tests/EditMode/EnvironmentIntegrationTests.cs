using NUnit.Framework;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class EnvironmentIntegrationTests
    {
        [Test]
        public void PlayerAndEnvironment_WhenRunningCompleteScenario_PreserveInteractionContracts()
        {
            string result = (string)IntegrationCheckBridge.Invoke("EnvironmentIntegrationChecks", "Run");
            Assert.That(result, Does.StartWith("Player/Environment integration PASS:"));
            TestContext.WriteLine(result);
        }
    }
}
