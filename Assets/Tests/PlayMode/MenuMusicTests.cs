using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class MenuMusicTests
    {
        [UnityTest]
        public IEnumerator RegisteredMenuMusic_UsesAudioApiAndResumesWithoutRestart()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("MenuMusicIntegrationChecks", "CheckRegisteredMusic");
        }
    }
}
