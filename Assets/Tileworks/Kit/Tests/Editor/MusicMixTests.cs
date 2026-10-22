using NUnit.Framework;
using Tileworks.Kit.Audio;

namespace Tileworks.Kit.Tests
{
    public sealed class MusicMixTests
    {
        [Test]
        public void TheSameTrack_DoesNotRestart()
        {
            var mix = new MusicMix();
            Assert.That(mix.PlayLoop(MusicTrack.Home, 0.5f), Is.True);
            mix.Tick(0.2f);

            Assert.That(mix.PlayLoop(MusicTrack.Home, 0.5f), Is.False);
            Assert.That(mix.LevelOf(mix.Live), Is.EqualTo(0.4f).Within(1e-5));
        }

        [Test]
        public void ANewTrack_CrossfadesOnTheOtherChannel()
        {
            var mix = new MusicMix();
            mix.PlayLoop(MusicTrack.Home, 0.5f);
            mix.Tick(1f);
            var home = mix.Live;

            Assert.That(mix.PlayLoop(MusicTrack.Game1, 0.5f), Is.True);
            Assert.That(mix.Live, Is.Not.EqualTo(home));
            mix.Tick(0.25f);
            Assert.That(mix.LevelOf(home), Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(mix.LevelOf(mix.Live), Is.EqualTo(0.5f).Within(1e-5));
            Assert.That(mix.Faded(home), Is.False);
            mix.Tick(0.25f);
            Assert.That(mix.Faded(home), Is.True);
            Assert.That(mix.LevelOf(mix.Live), Is.EqualTo(1f));
        }

        [Test]
        public void AfterAJingle_TheSameLoopFadesBackIn()
        {
            var mix = new MusicMix();
            mix.PlayLoop(MusicTrack.Game2, 0.5f);
            mix.Tick(1f);

            mix.StopLoop(0.1f);
            mix.Tick(0.1f);
            Assert.That(mix.Faded(mix.Live), Is.True);
            Assert.That(mix.PlayLoop(MusicTrack.Game2, 0.5f), Is.True);
        }

        [Test]
        public void ADuck_DipsAndRecovers()
        {
            var mix = new MusicMix();
            mix.PlayLoop(MusicTrack.Home, 0f);
            mix.StartDuck(0.3f, 1f, 0.1f);

            mix.Tick(0.1f);
            Assert.That(mix.Duck, Is.EqualTo(0.3f).Within(1e-5));
            mix.Tick(0.9f);
            mix.Tick(0.1f);
            Assert.That(mix.Duck, Is.EqualTo(1f).Within(1e-5));
        }

        [Test]
        public void ZeroFades_JumpWithoutNaN()
        {
            var mix = new MusicMix();
            mix.PlayLoop(MusicTrack.Home, 0f);
            mix.Tick(0f);
            Assert.That(mix.LevelOf(mix.Live), Is.EqualTo(0f));
            mix.Tick(0.016f);
            Assert.That(mix.LevelOf(mix.Live), Is.EqualTo(1f));
        }

        [Test]
        public void HardLevels_GetTheHardTrack_OthersAlternate()
        {
            Assert.That(MusicTracks.ForLevel(7, true), Is.EqualTo(MusicTrack.Hard));
            Assert.That(MusicTracks.ForLevel(1, false), Is.EqualTo(MusicTrack.Game1));
            Assert.That(MusicTracks.ForLevel(2, false), Is.EqualTo(MusicTrack.Game2));
        }
    }
}
