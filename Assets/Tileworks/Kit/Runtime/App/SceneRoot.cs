using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using UnityEngine;

namespace Tileworks.Kit.App
{
    [DisallowMultipleComponent]
    public sealed class SceneRoot : MonoBehaviour
    {
        [SerializeField, ReadOnlyInInspector] private SceneInstaller[] _installers = new SceneInstaller[0];
        [SerializeField, ReadOnlyInInspector] private string _bootScene;
        [SerializeField, ReadOnlyInInspector] private UiMotion _uiMotion;

#if UNITY_EDITOR
        // Played on its own in the Editor, a game scene has no app to build its scope from, so it boots the app. Scenes
        // are content, not in the player's scene list, so the Editor loads the boot scene by path.
        private void Awake()
        {
            if (UnityEngine.SceneManagement.SceneManager.sceneCount != 1) return;
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(_bootScene,
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        }
#endif

        // The scene's UI motion learns the player's preference before any presenter can ask a view to animate. The scene
        // gets its own task scope, so async work started by its presenters ends with the scene.
        public Scope BuildScope(Scope parent, string name)
        {
            if (_uiMotion != null && parent.TryResolve<IMotionPreference>(out var preference)) _uiMotion.Bind(preference);
            return parent.CreateChild(name, builder =>
            {
                builder.RegisterInstance(parent.TryResolve<TaskScope>(out var tasks) ? tasks.CreateChild() : new TaskScope(parent.Resolve<ILog>())).Owned();
                foreach (var installer in _installers)
                {
                    if (installer != null) installer.Install(builder);
                }
            });
        }
    }
}
