using System;
using Newtonsoft.Json.Linq;
using Tileworks.Kit.Core;
using Tileworks.Tiles.Levels;

namespace TileMatch.Model
{
    // Payload v1: tiles as [x, y, layer, kind] rows, modifiers by name, spawner queues as kind lists.
    public sealed class TileMatchLevelSerializer : ILevelSerializer<TileMatchLevel>
    {
        public const string GameId = "tilematch";

        public string Game => GameId;
        public int CurrentPayloadVersion => 1;

        public TileMatchLevel Read(LevelEnvelope envelope)
        {
            if (envelope.Game != GameId) throw new FormatException($"{envelope.Id} is a '{envelope.Game}' level.");
            if (envelope.PayloadVersion > CurrentPayloadVersion) throw new FormatException($"{envelope.Id} payload v{envelope.PayloadVersion} is newer than this build.");

            var p = envelope.Payload;
            var builder = new TileMatchLevelBuilder().Tray((int?)p["tray"] ?? 7).TimeLimit((int?)p["timeLimit"] ?? 0);
            foreach (var row in (JArray)p["tiles"])
                builder.Add((int)row[0], (int)row[1], (int)row[2], (ushort)((int?)row[3] ?? 0));

            if (p["modifiers"] is JArray modifiers)
            {
                foreach (var m in modifiers)
                    builder.Modifier(TypeOf((string)m["type"]), (int)m["tile"], (int?)m["value"] ?? 0);
            }

            if (p["spawners"] is JArray spawners)
            {
                foreach (var s in spawners)
                {
                    var queue = (JArray)s["queue"];
                    var kinds = new ushort[queue.Count];
                    for (var i = 0; i < kinds.Length; i++) kinds[i] = (ushort)(int)queue[i];
                    builder.Spawner((int)s["tile"], kinds);
                }
            }

            return builder.Build(envelope.Id);
        }

        public JObject Write(TileMatchLevel level)
        {
            var tiles = new JArray();
            for (var tile = 0; tile < level.BoardTileCount; tile++)
            {
                var slot = level.Layout.CoordOf(level.SlotOf(tile));
                tiles.Add(new JArray(slot.X, slot.Y, slot.Layer, level.KindOf(tile)));
            }

            var payload = new JObject { ["tray"] = level.TrayCapacity, ["timeLimit"] = level.TimeLimitSec, ["tiles"] = tiles };

            if (level.Modifiers.Length > 0)
            {
                var modifiers = new JArray();
                foreach (var m in level.Modifiers)
                {
                    var entry = new JObject { ["type"] = NameOf(m.Type), ["tile"] = m.Tile };
                    if (m.Value != 0) entry["value"] = m.Value;
                    modifiers.Add(entry);
                }

                payload["modifiers"] = modifiers;
            }

            if (level.Spawners.Length > 0)
            {
                var spawners = new JArray();
                foreach (var s in level.Spawners)
                {
                    var queue = new JArray();
                    foreach (var tile in s.Queue) queue.Add(level.KindOf(tile));
                    spawners.Add(new JObject { ["tile"] = s.Slot, ["queue"] = queue });
                }

                payload["spawners"] = spawners;
            }

            return payload;
        }

        public void Validate(TileMatchLevel level, ValidationReport report)
        {
            TileMatchLevelBuilder.Validate(level, report);
            foreach (var m in level.Modifiers)
            {
                report.Require(m.Tile >= 0 && m.Tile < level.TileCount, $"{NameOf(m.Type)} targets missing tile {m.Tile}");
                if (m.Type == ModifierTypes.Linked)
                    report.Require(m.Value >= 0 && m.Value < level.TileCount && m.Value != m.Tile, $"link {m.Tile} has an invalid partner {m.Value}");
            }
        }

        public static string NameOf(ushort type) => type switch
        {
            ModifierTypes.Frozen => "frozen",
            ModifierTypes.Hidden => "hidden",
            ModifierTypes.Linked => "linked",
            _ => type.ToString(),
        };

        public static ushort TypeOf(string name) => name switch
        {
            "frozen" => ModifierTypes.Frozen,
            "hidden" => ModifierTypes.Hidden,
            "linked" => ModifierTypes.Linked,
            _ => ushort.TryParse(name, out var id) ? id : throw new FormatException($"Unknown modifier '{name}'."),
        };
    }
}
