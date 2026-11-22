using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeTimelineFilterToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        public DodgeTimelineFilterToolboxGroup()
            : base(DodgeEditorStrings.TimelineFilter)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new Vector2(4);

            for (int index = 0; index < composer.TimelineLaneCount; index++)
            {
                EditorTimelineLane lane = composer.GetTimelineLaneByIndex(index);
                var visibility = composer.GetTimelineLaneVisibility(index);
                var button = new ExpandableButton
                {
                    Name = $"Timeline filter: {lane.ShortName}",
                    RelativeSizeAxes = Axes.X,
                    Height = 32,
                    TooltipText = LocalisableString.Interpolate($"{lane.Name}: {DodgeEditorStrings.TimelineFilterHint}"),
                    Action = () => visibility.Toggle(),
                };
                visibility.BindValueChanged(value =>
                {
                    string marker = value.NewValue ? "✓" : "−";
                    button.ContractedLabelText = $"{marker} {lane.ShortName}";
                    button.ExpandedLabelText = LocalisableString.Interpolate($"{marker} {lane.Name}");
                    button.Alpha = value.NewValue ? 1 : 0.5f;
                }, true);
                Add(button);
            }
        }
    }
}
