// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

namespace osu.Game.Users
{
    public class Medal
    {
        public string Name { get; set; }
        public string InternalName { get; set; }
        public string CoverUrl { get; set; }
        public string ImageUrl => MorasoomaMedalAssets.Resolve(InternalName, CoverUrl);
        public string Description { get; set; }
    }
}
