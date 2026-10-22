using Tileworks.Kit.Audio;

namespace BlockPuzzle.View
{
    public sealed class PuzzleSfx : SoundPlayer
    {
        public void Play(PuzzleCue cue, float delay = 0f, float pitch = 1f) => Play((int)cue, delay, pitch);

        public void PlayStep(PuzzleCue cue, int step, float delay = 0f) => PlayStep((int)cue, step, delay);
    }
}
