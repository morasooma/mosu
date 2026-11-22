using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapSet;

namespace osu.Game.Tests.Visual.Online
{
    public partial class TestScenePreparedBeatmapMetadata : OsuTestScene
    {
        [BackgroundDependencyLoader]
        private void load() => Dependencies.Cache(new OverlayColourProvider(OverlayColourScheme.Blue));

        [Test]
        public void TestMetadataPartsArePopulatedOffUpdateAndLatestValueWins()
        {
            ProbeSection section = null!;
            AddStep("create section", () => Child = section = new ProbeSection());
            AddStep("replace metadata rapidly", () =>
            {
                section.Metadata = "obsolete";
                section.Metadata = "latest 日本語 русский";
            });
            AddUntilStep("latest content published", () => section.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains("latest", StringComparison.Ordinal)));
            AddAssert("not populated on Update", () => section.UpdatePopulations, () => Is.Zero);
            AddAssert("obsolete content absent", () => !section.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString().Contains("obsolete", StringComparison.Ordinal)));
            AddStep("replace again", () => section.Metadata = "replacement");
            AddUntilStep("old flow removed", () => section.ChildrenOfType<LinkFlowContainer>().Count() == 1
                && section.ChildrenOfType<OsuSpriteText>().Any(t => t.Text.ToString() == "replacement"));
        }

        [Test]
        public void TestEmptyMetadataCancelsPendingPublicationAndReleasesOldFlow()
        {
            ProbeSection section = null!;
            AddStep("create section", () => Child = section = new ProbeSection());
            AddStep("publish metadata", () => section.Metadata = "previous");
            AddUntilStep("old flow ready", () => section.ChildrenOfType<LinkFlowContainer>().Any());
            AddStep("request then hide", () =>
            {
                section.Metadata = "must not publish";
                section.Metadata = string.Empty;
            });
            AddWaitStep("allow cancelled load to finish", 10);
            AddAssert("no old or stale flow retained", () => !section.ChildrenOfType<LinkFlowContainer>().Any());
            AddAssert("section stays hidden", () => section.Alpha == 0);
        }

        private partial class ProbeSection : MetadataSection
        {
            private int updatePopulations;
            public int UpdatePopulations => Volatile.Read(ref updatePopulations);

            public ProbeSection() : base(MetadataType.Source) { }

            protected override void AddMetadata(string metadata, LinkFlowContainer loaded)
            {
                if (ThreadSafety.IsUpdateThread)
                    Interlocked.Increment(ref updatePopulations);
                loaded.AddText(metadata);
            }
        }
    }
}
