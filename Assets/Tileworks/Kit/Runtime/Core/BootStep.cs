using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Tileworks.Kit.Core
{
    public interface IBootStep
    {
        string Name { get; }
        IReadOnlyList<string> DependsOn { get; }
        bool Critical { get; }
        int TimeoutMs { get; }
        UniTask RunAsync(CancellationToken ct);
    }

    // Convenience base for steps; most only override RunAsync.
    public abstract class BootStep : IBootStep
    {
        private static readonly string[] NoDependencies = Array.Empty<string>();

        public abstract string Name { get; }
        public virtual IReadOnlyList<string> DependsOn => NoDependencies;
        public virtual bool Critical => true;
        public virtual int TimeoutMs => 10000;
        public abstract UniTask RunAsync(CancellationToken ct);
    }
}
