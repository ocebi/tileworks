using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SQLite;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Save
{
    public sealed class DatabaseOptions
    {
        public string Path = ":memory:";

        public bool UseWal = true; // false on WebGL: the browser VFS has no shared memory

        public static DatabaseOptions InMemory() => new DatabaseOptions { Path = ":memory:", UseWal = false };
    }

    public sealed class SqliteDatabase : IDatabase, IDisposable
    {
        private readonly DatabaseOptions _options;
        private readonly ILog _log;
        private readonly List<Transaction> _stack = new List<Transaction>(4);
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private SQLiteConnection _connection;
        private int _rowsInTransaction;

        public SqliteDatabase(DatabaseOptions options, ILog log)
        {
            _options = Guard.NotNull(options, nameof(options));
            _log = Guard.NotNull(log, nameof(log));
        }

        public bool IsOpen => _connection != null;
        public string Path => _options.Path;
        public RingBuffer<TransactionRecord> RecentTransactions { get; } = new RingBuffer<TransactionRecord>(64);
        public ITransaction Current => _stack.Count == 0 ? null : _stack[_stack.Count - 1];
        public bool IsInMemory => _options.Path == ":memory:";

        public void Open()
        {
            if (IsOpen) return;
            if (!IsInMemory)
            {
                var directory = System.IO.Path.GetDirectoryName(_options.Path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            }

            _connection = new SQLiteConnection(_options.Path,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.NoMutex, storeDateTimeAsTicks: true);
            var journal = _options.UseWal && !IsInMemory ? "WAL" : "DELETE";
            _connection.ExecuteScalar<string>("PRAGMA journal_mode=" + journal);
            _connection.Execute("PRAGMA synchronous=NORMAL");
            _connection.Execute("PRAGMA foreign_keys=ON");
            _log.Info(LogTag.Save, "Opened {0} (journal {1})", IsInMemory ? ":memory:" : _options.Path, journal);
        }

        public bool QuickCheck()
        {
            EnsureOpen();
            try
            {
                return string.Equals(_connection.ExecuteScalar<string>("PRAGMA quick_check"), "ok", StringComparison.OrdinalIgnoreCase);
            }
            catch (SQLiteException e)
            {
                _log.Error(LogTag.Save, "quick_check failed", e);
                return false;
            }
        }

        public void BackupTo(string targetPath)
        {
            EnsureOpen();
            if (_stack.Count > 0) throw new InvalidOperationException("Cannot back up inside a transaction.");
            if (File.Exists(targetPath)) File.Delete(targetPath);
            _connection.Execute("VACUUM INTO ?", targetPath);
        }

        public void Close()
        {
            if (_connection == null) return;
            if (_stack.Count > 0)
            {
                _log.Warn(LogTag.Save, "Closing with {0} open transaction(s); rolling back.", _stack.Count);
                _connection.Execute("ROLLBACK");
                _stack.Clear();
            }

            _connection.Close();
            _connection.Dispose();
            _connection = null;
        }

        public void Dispose() => Close();

        public void Wipe()
        {
            foreach (var table in Connection.QueryScalars<string>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"))
                Connection.Execute("DROP TABLE IF EXISTS \"" + table + "\"");
            _log.Info(LogTag.Save, "Wiped every table");
        }

        public ITransaction Begin(string name)
        {
            EnsureOpen();
            var depth = _stack.Count;
            if (depth == 0)
            {
                _connection.Execute("BEGIN IMMEDIATE");
                _rowsInTransaction = 0;
                _stopwatch.Restart();
            }
            else
            {
                _connection.Execute("SAVEPOINT sp" + depth);
            }

            var transaction = new Transaction(this, name, depth);
            _stack.Add(transaction);
            return transaction;
        }

        private void Complete(Transaction transaction, bool commit)
        {
            if (_stack.Count == 0 || _stack[_stack.Count - 1] != transaction)
                throw new InvalidOperationException($"Transaction '{transaction.Name}' is not the innermost open transaction.");
            _stack.RemoveAt(_stack.Count - 1);

            if (transaction.Depth == 0)
            {
                _connection.Execute(commit ? "COMMIT" : "ROLLBACK");
                _stopwatch.Stop();
                RecentTransactions.Push(new TransactionRecord(transaction.Name, _stopwatch.Elapsed.TotalMilliseconds,
                    _rowsInTransaction, commit));
                if (commit) transaction.RunCommitted(_log);
                return;
            }

            if (commit)
            {
                _connection.Execute("RELEASE sp" + transaction.Depth);
                _stack[_stack.Count - 1].Adopt(transaction);
            }
            else
            {
                _connection.Execute("ROLLBACK TO sp" + transaction.Depth);
                _connection.Execute("RELEASE sp" + transaction.Depth);
            }
        }

        public int Execute(string sql, params object[] args) => Count(Connection.Execute(sql, args));
        public T Scalar<T>(string sql, params object[] args) => Connection.ExecuteScalar<T>(sql, args);
        public List<T> Query<T>(string sql, params object[] args) where T : new() => Connection.Query<T>(sql, args);
        public T Find<T>(object primaryKey) where T : class, new() => Connection.Find<T>(primaryKey);
        public int Insert(object entity) => Count(Connection.Insert(entity));
        public int InsertOrReplace(object entity) => Count(Connection.InsertOrReplace(entity));
        public int Update(object entity) => Count(Connection.Update(entity));
        public int Delete<T>(object primaryKey) => Count(Connection.Delete<T>(primaryKey));
        public string TableOf<T>() => Connection.GetMapping<T>().TableName;

        // DevTools SQL console only.
        public List<string[]> QueryText(string sql, int maxRows = 200)
        {
            var rows = new List<string[]>();
            var statement = SQLite3.Prepare2(Connection.Handle, sql);
            try
            {
                var columns = SQLite3.ColumnCount(statement);
                var header = new string[columns];
                for (var i = 0; i < columns; i++) header[i] = SQLite3.ColumnName16(statement, i);
                rows.Add(header);
                while (rows.Count <= maxRows && SQLite3.Step(statement) == SQLite3.Result.Row)
                {
                    var row = new string[columns];
                    for (var i = 0; i < columns; i++) row[i] = SQLite3.ColumnString(statement, i);
                    rows.Add(row);
                }
            }
            finally
            {
                SQLite3.Finalize(statement);
            }

            return rows;
        }

        private SQLiteConnection Connection
        {
            get
            {
                EnsureOpen();
                return _connection;
            }
        }

        private int Count(int rows)
        {
            if (_stack.Count > 0) _rowsInTransaction += rows;
            return rows;
        }

        private void EnsureOpen()
        {
            if (_connection == null) throw new InvalidOperationException("The database is not open.");
        }

        private sealed class Transaction : ITransaction
        {
            private readonly SqliteDatabase _db;
            private List<Action> _onCommitted;

            public Transaction(SqliteDatabase db, string name, int depth)
            {
                _db = db;
                Name = name;
                Depth = depth;
            }

            public string Name { get; }
            public int Depth { get; }
            public bool IsCompleted { get; private set; }

            public void Commit()
            {
                if (IsCompleted) throw new InvalidOperationException($"Transaction '{Name}' already completed.");
                IsCompleted = true;
                _db.Complete(this, commit: true);
            }

            public void OnCommitted(Action action)
            {
                if (IsCompleted) throw new InvalidOperationException($"Transaction '{Name}' already completed.");
                (_onCommitted ??= new List<Action>(4)).Add(action);
            }

            public void Dispose()
            {
                if (IsCompleted) return;
                IsCompleted = true;
                _db.Complete(this, commit: false);
            }

            public void Adopt(Transaction inner)
            {
                if (inner._onCommitted == null) return;
                (_onCommitted ??= new List<Action>(inner._onCommitted.Count)).AddRange(inner._onCommitted);
            }

            public void RunCommitted(ILog log)
            {
                if (_onCommitted == null) return;
                foreach (var action in _onCommitted)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        log.Error(LogTag.Save, $"After-commit action of '{Name}' failed", e);
                    }
                }
            }
        }
    }
}
