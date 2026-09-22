// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.IO.Stores;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Provides read-only access to absolute file paths for the shared stable carousel preview stores.
    /// </summary>
    internal sealed class AbsolutePathResourceStore : IResourceStore<byte[]>
    {
        public byte[] Get(string name) => File.Exists(name) ? File.ReadAllBytes(name) : null!;

        public async Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
            => File.Exists(name) ? await File.ReadAllBytesAsync(name, cancellationToken).ConfigureAwait(false) : null!;

        public Stream? GetStream(string name) => File.Exists(name) ? File.OpenRead(name) : null;

        public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();

        public void Dispose()
        {
        }
    }
}
