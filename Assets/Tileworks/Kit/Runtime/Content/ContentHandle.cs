using System;

namespace Tileworks.Kit.Content
{
    // A loaded asset and the right to release it. Dispose once the asset is no longer in use; disposing twice is harmless.
    public abstract class ContentHandle<T> : IDisposable where T : UnityEngine.Object
    {
        protected ContentHandle(T asset) => Asset = asset;

        public T Asset { get; }

        public abstract void Dispose();
    }
}
