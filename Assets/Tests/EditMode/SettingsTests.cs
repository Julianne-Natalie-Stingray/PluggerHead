using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class SettingsTests
    {
        [Test]
        public void AudioSettings_RejectNaNClampOtherInputsAndKeepIndependentDefaults()
        {
            IntegrationCheckBridge.Invoke("SettingsIntegrationChecks", "CheckAudioSetters");
        }

        [Test]
        public void Settings_ValidateLoadedAudioAndPreservePreviousFileOnSaveFailure()
        {
            LogAssert.Expect(LogType.Error, new Regex("Settings were not saved"));
            IntegrationCheckBridge.Invoke("SettingsIntegrationChecks", "CheckStorage");
        }
    }
}
