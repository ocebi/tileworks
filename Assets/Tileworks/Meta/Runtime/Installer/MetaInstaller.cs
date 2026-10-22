using Tileworks.Kit.Core;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Save;
using Tileworks.Meta.Api;
using Tileworks.Meta.Config;
using Tileworks.Meta.Economy;

namespace Tileworks.Meta.Installer
{
    public static class MetaInstaller
    {
        public static void Install(ContainerBuilder builder, string configJson)
        {
            builder.RegisterInstance<IConfigSource>(new JsonConfigSource(configJson));
            builder.Register<ConfigService>(Lifetime.Single).As<IConfig>().AsSelf();
            builder.Register<EconomySchema>(Lifetime.Single).As<ISchemaModule>();
            builder.Register<WalletDao>(Lifetime.Single);
            builder.Register<TransactionLogDao>(Lifetime.Single);
            builder.Register<GrantDao>(Lifetime.Single);
            builder.Register<LivesDao>(Lifetime.Single);
            builder.Register<EconomyService>(Lifetime.Single).As<IEconomy>().AsSelf();
            builder.Register<LivesService>(Lifetime.Single).As<ILives>().AsSelf();
            builder.Register<MetaLoadStep>(Lifetime.Single).As<IBootStep>();
        }
    }
}
