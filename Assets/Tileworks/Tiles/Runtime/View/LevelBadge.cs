namespace Tileworks.Tiles.View
{
    // How a level is flagged on the Home button, in the level list and on the level-start card.
    public enum LevelBadge
    {
        None,
        Hard,
        SuperHard,
    }

    public static class LevelBadges
    {
        // From a level's sequence label, "hard" or "superHard".
        public static LevelBadge FromLabel(string label) => label switch
        {
            "hard" => LevelBadge.Hard,
            "superHard" => LevelBadge.SuperHard,
            _ => LevelBadge.None,
        };
    }
}
