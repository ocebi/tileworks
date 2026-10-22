using System.Collections.Generic;

namespace Tileworks.Kit.Core
{
    public enum Severity : byte
    {
        Info,
        Warning,
        Error,
    }

    public readonly struct ValidationIssue
    {
        public readonly Severity Severity;
        public readonly string Path;
        public readonly string Message;

        public ValidationIssue(Severity severity, string path, string message)
        {
            Severity = severity;
            Path = path;
            Message = message;
        }

        public override string ToString() => $"[{Severity}] {Path}: {Message}";
    }

    public sealed class ValidationReport
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();
        private readonly Stack<string> _path = new Stack<string>();

        public IReadOnlyList<ValidationIssue> Issues => _issues;
        public bool HasErrors { get; private set; }
        public int ErrorCount { get; private set; }

        public void Error(string message) => Add(Severity.Error, message);
        public void Warning(string message) => Add(Severity.Warning, message);
        public void Info(string message) => Add(Severity.Info, message);

        public bool Require(bool condition, string message)
        {
            if (!condition) Error(message);
            return condition;
        }

        public PathScope Push(string segment)
        {
            _path.Push(segment);
            return new PathScope(this);
        }

        public override string ToString() => string.Join("\n", _issues);

        private void Add(Severity severity, string message)
        {
            if (severity == Severity.Error)
            {
                HasErrors = true;
                ErrorCount++;
            }

            _issues.Add(new ValidationIssue(severity, CurrentPath(), message));
        }

        private string CurrentPath()
        {
            if (_path.Count == 0) return string.Empty;
            var parts = _path.ToArray();
            System.Array.Reverse(parts);
            return string.Join("/", parts);
        }

        public readonly struct PathScope : System.IDisposable
        {
            private readonly ValidationReport _report;
            public PathScope(ValidationReport report) => _report = report;
            public void Dispose() => _report._path.Pop();
        }
    }

    public interface IValidatable
    {
        void Validate(ValidationReport report);
    }
}
