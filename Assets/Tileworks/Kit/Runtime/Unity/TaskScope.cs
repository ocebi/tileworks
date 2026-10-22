using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Unity
{
    // Owns long-running async work for one DI scope; disposing cancels everything it started.
    public sealed class TaskScope : IDisposable
    {
        private readonly CancellationTokenSource _cts;
        private readonly ILog _log;
        private readonly HashSet<string> _running = new HashSet<string>();
        private readonly List<TaskScope> _children = new List<TaskScope>();
        private readonly TaskScope _parent;

        public TaskScope(ILog log) : this(log, null) { }

        private TaskScope(ILog log, TaskScope parent)
        {
            _log = Guard.NotNull(log, nameof(log));
            _parent = parent;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(parent?.Token ?? CancellationToken.None);
        }

        public CancellationToken Token => _cts.Token;
        public bool IsDisposed { get; private set; }
        public IReadOnlyCollection<string> Running => _running;

        public TaskScope CreateChild()
        {
            var child = new TaskScope(_log, this);
            _children.Add(child);
            return child;
        }

        public void Run(Func<CancellationToken, UniTask> work, string name)
        {
            if (IsDisposed) return;
            RunTracked(work, name).Forget();
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            var children = _children.ToArray();
            _children.Clear();
            foreach (var child in children) child.Dispose();
            _parent?._children.Remove(this);
            _cts.Cancel();
            _cts.Dispose();
        }

        private async UniTaskVoid RunTracked(Func<CancellationToken, UniTask> work, string name)
        {
            _running.Add(name);
            try
            {
                await work(_cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _log.Error(LogTag.App, $"Task '{name}' failed", e);
            }
            finally
            {
                _running.Remove(name);
            }
        }
    }

    public static class UniTaskExtensions
    {
        public static void Forget(this UniTask task, ILog log, string name)
        {
            Run(task, log, name).Forget();
        }

        private static async UniTaskVoid Run(UniTask task, ILog log, string name)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                log.Error(LogTag.App, $"'{name}' failed", e);
            }
        }
    }
}
