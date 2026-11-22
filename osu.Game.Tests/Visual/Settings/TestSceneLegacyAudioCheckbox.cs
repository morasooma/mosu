using System;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.Audio;

namespace osu.Game.Tests.Visual.Settings
{
    [HeadlessTest]
    public partial class TestSceneLegacyAudioCheckbox : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private LegacyAudioCheckbox checkbox = null!;
        private TestAudioManager testAudio = null!;

        [TestCase(false)]
        [TestCase(true)]
        public void TestInitialValue(bool experimental)
        {
            AddStep("create checkbox", () => createCheckbox(experimental));
            AddUntilStep("checkbox loaded", () => checkbox.IsLoaded);
            AddUntilStep("initial value inverted", () => checkbox.Current.Value == !experimental);
        }

        [Test]
        public void TestUserChangesUpdateAudioConfiguration()
        {
            AddStep("create checkbox", () => createCheckbox(true));
            AddUntilStep("experimental audio selected", () => checkbox.IsLoaded && !checkbox.Current.Value);
            AddStep("enable legacy audio", () => checkbox.Current.Value = true);
            AddAssert("experimental audio disabled", () => !testAudio.UseExperimentalWasapi.Value);
            AddStep("disable legacy audio", () => checkbox.Current.Value = false);
            AddAssert("experimental audio enabled", () => testAudio.UseExperimentalWasapi.Value);
        }

        [Test]
        public void TestBackendFallbackUpdatesCheckboxOnUpdateThread()
        {
            bool changedOnUpdateThread = false;

            AddStep("create checkbox", () => createCheckbox(true));
            AddUntilStep("experimental audio selected", () => checkbox.IsLoaded && !checkbox.Current.Value);
            AddStep("observe UI change thread", () => checkbox.Current.BindValueChanged(_ => changedOnUpdateThread = ThreadSafety.IsUpdateThread));
            AddStep("simulate backend disabling WASAPI off update thread", () => Task.Run(() =>
            {
                Assert.That(ThreadSafety.IsUpdateThread, Is.False);
                testAudio.UseExperimentalWasapi.Value = false;
            }).GetAwaiter().GetResult());
            AddUntilStep("legacy audio selected", () => checkbox.Current.Value);
            AddAssert("UI notification ran on update thread", () => changedOnUpdateThread);
            AddAssert("fallback setting retained", () => !testAudio.UseExperimentalWasapi.Value);
        }

        [Test]
        public void TestRapidBackendChangesUseLatestValue()
        {
            AddStep("create checkbox", () => createCheckbox(true));
            AddUntilStep("experimental audio selected", () => checkbox.IsLoaded && !checkbox.Current.Value);
            AddStep("change backend repeatedly off update thread", () => Task.Run(() =>
            {
                testAudio.UseExperimentalWasapi.Value = false;
                testAudio.UseExperimentalWasapi.Value = true;
                testAudio.UseExperimentalWasapi.Value = false;
            }).GetAwaiter().GetResult());
            AddUntilStep("latest value applied", () => checkbox.Current.Value);
            AddAssert("latest backend value retained", () => !testAudio.UseExperimentalWasapi.Value);
        }

        [Test]
        public void TestBackendUpdateDoesNotWriteBackAConcurrentConfigurationChange()
        {
            var concurrentListener = new Bindable<bool>();
            AddStep("create checkbox", () => createCheckbox(true));
            AddUntilStep("experimental audio selected", () => checkbox.IsLoaded && !checkbox.Current.Value);
            AddStep("simulate backend recovery while UI receives fallback", () =>
            {
                // Bound copies receive propagation before Current's own listeners.
                concurrentListener.BindTo(checkbox.Current);
                concurrentListener.BindValueChanged(change =>
                {
                    if (change.NewValue)
                        testAudio.UseExperimentalWasapi.Value = true;
                });
                testAudio.UseExperimentalWasapi.Value = false;
            });
            AddWaitStep("allow UI configuration synchronisation", 3);
            AddAssert("newer backend configuration retained", () => testAudio.UseExperimentalWasapi.Value);
            AddAssert("checkbox reflects recovered backend", () => !checkbox.Current.Value);
        }

        private void createCheckbox(bool experimental)
        {
            Clear();
            testAudio?.Dispose();
            testAudio = new TestAudioManager(Dependencies.Get<GameHost>());
            testAudio.UseExperimentalWasapi.Value = experimental;
            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[] { (typeof(AudioManager), testAudio) },
                Child = checkbox = new LegacyAudioCheckbox(),
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            testAudio?.Dispose();
        }

        private class TestAudioManager : AudioManager
        {
            public TestAudioManager(GameHost host)
                : base(host.AudioThread, new ResourceStore<byte[]>(), new ResourceStore<byte[]>(), null)
            {
                Volume.Value = 0;
            }

            // Test configuration notifications without opening an actual WASAPI device.
            protected override bool InitBass(int device) => true;
        }
    }
}
