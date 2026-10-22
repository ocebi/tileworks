using System;
using System.Collections.Generic;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Injection
{
    internal enum RegistrationKind : byte
    {
        Type,
        Instance,
        Factory,
    }

    public sealed class Registration
    {
        private readonly List<Type> _services = new List<Type>(2);
        private bool _frozen;

        internal Registration(RegistrationKind kind, Type implementationType, Lifetime lifetime, int order,
            object instance = null, Func<IResolver, object> factory = null)
        {
            Kind = kind;
            ImplementationType = implementationType;
            Lifetime = lifetime;
            Order = order;
            Instance = instance;
            Factory = factory;
        }

        internal RegistrationKind Kind { get; }

        public Type ImplementationType { get; }

        public Lifetime Lifetime { get; }
        public IReadOnlyList<Type> Services => _services;
        internal int Order { get; }
        internal object Instance { get; }
        internal Func<IResolver, object> Factory { get; }
        internal bool IsEntryPoint { get; set; }

        internal bool IsOwned { get; private set; }

        public Registration As<TService>() => As(typeof(TService));

        public Registration As(Type service)
        {
            EnsureMutable();
            if (!service.IsAssignableFrom(ImplementationType))
                throw new ArgumentException($"{ImplementationType.Name} does not implement {service.Name}.");
            if (!_services.Contains(service)) _services.Add(service);
            return this;
        }

        public Registration AsSelf() => As(ImplementationType);

        // Skips IDisposable and lifecycle interfaces.
        public Registration AsImplementedInterfaces()
        {
            foreach (var type in ImplementationType.GetInterfaces())
            {
                if (IsInfrastructureInterface(type)) continue;
                As(type);
            }

            return this;
        }

        public bool Provides(Type service) =>
            _services.Count == 0 ? ImplementationType == service : _services.IndexOf(service) >= 0;

        public Registration Owned()
        {
            EnsureMutable();
            IsOwned = true;
            return this;
        }

        internal void Freeze()
        {
            if (_services.Count == 0) _services.Add(ImplementationType);
            _frozen = true;
        }

        public override string ToString() => $"{ImplementationType.Name} ({Lifetime}, {Kind})";

        internal static bool IsInfrastructureInterface(Type type) =>
            type == typeof(IDisposable)
            || type == typeof(IInitializable)
            || type == typeof(IAsyncInitializable)
            || type == typeof(ITickable)
            || type == typeof(ILateTickable)
            || type == typeof(IAppLifecycleListener);

        private void EnsureMutable()
        {
            if (_frozen) throw new InvalidOperationException("Registration cannot change after Build.");
        }
    }
}
