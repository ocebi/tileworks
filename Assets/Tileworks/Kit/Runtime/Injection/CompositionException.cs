using System;
using System.Collections.Generic;

namespace Tileworks.Kit.Injection
{
    public sealed class CompositionException : Exception
    {
        public CompositionException(string scopeName, IReadOnlyList<string> problems)
            : base(Format(scopeName, problems))
        {
            ScopeName = scopeName;
            Problems = problems;
        }

        public string ScopeName { get; }
        public IReadOnlyList<string> Problems { get; }

        private static string Format(string scopeName, IReadOnlyList<string> problems) =>
            $"Scope '{scopeName}' has {problems.Count} composition problem(s):\n  - " + string.Join("\n  - ", problems);
    }
}
