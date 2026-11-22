using System;
using System.Net.Http;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace osu.Game.Online.Chat
{
    /// <summary>
    /// Best-effort direct translation using Google's unofficial web endpoint. No keys or Mosu server involved;
    /// failure must never prevent the original chat message from being displayed.
    /// </summary>
    public static class ChatTranslator
    {
        private static readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        public static async Task<ChatTranslationResult?> TranslateAsync(string text, string target)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 2000 ||
                target.Length is < 2 or > 12 || !target.All(c => char.IsAsciiLetter(c) || c == '-'))
                return null;

            try
            {
                string url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=" +
                             Uri.EscapeDataString(target) + "&dt=t&dj=1&q=" + Uri.EscapeDataString(text);
                using var response = await client.GetAsync(url).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    return null;

                if (response.Content.Headers.ContentLength is > 32_768)
                    return null;
                await using var body = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var json = await JsonDocument.ParseAsync(body, new JsonDocumentOptions { MaxDepth = 8 }).ConfigureAwait(false);
                var root = json.RootElement;
                if (!root.TryGetProperty("sentences", out var sentences) || sentences.ValueKind != JsonValueKind.Array)
                    return null;

                var translatedBuilder = new StringBuilder();
                foreach (var sentence in sentences.EnumerateArray())
                {
                    if (sentence.ValueKind != JsonValueKind.Object || !sentence.TryGetProperty("trans", out var value) || value.ValueKind != JsonValueKind.String)
                        continue;
                    translatedBuilder.Append(value.GetString());
                    if (translatedBuilder.Length > 6000)
                        return null;
                }
                string translated = translatedBuilder.ToString();
                if (string.IsNullOrWhiteSpace(translated) || translated.Length > 6000)
                    return null;

                string? language = root.TryGetProperty("src", out var source) && source.ValueKind == JsonValueKind.String
                    ? source.GetString()
                    : null;
                return new ChatTranslationResult(translated, language);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or System.IO.IOException)
            {
                return null;
            }
        }
    }

    public sealed record ChatTranslationResult(string TranslatedText, string? SourceLanguage);
}
