using Tileworks.Kit.Audio;

namespace Match3.View
{
    public sealed class Match3Sfx : SoundPlayer
    {
        public void Play(Match3Cue cue, float delay = 0f, float pitch = 1f) => Play((int)cue, delay, pitch);

        public void PlayStep(Match3Cue cue, int step, float delay = 0f) => PlayStep((int)cue, step, delay);
    }
}
