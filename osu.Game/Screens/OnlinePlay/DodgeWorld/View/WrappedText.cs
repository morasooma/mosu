// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// Text that wraps between words.
    /// </summary>
    /// <remarks>
    /// <c>SpriteText.AllowMultiline</c> looks like the answer and is not: a sprite lays out
    /// glyphs and breaks wherever it runs out of width, so a wrapped sentence is cut mid-word. Only a
    /// text flow knows where the words are. Every wrapping label in Dodge World goes through here so
    /// that the mistake is not made again one label at a time.
    /// </remarks>
    internal static class WrappedText
    {
        public static OsuTextFlowContainer Paragraph(string text, ColourInfo colour, float size = 12,
                                                     FontWeight weight = FontWeight.Regular) =>
            new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.Default.With(size: size, weight: weight))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Colour = colour,
                Text = text,
            };
    }
}
