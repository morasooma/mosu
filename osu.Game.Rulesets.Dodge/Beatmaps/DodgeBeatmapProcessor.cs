// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Dodge.Beatmaps
{
    public class DodgeBeatmapProcessor : BeatmapProcessor, IEditorBreakProcessor
    {
        public bool AlwaysRegenerateAutomaticBreaks => true;

        public DodgeBeatmapProcessor(IBeatmap beatmap)
            : base(beatmap)
        {
        }

        public IEnumerable<HitObject> GetBreakRelevantHitObjects()
            => Beatmap.HitObjects.Where(hitObject => hitObject is DodgeBullet or DodgeEmitter or DodgeBeam);

        public void PostProcessAutomaticBreaks(EditorBeatmap beatmap)
        {
            DodgeHitObject[] hitObjects = beatmap.HitObjects.OfType<DodgeHitObject>().ToArray();

            if (hitObjects.Length == 0)
                return;

            BreakPeriod[] manualBreaks = beatmap.Breaks.Where(breakPeriod => breakPeriod is ManualBreakPeriod).ToArray();
            BreakPeriod[] automaticBreaks = beatmap.Breaks.Where(breakPeriod => breakPeriod is not ManualBreakPeriod).ToArray();
            IReadOnlyList<BreakPeriod> playableAutomaticBreaks = DodgeBreakPeriodCalculator.ExcludeMovingProjectiles(
                automaticBreaks,
                hitObjects,
                DodgeBeatmapSettings.GetBulletSize(beatmap.Difficulty));

            beatmap.Breaks.Clear();
            beatmap.Breaks.AddRange(manualBreaks);
            beatmap.Breaks.AddRange(playableAutomaticBreaks);
        }
    }
}
