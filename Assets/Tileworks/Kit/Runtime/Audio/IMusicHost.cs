namespace Tileworks.Kit.Audio
{
    // A root object of a boot scene that takes over the player carried from the previous app (AppRoot).
    public interface IMusicHost
    {
        void Adopt(MusicPlayer player);
    }
}
