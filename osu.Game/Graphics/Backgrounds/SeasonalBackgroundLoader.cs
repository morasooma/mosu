// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Graphics.Backgrounds
{
    public partial class SeasonalBackgroundLoader : Component
    {
        /// <summary>
        /// Fired when background should be changed due to receiving backgrounds from API
        /// or when the user setting is changed (as it might require unloading the seasonal background).
        /// </summary>
        public event Action SeasonalBackgroundChanged;

        [Resolved]
        private IAPIProvider api { get; set; }

        private Bindable<SeasonalBackgroundMode> seasonalBackgroundMode;
        private Bindable<APISeasonalBackgrounds> seasonalBackgrounds;

        private int current;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, SessionStatics sessionStatics)
        {
            seasonalBackgroundMode = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode);
            seasonalBackgroundMode.BindValueChanged(_ => SeasonalBackgroundChanged?.Invoke());

            seasonalBackgrounds = sessionStatics.GetBindable<APISeasonalBackgrounds>(Static.SeasonalBackgrounds);
            seasonalBackgrounds.BindValueChanged(_ =>
            {
                if (shouldShowSeasonal)
                    SeasonalBackgroundChanged?.Invoke();
            });

            fetchSeasonalBackgrounds();
        }

        private void fetchSeasonalBackgrounds()
        {
            if (seasonalBackgrounds.Value != null)
                return;

            var request = new GetSeasonalBackgroundsRequest();
            request.Success += response =>
            {
                seasonalBackgrounds.Value = response;
                current = RNG.Next(0, response.Backgrounds?.Count ?? 0);
            };

            api.PerformAsync(request);
        }

        public SeasonalBackground LoadNextBackground()
        {
            if (!shouldShowSeasonal)
                return null;

            var backgrounds = seasonalBackgrounds.Value.Backgrounds;

            current = (current + 1) % backgrounds.Count;
            APISeasonalBackground background = backgrounds[current];

            return new SeasonalBackground(getCacheAwareUrl(background, api.Endpoints.APIUrl), background.Hash);
        }

        private static string getCacheAwareUrl(APISeasonalBackground background, string apiUrl)
        {
            if (string.IsNullOrWhiteSpace(background.Hash) ||
                !Regex.IsMatch(background.Hash, "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant) ||
                !Uri.TryCreate(background.Url, UriKind.Absolute, out Uri uri) ||
                !Uri.TryCreate(apiUrl, UriKind.Absolute, out Uri apiUri) ||
                !string.Equals(uri.Host, apiUri.Host, StringComparison.OrdinalIgnoreCase) ||
                uri.Port != apiUri.Port ||
                !uri.AbsolutePath.StartsWith("/file/seasonal-backgrounds/", StringComparison.OrdinalIgnoreCase))
                return background.Url;

            var builder = new UriBuilder(uri);
            string hashParameter = "mosu_hash=" + Uri.EscapeDataString(background.Hash.ToLowerInvariant());
            builder.Query = string.IsNullOrEmpty(builder.Query)
                ? hashParameter
                : builder.Query.TrimStart('?') + "&" + hashParameter;
            return builder.Uri.AbsoluteUri;
        }

        private bool shouldShowSeasonal
        {
            get
            {
                if (seasonalBackgroundMode.Value == SeasonalBackgroundMode.Never)
                    return false;

                if (seasonalBackgroundMode.Value == SeasonalBackgroundMode.Sometimes && !isInSeason)
                    return false;

                return seasonalBackgrounds.Value?.Backgrounds?.Any() == true;
            }
        }

        private bool isInSeason => seasonalBackgrounds.Value != null && DateTimeOffset.Now < seasonalBackgrounds.Value.EndDate;
    }

    [LongRunningLoad]
    public partial class SeasonalBackground : Background
    {
        private readonly string url;
        private readonly string hash;
        private const string fallback_texture_name = @"Backgrounds/bg1";

        public SeasonalBackground(string url, string hash = null)
        {
            this.url = url;
            this.hash = hash;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, OnlineAssetCachingStore onlineTextures)
        {
            Sprite.Texture = (!string.IsNullOrWhiteSpace(hash) ? onlineTextures.GetSeasonalBackground(url, hash) : null)
                             ?? textures.Get(url)
                             ?? textures.Get(fallback_texture_name);
        }

        public override bool Equals(Background other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return other.GetType() == GetType()
                   && ((SeasonalBackground)other).url == url;
        }
    }
}
