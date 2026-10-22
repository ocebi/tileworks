using System;
using System.Collections.Generic;

namespace Tileworks.Kit.Injection
{
    public sealed class ContainerBuilder
    {
        private readonly List<Registration> _registrations = new List<Registration>(32);
        private bool _built;

        public IReadOnlyList<Registration> Registrations => _registrations;

        public Registration Register<TImpl>(Lifetime lifetime) where TImpl : class => Register(typeof(TImpl), lifetime);

        public Registration Register(Type implementationType, Lifetime lifetime)
        {
            EnsureNotBuilt();
            if (implementationType.IsAbstract || implementationType.IsInterface)
                throw new ArgumentException($"{implementationType.Name} is abstract; register a concrete type.");
            var registration = new Registration(RegistrationKind.Type, implementationType, lifetime, _registrations.Count);
            _registrations.Add(registration);
            return registration;
        }

        // Disposed with the scope only when marked Owned().
        public Registration RegisterInstance<T>(T instance) where T : class
        {
            EnsureNotBuilt();
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            var registration = new Registration(RegistrationKind.Instance, typeof(T), Lifetime.Single, _registrations.Count, instance);
            registration.As(typeof(T));
            _registrations.Add(registration);
            return registration;
        }

        public Registration RegisterFactory<T>(Func<IResolver, T> create, Lifetime lifetime) where T : class
        {
            EnsureNotBuilt();
            if (create == null) throw new ArgumentNullException(nameof(create));
            var registration = new Registration(RegistrationKind.Factory, typeof(T), lifetime, _registrations.Count, factory: create);
            registration.As(typeof(T));
            _registrations.Add(registration);
            return registration;
        }

        public Registration RegisterEntryPoint<TImpl>() where TImpl : class
        {
            var registration = Register(typeof(TImpl), Lifetime.Single);
            registration.IsEntryPoint = true;
            return registration;
        }

        public bool IsRegistered<TService>()
        {
            foreach (var registration in _registrations)
            {
                if (registration.Provides(typeof(TService))) return true;
            }

            return false;
        }

        public Scope Build(string name, Scope parent = null)
        {
            EnsureNotBuilt();
            _built = true;
            if (parent != null && parent.IsDisposed) throw new ObjectDisposedException(parent.Name);

            var map = new Dictionary<Type, List<Registration>>(_registrations.Count * 2);
            foreach (var registration in _registrations)
            {
                registration.Freeze();
                foreach (var service in registration.Services)
                {
                    if (!map.TryGetValue(service, out var list))
                    {
                        list = new List<Registration>(1);
                        map.Add(service, list);
                    }

                    list.Add(registration);
                }
            }

            var plans = parent != null ? parent.Plans : new TypePlanCache();
            var warnings = new List<string>();
            var problems = CompositionValidator.Validate(name, _registrations, map, parent, plans, warnings);
            if (problems.Count > 0) throw new CompositionException(name, problems);

            return new Scope(name, parent, _registrations.ToArray(), map, plans, warnings);
        }

        private void EnsureNotBuilt()
        {
            if (_built) throw new InvalidOperationException("This builder has already built a scope.");
        }
    }
}
