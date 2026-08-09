// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Online
{
    /// <summary>
    /// Non-secret identity for builds produced from the public source snapshot.
    /// Production credentials and score proof material exist only in the private release build.
    /// </summary>
    public static class MosuClientAuthentication
    {
        public const string HeaderName = @"X-Mosu-Client-Key";
        public const string HeaderValue = @"";
        public const string OAuthClientId = @"";
        public const string OAuthClientSecret = @"";

        public static bool ScoreSubmissionEnabled => false;
    }
}
