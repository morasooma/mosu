// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Online;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class TestServerProfileManager
    {
        [Test]
        public void TestInitializationAndDefaultProfiles()
        {
            using (var storage = new TemporaryNativeStorage("server-profile-test-" + Guid.NewGuid()))
            {
                var manager = new ServerProfileManager(storage);

                Assert.That(manager.Profiles.Count, Is.EqualTo(1));
                Assert.That(manager.Profiles[0].Id, Is.EqualTo("default"));
                Assert.That(manager.Profiles[0].Name, Is.EqualTo("Morasooma Server"));
                Assert.That(manager.Profiles[0].IsDefault, Is.True);
                Assert.That(manager.Profiles[0].SupportsSpecialRulesets, Is.True);

                Assert.That(manager.ActiveProfile.Id, Is.EqualTo("default"));
            }
        }

        [Test]
        public void TestSavingPreservesMosuSpecialRulesets()
        {
            using (var storage = new TemporaryNativeStorage("server-profile-test-mosu-rulesets-" + Guid.NewGuid()))
            {
                var manager = new ServerProfileManager(storage);
                var mosu = manager.Profiles.Single(p => p.Id == "default");

                Assert.That(mosu.SupportsSpecialRulesets, Is.True);

                manager.SaveProfiles();

                Assert.That(mosu.SupportsSpecialRulesets, Is.True);
            }
        }

        [Test]
        public void TestCustomClientBuildIsPreserved()
        {
            using (var storage = new TemporaryNativeStorage("server-profile-test-custom-update-" + Guid.NewGuid()))
            {
                var manager = new ServerProfileManager(storage);
                var custom = new ServerProfile
                {
                    Id = "custom",
                    Name = "Custom Server",
                    ClientVersion = "2026.624.0-lazer",
                    VersionHash = "227fbae0924686eaedb4ab30f6e5b8da"
                };
                manager.AddProfile(custom);

                var reloadedManager = new ServerProfileManager(storage);
                var reloadedCustom = reloadedManager.Profiles.Single(p => p.Id == "custom");

                Assert.That(reloadedCustom.ClientVersion, Is.EqualTo("2026.624.0-lazer"));
                Assert.That(reloadedCustom.VersionHash, Is.EqualTo("227fbae0924686eaedb4ab30f6e5b8da"));
            }
        }

        [Test]
        public void TestForbiddenProfileIsRemovedWhenReloaded()
        {
            using (var storage = new TemporaryNativeStorage("server-profile-test-forbidden-remove-" + Guid.NewGuid()))
            {
                var manager = new ServerProfileManager(storage);
                manager.AddProfile(new ServerProfile
                {
                    Id = "forbidden",
                    ApiUrl = "https://osu.akatsuki.gg",
                });

                var reloadedManager = new ServerProfileManager(storage);
                Assert.That(reloadedManager.Profiles.Any(p => p.Id == "forbidden"), Is.False);
            }
        }

        [TestCase("https://akatsuki.gg", "Akatsuki")]
        [TestCase("https://osu.akatsuki.gg/api/v2", "Akatsuki")]
        [TestCase("ussr.pl", "ussr.pl")]
        [TestCase("https://c.ussr.pl:443", "ussr.pl")]
        [TestCase("https://osuokayu.pw/", "osuokayu.pw")]
        [TestCase("https://api.osuokayu.pw", "osuokayu.pw")]
        [TestCase("https://osudesu.su/ru/", "osudesu.su")]
        [TestCase("https://api.osudesu.su", "osudesu.su")]
        [TestCase("https://mellowosu.ru", "mellowosu.ru")]
        [TestCase("https://c.mellowosu.ru:443", "mellowosu.ru")]
        public void TestForbiddenServerDetection(string url, string expectedName)
        {
            var profile = new ServerProfile { ApiUrl = url };

            Assert.That(ServerProfileManager.TryGetForbiddenServer(profile, out string serverName), Is.True);
            Assert.That(serverName, Is.EqualTo(expectedName));
        }

        [Test]
        public void TestSimilarDomainIsAllowed()
        {
            var profile = new ServerProfile { ApiUrl = "https://notakatsuki.gg" };

            Assert.That(ServerProfileManager.TryGetForbiddenServer(profile, out _), Is.False);
        }

        [Test]
        public void TestForbiddenProfileCannotBeSelected()
        {
            using (var storage = new TemporaryNativeStorage("server-profile-test-forbidden-select-" + Guid.NewGuid()))
            {
                var manager = new ServerProfileManager(storage);
                manager.AddProfile(new ServerProfile
                {
                    Id = "forbidden",
                    WebsiteUrl = "https://ussr.pl/",
                });

                Assert.That(manager.SelectProfile("forbidden"), Is.False);
                Assert.That(manager.ActiveProfile.Id, Is.EqualTo("default"));
            }
        }

        [Test]
        public void TestInvalidStableClientVersionIsReplaced()
        {
            var profile = new ServerProfile
            {
                UseStableProtocol = true,
                ClientVersion = "2026.711.0-lazer",
            };

            ServerProfileManager.EnsureStableDefaults(profile);

            Assert.That(profile.ClientVersion, Is.EqualTo(ServerProfileManager.DEFAULT_STABLE_CLIENT_VERSION));
        }

        [Test]
        public void TestValidStableClientVersionIsPreserved()
        {
            var profile = new ServerProfile
            {
                UseStableProtocol = true,
                ClientVersion = "b20250101",
            };

            ServerProfileManager.EnsureStableDefaults(profile);

            Assert.That(profile.ClientVersion, Is.EqualTo("b20250101"));
        }

        [Test]
        public void TestAddAndDeleteProfile()
        {
            using (var storage = new TemporaryNativeStorage("server-profile-test-add-" + Guid.NewGuid()))
            {
                var manager = new ServerProfileManager(storage);

                var newProfile = new ServerProfile
                {
                    Id = "custom-id",
                    Name = "Custom Server",
                    ApiUrl = "https://custom.api",
                    WebsiteUrl = "https://custom.web",
                    ClientId = "123",
                    ClientSecret = "secret123",
                    IsDefault = false
                };

                manager.AddProfile(newProfile);
                Assert.That(manager.Profiles.Count, Is.EqualTo(2));
                Assert.That(manager.Profiles[1].Name, Is.EqualTo("Custom Server"));

                manager.SelectProfile("custom-id");
                Assert.That(manager.ActiveProfile.Id, Is.EqualTo("custom-id"));

                manager.DeleteProfile("custom-id");
                Assert.That(manager.Profiles.Count, Is.EqualTo(1));
                Assert.That(manager.ActiveProfile.Id, Is.EqualTo("default"));
            }
        }

        [Test]
        public void TestMosuCredentialsProtection()
        {
            using (var storage = new TemporaryNativeStorage("server-profile-test-protect-" + Guid.NewGuid()))
            {
                var manager = new ServerProfileManager(storage);

                manager.Profiles[0].ClientId = "leaked-id";
                manager.Profiles[0].ClientSecret = "leaked-secret";

                manager.SaveProfiles();

                // Reload profiles
                var manager2 = new ServerProfileManager(storage);
                Assert.That(manager2.Profiles[0].ClientId, Is.EqualTo("Protected"));
                Assert.That(manager2.Profiles[0].ClientSecret, Is.EqualTo("Protected"));
            }
        }
    }
}
