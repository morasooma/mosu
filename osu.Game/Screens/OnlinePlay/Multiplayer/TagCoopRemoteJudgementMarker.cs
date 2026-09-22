// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.Multiplayer
{
    /// <summary>
    /// Delayed feedback for a miss reported by a remote Tag Co-op player.
    /// This is visual-only and intentionally does not create a local judgement.
    /// </summary>
    public partial class TagCoopRemoteJudgementMarker : CompositeDrawable
    {
        private readonly DrawableRuleset drawableRuleset;
        private readonly Vector2 gamefieldPosition;

        public TagCoopRemoteJudgementMarker(DrawableRuleset drawableRuleset, Vector2 gamefieldPosition, string username)
        {
            this.drawableRuleset = drawableRuleset;
            this.gamefieldPosition = gamefieldPosition;

            AutoSizeAxes = Axes.Both;
            Origin = Anchor.Centre;
            Depth = float.MinValue;

            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(6, 0),
                Children = new Drawable[]
                {
                    new Container
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(22),
                        Children = new Drawable[]
                        {
                            crossBar(45),
                            crossBar(-45),
                        }
                    },
                    new OsuSpriteText
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Text = TagCoopStrings.RemoteMiss(username),
                        Font = OsuFont.Torus.With(size: 14, weight: FontWeight.Bold),
                        Colour = Color4.Red,
                    }
                }
            };

        }

        private static Box crossBar(float rotation) => new Box
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = new Vector2(4, 22),
            Rotation = rotation,
            Colour = Color4.Red,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Vector2 screenPosition = drawableRuleset.Playfield.GamefieldToScreenSpace(gamefieldPosition);
            if (Parent != null)
                Position = Parent.ToLocalSpace(screenPosition);

            Alpha = 0;
            this.FadeIn(80)
                .MoveToOffset(new Vector2(0, -18), 900, Easing.OutQuint)
                .Delay(550)
                .FadeOut(270)
                .Expire();
        }
    }
}
