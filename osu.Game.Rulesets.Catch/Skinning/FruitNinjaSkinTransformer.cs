using osu.Framework.Graphics;
using osu.Game.Rulesets.Osu;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Catch.Skinning
{
    /// <summary>
    /// Adds only standard's existing cursor lookups to Catch's skin source.
    /// All fruit, HUD and hitsound lookups continue to use the Catch transformer.
    /// </summary>
    public class FruitNinjaSkinTransformer : SkinTransformer
    {
        private readonly ISkin cursorSkin;

        public FruitNinjaSkinTransformer(ISkin skin, ISkin cursorSkin)
            : base(skin)
        {
            this.cursorSkin = cursorSkin;
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
            => lookup is OsuSkinComponentLookup cursor && cursor.Component is OsuSkinComponents.Cursor or OsuSkinComponents.CursorTrail
                or OsuSkinComponents.CursorParticles or OsuSkinComponents.CursorRipple
                ? cursorSkin.GetDrawableComponent(lookup)
                : base.GetDrawableComponent(lookup);
    }
}
