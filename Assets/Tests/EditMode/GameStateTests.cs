using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class GameStateTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Loading_WhenPauseRequestsInterleave_PreservesOriginalStateAndTimeScale(bool initiallyFrozen)
        {
            Type manager = IntegrationCheckBridge.FindType("GameStateManager");
            FieldInfo current = manager.GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo scale = manager.GetField("timeScaleBeforeFreeze", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo loading = manager.GetField("stateBeforeLoading", BindingFlags.Static | BindingFlags.NonPublic);
            FieldInfo changed = manager.GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic);
            object previousCurrent = current.GetValue(null);
            object previousScale = scale.GetValue(null);
            object previousLoading = loading.GetValue(null);
            object previousChanged = changed.GetValue(null);
            float previousTimeScale = Time.timeScale;
            bool previousPause = AudioListener.pause;
            try
            {
                // Isolate scene subscribers and restore all static state even on assertion failure.
                changed.SetValue(null, null);
                current.SetValue(null, Enum.Parse(current.FieldType, "Playing"));
                Time.timeScale = 0.5f;
                AudioListener.pause = false;
                if (initiallyFrozen)
                {
                    IntegrationCheckBridge.Invoke("GameStateManager", "Freeze");
                }

                IntegrationCheckBridge.Invoke("GameStateManager", "EnterLoading");
                IntegrationCheckBridge.Invoke("GameStateManager", "EnterLoading");
                foreach (string method in new[] { "Resume", "Freeze", "Freeze", "Resume" })
                {
                    IntegrationCheckBridge.Invoke("GameStateManager", method);
                    Assert.That(current.GetValue(null).ToString(), Is.EqualTo("Loading"));
                    Assert.That(Time.timeScale, Is.EqualTo(initiallyFrozen ? 0f : 0.5f));
                    Assert.That(AudioListener.pause, Is.EqualTo(initiallyFrozen));
                }

                IntegrationCheckBridge.Invoke("GameStateManager", "ExitLoading");
                Assert.That(current.GetValue(null).ToString(), Is.EqualTo(initiallyFrozen ? "Freezed" : "Playing"));
                IntegrationCheckBridge.Invoke("GameStateManager", "Resume");
                Assert.That(Time.timeScale, Is.EqualTo(0.5f));
                Assert.That(AudioListener.pause, Is.False);
            }
            finally
            {
                current.SetValue(null, previousCurrent);
                scale.SetValue(null, previousScale);
                loading.SetValue(null, previousLoading);
                changed.SetValue(null, previousChanged);
                Time.timeScale = previousTimeScale;
                AudioListener.pause = previousPause;
            }
        }
    }
}
