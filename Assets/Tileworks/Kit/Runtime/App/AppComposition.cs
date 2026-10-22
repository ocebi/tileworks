using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Input;
using Tileworks.Kit.Unity;

namespace Tileworks.Kit.App
{
    // App-scope registrations every game shares; AppRoot and the composition tests both use it.
    public static class AppComposition
    {
        public static void RegisterCore(ContainerBuilder builder, ILog log, ILoopRegistry loop, ISceneLoader scenes, IAppRestarter restarter,
            IPointerSurface pointer, IGameSwitcher games)
        {
            builder.RegisterInstance(log);
            builder.RegisterInstance(loop);
            builder.RegisterInstance(restarter);
            builder.RegisterInstance(scenes);
            builder.RegisterInstance(pointer);
            builder.RegisterInstance(games);
            builder.RegisterInstance(new TaskScope(log)).Owned();
            builder.Register<MessageBus>(Lifetime.Single).As<IMessageBus>();
            builder.Register<InputGate>(Lifetime.Single).As<IInputGate>();
            builder.Register<GestureSource>(Lifetime.Single).As<IGestureSource>();
            builder.Register<BootPipeline>(Lifetime.Single);
            builder.Register<AppNavigator>(Lifetime.Single).As<IAppNavigator>().AsSelf();
            builder.Register<AppStateMachine>(Lifetime.Single).As<IAppStateMachine>().AsSelf();
            builder.Register<BootState>(Lifetime.Single).As<IAppState>();
        }
    }
}
