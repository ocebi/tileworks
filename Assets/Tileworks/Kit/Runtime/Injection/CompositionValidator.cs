using System;
using System.Collections.Generic;
using System.Text;
using Tileworks.Kit.Core;

namespace Tileworks.Kit.Injection
{
    internal static class CompositionValidator
    {
        public static List<string> Validate(string scopeName, IReadOnlyList<Registration> registrations,
            Dictionary<Type, List<Registration>> map, Scope parent, TypePlanCache plans, List<string> warnings)
        {
            var problems = new List<string>();

            foreach (var registration in registrations)
            {
                if (registration.Kind != RegistrationKind.Type) continue;

                var plan = plans.Get(registration.ImplementationType);
                if (!plan.IsValid)
                {
                    problems.Add($"{Name(registration.ImplementationType)} has {plan.PublicConstructorCount} public constructors; exactly one is required.");
                    continue;
                }

                for (var i = 0; i < plan.ParameterTypes.Length; i++)
                {
                    var dependency = plan.ParameterTypes[i];
                    if (CollectionTypes.TryGetElementType(dependency, out _)) continue;

                    var count = CountNearest(dependency, map, parent, out var nearest, out var nearestScope);
                    if (count == 0)
                    {
                        problems.Add($"{Name(registration.ImplementationType)} → {Name(dependency)} {plan.ParameterNames[i]} → ? " +
                                     $"(not registered in {Chain(scopeName, parent)})");
                        continue;
                    }

                    if (count > 1)
                    {
                        problems.Add($"{Name(registration.ImplementationType)} → {Name(dependency)} is ambiguous: " +
                                     $"{count} registrations in scope '{nearestScope}'. Inject IReadOnlyList<{Name(dependency)}> or register one.");
                        continue;
                    }

                    if (registration.Lifetime == Lifetime.Single && nearest.Lifetime == Lifetime.Transient &&
                        typeof(IDisposable).IsAssignableFrom(nearest.ImplementationType))
                    {
                        warnings.Add($"Single {Name(registration.ImplementationType)} captures transient disposable " +
                                     $"{Name(nearest.ImplementationType)}; it will live as long as the single.");
                    }
                }

                if (registration.Lifetime == Lifetime.Transient && ImplementsLoopInterface(registration.ImplementationType))
                {
                    warnings.Add($"Transient {Name(registration.ImplementationType)} implements a lifecycle interface; " +
                                 "only singles are started and ticked.");
                }
            }

            FindCycles(registrations, map, plans, problems);
            return problems;
        }

        internal static bool ImplementsLoopInterface(Type type) =>
            typeof(IInitializable).IsAssignableFrom(type) ||
            typeof(IAsyncInitializable).IsAssignableFrom(type) ||
            typeof(ITickable).IsAssignableFrom(type) ||
            typeof(ILateTickable).IsAssignableFrom(type) ||
            typeof(IAppLifecycleListener).IsAssignableFrom(type);

        private static int CountNearest(Type service, Dictionary<Type, List<Registration>> map, Scope parent,
            out Registration nearest, out string nearestScope)
        {
            if (map.TryGetValue(service, out var local))
            {
                nearest = local[0];
                nearestScope = "this";
                return local.Count;
            }

            for (var scope = parent; scope != null; scope = scope.Parent)
            {
                var list = scope.LocalRegistrations(service);
                if (list == null) continue;
                nearest = list[0];
                nearestScope = scope.Name;
                return list.Count;
            }

            nearest = null;
            nearestScope = null;
            return 0;
        }

        // Parents never see children, so only local edges can form a cycle.
        private static void FindCycles(IReadOnlyList<Registration> registrations, Dictionary<Type, List<Registration>> map,
            TypePlanCache plans, List<string> problems)
        {
            var state = new Dictionary<Registration, byte>(registrations.Count); // 1 = visiting, 2 = done
            var path = new List<Registration>();
            var reported = new HashSet<string>();

            foreach (var registration in registrations) Visit(registration);

            void Visit(Registration node)
            {
                if (state.TryGetValue(node, out var s))
                {
                    if (s == 1) ReportCycle(node);
                    return;
                }

                state[node] = 1;
                path.Add(node);
                if (node.Kind == RegistrationKind.Type)
                {
                    var plan = plans.Get(node.ImplementationType);
                    if (plan.IsValid)
                    {
                        foreach (var dependency in plan.ParameterTypes)
                        {
                            var service = CollectionTypes.TryGetElementType(dependency, out var element) ? element : dependency;
                            if (!map.TryGetValue(service, out var targets)) continue;
                            foreach (var target in targets) Visit(target);
                        }
                    }
                }

                path.RemoveAt(path.Count - 1);
                state[node] = 2;
            }

            void ReportCycle(Registration start)
            {
                var index = path.IndexOf(start);
                var text = new StringBuilder("Cycle: ");
                for (var i = index; i < path.Count; i++) text.Append(Name(path[i].ImplementationType)).Append(" → ");
                text.Append(Name(start.ImplementationType));
                var message = text.ToString();
                if (reported.Add(message)) problems.Add(message);
            }
        }

        private static string Chain(string scopeName, Scope parent)
        {
            var text = new StringBuilder(scopeName);
            for (var scope = parent; scope != null; scope = scope.Parent) text.Append(" or ").Append(scope.Name);
            return text.ToString();
        }

        internal static string Name(Type type)
        {
            if (!type.IsGenericType) return type.Name;
            var name = type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0) name = name.Substring(0, tick);
            var args = type.GetGenericArguments();
            var parts = new string[args.Length];
            for (var i = 0; i < args.Length; i++) parts[i] = Name(args[i]);
            return $"{name}<{string.Join(", ", parts)}>";
        }
    }
}
