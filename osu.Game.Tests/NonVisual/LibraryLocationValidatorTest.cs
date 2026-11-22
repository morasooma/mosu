using System;
using System.IO;
using NUnit.Framework;
using osu.Game.IO;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LibraryLocationValidatorTest
    {
        private string root = null!;

        [SetUp]
        public void SetUp() => root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"mosu-library-test-{Guid.NewGuid():N}")).FullName;

        [TearDown]
        public void TearDown() => Directory.Delete(root, true);

        [TestCase("osu!.exe")]
        [TestCase("osu!.db")]
        [TestCase("osu!.Player.cfg")]
        [TestCase("OSU!.EXE")]
        public void TestRejectsStableAndSubdirectoriesWithoutModifyingFiles(string marker)
        {
            File.WriteAllText(Path.Combine(root, marker), "keep me");
            string songs = Directory.CreateDirectory(Path.Combine(root, "Songs")).FullName;
            File.WriteAllText(Path.Combine(songs, "map.osu"), "keep this too");

            Assert.That(LibraryLocationValidator.IsInsideStableInstallation(root), Is.True);
            Assert.That(LibraryLocationValidator.IsInsideStableInstallation(songs), Is.True);
            Assert.That(LibraryLocationValidator.IsInsideStableInstallation(Path.Combine(root, "Skins", "new")), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(root, marker)), Is.EqualTo("keep me"));
            Assert.That(File.ReadAllText(Path.Combine(songs, "map.osu")), Is.EqualTo("keep this too"));
        }

        [Test]
        public void TestAllowsEmptyFolderAndLazerLibrary()
        {
            Assert.That(LibraryLocationValidator.IsInsideStableInstallation(root), Is.False);
            File.WriteAllText(Path.Combine(root, "client.realm"), "lazer");
            Assert.That(LibraryLocationValidator.IsInsideStableInstallation(root), Is.False);
        }
    }
}
