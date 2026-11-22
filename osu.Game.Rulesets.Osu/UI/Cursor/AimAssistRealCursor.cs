using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring.Render;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.UI.Cursor
{
    /// <summary>
    /// A cursor-only overlay: reads physical input, never the assisted gameplay position.
    /// Lives inside the gameplay cursor so it shares its visibility and draws above its skin.
    /// </summary>
    public partial class AimAssistRealCursor : CompositeDrawable
    {
        private readonly CircularContainer marker;
        private OsuInputManager? inputManager;
        private OsuModMosuAimAssist? mod;
        private bool replayRender;

        public AimAssistRealCursor()
        {
            RelativeSizeAxes = Axes.Both;
            Depth = -1;
            InternalChild = marker = new CircularContainer
            {
                Name = "physical cursor marker",
                Origin = Anchor.Centre,
                Size = new Vector2(24),
                Masking = true,
                BorderThickness = 2,
                BorderColour = Color4.Red,
                Alpha = 0,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Red,
                    Alpha = 0.2f,
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            inputManager = GetContainingInputManager() as OsuInputManager;
            mod = this.FindClosestParent<DrawableOsuRuleset>()?.Mods.OfType<OsuModMosuAimAssist>().FirstOrDefault();
            replayRender = this.FindClosestParent<ReplayRenderGame>() != null;
        }

        protected override void Update()
        {
            base.Update();

            // Replays contain the corrected cursor, not the original physical path. Do not
            // mislabel the spectator's mouse (or replay-bot input) as the player's real cursor.
            if (mod?.ShowRealCursor.Value != true || inputManager == null ||
                !inputManager.HasOriginalUserCursorPosition || inputManager.ReplayInputHandler != null ||
                inputManager.ReplayBotActive || replayRender)
            {
                marker.Alpha = 0;
                return;
            }

            Vector2 position = inputManager.OriginalUserCursorPosition;
            marker.Position = ToLocalSpace(position);
            // Keep the marker 24 screen pixels across, independent of playfield/UI/cursor scale.
            marker.Size = new Vector2(Vector2.Distance(ToLocalSpace(position), ToLocalSpace(position + new Vector2(24, 0))));
            marker.Alpha = 1;
        }
    }
}
