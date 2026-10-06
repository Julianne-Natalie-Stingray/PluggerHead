using NUnit.Framework;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class MenuToolSafetyTests
    {
        [Test]
        public void MenuTool_OnlyVerifiedOwnedFilesCanBeWrittenOrDeleted()
        {
            IntegrationCheckBridge.Invoke("MenuToolSafetyIntegrationChecks", "CheckOwnership");
        }

        [Test]
        public void MenuTool_GenerationAndDeletionShareContainedCSharpPathValidation()
        {
            IntegrationCheckBridge.Invoke("MenuToolSafetyIntegrationChecks", "CheckPaths");
        }

        [Test]
        public void MenuTool_IdentifiersRejectGeneratedMemberConflictsAndWhitespace()
        {
            IntegrationCheckBridge.Invoke("MenuToolSafetyIntegrationChecks", "CheckIdentifiers");
        }

        [Test]
        public void MenuTool_ExistingProjectDefinitionAndGeneratedApiRemainCompatible()
        {
            IntegrationCheckBridge.Invoke("MenuToolSafetyIntegrationChecks", "CheckProjectCompatibility");
        }
    }
}
