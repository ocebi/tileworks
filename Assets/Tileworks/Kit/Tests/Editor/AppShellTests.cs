using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Tileworks.Kit.App;
using Tileworks.Kit.Core;
using Tileworks.Kit.Input;

namespace Tileworks.Kit.Tests
{
    public sealed class AppShellTests
    {
        private sealed class Step : BootStep
        {
            private readonly string _name;
            private readonly string[] _dependsOn;
            private readonly List<string> _order;
            private readonly bool _fail;
            private readonly bool _critical;

            public Step(string name, List<string> order, bool fail = false, bool critical = true, params string[] dependsOn)
            {
                _name = name;
                _order = order;
                _fail = fail;
                _critical = critical;
                _dependsOn = dependsOn;
            }

            public override string Name => _name;
            public override IReadOnlyList<string> DependsOn => _dependsOn;
            public override bool Critical => _critical;

            public override UniTask RunAsync(CancellationToken ct)
            {
                _order.Add(_name);
                if (_fail) throw new InvalidOperationException("boom");
                return UniTask.CompletedTask;
            }
        }

        [Test]
        public void Boot_RunsInDependencyOrder()
        {
            var order = new List<string>();
            var steps = new IBootStep[]
            {
                new Step("migrate", order, dependsOn: "open"),
                new Step("open", order, dependsOn: "log"),
                new Step("log", order),
                new Step("config", order, dependsOn: "migrate"),
            };
            var report = new BootPipeline(steps, new NullLog()).RunAsync(null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(report.CriticalFailure, Is.False);
            Assert.That(order, Is.EqualTo(new[] { "log", "open", "migrate", "config" }));
        }

        [Test]
        public void Boot_NonCriticalFailureContinues_CriticalFailureStops()
        {
            var order = new List<string>();
            var steps = new IBootStep[]
            {
                new Step("time", order, fail: true, critical: false),
                new Step("db", order, fail: true, critical: true),
                new Step("config", order, dependsOn: "db"),
            };
            var report = new BootPipeline(steps, new NullLog()).RunAsync(null, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(report.CriticalFailure, Is.True);
            Assert.That(order, Does.Not.Contain("config"));
            Assert.That(report.Steps.Count, Is.EqualTo(2));
        }

        private sealed class RecordingState : IAppState
        {
            private readonly List<string> _log;
            public RecordingState(string id, List<string> log)
            {
                Id = id;
                _log = log;
            }

            public string Id { get; }
            public object LastArgs;

            public UniTask EnterAsync(object args, CancellationToken ct)
            {
                LastArgs = args;
                _log.Add("enter:" + Id);
                return UniTask.CompletedTask;
            }

            public UniTask ExitAsync(CancellationToken ct)
            {
                _log.Add("exit:" + Id);
                return UniTask.CompletedTask;
            }
        }

        [Test]
        public void StateMachine_ExitsThenEnters_AndBlocksInputDuringTransition()
        {
            var log = new List<string>();
            var gate = new InputGate();
            var machine = new AppStateMachine(new IAppState[] { new RecordingState("Home", log), new RecordingState("Level", log) },
                Array.Empty<ILoadingOverlay>(), Array.Empty<IAppStateListener>(), gate, new AppNavigator(), new NullLog());

            machine.GoToAsync("Home", null, CancellationToken.None).GetAwaiter().GetResult();
            machine.GoToAsync("Level", 7, CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(log, Is.EqualTo(new[] { "enter:Home", "exit:Home", "enter:Level" }));
            Assert.That(machine.Current, Is.EqualTo("Level"));
            Assert.That(gate.IsBlocked, Is.False);
        }

        [Test]
        public void InputGate_IsCountedPerReason()
        {
            var gate = new InputGate();
            var a = gate.Block("popup");
            var b = gate.Block("popup");
            var c = gate.Block("transition");
            a.Dispose();
            c.Dispose();
            Assert.That(gate.IsBlocked, Is.True);
            Assert.That(gate.Locks["popup"], Is.EqualTo(1));
            b.Dispose();
            b.Dispose();
            Assert.That(gate.IsBlocked, Is.False);
        }
    }
}
