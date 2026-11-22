using osu.Framework.Graphics;
using osu.Game.Rulesets.Dodge.Objects;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>A value snapshot of visual properties, never timing or collision geometry.</summary>
    internal sealed record DodgeObjectAppearance(Colour4 Colour, Colour4 OutlineColour, float Opacity,
                                                float OutlineThickness, DodgeBulletShape? Shape, float? BorderOpacity)
    {
        public static DodgeObjectAppearance Capture(DodgeHitObject source) => new DodgeObjectAppearance(
            source.Colour, source.OutlineColour, source.Opacity, source.OutlineThickness,
            source switch { DodgeBullet bullet => bullet.Shape, DodgeEmitter emitter => emitter.Shape, _ => null },
            source is DodgeArenaChange arena ? arena.BorderOpacity : null);

        public void ApplyTo(DodgeHitObject target)
        {
            target.Colour = Colour;
            target.OutlineColour = OutlineColour;
            target.Opacity = Opacity;
            target.OutlineThickness = OutlineThickness;
            if (Shape is { } shape)
            {
                if (target is DodgeBullet bullet) bullet.Shape = shape;
                if (target is DodgeEmitter emitter) emitter.Shape = shape;
            }
            if (BorderOpacity is { } borderOpacity && target is DodgeArenaChange arena)
                arena.BorderOpacity = borderOpacity;
        }
    }
}
