// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Dodge.Skinning.Components
{
    /// <summary>
    /// A skinnable Dodge component which has no built-in visual.
    /// It is only present when a skin supplies the requested resource.
    /// </summary>
    public partial class OptionalDodgeSkinDrawable : SkinnableDrawable
    {
        public DodgeSkinComponents Component { get; }

        public bool HasVisual => Drawable != null && Drawable is not MissingSkinDrawable;

        public OptionalDodgeSkinDrawable(DodgeSkinComponents component, ConfineMode confineMode = ConfineMode.ScaleToFit)
            : base(
                new DodgeSkinComponentLookup(component),
                _ => new MissingSkinDrawable(),
                confineMode)
        {
            Component = component;
        }

        private partial class MissingSkinDrawable : Drawable
        {
            public override bool IsPresent => false;
        }
    }
}
