using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Screens.Edit.Timing;

namespace osu.Game.Tests.Editing
{
    [TestFixture]
    public class ControlPointSelectionTest
    {
        private Bindable<ControlPointGroup?> primary = null!;
        private ControlPointSelection selection = null!;
        private ControlPointGroup[] groups = null!;

        [SetUp]
        public void SetUp()
        {
            groups = Enumerable.Range(0, 5).Select(i => new ControlPointGroup(i * 100)).ToArray();
            primary = new Bindable<ControlPointGroup?>();
            selection = new ControlPointSelection(primary);
        }

        [Test]
        public void TestShiftSelectsRangeInBothDirections()
        {
            selection.Select(groups[2], groups);
            selection.Select(groups[4], groups, range: true);
            Assert.That(selection.SelectedGroups, Is.EqualTo(groups.Skip(2)));
            Assert.That(primary.Value, Is.SameAs(groups[4]));

            selection.Select(groups[0], groups, range: true);
            Assert.That(selection.SelectedGroups, Is.EqualTo(groups.Take(3)));
            Assert.That(primary.Value, Is.SameAs(groups[0]));
        }

        [Test]
        public void TestControlClickTogglesIndividualRows()
        {
            selection.Select(groups[0], groups);
            selection.Select(groups[3], groups, toggle: true);
            Assert.That(selection.SelectedGroups, Is.EqualTo(new[] { groups[0], groups[3] }));

            selection.Select(groups[3], groups, toggle: true);
            Assert.That(selection.SelectedGroups, Is.EqualTo(new[] { groups[0] }));
            Assert.That(primary.Value, Is.SameAs(groups[0]));

            selection.Select(groups[0], groups, toggle: true);
            Assert.That(selection.SelectedGroups, Is.Empty);
            Assert.That(primary.Value, Is.Null);
        }

        [Test]
        public void TestControlShiftAddsRange()
        {
            selection.Select(groups[0], groups);
            selection.Select(groups[3], groups, toggle: true);
            selection.Select(groups[4], groups, range: true, toggle: true);
            Assert.That(selection.SelectedGroups, Is.EquivalentTo(new[] { groups[0], groups[3], groups[4] }));
        }

        [Test]
        public void TestPlainClickReplacesSelection()
        {
            selection.Select(groups[0], groups);
            selection.Select(groups[4], groups, range: true);
            selection.Select(groups[2], groups);
            Assert.That(selection.SelectedGroups, Is.EqualTo(new[] { groups[2] }));
        }

        [Test]
        public void TestExternalPrimaryChangeResetsSelection()
        {
            selection.Select(groups[0], groups);
            selection.Select(groups[4], groups, range: true);
            primary.Value = groups[2];
            Assert.That(selection.SelectedGroups, Is.EqualTo(new[] { groups[2] }));
            primary.Value = null;
            Assert.That(selection.SelectedGroups, Is.Empty);
        }

        [Test]
        public void TestUndoPrunesEqualButDetachedGroups()
        {
            selection.Select(groups[0], groups);
            selection.Select(groups[4], groups, range: true);
            selection.Prune(groups.Select(g => new ControlPointGroup(g.Time)));
            Assert.That(selection.SelectedGroups, Is.Empty);
            Assert.That(primary.Value, Is.Null);
        }

        [Test]
        public void TestPruneKeepsRemainingSelection()
        {
            selection.Select(groups[0], groups);
            selection.Select(groups[4], groups, toggle: true);
            selection.Prune(groups.Take(4));
            Assert.That(selection.SelectedGroups, Is.EqualTo(new[] { groups[0] }));
            Assert.That(primary.Value, Is.SameAs(groups[0]));
        }
    }
}
