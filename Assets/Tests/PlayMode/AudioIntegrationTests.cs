using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    [UnityPlatform(RuntimePlatform.WindowsEditor, RuntimePlatform.LinuxEditor, RuntimePlatform.OSXEditor)]
    public sealed class AudioIntegrationTests
    {
        [UnityTest]
        public IEnumerator Audio_WhenStoppedReusedAndCompleted_PreservesPlaybackLifecycle()
        {
            IEnumerator routine = (IEnumerator)IntegrationCheckBridge.Invoke(
                "AudioIntegrationChecks", "RunForTests");
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
