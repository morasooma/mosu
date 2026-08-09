// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Screens.Play.HUD.HitErrorMeters
{
    /// <summary>
    /// Provides gameplay presses which were made inside a hit window, but outside the target.
    /// </summary>
    public interface IPositionalMissProvider
    {
        event Action<double> NewPositionalMiss;
    }
}
