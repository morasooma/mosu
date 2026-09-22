// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Scoring.Render;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ReplayRenderProcessStartInfoBuilderTest
    {
        [Test]
        public void TestDirectExecutableLaunch()
        {
            var startInfo = ReplayRenderProcessStartInfoBuilder.Create(
                @"C:\Games\osu!.exe",
                @"C:\Games\osu!.dll",
                ["--render-replay", "abc", "--resolution", "1920x1080", "--skin", "01234567-89ab-cdef-0123-456789abcdef"],
                @"C:\Games");

            Assert.Multiple(() =>
            {
                Assert.That(startInfo.FileName, Is.EqualTo(@"C:\Games\osu!.exe"));
                Assert.That(startInfo.Arguments, Is.EqualTo("--render-replay abc --resolution 1920x1080 --skin 01234567-89ab-cdef-0123-456789abcdef"));
            });
        }

        [Test]
        public void TestDotnetHostLaunch()
        {
            var startInfo = ReplayRenderProcessStartInfoBuilder.Create(
                @"C:\Program Files\dotnet\dotnet.exe",
                @"C:\Games Folder\osu!.dll",
                ["--render-replay", "abc", "--status-file", @"C:\temp files\status.json"],
                @"C:\Games Folder");

            Assert.Multiple(() =>
            {
                Assert.That(startInfo.FileName, Is.EqualTo(@"C:\Program Files\dotnet\dotnet.exe"));
                Assert.That(startInfo.Arguments, Does.StartWith("\"C:\\Games Folder\\osu!.dll\""));
                Assert.That(startInfo.Arguments, Does.Contain("\"C:\\temp files\\status.json\""));
            });
        }
    }
}
