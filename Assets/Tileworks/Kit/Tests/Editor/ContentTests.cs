using System;
using System.Threading;
using NUnit.Framework;
using Tileworks.Kit.App;
using Tileworks.Kit.Content;
using UnityEngine;

namespace Tileworks.Kit.Tests
{
    public sealed class ContentTests
    {
        [Test]
        public void SceneKey_IsTheSceneName_ForNamesAndPaths()
        {
            Assert.That(SceneKey.Of("Home"), Is.EqualTo("Home"));
            Assert.That(SceneKey.Of("Assets/Games/TileMatch/Scenes/App.unity"), Is.EqualTo("App"));
            Assert.That(SceneKey.Of(null), Is.Null);
        }

        [Test]
        public void AssetsInitStep_IsCritical_AndInitialisesTheContent()
        {
            var content = new FakeContent();
            var step = new AssetsInitStep(content);

            step.RunAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(step.Critical, Is.True);
            Assert.That(step.DependsOn, Is.Empty);
            Assert.That(content.Initializations, Is.EqualTo(1));
        }

        [Test]
        public void AHandle_ReleasesOnce()
        {
            var content = new FakeContent();
            var clip = AudioClip.Create("clip", 8, 1, 8000, false);
            content.Assets["clip"] = clip;

            var handle = content.LoadAsync<AudioClip>("clip", CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(handle.Asset, Is.SameAs(clip));
            Assert.That(content.LiveHandles, Is.EqualTo(1));

            handle.Dispose();
            handle.Dispose();
            Assert.That(content.LiveHandles, Is.Zero);
            UnityEngine.Object.DestroyImmediate(clip);
        }

        [Test]
        public void BuildSettingsContent_KnowsNoAssets_AndNothingToDownload()
        {
            var content = new BuildSettingsContent();
            Assert.That(content.Exists("NoSuchScene"), Is.False);
            Assert.That(content.DownloadSizeAsync("Any", CancellationToken.None).GetAwaiter().GetResult(), Is.Zero);
            Assert.Throws<NotSupportedException>(() => content.LoadAsync<AudioClip>("clip", CancellationToken.None).GetAwaiter().GetResult());
        }
    }
}
