// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets;

namespace osu.Game.Skinning
{
    public static class SkinCustomFontSerializer
    {
        public static List<SkinCustomFontOverride> ExtractOverrides(GlobalSkinnableContainers container, RulesetInfo? ruleset, IEnumerable<Drawable> drawables)
        {
            var results = new List<SkinCustomFontOverride>();
            extractRecursive(container, ruleset, drawables.ToList(), new List<int>(), results);
            return results;
        }

        public static void ApplyOverrides(SkinCustomFontInfo fontInfo, GlobalSkinnableContainers container, RulesetInfo? ruleset,
                                          IReadOnlyList<SerialisedDrawableInfo> infos, IReadOnlyList<Drawable> drawables)
        {
            applyRecursive(fontInfo, container, ruleset, infos, drawables, new List<int>());
        }

        private static void extractRecursive(GlobalSkinnableContainers container, RulesetInfo? ruleset, IReadOnlyList<Drawable> drawables, List<int> pathPrefix,
                                             List<SkinCustomFontOverride> results)
        {
            for (int i = 0; i < drawables.Count; i++)
            {
                var path = pathPrefix.Concat(new[] { i }).ToArray();

                if (drawables[i] is FontAdjustableSkinComponent fontComponent && !string.IsNullOrEmpty(fontComponent.CustomFontFamily.Value))
                {
                    results.Add(new SkinCustomFontOverride
                    {
                        Container = container.ToString(),
                        Ruleset = ruleset?.ShortName ?? @"global",
                        Path = path,
                        FontFamily = fontComponent.CustomFontFamily.Value,
                        TextWeight = fontComponent.TextWeight.Value,
                    });
                }

                if (drawables[i] is Container composite)
                    extractRecursive(container, ruleset, composite.Children.ToArray(), path.ToList(), results);
            }
        }

        private static void applyRecursive(SkinCustomFontInfo fontInfo, GlobalSkinnableContainers container, RulesetInfo? ruleset,
                                           IReadOnlyList<SerialisedDrawableInfo> infos, IReadOnlyList<Drawable> drawables, List<int> pathPrefix)
        {
            for (int i = 0; i < infos.Count && i < drawables.Count; i++)
            {
                var path = pathPrefix.Concat(new[] { i }).ToList();
                var fontOverride = fontInfo.FindOverride(container, ruleset, path);

                if (fontOverride != null && drawables[i] is FontAdjustableSkinComponent fontComponent)
                {
                    fontComponent.CustomFontFamily.Value = fontOverride.FontFamily;
                    fontComponent.TextWeight.Value = fontOverride.TextWeight;
                }

                if (drawables[i] is Container composite && infos[i].Children.Count > 0)
                    applyRecursive(fontInfo, container, ruleset, infos[i].Children, composite.Children.ToArray(), path);
            }
        }
    }
}