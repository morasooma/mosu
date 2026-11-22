using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Scoring;
using osu.Game.Scoring.Render;
using osu.Game.Screens.Ranking;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.NonVisual;

[HeadlessTest]
public partial class ReplayRenderPopoverTest : OsuTestScene
{
    [Cached]
    private readonly OverlayColourProvider colours = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

    [TestCase(RenderEncodingSelector.AutoEncoderLabel, true)]
    [TestCase(RenderEncodingSelector.CompactEncoder, false)]
    public void TestBitrateRowFollowsEncoder(string savedEncoder, bool bitrateVisible)
    {
        ReplayRenderPopover popover = null!;
        OsuDropdown<string> encoder = null!;
        Drawable bitrateRow = null!;

        AddStep("open render settings", () =>
        {
            Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkReplayRenderEncoder, savedEncoder);
            Child = popover = new ReplayRenderPopover(new ScoreInfo());
            popover.Show();
            encoder = popover.ChildrenOfType<OsuDropdown<string>>().Single(d => d.Items.Contains(RenderEncodingSelector.CompactEncoder));
            bitrateRow = popover.ChildrenOfType<SettingsSlider<int>>().Single().Parent!.Parent!;
        });
        AddAssert("saved encoder selected", () => encoder.Current.Value == savedEncoder);
        AddAssert("bitrate row initial visibility", () => bitrateRow.IsPresent == bitrateVisible);
        AddStep("change encoder", () => encoder.Current.Value = bitrateVisible ? RenderEncodingSelector.CompactEncoder : RenderEncodingSelector.AutoEncoderLabel);
        AddAssert("bitrate row follows selection", () => bitrateRow.IsPresent != bitrateVisible);
    }
}
