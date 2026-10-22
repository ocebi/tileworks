using Hub.Controller;
using Hub.View;
using Tileworks.Kit.App;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Unity;
using UnityEngine;

namespace Hub.Installer
{
    public sealed class HubSceneInstaller : SceneInstaller
    {
        [SerializeField, ReadOnlyInInspector] private HubView _view;

        public override void Install(ContainerBuilder builder)
        {
            builder.RegisterInstance<IHubView>(_view);
            builder.Register<HubPresenter>(Lifetime.Single).As<ISceneEntry>();
        }
    }
}
