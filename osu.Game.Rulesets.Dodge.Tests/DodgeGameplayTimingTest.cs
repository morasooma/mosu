// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeGameplayTimingTest
    {
        [Test]
        public void TestInitialArenaChangeDoesNotPreventIntroSkip()
        {
            DodgeHitObject[] objects =
            {
                new DodgeArenaChange { StartTime = 0 },
                new DodgeBullet { StartTime = 15000 },
            };

            Assert.That(DodgeGameplayTiming.GetGameplayStartTime(objects), Is.EqualTo(13000));
        }

        [Test]
        public void TestEmitterStartsGameplay()
        {
            DodgeHitObject[] objects =
            {
                new DodgeArenaChange { StartTime = 0 },
                new DodgeEmitter { StartTime = 8000 },
                new DodgeBullet { StartTime = 12000 },
            };

            Assert.That(DodgeGameplayTiming.GetGameplayStartTime(objects), Is.EqualTo(6000));
        }

        [Test]
        public void TestMapWithoutThreatsStartsNormally()
        {
            DodgeHitObject[] objects =
            {
                new DodgeArenaChange { StartTime = 0 },
            };

            Assert.That(DodgeGameplayTiming.GetGameplayStartTime(objects), Is.Zero);
        }

        [Test]
        public void TestArenaChangeDoesNotExtendGameplayEnd()
        {
            DodgeHitObject[] objects =
            {
                new DodgeBullet { StartTime = 1000, Duration = 500 },
                new DodgeArenaChange { StartTime = 1200, Duration = 10000 },
            };

            Assert.That(DodgeGameplayTiming.GetGameplayEndTime(objects), Is.EqualTo(1500));
        }

        [Test]
        public void TestEmitterExtendsGameplayEnd()
        {
            DodgeHitObject[] objects =
            {
                new DodgeBullet { StartTime = 1000, Duration = 500 },
                new DodgeEmitter { StartTime = 2000, Duration = 750 },
            };

            Assert.That(DodgeGameplayTiming.GetGameplayEndTime(objects), Is.EqualTo(2750));
        }

        [Test]
        public void TestPlayfieldCachesOnlyRefreshAfterObjectChanges()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
            };
            var arenaChange = new DodgeArenaChange
            {
                StartTime = 0,
                TargetSize = DodgePlayfield.BASE_SIZE,
            };
            var playfield = new DodgePlayfield(new DodgeHitObject[] { bullet, arenaChange });

            Assert.Multiple(() =>
            {
                Assert.That(playfield.GameplayEndTime, Is.EqualTo(1500));
                Assert.That(playfield.GameplayEndTimeRefreshCount, Is.EqualTo(1));
                Assert.That(playfield.ArenaStateRefreshCount, Is.EqualTo(1));
            });

            for (int i = 0; i < 10; i++)
                playfield.RefreshCachedState();

            Assert.Multiple(() =>
            {
                Assert.That(playfield.GameplayEndTimeRefreshCount, Is.EqualTo(1));
                Assert.That(playfield.ArenaStateRefreshCount, Is.EqualTo(1));
            });

            bullet.StartTime = 2000;
            playfield.RefreshCachedState();

            Assert.Multiple(() =>
            {
                Assert.That(playfield.GameplayEndTime, Is.EqualTo(2500));
                Assert.That(playfield.GameplayEndTimeRefreshCount, Is.EqualTo(2));
                Assert.That(playfield.ArenaStateRefreshCount, Is.EqualTo(2));
            });

            bullet.Duration = 750;
            bullet.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            arenaChange.TargetSize = new Vector2(320, 240);
            arenaChange.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            playfield.RefreshCachedState();

            Assert.Multiple(() =>
            {
                Assert.That(playfield.GameplayEndTime, Is.EqualTo(2750));
                Assert.That(playfield.GameplayEndTimeRefreshCount, Is.EqualTo(3));
                Assert.That(playfield.ArenaStateRefreshCount, Is.EqualTo(3));
            });
        }
    }
}
