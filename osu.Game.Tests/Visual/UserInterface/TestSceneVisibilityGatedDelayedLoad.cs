using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Containers;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneVisibilityGatedDelayedLoad : OsuTestScene
    {
        [Test]
        public void TestHiddenAncestorUnloadsAndReopenRecreatesContent()
        {
            Container owner = null!;
            DelayedLoadUnloadWrapper wrapper = null!;
            DisposalProbe oldContent = null!;
            AddStep("create wrapper", () => Child = owner = new Container
            {
                Size = new Vector2(128),
                Child = wrapper = new DelayedLoadUnloadWrapper(
                    () => new DisposalProbe { RelativeSizeAxes = Axes.Both }, 0, 50)
                {
                    RelativeSizeAxes = Axes.Both
                }
            });
            AddUntilStep("loaded", () => wrapper.DelayedLoadCompleted);
            AddStep("hide ancestor", () =>
            {
                oldContent = (DisposalProbe)wrapper.Content;
                owner.Hide();
            });
            AddUntilStep("unloaded", () => wrapper.Content == null && !wrapper.DelayedLoadCompleted);
            AddUntilStep("disposed", () => oldContent.Disposed);
            AddWaitStep("stay hidden", 5);
            AddAssert("not reloaded while hidden", () => wrapper.Content == null);
            AddStep("show ancestor", () => owner.Show());
            AddUntilStep("reloaded", () => wrapper.DelayedLoadCompleted);
            AddAssert("fresh content", () => !ReferenceEquals(oldContent, wrapper.Content));
        }
        private partial class DisposalProbe : Box
        {
            public bool Disposed => IsDisposed;
        }
    }
}
