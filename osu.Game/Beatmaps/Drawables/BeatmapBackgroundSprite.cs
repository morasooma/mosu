// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Rendering;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds.AI;

namespace osu.Game.Beatmaps.Drawables
{
    public partial class BeatmapBackgroundSprite : Sprite
    {
        private readonly IWorkingBeatmap working;
        private BackgroundSafetyPresentation? safety;

        public BeatmapBackgroundSprite(IWorkingBeatmap working)
        {
            ArgumentNullException.ThrowIfNull(working);

            this.working = working;
        }

        [BackgroundDependencyLoader]
        private void load(BackgroundSafetyService? safetyService = null, OsuConfigManager? config = null, IRenderer? renderer = null, StillLifeService? stillLife = null)
        {
            var background = working.GetBackground();
            if (working is WorkingBeatmap beatmap && safetyService != null && config != null && renderer != null)
                safety = new BackgroundSafetyPresentation(this, background, beatmap, safetyService, config, renderer, stillLife);
            else if (background != null)
                Texture = background;
        }

        protected override void Update()
        {
            base.Update();
            safety?.Update();
        }

        protected override void Dispose(bool isDisposing)
        {
            safety?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
