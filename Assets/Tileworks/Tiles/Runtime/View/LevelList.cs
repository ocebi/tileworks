using System;

namespace Tileworks.Tiles.View
{
    // What the level list needs from a game: where the player is, and how to start a level.
    public interface ILevelLauncher : ILevelBadges
    {
        // Levels before it are cleared.
        int CurrentIndex { get; }

        // Tiles in the list: the sequence, or every level reached in a game that keeps going past its end.
        int LevelCount { get; }

        // Replays are always free; the current level needs a life.
        bool CanLaunch(int index);
        void Launch(int index);

        // Games without hard levels keep the default.
        LevelBadge ILevelBadges.BadgeOf(int index) => LevelBadge.None;
    }

    // Home state arguments that open the level list, as the result popup's Levels button does.
    public sealed class LevelListArgs
    {
    }

    public interface ILevelListView
    {
        // The Home's Levels button or its level pill.
        event Action OpenRequested;
        event Action<int> TileClicked;
        event Action PlayClicked;
        bool IsOpen { get; }

        // `count` tiles: levels before `current` are cleared, later ones locked.
        void Open(int count, int current, int selected, bool playable);
        void Select(int index, bool playable);
        void RefuseLocked(int index);
        void Close();

        // Where each tile's Hard or Super Hard mark comes from; set once.
        void SetBadges(ILevelBadges badges) { }
    }
}
