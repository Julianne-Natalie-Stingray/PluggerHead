using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class SceneSwitchRecoveryTests
    {
        [UnityTest]
        public IEnumerator Switch_WhenHostDeactivated_CompletesAndReleasesLoading()
        {
            yield return Check("Deactivate");
        }

        [UnityTest]
        public IEnumerator Switch_WhenHostDestroyed_CompletesAndReleasesLoading()
        {
            yield return Check("Destroy");
        }

        [UnityTest]
        public IEnumerator Switch_WhenComponentDisabled_CompletesAndReleasesLoading()
        {
            yield return Check("Disable");
        }

        [UnityTest]
        public IEnumerator Switch_WhenStateSubscribersThrow_CompletesAndReleasesLoading()
        {
            LogAssert.Expect(LogType.Exception,
                new Regex("InvalidOperationException: SceneSwitchRecovery expected Loading"));
            LogAssert.Expect(LogType.Exception,
                new Regex("InvalidOperationException: SceneSwitchRecovery expected Playing"));
            yield return Check("Throw");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("SceneSwitchRecoveryChecks", "Cleanup");
        }

        private static IEnumerator Check(string interruption)
        {
            return (IEnumerator)IntegrationCheckBridge.Invoke("SceneSwitchRecoveryChecks", "CheckRecovery", interruption);
        }
    }
}
