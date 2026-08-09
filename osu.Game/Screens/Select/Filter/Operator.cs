// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.Select.Filter
{
    /// <summary>
    /// Defines logical operators that can be used in the song select search box keyword filters.
    /// </summary>
    public enum Operator
    {
        Less,
        LessOrEqual,
        Equal,
        NotEqual,
        GreaterOrEqual,
        Greater,

        /// <summary>
        /// Matches values within ±10% of the specified number.
        /// Triggered by the <c>~</c> operator (e.g. <c>bpm~200</c>, <c>obj~1000</c>).
        /// </summary>
        Approximate,
    }
}
