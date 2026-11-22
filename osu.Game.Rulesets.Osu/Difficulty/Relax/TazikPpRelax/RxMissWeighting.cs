// Adapted from taziksfear’s MosuPP 2.0.0 (PR #7, 3d3a196ed6540a3099d246dd5c6f662366c28db8).
using System;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.TazikPpRelax
{
    /// <summary>Experimental miss weights. Distances are outside the hit area in units of its radius.</summary>
    public static class RxMissWeighting
    {
        public static double DistanceFactor(double outsideRadii)
        {
            if (!double.IsFinite(outsideRadii)) throw new ArgumentOutOfRangeException(nameof(outsideRadii));
            double t = Math.Clamp(outsideRadii, 0, 1);
            return 0.25 + 0.75 * t * t * (3 - 2 * t);
        }

        public static double[] Weights(double[] noteLoads, bool[] missed, double[] outsideRadii)
        {
            if (noteLoads.Length != missed.Length || missed.Length != outsideRadii.Length)
                throw new ArgumentException("Miss evidence must match the note count.");
            var weights = new double[missed.Length];
            for (int i = 0; i < weights.Length; i++)
            {
                if (!missed[i]) continue;
                if (!double.IsFinite(noteLoads[i]) || noteLoads[i] < 0)
                    throw new ArgumentOutOfRangeException(nameof(noteLoads));
                weights[i] = Math.Clamp(noteLoads[i], 0, 1) * DistanceFactor(outsideRadii[i]);
            }
            return weights;
        }
    }
}
