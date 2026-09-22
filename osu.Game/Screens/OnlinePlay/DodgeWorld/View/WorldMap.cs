// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// The map of the world: one node per room, one line per way between them.
    /// </summary>
    /// <remarks>
    /// A map of rooms rather than a map of one room. Rooms are already visible in full while standing
    /// in them, so what is worth drawing is the shape of the world — which is exactly what the document
    /// does not otherwise show, since a room only knows the ids of the rooms it leads to.
    /// <para>
    /// Walking is not offered here, and that has not changed: a map that teleported from anywhere would
    /// make warps pointless. Opened from a warp, though, the same map becomes the way to choose where that
    /// warp sends you — the destinations were already paid for, and seeing them in the shape of the world
    /// beats reading them as a list.
    /// </para>
    /// </remarks>
    internal partial class WorldMap : CompositeDrawable
    {
        private static readonly Vector2 node_size = new Vector2(176, 62);

        /// <summary>Room around the outermost nodes, so a node is never flush against the edge.</summary>
        private const float board_padding = 120;

        private readonly OsuColour colours;
        private readonly Container panel;
        private readonly Container board;
        private readonly Container<Box> linkLayer;
        private readonly Container<RoomNode> nodeLayer;
        private readonly OsuSpriteText title;
        private readonly OsuSpriteText hint;
        private readonly RoundedButton closeButton;

        private Vector2 boardOrigin;

        /// <summary>Warps that can be travelled to, when the map was opened from one.</summary>
        private IReadOnlyList<WarpPoint> reachable = Array.Empty<WarpPoint>();

        private long? coins;

        /// <summary>Asked to open a room, in the editor only.</summary>
        public Action<string>? OpenRoom;

        /// <summary>Told that a room was dragged to a new place on the map, in map units.</summary>
        public Action<string, Vector2>? PlaceRoom;

        /// <summary>Asked to travel to a warp. The screen decides whether it can be paid for.</summary>
        public Action<WarpPoint>? Travel;

        /// <summary>
        /// Asked which warp of a room, when that room holds more than one that can be travelled to.
        /// </summary>
        public Action<string>? ChooseWarpInRoom;

        /// <summary>Asked to point the passage being linked at a room, in the editor only.</summary>
        public Action<string>? LinkRoom;

        /// <summary>
        /// Asked to make the chosen room's passages leave the player at the spot being looked at.
        /// </summary>
        public Action<string>? ArriveFromRoom;

        /// <summary>What question the map is open to answer.</summary>
        private enum Purpose
        {
            /// <summary>The shape of the world, and in the editor a way to open a room.</summary>
            Map,

            /// <summary>Where a warp should send the player.</summary>
            Travel,

            /// <summary>Which room a passage leads to.</summary>
            Link,

            /// <summary>Which room the player will be arriving here from.</summary>
            Arrival,
        }

        private Purpose purpose = Purpose.Map;

        /// <summary>Whether the map is open as a warp destination picker rather than as a map.</summary>
        public bool IsTravelling => purpose == Purpose.Travel;

        /// <summary>Whether the map is open to point a passage at a room.</summary>
        public bool IsLinking => purpose == Purpose.Link;

        /// <summary>Whether the map is open to choose which room the player arrives here from.</summary>
        public bool IsChoosingArrival => purpose == Purpose.Arrival;

        public WorldMap(OsuColour colours)
        {
            this.colours = colours;

            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black.Opacity(0.72f) },
                panel = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Size = new Vector2(0.86f, 0.82f),
                    Masking = true,
                    CornerRadius = 16,
                    BorderThickness = 2,
                    BorderColour = colours.Pink1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1.Opacity(0.98f) },
                        title = new OsuSpriteText
                        {
                            Margin = new MarginPadding { Left = 24, Top = 18 },
                            Text = "КАРТА МИРА",
                            Font = OsuFont.Default.With(size: 22, weight: FontWeight.Bold),
                            Colour = colours.Pink1,
                        },
                        hint = new OsuSpriteText
                        {
                            Margin = new MarginPadding { Left = 24, Top = 48 },
                            Font = OsuFont.Default.With(size: 14),
                            Colour = colours.GrayA,
                        },
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 78, Bottom = 66, Horizontal = 16 },
                            Masking = true,
                            Child = board = new Container
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Children = new Drawable[]
                                {
                                    linkLayer = new Container<Box> { RelativeSizeAxes = Axes.Both },
                                    nodeLayer = new Container<RoomNode> { RelativeSizeAxes = Axes.Both },
                                },
                            },
                        },
                        closeButton = new RoundedButton
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Margin = new MarginPadding { Bottom = 16 },
                            Width = 240,
                            Height = 34,
                            Text = "Закрыть (M)",
                            Action = Close,
                        },
                    },
                },
            };
        }

        public bool IsOpen { get; private set; }

        public void Open(WorldGraph graph, string currentRoomId, bool editing)
        {
            purpose = Purpose.Map;
            reachable = Array.Empty<WarpPoint>();
            coins = null;

            title.Text = "КАРТА МИРА";
            closeButton.Text = "Закрыть (M)";
            hint.Text = editing
                ? "Перетащи комнату, чтобы задать её место на карте. Клик — открыть комнату. Не забудь сохранить мир."
                : "Комнаты и переходы между ними. Перемещаться между комнатами можно варпами и проходами.";

            show(graph, currentRoomId, editing);
        }

        /// <summary>
        /// Opens the map as the destination picker of a warp being stood on.
        /// </summary>
        /// <param name="graph">The world graph.</param>
        /// <param name="currentRoomId">The current room identifier.</param>
        /// <param name="reachableWarps">
        /// The warps this player has opened, minus the one they are standing on. Rooms holding none of
        /// them are drawn as context and cannot be travelled to.
        /// </param>
        /// <param name="balance">The player's coins, or null when unknown — then nothing is refused here.</param>
        public void OpenForTravel(WorldGraph graph, string currentRoomId, IReadOnlyList<WarpPoint> reachableWarps,
                                  long? balance)
        {
            purpose = Purpose.Travel;
            reachable = reachableWarps;
            coins = balance;

            title.Text = "СЕТЬ ВАРПОВ";
            closeButton.Text = "Закрыть (E)";
            hint.Text = reachableWarps.Count == 0
                ? "Открытых варпов пока нет. Найди их в мире и открой на месте."
                : balance == null
                    ? "Выбери комнату с варпом. Баланс недоступен."
                    : $"Выбери комнату с варпом. Монет: {balance.Value.ToString("N0", CultureInfo.CurrentCulture)}";

            show(graph, currentRoomId, editing: false);
        }

        /// <summary>
        /// Opens the map to answer where a passage leads.
        /// </summary>
        /// <remarks>
        /// The same map, because the question is the same shape as travelling: which room. Typing an id
        /// into a field asks the author to remember what the map already shows them.
        /// </remarks>
        public void OpenForLinking(WorldGraph graph, string currentRoomId, string passageName)
        {
            purpose = Purpose.Link;
            reachable = Array.Empty<WarpPoint>();
            coins = null;

            title.Text = "КУДА ВЕДЁТ ПРОХОД";
            closeButton.Text = "Отмена (M)";
            hint.Text = $"Выбери комнату, в которую поведёт «{passageName}». Комната, в которой ты стоишь, тоже подходит.";

            show(graph, currentRoomId, editing: false);
        }

        /// <summary>
        /// Opens the map to ask which room the player will be arriving from, so that the spot they arrive
        /// at can be the one the author is looking at.
        /// </summary>
        /// <remarks>
        /// Asked from this end rather than the other because this is the end that can be pointed at: the
        /// author stands where they want the player to come out, and names where they will be coming from.
        /// </remarks>
        public void OpenForArrival(WorldGraph graph, string currentRoomId, string roomName)
        {
            purpose = Purpose.Arrival;
            reachable = Array.Empty<WarpPoint>();
            coins = null;

            title.Text = "ОТКУДА СЮДА ПРИХОДЯТ";
            closeButton.Text = "Отмена (M)";
            hint.Text = $"Выбери комнату: все её проходы, ведущие в «{roomName}», будут выводить игрока в центр экрана.";

            show(graph, currentRoomId, editing: false);
        }

        private void show(WorldGraph graph, string currentRoomId, bool editing)
        {
            build(graph, currentRoomId, editing);

            IsOpen = true;
            this.FadeIn(150, Easing.OutQuint);
            panel.ScaleTo(0.97f).ScaleTo(1, 220, Easing.OutQuint);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            purpose = Purpose.Map;
            this.FadeOut(130, Easing.OutQuint);
        }

        private void build(WorldGraph graph, string currentRoomId, bool editing)
        {
            linkLayer.Clear();
            nodeLayer.Clear();

            if (graph.Nodes.Count == 0)
                return;

            Vector2 minimum = new Vector2(graph.Nodes.Min(node => node.Position.X), graph.Nodes.Min(node => node.Position.Y));
            Vector2 maximum = new Vector2(graph.Nodes.Max(node => node.Position.X), graph.Nodes.Max(node => node.Position.Y));

            boardOrigin = minimum - new Vector2(board_padding);
            board.Size = maximum - minimum + new Vector2(board_padding * 2);

            var positions = new Dictionary<string, Vector2>();

            foreach (WorldGraphNode node in graph.Nodes)
            {
                Vector2 position = node.Position - boardOrigin;
                positions[node.RoomId] = position;

                bool current = node.RoomId == currentRoomId;
                TravelOption? travel = describeTravel(node.RoomId, current);

                var drawable = new RoomNode(colours, node, current, editing, travel)
                {
                    Position = position,
                    Draggable = editing,
                };

                string roomId = node.RoomId;

                // A room that cannot be travelled to gets no action at all: assigning one is what enables
                // a clickable container, so this is also what leaves it visibly inert.
                if (travel is null or { Count: > 0, Affordable: true })
                    drawable.Action = () => choose(roomId);
                drawable.Moved = moved =>
                {
                    redrawLinks(graph, positions);
                    PlaceRoom?.Invoke(roomId, moved + boardOrigin);
                };

                nodeLayer.Add(drawable);
            }

            redrawLinks(graph, positions);
            fitBoard();
        }

        /// <summary>
        /// What a room offers a traveller, or null when the map is not being used to travel.
        /// </summary>
        /// <remarks>
        /// The room being stood in is never a destination: the player is already there, and a second warp
        /// in the same room is not somewhere to go.
        /// </remarks>
        private TravelOption? describeTravel(string roomId, bool current)
        {
            if (!IsTravelling)
                return null;

            if (current)
                return new TravelOption(0, 0, false);

            int[] costs = reachable.Where(warp => warp.RoomId == roomId).Select(warp => warp.TravelCost).ToArray();

            if (costs.Length == 0)
                return new TravelOption(0, 0, false);

            int cheapest = costs.Min();
            return new TravelOption(costs.Length, cheapest, coins == null || coins.Value >= cheapest);
        }

        /// <summary>
        /// Acts on a clicked room: opening it in the editor, linking a passage to it, or travelling to its
        /// warp.
        /// </summary>
        private void choose(string roomId)
        {
            if (purpose == Purpose.Link)
            {
                LinkRoom?.Invoke(roomId);
                return;
            }

            if (purpose == Purpose.Arrival)
            {
                ArriveFromRoom?.Invoke(roomId);
                return;
            }

            if (!IsTravelling)
            {
                OpenRoom?.Invoke(roomId);
                return;
            }

            WarpPoint[] here = reachable.Where(warp => warp.RoomId == roomId).ToArray();

            if (here.Length == 0)
                return;

            // A room with one warp is unambiguous, so the click is the choice. With several, which one is
            // a real question, and the list answers it rather than this guessing.
            if (here.Length == 1)
                Travel?.Invoke(here[0]);
            else
                ChooseWarpInRoom?.Invoke(roomId);
        }

        /// <summary>
        /// Draws the ways between rooms as rotated bars, refreshed while a room is being dragged.
        /// </summary>
        private void redrawLinks(WorldGraph graph, Dictionary<string, Vector2> positions)
        {
            foreach (RoomNode node in nodeLayer.Children)
                positions[node.RoomId] = node.Position;

            linkLayer.Clear();

            foreach (WorldGraphLink link in graph.Links)
            {
                if (!positions.TryGetValue(link.FromRoomId, out Vector2 from) || !positions.TryGetValue(link.ToRoomId, out Vector2 to))
                    continue;

                Vector2 delta = to - from;
                float length = delta.Length;

                if (length < 1)
                    continue;

                linkLayer.Add(new Box
                {
                    Origin = Anchor.CentreLeft,
                    Position = from,
                    Size = new Vector2(length, 4),
                    Rotation = MathHelper.RadiansToDegrees((float)Math.Atan2(delta.Y, delta.X)),
                    Colour = colours.Gray5,
                });
            }
        }

        /// <summary>
        /// Scales the whole map down until it fits, never up: a two-room world should not be blown up
        /// to fill the panel.
        /// </summary>
        private void fitBoard()
        {
            Vector2 available = board.Parent!.ChildSize;

            if (board.Size.X <= 0 || board.Size.Y <= 0 || available.X <= 0 || available.Y <= 0)
                return;

            board.Scale = new Vector2(Math.Min(1, Math.Min(available.X / board.Size.X, available.Y / board.Size.Y)));
        }

        protected override void Update()
        {
            base.Update();

            if (IsOpen)
                fitBoard();
        }

        /// <summary>Swallows clicks that miss a room, so they do not reach the world behind.</summary>
        protected override bool OnMouseDown(MouseDownEvent e) => true;

        internal int NodeCountForTesting => nodeLayer.Count;
        internal int LinkCountForTesting => linkLayer.Count;

        /// <summary>The rooms that can be travelled to right now, for tests to assert on what is offered.</summary>
        internal IReadOnlyList<string> TravelDestinationsForTesting =>
            nodeLayer.Children.Where(node => node.Enabled.Value).Select(node => node.RoomId).ToArray();

        /// <summary>Clicks a room on the map, as a player would.</summary>
        internal void ClickRoomForTesting(string roomId) =>
            nodeLayer.Children.Single(node => node.RoomId == roomId).TriggerClick();

        /// <summary>
        /// What travelling from this room offers: how many opened warps it holds, what the cheapest one
        /// costs, and whether the player can pay for it.
        /// </summary>
        private readonly record struct TravelOption(int Count, int Cost, bool Affordable);

        private partial class RoomNode : OsuClickableContainer
        {
            public readonly string RoomId;

            public bool Draggable;

            /// <summary>Told where this node was dragged to, in board coordinates.</summary>
            public Action<Vector2>? Moved;

            private Vector2 grabOffset;

            public RoomNode(OsuColour colours, WorldGraphNode node, bool current, bool editing, TravelOption? travel)
            {
                RoomId = node.RoomId;
                Origin = Anchor.Centre;
                Size = node_size;
                Masking = true;
                CornerRadius = 12;

                bool destination = travel is { Count: > 0 };
                bool affordable = travel?.Affordable != false;

                // While travelling, a room is either somewhere to go or context. Dimming the rest is what
                // turns the map into a picker without hiding the shape of the world.
                if (travel != null)
                    Alpha = destination || current ? 1 : 0.45f;

                BorderThickness = current || destination ? 4 : 2;
                BorderColour = current
                    ? colours.Pink1
                    : destination
                        ? affordable ? colours.Blue1 : colours.Red1
                        : colours.Gray6;

                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = current ? colours.Gray3 : colours.Gray2 },
                    new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 3),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = node.Name,
                                Font = OsuFont.Default.With(size: 16, weight: FontWeight.Bold),
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Text = subtitle(node, current, editing, travel),
                                Font = OsuFont.Default.With(size: 12),
                                Colour = travel is { Count: > 0 }
                                    ? affordable ? colours.YellowLight : colours.Red1
                                    : colours.GrayA,
                            },
                        },
                    },
                };
            }

            private static string subtitle(WorldGraphNode node, bool current, bool editing, TravelOption? travel)
            {
                if (current)
                    return "вы здесь";

                if (travel is { } option)
                {
                    if (option.Count == 0)
                        return "нет открытого варпа";

                    string price = option.Cost > 0 ? $"{option.Cost} монет" : "бесплатно";
                    return option.Count == 1 ? price : $"варпов: {option.Count} · от {price}";
                }

                var parts = new List<string>();

                if (node.Warps > 0)
                    parts.Add(node.Warps == 1 ? "варп" : $"варпов: {node.Warps}");

                // Only the author needs to know a room has never been placed by hand.
                if (editing && !node.Placed)
                    parts.Add("авто");

                return parts.Count > 0 ? string.Join(" • ", parts) : node.RoomId;
            }

            protected override bool OnDragStart(DragStartEvent e)
            {
                if (!Draggable)
                    return false;

                grabOffset = Parent!.ToLocalSpace(e.ScreenSpaceMouseDownPosition) - Position;
                return true;
            }

            protected override void OnDrag(DragEvent e)
            {
                Position = Parent!.ToLocalSpace(e.ScreenSpaceMousePosition) - grabOffset;
                Moved?.Invoke(Position);
            }
        }
    }
}
