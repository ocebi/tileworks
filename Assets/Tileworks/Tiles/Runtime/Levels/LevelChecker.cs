using System;
using Tileworks.Kit.Core;

namespace Tileworks.Tiles.Levels
{
    // Decides whether this build can play a downloaded level.
    public interface ILevelChecker
    {
        string Game { get; }
        int PayloadVersion { get; }

        // Null when playable, otherwise why not.
        string Check(LevelEnvelope envelope);
    }

    public sealed class SerializerLevelChecker<TLevel> : ILevelChecker
    {
        private readonly ILevelSerializer<TLevel> _serializer;

        public SerializerLevelChecker(ILevelSerializer<TLevel> serializer) => _serializer = Guard.NotNull(serializer, nameof(serializer));

        public string Game => _serializer.Game;
        public int PayloadVersion => _serializer.CurrentPayloadVersion;

        public string Check(LevelEnvelope envelope)
        {
            try
            {
                var report = new ValidationReport();
                _serializer.Validate(_serializer.Read(envelope), report);
                return report.HasErrors ? report.ToString() : null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
