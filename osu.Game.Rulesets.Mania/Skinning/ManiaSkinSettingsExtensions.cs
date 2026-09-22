// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning
{
    public static class ManiaSkinSettingsExtensions
    {
        /// <summary>
        /// Finds the highest-priority concrete skin carrying Mosu settings.
        /// Transformers are intentionally skipped; their underlying sources are present in <see cref="ISkinSource.AllSources"/>.
        /// </summary>
        public static Bindable<int>? GetManiaNoteScale(this ISkinSource source)
        {
            foreach (var candidate in source.AllSources)
            {
                Skin? skin = findSkin(candidate);

                if (skin != null)
                    return skin.MosuSettings.ManiaNoteScalePercent;
            }

            return null;
        }

        /// <summary>
        /// Gets the scale belonging to the selected client skin, even when another skin source (for example,
        /// a beatmap-provided skin) has higher lookup priority for visual components.
        /// </summary>
        public static Bindable<int>? GetManiaNoteScale(this ISkinSource source, Skin preferredSkin)
        {
            foreach (var candidate in source.AllSources)
            {
                if (ReferenceEquals(findSkin(candidate), preferredSkin))
                    return preferredSkin.MosuSettings.ManiaNoteScalePercent;
            }

            return source.GetManiaNoteScale();
        }

        private static Skin? findSkin(ISkin source)
        {
            if (source is Skin skin)
                return skin;

            return source is ISkinTransformer transformer ? findSkin(transformer.Skin) : null;
        }
    }
}
