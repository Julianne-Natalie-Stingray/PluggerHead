using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class AudioHandleTests
    {
        [TestCase(0f, false)]
        [TestCase(0.25f, false)]
        [TestCase(0f, true)]
        [TestCase(0.25f, true)]
        public void DestroyedEmitter_RejectsControlsAndAllowsOwnerCleanup(float fadeOut, bool environmentCleanup)
        {
            IntegrationCheckBridge.Invoke("AudioHandleIntegrationChecks", "CheckDestroyedEmitter", fadeOut, environmentCleanup);
        }

        [UnityTest]
        public IEnumerator Handle_NaturalCompletionIsolatesSubscribersAndRejectsNaN()
        {
            return Check(false);
        }

        [UnityTest]
        public IEnumerator Handle_GracefulStopIsolatesSubscribersAndRejectsNaN()
        {
            return Check(true);
        }

        private static IEnumerator Check(bool gracefulStop)
        {
            LogAssert.Expect(LogType.Exception, new Regex("^InvalidOperationException: Expected AudioHandle subscriber failure\\."));
            IEnumerator routine = (IEnumerator)IntegrationCheckBridge.Invoke("AudioHandleIntegrationChecks", "Run", gracefulStop);
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
