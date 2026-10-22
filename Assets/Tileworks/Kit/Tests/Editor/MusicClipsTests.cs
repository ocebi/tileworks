using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Tileworks.Kit.Audio;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tileworks.Kit.Tests
{
    public sealed class MusicClipsTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        [Test]
        public void TheConfig_ServesItsOwnClips_AtOnce()
        {
            var home = Clip();
            var config = Config(home);

            var task = ((IMusicClips)config).GetAsync(MusicTrack.Home, CancellationToken.None);

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(task.GetAwaiter().GetResult(), Is.SameAs(home));
            Assert.That(((IMusicClips)config).GetAsync(MusicTrack.Fail, CancellationToken.None).GetAwaiter().GetResult(), Is.Null);
        }

        [Test]
        public void PlayLoop_WaitsForTheClip_AndTheNewestRequestWins()
        {
            var player = Player(Config(Clip()));
            var clips = new SlowClips();
            player.Bind(clips);

            player.PlayLoop(MusicTrack.Game1);
            player.PlayLoop(MusicTrack.Game2);
            Assert.That(Clips(player), Is.Empty, "a loop started before its clip arrived");

            var late = Clip();
            clips.Pending[MusicTrack.Game1].TrySetResult(late);
            Assert.That(Clips(player), Is.Empty, "a superseded request still played");

            var wanted = Clip();
            clips.Pending[MusicTrack.Game2].TrySetResult(wanted);
            Assert.That(Clips(player), Is.EqualTo(new[] { wanted }));
        }

        private static List<AudioClip> Clips(Component player)
        {
            var clips = new List<AudioClip>();
            foreach (var source in player.GetComponents<AudioSource>())
            {
                if (source.clip != null) clips.Add(source.clip);
            }

            return clips;
        }

        private MusicPlayer Player(MusicConfig config)
        {
            var go = new GameObject("Music");
            _created.Add(go);
            var player = go.AddComponent<MusicPlayer>();
            typeof(MusicPlayer).GetField("_config", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, config);
            return player;
        }

        private MusicConfig Config(AudioClip home)
        {
            var config = ScriptableObject.CreateInstance<MusicConfig>();
            _created.Add(config);
            config.Configure(home, null, null, null, null, null);
            return config;
        }

        private AudioClip Clip()
        {
            var clip = AudioClip.Create("clip", 441, 1, 44100, false);
            _created.Add(clip);
            return clip;
        }

        private sealed class SlowClips : IMusicClips
        {
            public readonly Dictionary<MusicTrack, UniTaskCompletionSource<AudioClip>> Pending = new Dictionary<MusicTrack, UniTaskCompletionSource<AudioClip>>();

            public UniTask<AudioClip> GetAsync(MusicTrack track, CancellationToken ct)
            {
                var source = new UniTaskCompletionSource<AudioClip>();
                Pending[track] = source;
                return source.Task;
            }
        }
    }
}
