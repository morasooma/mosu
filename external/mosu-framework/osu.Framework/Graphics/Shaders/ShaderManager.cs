// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using osu.Framework.Graphics.Rendering;
using osu.Framework.IO.Stores;

namespace osu.Framework.Graphics.Shaders
{
    public class ShaderManager : IShaderStore, IDisposable
    {
        private const string shader_prefix = @"sh_";

        // Enabled by default; retain an explicit opt-out for diagnostics or driver issues.
        internal static readonly bool MultiTextureTextEnabled = Environment.GetEnvironmentVariable("MOSU_TEXT_MULTI_BATCH") != "0";

        private readonly ConcurrentDictionary<string, IShaderPart> partCache = new ConcurrentDictionary<string, IShaderPart>();
        private readonly ConcurrentDictionary<(string, string), IShader> shaderCache = new ConcurrentDictionary<(string, string), IShader>();

        private readonly IRenderer renderer;
        private readonly IResourceStore<byte[]> store;

        private static readonly ConditionalWeakTable<IShader, object> standard_texture_shaders = new ConditionalWeakTable<IShader, object>();
        private readonly ConcurrentDictionary<IShaderPart, byte> standardTextureParts = new ConcurrentDictionary<IShaderPart, byte>();

        // Names are not a shader contract: resource stores may replace either the
        // shader or its includes. Only recognise an immutable snapshot of the
        // framework's complete standard Texture2D/Texture source closure.
        private static readonly Lazy<Dictionary<string, byte[]>> standard_texture_sources = new Lazy<Dictionary<string, byte[]>>(() =>
        {
            using var resources = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ShaderManager).Assembly), "Resources/Shaders");
            var sources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string name in new[]
                     {
                         "sh_Texture2D.vs", "sh_Texture.fs", "sh_Utils.h", "sh_Masking.h", "sh_TextureWrapping.h",
                         "Internal/sh_Compatibility.h", "Internal/sh_GlobalUniforms.h", "Internal/sh_Vertex_Output.h", "Internal/sh_Fragment_Output.h"
                     })
                sources.Add(name, resources.Get(name)!);
            return sources;
        });

        internal static bool IsStandardTextureShader(IShader? shader) => shader != null && standard_texture_shaders.TryGetValue(shader, out _);

        // Do not let a game resource override the companion shader while the
        // original shader has been verified as the framework default.
        internal IShader? LoadMultiTextureShader()
        {
            if (renderer is not OpenGL.GLRenderer { SupportsMultiTextureText: true })
                return null;

            const string key = "__framework_multi_texture_text";
            lock (shaderCache)
            {
                if (shaderCache.TryGetValue((key, key), out IShader? cached))
                    return cached;

                using var resources = new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(ShaderManager).Assembly), "Resources/Shaders");
                var sources = new Dictionary<string, byte[]>(standard_texture_sources.Value, StringComparer.Ordinal)
                {
                    ["sh_TextureMulti.vs"] = resources.Get("sh_TextureMulti.vs")!,
                    ["sh_TextureMulti.fs"] = resources.Get("sh_TextureMulti.fs")!
                };
                var snapshot = new TextureShaderSnapshot(sources);
                IShaderPart vertex = renderer.CreateShaderPart(snapshot, "sh_TextureMulti.vs", sources["sh_TextureMulti.vs"], ShaderPartType.Vertex);
                IShaderPart fragment = renderer.CreateShaderPart(snapshot, "sh_TextureMulti.fs", sources["sh_TextureMulti.fs"], ShaderPartType.Fragment);
                partCache.TryAdd(key + ".vs", vertex);
                partCache.TryAdd(key + ".fs", fragment);
                return shaderCache[(key, key)] = renderer.CreateShader(key, new[] { vertex, fragment });
            }
        }

        /// <summary>
        /// Constructs a new <see cref="ShaderManager"/>.
        /// </summary>
        public ShaderManager(IRenderer renderer, IResourceStore<byte[]> store)
        {
            this.renderer = renderer;
            this.store = store;
        }

        /// <summary>
        /// Retrieves a usable <see cref="IShader"/> given the vertex and fragment shaders.
        /// </summary>
        /// <param name="vertex">The vertex shader name.</param>
        /// <param name="fragment">The fragment shader name.</param>
        public IShader Load(string vertex, string fragment)
        {
            IShader? cached = GetCachedShader(vertex, fragment);
            if (cached != null)
                return cached;

            IShaderPart vertexPart = resolveShaderPart(vertex, ShaderPartType.Vertex);
            IShaderPart fragmentPart = resolveShaderPart(fragment, ShaderPartType.Fragment);
            IShader shader = renderer.CreateShader($"{vertex}/{fragment}", new[] { vertexPart, fragmentPart });

            if (GetType() == typeof(ShaderManager) && vertex == VertexShaderDescriptor.TEXTURE_2 && fragment == FragmentShaderDescriptor.TEXTURE
                && standardTextureParts.ContainsKey(vertexPart) && standardTextureParts.ContainsKey(fragmentPart)
                && (shader.GetType() == typeof(OpenGL.Shaders.GLShader) || shader.GetType() == typeof(Veldrid.Shaders.VeldridShader)
                    || shader.GetType() == typeof(Rendering.Deferred.DeferredShader) || shader.GetType() == typeof(Rendering.Dummy.DummyShader)))
                standard_texture_shaders.TryAdd(shader, new object());

            return shaderCache[(vertex, fragment)] = shader;
        }

        /// <summary>
        /// Attempts to retrieve an already-cached shader.
        /// </summary>
        /// <param name="vertex">The vertex shader name.</param>
        /// <param name="fragment">The fragment shader name.</param>
        /// <returns>A cached <see cref="IShader"/> instance, if existing.</returns>
        public virtual IShader? GetCachedShader(string vertex, string fragment)
            => shaderCache.GetValueOrDefault((vertex, fragment));

        /// <summary>
        /// Attempts to retrieve an already-cached shader part.
        /// </summary>
        /// <param name="name">The name of the shader part.</param>
        /// <returns>A cached <see cref="IShaderPart"/> instance, if existing.</returns>
        public virtual IShaderPart? GetCachedShaderPart(string name)
            => partCache.GetValueOrDefault(name);

        /// <summary>
        /// Attempts to retrieve the raw data for a shader file.
        /// </summary>
        /// <param name="fileName">The file name.</param>
        /// <returns></returns>
        public virtual byte[]? GetRawData(string fileName) => store.Get(fileName);

        private IShaderPart resolveShaderPart(string name, ShaderPartType type)
        {
            name = ensureValidName(name, type);

            IShaderPart? cached = GetCachedShaderPart(name);
            if (cached != null)
                return cached;

            byte[]? rawData = GetRawData(name);

            if (rawData == null)
                throw new FileNotFoundException($"{type} shader part could not be found.", name);

            IShaderStore shaderStore = this;
            if (MultiTextureTextEnabled && GetType() == typeof(ShaderManager) && (name == "sh_Texture2D.vs" || name == "sh_Texture.fs"))
            {
                var snapshot = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                bool unmodified = true;
                foreach (var expected in standard_texture_sources.Value)
                {
                    byte[]? data = expected.Key == name ? rawData : GetRawData(expected.Key);
                    if (data == null || !data.AsSpan().SequenceEqual(expected.Value))
                    {
                        unmodified = false;
                        break;
                    }
                    snapshot.Add(expected.Key, expected.Value);
                }

                if (unmodified)
                {
                    rawData = snapshot[name];
                    shaderStore = new TextureShaderSnapshot(snapshot);
                }
            }

            IShaderPart part = renderer.CreateShaderPart(shaderStore, name, rawData, type);
            if (shaderStore != this)
                standardTextureParts.TryAdd(part, 0);
            return partCache[name] = part;
        }

        private sealed class TextureShaderSnapshot : IShaderStore
        {
            private readonly Dictionary<string, byte[]> sources;

            public TextureShaderSnapshot(Dictionary<string, byte[]> sources) => this.sources = sources;

            public byte[]? GetRawData(string fileName) => sources.GetValueOrDefault(fileName);
        }

        private string ensureValidName(string name, ShaderPartType partType)
        {
            string ending = string.Empty;
            if (string.IsNullOrEmpty(Path.GetExtension(name)))
                ending = getFileEnding(partType);

            if (!name.StartsWith(shader_prefix, StringComparison.Ordinal))
                name = shader_prefix + name;
            if (name.EndsWith(ending, StringComparison.Ordinal))
                return name;

            return name + ending;
        }

        private string getFileEnding(ShaderPartType partType)
        {
            switch (partType)
            {
                case ShaderPartType.Fragment:
                    return @".fs";

                case ShaderPartType.Vertex:
                    return @".vs";
            }

            return string.Empty;
        }

        #region IDisposable Support

        private bool isDisposed;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;

                store.Dispose();

                renderer.ScheduleDisposal(s =>
                {
                    foreach (var shader in s.shaderCache.Values)
                        shader.Dispose();

                    foreach (var part in s.partCache.Values)
                        part.Dispose();
                }, this);
            }
        }

        #endregion
    }

    public static class VertexShaderDescriptor
    {
        public const string TEXTURE_2 = "Texture2D";
        public const string TEXTURE_3 = "Texture3D";
        public const string POSITION = "Position";
    }

    public static class FragmentShaderDescriptor
    {
        public const string TEXTURE = "Texture";
        public const string GLOW = "Glow";
        public const string BLUR = "Blur";
        public const string GRAYSCALE = "Grayscale";
        public const string VIDEO = "Video";
    }
}
