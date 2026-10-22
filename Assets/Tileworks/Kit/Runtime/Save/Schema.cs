using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Save
{
    public interface ISchemaModule
    {
        string Module { get; }

        IReadOnlyList<Migration> Migrations { get; }
    }

    public sealed class Migration
    {
        public Migration(int version, string description, string[] sql, Action<IDatabase> data = null)
        {
            Version = version;
            Description = Guard.NotEmpty(description, nameof(description));
            Sql = sql ?? Array.Empty<string>();
            Data = data;
        }

        public int Version { get; }
        public string Description { get; }

        public string[] Sql { get; }

        public Action<IDatabase> Data { get; }
    }

    public sealed class MigrationException : Exception
    {
        public MigrationException(string module, int version, Exception inner)
            : base($"Migration {module} v{version} failed: {inner.Message}", inner)
        {
            Module = module;
            Version = version;
        }

        public MigrationException(string module, string message) : base($"{module}: {message}")
        {
            Module = module;
        }

        public string Module { get; }
        public int Version { get; }
    }

    public readonly struct AppliedMigration
    {
        public readonly string Module;
        public readonly int Version;
        public readonly string Description;

        public AppliedMigration(string module, int version, string description)
        {
            Module = module;
            Version = version;
            Description = description;
        }

        public override string ToString() => $"{Module} v{Version}: {Description}";
    }
}
