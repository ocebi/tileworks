using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Injection
{
    public sealed class Scope : IResolver, IDisposable
    {
        private readonly Registration[] _registrations;
        private readonly Dictionary<Type, List<Registration>> _map;
        private readonly Dictionary<Registration, object> _singles = new Dictionary<Registration, object>(32);
        private readonly List<CreatedInstance> _created = new List<CreatedInstance>(32);
        private readonly List<Scope> _children = new List<Scope>(2);
        private readonly HashSet<Registration> _resolving = new HashSet<Registration>();
        private readonly List<IDisposable> _loopHandles = new List<IDisposable>(4);
        private Action<Exception> _errorSink;

        internal Scope(string name, Scope parent, Registration[] registrations, Dictionary<Type, List<Registration>> map,
            TypePlanCache plans, List<string> warnings)
        {
            Name = name;
            Parent = parent;
            Path = parent == null ? name : parent.Path + "/" + name;
            _registrations = registrations;
            _map = map;
            Plans = plans;
            Warnings = warnings;
            parent?._children.Add(this);
        }

        public string Name { get; }
        public string Path { get; }
        public Scope Parent { get; }
        public IReadOnlyList<Scope> Children => _children;
        public IReadOnlyList<string> Warnings { get; }
        public bool IsStarted { get; private set; }
        public bool IsDisposed { get; private set; }
        public int InstanceCount => _created.Count;
        internal TypePlanCache Plans { get; }

        public Action<Exception> ErrorSink
        {
            get => _errorSink ?? Parent?.ErrorSink;
            set => _errorSink = value;
        }

        public IEnumerable<(Type Type, long CreatedAtTimestamp)> DescribeInstances()
        {
            foreach (var created in _created) yield return (created.Instance.GetType(), created.Timestamp);
        }

        public T Resolve<T>() => (T)Resolve(typeof(T));

        public bool TryResolve<T>(out T value)
        {
            ThrowIfDisposed();
            if (CollectionTypes.TryGetElementType(typeof(T), out _) || TryFind(typeof(T), out _, out _))
            {
                value = Resolve<T>();
                return true;
            }

            value = default;
            return false;
        }

        public object Resolve(Type type)
        {
            ThrowIfDisposed();
            if (CollectionTypes.TryGetElementType(type, out var element)) return ResolveAll(element);
            if (!TryFind(type, out var registration, out var owner))
                throw new InvalidOperationException($"{CompositionValidator.Name(type)} is not registered in scope '{Path}'.");
            return owner.GetOrCreate(registration);
        }

        public Scope CreateChild(string name, Action<ContainerBuilder> configure)
        {
            ThrowIfDisposed();
            var builder = new ContainerBuilder();
            configure?.Invoke(builder);
            return builder.Build(name, this);
        }

        // Initializes in creation order, which puts dependencies first.
        public async UniTask StartAsync(CancellationToken ct)
        {
            ThrowIfDisposed();
            if (IsStarted) throw new InvalidOperationException($"Scope '{Path}' was already started.");
            IsStarted = true;

            foreach (var registration in _registrations)
            {
                var eager = registration.IsEntryPoint ||
                            (registration.Lifetime == Lifetime.Single && registration.Kind != RegistrationKind.Factory &&
                             CompositionValidator.ImplementsLoopInterface(registration.ImplementationType));
                if (eager) GetOrCreate(registration);
            }

            var started = new List<object>(_created.Count);
            foreach (var created in _created)
            {
                if (_singles.ContainsValue(created.Instance)) started.Add(created.Instance);
            }

            foreach (var instance in started)
            {
                if (instance is IInitializable initializable) initializable.Initialize();
            }

            foreach (var instance in started)
            {
                ct.ThrowIfCancellationRequested();
                if (instance is IAsyncInitializable asyncInitializable) await asyncInitializable.InitializeAsync(ct);
            }

            HookLoop(started);
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            List<Exception> errors = null;

            for (var i = _children.Count - 1; i >= 0; i--)
            {
                try
                {
                    _children[i].Dispose();
                }
                catch (Exception e)
                {
                    (errors ??= new List<Exception>()).Add(e);
                }
            }

            for (var i = _loopHandles.Count - 1; i >= 0; i--) _loopHandles[i].Dispose();
            _loopHandles.Clear();

            for (var i = _created.Count - 1; i >= 0; i--)
            {
                var created = _created[i];
                if (!created.Owned || !(created.Instance is IDisposable disposable)) continue;
                try
                {
                    disposable.Dispose();
                }
                catch (Exception e)
                {
                    (errors ??= new List<Exception>()).Add(e);
                }
            }

            _created.Clear();
            _singles.Clear();
            Parent?._children.Remove(this);

            if (errors == null) return;
            var sink = ErrorSink;
            if (sink == null) throw new AggregateException($"Errors while disposing scope '{Path}'.", errors);
            foreach (var error in errors) sink(error);
        }

        public override string ToString() => $"Scope({Path}, {_created.Count} instances)";

        internal List<Registration> LocalRegistrations(Type service) => _map.TryGetValue(service, out var list) ? list : null;

        private bool TryFind(Type service, out Registration registration, out Scope owner)
        {
            for (var scope = this; scope != null; scope = scope.Parent)
            {
                if (!scope._map.TryGetValue(service, out var list)) continue;
                if (list.Count > 1)
                    throw new InvalidOperationException(
                        $"{CompositionValidator.Name(service)} has {list.Count} registrations in scope '{scope.Path}'; resolve IReadOnlyList<> instead.");
                registration = list[0];
                owner = scope;
                return true;
            }

            registration = null;
            owner = null;
            return false;
        }

        // Nearest scope first, then registration order within each scope.
        private Array ResolveAll(Type element)
        {
            var count = 0;
            for (var scope = this; scope != null; scope = scope.Parent)
            {
                var list = scope.LocalRegistrations(element);
                if (list != null) count += list.Count;
            }

            var result = Array.CreateInstance(element, count);
            var index = 0;
            for (var scope = this; scope != null; scope = scope.Parent)
            {
                var list = scope.LocalRegistrations(element);
                if (list == null) continue;
                foreach (var registration in list) result.SetValue(scope.GetOrCreate(registration), index++);
            }

            return result;
        }

        private object GetOrCreate(Registration registration)
        {
            ThrowIfDisposed();
            if (registration.Lifetime == Lifetime.Single && _singles.TryGetValue(registration, out var existing)) return existing;

            var instance = Create(registration);
            if (registration.Lifetime == Lifetime.Single) _singles[registration] = instance;
            return instance;
        }

        private object Create(Registration registration)
        {
            if (!_resolving.Add(registration))
                throw new InvalidOperationException($"Cycle detected while creating {registration.ImplementationType.Name} in '{Path}'.");

            try
            {
                object instance;
                switch (registration.Kind)
                {
                    case RegistrationKind.Instance:
                        instance = registration.Instance;
                        break;
                    case RegistrationKind.Factory:
                        instance = registration.Factory(this) ??
                                   throw new InvalidOperationException($"Factory for {registration.ImplementationType.Name} returned null.");
                        break;
                    default:
                        instance = Activate(registration.ImplementationType);
                        break;
                }

                var owned = registration.Kind != RegistrationKind.Instance || registration.IsOwned;
                _created.Add(new CreatedInstance(instance, owned, Stopwatch.GetTimestamp()));
                return instance;
            }
            finally
            {
                _resolving.Remove(registration);
            }
        }

        private object Activate(Type type)
        {
            var plan = Plans.Get(type);
            if (!plan.IsValid) throw new InvalidOperationException($"{type.Name} needs exactly one public constructor.");

            var args = plan.ParameterTypes.Length == 0 ? Array.Empty<object>() : new object[plan.ParameterTypes.Length];
            for (var i = 0; i < args.Length; i++) args[i] = Resolve(plan.ParameterTypes[i]);

            try
            {
                return plan.Constructor.Invoke(args);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        private void HookLoop(List<object> instances)
        {
            ILoopRegistry loop = null;
            foreach (var instance in instances)
            {
                var tickable = instance as ITickable;
                var lateTickable = instance as ILateTickable;
                var listener = instance as IAppLifecycleListener;
                if (tickable == null && lateTickable == null && listener == null) continue;
                if (instance is ILoopRegistry) continue;

                if (loop == null && !TryResolve(out loop))
                    throw new InvalidOperationException(
                        $"{instance.GetType().Name} needs ticking but no ILoopRegistry is registered above '{Path}'.");

                if (tickable != null) _loopHandles.Add(loop.AddTickable(tickable));
                if (lateTickable != null) _loopHandles.Add(loop.AddLateTickable(lateTickable));
                if (listener != null) _loopHandles.Add(loop.AddLifecycleListener(listener));
            }
        }

        private void ThrowIfDisposed()
        {
            if (IsDisposed) throw new ObjectDisposedException($"Scope '{Path}'");
        }

        private readonly struct CreatedInstance
        {
            public readonly object Instance;
            public readonly bool Owned;
            public readonly long Timestamp;

            public CreatedInstance(object instance, bool owned, long timestamp)
            {
                Instance = instance;
                Owned = owned;
                Timestamp = timestamp;
            }
        }
    }
}
