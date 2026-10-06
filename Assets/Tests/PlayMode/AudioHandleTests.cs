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
