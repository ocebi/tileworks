using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Tileworks.Kit.Audio
{
    // Where the player gets a track's clip: the config's own clips, or content loaded on demand. Null means no clip.
    public interface IMusicClips
    {
        UniTask<AudioClip> GetAsync(MusicTrack track, CancellationToken ct);
    }
}
