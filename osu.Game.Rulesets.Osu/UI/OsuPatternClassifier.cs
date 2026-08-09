// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    internal enum OsuPatternKind
    {
        Single,
        Stack,
        Burst,
        Stream
    }

    internal enum OsuStreamShapeKind
    {
        None,
        Straight,
        Arc,
        ZigZag
    }

    internal enum OsuStreamSpacingKind
    {
        None,
        Tight,
        Even,
        Variable,
        Spaced
    }

    internal readonly struct OsuPatternNode
    {
        public readonly Vector2 Position;
        public readonly double Time;
        public readonly float Radius;
        public readonly bool IsCircle;

        public OsuPatternNode(Vector2 position, double time, float radius, bool isCircle)
        {
            Position = position;
            Time = time;
            Radius = radius;
            IsCircle = isCircle;
        }
    }

    internal readonly struct OsuPatternInfo
    {
        public readonly OsuPatternKind Kind;
        public readonly OsuStreamShapeKind StreamShape;
        public readonly OsuStreamSpacingKind StreamSpacing;
        public readonly int ChainStartIndex;
        public readonly int ChainEndIndex;
        public readonly float DensityWeight;
        public readonly float ContinuityWeight;
        public readonly float AverageSpacingRatio;
        public readonly float SpacingVariance;

        public int ChainLength => Math.Max(1, ChainEndIndex - ChainStartIndex + 1);
        public bool IsBurstLike => Kind is OsuPatternKind.Burst or OsuPatternKind.Stream;
        public bool IsContinuousFlow => Kind == OsuPatternKind.Stream;
        public bool SupportsFlowAim => Kind == OsuPatternKind.Stream || ChainLength >= 4 && ContinuityWeight >= 0.5f || ChainLength >= 3 && ContinuityWeight >= 0.58f;
        public bool SupportsRailHandoff => Kind == OsuPatternKind.Stream || ChainLength >= 4 && ContinuityWeight >= 0.58f || ChainLength >= 3 && ContinuityWeight >= 0.66f;
        public bool IsLowDensityFlow => SupportsFlowAim && DensityWeight < 0.42f;
        public bool IsGentleFlow => SupportsFlowAim
                                    && DensityWeight < 0.38f
                                    && AverageSpacingRatio <= 1.85f
                                    && ContinuityWeight >= 0.62f
                                    && (Kind != OsuPatternKind.Stream
                                        || StreamShape != OsuStreamShapeKind.ZigZag
                                        && StreamSpacing is not OsuStreamSpacingKind.Spaced and not OsuStreamSpacingKind.Variable);
        public bool IsSpacedStream => Kind == OsuPatternKind.Stream && StreamSpacing == OsuStreamSpacingKind.Spaced;
        public bool IsVariableStream => Kind == OsuPatternKind.Stream && StreamSpacing == OsuStreamSpacingKind.Variable;
        public bool IsZigZagStream => Kind == OsuPatternKind.Stream && StreamShape == OsuStreamShapeKind.ZigZag;
        public bool IsArcStream => Kind == OsuPatternKind.Stream && StreamShape == OsuStreamShapeKind.Arc;

        public OsuPatternInfo(OsuPatternKind kind, OsuStreamShapeKind streamShape, OsuStreamSpacingKind streamSpacing, int chainStartIndex, int chainEndIndex,
                              float densityWeight, float continuityWeight, float averageSpacingRatio, float spacingVariance)
        {
            Kind = kind;
            StreamShape = streamShape;
            StreamSpacing = streamSpacing;
            ChainStartIndex = chainStartIndex;
            ChainEndIndex = chainEndIndex;
            DensityWeight = densityWeight;
            ContinuityWeight = continuityWeight;
            AverageSpacingRatio = averageSpacingRatio;
            SpacingVariance = spacingVariance;
        }
    }

    internal static class OsuPatternClassifier
    {
        public static OsuPatternInfo Classify(IReadOnlyList<OsuPatternNode> nodes, int currentIndex, double beatLength)
        {
            if (nodes.Count == 0 || currentIndex < 0 || currentIndex >= nodes.Count)
                return new OsuPatternInfo(OsuPatternKind.Single, OsuStreamShapeKind.None, OsuStreamSpacingKind.None, currentIndex, currentIndex, 0, 0, 0, 0);

            OsuPatternNode current = nodes[currentIndex];

            if (!current.IsCircle)
                return new OsuPatternInfo(OsuPatternKind.Single, OsuStreamShapeKind.None, OsuStreamSpacingKind.None, currentIndex, currentIndex, 0, 0, 0, 0);

            bool stackedCandidate = isStackedNode(nodes, currentIndex);

            int chainStart = currentIndex;
            int chainEnd = currentIndex;

            while (chainStart > 0 && IsFlowConnection(nodes[chainStart - 1], nodes[chainStart], beatLength))
                chainStart--;

            while (chainEnd + 1 < nodes.Count && IsFlowConnection(nodes[chainEnd], nodes[chainEnd + 1], beatLength))
                chainEnd++;

            if (stackedCandidate && shouldClassifyAsStack(nodes, currentIndex, chainStart, chainEnd))
                return new OsuPatternInfo(OsuPatternKind.Stack, OsuStreamShapeKind.None, OsuStreamSpacingKind.None, currentIndex, currentIndex, 0, 0, 0, 0);

            if (chainEnd <= chainStart)
                return new OsuPatternInfo(OsuPatternKind.Single, OsuStreamShapeKind.None, OsuStreamSpacingKind.None, currentIndex, currentIndex, 0, 0, 0, 0);

            int segmentCount = chainEnd - chainStart;
            double totalGap = 0;
            double maxGap = 0;
            float totalSpacingRatio = 0;
            float maxSpacingRatio = 0;
            float minSpacingRatio = float.MaxValue;
            int spacingCount = 0;
            int turnCount = 0;
            float turnDotSum = 0;
            float turnAbsDotSum = 0;
            float maxTurnCross = 0;
            int zigZagFlipCount = 0;
            float? lastMeaningfulCrossSign = null;

            for (int i = chainStart; i < chainEnd; i++)
            {
                double gap = Math.Max(0, nodes[i + 1].Time - nodes[i].Time);
                float spacingRatio = getSpacingRatio(nodes[i], nodes[i + 1]);

                totalGap += gap;
                maxGap = Math.Max(maxGap, gap);
                totalSpacingRatio += spacingRatio;
                maxSpacingRatio = Math.Max(maxSpacingRatio, spacingRatio);
                minSpacingRatio = Math.Min(minSpacingRatio, spacingRatio);
                spacingCount++;
            }

            for (int i = chainStart + 1; i < chainEnd; i++)
            {
                Vector2 previousDirection = normaliseOrZero(nodes[i].Position - nodes[i - 1].Position);
                Vector2 nextDirection = normaliseOrZero(nodes[i + 1].Position - nodes[i].Position);

                if (previousDirection.LengthSquared <= 0.0001f || nextDirection.LengthSquared <= 0.0001f)
                    continue;

                float dot = Math.Clamp(Vector2.Dot(previousDirection, nextDirection), -1, 1);
                float cross = previousDirection.X * nextDirection.Y - previousDirection.Y * nextDirection.X;
                float absCross = Math.Abs(cross);

                turnDotSum += dot;
                turnAbsDotSum += Math.Abs(dot);
                maxTurnCross = Math.Max(maxTurnCross, absCross);
                turnCount++;

                if (absCross < 0.16f)
                    continue;

                float sign = MathF.Sign(cross);

                if (lastMeaningfulCrossSign.HasValue && sign != lastMeaningfulCrossSign.Value)
                    zigZagFlipCount++;

                lastMeaningfulCrossSign = sign;
            }

            double averageGap = segmentCount > 0 ? totalGap / segmentCount : double.PositiveInfinity;
            float averageSpacingRatio = spacingCount > 0 ? totalSpacingRatio / spacingCount : 0;
            float spacingVariance = computeSpacingVariance(nodes, chainStart, chainEnd, averageSpacingRatio);
            float gapVariance = computeGapVariance(nodes, chainStart, chainEnd, averageGap);
            float densityWeight = getDensityWeight(averageGap, maxGap, beatLength);
            float continuityWeight = getContinuityWeight(gapVariance, spacingVariance, turnCount > 0 ? turnAbsDotSum / turnCount : 1f, zigZagFlipCount);

            OsuPatternKind kind = classifyPatternKind(chainEnd - chainStart + 1, densityWeight, continuityWeight, averageGap, beatLength);
            OsuStreamShapeKind shape = kind == OsuPatternKind.Stream
                ? classifyStreamShape(turnCount > 0 ? turnDotSum / turnCount : 1f, turnCount > 0 ? turnAbsDotSum / turnCount : 1f, maxTurnCross, zigZagFlipCount)
                : OsuStreamShapeKind.None;
            OsuStreamSpacingKind spacing = kind == OsuPatternKind.Stream
                ? classifyStreamSpacing(averageSpacingRatio, spacingVariance, minSpacingRatio, maxSpacingRatio)
                : OsuStreamSpacingKind.None;

            return new OsuPatternInfo(kind, shape, spacing, chainStart, chainEnd, densityWeight, continuityWeight, averageSpacingRatio, spacingVariance);
        }

        private static OsuPatternKind classifyPatternKind(int chainLength, float densityWeight, float continuityWeight, double averageGap, double beatLength)
        {
            if (chainLength >= 4 && densityWeight >= 0.16f)
                return OsuPatternKind.Stream;

            if (chainLength >= 4 && continuityWeight >= 0.62f && averageGap <= Math.Clamp(beatLength * 1.02, 110, 290))
                return OsuPatternKind.Stream;

            if (chainLength >= 5 && continuityWeight >= 0.52f && averageGap <= Math.Clamp(beatLength * 1.12, 130, 315))
                return OsuPatternKind.Stream;

            if (chainLength >= 3)
                return OsuPatternKind.Burst;

            return chainLength >= 2 ? OsuPatternKind.Burst : OsuPatternKind.Single;
        }

        private static OsuStreamShapeKind classifyStreamShape(float averageDot, float averageAbsDot, float maxTurnCross, int zigZagFlipCount)
        {
            if (zigZagFlipCount > 0 && averageAbsDot <= 0.96f)
                return OsuStreamShapeKind.ZigZag;

            if (averageDot >= 0.84f && maxTurnCross <= 0.42f)
                return OsuStreamShapeKind.Straight;

            return OsuStreamShapeKind.Arc;
        }

        private static OsuStreamSpacingKind classifyStreamSpacing(float averageSpacingRatio, float spacingVariance, float minSpacingRatio, float maxSpacingRatio)
        {
            if (averageSpacingRatio >= 2.35f || maxSpacingRatio >= 3.1f)
                return OsuStreamSpacingKind.Spaced;

            if (spacingVariance >= 0.22f || (minSpacingRatio > 0 && maxSpacingRatio / minSpacingRatio >= 1.7f))
                return OsuStreamSpacingKind.Variable;

            if (averageSpacingRatio <= 1.08f)
                return OsuStreamSpacingKind.Tight;

            return OsuStreamSpacingKind.Even;
        }

        private static float getDensityWeight(double averageGap, double maxGap, double beatLength)
        {
            double denseReference = Math.Clamp(beatLength * 0.42, 58, 145);
            double looseReference = Math.Clamp(beatLength * 0.82, 95, 240);
            float averageWeight = (float)Math.Clamp((looseReference - averageGap) / Math.Max(1, looseReference - denseReference), 0, 1);
            float maxWeight = (float)Math.Clamp((looseReference * 1.08 - maxGap) / Math.Max(1, looseReference - denseReference), 0, 1);
            return Math.Clamp(averageWeight * 0.72f + maxWeight * 0.28f, 0, 1);
        }

        private static float getContinuityWeight(float gapVariance, float spacingVariance, float averageAbsDot, int zigZagFlipCount)
        {
            float gapConsistency = 1 - Math.Clamp(gapVariance / 0.42f, 0, 1);
            float spacingConsistency = 1 - Math.Clamp(spacingVariance / 0.42f, 0, 1);
            float shapeConsistency = zigZagFlipCount > 0
                ? Math.Clamp(0.56f + averageAbsDot * 0.44f, 0, 1)
                : Math.Clamp((averageAbsDot - 0.12f) / 0.88f, 0, 1);
            return Math.Clamp(gapConsistency * 0.4f + spacingConsistency * 0.28f + shapeConsistency * 0.32f, 0, 1);
        }

        private static float computeSpacingVariance(IReadOnlyList<OsuPatternNode> nodes, int chainStart, int chainEnd, float averageSpacingRatio)
        {
            if (chainEnd <= chainStart || averageSpacingRatio <= 0.0001f)
                return 0;

            float variance = 0;
            int count = 0;

            for (int i = chainStart; i < chainEnd; i++)
            {
                float spacingRatio = getSpacingRatio(nodes[i], nodes[i + 1]);
                float delta = spacingRatio - averageSpacingRatio;
                variance += delta * delta;
                count++;
            }

            return count > 0
                ? MathF.Sqrt(variance / count) / Math.Max(averageSpacingRatio, 0.0001f)
                : 0;
        }

        private static float computeGapVariance(IReadOnlyList<OsuPatternNode> nodes, int chainStart, int chainEnd, double averageGap)
        {
            if (chainEnd <= chainStart || averageGap <= 0.0001)
                return 0;

            double variance = 0;
            int count = 0;

            for (int i = chainStart; i < chainEnd; i++)
            {
                double gap = Math.Max(0, nodes[i + 1].Time - nodes[i].Time);
                double delta = gap - averageGap;
                variance += delta * delta;
                count++;
            }

            return count > 0
                ? (float)(Math.Sqrt(variance / count) / Math.Max(averageGap, 0.0001))
                : 0;
        }

        private static bool isStackedNode(IReadOnlyList<OsuPatternNode> nodes, int index)
        {
            OsuPatternNode current = nodes[index];
            float stackThreshold = Math.Max(6f, current.Radius * 0.34f);

            return isStackedNeighbour(index - 1) || isStackedNeighbour(index + 1);

            bool isStackedNeighbour(int neighbourIndex)
            {
                if (neighbourIndex < 0 || neighbourIndex >= nodes.Count)
                    return false;

                OsuPatternNode neighbour = nodes[neighbourIndex];

                if (!neighbour.IsCircle)
                    return false;

                return (neighbour.Position - current.Position).Length <= stackThreshold;
            }
        }

        private static bool shouldClassifyAsStack(IReadOnlyList<OsuPatternNode> nodes, int currentIndex, int chainStart, int chainEnd)
        {
            int chainLength = chainEnd - chainStart + 1;
            OsuPatternNode current = nodes[currentIndex];
            float stackThreshold = Math.Max(6f, current.Radius * 0.34f);
            bool closePrevious = currentIndex > 0 && (nodes[currentIndex - 1].Position - current.Position).Length <= stackThreshold;
            bool closeNext = currentIndex + 1 < nodes.Count && (nodes[currentIndex + 1].Position - current.Position).Length <= stackThreshold;

            if (chainLength <= 2)
                return true;

            if (closePrevious ^ closeNext && chainLength <= 3)
                return true;

            float totalDisplacement = (nodes[chainEnd].Position - nodes[chainStart].Position).Length;

            if (totalDisplacement <= stackThreshold * 1.2f)
                return true;

            float localTravel = 0;

            for (int i = chainStart; i < chainEnd; i++)
                localTravel += (nodes[i + 1].Position - nodes[i].Position).Length;

            return localTravel <= stackThreshold * Math.Max(1.45f, chainLength * 0.36f);
        }

        internal static bool IsFlowConnection(OsuPatternNode start, OsuPatternNode end, double beatLength)
        {
            if (!start.IsCircle || !end.IsCircle)
                return false;

            double gap = end.Time - start.Time;

            if (gap <= 0)
                return false;

            double maxGap = Math.Clamp(beatLength * 0.92, 90, 300);

            if (gap > maxGap)
                return false;

            float spacingRatio = getSpacingRatio(start, end);
            double denseGap = Math.Clamp(beatLength * 0.42, 58, 145);
            double closeGap = Math.Clamp(beatLength * 0.72, 85, 220);
            float maxSpacingRatio = gap <= denseGap
                ? 3.95f
                : gap <= closeGap
                    ? 5.15f
                    : (float)(4.85 - 0.65 * Math.Clamp((gap - closeGap) / Math.Max(1, maxGap - closeGap), 0.0, 1.0));

            return spacingRatio <= maxSpacingRatio;
        }

        private static float getSpacingRatio(OsuPatternNode start, OsuPatternNode end)
        {
            float radius = Math.Max(1f, Math.Min(start.Radius, end.Radius));
            return (end.Position - start.Position).Length / (radius * 2f);
        }

        private static Vector2 normaliseOrZero(Vector2 value)
        {
            float length = value.Length;
            return length > 0.0001f ? value / length : Vector2.Zero;
        }
    }
}
