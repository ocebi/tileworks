using TileMatch.Model;

namespace TileMatch.View
{
    public static class BoosterText
    {
        public static string Label(BoosterKind kind) => kind switch
        {
            BoosterKind.Undo => "Undo",
            BoosterKind.Magnet => "Magnet",
            BoosterKind.Shuffle => "Shuffle",
            _ => "+1 Slot",
        };

        public static string Name(BoosterKind kind) => kind == BoosterKind.ExtraSlot ? "Extra Slot" : Label(kind);

        public static string What(BoosterKind kind) => kind switch
        {
            BoosterKind.Undo => "Puts the last tile you picked back on the board.",
            BoosterKind.Magnet => "Makes a match for you: pulls the missing tiles into the tray.",
            BoosterKind.Shuffle => "Mixes up the tiles on the board to open new matches.",
            _ => "Adds one more slot to the tray for this level.",
        };
    }
}
