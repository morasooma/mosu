using System;
using osuTK;

namespace osu.Game.Graphics.Cursor
{
    /// <summary>
    /// One Euro filter for tooltip anchors. Slow motion is stabilised; deliberate motion
    /// raises the cutoff to keep the tooltip responsive. Coordinates are local UI units.
    /// </summary>
    internal sealed class TooltipPositionFilter
    {
        private const double minimum_cutoff = 2;
        private const double speed_coefficient = 0.025;
        private const double derivative_cutoff = 1;
        private const double maximum_interval = 0.1;
        private const float teleport_distance = 300;

        private bool initialised;
        private double lastTime;
        private Vector2 previousInput;
        private Vector2 position;
        private Vector2 velocity;

        public void Reset() => initialised = false;

        public Vector2 Filter(Vector2 input, double time)
        {
            double elapsed = (time - lastTime) / 1000;

            if (!initialised || elapsed < 0 || elapsed > maximum_interval || (input - previousInput).LengthSquared > teleport_distance * teleport_distance)
            {
                initialised = true;
                lastTime = time;
                previousInput = position = input;
                velocity = Vector2.Zero;
                return position;
            }

            if (elapsed == 0)
                return position;

            Vector2 rawVelocity = (input - previousInput) / (float)elapsed;
            velocity += (rawVelocity - velocity) * alpha(derivative_cutoff, elapsed);
            double cutoff = minimum_cutoff + speed_coefficient * velocity.Length;
            position += (input - position) * alpha(cutoff, elapsed);

            previousInput = input;
            lastTime = time;
            return position;
        }

        private static float alpha(double cutoff, double elapsed)
            => (float)(1 / (1 + 1 / (2 * Math.PI * cutoff * elapsed)));
    }
}
