using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tileworks.Kit.Audio
{
    // Lives in the app scene, so a track keeps playing across scene loads, and is carried across a game switch
    // (CarryOver, then handed to the next scene's IMusicHost). Ignores the listener volume, so the Sound setting mutes effects only; the
    // Music setting is `Enabled`.
    public sealed class MusicPlayer : MonoBehaviour, IMusic
    {
        [SerializeField, ReadOnlyInInspector] private MusicConfig _config;

        private readonly MusicMix _mix = new MusicMix();
        private AudioSource[] _loops;
        private AudioSource _jingle;
        private IMusicClips _clips;
        private bool _enabled = true;
        private int _request;
        private bool _carried;

        public MusicConfig Config => _config;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (_loops == null) return;
                foreach (var loop in _loops) loop.mute = !value;
                _jingle.mute = !value;
            }
        }

        // Where clips come from; null goes back to the config's own clips.
        public void Bind(IMusicClips clips) => _clips = clips;

        public void PlayLoop(MusicTrack track)
        {
            if (_config == null) return;
            EnsureSources();
            if (!_mix.PlayLoop(track, _config.Crossfade)) return;
            _jingle.Stop();
            var source = _loops[_mix.Live];
            source.Stop();
            StartLoopAsync(source, track, ++_request, destroyCancellationToken).Forget();
            Apply();
        }

        public void PlayOnce(MusicTrack jingle)
        {
            if (_config == null) return;
            EnsureSources();
            _mix.StopLoop(_config.JingleFade);
            StartJingleAsync(jingle, ++_request, destroyCancellationToken).Forget();
        }

        public void Duck(float volume, float seconds)
        {
            if (_config == null) return;
            _mix.StartDuck(volume, seconds, _config.DuckFade);
        }

        // Survives the unload of a game switch, then hands itself to the IMusicHost among the next scene's root objects
        // as soon as that scene has loaded (after its Awake, before its Start). A scene without one lets it go.
        public void CarryOver()
        {
            if (_carried) return;
            _carried = true;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _carried = false;
            var host = HostOf(scene);
            if (host == null)
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.MoveGameObjectToScene(gameObject, scene);
            host.Adopt(this);
        }

        private static IMusicHost HostOf(Scene scene)
        {
            var roots = new List<GameObject>(8);
            scene.GetRootGameObjects(roots);
            foreach (var root in roots)
            {
                if (root.TryGetComponent<IMusicHost>(out var host)) return host;
            }

            return null;
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void Update()
        {
            if (_loops == null) return;
            _mix.Tick(Time.unscaledDeltaTime);
            Apply();
        }

        private async UniTaskVoid StartLoopAsync(AudioSource source, MusicTrack track, int request, CancellationToken ct)
        {
            var clip = await GetClipAsync(track, ct);
            if (clip == null || request != _request) return;
            source.clip = clip;
            source.Play();
            Apply();
        }

        private async UniTaskVoid StartJingleAsync(MusicTrack jingle, int request, CancellationToken ct)
        {
            var clip = await GetClipAsync(jingle, ct);
            if (clip == null || request != _request) return;
            _jingle.clip = clip;
            _jingle.volume = _config.JingleVolume;
            _jingle.Play();
        }

        // Completes at once from the config; a loading source may take a while, and a newer request wins.
        private async UniTask<AudioClip> GetClipAsync(MusicTrack track, CancellationToken ct)
        {
            try
            {
                return await (_clips ?? _config).GetAsync(track, ct);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        private void Apply()
        {
            for (var c = 0; c < _loops.Length; c++)
            {
                _loops[c].volume = _mix.LevelOf(c) * _mix.Duck * _config.LoopVolume;
                if (_mix.Faded(c) && _loops[c].isPlaying) _loops[c].Stop();
            }
        }

        private void EnsureSources()
        {
            if (_loops != null) return;
            _loops = new[] { CreateSource(true), CreateSource(true) };
            _jingle = CreateSource(false);
            Enabled = _enabled;
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.ignoreListenerVolume = true;
            return source;
        }
    }
}
