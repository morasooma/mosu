// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Online.DodgeWorld;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// Draws the mobs, shots and warnings the server is running.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="SimulationView"/> for a room the server owns. Positions arrive about
    /// twenty times a second, which is not enough to draw from directly: mobs are eased towards where they
    /// were last reported, and shots carry their velocity so they can be carried on between snapshots
    /// instead of stuttering. Nothing here decides anything — a mob's health, its death and the damage it
    /// does are all the server's answers.
    /// </remarks>
    internal sealed partial class RemoteCombatView : CompositeDrawable
    {
        /// <summary>How quickly a drawn mob closes the gap to where it was last reported.</summary>
        private const double mob_smoothing = 70;

        private readonly Container worldCamera;
        private readonly OsuColour colours;
        private readonly DodgeWorldClient client;
        private readonly Func<string, MobAppearance> mobAppearance;

        private readonly Dictionary<int, RemoteMob> mobs = new Dictionary<int, RemoteMob>();
        private readonly Dictionary<int, RemoteProjectile> projectiles = new Dictionary<int, RemoteProjectile>();
        private readonly Dictionary<int, MobAttackTelegraph> telegraphs = new Dictionary<int, MobAttackTelegraph>();
        private readonly Dictionary<int, WorldBeam> beams = new Dictionary<int, WorldBeam>();

        private bool dimmed;

        public RemoteCombatView(Container worldCamera, OsuColour colours, DodgeWorldClient client,
                                Func<string, MobAppearance> mobAppearance)
        {
            this.worldCamera = worldCamera;
            this.colours = colours;
            this.client = client;
            this.mobAppearance = mobAppearance;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            client.RoomStateReceived += onRoomState;
            client.MobDefeated += onMobDefeated;
        }

        /// <summary>
        /// A death is announced the moment it happens, so the mob goes as it is killed rather than at the
        /// next snapshot, up to a twentieth of a second later.
        /// </summary>
        private void onMobDefeated(int mobId, int byUserId, string zoneId) => retire(mobId);

        /// <summary>
        /// Fades the mobs, as the editor does to its own.
        /// </summary>
        public void SetDimmed(bool value)
        {
            dimmed = value;

            foreach (RemoteMob mob in mobs.Values)
                mob.Drawable.FadeTo(value ? 0.3f : 1, 160, Easing.OutQuint);
        }

        /// <summary>
        /// Retires every drawable, for leaving a room or for the server handing its mobs back.
        /// </summary>
        public void Clear()
        {
            foreach (RemoteMob mob in mobs.Values)
                worldCamera.Remove(mob.Drawable, true);

            foreach (RemoteProjectile projectile in projectiles.Values)
                worldCamera.Remove(projectile.Drawable, true);

            foreach (MobAttackTelegraph telegraph in telegraphs.Values)
                worldCamera.Remove(telegraph, true);

            foreach (WorldBeam beam in beams.Values)
                worldCamera.Remove(beam, true);

            mobs.Clear();
            projectiles.Clear();
            telegraphs.Clear();
            beams.Clear();
        }

        private void onRoomState()
        {
            DodgeWorldRoomSnapshot snapshot = client.RoomState;

            syncMobs(snapshot.Mobs);
            syncProjectiles(snapshot.Projectiles);
            syncTelegraphs(snapshot.Telegraphs);
            syncBeams(snapshot.Beams);
        }

        /// <summary>
        /// Mirrors the room's beams. Unlike a mob or a shot, a beam is a fixture: it is reported in every
        /// snapshot, so absence means the room no longer has it rather than that it was destroyed.
        /// </summary>
        private void syncBeams(DodgeWorldBeam[] reported)
        {
            foreach (DodgeWorldBeam beam in reported)
            {
                if (!beams.TryGetValue(beam.Id, out WorldBeam? drawable))
                {
                    beams[beam.Id] = drawable = new WorldBeam(colours) { Alpha = 0, Depth = -6 };
                    worldCamera.Add(drawable);
                }

                drawable.SyncFrom(new Vector2(beam.X, beam.Y), beam.AngleDegrees, beam.Length, beam.Width,
                    beam.Active, beam.Warning);
            }

            foreach (int id in beams.Keys.Except(reported.Select(beam => beam.Id)).ToArray())
            {
                worldCamera.Remove(beams[id], true);
                beams.Remove(id);
            }
        }

        private void syncMobs(DodgeWorldMob[] reported)
        {
            foreach (DodgeWorldMob mob in reported)
            {
                if (!mobs.TryGetValue(mob.Id, out RemoteMob? existing))
                {
                    var drawable = new WorldMob(colours)
                    {
                        Position = new Vector2(mob.X, mob.Y),
                        Alpha = dimmed ? 0.3f : 1,
                    };

                    worldCamera.Add(drawable);
                    mobs[mob.Id] = existing = new RemoteMob(drawable, new Vector2(mob.X, mob.Y), mob.Health);
                }

                // Every snapshot, not only on the first: the zone's image may still have been loading when
                // the mob was reported.
                existing.Drawable.SetAppearance(mobAppearance(mob.ZoneId));
                existing.Target = new Vector2(mob.X, mob.Y);

                if (mob.Health < existing.Health)
                    existing.Drawable.ShowDamage(mob.MaximumHealth > 0 ? (float)mob.Health / mob.MaximumHealth : 0);

                existing.Health = mob.Health;
            }

            // Absent from the snapshot means gone. Whether it was killed or its room was unloaded is not
            // something the drawable needs to know.
            foreach (int id in mobs.Keys.Except(reported.Select(mob => mob.Id)).ToArray())
                retire(id);
        }

        private void retire(int mobId)
        {
            if (!mobs.Remove(mobId, out RemoteMob? removed))
                return;

            removed.Drawable.ShowDamage(0);
            removed.Drawable.FadeOut(150, Easing.OutQuint).Expire();
        }

        private void syncProjectiles(DodgeWorldProjectile[] reported)
        {
            foreach (DodgeWorldProjectile projectile in reported)
            {
                if (!projectiles.TryGetValue(projectile.Id, out RemoteProjectile? existing))
                {
                    var drawable = new WorldProjectile(colours) { Depth = -projectile.Y - 5 };
                    worldCamera.Add(drawable);
                    projectiles[projectile.Id] = existing = new RemoteProjectile(drawable);
                }

                existing.Drawable.Position = new Vector2(projectile.X, projectile.Y);
                existing.Velocity = new Vector2(projectile.VelocityX, projectile.VelocityY);
            }

            foreach (int id in projectiles.Keys.Except(reported.Select(projectile => projectile.Id)).ToArray())
            {
                // Faded rather than removed, like a locally simulated shot: the drawable is dropped from
                // the room by the framework once its fade is done.
                projectiles[id].Drawable.Retire();
                projectiles.Remove(id);
            }
        }

        private void syncTelegraphs(DodgeWorldTelegraph[] reported)
        {
            foreach (DodgeWorldTelegraph telegraph in reported)
            {
                if (telegraphs.ContainsKey(telegraph.Id))
                    continue;

                Vector2[] directions = telegraph.Angles
                                                .Select(angle => new Vector2(MathF.Cos(angle), MathF.Sin(angle)))
                                                .ToArray();

                var drawable = new MobAttackTelegraph(colours, directions, telegraph.Range)
                {
                    Position = new Vector2(telegraph.X, telegraph.Y),
                    Depth = -telegraph.Y - 4,
                };

                telegraphs[telegraph.Id] = drawable;
                worldCamera.Add(drawable);
                drawable.Play();
            }

            foreach (int id in telegraphs.Keys.Except(reported.Select(telegraph => telegraph.Id)).ToArray())
            {
                worldCamera.Remove(telegraphs[id], true);
                telegraphs.Remove(id);
            }
        }

        protected override void Update()
        {
            base.Update();

            foreach (RemoteMob mob in mobs.Values)
            {
                Vector2 position = mob.Drawable.Position;
                position += (mob.Target - position) * (1 - (float)Math.Exp(-Time.Elapsed / mob_smoothing));

                mob.Drawable.Position = position;

                if (mob.Drawable.Parent != null && mob.Drawable.Depth != -position.Y)
                    worldCamera.ChangeChildDepth(mob.Drawable, -position.Y);
            }

            // Shots are carried on from their last reported position: at twenty snapshots a second a fast
            // projectile would otherwise appear to jump from one place to the next.
            float elapsedSeconds = (float)(Time.Elapsed / 1000);

            foreach (RemoteProjectile projectile in projectiles.Values)
                projectile.Drawable.Position += projectile.Velocity * elapsedSeconds;
        }

        /// <summary>
        /// Lets go of the drawn mobs without touching the world container, unlike <see cref="Clear"/>.
        /// </summary>
        /// <remarks>
        /// Disposal does not run on the update thread, and removing a child from a loaded container off it
        /// throws. Nothing is leaked: this view and the container it draws into are both owned by the
        /// screen, so they are disposed together.
        /// </remarks>
        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            client.RoomStateReceived -= onRoomState;
            client.MobDefeated -= onMobDefeated;

            mobs.Clear();
            projectiles.Clear();
            telegraphs.Clear();
            beams.Clear();
        }

        private sealed class RemoteMob
        {
            public readonly WorldMob Drawable;

            public Vector2 Target;
            public int Health;

            public RemoteMob(WorldMob drawable, Vector2 target, int health)
            {
                Drawable = drawable;
                Target = target;
                Health = health;
            }
        }

        private sealed class RemoteProjectile
        {
            public readonly WorldProjectile Drawable;

            public Vector2 Velocity;

            public RemoteProjectile(WorldProjectile drawable)
            {
                Drawable = drawable;
            }
        }

        internal int MobCountForTesting => mobs.Count;
        internal int ProjectileCountForTesting => projectiles.Count;
        internal int TelegraphCountForTesting => telegraphs.Count;
    }
}
