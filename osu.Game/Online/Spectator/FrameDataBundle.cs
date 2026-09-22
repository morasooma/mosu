// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Replays.Legacy;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Online.Spectator
{
    [Serializable]
    [MessagePackObject]
    public class FrameDataBundle
    {
        [Key(0)]
        public FrameHeader Header { get; set; }

        [Key(1)]
        public IList<LegacyReplayFrame> Frames { get; set; }

        /// <summary>
        /// Optional labelled cursor tracks for a Tag Co-op replay. They do not participate in
        /// normal replay playback and are ignored by older/default clients.
        /// </summary>
        [Key(2)]
        public IList<TagCoopReplayFrame> TagCoopFrames { get; set; }

        /// <summary>
        /// Whether <see cref="TagCoopFrames"/> replaces lower-quality live samples already
        /// received for this play session.
        /// </summary>
        [Key(3)]
        public bool ReplaceTagCoopReplay { get; set; }

        public FrameDataBundle(ScoreInfo score, ScoreProcessor scoreProcessor, IList<LegacyReplayFrame> frames, IList<TagCoopReplayFrame>? tagCoopFrames = null)
        {
            Frames = frames;
            TagCoopFrames = tagCoopFrames ?? Array.Empty<TagCoopReplayFrame>();
            Header = new FrameHeader(score, scoreProcessor.GetScoreProcessorStatistics());
        }

        [JsonConstructor]
        public FrameDataBundle(FrameHeader header, IList<LegacyReplayFrame> frames, IList<TagCoopReplayFrame>? tagCoopFrames = null)
        {
            Header = header;
            Frames = frames;
            TagCoopFrames = tagCoopFrames ?? Array.Empty<TagCoopReplayFrame>();
        }
    }
}
