using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Audio;
using Tileworks.Kit.Content;
using Tileworks.Kit.Core;
using UnityEngine;

namespace Tileworks.Kit.App
{
    // Music clips by their content key, one handle per track. The handles are never released: a player carried over
    // to the next app may still be playing a clip when this scope goes, and there are six tracks in all.
    public sealed class ContentMusicClips : IMusicClips
    {
        private readonly IContent _content;
        private readonly MusicConfig _config;
        private readonly Dictionary<MusicTrack, ContentHandle<AudioClip>> _handles = new Dictionary<MusicTrack, ContentHandle<AudioClip>>();

        public ContentMusicClips(IContent content, MusicConfig config)
        {
            _content = Guard.NotNull(content, nameof(content));
            _config = Guard.NotNull(config, nameof(config));
        }

        public async UniTask<AudioClip> GetAsync(MusicTrack track, CancellationToken ct)
        {
            if (_handles.TryGetValue(track, out var cached)) return cached.Asset;
            var key = _config.Key(track);
            if (string.IsNullOrEmpty(key) || !_content.Exists(key)) return _config.Clip(track);
            var handle = await _content.LoadAsync<AudioClip>(key, ct);
            if (_handles.TryGetValue(track, out cached))
            {
                handle.Dispose();
                return cached.Asset;
            }

            _handles.Add(track, handle);
            return handle.Asset;
        }
    }
}
