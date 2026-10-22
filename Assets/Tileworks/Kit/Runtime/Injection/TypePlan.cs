using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tileworks.Kit.Injection
{
    internal sealed class TypePlan
    {
        public TypePlan(Type type, ConstructorInfo constructor, Type[] parameterTypes, string[] parameterNames, int publicConstructorCount)
        {
            Type = type;
            Constructor = constructor;
            ParameterTypes = parameterTypes;
            ParameterNames = parameterNames;
            PublicConstructorCount = publicConstructorCount;
        }

        public Type Type { get; }

        public ConstructorInfo Constructor { get; }

        public Type[] ParameterTypes { get; }
        public string[] ParameterNames { get; }
        public int PublicConstructorCount { get; }
        public bool IsValid => Constructor != null;
    }

    internal sealed class TypePlanCache
    {
        private readonly Dictionary<Type, TypePlan> _plans = new Dictionary<Type, TypePlan>(128);

        public TypePlan Get(Type type)
        {
            if (_plans.TryGetValue(type, out var plan)) return plan;

            var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (constructors.Length != 1 || type.IsAbstract)
            {
                plan = new TypePlan(type, null, Array.Empty<Type>(), Array.Empty<string>(), type.IsAbstract ? 0 : constructors.Length);
            }
            else
            {
                var parameters = constructors[0].GetParameters();
                var types = new Type[parameters.Length];
                var names = new string[parameters.Length];
                for (var i = 0; i < parameters.Length; i++)
                {
                    types[i] = parameters[i].ParameterType;
                    names[i] = parameters[i].Name;
                }

                plan = new TypePlan(type, constructors[0], types, names, 1);
            }

            _plans.Add(type, plan);
            return plan;
        }
    }

    internal static class CollectionTypes
    {
        public static bool TryGetElementType(Type type, out Type element)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                element = type.GetGenericArguments()[0];
                return true;
            }

            element = null;
            return false;
        }
    }
}
