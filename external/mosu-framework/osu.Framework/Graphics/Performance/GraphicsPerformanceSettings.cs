// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;

namespace osu.Framework.Graphics.Performance
{
    /// <summary>
    /// Runtime switches for experimental scene-graph optimisations.
    /// </summary>
    public static class GraphicsPerformanceSettings
    {
        private static int cacheStaticChildLifetimes;
        private static int atlasRegionReuse;

        static GraphicsPerformanceSettings()
        {
            AtlasRegionReuse = Environment.GetEnvironmentVariable("MOSU_ATLAS_REGION_ALLOCATOR") == "1";
        }

        /// <summary>
        /// Whether composites may skip direct-child scans until the next predictable lifetime boundary.
        /// </summary>
        public static bool CacheStaticChildLifetimes
        {
            get => Volatile.Read(ref cacheStaticChildLifetimes) != 0;
            set => Volatile.Write(ref cacheStaticChildLifetimes, value ? 1 : 0);
        }

        /// <summary>
        /// Whether <see cref="Textures.TextureAtlas"/> should use a free-list region allocator that
        /// supports freeing and reusing regions of purged textures, instead of the append-only
        /// shelf-packing cursor. Off by default; initialised from <c>MOSU_ATLAS_REGION_ALLOCATOR</c>.
        /// </summary>
        public static bool AtlasRegionReuse
        {
            get => Volatile.Read(ref atlasRegionReuse) != 0;
            set => Volatile.Write(ref atlasRegionReuse, value ? 1 : 0);
        }
    }
}
