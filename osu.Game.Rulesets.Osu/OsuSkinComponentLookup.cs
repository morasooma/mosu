// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Skinning;
using osu.Game.Configuration;

namespace osu.Game.Rulesets.Osu
{
    public class OsuSkinComponentLookup : SkinComponentLookup<OsuSkinComponents>, IHasVisibilitySetting
    {
        public OsuSetting? VisibilitySetting => Component switch
        {
            OsuSkinComponents.HitCircleText => OsuSetting.ForkShowHitCircleNumbers,
            OsuSkinComponents.FollowPoint => OsuSetting.ForkShowFollowPoints,
            OsuSkinComponents.SliderBall => OsuSetting.ForkShowSliderBall,
            OsuSkinComponents.SliderFollowCircle => OsuSetting.ForkShowSliderFollowCircle,
            OsuSkinComponents.ReverseArrow => OsuSetting.ForkShowReverseArrows,
            OsuSkinComponents.SliderScorePoint => OsuSetting.ForkShowSliderTicks,
            OsuSkinComponents.SpinnerBody => OsuSetting.ForkShowSpinners,
            _ => null,
        };

        public OsuSkinComponentLookup(OsuSkinComponents component)
            : base(component)
        {
        }
    }
}
