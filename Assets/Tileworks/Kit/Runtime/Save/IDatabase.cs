using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Save
{
    public interface IDatabase
    {
        bool IsOpen { get; }
        string Path { get; }

        // Nested Begin is a savepoint. Dispose without Commit rolls back.
        ITransaction Begin(string name);

        ITransaction Current { get; }

        RingBuffer<TransactionRecord> RecentTransactions { get; }

        int Execute(string sql, params object[] args);
        T Scalar<T>(string sql, params object[] args);
        List<T> Query<T>(string sql, params object[] args) where T : new();

        T Find<T>(object primaryKey) where T : class, new();
        int Insert(object entity);
        int InsertOrReplace(object entity);
        int Update(object entity);
        int Delete<T>(object primaryKey);
        string TableOf<T>();

        // Drops every table; the next boot migrates from nothing.
        void Wipe();
    }

    public interface ITransaction : IDisposable
    {
        string Name { get; }
        bool IsCompleted { get; }
        void Commit();

        // Runs after the outermost commit; dropped on rollback.
        void OnCommitted(Action action);
    }

    public readonly struct TransactionRecord
    {
        public readonly string Name;
        public readonly double Milliseconds;
        public readonly int RowsChanged;
        public readonly bool Committed;

        public TransactionRecord(string name, double milliseconds, int rowsChanged, bool committed)
        {
            Name = name;
            Milliseconds = milliseconds;
            RowsChanged = rowsChanged;
            Committed = committed;
        }

        public override string ToString() =>
            $"{Name}: {(Committed ? "committed" : "rolled back")} in {Milliseconds:0.00} ms, {RowsChanged} rows";
    }
}
