using Tileworks.Kit.Audio;

namespace Blast.View
{
    public sealed class BlastSfx : SoundPlayer
    {
        public void Play(BlastCue cue, float delay = 0f, float pitch = 1f) => Play((int)cue, delay, pitch);

        public void PlayStep(BlastCue cue, int step, float delay = 0f) => PlayStep((int)cue, step, delay);
    }
}
