using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class PlayerAnimationAudioPlaybackTests
    {
        [Test]
        public void AnimationEvents_PlayThroughCore_RespectFreeze_AndReleaseVoices()
        {
            IntegrationCheckBridge.Invoke("PlayerAnimationAudioChecks", "Run");
        }
    }
}
