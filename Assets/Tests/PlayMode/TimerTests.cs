using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class TimerTests
    {
        [UnityTest]
        public IEnumerator AtZeroStop_PreservesProgressAndResumes()
        {
            yield return Run("TimestampStop");
        }

        [UnityTest]
        public IEnumerator AtZeroRestart_DoesNotOverwriteNewRun()
        {
            yield return Run("TimestampRestart");
        }

        [UnityTest]
        public IEnumerator ConditionStop_AndInfinityResume_PreserveGeneration()
        {
            yield return Run("ConditionStop");
        }

        [UnityTest]
        public IEnumerator ConditionRestart_DiscardsOldTrueResult()
        {
            yield return Run("ConditionRestart");
        }

        [UnityTest]
        public IEnumerator ZeroDurationCompletionRestart_SkipsOldSubscribers()
        {
            yield return Run("CompletionRestart");
        }

        [UnityTest]
        public IEnumerator LaterFrameStopRestart_StopsOldDispatch()
        {
            yield return Run("LaterFrame");
        }

        [UnityTest]
        public IEnumerator TimestampException_StopsAndCanResume()
        {
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: TimerFault timestamp"));
            yield return Run("TimestampException");
        }

        [UnityTest]
        public IEnumerator ConditionException_StopsAndCanResume()
        {
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: TimerFault condition"));
            yield return Run("ConditionException");
        }

        [UnityTest]
        public IEnumerator CompletionException_RetainsCompletedState()
        {
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: TimerFault completion"));
            yield return Run("CompletionException");
        }

        [UnityTest]
        public IEnumerator OldCallbackException_PreservesRestartedRun()
        {
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: TimerFault after restart"));
            yield return Run("RestartException");
        }

        [UnityTest]
        public IEnumerator ZeroDurationException_ClearsStateAndCanResume()
        {
            yield return Run("ZeroException");
        }

        [UnityTest]
        public IEnumerator Stop_AfterGlobalRunnerChanges_UsesOriginalHost()
        {
            yield return Run("OriginalHost");
        }

        private static IEnumerator Run(string scenario)
        {
            return (IEnumerator)IntegrationCheckBridge.Invoke("TimerIntegrationChecks", "Run", scenario);
        }
    }
}
