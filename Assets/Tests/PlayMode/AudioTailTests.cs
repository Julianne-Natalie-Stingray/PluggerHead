using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class AudioTailTests
    {
        [UnityTest]
        public IEnumerator NaturalTail_PitchChangesOverlapAndPausedCursor_PreserveEnvelope()
        {
            yield return Run("Values");
        }

        [UnityTest]
        public IEnumerator NaturalTail_HalfSpeed_CompletesAndReusesPoolSlot()
        {
            yield return Run("Slow");
        }

        [UnityTest]
        public IEnumerator NaturalTail_DoubleSpeed_CompletesAndReusesPoolSlot()
        {
            yield return Run("Fast");
        }

        [UnityTest]
        public IEnumerator NaturalTail_LivePitchChange_DoesNotRiseAndCompletes()
        {
            yield return Run("Change");
        }

        [UnityTest]
        public IEnumerator ManualStop_WhileFrozen_CompletesOnUnscaledTime()
        {
            yield return Run("FrozenStop");
        }

        private static IEnumerator Run(string scenario)
        {
            IEnumerator routine = (IEnumerator)IntegrationCheckBridge.Invoke("AudioTailIntegrationChecks", "Run", scenario);
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
