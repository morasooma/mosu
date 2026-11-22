using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Beatmaps
{
    internal static class SegmentTimingClone
    {
        private static readonly JsonSerializerSettings settings = createSettings();

        public static T Clone<T>(T obj) where T : HitObject
            => (T)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(obj, settings), obj.GetType(), settings)!;

        private static JsonSerializerSettings createSettings()
        {
            var result = JsonSerializableExtensions.CreateGlobalSettings();
            result.ContractResolver = new ObjectContractResolver();
            return result;
        }

        private sealed class ObjectContractResolver : SnakeCaseKeyContractResolver
        {
            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                // Copy authored state, not computed gameplay data (some getters require
                // defaults/nested objects which don't exist on decoded native objects yet).
                if (member.DeclaringType != null && typeof(HitObject).IsAssignableFrom(member.DeclaringType)
                    && member is PropertyInfo info
                    && (info.SetMethod?.IsPublic != true || info.Name == "EndTime"))
                    property.Ignored = true;
                return property;
            }
        }
    }
}
