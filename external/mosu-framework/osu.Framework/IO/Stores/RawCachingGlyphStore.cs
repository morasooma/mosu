// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using SharpFNT;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.IO.Stores
{
    /// <summary>
    /// A glyph store which caches font sprite sheets as raw alpha pixels to disk on first use.
    /// A bounded shared memory cache avoids repeated synchronous reads for individual characters.
    /// </summary>
    /// <remarks>
    /// Pages larger than the memory budget still use region reads from the disk cache.
    /// Consider <see cref="TimedExpiryGlyphStore"/> if keeping full RGBA pages is preferable.
    /// </remarks>
    public class RawCachingGlyphStore : GlyphStore
    {
        /// <summary>
        /// A storage backing to be used for storing decompressed glyph sheets.
        /// </summary>
        internal Storage? CacheStorage { get; set; }

        private readonly Dictionary<string, Stream> pageStreamHandles = new Dictionary<string, Stream>();

        private readonly Dictionary<int, PageInfo> pageLookup = new Dictionary<int, PageInfo>();
        private bool disposed;

        internal RawGlyphPageCache PageCache { get; set; } = RawGlyphPageCache.Shared;

        public RawCachingGlyphStore(ResourceStore<byte[]> store, string? assetName = null, IResourceStore<TextureUpload>? textureLoader = null)
            : base(store, assetName, textureLoader)
        {
        }

        protected override TextureUpload LoadCharacter(Character character)
        {
            using var operation = StutterTraceEventSource.Log.Measure("Glyph.LoadCharacter");
            if (CacheStorage == null)
                throw new InvalidOperationException($"{nameof(CacheStorage)} should be set before requesting characters.");

            // Use simple global locking for the time being.
            // If necessary, a per-lookup-key (page number) locking mechanism could be implemented similar to TextureStore.
            lock (pageLookup)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (!pageLookup.TryGetValue(character.Page, out var pageInfo))
                    pageInfo = createCachedPageInfo(character.Page);

                return createTextureUpload(character, pageInfo);
            }
        }

        /// <summary>
        /// Prepares only pages used by the supplied characters during off-Update loading.
        /// No glyph images, textures, persistent streams or background jobs are created.
        /// </summary>
        internal int PreparePages(string characters, int maximumBytes, HashSet<string> preparedKeys, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThreadSafety.IsUpdateThread || CacheStorage == null || maximumBytes <= 0)
                return 0;

            var font = Font;
            if (font == null)
                return 0;
            // BMFont declares the page upper bound even when PNG pages are trimmed.
            // Reject oversized pages before a cache miss could decode a huge image.
            long pageUpperBound = (long)font.Common.ScaleWidth * font.Common.ScaleHeight;
            if (pageUpperBound <= 0 || pageUpperBound > maximumBytes)
                return 0;

            var selectedPages = new HashSet<int>();
            foreach (char character in characters)
            {
                if (font.Characters.TryGetValue(character, out var glyph))
                    selectedPages.Add(glyph.Page);
            }

            int preparedBytes = 0;
            lock (pageLookup)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                foreach (int page in selectedPages)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (pageUpperBound > maximumBytes - preparedBytes)
                        break;
                    if (!pageLookup.TryGetValue(page, out var info))
                        info = createCachedPageInfo(page);
                    int bytes = checked(info.Size.Width * info.Size.Height);
                    if (preparedKeys.Contains(info.Filename) || !PageCache.CanCache(bytes) || bytes > maximumBytes - preparedBytes)
                        continue;

                    if (PageCache.Get(info.Filename) == null)
                    {
                        using var source = CacheStorage.GetStream(info.Filename);
                        byte[] pixels = new byte[bytes];
                        source.ReadExactly(pixels);
                        cancellationToken.ThrowIfCancellationRequested();
                        PageCache.Add(info.Filename, pixels);
                    }
                    preparedKeys.Add(info.Filename);
                    preparedBytes += bytes;
                }
            }
            return preparedBytes;
        }

        private PageInfo createCachedPageInfo(int page)
        {
            using var operation = StutterTraceEventSource.Log.Measure("Glyph.PageInfoHashAndCache", page);
            Debug.Assert(CacheStorage != null);

            string filename = GetFilenameForPage(page);

            using (var stream = Store.GetStream(filename))
            {
                // The md5 of the original (compressed png) content.
                string streamMd5 = stream.ComputeMD5Hash();

                // The md5 of the access filename, including font name and page number.
                string filenameMd5 = filename.ComputeMD5Hash();

                string accessFilename = $"{filenameMd5}#{streamMd5}";

                // Finding an existing file validates that the file both exists on disk, and was generated for the correct font.
                // It doesn't guarantee that the generated cache file is in a good state.
                string? existing = CacheStorage.GetFiles(string.Empty, $"{accessFilename}*").FirstOrDefault();

                if (existing != null)
                {
                    // Filename format is "filenameHashMD5#contentHashMD5#width#height"
                    string[] split = existing.Split('#');

                    if (split.Length == 4
                        && int.TryParse(split[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width)
                        && int.TryParse(split[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
                    {
                        // Sanity check that the length of the file is expected, based on the width and height.
                        // If we ever see corrupt files in the wild, this should be changed to a full md5 check. Hopefully it will never happen.
                        using (var testStream = CacheStorage.GetStream(existing))
                        {
                            if (testStream.Length == width * height)
                            {
                                return pageLookup[page] = new PageInfo
                                {
                                    Size = new Size(width, height),
                                    Filename = existing
                                };
                            }
                        }
                    }
                }

                using (var convert = GetPageImage(page))
                using (var buffer = SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.Allocate<byte>(convert.Width * convert.Height))
                {
                    var output = buffer.Memory.Span;
                    var source = convert.Data;

                    for (int i = 0; i < output.Length; i++)
                        output[i] = source[i].A;

                    // ensure any stale cached versions are deleted.
                    foreach (string f in CacheStorage.GetFiles(string.Empty, $"{filenameMd5}*"))
                        CacheStorage.Delete(f);

                    accessFilename += FormattableString.Invariant($"#{convert.Width}#{convert.Height}");

                    using (var outStream = CacheStorage.CreateFileSafely(accessFilename))
                        outStream.Write(buffer.Memory.Span);

                    return pageLookup[page] = new PageInfo
                    {
                        Size = new Size(convert.Width, convert.Height),
                        Filename = accessFilename
                    };
                }
            }
        }

        private TextureUpload createTextureUpload(Character character, PageInfo page)
        {
            Debug.Assert(CacheStorage != null);

            int pageWidth = page.Size.Width;

            // The filename contains the source and content hashes plus dimensions. Sharing by
            // that key is safe across stores and storage roots; no stream or Storage is retained.
            byte[]? pixels = PageCache.Get(page.Filename);
            byte[]? readBuffer = null;
            int offset = 0;
            int pageBytes = checked(pageWidth * page.Size.Height);

            try
            {
                if (pixels == null)
                {
                    if (!pageStreamHandles.TryGetValue(page.Filename, out var source))
                        source = pageStreamHandles[page.Filename] = CacheStorage.GetStream(page.Filename);

                    if (PageCache.CanCache(pageBytes))
                    {
                        using var read = StutterTraceEventSource.Log.Measure("Glyph.ReadColdPage", pageBytes);
                        pixels = new byte[pageBytes];
                        source.Seek(0, SeekOrigin.Begin);
                        source.ReadExactly(pixels);
                        pixels = PageCache.Add(page.Filename, pixels);
                    }
                    else
                    {
                        // Do not retain an oversized page. Read only rows intersecting the glyph,
                        // leaving pixels outside a trimmed page transparent.
                        int height = Math.Max(0, Math.Min(character.Height, page.Size.Height - character.Y));
                        int characterByteRegion = checked(pageWidth * height);
                        pixels = readBuffer = ArrayPool<byte>.Shared.Rent(characterByteRegion);
                        if (characterByteRegion > 0)
                        {
                            using var read = StutterTraceEventSource.Log.Measure("Glyph.ReadOversizedRegion", characterByteRegion);
                            source.Seek((long)pageWidth * character.Y, SeekOrigin.Begin);
                            source.ReadExactly(readBuffer.AsSpan(0, characterByteRegion));
                        }
                        offset = -character.Y;
                    }
                }

                var image = new Image<Rgba32>(SixLabors.ImageSharp.Configuration.Default, character.Width, character.Height);
                try
                {
                    // The spritesheet may have unused pixels trimmed.
                    int readableHeight = Math.Min(character.Height, page.Size.Height - character.Y);
                    int readableWidth = Math.Min(character.Width, pageWidth - character.X);

                    for (int y = 0; y < character.Height; y++)
                    {
                        var span = image.DangerousGetPixelRowMemory(y).Span;
                        int readOffset = (y + character.Y + offset) * pageWidth + character.X;

                        for (int x = 0; x < character.Width; x++)
                            span[x] = new Rgba32(255, 255, 255, x < readableWidth && y < readableHeight ? pixels[readOffset + x] : (byte)0);
                    }

                    return new TextureUpload(image);
                }
                catch
                {
                    image.Dispose();
                    throw;
                }
            }
            finally
            {
                if (readBuffer != null)
                    ArrayPool<byte>.Shared.Return(readBuffer);
            }
        }

        protected override void Dispose(bool disposing)
        {
            lock (pageLookup)
            {
                if (disposed)
                    return;
                disposed = true;
                base.Dispose(disposing);

                foreach (var handle in pageStreamHandles.Values)
                    handle.Dispose();
                pageStreamHandles.Clear();
            }
        }

        private record PageInfo
        {
            public string Filename { get; set; } = string.Empty;
            public Size Size { get; set; }
        }
    }
}
