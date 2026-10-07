using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class AudioLimitTests
    {
        [UnityTest]
        public IEnumerator Limits_PerIdCallbackCannotRefillAndAdmitOuterRequest() => Run("PerIdReentry");

        [UnityTest]
        public IEnumerator Limits_GlobalCallbackCannotRefillAndAdmitOuterRequest() => Run("GlobalReentry");

        [UnityTest]
        public IEnumerator Limits_GlobalCallbackMustRecheckRequestedId() => Run("CrossCapReentry");

        [UnityTest]
        public IEnumerator Limits_CallbackReductionUsesCurrentPerIdCap() => Run("CallbackReducesCap");

        [UnityTest]
        public IEnumerator Limits_PerIdReductionRefusesAfterOnePreemption() => Run("PerIdReduction");

        [UnityTest]
        public IEnumerator Limits_GlobalReductionRefusesAfterOnePreemption() => Run("GlobalReduction");

        [UnityTest]
        public IEnumerator Limits_OrdinaryPerIdPreemptsOldest() => Run("OrdinaryPerId");

        [UnityTest]
        public IEnumerator Limits_OrdinaryGlobalPreemptsOldest() => Run("OrdinaryGlobal");

        [UnityTest]
        public IEnumerator Limits_LoopsRemainProtected() => Run("LoopProtection");

        [UnityTest]
        public IEnumerator Limits_NonPositiveCapsRejectWithoutPreemption() => Run("NonPositiveCaps");

        [UnityTest]
        public IEnumerator BackgroundMusic_ReusesReplacesAndRecoversPlayback() => Run("BackgroundMusic");

        [UnityTest]
        public IEnumerator BackgroundMusic_CompletionCallbackCannotStackAnotherTrack() => Run("BackgroundMusicReentry");

        private static IEnumerator Run(string scenario)
        {
            IEnumerator routine = (IEnumerator)IntegrationCheckBridge.Invoke("AudioLimitIntegrationChecks", "Run", scenario);
            try
            {
                while (routine.MoveNext())
                {
                    yield return routine.Current;
                }
            }
            finally
            {
                (routine as IDisposable)?.Dispose();
            }
        }
    }
}
