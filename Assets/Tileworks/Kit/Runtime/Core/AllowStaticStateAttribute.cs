using System;

namespace Tileworks.Kit.Core
{
    // Exempts a static mutable field from the architecture test.
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AllowStaticStateAttribute : Attribute
    {
        public AllowStaticStateAttribute(string reason) => Reason = reason;

        public string Reason { get; }
    }
}
