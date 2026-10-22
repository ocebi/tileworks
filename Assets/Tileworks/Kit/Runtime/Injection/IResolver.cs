using System;

namespace Tileworks.Kit.Injection
{
    // Installers and factories only; anywhere else it is a service locator.
    public interface IResolver
    {
        T Resolve<T>();
        bool TryResolve<T>(out T value);
        object Resolve(Type type);
    }
}
