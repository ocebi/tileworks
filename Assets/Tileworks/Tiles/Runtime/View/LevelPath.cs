namespace Tileworks.Tiles.View
{
    public interface ILevelBadges
    {
        LevelBadge BadgeOf(int index);
    }

    public interface ILevelPathHandler
    {
        void OnNodeTapped(int index);
    }

    // A stretch of the level sequence drawn as a path on the Home screen.
    public interface ILevelPathView
    {
        // Null unbinds.
        void Bind(ILevelPathHandler handler);

        // Nodes for levels [first, first + count); levels before `current` are cleared, those after it locked.
        void Show(int first, int count, int current, ILevelBadges badges);
        void RefuseLocked(int index);
    }

    // Opens the Home's level list on a level, as a path node does.
    public interface ILevelListOpener
    {
        void OpenAt(int index);
    }
}
