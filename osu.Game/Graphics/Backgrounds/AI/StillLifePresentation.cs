using System;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>Only completed RGB data is uploaded here; generation/downloads stay on the worker.</summary>
    internal sealed class StillLifePresentation : IDisposable
    {
        private readonly WorkingBeatmap beatmap;
        private readonly StillLifeService service;
        private readonly IRenderer renderer;
        private readonly bool generate;
        private readonly string? key;
        private readonly string? hash;
        private CancellationTokenSource? cancellation;
        private Task<BackgroundImageCache.Artwork?>? pending;
        private Texture? texture;
        private bool attempted;
        private int revision;
        private long lastAttempt;
        public StillLifePresentation(WorkingBeatmap beatmap, StillLifeService service, IRenderer renderer, bool generate)
        {
            this.beatmap = beatmap;
            this.service = service;
            this.renderer = renderer;
            this.generate = generate;
            key = beatmap.BackgroundSafetyCacheKey;
            hash = beatmap.BackgroundContentHash;
            revision = service.Revision;
        }

        public Texture? Update(bool visible, bool allowed)
        {
            if (!service.Options.Enabled || !allowed || revision != service.Revision)
            {
                Reset();
                revision = service.Revision;
                if (!service.Options.Enabled || !allowed)
                    return null;
            }
            if (texture != null)
                return texture;
            BackgroundImageCache.Artwork? artwork = null;
            if (service.TryGet(key, out artwork))
            {
                cancellation?.Cancel();
                pending = null;
            }
            else if (pending?.IsCompleted == true)
            {
                if (pending.IsCompletedSuccessfully)
                    artwork = pending.GetResultSafely();
                else if (pending.IsFaulted)
                    _ = pending.Exception;
                pending = null;
            }
            if (artwork != null)
            {
                using var rgb = Image.LoadPixelData<Rgb24>(artwork.Rgb, artwork.Width, artwork.Height);
                texture = new DisposableTexture(renderer.CreateTexture(artwork.Width, artwork.Height));
                texture.SetData(new TextureUpload(rgb.CloneAs<Rgba32>()));
            }
            else if (visible && pending == null && (!attempted || Stopwatch.GetElapsedTime(lastAttempt).TotalSeconds >= (generate ? 30 : 2)))
            {
                cancellation?.Dispose();
                cancellation = new CancellationTokenSource();
                lastAttempt = Stopwatch.GetTimestamp();
                pending = service.RequestBackground(beatmap, hash, key, cancellation.Token, generate);
                attempted = true;
            }
            return texture;
        }

        private void Reset()
        {
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            pending = null;
            texture?.Dispose();
            texture = null;
            attempted = false;
        }
        public void Dispose() => Reset();
    }
}
