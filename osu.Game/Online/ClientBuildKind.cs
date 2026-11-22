using System;
using osu.Game.Configuration;

namespace osu.Game.Online
{
    public static class ClientBuildKind
    {
        // Stable binaries are also packaged for the protected dev feed. Presence
        // describes the selected stream, not the immutable compilation suffix.
        // Custom builds must not become official merely by selecting a stream.
        public static string FromReleaseStream(string? version, ReleaseStream stream)
            => FromInformationalVersion(version) == "Custom"
                ? "Custom"
                : stream == ReleaseStream.DevBuild ? "Dev" : "Release";

        public static string FromInformationalVersion(string? version)
        {
            int separator = version?.IndexOf('+') ?? -1;
            if (separator < 0)
                return "Custom";

            // The .NET SDK appends SourceRevisionId: +dev.<commit>, +stable.<commit>.
            string channel = version![(separator + 1)..].Split('.')[0];
            return channel switch
            {
                "dev" or "dev-linux" => "Dev",
                "stable" or "release" or "stable-linux" => "Release",
                _ => "Custom"
            };
        }
    }
}
