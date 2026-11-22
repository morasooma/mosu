using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay;
using osuTK;

namespace osu.Game.Screens.Select
{
    /// <summary>The same map panel used by multiplayer and the ranked pool, with a keep action.</summary>
    internal partial class BeatmapGroupDeletionRow : DrawableRoomPlaylistItem
    {
        public BeatmapGroupDeletionRow(BeatmapGroupDeletion.Entry entry, long id)
            : base(new PlaylistItem(entry.Set.Beatmaps[0]) { ID = id, RulesetID = entry.Set.Beatmaps[0].Ruleset.OnlineID }, loadImmediately: true)
        {
            UseLocalBeatmap = true;
            AllowReordering = false;
            Description = entry.CanDelete ? BeatmapGroupDeletionStrings.Difficulties(entry.Set.Beatmaps.Count) : entry.Reason;
            CreateActionButtons = () => createActions(entry);
        }

        internal const float PANEL_SCALE = 1.3f;

        protected override Drawable CreateContent()
        {
            var panel = base.CreateContent();
            panel.Width = 1 / PANEL_SCALE;
            panel.Scale = new Vector2(PANEL_SCALE);
            return new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Child = panel,
            };
        }

        private static IEnumerable<Drawable> createActions(BeatmapGroupDeletion.Entry entry)
        {
            if (entry.CanDelete)
                yield return new OsuTabControlCheckbox
                {
                    Anchor = Anchor.Centre, Origin = Anchor.Centre,
                    Text = BeatmapGroupDeletionStrings.Keep, Current = entry.Keep,
                };
            else
                yield return new SpriteIcon
                {
                    Anchor = Anchor.Centre, Origin = Anchor.Centre,
                    Size = new Vector2(16), Icon = FontAwesome.Solid.Lock,
                };
        }
    }
}
