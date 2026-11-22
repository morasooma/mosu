// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using OpenTabletDriver.Plugin.Timing;
using osuTK;

namespace osu.Framework.Input.Handlers.Tablet
{
    /// <summary>
    /// Native implementation of Kuuube's CHATTER EXTERMINATOR RAW.
    /// The filtering formula intentionally matches the original OpenTabletDriver plugin.
    /// Source: https://github.com/Kuuuube/Kuuube-s-CHATTER-EXTERMINATOR
    /// </summary>
    internal sealed class ChatterExterminatorFilter
    {
        private const float weight = 0.9f;
        private const float y_offset = 15;
        private const float stretch = 1.96f;
        private const float x_offset = 1.7f;

        private Vector2 lastPosition;

        public Vector2 Filter(Vector2 input, float strength)
        {
            Vector2 delta = input - lastPosition;
            float distance = MathF.Sqrt(MathF.Pow(delta.X, 2) + MathF.Pow(delta.Y, 2));

            float function = MathF.Pow((distance * -1 + (strength - x_offset)) / stretch, 19) + y_offset;
            float weightModifier = Math.Clamp(function + y_offset, 0, float.MaxValue);

            weightModifier = Math.Clamp(weight / weightModifier, 0, 1);

            lastPosition.X += delta.X * weightModifier;
            lastPosition.Y += delta.Y * weightModifier;

            return lastPosition;
        }

        public void Reset() => lastPosition = Vector2.Zero;
    }

    /// <summary>
    /// Screen-space implementation of AbstractQbit's Radial Follow Smoothing.
    /// The radial curve and reset behaviour intentionally match the original OpenTabletDriver plugin.
    /// Source: https://github.com/AbstractQbit/AbstractOTDPlugins
    /// </summary>
    internal sealed class RadialFollowFilter
    {
        private double outerRadius;
        private double innerRadius;
        private double smoothingCoefficient;
        private double softKneeScale;
        private double smoothingLeakCoefficient;

        private double xOffset;
        private double scaleComp;
        private bool derivedParametersInitialised;

        private Vector2 cursor;
        private readonly HPETDeltaStopwatch stopwatch = new HPETDeltaStopwatch(true);

        public void Configure(double outerRadius, double innerRadius, double smoothingCoefficient, double softKneeScale, double smoothingLeakCoefficient)
        {
            this.outerRadius = Math.Clamp(outerRadius, 0.0, 1000000.0);
            this.innerRadius = Math.Clamp(innerRadius, 0.0, 1000000.0);
            this.smoothingCoefficient = Math.Clamp(smoothingCoefficient, 0.0001, 1.0);
            this.smoothingLeakCoefficient = Math.Clamp(smoothingLeakCoefficient, 0.0, 1.0);

            double clampedSoftKneeScale = Math.Clamp(softKneeScale, 0.0, 100.0);

            if (!derivedParametersInitialised || this.softKneeScale != clampedSoftKneeScale)
            {
                this.softKneeScale = clampedSoftKneeScale;
                updateDerivedParameters();
                derivedParametersInitialised = true;
            }
        }

        public Vector2 Filter(Vector2 target)
        {
            Vector2 direction = target - cursor;
            float distanceToMove = SampleRadialCurve(direction.Length);
            direction /= direction.Length;
            cursor += direction * distanceToMove;

            // Catch NaNs and pen redetection.
            if (!(float.IsFinite(cursor.X) & float.IsFinite(cursor.Y) & stopwatch.Restart().TotalMilliseconds < 50))
                cursor = target;

            return cursor;
        }

        public void Reset()
        {
            cursor = Vector2.Zero;
            stopwatch.Reset();
            stopwatch.Start();
        }

        internal float SampleRadialCurve(float distance) => (float)deltaFunction(distance, xOffset, scaleComp);

        private void updateDerivedParameters()
        {
            if (softKneeScale > 0.0001)
            {
                xOffset = getXOffset();
                scaleComp = getScaleComp();
            }
            else
            {
                // Calculating these values with the functions would divide by zero.
                xOffset = -1;
                scaleComp = 1;
            }
        }

        private double kneeFunction(double x) => x switch
        {
            < -3 => x,
            < 3 => Math.Log(Math.Tanh(Math.Exp(x)), Math.E),
            _ => 0,
        };

        private double kneeScaled(double x) => softKneeScale switch
        {
            > 0.0001 => softKneeScale * kneeFunction(x / softKneeScale) + 1,
            _ => x > 0 ? 1 : 1 + x,
        };

        private static double inverseTanh(double x) => Math.Log((1 + x) / (1 - x), Math.E) / 2;

        private double inverseKneeScaled(double x) => softKneeScale * Math.Log(inverseTanh(Math.Exp((x - 1) / softKneeScale)), Math.E);

        private double derivativeKneeScaled(double x)
        {
            double exponential = Math.Exp(x / softKneeScale);
            double tanh = Math.Tanh(exponential);
            return (exponential - exponential * (tanh * tanh)) / tanh;
        }

        private double getXOffset() => inverseKneeScaled(0);

        private double getScaleComp() => derivativeKneeScaled(getXOffset());

        private double adjustedOuterRadius => Math.Max(outerRadius, innerRadius + 0.0001);

        private double leakedFunction(double x, double offset, double scaleComp)
            => kneeScaled(x + offset) * (1 - smoothingLeakCoefficient) + x * smoothingLeakCoefficient * scaleComp;

        private double smoothedFunction(double x, double offset, double scaleComp)
            => leakedFunction(x * smoothingCoefficient / scaleComp, offset, scaleComp);

        private double scaleToOuter(double x, double offset, double scaleComp)
            => (adjustedOuterRadius - innerRadius) * smoothedFunction(x / (adjustedOuterRadius - innerRadius), offset, scaleComp);

        private double deltaFunction(double x, double offset, double scaleComp)
            => x > innerRadius ? x - scaleToOuter(x - innerRadius, offset, scaleComp) - innerRadius : 0;
    }

    /// <summary>
    /// Screen-space implementation of AbstractQbit's BezierInterpolator.
    /// Raw pen positions are EMA-smoothed and treated as control points of a quadratic bezier spline;
    /// the emitted position is the spline's endpoint at each report boundary.
    /// The filtering formula intentionally matches the original OpenTabletDriver plugin.
    /// Source: https://github.com/AbstractQbit/AbstractOTDPlugins
    /// </summary>
    internal sealed class BezierInterpolatorFilter
    {
        private Vector2 emaTarget;
        private Vector2 controlPoint;
        private Vector2 controlPointNext;
        private bool initialised;

        public Vector2 Filter(Vector2 input, float smoothingFactor)
        {
            if (!initialised)
            {
                emaTarget = input;
                controlPoint = input;
                controlPointNext = input;
                initialised = true;

                return input;
            }

            emaTarget += smoothingFactor * (input - emaTarget);

            controlPoint = controlPointNext;
            controlPointNext = emaTarget;

            // The interpolator emits positions along a quadratic bezier between consecutive reports;
            // at each report boundary (alpha = 1) this evaluates to the midpoint of the two control points.
            return (controlPoint + controlPointNext) / 2;
        }

        public void Reset() => initialised = false;
    }
}
