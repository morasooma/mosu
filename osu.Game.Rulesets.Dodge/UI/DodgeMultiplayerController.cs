// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Dodge.Skinning;
using osu.Game.Rulesets.Dodge.Skinning.Components;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.UI
{
    /// <summary>
    /// Displays live remote players for a normal Dodge multiplayer match.
    /// </summary>
    public partial class DodgeMultiplayerController : DodgeMultiplayerGameplayController
    {
        private readonly DrawableDodgeRuleset drawableRuleset;
        private readonly Dictionary<int, DodgeMultiplayerPlayer> remotePlayers = new Dictionary<int, DodgeMultiplayerPlayer>();
        private readonly DodgeMultiplayerPlayer localPlayerLabel;

        public override Vector2 LocalPlayerPosition
        {
            get
            {
                Vector2 position = drawableRuleset.Playfield.Player.Position;
                var normalised = new Vector2(position.X / DodgePlayfield.WIDTH, position.Y / DodgePlayfield.HEIGHT);
                return Vector2.ComponentMin(Vector2.One, Vector2.ComponentMax(Vector2.Zero, normalised));
            }
        }

        public IReadOnlyDictionary<int, DodgeMultiplayerPlayer> RemotePlayers => remotePlayers;

        public DodgeMultiplayerController(DrawableDodgeRuleset drawableRuleset, DodgeMultiplayerGameplayConfiguration configuration)
        {
            this.drawableRuleset = drawableRuleset;

            RelativeSizeAxes = Axes.Both;
            Depth = float.MinValue;

            foreach ((int userId, string username) in configuration.Usernames)
            {
                Color4 colour = configuration.PlayerColours.TryGetValue(userId, out Color4 playerColour)
                    ? playerColour
                    : Color4.White;

                if (userId == configuration.LocalUserId)
                    continue;

                var remotePlayer = new DodgeMultiplayerPlayer(username, colour, drawableRuleset, configuration.IsBreakTime);
                remotePlayers[userId] = remotePlayer;
                AddInternal(remotePlayer);
            }

            localPlayerLabel = new DodgeMultiplayerPlayer(
                configuration.Usernames.TryGetValue(configuration.LocalUserId, out string? localUsername) ? localUsername : $"Player {configuration.LocalUserId}",
                configuration.PlayerColours.TryGetValue(configuration.LocalUserId, out Color4 localColour) ? localColour : Color4.White,
                drawableRuleset,
                configuration.IsBreakTime,
                showBody: false);
            AddInternal(localPlayerLabel);
            localPlayerLabel.SetLocalState(LocalPlayerPosition, 0);
        }

        public override void SetLocalPing(ushort pingMilliseconds)
            => localPlayerLabel.SetLocalState(LocalPlayerPosition, pingMilliseconds);

        public override void PushRemotePlayerState(int userId, uint sequence, Vector2 normalisedPosition, ushort pingMilliseconds)
        {
            if (remotePlayers.TryGetValue(userId, out DodgeMultiplayerPlayer? player))
                player.Push(sequence, normalisedPosition, pingMilliseconds);
        }

        protected override void Update()
        {
            base.Update();

            // Keep the local break-time label attached to the authoritative local player.
            localPlayerLabel.SetLocalPosition(LocalPlayerPosition);
        }
    }

    public partial class DodgeMultiplayerPlayer : CompositeDrawable
    {
        public const int LOW_LATENCY_THRESHOLD = 200;
        public const float GAMEPLAY_ALPHA = 0.52f;
        public const float HIGH_LATENCY_GAMEPLAY_ALPHA = 0.24f;
        public const float BREAK_ALPHA = 0.78f;

        private readonly string username;
        private readonly DrawableDodgeRuleset drawableRuleset;
        private readonly IBindable<bool> isBreakTime;
        private readonly bool showBody;
        private readonly Container body;
        private readonly OsuSpriteText label;

        private Vector2 targetPosition;
        private Vector2 displayedPosition;
        private bool hasPosition;
        private long lastSampleTimestamp;
        private ushort ping;

        public uint LastSequence { get; private set; }

        public ushort PingMilliseconds => ping;

        public bool IsLowLatency => ping < LOW_LATENCY_THRESHOLD;

        public float BodyAlpha => body.Alpha;

        public float LabelAlpha => label.Alpha;

        public DodgeMultiplayerPlayer(
            string username,
            Color4 colour,
            DrawableDodgeRuleset drawableRuleset,
            IBindable<bool> isBreakTime,
            bool showBody = true)
        {
            this.username = username;
            this.drawableRuleset = drawableRuleset;
            this.isBreakTime = isBreakTime;
            this.showBody = showBody;

            Origin = Anchor.Centre;
            AlwaysPresent = true;
            Alpha = 0;

            InternalChildren = new Drawable[]
            {
                body = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colour,
                    Masking = true,
                    BorderThickness = 2,
                    BorderColour = Color4.White,
                    Children = new Drawable[]
                    {
                        new SkinnableDrawable(
                            new DodgeSkinComponentLookup(DodgeSkinComponents.Player),
                            _ => new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = Color4.White,
                            },
                            ConfineMode.ScaleToFit)
                        {
                            RelativeSizeAxes = Axes.Both,
                        },
                    },
                },
                label = new OsuSpriteText
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreLeft,
                    X = 8,
                    Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                    Colour = colour,
                    Shadow = true,
                    Alpha = 0,
                },
            };
        }

        public void Push(uint sequence, Vector2 normalisedPosition, ushort pingMilliseconds)
        {
            if (hasPosition && sequence <= LastSequence)
                return;

            LastSequence = sequence;
            setState(normalisedPosition, pingMilliseconds, true);
        }

        public void SetLocalState(Vector2 normalisedPosition, ushort pingMilliseconds)
            => setState(normalisedPosition, pingMilliseconds, false);

        public void SetLocalPosition(Vector2 normalisedPosition)
        {
            targetPosition = clampPosition(normalisedPosition);
            displayedPosition = targetPosition;
            hasPosition = true;
            lastSampleTimestamp = Stopwatch.GetTimestamp();
        }

        private void setState(Vector2 normalisedPosition, ushort pingMilliseconds, bool interpolate)
        {
            targetPosition = clampPosition(normalisedPosition);
            ping = pingMilliseconds;
            lastSampleTimestamp = Stopwatch.GetTimestamp();

            if (!hasPosition || !interpolate)
                displayedPosition = targetPosition;

            hasPosition = true;
            label.Text = $"{username}  {ping} ms";
        }

        protected override void Update()
        {
            base.Update();

            if (!hasPosition)
                return;

            float interpolation = (float)(1 - Math.Exp(-Time.Elapsed / 1000 * 22));
            displayedPosition = Vector2.Lerp(displayedPosition, targetPosition, interpolation);

            Vector2 gamefieldPosition = displayedPosition * DodgePlayfield.BASE_SIZE;
            Vector2 screenPosition = drawableRuleset.Playfield.GamefieldToScreenSpace(gamefieldPosition);

            if (Parent != null)
                Position = Parent.ToLocalSpace(screenPosition);

            Vector2 screenUnit = drawableRuleset.Playfield.GamefieldToScreenSpace(gamefieldPosition + Vector2.UnitX) - screenPosition;
            float scale = Math.Max(0.01f, screenUnit.Length);
            Size = new Vector2(drawableRuleset.Playfield.Player.PlayerSize * scale);

            bool inBreak = isBreakTime.Value;
            bool fresh = Stopwatch.GetElapsedTime(lastSampleTimestamp).TotalSeconds < (inBreak ? 3 : 1);

            Alpha = fresh ? 1 : 0;
            body.Alpha = showBody ? inBreak ? BREAK_ALPHA : IsLowLatency ? GAMEPLAY_ALPHA : HIGH_LATENCY_GAMEPLAY_ALPHA : 0;
            label.Alpha = inBreak && Alpha > 0 ? 1 : 0;
        }

        private static Vector2 clampPosition(Vector2 position)
            => Vector2.ComponentMin(Vector2.One, Vector2.ComponentMax(Vector2.Zero, position));
    }
}
