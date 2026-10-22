using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Meta.Boosters;

namespace Tileworks.Meta.Installer
{
    // The Hammer and Shuffle boosters Blast and Gem Swap share.
    public static class BoosterInstaller
    {
        public static void Install(ContainerBuilder builder)
        {
            builder.Register<BoosterService>(Lifetime.Single);
            builder.Register<BoosterStarterStep>(Lifetime.Single).As<IBootStep>();
        }
    }
}
