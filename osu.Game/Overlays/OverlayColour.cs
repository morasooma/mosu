// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Overlays
{
    /// <summary>
    /// A colour supplied by an <see cref="OverlayColourProvider"/>.
    ///
    /// This enum is the reactive counterpart of the legacy colour properties.
    /// New UI should request a bound colour via
    /// <see cref="OverlayColourProvider.GetColourBindable"/> rather than take a
    /// one-time <c>Color4</c> snapshot from a property.
    /// </summary>
    public enum OverlayColour
    {
        Colour0,
        Colour1,
        Colour2,
        Colour3,
        Colour4,
        Highlight1,
        Content1,
        Content2,
        Light1,
        Light2,
        Light3,
        Light4,
        Dark1,
        Dark2,
        Dark3,
        Dark4,
        Dark5,
        Dark6,
        Foreground1,
        Background1,
        Background2,
        Background3,
        Background4,
        Background5,
        Background6,
    }
}
