// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osuTK;
using Realms;

namespace osu.Game.Screens.Ranking
{
    public partial class CollectionButton : GrayButton, IHasPopover
    {
        private readonly BeatmapInfo beatmapInfo;
        private readonly Bindable<bool> isInAnyCollection;

        [Resolved]
        private RealmAccess realmAccess { get; set; } = null!;

        private IDisposable? collectionSubscription;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        public CollectionButton(BeatmapInfo beatmapInfo)
            : base(FontAwesome.Solid.Book)
        {
            this.beatmapInfo = beatmapInfo;
            isInAnyCollection = new Bindable<bool>(false);

            Size = new Vector2(75, 30);

            TooltipText = CommonStrings.Collections.ToLower();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Action = this.ShowPopover;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            colourProvider.PaletteVersion.BindValueChanged(_ => updateState());
            collectionSubscription = realmAccess.RegisterForNotifications(r => r.All<BeatmapCollection>(), collectionsChanged);

            isInAnyCollection.BindValueChanged(_ => updateState(), true);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            collectionSubscription?.Dispose();
        }

        private void collectionsChanged(IRealmCollection<BeatmapCollection> sender, ChangeSet? changes)
        {
            isInAnyCollection.Value = sender.AsEnumerable().Any(c => c.BeatmapMD5Hashes.Contains(beatmapInfo.MD5Hash));
        }

        private void updateState()
        {
            Colour4 activeColour = OverlayColourProvider.GetEffectiveContextColour(
                ThemeSurfaceContext.Results,
                SurfaceColourRole.Accent,
                colours.Green);

            Background.FadeColour(isInAnyCollection.Value ? activeColour : colours.Gray4, 500, Easing.InOutExpo);
        }

        public Popover GetPopover() => new CollectionPopover(beatmapInfo);
    }
}
