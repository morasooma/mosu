using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;

using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneBeatmapGroupDeletionDialog : OsuTestScene
    {
        private DialogOverlay dialogs = null!;
        private BeatmapGroupDeletion selection = null!;
        private BeatmapGroupDeletionDialog dialog = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(new OsuConfigManager(LocalStorage));
            Dependencies.CacheAs<IDialogOverlay>(dialogs = new DialogOverlay());
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("open selection", () =>
            {
                var sets = Enumerable.Range(0, 3).Select(i =>
                {
                    var set = new BeatmapSetInfo();
                    for (int j = 0; j < 2; j++)
                        set.Beatmaps.Add(new BeatmapInfo { BeatmapSet = set, Metadata = new BeatmapMetadata { Artist = "Artist", Title = "Map " + i } });
                    return set;
                }).ToArray();
                var maps = sets.Take(2).SelectMany(s => s.Beatmaps).Concat(sets[2].Beatmaps.Take(1)).ToArray();
                selection = new BeatmapGroupDeletion(maps);
                if (dialogs.Parent == null)
                    Child = dialogs;
                dialogs.Push(dialog = new BeatmapGroupDeletionDialog("Test group", selection, () => maps.Select(b => b.ID).ToHashSet()));
            });
            AddUntilStep("loaded", () => dialog.IsLoaded && dialog.ChildrenOfType<OsuTabControlCheckbox>().Count() == 2);
        }

        [Test]
        public void TestKeepAllClearAndCancel()
        {
            AddAssert("partial set locked", () => selection.Entries.Count(e => e.Keep.Disabled && e.Keep.Value) == 1);
            AddAssert("continue initially available", () => dialog.ChildrenOfType<PopupDialogOkButton>().Single().Enabled.Value);
            AddStep("keep all", () => dialog.ChildrenOfType<RoundedButton>().Single(b => b.Text == BeatmapGroupDeletionStrings.KeepAll).TriggerClick());
            AddAssert("nothing to delete", () => selection.Entries.All(e => e.Keep.Value) && !dialog.ChildrenOfType<PopupDialogOkButton>().Single().Enabled.Value);
            AddStep("clear keep selection", () => dialog.ChildrenOfType<RoundedButton>().Single(b => b.Text == BeatmapGroupDeletionStrings.ClearKept).TriggerClick());
            AddAssert("only deletable sets unmarked", () => selection.Entries.Count(e => !e.Keep.Value) == 2 && selection.Entries.Single(e => !e.CanDelete).Keep.Value);
            AddStep("keep one", () => dialog.ChildrenOfType<OsuTabControlCheckbox>().First(c => !c.Current.Disabled).Current.Value = true);
            AddAssert("individual keep bound", () => selection.Entries.Count(e => e.CanDelete && e.Keep.Value) == 1);
            AddStep("cancel", () => dialog.PerformAction<PopupDialogCancelButton>());
            AddUntilStep("closed", () => dialog.State.Value == Visibility.Hidden);
            AddAssert("no data touched", () => selection.Entries.All(e => !e.Set.DeletePending));
        }

        [TestCase(480)]
        [TestCase(720)]
        public void TestControlsFit(float width)
        {
            AddStep("set width", () => { dialog.RelativeSizeAxes = Axes.None; dialog.Width = width; });
            AddWaitStep("settle layout", 5);
            AddAssert("controls fit in dialog", () => dialog.ChildrenOfType<OsuTabControlCheckbox>().All(c =>
                c.ScreenSpaceDrawQuad.TopLeft.X >= dialog.ScreenSpaceDrawQuad.TopLeft.X - 1
                && c.ScreenSpaceDrawQuad.TopRight.X <= dialog.ScreenSpaceDrawQuad.TopRight.X + 1));
            AddAssert("buttons fit vertically", () => dialog.ChildrenOfType<PopupDialogButton>().All(b =>
                b.ScreenSpaceDrawQuad.BottomLeft.Y <= dialog.ScreenSpaceDrawQuad.BottomLeft.Y + 1));
        }
    }
}
