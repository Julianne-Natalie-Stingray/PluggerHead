using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class PlayerAnimationAudioPlaybackTests
    {
        [Test]
        public void UIAudio_HandlesPointerSubmitPauseAndSliderLifecycle()
        {
            IntegrationCheckBridge.Invoke("PlayerAnimationAudioChecks", "RunScenario", "UI");
        }

        [Test]
        public void Footsteps_UseActualSupportAndPreserveSequenceOnRejectedEvents()
        {
            IntegrationCheckBridge.Invoke("PlayerAnimationAudioChecks", "RunScenario", "Footsteps");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return (IEnumerator)IntegrationCheckBridge.Invoke("GroundPolarityIntegrationChecks", "Cleanup");
        }
        [Test]
        public void CircuitAudio_UsesSuccessfulTransitionsAndSurvivesVictoryFreeze()
        {
            IntegrationCheckBridge.Invoke("PlayerAnimationAudioChecks", "RunScenario", "Circuit");
        }
        [Test]
        public void AnimationEvents_PlayThroughCore_RespectFreeze_AndReleaseVoices()
        {
            IntegrationCheckBridge.Invoke("PlayerAnimationAudioChecks", "Run");
        }
    }
}
