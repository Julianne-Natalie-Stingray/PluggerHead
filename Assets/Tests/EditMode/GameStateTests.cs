using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class GameStateTests
    {
        private readonly Dictionary<FieldInfo, object> savedFields = new Dictionary<FieldInfo, object>();
        private HashSet<object> owners;
        private object[] savedOwners;
        private float savedTimeScale;
        private bool savedPause;

        [SetUp]
        public void IsolateState()
        {
            Type manager = IntegrationCheckBridge.FindType("GameStateManager");
            foreach (FieldInfo field in manager.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
            {
                if (!field.IsInitOnly) savedFields[field] = field.GetValue(null);
            }
            owners = (HashSet<object>)manager.GetField("freezeOwners", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            savedOwners = new object[owners.Count];
            owners.CopyTo(savedOwners);
            owners.Clear();
            savedTimeScale = Time.timeScale;
            savedPause = AudioListener.pause;
            manager.GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            manager.GetField("manualFreeze", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
            manager.GetField("restoreTimeAfterLoading", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
            FieldInfo current = manager.GetField("<Current>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            current.SetValue(null, Enum.Parse(current.FieldType, "Playing"));
            Time.timeScale = 0.5f;
            AudioListener.pause = false;
        }

        [TearDown]
        public void RestoreState()
        {
            owners.Clear();
            owners.UnionWith(savedOwners);
            foreach (var entry in savedFields) entry.Key.SetValue(null, entry.Value);
            savedFields.Clear();
            Time.timeScale = savedTimeScale;
            AudioListener.pause = savedPause;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OverlappingScreens_StayFrozenUntilLastRequestIsReleased(bool reverseOrder)
        {
            object first = new object();
            object second = new object();
            Call("RequestFreeze", first);
            Call("RequestFreeze", first);
            Call("RequestFreeze", second);
            Call("Resume");
            Call("ReleaseFreeze", reverseOrder ? second : first);
            AssertState("Freezed", 0f);
            Call("ReleaseFreeze", reverseOrder ? first : second);
            Call("ReleaseFreeze", first);
            AssertState("Playing", 0.5f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ScreenRelease_PreservesManualFreeze(bool freezeAfterScreen)
        {
            object owner = new object();
            if (!freezeAfterScreen) Call("Freeze");
            Call("RequestFreeze", owner);
            if (freezeAfterScreen) Call("Freeze");
            Call("ReleaseFreeze", owner);
            AssertState("Freezed", 0f);
            Call("Resume");
            AssertState("Playing", 0.5f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Loading_ReconcilesRemovedAndNewScreenRequests(bool newScreenVisible)
        {
            object oldScreen = new object();
            object newScreen = new object();
            Call("RequestFreeze", oldScreen);
            Call("EnterLoading");
            Call("ReleaseFreeze", oldScreen);
            if (newScreenVisible) Call("RequestFreeze", newScreen);
            Assert.That(Current, Is.EqualTo("Loading"));
            Assert.That(Time.timeScale, Is.Zero);
            Call("ExitLoading");
            AssertState(newScreenVisible ? "Freezed" : "Playing", newScreenVisible ? 0f : 0.5f);
            Call("ReleaseFreeze", newScreen);
            AssertState("Playing", 0.5f);
        }

        [Test]
        public void Loading_NewScreenRequestIsAppliedAfterLoading()
        {
            object owner = new object();
            Call("EnterLoading");
            Call("RequestFreeze", owner);
            Assert.That(Current, Is.EqualTo("Loading"));
            Call("ExitLoading");
            AssertState("Freezed", 0f);
            Call("ReleaseFreeze", owner);
            AssertState("Playing", 0.5f);
        }

        private static void Call(string method, params object[] arguments)
        {
            IntegrationCheckBridge.Invoke("GameStateManager", method, arguments);
        }

        private static string Current => IntegrationCheckBridge.FindType("GameStateManager")
            .GetProperty("Current").GetValue(null).ToString();

        private static void AssertState(string expected, float timeScale)
        {
            Assert.That(Current, Is.EqualTo(expected));
            Assert.That(Time.timeScale, Is.EqualTo(timeScale));
            Assert.That(AudioListener.pause, Is.EqualTo(expected == "Freezed"));
        }

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
