using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;

using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneSongSelectGroupDeletion : SongSelectTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        [TestCase(ForkSongSelectStyle.Modern, false)]
        [TestCase(ForkSongSelectStyle.LegacySkinned, false)]
        [TestCase(ForkSongSelectStyle.Modern, true)]
        public void TestWholeSetDeletionThroughRealCarousel(ForkSongSelectStyle style, bool changeGroupBeforeConfirm)
        {
            BeatmapSetInfo complete = null!;
            BeatmapSetInfo partial = null!;
            BeatmapGroupDeletionDialog dialog = null!;
            PopupDialog confirmation = null!;
            AddStep("choose layout", () => Config.SetValue(OsuSetting.ForkSongSelectStyle, style));
            ImportBeatmapForRuleset(set =>
            {
                complete = set;
                foreach (var map in set.Beatmaps) map.Length = 90_000;
            }, 2, 0);
            ImportBeatmapForRuleset(set =>
            {
                partial = set;
                foreach (var map in set.Beatmaps) map.Length = 90_000;
                set.Beatmaps.Last().Length = 300_000;
            }, 2, 0);
            GroupBy(GroupMode.Length);
            LoadSongSelect();
            AddUntilStep("two groups presented", () => Carousel.Filters.OfType<BeatmapCarouselFilterGrouping>().Single().GroupItems.Count == 2);
            AddStep("open group deletion from panel menu", () =>
            {
                var group = Carousel.Filters.OfType<BeatmapCarouselFilterGrouping>().Single().GroupItems.Keys
                                    .First(g => Carousel.GetBeatmapsInGroup(g).Count == 3);
                var panel = SongSelect.ChildrenOfType<PanelGroup>().First(p => p.Item?.Model is GroupDefinition definition && definition == group);
                panel.ContextMenuItems.Single(i => i.Text.Value == BeatmapGroupDeletionStrings.Open).Action.Value?.Invoke();
            });
            AddUntilStep("selection dialog loaded", () =>
            {
                dialog = Dependencies.Get<IDialogOverlay>().CurrentDialog as BeatmapGroupDeletionDialog ?? null!;
                return dialog?.IsLoaded == true;
            });
            AddAssert("partial set cannot be selected even if carousel projects it", () =>
                dialog.ChildrenOfType<OsuTabControlCheckbox>().Count() == 1
                && dialog.ChildrenOfType<BeatmapGroupDeletionRow>().Count() == 2);
            AddStep("continue to separate confirmation", () => dialog.PerformOkAction());
            AddUntilStep("dangerous confirmation loaded", () =>
            {
                confirmation = Dependencies.Get<IDialogOverlay>().CurrentDialog!;
                return confirmation != null && confirmation != dialog && confirmation.IsLoaded;
            });
            AddAssert("nothing deleted before confirmation", () => !Realm.Run(r => r.Find<BeatmapSetInfo>(complete.ID)!.DeletePending));
            if (changeGroupBeforeConfirm)
            {
                GroupBy(GroupMode.None);
                WaitForFiltering();
            }
            AddStep("confirm through existing dangerous action", () => confirmation.ChildrenOfType<PopupDialogDangerousButton>().Single().Action.Invoke());
            AddUntilStep("only unchanged group can be deleted", () => Realm.Run(r => r.Find<BeatmapSetInfo>(complete.ID)!.DeletePending) == !changeGroupBeforeConfirm);
            AddAssert("split set untouched", () => Realm.Run(r => !r.Find<BeatmapSetInfo>(partial.ID)!.DeletePending && r.Find<BeatmapSetInfo>(partial.ID)!.Beatmaps.Count == 2));
            AddUntilStep("carousel reflects deletion", () => !SongSelect.IsFiltering
                && Carousel.Filters.OfType<BeatmapCarouselFilterGrouping>().Single().SetItems.Keys.Any(s => s.BeatmapSet.ID == complete.ID) == changeGroupBeforeConfirm);
        }
    }
}
