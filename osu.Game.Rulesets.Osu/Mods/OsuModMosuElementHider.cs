using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModMosuElementHider : Mod, IHidesSkinElements
    {
        public override string Name => "Skin Editor";
        public override string Acronym => "EH";
        public override IconUsage? Icon => FontAwesome.Solid.EyeSlash;
        public override ModType Type => ModType.Mosu;
        public override LocalisableString Description => SkinElementModStrings.Description;
        public override bool HasImplementation => true;
        public override bool Ranked => true;

        [SettingSource(typeof(SkinElementModStrings), nameof(SkinElementModStrings.Numbers), nameof(SkinElementModStrings.NumbersHint), 0)]
        public BindableBool HideNumbers { get; } = new BindableBool();

        [SettingSource(typeof(SkinElementModStrings), nameof(SkinElementModStrings.FollowPoints), nameof(SkinElementModStrings.FollowPointsHint), 1)]
        public BindableBool HideFollowPoints { get; } = new BindableBool();

        [SettingSource(typeof(SkinElementModStrings), nameof(SkinElementModStrings.Ball), nameof(SkinElementModStrings.BallHint), 2)]
        public BindableBool HideSliderBall { get; } = new BindableBool(true);

        [SettingSource(typeof(SkinElementModStrings), nameof(SkinElementModStrings.FollowCircle), nameof(SkinElementModStrings.FollowCircleHint), 3)]
        public BindableBool HideSliderFollowCircle { get; } = new BindableBool(true);

        [SettingSource(typeof(SkinElementModStrings), nameof(SkinElementModStrings.Arrows), nameof(SkinElementModStrings.ArrowsHint), 4)]
        public BindableBool HideReverseArrows { get; } = new BindableBool(true);

        [SettingSource(typeof(SkinElementModStrings), nameof(SkinElementModStrings.Ticks), nameof(SkinElementModStrings.TicksHint), 5)]
        public BindableBool HideSliderTicks { get; } = new BindableBool();

        [SettingSource(typeof(SkinElementModStrings), nameof(SkinElementModStrings.Spinner), nameof(SkinElementModStrings.SpinnerHint), 6)]
        public BindableBool HideSpinners { get; } = new BindableBool();

        public bool Hides(OsuSetting visibilitySetting) => visibilitySetting switch
        {
            OsuSetting.ForkShowHitCircleNumbers => HideNumbers.Value,
            OsuSetting.ForkShowFollowPoints => HideFollowPoints.Value,
            OsuSetting.ForkShowSliderBall => HideSliderBall.Value,
            OsuSetting.ForkShowSliderFollowCircle => HideSliderFollowCircle.Value,
            OsuSetting.ForkShowReverseArrows => HideReverseArrows.Value,
            OsuSetting.ForkShowSliderTicks => HideSliderTicks.Value,
            OsuSetting.ForkShowSpinners => HideSpinners.Value,
            _ => false,
        };
    }
}
