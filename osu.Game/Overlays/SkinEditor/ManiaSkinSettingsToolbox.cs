// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Skinning;

namespace osu.Game.Overlays.SkinEditor
{
    internal partial class ManiaSkinSettingsToolbox : EditorSidebarSection
    {
        public ManiaSkinSettingsToolbox(Skin skin, Action? settingChanged = null)
            : base("Mania")
        {
            var slider = new SettingsItemV2(new FormSliderBar<int>
            {
                Caption = "Playfield scale",
                HintText = "Scales the mania notefield around the receptor line, including notes, receptors, judgements and column spacing.",
                Current = skin.MosuSettings.ManiaNoteScalePercent,
                KeyboardStep = 1,
                LabelFormat = value => $"{value}%",
            });

            slider.SettingChanged += () => settingChanged?.Invoke();
            Children = new Drawable[] { slider };
        }
    }
}
