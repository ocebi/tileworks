using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;

namespace Tileworks.Kit.Tests
{
    public sealed class ContainerTests
    {
        public interface IService { }

        public sealed class ServiceA : IService { }

        public sealed class ServiceB : IService { }

        public sealed class NeedsService
        {
            public NeedsService(IService service) => Service = service;
            public IService Service { get; }
        }

        public sealed class NeedsAll
        {
            public NeedsAll(IReadOnlyList<IService> services) => Services = services;
            public IReadOnlyList<IService> Services { get; }
        }

        public sealed class CycleA
        {
            public CycleA(CycleB b) { }
        }

        public sealed class CycleB
        {
            public CycleB(CycleA a) { }
        }

        public sealed class TwoConstructors
        {
            public TwoConstructors() { }
            public TwoConstructors(IService service) { }
        }

        public sealed class Recorder
        {
            public readonly List<string> Events = new List<string>();
        }

        public sealed class Tracked : IDisposable, IInitializable, IAsyncInitializable
        {
            private readonly Recorder _recorder;
            private readonly string _name;

            public Tracked(Recorder recorder, string name)
            {
                _recorder = recorder;
                _name = name;
            }

            public void Initialize() => _recorder.Events.Add("init:" + _name);

            public UniTask InitializeAsync(CancellationToken ct)
            {
                _recorder.Events.Add("async:" + _name);
                return UniTask.CompletedTask;
            }

            public void Dispose() => _recorder.Events.Add("dispose:" + _name);
        }

        public sealed class First : IDisposable, IInitializable
        {
            private readonly Recorder _recorder;
            public First(Recorder recorder) => _recorder = recorder;
            public void Initialize() => _recorder.Events.Add("init:first");
            public void Dispose() => _recorder.Events.Add("dispose:first");
        }

        public sealed class Second : IDisposable, IInitializable
        {
            private readonly Recorder _recorder;
            public Second(Recorder recorder, First first) => _recorder = recorder;
            public void Initialize() => _recorder.Events.Add("init:second");
            public void Dispose() => _recorder.Events.Add("dispose:second");
        }

        public sealed class Counter : ITickable
        {
            public int Ticks;
            public void Tick() => Ticks++;
        }

        public sealed class FakeLoop : ILoopRegistry
        {
            public readonly List<ITickable> Tickables = new List<ITickable>();

            public IDisposable AddTickable(ITickable tickable)
            {
                Tickables.Add(tickable);
                return new Core.Disposable(() => Tickables.Remove(tickable));
            }

            public IDisposable AddLateTickable(ILateTickable tickable) => Core.Disposable.Empty;
            public IDisposable AddLifecycleListener(IAppLifecycleListener listener) => Core.Disposable.Empty;
        }

        public sealed class DisposableTransient : IDisposable
        {
            public bool Disposed;
            public void Dispose() => Disposed = true;
        }

        public sealed class HoldsTransient
        {
            public HoldsTransient(DisposableTransient transient) { }
        }

        [Test]
        public void Resolves_ConstructorDependencies()
        {
            var b = new ContainerBuilder();
            b.Register<ServiceA>(Lifetime.Single).As<IService>();
            b.Register<NeedsService>(Lifetime.Single);
            using var scope = b.Build("App");

            var consumer = scope.Resolve<NeedsService>();
            Assert.That(consumer.Service, Is.InstanceOf<ServiceA>());
            Assert.That(scope.Resolve<IService>(), Is.SameAs(consumer.Service), "Single is shared");
        }

        [Test]
        public void Transient_CreatesNewInstanceEachTime()
        {
            var b = new ContainerBuilder();
            b.Register<ServiceA>(Lifetime.Transient).As<IService>();
            using var scope = b.Build("App");

            Assert.That(scope.Resolve<IService>(), Is.Not.SameAs(scope.Resolve<IService>()));
        }

        [Test]
        public void Collections_GatherNearestScopeFirst_ThenRegistrationOrder()
        {
            var appBuilder = new ContainerBuilder();
            appBuilder.Register<ServiceA>(Lifetime.Single).As<IService>();
            using var app = appBuilder.Build("App");

            using var child = app.CreateChild("Level", b =>
            {
                b.Register<ServiceB>(Lifetime.Single).As<IService>();
                b.Register<NeedsAll>(Lifetime.Single);
            });

            var all = child.Resolve<NeedsAll>().Services;
            Assert.That(all.Count, Is.EqualTo(2));
            Assert.That(all[0], Is.InstanceOf<ServiceB>());
            Assert.That(all[1], Is.InstanceOf<ServiceA>());
        }

        [Test]
        public void EmptyCollection_ResolvesToEmptyList()
        {
            var b = new ContainerBuilder();
            b.Register<NeedsAll>(Lifetime.Single);
            using var scope = b.Build("App");

            Assert.That(scope.Resolve<NeedsAll>().Services, Is.Empty);
        }

        [Test]
        public void ChildScope_SeesParentRegistrations_AndParentSinglesStayInParent()
        {
            var appBuilder = new ContainerBuilder();
            appBuilder.Register<ServiceA>(Lifetime.Single).As<IService>();
            using var app = appBuilder.Build("App");
            var child = app.CreateChild("Game", b => b.Register<NeedsService>(Lifetime.Single));

            Assert.That(child.Resolve<NeedsService>().Service, Is.SameAs(app.Resolve<IService>()));
            child.Dispose();
            Assert.That(app.Resolve<IService>(), Is.Not.Null, "Disposing a child leaves the parent's singles alive");
            Assert.That(app.Children, Is.Empty);
        }

        [Test]
        public void Factory_ReceivesResolver()
        {
            var b = new ContainerBuilder();
            b.Register<ServiceA>(Lifetime.Single).As<IService>();
            b.RegisterFactory(r => new NeedsService(r.Resolve<IService>()), Lifetime.Single);
            using var scope = b.Build("App");

            Assert.That(scope.Resolve<NeedsService>().Service, Is.InstanceOf<ServiceA>());
        }

        [Test]
        public void Build_ReportsMissingDependency_WithScopeChain()
        {
            using var app = new ContainerBuilder().Build("App");
            var b = new ContainerBuilder();
            b.Register<NeedsService>(Lifetime.Single);

            var e = Assert.Throws<CompositionException>(() => b.Build("Shop", app));
            Assert.That(e.Problems.Count, Is.EqualTo(1));
            StringAssert.Contains("NeedsService → IService", e.Problems[0]);
            StringAssert.Contains("Shop or App", e.Problems[0]);
        }

        [Test]
        public void Build_ReportsCycle()
        {
            var b = new ContainerBuilder();
            b.Register<CycleA>(Lifetime.Single);
            b.Register<CycleB>(Lifetime.Single);

            var e = Assert.Throws<CompositionException>(() => b.Build("App"));
            Assert.That(e.Problems, Has.Some.Contains("Cycle: CycleA → CycleB → CycleA"));
        }

        [Test]
        public void Build_ReportsMultipleConstructors()
        {
            var b = new ContainerBuilder();
            b.Register<TwoConstructors>(Lifetime.Single);

            var e = Assert.Throws<CompositionException>(() => b.Build("App"));
            StringAssert.Contains("2 public constructors", e.Problems[0]);
        }

        [Test]
        public void Build_ReportsAmbiguousSingleValuedDependency()
        {
            var b = new ContainerBuilder();
            b.Register<ServiceA>(Lifetime.Single).As<IService>();
            b.Register<ServiceB>(Lifetime.Single).As<IService>();
            b.Register<NeedsService>(Lifetime.Single);

            var e = Assert.Throws<CompositionException>(() => b.Build("App"));
            StringAssert.Contains("ambiguous", e.Problems[0]);
        }

        [Test]
        public void Build_ReportsEveryProblemAtOnce()
        {
            var b = new ContainerBuilder();
            b.Register<NeedsService>(Lifetime.Single);
            b.Register<TwoConstructors>(Lifetime.Single);
            b.Register<CycleA>(Lifetime.Single);
            b.Register<CycleB>(Lifetime.Single);

            var e = Assert.Throws<CompositionException>(() => b.Build("App"));
            Assert.That(e.Problems.Count, Is.EqualTo(3));
        }

        [Test]
        public void Build_WarnsWhenSingleCapturesDisposableTransient()
        {
            var b = new ContainerBuilder();
            b.Register<DisposableTransient>(Lifetime.Transient);
            b.Register<HoldsTransient>(Lifetime.Single);
            using var scope = b.Build("App");

            Assert.That(scope.Warnings, Has.Some.Contains("captures transient disposable"));
        }

        [Test]
        public void Start_InitializesInCreationOrder_AndDisposeRunsInReverse()
        {
            var recorder = new Recorder();
            var b = new ContainerBuilder();
            b.RegisterInstance(recorder);
            b.Register<Second>(Lifetime.Single); // registered first, but depends on First
            b.Register<First>(Lifetime.Single);
            var scope = b.Build("App");

            scope.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            scope.Dispose();

            Assert.That(recorder.Events, Is.EqualTo(new[]
            {
                "init:first", "init:second", "dispose:second", "dispose:first",
            }));
        }

        [Test]
        public void Start_RunsSyncThenAsyncInitialization()
        {
            var recorder = new Recorder();
            var b = new ContainerBuilder();
            b.RegisterInstance(recorder);
            b.RegisterFactory(r => new Tracked(r.Resolve<Recorder>(), "x"), Lifetime.Single);
            b.RegisterEntryPoint<EntryPointUsingTracked>();
            var scope = b.Build("App");

            scope.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(recorder.Events, Is.EqualTo(new[] { "init:x", "async:x" }));
        }

        public sealed class EntryPointUsingTracked
        {
            public EntryPointUsingTracked(Tracked tracked) { }
        }

        [Test]
        public void Dispose_DisposesChildrenFirst()
        {
            var recorder = new Recorder();
            var appBuilder = new ContainerBuilder();
            appBuilder.RegisterInstance(recorder);
            appBuilder.RegisterFactory(r => new Tracked(r.Resolve<Recorder>(), "app"), Lifetime.Single);
            var app = appBuilder.Build("App");
            app.Resolve<Tracked>();

            var level = app.CreateChild("Level", b => b.RegisterFactory(r => new Tracked(r.Resolve<Recorder>(), "level"), Lifetime.Single));
            level.Resolve<Tracked>();

            app.Dispose();
            Assert.That(recorder.Events, Is.EqualTo(new[] { "dispose:level", "dispose:app" }));
            Assert.That(level.IsDisposed, Is.True);
        }

        [Test]
        public void RegisteredInstances_AreNotDisposed_UnlessOwned()
        {
            var notOwned = new DisposableTransient();
            var owned = new DisposableTransient();
            var b = new ContainerBuilder();
            b.RegisterInstance(notOwned);
            b.RegisterInstance<IDisposable>(owned).Owned();
            var scope = b.Build("App");
            scope.Resolve<DisposableTransient>();
            scope.Resolve<IDisposable>();

            scope.Dispose();
            Assert.That(notOwned.Disposed, Is.False);
            Assert.That(owned.Disposed, Is.True);
        }

        [Test]
        public void Tickables_AreHookedAtStart_AndUnhookedAtDispose()
        {
            var loop = new FakeLoop();
            var appBuilder = new ContainerBuilder();
            appBuilder.RegisterInstance<ILoopRegistry>(loop);
            using var app = appBuilder.Build("App");

            var level = app.CreateChild("Level", b => b.Register<Counter>(Lifetime.Single));
            level.StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(loop.Tickables.Count, Is.EqualTo(1));

            level.Dispose();
            Assert.That(loop.Tickables, Is.Empty);
        }

        [Test]
        public void Resolve_AfterDispose_Throws()
        {
            var scope = new ContainerBuilder().Build("App");
            scope.Dispose();
            Assert.Throws<ObjectDisposedException>(() => scope.Resolve<Recorder>());
        }

        [Test]
        public void ScopePath_ReflectsTree()
        {
            using var app = new ContainerBuilder().Build("App");
            var game = app.CreateChild("Game", null);
            var level = game.CreateChild("Level", null);
            Assert.That(level.Path, Is.EqualTo("App/Game/Level"));
        }
    }
}
