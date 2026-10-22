using Tileworks.Kit.App;
using Tileworks.Kit.Content.Addressables;
using Tileworks.Kit.Injection;
using UnityEngine;

namespace Hub.Installer
{
    // Composition root of the hub's App scope: the kit services and one Home state that shows the games.
    [CreateAssetMenu(menuName = "Tileworks/Hub/App Installer")]
    public sealed class HubAppInstaller : AppInstaller
    {
        public const string HubScene = "Hub";

        public override void ConfigureKit(KitOptions options)
        {
            options.SaveFolder = "save-hub";
            options.Content = new AddressablesContent();
        }

        public override void Install(ContainerBuilder builder) =>
            builder.RegisterFactory<IAppState>(r => new SceneState(AppStates.Home, HubScene, r.Resolve<ISceneLoader>()), Lifetime.Single);
    }
}
