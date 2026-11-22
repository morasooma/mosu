using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;

namespace osu.Game.Overlays.Settings
{
    /// <summary>
    /// Borrows an existing row for the favourite panel, restoring its flow position afterwards.
    /// Its owner stays alive, retaining all dynamic constraints, side effects and warnings.
    /// </summary>
    internal sealed class FavouriteSettingEntry
    {
        public readonly Drawable Row;
        public readonly IFavouriteSettingsItem Item;
        public readonly LocalisableString Path;
        private readonly Container<Drawable> parent;
        private readonly FillFlowContainer? flow;
        private float position;
        private osuTK.Vector2 originalPosition;
        private readonly Drawable placeholder = new Container { RelativeSizeAxes = Axes.X, Height = 0 };
        private Container<Drawable>? borrower;

        public FavouriteSettingEntry(Drawable row, IFavouriteSettingsItem item, Container<Drawable> parent, LocalisableString path)
        {
            Row = row;
            Item = item;
            Path = path;
            this.parent = parent;
            flow = parent as FillFlowContainer;

            // Most settings use equal (zero) flow positions, ordered by ChildID. Give them
            // distinct equivalent positions before any row is borrowed, so returning several
            // siblings in a different order cannot change their original visual order.
            if (flow != null)
            {
                var ordered = parent.Children.OrderBy(flow.GetLayoutPosition).ToArray();
                for (int i = 0; i < ordered.Length; i++)
                    flow.SetLayoutPosition(ordered[i], i);
            }
        }

        public void Borrow(Container<Drawable> destination)
        {
            if (borrower != null)
                throw new InvalidOperationException("Setting is already borrowed.");
            position = flow?.GetLayoutPosition(Row) ?? 0;
            originalPosition = Row.Position;
            parent.Remove(Row, false);
            parent.Add(placeholder);
            flow?.SetLayoutPosition(placeholder, position);
            // Flow coordinates belong to the original parent. The favourite host is a plain
            // auto-sized container, so retaining Y would turn it into a giant empty spacer.
            Row.Position = osuTK.Vector2.Zero;
            destination.Add(Row);
            borrower = destination;
            ((IFilterable)Row).MatchingFilter = true;
        }

        public void Return()
        {
            if (borrower == null)
                return;
            borrower.Remove(Row, false);
            borrower = null;
            parent.Remove(placeholder, false);
            Row.Position = originalPosition;
            parent.Add(Row);
            flow?.SetLayoutPosition(Row, position);
            ((IFilterable)Row).MatchingFilter = true;
        }

        public void DisposePlaceholder() => placeholder.Dispose();
    }
}
