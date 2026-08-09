// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using Newtonsoft.Json;

namespace osu.Game.Skinning
{
    public sealed class SerialisedDrawableTypeConverter : JsonConverter<Type>
    {
        public override void WriteJson(JsonWriter writer, Type? value, JsonSerializer serializer)
            => writer.WriteValue(value?.AssemblyQualifiedName ?? value?.FullName);

        public override Type ReadJson(JsonReader reader, Type objectType, Type? existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                throw new JsonSerializationException("Drawable type cannot be null.");

            if (reader.TokenType != JsonToken.String || reader.Value is not string typeName || string.IsNullOrWhiteSpace(typeName))
                throw new JsonSerializationException($"Expected drawable type name string but got {reader.TokenType}.");

            Type? resolvedType = resolveType(normaliseLegacyTypeName(typeName));

            if (resolvedType == null)
                throw new JsonSerializationException($"Unable to resolve drawable type '{typeName}'.");

            return resolvedType;
        }

        private static string normaliseLegacyTypeName(string typeName)
        {
            return typeName.Replace(@"osu.Game.Screens.Play.SongProgress", @"osu.Game.Screens.Play.HUD.DefaultSongProgress")
                           .Replace(@"osu.Game.Screens.Play.HUD.LegacyComboCounter", @"osu.Game.Skinning.LegacyComboCounter")
                           .Replace(@"osu.Game.Skinning.LegacyComboCounter", @"osu.Game.Skinning.LegacyDefaultComboCounter")
                           .Replace(@"osu.Game.Screens.Play.HUD.PerformancePointsCounter", @"osu.Game.Skinning.Triangles.TrianglesPerformancePointsCounter")
                           .Replace(@"osu.Game.Screens.Play.HUD.UnstableRateCounter", @"osu.Game.Skinning.Triangles.TrianglesUnstableRateCounter");
        }

        private static Type? resolveType(string typeName)
        {
            Type? direct = Type.GetType(typeName, false);
            if (direct != null)
                return direct;

            int separatorIndex = typeName.IndexOf(',');
            string fullName = separatorIndex >= 0 ? typeName[..separatorIndex].Trim() : typeName.Trim();
            string? assemblyName = null;

            if (separatorIndex >= 0)
                assemblyName = typeName[(separatorIndex + 1)..].Split(',')[0].Trim();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.IsNullOrEmpty(assemblyName) && !string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
                    continue;

                Type? resolved = assembly.GetType(fullName, false);
                if (resolved != null)
                    return resolved;
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                            .Select(assembly => assembly.GetType(fullName, false))
                            .FirstOrDefault(type => type != null);
        }
    }
}
