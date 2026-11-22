using NUnit.Framework;
using osu.Game.Online;
using osu.Game.Configuration;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class ClientBuildKindTest
    {
        [TestCase("2026.1003.0+dev", "Dev")]
        [TestCase("2026.1003.0+dev.a51445d2c2dfa1b57a030c506ef9e090683ce9f8", "Dev")]
        [TestCase("2026.1003.0+dev-linux.abcdef", "Dev")]
        [TestCase("2026.1003.0+stable.abcdef", "Release")]
        [TestCase("2026.1003.0+stable", "Release")]
        [TestCase("2026.1003.0+stable-linux.abcdef", "Release")]
        [TestCase("2026.1003.0+release.abcdef", "Release")]
        [TestCase("2026.1003.0+local.20261003", "Custom")]
        [TestCase("2026.1003.0+dev-custom.abcdef", "Custom")]
        [TestCase("2026.1003.0", "Custom")]
        [TestCase(null, "Custom")]
        public void RecognisesChannelBeforeSdkRevision(string? version, string expected)
            => Assert.That(ClientBuildKind.FromInformationalVersion(version), Is.EqualTo(expected));

        [TestCase("2026.1117.0+stable.abcdef", ReleaseStream.DevBuild, "Dev")]
        [TestCase("2026.1117.0+stable.abcdef", ReleaseStream.Lazer, "Release")]
        [TestCase("2026.1117.0+dev.abcdef", ReleaseStream.Lazer, "Release")]
        [TestCase("2026.1117.0+dev.abcdef", ReleaseStream.DevBuild, "Dev")]
        [TestCase("2026.1117.0+local.abcdef", ReleaseStream.DevBuild, "Custom")]
        [TestCase(null, ReleaseStream.DevBuild, "Custom")]
        public void SelectedStreamSurvivesStableBinaryPromotion(string? version, ReleaseStream stream, string expected)
            => Assert.That(ClientBuildKind.FromReleaseStream(version, stream), Is.EqualTo(expected));
    }
}
