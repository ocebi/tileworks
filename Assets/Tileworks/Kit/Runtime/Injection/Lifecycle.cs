namespace Tileworks.Kit.Injection
{
    public enum Lifetime : byte
    {
        Single, // one instance per registering scope

        Transient,
    }
}
