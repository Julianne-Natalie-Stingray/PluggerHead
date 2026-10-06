using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace PluggerHead.Tests
{
    [Category("Integration")]
    public sealed class DebugSettingsTests
    {
        [Test]
        public void SettingsButtons_InEditMode_DoNotAccessUninitializedSettings()
        {
            Type bootstrap = IntegrationCheckBridge.FindType("SettingBootstrap");
            FieldInfo store = bootstrap.GetField("store", BindingFlags.Static | BindingFlags.NonPublic);
            object previous = store.GetValue(null);
            var host = new GameObject("DebugSettingsTest");
            try
            {
                Assert.That(Application.isPlaying, Is.False);
                store.SetValue(null, null);
                Type type = IntegrationCheckBridge.FindType("DebugScript");
                Component component = host.AddComponent(type);
                foreach (string name in new[] { "TestWriteInSettings", "TestResettingSettings" })
                {
                    Assert.DoesNotThrow(() => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(component, null));
                    Assert.That(store.GetValue(null), Is.Null);
                }
            }
            finally
            {
                store.SetValue(null, previous);
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
