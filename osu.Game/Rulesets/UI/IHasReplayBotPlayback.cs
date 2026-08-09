// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using JetBrains.Annotations;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.UI
{
    public interface IHasReplayBotPlayback
    {
        void SetReplayBotScore([CanBeNull] Score replayBotScore);
    }
}
