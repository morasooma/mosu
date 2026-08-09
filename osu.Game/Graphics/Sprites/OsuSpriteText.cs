// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Text;
using osu.Game.Configuration;
using osu.Game.Graphics;

namespace osu.Game.Graphics.Sprites
{
    public partial class OsuSpriteText : SpriteText
    {
        [Obsolete("Use TruncatingSpriteText instead.")]
        public new bool Truncate
        {
            set => throw new InvalidOperationException($"Use {nameof(TruncatingSpriteText)} instead.");
        }

        public OsuSpriteText()
        {
            Shadow = true;
            Font = OsuFont.Default;
        }

        private IBindable<string> customFont = null!;
        private IBindable<bool> russianFontFix = null!;
        private string originalFamily = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            originalFamily = Font.Family;
            customFont = config.GetBindable<string>(OsuSetting.ForkCustomUIFont);
            russianFontFix = config.GetBindable<bool>(OsuSetting.ForkRussianFontFix);

            customFont.BindValueChanged(onFontChanged, true);
            russianFontFix.BindValueChanged(onRussianFontFixChanged);
        }

        private void onFontChanged(ValueChangedEvent<string> font)
        {
            // Do not replace icons (OsuIcon)
            if (originalFamily != "OsuIcon")
            {
                string family = (string.IsNullOrEmpty(font.NewValue) || font.NewValue == "Default") ? originalFamily : font.NewValue;
                Font = Font.With(family: family);
            }
        }

        private void onRussianFontFixChanged(ValueChangedEvent<bool> _)
        {
            scheduleGlyphRefresh();
        }

        private void scheduleGlyphRefresh()
        {
            if (!IsLoaded)
                return;

            Scheduler.Add(refreshGlyphLayout);
        }

        private void refreshGlyphLayout()
        {
            if (IsDisposed)
                return;

            // Reassigning Font invalidates both character and text builder caches safely on the next update.
            Font = Font;
        }

        protected override TextBuilder CreateTextBuilder(ITexturedGlyphLookupStore store)
        {
            if (russianFontFix?.Value == true && originalFamily != "OsuIcon")
                store = new CyrillicFallbackGlyphStore(store);

            return base.CreateTextBuilder(store);
        }
    }
}