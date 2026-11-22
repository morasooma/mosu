using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneBeatmapEditorNavigation
    {
        [Test]
        [Explicit("Local memory diagnostic: exited editor graphs currently remain alive after disposal and full GC.")]
        public void TestExitedEditorsAreCollected()
        {
            var editors = new List<WeakReference<Editor>>();
            var beatmaps = new List<WeakReference<EditorBeatmap>>();
            var clocks = new List<WeakReference<EditorClock>>();

            prepareBeatmap();

            for (int pass = 0; pass < 8; pass++)
            {
                int iteration = pass;
                openEditor();
                AddWaitStep("allow compose resources to load", 10);
                AddStep("record and exit editor", () =>
                {
                    editors.Add(new WeakReference<Editor>(getEditor()));
                    beatmaps.Add(new WeakReference<EditorBeatmap>(getEditorBeatmap()));
                    clocks.Add(new WeakReference<EditorClock>(getEditor().ChildrenOfType<EditorClock>().Single()));
                    getEditor().Exit();
                });
                AddUntilStep("returned to song select", () => Game.ScreenStack.CurrentScreen is SoloSongSelect);
                AddWaitStep("allow async disposal and transitions", 20);
                AddStep("collect and report editor resources", () =>
                {
                    collectEditorMemory();
                    using var process = Process.GetCurrentProcess();
                    TestContext.Progress.WriteLine($"EDITOR_MEMORY pass={iteration} managed_mib={GC.GetTotalMemory(false) / 1048576.0:F1} rss_mib={process.WorkingSet64 / 1048576.0:F1} private_mib={process.PrivateMemorySize64 / 1048576.0:F1}");
                });
            }

            var settling = new Stopwatch();
            AddStep("start real-time disposal settling", settling.Start);
            AddUntilStep("wait five real seconds for background cleanup", () => settling.Elapsed.TotalSeconds >= 5);
            AddStep("collect after settling", collectEditorMemory);
            AddWaitStep("leave collection frame", 5);
            AddStep("report retained editor graphs", () =>
                TestContext.Progress.WriteLine($"EDITOR_SETTLED pid={Environment.ProcessId} editors={editors.Count(r => r.TryGetTarget(out _))} beatmaps={beatmaps.Count(r => r.TryGetTarget(out _))} clocks={clocks.Count(r => r.TryGetTarget(out _))} managed_mib={GC.GetTotalMemory(false) / 1048576.0:F1}"));

            AddAssert("all exited editors collected", () => editors.All(r => !r.TryGetTarget(out _)));
            AddAssert("all editor beatmaps collected", () => beatmaps.All(r => !r.TryGetTarget(out _)));
            AddAssert("all editor clocks collected", () => clocks.All(r => !r.TryGetTarget(out _)));
        }

        private static void collectEditorMemory()
        {
            // Diagnostic only. Do not add forced collections to the editor's normal exit path.
            for (int i = 0; i < 4; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
            }
        }
    }
}
