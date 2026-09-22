// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.DodgeWorld;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// Draws the other players in the room.
    /// </summary>
    /// <remarks>
    /// The mirror image of <see cref="SimulationView"/> for players who are not simulated here: their
    /// positions arrive over the network at about twenty a second, so each one is eased towards its
    /// last reported position instead of being snapped to it, which is what makes a remote player look
    /// like they are walking rather than teleporting.
    /// </remarks>
    internal sealed partial class RemotePlayerView : CompositeDrawable
    {
        /// <summary>
        /// How quickly a drawn player closes the gap to where they were last reported, as a time
        /// constant in milliseconds. Small enough to stay responsive, large enough to smooth over the
        /// gap between updates.
        /// </summary>
        private const double smoothing = 60;

        /// <summary>
        /// A jump further than this is treated as a warp or a room change rather than as walking, and
        /// is not eased.
        /// </summary>
        private const float teleport_distance = 400;

        private readonly Container worldCamera;
        private readonly OsuColour colours;
        private readonly DodgeWorldClient client;
        private readonly IBindableDictionary<int, DodgeWorldPlayerState> players;
        private readonly Func<Texture?> weaponTexture;
        private readonly Func<WeaponSkin> weaponSkin;

        private readonly Dictionary<int, RemotePlayer> drawables = new Dictionary<int, RemotePlayer>();

        [Resolved]
        private UserLookupCache users { get; set; } = null!;

        public RemotePlayerView(Container worldCamera, OsuColour colours, DodgeWorldClient client,
                                Func<Texture?> weaponTexture, Func<WeaponSkin> weaponSkin)
        {
            this.worldCamera = worldCamera;
            this.colours = colours;
            this.client = client;
            this.weaponTexture = weaponTexture;
            this.weaponSkin = weaponSkin;
            players = client.Players.GetBoundCopy();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            players.BindCollectionChanged((_, _) => synchronise(), true);
            client.UserAttacked += onUserAttacked;
        }

        /// <summary>
        /// Draws somebody else's swing.
        /// </summary>
        /// <remarks>
        /// The swinger's own client drew this the moment they clicked, without waiting for the server, so
        /// this is the same animation arriving a round trip later for everyone else. It is drawn from where
        /// the player is currently <em>shown</em> rather than from their last reported position, so the
        /// sword leaves the hand it appears to be in.
        /// </remarks>
        private void onUserAttacked(int userId, Vector2 direction)
        {
            if (!drawables.TryGetValue(userId, out RemotePlayer? player) || direction.LengthSquared <= 0)
                return;

            // Which way the blade travels is decoration, and the wire message does not carry it. Alternating
            // per player locally gives the same left-right-left rhythm the swinger sees.
            bool clockwise = player.NextSwingClockwise;
            player.NextSwingClockwise = !player.NextSwingClockwise;

            Vector2 origin = player.Drawable.Position + CombatRules.WEAPON_ORIGIN_OFFSET;

            var slash = new SwordSwing(weaponTexture(), weaponSkin(), player.Drawable.AccentColour,
                MathF.Atan2(direction.Y, direction.X) * 180 / MathF.PI, clockwise)
            {
                Position = origin,
                Depth = -origin.Y - 5,
            };

            LoadComponentAsync(slash, loaded =>
            {
                if (!worldCamera.IsAlive)
                    return;

                worldCamera.Add(loaded);
                loaded.Play();
            });
        }

        /// <summary>
        /// Adds and removes drawables so that there is exactly one per player in the room.
        /// </summary>
        private void synchronise()
        {
            foreach ((int userId, DodgeWorldPlayerState state) in players)
            {
                if (!drawables.TryGetValue(userId, out RemotePlayer? player))
                {
                    drawables[userId] = player = new RemotePlayer(colours, state.Position);
                    worldCamera.Add(player.Drawable);
                    lookUpUser(userId, player);
                }

                player.Target = state;
            }

            foreach (int userId in drawables.Keys.ToArray())
            {
                if (players.ContainsKey(userId))
                    continue;

                worldCamera.Remove(drawables[userId].Drawable, true);
                drawables.Remove(userId);
            }
        }

        /// <summary>
        /// Puts a name and colour on a player. Asynchronous, so the square appears immediately and is
        /// labelled a moment later rather than not appearing until the lookup returns.
        /// </summary>
        private void lookUpUser(int userId, RemotePlayer player)
        {
            users.GetUserAsync(userId, CancellationToken.None).ContinueWith(task => Schedule(() =>
            {
                if (!task.IsCompletedSuccessfully || task.GetResultSafely() is not APIUser user)
                    return;

                // The player may have left the room while the lookup was in flight.
                if (drawables.TryGetValue(userId, out RemotePlayer? current) && current == player)
                    player.Drawable.SetUser(user, colours);
            }));
        }

        protected override void Update()
        {
            base.Update();

            foreach (RemotePlayer player in drawables.Values)
                player.Advance(Time.Elapsed, worldCamera);
        }

        /// <summary>
        /// Lets go of the drawn players without touching the world container.
        /// </summary>
        /// <remarks>
        /// Disposal does not run on the update thread, and removing a child from a loaded container off it
        /// throws. Nothing is leaked by leaving them: this view and the container it draws into are both
        /// owned by the screen, so they are disposed together, and a room change empties the container
        /// while the game is still running.
        /// </remarks>
        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            client.UserAttacked -= onUserAttacked;
            drawables.Clear();
        }

        /// <summary>
        /// One player on another client: the drawable, where they were last reported to be, and where
        /// they are currently drawn on the way there.
        /// </summary>
        private sealed class RemotePlayer
        {
            public readonly WorldPlayer Drawable;

            public DodgeWorldPlayerState Target = new DodgeWorldPlayerState();

            /// <summary>Which way this player's next drawn swing travels. Decoration only.</summary>
            public bool NextSwingClockwise = true;

            private Vector2 shown;

            public RemotePlayer(OsuColour colours, Vector2 position)
            {
                Drawable = new WorldPlayer(colours) { Position = position };
                shown = position;
                Target.Position = position;
            }

            public void Advance(double elapsed, Container worldCamera)
            {
                Vector2 destination = Target.Position;
                float distance = (destination - shown).Length;

                if (distance > teleport_distance || elapsed <= 0)
                    shown = destination;
                else
                    shown += (destination - shown) * (1 - (float)Math.Exp(-elapsed / smoothing));

                float step = (shown - Drawable.Position).Length;

                Drawable.SyncFrom(shown, Target.Facing, Target.Moving, step, Target.Health);

                if (Drawable.Parent != null && Drawable.Depth != -shown.Y)
                    worldCamera.ChangeChildDepth(Drawable, -shown.Y);
            }
        }
    }
}
