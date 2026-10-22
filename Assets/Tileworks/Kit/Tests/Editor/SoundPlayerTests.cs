using NUnit.Framework;
using Tileworks.Kit.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Tileworks.Kit.Tests
{
    public sealed class SoundPlayerTests
    {
        private GameObject _host;
        private TestSoundBank _bank;
        private AudioClip _silence;
        private SoundPlayer _player;

        [SetUp]
        public void SetUp()
        {
            _silence = AudioClip.Create("silence", 4410, 1, 44100, false);
            _bank = ScriptableObject.CreateInstance<TestSoundBank>();
            _bank.Configure(new[]
            {
                new SoundCue("Pop", 0, new[] { _silence, _silence }, 0f).WithVoices(4),
                new SoundCue("Boom", 3, new[] { _silence }, 0f).WithPriority(5),
            });
            (_host, _player) = Player();
        }

        private (GameObject, SoundPlayer) Player()
        {
            var host = new GameObject("Sfx") { hideFlags = HideFlags.HideAndDontSave };
            var player = host.AddComponent<SoundPlayer>();
            var wiring = new SerializedObject(player);
            wiring.FindProperty("_sounds").objectReferenceValue = _bank;
            wiring.ApplyModifiedPropertiesWithoutUndo();
            return (host, player);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_bank);
            Object.DestroyImmediate(_silence);
        }

        [Test]
        public void ABurst_NeverUsesMoreThan24Sources()
        {
            for (var i = 0; i < 60; i++) _player.Play(i % 2 == 0 ? 0 : 3, i * 0.05f);

            Assert.That(_host.GetComponents<AudioSource>().Length, Is.InRange(1, 24));
        }

        [Test]
        public void UnknownCues_AreIgnored()
        {
            _player.Play(1);
            _player.Play(-1);
            _player.PlayStep(99, 3);

            Assert.That(_host.GetComponents<AudioSource>(), Is.Empty);
        }

        [Test]
        public void Playing_DoesNotAllocate_AfterWarmUp()
        {
            // A whole run on a throwaway player first, so lazy statics are set up whatever ran before.
            var (spare, warm) = Player();
            WarmUp(warm);
            Burst(warm);
            Object.DestroyImmediate(spare);

            WarmUp(_player);
            Assert.That(() => Burst(_player), Is.Not.AllocatingGCMemory());
        }

        private static void WarmUp(SoundPlayer player)
        {
            for (var i = 0; i < 24; i++) player.Play(i % 2 == 0 ? 0 : 3, i * 0.05f);
        }

        private static void Burst(SoundPlayer player)
        {
            for (var i = 0; i < 200; i++)
            {
                player.Play(0, (i % 7) * 0.05f);
                player.PlayStep(0, i % 14, (i % 5) * 0.05f);
            }
        }
    }
}
