using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.App
{
    public readonly struct BootStepResult
    {
        public readonly string Name;
        public readonly double Milliseconds;
        public readonly bool Succeeded;
        public readonly bool Critical;
        public readonly string Error;

        public BootStepResult(string name, double milliseconds, bool succeeded, bool critical, string error)
        {
            Name = name;
            Milliseconds = milliseconds;
            Succeeded = succeeded;
            Critical = critical;
            Error = error;
        }
    }

    public sealed class BootReport
    {
        public readonly List<BootStepResult> Steps = new List<BootStepResult>();
        public bool CriticalFailure { get; internal set; }
        public double TotalMilliseconds { get; internal set; }

        public override string ToString()
        {
            var text = new StringBuilder($"Boot {(CriticalFailure ? "FAILED" : "ok")} in {TotalMilliseconds:0} ms");
            foreach (var step in Steps)
                text.Append($"\n  {step.Name,-18} {step.Milliseconds,7:0.0} ms  {(step.Succeeded ? "ok" : "FAILED " + step.Error)}");
            return text.ToString();
        }
    }

    // Runs steps in dependency waves; steps in the same wave run concurrently.
    public sealed class BootPipeline
    {
        private readonly IReadOnlyList<IBootStep> _steps;
        private readonly ILog _log;

        public BootPipeline(IReadOnlyList<IBootStep> steps, ILog log)
        {
            _steps = Guard.NotNull(steps, nameof(steps));
            _log = Guard.NotNull(log, nameof(log));
        }

        public async UniTask<BootReport> RunAsync(IProgress<float> progress, CancellationToken ct)
        {
            var report = new BootReport();
            var total = Stopwatch.StartNew();
            var done = new HashSet<string>();
            var pending = new List<IBootStep>(_steps);
            ValidateNames();

            while (pending.Count > 0 && !report.CriticalFailure)
            {
                var wave = pending.FindAll(step => AllDone(step, done));
                if (wave.Count == 0)
                    throw new InvalidOperationException("Boot steps have a cycle or a missing dependency: " +
                                                        string.Join(", ", pending.ConvertAll(s => s.Name)));

                var results = await UniTask.WhenAll(wave.ConvertAll(step => RunStepAsync(step, ct)));
                foreach (var result in results)
                {
                    report.Steps.Add(result);
                    done.Add(result.Name);
                    if (!result.Succeeded && result.Critical)
                    {
                        report.CriticalFailure = true;
                    }
                }

                pending.RemoveAll(step => done.Contains(step.Name));
                progress?.Report((float)done.Count / _steps.Count);
            }

            report.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            _log.Write(report.CriticalFailure ? LogLevel.Error : LogLevel.Info, LogTag.Boot, report.ToString());
            return report;
        }

        private async UniTask<BootStepResult> RunStepAsync(IBootStep step, CancellationToken ct)
        {
            var watch = Stopwatch.StartNew();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            if (step.TimeoutMs > 0) timeout.CancelAfter(step.TimeoutMs);
            try
            {
                await step.RunAsync(timeout.Token);
                return new BootStepResult(step.Name, watch.Elapsed.TotalMilliseconds, true, step.Critical, null);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new BootStepResult(step.Name, watch.Elapsed.TotalMilliseconds, false, step.Critical, "timed out");
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                _log.Error(LogTag.Boot, $"Boot step {step.Name} failed", e);
                return new BootStepResult(step.Name, watch.Elapsed.TotalMilliseconds, false, step.Critical, e.Message);
            }
        }

        private static bool AllDone(IBootStep step, HashSet<string> done)
        {
            foreach (var dependency in step.DependsOn)
            {
                if (!done.Contains(dependency)) return false;
            }

            return true;
        }

        private void ValidateNames()
        {
            var names = new HashSet<string>();
            foreach (var step in _steps)
            {
                if (!names.Add(step.Name)) throw new InvalidOperationException($"Duplicate boot step '{step.Name}'.");
            }
        }
    }
}
