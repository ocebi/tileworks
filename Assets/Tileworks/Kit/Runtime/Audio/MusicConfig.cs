using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Tileworks.Kit.Audio
{
    // The app's music tracks and how they fade, shared by every game. It serves its own clips (IMusicClips) when the
    // player has no other source.
    [CreateAssetMenu(menuName = "Tileworks/Music")]
    public sealed class MusicConfig : ScriptableObject, IMusicClips
    {
        [Header("Tracks")]
        [SerializeField] private AudioClip _home;
        [SerializeField] private AudioClip _game1;
        [SerializeField] private AudioClip _game2;
        [SerializeField] private AudioClip _hard;
        [SerializeField, Tooltip("Played once on the win screen.")] private AudioClip _clear;
        [SerializeField, Tooltip("Played once on the lose screen.")] private AudioClip _fail;

        [Header("Content keys"), Tooltip("Loaded through the content system when the clips above are left empty.")]
        [SerializeField] private string _homeKey;
        [SerializeField] private string _game1Key;
        [SerializeField] private string _game2Key;
        [SerializeField] private string _hardKey;
        [SerializeField] private string _clearKey;
        [SerializeField] private string _failKey;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float _loopVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _jingleVolume = 0.8f;
        [SerializeField, Range(0f, 3f), Tooltip("Seconds for one loop to fade into the next.")] private float _crossfade = 0.5f;
        [SerializeField, Range(0f, 1f), Tooltip("Seconds for the loop to fade out when a jingle starts.")] private float _jingleFade = 0.15f;
        [SerializeField, Range(0f, 1f), Tooltip("Seconds to dip into and recover from a duck.")] private float _duckFade = 0.15f;

        public float LoopVolume => _loopVolume;
        public float JingleVolume => _jingleVolume;
        public float Crossfade => _crossfade;
        public float JingleFade => _jingleFade;
        public float DuckFade => _duckFade;

        public AudioClip Clip(MusicTrack track) => track switch
        {
            MusicTrack.Home => _home,
            MusicTrack.Game1 => _game1,
            MusicTrack.Game2 => _game2,
            MusicTrack.Hard => _hard,
            MusicTrack.Clear => _clear,
            MusicTrack.Fail => _fail,
            _ => null,
        };

        public string Key(MusicTrack track) => track switch
        {
            MusicTrack.Home => _homeKey,
            MusicTrack.Game1 => _game1Key,
            MusicTrack.Game2 => _game2Key,
            MusicTrack.Hard => _hardKey,
            MusicTrack.Clear => _clearKey,
            MusicTrack.Fail => _failKey,
            _ => null,
        };

        public UniTask<AudioClip> GetAsync(MusicTrack track, CancellationToken ct) => UniTask.FromResult(Clip(track));

        public void Configure(AudioClip home, AudioClip game1, AudioClip game2, AudioClip hard, AudioClip clear, AudioClip fail)
        {
            _home = home;
            _game1 = game1;
            _game2 = game2;
            _hard = hard;
            _clear = clear;
            _fail = fail;
        }

        public void ConfigureKeys(string home, string game1, string game2, string hard, string clear, string fail)
        {
            _homeKey = home;
            _game1Key = game1;
            _game2Key = game2;
            _hardKey = hard;
            _clearKey = clear;
            _failKey = fail;
        }
    }
}
