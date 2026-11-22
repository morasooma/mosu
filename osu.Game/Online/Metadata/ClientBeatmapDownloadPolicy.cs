using System;

namespace osu.Game.Online.Metadata
{
    internal static class ClientBeatmapDownloadPolicy
    {
        // This grants remote download authority, so do not reuse the broader presence
        // detector (which also accepts HTTP, arbitrary paths and release loopback URLs).
        public static bool IsTrusted(EndpointConfiguration endpoints, bool thirdParty, bool stableProtocol)
        {
            if (thirdParty || stableProtocol ||
                !Uri.TryCreate(endpoints.APIUrl, UriKind.Absolute, out var api) ||
                !Uri.TryCreate(endpoints.MetadataUrl, UriKind.Absolute, out var metadata))
                return false;

            return trustedOrigin(api) && trustedOrigin(metadata) &&
                   api.GetLeftPart(UriPartial.Authority).Equals(metadata.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) &&
                   api.AbsolutePath == "/" && metadata.AbsolutePath == "/metadata" &&
                   string.IsNullOrEmpty(api.UserInfo) && string.IsNullOrEmpty(metadata.UserInfo) &&
                   string.IsNullOrEmpty(api.Query) && string.IsNullOrEmpty(metadata.Query) &&
                   string.IsNullOrEmpty(api.Fragment) && string.IsNullOrEmpty(metadata.Fragment);
        }

        private static bool trustedOrigin(Uri uri)
        {
            if (uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443 &&
                uri.IdnHost is "morasooma.net" or "proxy.morasooma.net" or "proxy2.morasooma.net")
                return true;

#if DEBUG
            // Only the explicit development gateway, never an arbitrary loopback service.
            return uri.Scheme == Uri.UriSchemeHttp && uri.Host == "127.0.0.1" && uri.Port == 18082;
#else
            return false;
#endif
        }
    }
}
