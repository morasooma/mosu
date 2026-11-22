using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>Update-thread adapter. Only uploads a proportional thumbnail; never runs inference or waits for work.</summary>
    internal sealed class BackgroundSafetyPresentation : IDisposable
    {
        private readonly Sprite sprite;
        private readonly WorkingBeatmap beatmap;
        private readonly BackgroundSafetyService service;
        private readonly IRenderer renderer;
        private Texture? original;
        private Texture? blurred;
        private Task<BackgroundSafetyService.SafetyResult?>? pending;
        private BackgroundSafetyService.SafetyResult? result;
        private CancellationTokenSource? cancellation;
        private bool active;
        private bool failed;
        private Texture? applied;
        private readonly string? hash;
        private readonly string? cacheKey;
        private BackgroundSafetyPolicy? lastPolicy;
        private readonly StillLifePresentation? stillLife;
        private int cacheRevision;

        internal Task Prepare(bool priority = false)
        {
            if (!service.Enabled || result != null)
                return Task.CompletedTask;
            if (pending == null)
            {
                cancellation?.Dispose();
                cancellation = new CancellationTokenSource();
                pending = service.RequestBackground(beatmap, cancellation.Token, hash, priority, cacheKey);
            }
            return pending;
        }

        public BackgroundSafetyPresentation(Sprite sprite, Texture? source, WorkingBeatmap beatmap, BackgroundSafetyService service, OsuConfigManager config, IRenderer renderer, StillLifeService? stillLifeService = null, bool generateStillLife = false)
        {
            this.sprite = sprite;
            this.beatmap = beatmap;
            this.service = service;
            this.renderer = renderer;
            if (stillLifeService != null)
                stillLife = new StillLifePresentation(beatmap, stillLifeService, renderer, generateStillLife);
            hash = beatmap.BackgroundContentHash; // Constructed on the existing drawable loader, never resolve Realm in Update.
            cacheKey = beatmap.BackgroundSafetyCacheKey;
            original = source;
            sprite.Texture = source == null ? null : new Texture(source) { ScaleAdjust = source.ScaleAdjust };
        }

        public void Update()
        {
            if (!service.Enabled)
            {
                if (active)
                {
                    cancellation?.Cancel();
                    cancellation?.Dispose();
                    cancellation = null;
                    sprite.Texture = original == null ? null : new Texture(original) { ScaleAdjust = original.ScaleAdjust };
                    blurred?.Dispose();
                    blurred = null;
                    pending = null;
                    result = null;
                    failed = false;
                    applied = null;
                    active = false;
                }
                Texture? unfilteredTarget = stillLife?.Update(sprite.IsPresent, allowed: true) ?? original;
                if (!ReferenceEquals(applied, unfilteredTarget))
                {
                    sprite.Texture = unfilteredTarget == null ? null : new Texture(unfilteredTarget) { ScaleAdjust = unfilteredTarget.ScaleAdjust };
                    applied = unfilteredTarget;
                }
                return;
            }

            if (cacheRevision != service.CacheRevision)
            {
                cacheRevision = service.CacheRevision;
                cancellation?.Cancel();
                pending = null;
                result = null;
                failed = false;
                blurred?.Dispose();
                blurred = null;
                sprite.Texture = null;
                applied = null;
            }
            var policy = service.Policy;
            if (lastPolicy != policy)
            {
                failed = false;
                lastPolicy = policy;
            }
            if (result != null && !service.IsCurrent(result))
            {
                result = null;
                blurred?.Dispose();
                blurred = null;
                applied = null;
                sprite.Texture = null;
            }
            if (!active)
            {
                active = true;
                // Unknown images are covered, never flashed unfiltered during cold inference.
                sprite.Texture = null;
            }
            // A different surface may have completed the same artwork while this
            // preview was queued. Consume it now, without waiting for our own request.
            if (result == null && service.TryGetCompleted(cacheKey, out result))
            {
                cancellation?.Cancel();
                pending = null;
                failed = false;
            }
            if (pending == null && result == null && !failed && sprite.IsPresent)
                Prepare();
            if (pending?.IsCompleted == true)
            {
                if (pending.IsCanceled)
                {
                    pending = null;
                    return;
                }
                if (pending.IsFaulted)
                {
                    _ = pending.Exception;
                    failed = true;
                }
                else
                {
                    result = pending.GetResultSafely();
                    failed = result == null;
                }
                pending = null;
            }
            if (result == null || !service.IsCurrent(result))
            {
                result = null;
                stillLife?.Update(false, allowed: false);
                sprite.Texture = null;
                applied = null;
                return;
            }

            bool shouldBlur = policy.ShouldBlur(result.Scores);
            if (shouldBlur && blurred == null && result.BlurredRgb != null)
            {
                using var rgb = Image.LoadPixelData<Rgb24>(result.BlurredRgb, result.Width, result.Height);
                blurred = new DisposableTexture(renderer.CreateTexture(result.Width, result.Height));
                blurred.SetData(new TextureUpload(rgb.CloneAs<Rgba32>()));
            }
            Texture? generated = stillLife?.Update(sprite.IsPresent, allowed: !shouldBlur);
            Texture? target = shouldBlur ? blurred : generated ?? original;
            if (!ReferenceEquals(applied, target))
            {
                sprite.Texture = target == null ? null : new Texture(target) { ScaleAdjust = target.ScaleAdjust };
                applied = target;
            }
        }

        public void Dispose()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            blurred?.Dispose();
            stillLife?.Dispose();
            result = null;
            original?.Dispose();
            original = null;
        }
    }
}
