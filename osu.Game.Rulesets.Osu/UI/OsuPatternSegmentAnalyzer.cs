// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    internal enum OsuPatternSegmentKind
    {
        Point,
        Jump,
        Stack,
        BurstFlow,
        Stream
    }

    internal readonly struct OsuPatternState
    {
        public readonly OsuPatternInfo PatternInfo;
        public readonly OsuPatternSegmentKind Candidate;
        public readonly int SegmentStartIndex;
        public readonly int SegmentEndIndex;
        public readonly float JumpSeverity;

        public int SegmentLength => Math.Max(1, SegmentEndIndex - SegmentStartIndex + 1);
        public bool SupportsFlowPath => Candidate is OsuPatternSegmentKind.BurstFlow or OsuPatternSegmentKind.Stream;
        public bool IsCommittedStream => Candidate == OsuPatternSegmentKind.Stream;

        public OsuPatternState(OsuPatternInfo patternInfo, OsuPatternSegmentKind candidate, int segmentStartIndex, int segmentEndIndex, float jumpSeverity)
        {
            PatternInfo = patternInfo;
            Candidate = candidate;
            SegmentStartIndex = segmentStartIndex;
            SegmentEndIndex = segmentEndIndex;
            JumpSeverity = jumpSeverity;
        }
    }

    internal static class OsuPatternSegmentAnalyzer
    {
        public static OsuPatternState[] Analyze(IReadOnlyList<OsuPatternNode> nodes, Func<int, double> beatLengthAt)
        {
            if (nodes.Count == 0)
                return Array.Empty<OsuPatternState>();

            OsuPatternInfo[] patternInfos = new OsuPatternInfo[nodes.Count];
            OsuPatternSegmentKind[] candidates = new OsuPatternSegmentKind[nodes.Count];
            float[] jumpSeverities = new float[nodes.Count];

            for (int i = 0; i < nodes.Count; i++)
            {
                OsuPatternInfo patternInfo = OsuPatternClassifier.Classify(nodes, i, beatLengthAt(i));
                float jumpSeverity = getJumpSeverity(nodes, i, patternInfo);

                patternInfos[i] = patternInfo;
                jumpSeverities[i] = jumpSeverity;
                candidates[i] = classifyCandidate(patternInfo, jumpSeverity);
            }

            smoothCandidates(patternInfos, candidates, jumpSeverities);

            OsuPatternState[] states = new OsuPatternState[nodes.Count];
            int segmentStart = 0;

            while (segmentStart < nodes.Count)
            {
                int segmentEnd = segmentStart;

                while (segmentEnd + 1 < nodes.Count && shouldMergeIntoSameSegment(patternInfos[segmentEnd], patternInfos[segmentEnd + 1], candidates[segmentEnd], candidates[segmentEnd + 1]))
                    segmentEnd++;

                for (int i = segmentStart; i <= segmentEnd; i++)
                    states[i] = new OsuPatternState(patternInfos[i], candidates[i], segmentStart, segmentEnd, jumpSeverities[i]);

                segmentStart = segmentEnd + 1;
            }

            return states;
        }

        private static OsuPatternSegmentKind classifyCandidate(OsuPatternInfo patternInfo, float jumpSeverity)
        {
            if (patternInfo.Kind == OsuPatternKind.Stack)
                return OsuPatternSegmentKind.Stack;

            if (patternInfo.IsContinuousFlow)
                return OsuPatternSegmentKind.Stream;

            if (jumpSeverity >= 0.28f && patternInfo.AverageSpacingRatio >= 2.05f)
                return OsuPatternSegmentKind.Jump;

            if (patternInfo.SupportsFlowAim)
            {
                if (patternInfo.AverageSpacingRatio <= 2.12f || patternInfo.ContinuityWeight >= 0.72f)
                    return OsuPatternSegmentKind.BurstFlow;

                if (jumpSeverity < 0.18f && patternInfo.AverageSpacingRatio <= 2.45f)
                    return OsuPatternSegmentKind.BurstFlow;
            }

            if (jumpSeverity >= 0.18f)
                return OsuPatternSegmentKind.Jump;

            return OsuPatternSegmentKind.Point;
        }

        private static void smoothCandidates(OsuPatternInfo[] patternInfos, OsuPatternSegmentKind[] candidates, float[] jumpSeverities)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] == OsuPatternSegmentKind.BurstFlow)
                {
                    bool touchesCommittedStream = i > 0 && candidates[i - 1] == OsuPatternSegmentKind.Stream
                                                  || i + 1 < candidates.Length && candidates[i + 1] == OsuPatternSegmentKind.Stream;

                    if (touchesCommittedStream)
                        candidates[i] = OsuPatternSegmentKind.Stream;
                }
            }

            for (int i = 1; i < candidates.Length - 1; i++)
            {
                if (candidates[i - 1] != candidates[i + 1])
                    continue;

                if (candidates[i - 1] is not (OsuPatternSegmentKind.Stream or OsuPatternSegmentKind.BurstFlow))
                    continue;

                if (!patternInfos[i].SupportsFlowAim || jumpSeverities[i] >= 0.24f)
                    continue;

                if (candidates[i] is OsuPatternSegmentKind.Point or OsuPatternSegmentKind.Stack or OsuPatternSegmentKind.Jump)
                    candidates[i] = candidates[i - 1];
            }

            for (int i = 0; i < candidates.Length - 1; i++)
            {
                if (candidates[i] is not (OsuPatternSegmentKind.Point or OsuPatternSegmentKind.Jump))
                    continue;

                if (candidates[i + 1] != OsuPatternSegmentKind.Stream)
                    continue;

                if (patternInfos[i].Kind != OsuPatternKind.Burst || patternInfos[i].ChainLength < 3)
                    continue;

                if (jumpSeverities[i] >= 0.18f)
                    continue;

                candidates[i] = OsuPatternSegmentKind.Stream;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != OsuPatternSegmentKind.Point)
                    continue;

                bool touchesCommittedStream = i > 0 && candidates[i - 1] == OsuPatternSegmentKind.Stream
                                              || i + 1 < candidates.Length && candidates[i + 1] == OsuPatternSegmentKind.Stream;

                if (!touchesCommittedStream || !patternInfos[i].SupportsFlowAim || jumpSeverities[i] >= 0.16f)
                    continue;

                candidates[i] = OsuPatternSegmentKind.Stream;
            }
        }

        private static bool shouldMergeIntoSameSegment(OsuPatternInfo current, OsuPatternInfo next, OsuPatternSegmentKind currentCandidate, OsuPatternSegmentKind nextCandidate)
        {
            if (currentCandidate != nextCandidate)
                return false;

            return currentCandidate switch
            {
                OsuPatternSegmentKind.Stream => current.StreamShape == next.StreamShape && current.StreamSpacing == next.StreamSpacing,
                OsuPatternSegmentKind.BurstFlow => true,
                OsuPatternSegmentKind.Jump => true,
                OsuPatternSegmentKind.Stack => true,
                _ => false,
            };
        }

        private static float getJumpSeverity(IReadOnlyList<OsuPatternNode> nodes, int currentIndex, OsuPatternInfo patternInfo)
        {
            if (patternInfo.IsContinuousFlow)
                return 0;

            float maxSpacingRatio = 0;

            if (currentIndex > 0)
                maxSpacingRatio = Math.Max(maxSpacingRatio, getSpacingRatio(nodes[currentIndex - 1], nodes[currentIndex]));

            if (currentIndex + 1 < nodes.Count)
                maxSpacingRatio = Math.Max(maxSpacingRatio, getSpacingRatio(nodes[currentIndex], nodes[currentIndex + 1]));

            return Math.Clamp((maxSpacingRatio - 1.45f) / 1.75f, 0, 1);
        }

        private static float getSpacingRatio(OsuPatternNode first, OsuPatternNode second)
        {
            float radius = Math.Max(1f, Math.Min(first.Radius, second.Radius));
            return (second.Position - first.Position).Length / (radius * 2f);
        }
    }
}
