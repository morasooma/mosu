// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Exercises Dodge World combat and movement directly, with no game loop and no real time.
    /// </summary>
    [TestFixture]
    public class WorldSimulationTest
    {
        private const double frame = 16;

        private static readonly Vector2 room_size = new Vector2(2000, 1000);

        private WorldSimulation simulation = null!;

        /// <summary>The local player, which is the only one a client ever simulates.</summary>
        private PlayerState player = null!;

        [SetUp]
        public void SetUp()
        {
            simulation = new WorldSimulation();
            player = simulation.AddPlayer();
        }

        /// <summary>
        /// A stationary mob. A zone of zero size collapses the wander path to a point, so the mob
        /// stays exactly where the test puts it.
        /// </summary>
        private static MobSpawnDefinition stationaryMob(Vector2 position, int health = 2, int contactDamage = 12,
                                                        int projectileCount = 0, float projectileSpeed = 110,
                                                        float projectileRange = 240, int projectileDamage = 8,
                                                        float detectionRadius = 420, int attackCooldown = 2200,
                                                        string id = "zone", float speed = MobSpawnRules.DEFAULT_SPEED,
                                                        bool chases = false) =>
            new MobSpawnDefinition(id, position, Vector2.Zero, Count: 1, MaxHealth: health, ContactDamage: contactDamage,
                DetectionRadius: detectionRadius, ProjectileCount: projectileCount, ProjectileDamage: projectileDamage,
                ProjectileSpeed: projectileSpeed, ProjectileRange: projectileRange, AttackCooldown: attackCooldown,
                Speed: speed, Chases: chases);

        private void loadRoom(params Obstacle[] obstacles) =>
            simulation.LoadRoom(new RoomLayout(room_size, Vector2.Zero, obstacles, new List<MobSpawnDefinition>()));

        /// <summary>Advances the world in frame-sized steps, as the screen would.</summary>
        private void advance(double milliseconds, PlayerInput input = default)
        {
            for (double elapsed = 0; elapsed < milliseconds; elapsed += frame)
            {
                simulation.SetInput(player.UserId, input);
                simulation.Step(frame);
            }
        }

        [Test]
        public void RoomLoadPlacesPlayerAtSpawnWithFullHealth()
        {
            simulation.LoadRoom(new RoomLayout(room_size, new Vector2(10, 350), new List<Obstacle>(), new List<MobSpawnDefinition>()));

            Assert.That(player.Position, Is.EqualTo(new Vector2(10, 350)));
            Assert.That(player.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH));
        }

        [Test]
        public void SpawnZonesPopulateOnRoomLoad()
        {
            var zone = new MobSpawnDefinition("zone", Vector2.Zero, new Vector2(200, 200), Count: 3, MaxHealth: 3,
                ContactDamage: 10, DetectionRadius: 420, ProjectileCount: 0, ProjectileDamage: 8,
                ProjectileSpeed: 110, ProjectileRange: 240, AttackCooldown: 2200);

            simulation.LoadRoom(new RoomLayout(room_size, Vector2.Zero, new List<Obstacle>(), new[] { zone }));

            Assert.That(simulation.Mobs, Has.Count.EqualTo(3));
        }

        #region movement

        [Test]
        public void DiagonalMovementIsNotFaster()
        {
            loadRoom();
            advance(1000, new PlayerInput(new Vector2(1, 0), null));
            float straight = player.Position.X;

            simulation.LoadRoom(new RoomLayout(room_size, Vector2.Zero, new List<Obstacle>(), new List<MobSpawnDefinition>()));
            advance(1000, new PlayerInput(new Vector2(1, 1), null));
            float diagonal = player.Position.Length;

            Assert.That(diagonal, Is.EqualTo(straight).Within(0.5f));
        }

        [Test]
        public void ObstacleBlocksMovement()
        {
            loadRoom(new Obstacle(new Vector2(60, CombatRules.PLAYER_TORSO_OFFSET.Y), new Vector2(40, 200)));

            advance(1000, new PlayerInput(new Vector2(1, 0), null));

            Assert.That(player.Position.X, Is.LessThan(60));
        }

        [Test]
        public void WalkingIntoObstacleDiagonallySlidesAlongIt()
        {
            // A wall directly to the right. Pushing right and down should still make downward progress.
            loadRoom(new Obstacle(new Vector2(60, CombatRules.PLAYER_TORSO_OFFSET.Y), new Vector2(40, 400)));

            advance(500, new PlayerInput(new Vector2(1, 1), null));

            Assert.That(player.Position.X, Is.LessThan(60), "should not pass through the wall");
            Assert.That(player.Position.Y, Is.GreaterThan(50), "should still slide downwards");
        }

        [Test]
        public void PlayerIsClampedInsideRoom()
        {
            loadRoom();

            advance(20000, new PlayerInput(new Vector2(1, 0), null));

            Assert.That(player.Position.X,
                Is.EqualTo(room_size.X / 2 - CombatRules.ROOM_EDGE_PADDING).Within(0.01f));
        }

        [Test]
        public void ReleasingKeysStopsMovement()
        {
            loadRoom();
            advance(200, new PlayerInput(new Vector2(1, 0), null));
            Assert.That(player.Moving, Is.True);

            advance(frame * 2);

            Assert.That(player.Moving, Is.False);
            Assert.That(player.StepDistance, Is.Zero);
        }

        #endregion

        #region player attack

        [Test]
        public void SwingDamagesMobInsideArc()
        {
            loadRoom();
            MobState mob = simulation.SpawnMob(stationaryMob(new Vector2(70, 0)));

            simulation.Swing(Vector2.UnitX);

            Assert.That(mob.Health, Is.EqualTo(mob.MaximumHealth - player.SwordDamage));
        }

        [Test]
        public void SwingLeavesMobBehindPlayerUntouched()
        {
            loadRoom();
            MobState behind = simulation.SpawnMob(stationaryMob(new Vector2(-70, 0)));

            simulation.Swing(Vector2.UnitX);

            Assert.That(behind.Health, Is.EqualTo(behind.MaximumHealth));
        }

        [Test]
        public void SwingLeavesMobBeyondReachUntouched()
        {
            loadRoom();
            MobState far = simulation.SpawnMob(stationaryMob(new Vector2(CombatRules.ATTACK_RADIUS + 40, 0)));

            simulation.Swing(Vector2.UnitX);

            Assert.That(far.Health, Is.EqualTo(far.MaximumHealth));
        }

        [Test]
        public void SwingIsRateLimited()
        {
            loadRoom();
            MobState mob = simulation.SpawnMob(stationaryMob(new Vector2(70, 0), health: 10));

            simulation.Swing(Vector2.UnitX);
            int afterFirst = mob.Health;

            advance(CombatRules.ATTACK_COOLDOWN / 2);
            simulation.Swing(Vector2.UnitX);

            Assert.That(mob.Health, Is.EqualTo(afterFirst), "a swing inside the cooldown should do nothing");

            advance(CombatRules.ATTACK_COOLDOWN);
            simulation.Swing(Vector2.UnitX);

            Assert.That(mob.Health, Is.EqualTo(afterFirst - player.SwordDamage));
        }

        [Test]
        public void MobIsDefeatedOnceHealthIsSpent()
        {
            loadRoom();
            MobState mob = simulation.SpawnMob(stationaryMob(new Vector2(70, 0), health: 2));

            MobState? defeated = null;
            simulation.MobDefeated += (m, _) => defeated = m;

            simulation.Swing(Vector2.UnitX);
            Assert.That(simulation.Mobs, Has.Count.EqualTo(1));

            advance(CombatRules.ATTACK_COOLDOWN + frame);
            simulation.Swing(Vector2.UnitX);

            Assert.That(simulation.Mobs, Is.Empty);
            Assert.That(defeated, Is.SameAs(mob));
        }

        [Test]
        public void DefeatedMobReturnsAfterRespawnDelay()
        {
            var zone = new MobSpawnDefinition("zone", new Vector2(70, 0), Vector2.Zero, Count: 1, MaxHealth: 1,
                ContactDamage: 0, DetectionRadius: 0, ProjectileCount: 0, ProjectileDamage: 1,
                ProjectileSpeed: 110, ProjectileRange: 240, AttackCooldown: 2200);

            simulation.LoadRoom(new RoomLayout(room_size, Vector2.Zero, new List<Obstacle>(), new[] { zone }));
            Assert.That(simulation.Mobs, Has.Count.EqualTo(1));

            simulation.Swing(Vector2.UnitX);
            Assert.That(simulation.Mobs, Is.Empty);

            advance(CombatRules.MOB_RESPAWN_DELAY / 2);
            Assert.That(simulation.Mobs, Is.Empty, "should not return early");

            advance(CombatRules.MOB_RESPAWN_DELAY);
            Assert.That(simulation.Mobs, Has.Count.EqualTo(1));
        }

        /// <summary>
        /// What a swing takes off is the world's to decide. It was fixed at one, which made a mob's health
        /// a count of swings and left weapon skins as pictures with no effect on a fight.
        /// </summary>
        [Test]
        public void TheSwordDecidesWhatASwingTakesOff()
        {
            loadRoom();
            MobState mob = simulation.SpawnMob(stationaryMob(new Vector2(70, 0), health: 20));

            player.SetSwordDamage(6);
            simulation.Swing(Vector2.UnitX);

            Assert.That(mob.Health, Is.EqualTo(mob.MaximumHealth - 6));
        }

        #endregion

        #region mob movement

        /// <summary>
        /// A mob that chases walks at whoever it has noticed, instead of tracing its own curve while the
        /// player stands next to it.
        /// </summary>
        [Test]
        public void AChasingMobWalksTowardsThePlayer()
        {
            loadRoom();
            simulation.PlacePlayer(Vector2.Zero);

            MobState mob = simulation.SpawnMob(stationaryMob(new Vector2(600, 0), health: 99, contactDamage: 1,
                detectionRadius: 2000, speed: 150, chases: true));

            advance(1000);

            Assert.That(mob.Position.X, Is.LessThan(500), "the mob did not come any closer");
        }

        /// <summary>
        /// It stops just inside touching distance rather than standing on the player: close enough to keep
        /// hurting them, far enough that the two bodies are still two bodies.
        /// </summary>
        [Test]
        public void AChasingMobStopsShortOfStandingOnThePlayer()
        {
            loadRoom();
            simulation.PlacePlayer(Vector2.Zero);

            MobState mob = simulation.SpawnMob(stationaryMob(new Vector2(400, 0), health: 99, contactDamage: 1,
                detectionRadius: 2000, speed: 200, chases: true));

            advance(4000);

            Assert.Multiple(() =>
            {
                Assert.That((mob.Torso - player.Torso).Length, Is.GreaterThan(8), "the mob walked into the player");
                Assert.That(player.Health, Is.LessThan(PlayerState.MAXIMUM_HEALTH), "close enough to hurt, and it did not");
            });
        }

        /// <summary>
        /// Nobody noticed, nothing changes: it keeps to its zone, which is how every mob behaved before a
        /// zone could ask for a chase.
        /// </summary>
        [Test]
        public void AChasingMobThatHasNoticedNobodyKeepsToItsZone()
        {
            loadRoom();
            simulation.PlacePlayer(Vector2.Zero);

            MobState mob = simulation.SpawnMob(stationaryMob(new Vector2(800, 0), health: 99,
                detectionRadius: 100, speed: 150, chases: true));

            advance(2000);

            Assert.That(mob.Position, Is.EqualTo(new Vector2(800, 0)), "it left a zone it had no reason to leave");
        }

        /// <summary>
        /// Zero speed is how a mob that holds its ground is authored — a turret rather than a walker.
        /// </summary>
        [Test]
        public void AMobWithNoSpeedHoldsItsGround()
        {
            loadRoom();
            simulation.PlacePlayer(Vector2.Zero);

            MobState mob = simulation.SpawnMob(stationaryMob(new Vector2(300, 0), health: 99,
                detectionRadius: 2000, speed: 0, chases: true));

            advance(2000);

            Assert.That(mob.Position, Is.EqualTo(new Vector2(300, 0)));
        }

        #endregion

        #region mob attacks

        [Test]
        public void ContactDamageIsRateLimited()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(Vector2.Zero, health: 99, contactDamage: 12));

            advance(frame);
            int afterFirst = player.Health;
            Assert.That(afterFirst, Is.EqualTo(PlayerState.MAXIMUM_HEALTH - 12));

            advance(CombatRules.CONTACT_DAMAGE_COOLDOWN / 2);
            Assert.That(player.Health, Is.EqualTo(afterFirst), "contact inside the cooldown should not hurt");

            advance(CombatRules.CONTACT_DAMAGE_COOLDOWN);
            Assert.That(player.Health, Is.EqualTo(afterFirst - 12));
        }

        [Test]
        public void RangedMobTelegraphsBeforeFiring()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(new Vector2(300, 0), health: 99, contactDamage: 0, projectileCount: 8));

            advance(frame);

            Assert.That(simulation.Telegraphs, Has.Count.EqualTo(1));
            Assert.That(simulation.Projectiles, Is.Empty, "nothing should be in flight during the telegraph");

            advance(CombatRules.MOB_ATTACK_TELEGRAPH_DURATION + frame);

            Assert.That(simulation.Telegraphs, Is.Empty);
            Assert.That(simulation.Projectiles, Has.Count.EqualTo(8), "one projectile per configured count");
        }

        [Test]
        public void MobOutsideDetectionRadiusDoesNotFire()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(new Vector2(900, 0), health: 99, contactDamage: 0,
                projectileCount: 8, detectionRadius: 200));

            advance(CombatRules.MOB_ATTACK_TELEGRAPH_DURATION * 2);

            Assert.That(simulation.Telegraphs, Is.Empty);
            Assert.That(simulation.Projectiles, Is.Empty);
        }

        [Test]
        public void MobKilledDuringTelegraphNeverFires()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(new Vector2(70, 0), health: 1, contactDamage: 0, projectileCount: 8));

            advance(frame);
            Assert.That(simulation.Telegraphs, Has.Count.EqualTo(1));

            simulation.Swing(Vector2.UnitX);
            Assert.That(simulation.Mobs, Is.Empty);

            advance(CombatRules.MOB_ATTACK_TELEGRAPH_DURATION + frame);

            Assert.That(simulation.Projectiles, Is.Empty);
        }

        [Test]
        public void ProjectileExpiresAtEndOfItsRange()
        {
            loadRoom();
            // Aimed away from the player, so it expires rather than landing.
            simulation.SpawnMob(stationaryMob(new Vector2(-600, 0), health: 99, contactDamage: 0,
                projectileCount: 1, projectileSpeed: 200, projectileRange: 100, detectionRadius: 2000));

            advance(CombatRules.MOB_ATTACK_TELEGRAPH_DURATION + frame * 2);
            Assert.That(simulation.Projectiles, Is.Not.Empty);

            // 100 units of range at 200 units/second is half a second of flight.
            advance(600);

            Assert.That(simulation.Projectiles, Is.Empty);
        }

        [Test]
        public void ProjectileHitCostsPlayerHealth()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(new Vector2(160, 0), health: 99, contactDamage: 0,
                projectileCount: 1, projectileDamage: 8, projectileSpeed: 110, projectileRange: 400));

            advance(CombatRules.MOB_ATTACK_TELEGRAPH_DURATION + 2500);

            Assert.That(player.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH - 8));
        }

        #endregion

        #region death

        /// <summary>
        /// Death is reported, not resolved: the simulation leaves the player dead and lets the screen
        /// decide which room to send them to.
        /// </summary>
        [Test]
        public void PlayerDeathIsReportedWithoutRespawning()
        {
            simulation.LoadRoom(new RoomLayout(room_size, new Vector2(0, 350), new List<Obstacle>(), new List<MobSpawnDefinition>()));
            player.Position = Vector2.Zero;

            // Enough contact damage to kill outright.
            simulation.SpawnMob(stationaryMob(Vector2.Zero, health: 99, contactDamage: PlayerState.MAXIMUM_HEALTH));

            var died = false;
            simulation.PlayerDied += _ => died = true;

            advance(frame);

            Assert.That(died, Is.True);
            Assert.That(player.Health, Is.Zero);
        }

        /// <summary>
        /// Loading a room places the player at its entrance and leaves their health alone. Rooms are
        /// doorways, not checkpoints: healing here meant walking through any door undid every wound.
        /// </summary>
        [Test]
        public void LoadingARoomMovesThePlayerWithoutHealingThem()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(Vector2.Zero, health: 99, contactDamage: 25));
            advance(frame);

            int hurt = player.Health;
            Assert.That(hurt, Is.LessThan(PlayerState.MAXIMUM_HEALTH));

            simulation.LoadRoom(new RoomLayout(room_size, new Vector2(0, 350), new List<Obstacle>(), new List<MobSpawnDefinition>()));

            Assert.That(player.Position, Is.EqualTo(new Vector2(0, 350)));
            Assert.That(player.Health, Is.EqualTo(hurt), "walking into another room healed the player");
        }

        /// <summary>
        /// Nobody is ever put down inside a wall. Arrival points are placed beside the way back, and the
        /// way back is usually up against something — and a player standing inside an obstacle cannot walk
        /// out of it, because both axes are refused while the box overlaps.
        /// </summary>
        [Test]
        public void ArrivingInsideSomethingLeavesThePlayerBesideItInstead()
        {
            var wall = new Obstacle(new Vector2(200, 0), new Vector2(160, 160));
            loadRoom(wall);

            simulation.PlacePlayer(wall.Centre);

            Assert.That(simulation.CanOccupy(player.Position), Is.True, "the player was left standing inside a wall");

            // Beside it, not across the room: an arrival point says where the author wants the player.
            Assert.That((player.Position - wall.Centre).Length, Is.LessThan(200));
        }

        /// <summary>
        /// And if something does end up on top of them — an object dropped there in the editor, or a room
        /// whose layout changed underfoot — they can still walk out. Being unable to move at all is worse
        /// than a step through a wall.
        /// </summary>
        [Test]
        public void APlayerWithSomethingDroppedOnThemCanStillWalkOutOfIt()
        {
            loadRoom();
            simulation.PlacePlayer(Vector2.Zero);

            simulation.ReplaceLayout(new RoomLayout(room_size, Vector2.Zero,
                new[] { new Obstacle(Vector2.Zero, new Vector2(400, 400)) }, new List<MobSpawnDefinition>()));

            Assert.That(simulation.CanOccupy(player.Position), Is.False, "the test means to bury the player");

            advance(500, new PlayerInput(new Vector2(1, 0), null));

            Assert.That(player.Position.X, Is.GreaterThan(0), "the player could not walk out of what buried them");
        }

        /// <summary>
        /// A turned wall blocks where it is drawn, not where its bounding box is. Before the obstacle knew
        /// its own angle there was nothing to turn, which is why the kinds that block movement did not
        /// offer a rotation at all.
        /// </summary>
        [Test]
        public void ATurnedWallBlocksAlongItselfRatherThanAroundItself()
        {
            // A long thin wall lying at 45°, so its bounding box covers both diagonal corners while the
            // wall itself covers neither.
            var wall = new Obstacle(Vector2.Zero, new Vector2(400, 40), 45);
            loadRoom(wall);

            Vector2 alongTheWall = new Vector2(100, 100) - CombatRules.PLAYER_TORSO_OFFSET;
            Vector2 besideTheWall = new Vector2(100, -100) - CombatRules.PLAYER_TORSO_OFFSET;

            Assert.Multiple(() =>
            {
                Assert.That(simulation.CanOccupy(alongTheWall), Is.False, "the wall runs through this corner");
                Assert.That(simulation.CanOccupy(besideTheWall), Is.True, "this corner is only inside the bounding box");
            });
        }

        /// <summary>
        /// And a player put down inside a turned wall is pushed out of a side it actually has.
        /// </summary>
        [Test]
        public void BeingPutInsideATurnedWallStillLeavesTheirFeetFree()
        {
            var wall = new Obstacle(new Vector2(200, 0), new Vector2(400, 60), 30);
            loadRoom(wall);

            simulation.PlacePlayer(wall.Centre);

            Assert.That(simulation.CanOccupy(player.Position), Is.True, "the player was left standing inside a turned wall");
            Assert.That((player.Position - wall.Centre).Length, Is.LessThan(200));
        }

        /// <summary>
        /// Whoever decides a player should be whole again says so: the screen, on death. Nothing else
        /// hands out health, which is what keeps damage worth avoiding.
        /// </summary>
        [Test]
        public void HealingIsAskedForRatherThanImplied()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(Vector2.Zero, health: 99, contactDamage: PlayerState.MAXIMUM_HEALTH));
            advance(frame);
            Assert.That(player.Health, Is.Zero);

            player.HealFull();
            Assert.That(player.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH));
        }

        /// <summary>
        /// Death used to empty the room of everything in flight. That is wrong once a room can hold more
        /// than one player: one player dying must not disarm the volley aimed at the next. Loading a
        /// room is what clears it, and that is where the client goes after dying.
        /// </summary>
        [Test]
        public void PlayerDeathLeavesTheRoomAloneAndReloadingClearsIt()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(new Vector2(300, 0), health: 99, contactDamage: 0, projectileCount: 8));

            advance(CombatRules.MOB_ATTACK_TELEGRAPH_DURATION + frame);
            Assert.That(simulation.Projectiles, Is.Not.Empty);

            simulation.SpawnMob(stationaryMob(Vector2.Zero, health: 99, contactDamage: PlayerState.MAXIMUM_HEALTH, id: "melee"));
            advance(frame);

            Assert.That(player.Health, Is.Zero, "the melee mob should have killed the player");
            Assert.That(simulation.Projectiles, Is.Not.Empty, "the volley belongs to the room, not to the player");

            loadRoom();
            Assert.That(simulation.Projectiles, Is.Empty);
        }

        /// <summary>
        /// A dead player is no longer a target: they stop being shot at and stop taking contact damage,
        /// which is what stops the server hammering a corpse until its client reloads the room.
        /// </summary>
        [Test]
        public void ADeadPlayerIsNoLongerATarget()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(Vector2.Zero, health: 99, contactDamage: PlayerState.MAXIMUM_HEALTH));

            var deaths = 0;
            simulation.PlayerDied += _ => deaths++;

            advance(CombatRules.CONTACT_DAMAGE_COOLDOWN * 3);

            Assert.That(player.Health, Is.Zero);
            Assert.That(deaths, Is.EqualTo(1), "death should be reported once, not on every cooldown");
        }

        /// <summary>
        /// A room on the server holds everyone in it, so a mob has to pick a target rather than assume
        /// there is only one.
        /// </summary>
        [Test]
        public void MobsAimAtTheNearestLivingPlayer()
        {
            var simulation = new WorldSimulation { MovesPlayers = false };
            simulation.LoadRoom(new RoomLayout(room_size, Vector2.Zero, new List<Obstacle>(), new List<MobSpawnDefinition>()));

            PlayerState near = simulation.AddPlayer(1);
            PlayerState far = simulation.AddPlayer(2);

            simulation.SetPlayerPosition(1, new Vector2(-100, 0), Vector2.UnitY, false);
            simulation.SetPlayerPosition(2, new Vector2(-380, 0), Vector2.UnitY, false);

            simulation.SpawnMob(stationaryMob(Vector2.Zero, health: 99, contactDamage: 0, projectileCount: 1,
                projectileSpeed: 600, projectileRange: 900, projectileDamage: 5));

            for (double elapsed = 0; elapsed < CombatRules.MOB_ATTACK_TELEGRAPH_DURATION + 1200; elapsed += frame)
                simulation.Step(frame);

            Assert.That(near.Health, Is.LessThan(PlayerState.MAXIMUM_HEALTH), "the aimed shot should reach the nearer player");
            Assert.That(far.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH), "and be spent on them rather than carrying on");
        }

        /// <summary>
        /// Contact damage is throttled per player: one player standing in a mob must not make everybody
        /// else in the room immune while the cooldown runs.
        /// </summary>
        [Test]
        public void ContactDamageIsThrottledPerPlayerRatherThanPerRoom()
        {
            var simulation = new WorldSimulation { MovesPlayers = false };
            simulation.LoadRoom(new RoomLayout(room_size, Vector2.Zero, new List<Obstacle>(), new List<MobSpawnDefinition>()));

            PlayerState first = simulation.AddPlayer(1);
            PlayerState second = simulation.AddPlayer(2);

            simulation.SetPlayerPosition(1, Vector2.Zero, Vector2.UnitY, false);
            simulation.SetPlayerPosition(2, Vector2.Zero, Vector2.UnitY, false);

            simulation.SpawnMob(stationaryMob(Vector2.Zero, health: 99, contactDamage: 7));

            simulation.Step(frame);

            Assert.That(first.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH - 7));
            Assert.That(second.Health, Is.EqualTo(PlayerState.MAXIMUM_HEALTH - 7));
        }

        /// <summary>
        /// The server does not move players: their position arrives from their own client, and recomputing
        /// it from input would fight with them.
        /// </summary>
        [Test]
        public void ASimulationThatDoesNotMovePlayersIgnoresTheirInput()
        {
            var simulation = new WorldSimulation { MovesPlayers = false };
            simulation.LoadRoom(new RoomLayout(room_size, Vector2.Zero, new List<Obstacle>(), new List<MobSpawnDefinition>()));

            PlayerState player = simulation.AddPlayer(1);
            simulation.SetPlayerPosition(1, new Vector2(50, 60), Vector2.UnitY, false);

            simulation.SetInput(1, new PlayerInput(Vector2.UnitX, null));
            simulation.Step(frame);

            Assert.That(player.Position, Is.EqualTo(new Vector2(50, 60)));
        }

        /// <summary>
        /// Leaving a room has to report its mobs as gone, or their drawables stay on screen in the
        /// room the player moves to.
        /// </summary>
        [Test]
        public void RoomLoadReportsRemovalOfPreviousMobs()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(new Vector2(200, 0)));
            simulation.SpawnMob(stationaryMob(new Vector2(300, 0), id: "second"));

            var removed = new List<int>();
            simulation.MobRemoved += mob => removed.Add(mob.Id);

            loadRoom();

            Assert.That(removed, Has.Count.EqualTo(2));
            Assert.That(simulation.Mobs, Is.Empty);
        }

        [Test]
        public void RoomLoadClearsCombatFromPreviousRoom()
        {
            loadRoom();
            simulation.SpawnMob(stationaryMob(new Vector2(300, 0), health: 99, contactDamage: 0, projectileCount: 8));

            advance(CombatRules.MOB_ATTACK_TELEGRAPH_DURATION + frame);
            Assert.That(simulation.Projectiles, Is.Not.Empty);

            loadRoom();

            Assert.That(simulation.Projectiles, Is.Empty);
            Assert.That(simulation.Telegraphs, Is.Empty);
            Assert.That(simulation.Mobs, Is.Empty);
            Assert.That(simulation.CurrentTime, Is.Zero);
        }

        #endregion
    }
}
