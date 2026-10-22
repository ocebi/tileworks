using Tileworks.Kit.Injection;
using UnityEngine;

namespace Tileworks.Kit.App
{
    public abstract class AppInstaller : ScriptableObject
    {
        public abstract void Install(ContainerBuilder builder);

        public virtual void ConfigureKit(KitOptions options)
        {
        }
    }

    // Lives in a scene so it can register that scene's passive views.
    public abstract class SceneInstaller : MonoBehaviour
    {
        public abstract void Install(ContainerBuilder builder);
    }
}
