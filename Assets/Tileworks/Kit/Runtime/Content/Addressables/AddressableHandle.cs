using UnityEngine.ResourceManagement.AsyncOperations;
using Addr = UnityEngine.AddressableAssets.Addressables;

namespace Tileworks.Kit.Content.Addressables
{
    public sealed class AddressableHandle<T> : ContentHandle<T> where T : UnityEngine.Object
    {
        private AsyncOperationHandle<T> _handle;
        private bool _released;

        public AddressableHandle(AsyncOperationHandle<T> handle) : base(handle.Result) => _handle = handle;

        public override void Dispose()
        {
            if (_released) return;
            _released = true;
            Addr.Release(_handle);
            _handle = default;
        }
    }
}
