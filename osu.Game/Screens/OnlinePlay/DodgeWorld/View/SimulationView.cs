// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// Keeps the drawables in the world container matching the simulation.
    /// </summary>
    /// <remarks>
    /// Nothing here decides anything: it subscribes to what the simulation reports and mirrors the
    /// resulting state each frame. Combat used to live inside these drawables, which is why it could
    /// not be tested without rendering.
    /// </remarks>
    /// <remarks>
    /// A drawable so that osu!framework drives its update: a composite runs its own <c>Update</c>
    /// before its children, so the screen has already stepped the simulation by the time this mirrors
    /// it. It draws nothing itself — the entities it manages live in the world container.
    /// </remarks>
    internal sealed partial class SimulationView : CompositeDrawable
    {
        private readonly Container worldCamera;
        private readonly OsuColour colours;
        private readonly WorldSimulation simulation;
        private readonly WorldPlayer player;

        /// <summary>
        /// The simulated player <see cref="player"/> draws. Passed in rather than looked up, because a
        /// simulation can now hold several players and only one of them is the local one.
        /// </summary>
        private readonly PlayerState playerState;
        private readonly Func<Texture?> weaponTexture;
        private readonly Func<WeaponSkin> weaponSkin;
        private readonly Func<string, MobAppearance> mobAppearance;

        private readonly Dictionary<int, WorldMob> mobs = new Dictionary<int, WorldMob>();
        private readonly Dictionary<int, WorldProjectile> projectiles = new Dictionary<int, WorldProjectile>();
        private readonly Dictionary<int, MobAttackTelegraph> telegraphs = new Dictionary<int, MobAttackTelegraph>();
        private readonly Dictionary<int, WorldBeam> beams = new Dictionary<int, WorldBeam>();

        /// <summary>
        /// Whether mobs should be dimmed, as they are while the editor is open.
        /// </summary>
        public bool Dimmed { get; private set; }

        public SimulationView(Container worldCamera, WorldPlayer player, PlayerState playerState, OsuColour colours,
                              WorldSimulation simulation, Func<Texture?> weaponTexture, Func<WeaponSkin> weaponSkin,
                              Func<string, MobAppearance> mobAppearance)
        {
            this.worldCamera = worldCamera;
            this.player = player;
            this.playerState = playerState;
            this.colours = colours;
            this.simulation = simulation;
            this.weaponTexture = weaponTexture;
            this.weaponSkin = weaponSkin;
            this.mobAppearance = mobAppearance;

            simulation.MobSpawned += onMobSpawned;
            simulation.MobDefeated += onMobDefeated;
            simulation.MobDamaged += onMobDamaged;
            simulation.MobRemoved += onMobRemoved;
            simulation.ProjectileSpawned += onProjectileSpawned;
            simulation.ProjectileExpired += onProjectileExpired;
            simulation.TelegraphStarted += onTelegraphStarted;
            simulation.TelegraphEnded += onTelegraphEnded;
            simulation.PlayerSwung += onPlayerSwung;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            Detach();
        }

        public void Detach()
        {
            simulation.MobSpawned -= onMobSpawned;
            simulation.MobDefeated -= onMobDefeated;
            simulation.MobDamaged -= onMobDamaged;
            simulation.MobRemoved -= onMobRemoved;
            simulation.ProjectileSpawned -= onProjectileSpawned;
            simulation.ProjectileExpired -= onProjectileExpired;
            simulation.TelegraphStarted -= onTelegraphStarted;
            simulation.TelegraphEnded -= onTelegraphEnded;
            simulation.PlayerSwung -= onPlayerSwung;
        }

        /// <summary>
        /// Copies simulated positions onto the drawables and keeps the draw order back-to-front.
        /// </summary>
        protected override void Update()
        {
            base.Update();

            foreach (MobState mob in simulation.Mobs)
            {
                if (!mobs.TryGetValue(mob.Id, out WorldMob? drawable))
                    continue;

                drawable.SyncFrom(mob);
                drawable.SetAppearance(mobAppearance(mob.SpawnZoneId));
                setDepth(drawable, -mob.Position.Y);
            }

            foreach (ProjectileState projectile in simulation.Projectiles)
            {
                if (projectiles.TryGetValue(projectile.Id, out WorldProjectile? drawable))
                    drawable.SyncFrom(projectile);
            }

            syncBeams();

            player.SyncFrom(playerState);
            setDepth(player, -playerState.Position.Y);
        }

        /// <summary>
        /// Mirrors the room's beams. Kept in step by id rather than by events, because a beam is not born
        /// and does not die: it is a fixture of the room that turns on and off.
        /// </summary>
        private void syncBeams()
        {
            foreach (HazardState hazard in simulation.Hazards)
            {
                if (hazard.Kind != HazardKind.Beam)
                    continue;

                if (!beams.TryGetValue(hazard.Id, out WorldBeam? drawable))
                {
                    beams[hazard.Id] = drawable = new WorldBeam(colours) { Alpha = 0, Depth = -6 };
                    worldCamera.Add(drawable);
                }

                drawable.SyncFrom(hazard.Origin, hazard.Angle * 180 / MathF.PI, hazard.Definition.Range,
                    hazard.Definition.Width, hazard.Active, hazard.Warning);
            }

            if (beams.Count == 0)
                return;

            foreach (int id in beams.Keys.ToArray())
            {
                if (simulation.Hazards.Any(hazard => hazard.Id == id))
                    continue;

                worldCamera.Remove(beams[id], true);
                beams.Remove(id);
            }
        }

        public void SetDimmed(bool dimmed)
        {
            Dimmed = dimmed;

            foreach (WorldMob mob in mobs.Values)
                mob.FadeTo(dimmed ? 0.3f : 1, 160, Easing.OutQuint);
        }

        private void setDepth(Drawable drawable, float depth)
        {
            if (drawable.Depth != depth && drawable.Parent != null)
                worldCamera.ChangeChildDepth(drawable, depth);
        }

        private void onMobSpawned(MobState mob)
        {
            var drawable = new WorldMob(colours) { Alpha = Dimmed ? 0.3f : 1 };

            drawable.SetAppearance(mobAppearance(mob.SpawnZoneId));
            drawable.SyncFrom(mob);
            mobs[mob.Id] = drawable;
            worldCamera.Add(drawable);
        }

        private void onMobDamaged(MobState mob)
        {
            if (mobs.TryGetValue(mob.Id, out WorldMob? drawable))
                drawable.ShowDamage((float)mob.Health / mob.MaximumHealth);
        }

        private void onMobRemoved(MobState mob)
        {
            if (mobs.Remove(mob.Id, out WorldMob? drawable))
                worldCamera.Remove(drawable, true);
        }

        private void onMobDefeated(MobState mob, PlayerState killer)
        {
            if (!mobs.Remove(mob.Id, out WorldMob? drawable))
                return;

            drawable.ShowDamage(0);
            drawable.FadeOut(150, Easing.OutQuint).Expire();
        }

        private void onProjectileSpawned(ProjectileState projectile)
        {
            var drawable = new WorldProjectile(colours) { Depth = -projectile.Position.Y - 5 };

            drawable.SyncFrom(projectile);
            projectiles[projectile.Id] = drawable;
            worldCamera.Add(drawable);
        }

        private void onProjectileExpired(ProjectileState projectile)
        {
            if (projectiles.Remove(projectile.Id, out WorldProjectile? drawable))
                drawable.Retire();
        }

        private void onTelegraphStarted(TelegraphState telegraph)
        {
            var drawable = new MobAttackTelegraph(colours, telegraph.Directions, telegraph.Range)
            {
                Position = telegraph.Origin,
                Depth = -telegraph.Origin.Y - 4,
            };

            telegraphs[telegraph.Id] = drawable;
            worldCamera.Add(drawable);
            drawable.Play();
        }

        private void onTelegraphEnded(TelegraphState telegraph)
        {
            if (telegraphs.Remove(telegraph.Id, out MobAttackTelegraph? drawable))
                drawable.Expire();
        }

        private void onPlayerSwung(PlayerState swinger, SwingRequest swing)
        {
            var slash = new SwordSwing(weaponTexture(), weaponSkin(), player.AccentColour, swing.AngleDegrees, swing.Clockwise)
            {
                Position = swing.Origin,
                Depth = -swing.Origin.Y - 5,
            };

            LastSwing = slash;

            // Loaded asynchronously because the weapon texture may still be resolving; the swing is
            // decorative, so a frame of delay costs nothing.
            LoadComponentAsync(slash, loaded =>
            {
                if (!worldCamera.IsAlive)
                    return;

                worldCamera.Add(loaded);
                loaded.Play();
            });
        }

        /// <summary>
        /// The most recent swing, for tests to assert against.
        /// </summary>
        internal SwordSwing? LastSwing { get; private set; }
    }
}
