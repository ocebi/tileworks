using Tileworks.Kit.Audio;
using Tileworks.Kit.Content;
using Tileworks.Kit.Core;
using Tileworks.Kit.Haptics;
using Tileworks.Kit.Injection;
using Tileworks.Kit.Save;
using Tileworks.Kit.Time;
using Tileworks.Kit.UI;
using Tileworks.Kit.Unity;
using UnityEngine;

namespace Tileworks.Kit.App
{
    public sealed class KitOptions
    {
        public bool InMemoryDatabase;
        public string PersistentRoot;

        // Games built from one project share the Editor's persistent folder, so each keeps its saves apart.
        public string SaveFolder = "save";

        // The app scene's music player; without one the app stays silent.
        public IMusic Music;

        // Where scenes and assets come from; without one only the player's scene list is available.
        public IContent Content;
    }

    // Kit services every game gets: persistence with its boot steps and the clock.
    public static class KitInstaller
    {
        public static void Install(ContainerBuilder builder, KitOptions options, ILog log)
        {
            var root = string.IsNullOrEmpty(options.PersistentRoot) ? Application.persistentDataPath : options.PersistentRoot;
            var profile = new ProfileStore(root, options.SaveFolder);
            profile.Resolve();
            var database = new SqliteDatabase(options.InMemoryDatabase
                ? DatabaseOptions.InMemory()
                : new DatabaseOptions { Path = profile.DatabasePath, UseWal = Application.platform != RuntimePlatform.WebGLPlayer }, log);

            builder.RegisterInstance(profile);
            builder.RegisterInstance(database).As<IDatabase>().Owned();
            builder.Register<DatabaseOpenStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<MigrationStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<CacheWarmStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<KitSchema>(Lifetime.Single).As<ISchemaModule>();
            builder.Register<SettingsDao>(Lifetime.Single).AsSelf().As<ICachedDao>();
            builder.Register<KeyValueStore>(Lifetime.Single).As<IKeyValueStore>();
            builder.Register<PlatformMonotonicSource>(Lifetime.Single).As<IMonotonicSource>();
            builder.Register<SystemDeviceTime>(Lifetime.Single).As<IDeviceTime>();
            builder.Register<TrustedClock>(Lifetime.Single).As<IClock>().AsSelf();
            builder.Register<UnityHttpClient>(Lifetime.Single).As<IHttpClient>();
            builder.Register<TimeGuardStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<UserSettings>(Lifetime.Single).As<IUserSettings>().As<IMotionPreference>().AsSelf();
            builder.Register<UserSettingsStep>(Lifetime.Single).As<IBootStep>();
            builder.Register<DeviceHaptics>(Lifetime.Single).As<IHaptics>();
            builder.RegisterInstance(options.Music ?? new SilentMusic());
            builder.RegisterInstance(options.Content ?? new BuildSettingsContent());
            builder.Register<AssetsInitStep>(Lifetime.Single).As<IBootStep>();
        }
    }
}
