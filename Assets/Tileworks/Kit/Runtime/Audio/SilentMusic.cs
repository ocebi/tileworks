namespace Tileworks.Kit.Audio
{
    // Plays nothing: for app scopes built without an app scene, such as tests.
    public sealed class SilentMusic : IMusic
    {
        public bool Enabled { get; set; } = true;

        public void PlayLoop(MusicTrack track)
        {
        }

        public void PlayOnce(MusicTrack jingle)
        {
        }

        public void Duck(float volume, float seconds)
        {
        }
    }
}
