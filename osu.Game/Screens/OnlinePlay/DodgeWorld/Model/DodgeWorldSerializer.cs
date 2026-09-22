// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// Reads and writes <see cref="DodgeWorldDocument"/>, migrating documents written by clients
    /// that serialised C# property names instead of the current snake_case field names.
    /// </summary>
    /// <remarks>
    /// The legacy name of a field is always the PascalCase C# property name, so the migration is
    /// derived from the model types by reflection rather than maintained as a hand-written list.
    /// Adding a field to a model is therefore enough — it becomes migratable on its own.
    /// </remarks>
    public static class DodgeWorldSerializer
    {
        public const string FILENAME = "world-v1.json";

        private static readonly ConcurrentDictionary<Type, FieldMigration[]> migrations = new ConcurrentDictionary<Type, FieldMigration[]>();

        /// <summary>
        /// Parses a world document from JSON, applying legacy field-name migration first.
        /// </summary>
        /// <returns>The document, or <c>null</c> if the JSON does not describe one.</returns>
        public static DodgeWorldDocument? Deserialize(string json)
        {
            var root = JObject.Parse(json);
            MigrateLegacyFieldNames(root, typeof(DodgeWorldDocument));
            return root.ToObject<DodgeWorldDocument>();
        }

        public static string Serialize(DodgeWorldDocument document, bool indented = false) =>
            JsonConvert.SerializeObject(document, indented ? Formatting.Indented : Formatting.None);

        /// <summary>
        /// A deep copy of one placed object.
        /// </summary>
        /// <remarks>
        /// Through the wire format rather than field by field, so a field added to the record is copied
        /// without anybody remembering to copy it, and so the copy shares no dialogue array with the
        /// original — editing one would otherwise edit both.
        /// </remarks>
        public static EntityRecord Clone(EntityRecord record) =>
            JsonConvert.DeserializeObject<EntityRecord>(JsonConvert.SerializeObject(record))
            ?? throw new InvalidOperationException("A placed object could not be copied.");

        /// <summary>
        /// Rewrites any PascalCase keys in <paramref name="node"/> to the names the model expects,
        /// recursing through nested model collections.
        /// </summary>
        private static void MigrateLegacyFieldNames(JObject node, Type modelType)
        {
            foreach (FieldMigration field in describe(modelType))
            {
                applyRename(node, field.LegacyName, field.CurrentName);

                if (field.ElementType == null || node[field.CurrentName] is not JArray children)
                    continue;

                foreach (JObject child in children.OfType<JObject>())
                    MigrateLegacyFieldNames(child, field.ElementType);
            }
        }

        /// <summary>
        /// Moves <paramref name="legacyName"/> to <paramref name="currentName"/>.
        /// </summary>
        /// <remarks>
        /// When both are present the current name wins and the legacy key is dropped, so that
        /// deserialisation cannot depend on which property Newtonsoft happens to visit last.
        /// </remarks>
        private static void applyRename(JObject node, string legacyName, string currentName)
        {
            if (legacyName == currentName || node.Property(legacyName) is not JProperty legacy)
                return;

            if (node[currentName] is not null)
            {
                legacy.Remove();
                return;
            }

            legacy.Replace(new JProperty(currentName, legacy.Value));
        }

        private static FieldMigration[] describe(Type modelType) => migrations.GetOrAdd(modelType, static type =>
        {
            var described = new List<FieldMigration>();

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string currentName = property.GetCustomAttribute<JsonPropertyAttribute>()?.PropertyName ?? property.Name;

                if (property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                    continue;

                described.Add(new FieldMigration(property.Name, currentName, elementTypeOf(property.PropertyType)));
            }

            return described.ToArray();
        });

        /// <summary>
        /// The model type held by a collection property, or <c>null</c> for anything else.
        /// </summary>
        private static Type? elementTypeOf(Type propertyType)
        {
            if (!propertyType.IsGenericType || propertyType.GetGenericTypeDefinition() != typeof(List<>))
                return null;

            Type element = propertyType.GetGenericArguments()[0];
            return element.Namespace == typeof(DodgeWorldDocument).Namespace ? element : null;
        }

        private readonly record struct FieldMigration(string LegacyName, string CurrentName, Type? ElementType);
    }
}
