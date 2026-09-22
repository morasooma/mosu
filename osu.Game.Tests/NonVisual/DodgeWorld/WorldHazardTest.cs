// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Exercises the repeating hazards a course is built from: emitters and beams, with no game loop and
    /// no real time.
    /// </summary>
    /// <remarks>
    /// Everything here is a function of the time since the room was entered, which is the property that
    /// lets an author stagger several devices into a pattern and lets two clients agree on it.
    /// </remarks>
    [TestFixture]
    public class WorldHazardTest
    {
        private const double frame = 16;

        private static readonly Vector2 room_size = new Vector2(2000, 1000);

        private WorldSimulation simulation = null!;
        private PlayerState player = null!;

        [SetUp]
        public void SetUp()
        {
            simulation = new WorldSimulation();
            player = simulation.AddPlayer();
        }

        private static HazardDefinition emitter(int cycle = 1000, int phase = 0, int count = 3, float spread = 90,
                                                float direction = 0, float turn = 0, float speed = 200,
                                                float range = 600, int damage = 8, string id = "emitter") =>
            new HazardDefinition(id, HazardKind.Emitter, Vector2.Zero, direction, turn, cycle, phase, count, spread,
                damage, speed, range, 0, 0);

        private static HazardDefinition beam(int cycle = 1000, int active = 400, int phase = 0, float direction = 0,
                                             float turn = 0, float length = 800, float width = 40, int damage = 12,
                                             string id = "beam") =>
            new HazardDefinition(id, HazardKind.Beam, Vector2.Zero, direction, turn, cycle, phase, 0, 0, damage, 0,
                length, width, active);

        private void loadRoom(params HazardDefinition[] hazards)
        {
            simulation.LoadRoom(new RoomLayout(room_size, Vector2.Zero, Array.Empty<Obstacle>(),
                Array.Empty<MobSpawnDefinition>(), hazards));

            // Out of the way by default. Every hazard here sits at the origin, and so does the room's
            // spawn, so a test counting shots would otherwise have the player absorb them as they appear.
            simulation.PlacePlayer(new Vector2(0, 460));
        }

        private void advance(double milliseconds)
        {
            for (double elapsed = 0; elapsed < milliseconds; elapsed += frame)
                simulation.Step(frame);
        }

        /// <summary>
        /// Walking into a room must not cost a shot the player never saw coming, so the first volley is one
        /// whole cycle away rather than immediate.
        /// </summary>
        [Test]
        public void AnEmitterFiresOnceACycleStartingAfterTheFirst()
        {
            loadRoom(emitter(cycle: 1000, count: 3));

            advance(900);
            Assert.That(simulation.Projectiles, Is.Empty, "fired before its first full cycle");

            advance(200);
            Assert.That(simulation.Projectiles.Count, Is.EqualTo(3));

            advance(1000);
            Assert.That(simulation.Projectiles.Count, Is.EqualTo(6));
        }

        /// <summary>
        /// The phase is what an author staggers a course with: same cycle, different offsets, one wave.
        /// </summary>
        [Test]
        public void ThePhaseDelaysAnEmitterWithoutChangingItsRhythm()
        {
            loadRoom(emitter(cycle: 1000, count: 1, id: "early"),
                emitter(cycle: 1000, phase: 500, count: 1, id: "late"));

            advance(1100);
            Assert.That(simulation.Projectiles.Count, Is.EqualTo(1), "both fired at once");

            advance(500);
            Assert.That(simulation.Projectiles.Count, Is.EqualTo(2));
        }

        /// <summary>
        /// A spread of zero is a single stream; 360 is a ring that divides evenly instead of doubling up
        /// at the seam.
        /// </summary>
        [Test]
        public void TheSpreadDecidesTheShapeOfTheFan()
        {
            loadRoom(emitter(cycle: 500, count: 4, spread: 0, direction: 0));
            advance(600);

            Assert.That(simulation.Projectiles.Count, Is.EqualTo(4));
            Assert.That(simulation.Projectiles.Select(shot => shot.Direction.X), Has.All.EqualTo(1).Within(0.001f),
                "a spread of zero should be one aimed stream");

            SetUp();
            loadRoom(emitter(cycle: 500, count: 4, spread: 360));
            advance(600);

            float[] angles = simulation.Projectiles
                                       .Select(shot => MathF.Atan2(shot.Direction.Y, shot.Direction.X))
                                       .OrderBy(angle => angle)
                                       .ToArray();

            Assert.That(angles.Length, Is.EqualTo(4));
            Assert.That(angles.Distinct().Count(), Is.EqualTo(4), "a ring should not fire twice in one direction");
        }

        /// <summary>
        /// A turn per cycle is how a spinner is authored: one device, a different angle each time.
        /// </summary>
        [Test]
        public void ATurnPerCycleRotatesTheFan()
        {
            loadRoom(emitter(cycle: 500, count: 1, spread: 0, direction: 0, turn: 90));

            advance(600);
            float first = MathF.Atan2(simulation.Projectiles[0].Direction.Y, simulation.Projectiles[0].Direction.X);

            advance(500);
            ProjectileState second = simulation.Projectiles[^1];
            float turned = MathF.Atan2(second.Direction.Y, second.Direction.X);

            Assert.That(turned, Is.Not.EqualTo(first).Within(0.01f));
            Assert.That(MathF.Abs(turned - first), Is.EqualTo(MathF.PI / 2).Within(0.02f));
        }

        /// <summary>
        /// A frame long enough to cover several cycles must fire once rather than once per cycle missed: a
        /// stutter should not turn into a wall of bullets.
        /// </summary>
        [Test]
        public void ALongFrameSkipsMissedVolleysRatherThanCatchingUp()
        {
            var fired = 0;

            loadRoom(emitter(cycle: 300, count: 1));
            simulation.ProjectileSpawned += _ => fired++;

            // Ten cycles' worth in one step. Counted as they are spawned, because a shot that far into its
            // own flight may already be out of range by the time the step ends.
            simulation.Step(3000);

            Assert.That(fired, Is.EqualTo(1));
        }

        /// <summary>
        /// Walking into a room must be safe for one cycle, so a course is entered rather than spawned into.
        /// </summary>
        [Test]
        public void NothingHarmsAnybodyDuringTheCycleTheRoomWasEnteredOn()
        {
            loadRoom(beam(cycle: 1000, active: 900, width: 40, length: 800));
            simulation.PlacePlayer(new Vector2(300, 0));

            advance(800);
            Assert.That(hazard.Active, Is.False, "lit during the cycle the player walked in on");
            Assert.That(player.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH));
        }

        [Test]
        public void ABeamIsLethalForItsActivePartOfTheCycle()
        {
            loadRoom(beam(cycle: 1000, active: 400, width: 40, length: 800));

            // On the line, a little along it.
            simulation.PlacePlayer(new Vector2(300, 0));

            advance(1100);
            Assert.That(hazard.Active, Is.True, "not lit at the start of a running cycle");
            Assert.That(player.Health, Is.LessThan(PlayerState.MAXIMUM_HEALTH));

            int burnt = player.Health;

            advance(500);
            Assert.That(hazard.Active, Is.False, "still lit past its active window");
            Assert.That(player.Health, Is.EqualTo(burnt), "burnt while off");
        }

        /// <summary>
        /// A beam warns before it becomes lethal, so a course can be learned rather than memorised.
        /// </summary>
        [Test]
        public void ABeamWarnsBeforeItLights()
        {
            loadRoom(beam(cycle: 2000, active: 400));

            advance(1000);
            Assert.That(hazard.Active, Is.False);
            Assert.That(hazard.Warning, Is.False, "warning far too early");

            // Inside the last 600 ms before the window opens.
            advance(500);
            Assert.That(hazard.Active, Is.False);
            Assert.That(hazard.Warning, Is.True);
        }

        [Test]
        public void ABeamOnlyBurnsWhatItCovers()
        {
            loadRoom(beam(cycle: 1000, active: 900, width: 40, length: 400, direction: 0));

            // Past the first cycle, so the beam is running.
            advance(1100);

            // Past the far end of the line, so on the beam's axis but out of its reach.
            simulation.PlacePlayer(new Vector2(900, 0));
            advance(200);
            Assert.That(player.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH), "burnt beyond its length");

            // Beside the line, further than half its width.
            simulation.PlacePlayer(new Vector2(200, 300));
            advance(200);
            Assert.That(player.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH), "burnt beside the beam");
        }

        /// <summary>
        /// Standing in a beam should not empty a health bar in one frame; it is throttled like walking
        /// into a mob.
        /// </summary>
        [Test]
        public void StandingInABeamIsThrottled()
        {
            loadRoom(beam(cycle: 2000, active: 1900, damage: 5));

            // Into the second cycle, where the beam runs, and then onto the line.
            advance(2100);
            simulation.PlacePlayer(new Vector2(100, 0));

            advance(600);
            int once = PlayerState.MAXIMUM_HEALTH - player.Health;
            Assert.That(once, Is.EqualTo(5));

            advance(600);
            Assert.That(PlayerState.MAXIMUM_HEALTH - player.Health, Is.EqualTo(10));
        }

        /// <summary>
        /// A course is not a fight: hazards run whether anybody is there or not, and never aim.
        /// </summary>
        [Test]
        public void HazardsIgnoreWhereThePlayerIs()
        {
            loadRoom(emitter(cycle: 500, count: 1, spread: 0, direction: 0));

            simulation.PlacePlayer(new Vector2(0, 400));
            advance(600);

            Assert.That(simulation.Projectiles[0].Direction.X, Is.EqualTo(1).Within(0.001f),
                "an emitter should not have aimed at anybody");
        }

        /// <summary>
        /// Reloading a room, which is what an edit in the editor does, must not leave the old course
        /// running alongside the new one.
        /// </summary>
        [Test]
        public void ReplacingTheLayoutReplacesTheCourse()
        {
            loadRoom(emitter(cycle: 500, count: 2, id: "first"));
            advance(600);
            Assert.That(simulation.Hazards.Count, Is.EqualTo(1));

            simulation.ReplaceLayout(new RoomLayout(room_size, Vector2.Zero, Array.Empty<Obstacle>(),
                Array.Empty<MobSpawnDefinition>(), new List<HazardDefinition> { beam(id: "second") }));

            Assert.That(simulation.Hazards.Count, Is.EqualTo(1));
            Assert.That(simulation.Hazards[0].EntityId, Is.EqualTo("second"));
            Assert.That(simulation.Projectiles, Is.Empty, "the old course's shots outlived it");
        }

        /// <summary>
        /// A device is placed by its feet like everything else, but what the player sees is the housing in
        /// the middle of it. Firing from the anchor put the shots out from under the device.
        /// </summary>
        [Test]
        public void AStoredDeviceFiresFromItsHousingAndNotFromItsFeet()
        {
            var record = new EntityRecord { Id = "emitter", Kind = EntityKinds.EMITTER, X = 100, Y = 200 };

            Assert.That(EmitterKind.Describe(record).Origin,
                Is.EqualTo(new Vector2(100, 200 - CombatRules.HAZARD_MUZZLE_HEIGHT)));

            // A device the author shrank has a smaller housing, so the muzzle follows the scale.
            record.Scale = 0.5f;
            Assert.That(EmitterKind.Describe(record).Origin.Y,
                Is.EqualTo(200 - CombatRules.HAZARD_MUZZLE_HEIGHT / 2).Within(0.001f));

            // Both kinds of device are built the same way, so a beam starts where an emitter would fire.
            Assert.That(BeamKind.Describe(record).Origin, Is.EqualTo(EmitterKind.Describe(record).Origin));
        }

        /// <summary>
        /// The hitbox is the square the player is looking at. A shot that passes beside the body must miss
        /// even though it passes over the ground the body stands on.
        /// </summary>
        [Test]
        public void AShotHitsThePlayerExactlyWhereTheBodyIsDrawn()
        {
            // Two units of clearance between the drawn body's top edge, widened by the shot's own radius,
            // and the line the shots travel along.
            float beside = -CombatRules.PLAYER_TORSO_OFFSET.Y - CombatRules.PLAYER_HALF_SIZE
                           - CombatRules.PROJECTILE_RADIUS - 2;

            fireOneStreamAt(new Vector2(400, beside));
            Assert.That(player.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH), "hit by a shot passing beside the body");

            // Four units the other way, so the same line crosses the body instead of clearing it.
            fireOneStreamAt(new Vector2(400, beside + 4));
            Assert.That(player.Health, Is.LessThan(PlayerState.MAXIMUM_HEALTH), "missed a shot drawn over the body");
        }

        /// <summary>
        /// Fires a single stream along the room's middle and lets it reach <paramref name="standing"/>.
        /// </summary>
        private void fireOneStreamAt(Vector2 standing)
        {
            SetUp();
            loadRoom(emitter(cycle: 400, count: 1, spread: 0, direction: 0, speed: 400, range: 1500));
            simulation.PlacePlayer(standing);
            advance(1600);
        }

        private HazardState hazard => simulation.Hazards[0];
    }
}
