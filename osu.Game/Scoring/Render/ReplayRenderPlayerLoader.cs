// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Screens.Play;

namespace osu.Game.Scoring.Render
{
    internal partial class ReplayRenderPlayerLoader : PlayerLoader
    {
        protected override double PlayerPushDelay => 0;

        protected override bool ReadyForGameplay => true;

        protected override bool UseHighPerformanceSession => false;

        public override bool AllowUserExit => false;

        public ReplayRenderPlayerLoader(Func<Player> createPlayer)
            : base(createPlayer)
        {
        }
    }
}
