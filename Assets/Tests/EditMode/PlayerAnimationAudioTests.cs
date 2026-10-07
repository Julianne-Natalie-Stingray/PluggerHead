using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PluggerHead.Tests
{
    public sealed class PlayerAnimationAudioTests
    {
        [TestCase("Death")]
        [TestCase("Move")]
        [TestCase("Interact")]
        public void AnimationEvent_HasReceiverOnAnimatorObject(string action)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
            Animator animator = prefab.GetComponentInChildren<Animator>(true);
            Type receiverType = IntegrationCheckBridge.FindType("PlayerAnimationAudio");
            Assert.That(animator.GetComponent(receiverType), Is.Not.Null);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Visual/Player/Player{action}Anim.anim");
            Assert.That(animator.runtimeAnimatorController.animationClips, Does.Contain(clip));
            string callback = $"Play{action}Audio";
            Assert.That(AnimationUtility.GetAnimationEvents(clip).Any(item => item.functionName == callback), Is.True);
            var method = receiverType.GetMethod(callback, Type.EmptyTypes);
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
        }

        [Test]
        public void UnconfiguredEvents_AndDisabledReceiver_AreSafe()
        {
            var host = new GameObject("Animation audio test");
            try
            {
                Type type = IntegrationCheckBridge.FindType("PlayerAnimationAudio");
                var receiver = (Behaviour)host.AddComponent(type);
                foreach (bool enabled in new[] { true, false })
                {
                    receiver.enabled = enabled;
                    foreach (string action in new[] { "Death", "Move", "Interact" })
                    {
                        Assert.DoesNotThrow(() => type.GetMethod($"Play{action}Audio").Invoke(receiver, null));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
