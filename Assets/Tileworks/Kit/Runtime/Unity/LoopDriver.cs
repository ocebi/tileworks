using UnityEngine;

namespace Tileworks.Kit.Unity
{
    [DisallowMultipleComponent]
    public sealed class LoopDriver : MonoBehaviour
    {
        private UpdateLoop _loop;

        public void Bind(UpdateLoop loop)
        {
            _loop = loop;
            Application.lowMemory -= OnLowMemory;
            Application.lowMemory += OnLowMemory;
        }

        private void Update() => _loop?.Tick();
        private void LateUpdate() => _loop?.LateTick();
        private void OnApplicationPause(bool paused) => _loop?.Pause(paused);
        private void OnApplicationFocus(bool focused) => _loop?.Focus(focused);
        private void OnApplicationQuit() => _loop?.Quit();
        private void OnLowMemory() => _loop?.LowMemory();
        private void OnDestroy() => Application.lowMemory -= OnLowMemory;
    }
}
