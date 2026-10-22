using Tileworks.Kit.Audio;

namespace TileMatch.View
{
    public sealed class SfxPlayer : SoundPlayer
    {
        public void Play(Sfx cue, float delay = 0f, float pitch = 1f) => Play((int)cue, delay, pitch);

        public void PlayStep(Sfx cue, int step, float delay = 0f) => PlayStep((int)cue, step, delay);
    }
}
