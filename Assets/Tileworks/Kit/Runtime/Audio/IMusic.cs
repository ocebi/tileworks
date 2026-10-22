namespace Tileworks.Kit.Audio
{
    public enum MusicTrack
    {
        None,
        Home,
        Game1,
        Game2,
        Hard,
        Clear,
        Fail,
    }

    public interface IMusic
    {
        bool Enabled { get; set; }

        // Crossfades to a looping track; does nothing if it is already playing.
        void PlayLoop(MusicTrack track);

        // Fades the loop out and plays a jingle once; the next PlayLoop fades back in.
        void PlayOnce(MusicTrack jingle);

        // Lowers the loop to `volume` (0..1 of normal) for `seconds`.
        void Duck(float volume, float seconds);
    }

    public static class MusicTracks
    {
        // Two game loops take turns by level, and hard levels get their own.
        public static MusicTrack ForLevel(int levelNumber, bool hard) =>
            hard ? MusicTrack.Hard : levelNumber % 2 == 1 ? MusicTrack.Game1 : MusicTrack.Game2;
    }
}
