// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using System.Threading.Tasks;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds.AI;
using osu.Framework.Graphics.Rendering;

namespace osu.Game.Graphics.Backgrounds
{
    public partial class BeatmapBackground : Background
    {
        public readonly WorkingBeatmap Beatmap;

        private readonly string fallbackTextureName;
        private BackgroundSafetyPresentation safety;
        private Texture sourceTexture;

        // Check the original upload even when safety presentation temporarily covers the sprite.
        // This never waits for inference or generated artwork.
        internal bool TextureUploadComplete => sourceTexture?.UploadComplete != false;

        public BeatmapBackground(WorkingBeatmap beatmap, string fallbackTextureName = @"Backgrounds/bg1")
        {
            Beatmap = beatmap;
            this.fallbackTextureName = fallbackTextureName;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, BackgroundSafetyService safetyService = null, OsuConfigManager config = null, IRenderer renderer = null, StillLifeService stillLife = null)
        {
            var texture = sourceTexture = Beatmap?.GetBackground() ?? textures.Get(fallbackTextureName);
            if (Beatmap != null && safetyService != null && config != null && renderer != null)
                safety = new BackgroundSafetyPresentation(Sprite, texture, Beatmap, safetyService, config, renderer, stillLife, generateStillLife: true);
            else
                Sprite.Texture = texture;
        }

        internal Task PrepareSafety() => safety?.Prepare(priority: true) ?? Task.CompletedTask;

        internal void ApplySafety() => safety?.Update();

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

        public override bool Equals(Background other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return other.GetType() == GetType()
                   && ((BeatmapBackground)other).Beatmap == Beatmap;
        }
    }
}
