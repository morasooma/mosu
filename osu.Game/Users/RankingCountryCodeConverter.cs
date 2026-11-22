using System;
using Newtonsoft.Json;

namespace osu.Game.Users
{
    /// <summary>
    /// Ranking feeds may contain country codes not represented by the client's country list.
    /// Keep the rest of the leaderboard visible when that happens.
    /// </summary>
    public class RankingCountryCodeConverter : JsonConverter<CountryCode>
    {
        public override CountryCode ReadJson(JsonReader reader, Type objectType, CountryCode existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String && reader.Value is string code
                && Enum.TryParse(code, false, out CountryCode country) && Enum.IsDefined(country))
                return country;

            return CountryCode.Unknown;
        }

        public override void WriteJson(JsonWriter writer, CountryCode value, JsonSerializer serializer) => writer.WriteValue(value.ToString());
    }
}
