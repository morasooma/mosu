// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;

namespace osu.Framework.IO.Stores
{
    public class DllResourceStore : IResourceStore<byte[]>
    {
        private readonly Assembly assembly;
        private readonly string prefix;

        // Embedded resources in a loaded, non-dynamic assembly are immutable. Failed native
        // manifest lookups can be surprisingly expensive for cold font/page names on Update.
        // Share only the name index, never streams or resource bytes, and keep per-store
        // resolved-name/miss caching bounded even for arbitrary user-provided lookup strings.
        private static readonly ConditionalWeakTable<Assembly, Lazy<HashSet<string>>> resource_names = new ConditionalWeakTable<Assembly, Lazy<HashSet<string>>>();
        private const int maximum_cached_names = 2048;
        private readonly Dictionary<string, string> resolvedNames = new Dictionary<string, string>(StringComparer.Ordinal);

        public DllResourceStore(string dllName)
        {
            string filePath = Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location).AsNonNull(), dllName);

            // prefer the local file if it exists, else load from assembly cache.
            assembly = File.Exists(filePath) ? Assembly.LoadFrom(filePath) : Assembly.Load(Path.GetFileNameWithoutExtension(dllName));

            prefix = Path.GetFileNameWithoutExtension(dllName);
        }

        public DllResourceStore(AssemblyName name)
            : this(Assembly.Load(name))
        {
        }

        public DllResourceStore(Assembly assembly)
        {
            this.assembly = assembly;
            prefix = assembly.GetName().Name;
        }

        public byte[] Get(string name)
        {
            this.LogIfNonBackgroundThread(name);

            using (Stream input = GetStream(name))
                return input?.ReadAllBytesToArray();
        }

        public virtual async Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
        {
            this.LogIfNonBackgroundThread(name);

            using (Stream input = GetStream(name))
            {
                if (input == null)
                    return null;

                return await input.ReadAllBytesToArrayAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Retrieve a list of available resources provided by this store.
        /// </summary>
        public IEnumerable<string> GetAvailableResources() =>
            assembly.GetManifestResourceNames().Select(n =>
            {
                n = n.Substring(n.StartsWith(prefix, StringComparison.Ordinal) ? prefix.Length + 1 : 0);

                int lastDot = n.LastIndexOf('.');

                char[] chars = n.ToCharArray();

                for (int i = 0; i < lastDot; i++)
                {
                    if (chars[i] == '.')
                        chars[i] = '/';
                }

                return new string(chars);
            });

        public Stream GetStream(string name)
        {
            this.LogIfNonBackgroundThread(name);

            if (assembly == null)
                return null;

            // Dynamic/custom emit assemblies may add resources after construction.
            if (assembly.IsDynamic)
                return assembly.GetManifestResourceStream(resolveName(name));

            string resolved;
            bool cached;
            lock (resolvedNames)
                cached = resolvedNames.TryGetValue(name, out resolved);

            if (!cached)
            {
                resolved = resolveName(name);
                var names = resource_names.GetValue(assembly, static a => new Lazy<HashSet<string>>(
                    () => new HashSet<string>(a.GetManifestResourceNames(), StringComparer.Ordinal))).Value;
                if (!names.Contains(resolved))
                    resolved = null;

                lock (resolvedNames)
                {
                    if (resolvedNames.Count < maximum_cached_names)
                        resolvedNames.TryAdd(name, resolved);
                }
            }

            return resolved == null ? null : assembly.GetManifestResourceStream(resolved);
        }

        private string resolveName(string name)
        {
            string[] split = name.Split('/');
            for (int i = 0; i < split.Length - 1; i++)
                split[i] = split[i].Replace('-', '_');

            return $@"{prefix}.{string.Join('.', split)}";
        }

        #region IDisposable Support

        public void Dispose()
        {
        }

        #endregion
    }
}
