using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Tileworks.Tiles.Levels;
using UnityEngine;

namespace Tileworks.Tiles.Editor
{
    // A game's level editor in the Inspector: turns a payload into an editable board and back. The level inspector picks it
    // by game id, so a new game only adds a subclass.
    public abstract class LevelDesigner
    {
        private static readonly string[] Bands = { "easy", "normal", "hard", "superHard" };

        public abstract string Game { get; }

        public abstract string Title { get; }

        // Holds Levels/*.json, sequence.json and LevelPack.asset.
        public abstract string Folder { get; }

        // New levels are named <prefix>_0001 upwards.
        public abstract string Prefix { get; }

        public virtual string[] Difficulties => Bands;

        // The badge the level map shows for a difficulty; empty for none.
        public virtual string BadgeOf(string difficulty) => difficulty == "hard" || difficulty == "superHard" ? difficulty : string.Empty;

        // A pixel per cell, for the level list; null draws no picture.
        public virtual Texture2D Thumbnail(JObject payload) => null;

        // Called when the file opens and after every undo or redo.
        public abstract void Load(JObject payload);

        public abstract JObject Save();

        public abstract JObject NewPayload();

        // Calls edit.Change before changing anything.
        public abstract void OnGUI(LevelEdit edit);

        public abstract void Validate(LevelEnvelope envelope, List<string> problems);

        // Bot playouts; null when the game has no bot.
        public virtual LevelMeasure? Measure(LevelEnvelope envelope) => null;

        public virtual bool CanPlay => false;

        public virtual void Play(LevelEnvelope envelope)
        {
        }
    }

    public readonly struct LevelMeasure
    {
        public readonly float WinRate;
        public readonly float AverageMoves;
        public readonly string Difficulty;
        public readonly string Metric;

        // `metric` is the key the win rate is stored under in the level's metrics; average moves 0 means unknown.
        public LevelMeasure(float winRate, float averageMoves, string difficulty, string metric = "winRate")
        {
            WinRate = winRate;
            AverageMoves = averageMoves;
            Difficulty = difficulty;
            Metric = metric;
        }
    }
}
