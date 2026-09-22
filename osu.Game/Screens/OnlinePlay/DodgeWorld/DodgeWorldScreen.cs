// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.DodgeWorld;
using osu.Game.Screens.Backgrounds;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Editor;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Net;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Textures;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld
{
    /// <summary>
    /// Dodge World: a shared, author-editable overworld.
    /// </summary>
    /// <remarks>
    /// This screen composes the feature rather than implementing it. The world document lives in
    /// <c>Model</c>, where it comes from in <c>Net</c>, what the objects are in <c>Entities</c>, how
    /// they are drawn in <c>View</c>, the rules of play in <c>Simulation</c>, and the authoring tools
    /// in <c>Editor</c>. What is left here is wiring: input, the camera, and moving between rooms.
    /// </remarks>
    public partial class DodgeWorldScreen : OsuScreen, IDodgeWorldEditorHost, IAcceptDroppedFiles
    {
        private const string storage_directory = "dodge-world-prototype";

        /// <summary>
        /// No footer: its back button sat over the world, and this screen is left with Escape or the
        /// toolbar instead.
        /// </summary>
        public override bool ShowFooter => false;

        protected override bool InitialBackButtonVisibility => false;

        protected override BackgroundScreen CreateBackground() => new BackgroundScreenDefault();

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            // Leaving for a Dodge match through the portal: the player is no longer in the world, so
            // they should stop appearing in it for everyone else.
            realtime?.LeaveRoom();
            base.OnSuspending(e);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            reportLocalState();
            realtime?.JoinRoom(currentRoomId);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            realtime?.LeaveRoom();
            return base.OnExiting(e);
        }

        /// <summary>
        /// Stops listening to the realtime client.
        /// </summary>
        /// <remarks>
        /// The client belongs to the game, not to this screen, so a subscription outlives the screen that
        /// made it. Leaving them attached crashed the game on the first live run: entering the world a
        /// second time left the first screen still listening, and the next death had it rebuild a room out
        /// of drawables it had already disposed.
        /// </remarks>
        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (realtime == null)
                return;

            realtime.HealthReceived -= onServerHealth;
            realtime.DeathReceived -= onPlayerDied;
            realtime.RewardReceived -= onServerReward;
        }

        private readonly bool allowLocalPreview;
        private readonly WorldEntityRegistry registry = WorldEntityRegistry.Default;
        private readonly WorldSimulation simulation = new WorldSimulation();

        /// <summary>
        /// The local player inside <see cref="simulation"/>. Identified as zero rather than by user id:
        /// locally there is only one, and messages the server sends about this player are addressed to
        /// the connection rather than by id.
        /// </summary>
        private readonly PlayerState localPlayerState;
        private readonly CameraLookAhead lookAhead = new CameraLookAhead();
        private readonly Dictionary<string, RoomDefinition> rooms = new Dictionary<string, RoomDefinition>();
        private readonly Dictionary<string, WeaponSkin> weaponSkins = new Dictionary<string, WeaponSkin>();
        private readonly List<EditableWorldEntity> entities = new List<EditableWorldEntity>();
        private readonly HashSet<Key> pressedKeys = new HashSet<Key>();

        /// <summary>
        /// Snapshots of the current room, most recent last. A room is a plain document, so undo is
        /// just restoring one of these.
        /// </summary>
        private readonly List<RoomDefinition> undoHistory = new List<RoomDefinition>();

        private const int undo_history_limit = 60;

        private IAPIProvider api = null!;
        private OsuColour colours = null!;
        private LargeTextureStore worldTextures = null!;
        private DodgeWorldSession session = null!;
        private DodgeWorldTextureLibrary textureLibrary = null!;
        private WorldEntityContext entityContext = null!;
        private TexturePicker texturePicker = null!;

        private Container worldViewport = null!;
        private Container worldCamera = null!;
        private WorldPlayer localPlayer = null!;
        private SimulationView simulationView = null!;
        private DodgeWorldHud hud = null!;
        private HurtSound hurtSound = null!;
        private LobbyChatPanel chat = null!;
        private DodgeWorldEditorPanel editorPanel = null!;
        private WarpMenu warpMenu = null!;
        private WorldMap worldMap = null!;
        private DodgeWorldClient? realtime;
        private IBindable<bool>? serverSimulatedBinding;
        private RemoteCombatView? remoteCombat;

        /// <summary>
        /// Whether the room's mobs belong to the server. False in the editor, which plays with the layout
        /// on screen rather than the published one.
        /// </summary>
        private bool serverOwnsMobs => !editorMode && realtime?.ServerSimulated.Value == true;

        // No dimming while editing: the author is looking at the room they are building, and a room seen
        // through a grey sheet is a room whose colours and textures cannot be judged.
        private Box passageFlash = null!;
        private OsuSpriteText passageText = null!;

        private string currentRoomId = DodgeWorldDefaults.ROOT_ROOM_ID;

        /// <summary>
        /// The room the world starts in, which is also where death returns a player. Authored rather than
        /// fixed to the seeded hub, or a world could never begin anywhere else.
        /// </summary>
        private string initialRoomId = DodgeWorldDefaults.ROOT_ROOM_ID;

        /// <summary>The room walked out of most recently, or null before the first move.</summary>
        private string? previousRoomId;

        private string currentRoomName = DefaultWorld.ROOT_ROOM_NAME;
        private Vector2 currentRoomSize = DodgeWorldDefaults.MAP_SIZE;
        private string? defaultWeaponSkinId;

        private EditableWorldEntity? selectedEntity;
        private InteractiveNpc? activeNpc;
        private ITexturedEntity? textureImportTarget;
        private TextureImportPurpose importPurpose;

        private Vector2 cameraPosition;


        /// <summary>
        /// Where the view actually sits: the followed position plus the lean, clamped to the room.
        /// </summary>
        private Vector2 renderedCameraPosition;

        private Vector2 editorPanStartMouse;
        private Vector2 editorPanStartCamera;
        private float editorZoom = 1;
        private bool editorMode;
        private bool worldLoaded;
        private double passageCooldown;
        private int customEntityCounter;
        private int roomCounter = 1;
        private Vector2? pendingAttack;

        public DodgeWorldScreen(bool allowLocalPreview = false)
        {
            this.allowLocalPreview = allowLocalPreview;
            localPlayerState = simulation.AddPlayer();
        }

        /// <summary>
        /// Runs against a supplied session instead of the server or local storage.
        /// </summary>
        /// <remarks>
        /// Used by tests, so that they neither reach the network nor share a world file with each other.
        /// </remarks>
        internal DodgeWorldScreen(DodgeWorldSession session)
        {
            allowLocalPreview = true;
            injectedSession = session;
            localPlayerState = simulation.AddPlayer();
        }

        private readonly DodgeWorldSession? injectedSession;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(LargeTextureStore textures, Storage storage, OsuColour colours, IAPIProvider api,
                          ChannelManager? channelManager, DodgeWorldClient? realtimeClient)
        {
            this.api = api;
            this.colours = colours;
            worldTextures = textures;

            // Presence is only meaningful against the published world. A local preview edits a document
            // nobody else has, so telling the server where the player is standing in it would be a lie.
            realtime = allowLocalPreview ? null : realtimeClient;

            Storage layoutStorage = storage.GetStorageForDirectory(storage_directory);
            Storage textureStorage = layoutStorage.GetStorageForDirectory("textures");

            session = injectedSession
                      ?? (allowLocalPreview
                          ? new LocalDodgeWorldSession(layoutStorage, textureStorage)
                          : new ServerDodgeWorldSession(api));

            textureLibrary = new DodgeWorldTextureLibrary(textureStorage, textures);

            entityContext = new WorldEntityContext(colours, textures, () => editorMode, selectEntity, dialogueLanguage,
                requestTexture);

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray0 },
                worldViewport = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    // Clears the toolbar at the top; the bottom is free now that there is no footer.
                    Padding = new MarginPadding { Top = 58 },
                    Masking = true,
                    Child = worldCamera = new Container
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = currentRoomSize,
                    },
                },
                textureLibrary,
                hurtSound = new HurtSound(),
                hud = new DodgeWorldHud(colours, session) { Depth = -100 },
                chat = new LobbyChatPanel(colours, channelManager, dialogueLanguage),
                passageFlash = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Gray0,
                    Alpha = 0,
                    Depth = -105,
                },
                passageText = new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Font = OsuFont.Default.With(size: 24, weight: FontWeight.Bold),
                    Alpha = 0,
                    Depth = -106,
                },
                warpMenu = new WarpMenu(colours) { Depth = -115 },
                worldMap = new WorldMap(colours) { Depth = -118 },
                editorPanel = new DodgeWorldEditorPanel(colours, registry, textureLibrary, this) { Depth = -120 },
                texturePicker = new TexturePicker(colours, SupportedExtensions.IMAGE_EXTENSIONS) { Depth = -125 },
            };

            warpMenu.Travel = travelTo;
            worldMap.OpenRoom = openRoomFromMap;
            worldMap.PlaceRoom = placeRoomOnMap;
            worldMap.Travel = travelTo;
            worldMap.ChooseWarpInRoom = chooseWarpInRoom;
            worldMap.LinkRoom = linkSelectedPassageTo;
            worldMap.ArriveFromRoom = setArrivalFromRoom;

            worldCamera.Add(new RoomBackdrop(colours) { Depth = 10_000 });
            worldCamera.Add(localPlayer = new WorldPlayer(colours));

            AddInternal(simulationView = new SimulationView(worldCamera, localPlayer, localPlayerState, colours,
                simulation, textureLibrary.GetWeaponTexture, textureLibrary.GetRenderSkin, mobAppearance));

            if (realtime != null)
            {
                AddInternal(new RemotePlayerView(worldCamera, colours, realtime,
                    textureLibrary.GetWeaponTexture, textureLibrary.GetRenderSkin));
                AddInternal(remoteCombat = new RemoteCombatView(worldCamera, colours, realtime, mobAppearance));

                // The server takes a room over a round trip after it is entered, so the local mobs have
                // to be dropped when the answer arrives rather than when the room is loaded. Bound as a
                // copy, so the subscription dies with this screen rather than being held by a client that
                // outlives it.
                serverSimulatedBinding = realtime.ServerSimulated.GetBoundCopy();
                serverSimulatedBinding.BindValueChanged(simulated =>
                {
                    if (!simulated.NewValue)
                        remoteCombat.Clear();

                    reloadLayout();
                });

                realtime.HealthReceived += onServerHealth;
                realtime.DeathReceived += onPlayerDied;
                realtime.RewardReceived += onServerReward;
            }

            api.LocalUser.GetBoundCopy().BindValueChanged(user => localPlayer.SetUser(user.NewValue, colours), true);

            simulation.PlayerHealthChanged += player => hud.SetHealth(player.Health);

            // Only this player's own hits are sounded. Somebody else being shot across the room is
            // their business, and in a busy room it would be a constant rattle.
            simulation.PlayerDamaged += (player, _) =>
            {
                if (player == localPlayerState)
                    hurtSound.Play();
            };

            simulation.PlayerDied += _ => onPlayerDied();

            simulation.MobDefeated += (_, killer) =>
            {
                if (killer == localPlayerState)
                    onLocalKill();
            };

            session.Availability.BindValueChanged(value => onAvailabilityChanged(value.NewValue), true);

            // The story deciding what a room contains means the room has to be rebuilt when it advances,
            // rather than only on the next visit: a door that opens while standing in front of it should
            // open on screen.
            session.Flags.BindValueChanged(_ => refreshStoryVisibility());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Loading starts here rather than in the loader: a local session answers synchronously, and
            // applying a world touches the texture library, which is only initialised once its own
            // asynchronous load has run.
            session.Load(applyDocument);
        }

        #region world loading

        private void onAvailabilityChanged(DodgeWorldAvailability availability)
        {
            switch (availability)
            {
                case DodgeWorldAvailability.Loading:
                    editorPanel.SetStatus("Загрузка Dodge World с сервера…");
                    hud.SetRoom("LOADING WORLD…");
                    break;

                case DodgeWorldAvailability.LoginRequired:
                    editorPanel.SetStatus("Для Dodge World нужна активная онлайн-сессия.");
                    hud.SetRoom("ONLINE REQUIRED");
                    break;

                case DodgeWorldAvailability.Unreachable:
                    editorPanel.SetStatus("Сервер Dodge World недоступен.");
                    hud.SetRoom("WORLD UNAVAILABLE");
                    break;

                case DodgeWorldAvailability.InvalidDocument:
                    editorPanel.SetStatus("Сервер вернул некорректный документ мира.");
                    hud.SetRoom("WORLD DATA ERROR");
                    break;
            }
        }

        private void applyDocument(DodgeWorldDocument document)
        {
            rooms.Clear();

            foreach (RoomDefinition room in document.Rooms)
                rooms[room.Id] = room;

            weaponSkins.Clear();

            foreach (WeaponSkin skin in document.WeaponSkins ?? Enumerable.Empty<WeaponSkin>())
                weaponSkins[skin.Id] = skin;

            defaultWeaponSkinId = document.DefaultWeaponSkinId;
            textureLibrary.RegisterFromDocument(document);
            textureLibrary.SetActiveSkin(resolveSkin(defaultWeaponSkinId));

            applySwordDamage();

            RoomDefinition? initial = document.ResolveInitialRoom();

            if (initial == null)
                return;

            // From the resolved room rather than from the field: a document naming a room it no longer
            // contains would otherwise keep sending players nowhere.
            initialRoomId = initial.Id;

            worldLoaded = true;
            enterRoom(initial);
            editorPanel.SetStatus($"Мир загружен: комнат {rooms.Count}, сохранение №{session.Revision.Value}.");
        }

        private WeaponSkin? resolveSkin(string? id) =>
            id != null && weaponSkins.TryGetValue(id, out WeaponSkin? skin) ? skin : null;

        /// <summary>
        /// The damage of the world's sword, as the author left it.
        /// </summary>
        private int swordDamage =>
            resolveSkin(defaultWeaponSkinId)?.EffectiveDamage ?? WeaponSkin.DEFAULT_DAMAGE;

        private void applySwordDamage()
        {
            localPlayerState.SetSwordDamage(swordDamage);
            editorPanel.ShowSwordDamage(swordDamage);
        }

        /// <summary>
        /// Sets what one swing takes off a mob.
        /// </summary>
        /// <remarks>
        /// A world with no sword of its own gets one carrying no texture: that draws the built-in sword as
        /// before and holds the damage.
        /// </remarks>
        public void SetSwordDamage(int damage)
        {
            WeaponSkin? skin = resolveSkin(defaultWeaponSkinId);

            if (skin == null)
            {
                skin = new WeaponSkin { Id = "default", DisplayName = "Default sword" };
                weaponSkins[skin.Id] = skin;
                defaultWeaponSkinId = skin.Id;
                textureLibrary.SetActiveSkin(skin);
            }

            skin.Damage = Math.Clamp(damage, WeaponSkin.MIN_DAMAGE, WeaponSkin.MAX_DAMAGE);
            localPlayerState.SetSwordDamage(skin.EffectiveDamage);
            editorPanel.SetStatus($"Урон меча: {skin.EffectiveDamage}. Сохрани мир, чтобы это увидели остальные.");
        }

        /// <summary>
        /// Replaces every entity in the world container with the contents of a room, and hands the
        /// simulation its new collision map and mob population.
        /// </summary>
        private void enterRoom(RoomDefinition room)
        {
            clearRoom();

            currentRoomId = room.Id;
            currentRoomName = room.Name;
            applyRoomSize(room.Size);

            buildRoomEntities(room);
            refreshArrivalMarkers();

            simulation.LoadRoom(buildLayout(room));

            hud.SetRoom(room.Name);
            hud.SetHealth(localPlayerState.Health);
            cameraPosition = Vector2.Zero;
            lookAhead.Reset();
            activeNpc = null;

            editorPanel.SetRoom(room.Name, currentRoomSize);
            setSelection(null);
            roomPendingDeletion = null;
            editorPanel.SetStatus($"Комната: {room.Name}." + (room.Id == initialRoomId ? " Начальная." : string.Empty));

            // Announced after the local player has been placed, so the position that goes out with the
            // join is where they actually are rather than where they were in the room just left.
            reportLocalState();
            realtime?.JoinRoom(room.Id);
        }

        /// <summary>
        /// Tells the realtime client where the local player is, for it to forward when it sees fit.
        /// </summary>
        private void reportLocalState() => realtime?.SetLocalState(localPlayerState.Position, localPlayerState.Facing,
            localPlayerState.Moving, localPlayerState.Health);

        /// <summary>
        /// Derives what the simulation needs from the live entities.
        /// </summary>
        private RoomLayout buildLayout(RoomDefinition room)
        {
            var obstacles = entities
                            .Where(entity => entity.BlocksMovement)
                            .Select(entity => new Obstacle(
                                entity.Position + CombatRules.Rotate(entity.CollisionCentreOffset * entity.Scale, entity.FacingDegrees),
                                entity.CollisionSize * entity.Scale,
                                entity.FacingDegrees))
                            .ToArray();

            // No local mobs while the editor is open, and none at all once the server is running them:
            // two sets of mobs in one room would be two fights over the same zones.
            MobSpawnDefinition[] zones = editorMode || serverOwnsMobs
                ? Array.Empty<MobSpawnDefinition>()
                : entities.OfType<MobSpawnZone>().Select(describeZone).ToArray();

            // Hazards are held to the same rule as mobs: the editor is calm, and a room the server runs
            // has exactly one course running in it.
            HazardDefinition[] hazards = editorMode || serverOwnsMobs
                ? Array.Empty<HazardDefinition>()
                : entities.OfType<WorldHazardDevice>().Select(describeHazard).ToArray();

            return new RoomLayout(room.Size, room.Spawn, obstacles, zones, hazards);
        }

        /// <summary>
        /// Describes a hazard from the live entity, which is what the editor is changing, rather than from
        /// the document, which is what was last saved.
        /// </summary>
        private static HazardDefinition describeHazard(WorldHazardDevice device) => device switch
        {
            WorldEmitter emitter => new HazardDefinition(emitter.EntityId, HazardKind.Emitter, muzzleOf(emitter),
                emitter.Direction, emitter.Turn, emitter.CycleMilliseconds, emitter.PhaseMilliseconds,
                emitter.ProjectileCount, emitter.Spread, emitter.ProjectileDamage, emitter.ProjectileSpeed,
                emitter.ProjectileRange, 0, 0),

            WorldBeamDevice beam => new HazardDefinition(beam.EntityId, HazardKind.Beam, muzzleOf(beam),
                beam.Direction, beam.Turn, beam.CycleMilliseconds, beam.PhaseMilliseconds, 0, 0, beam.Damage, 0,
                beam.Length, beam.Width, beam.ActiveMilliseconds),

            _ => throw new ArgumentOutOfRangeException(nameof(device), device, "Unknown hazard device"),
        };

        /// <summary>
        /// Where a live device fires from, by the same rule the stored one uses.
        /// </summary>
        private static Vector2 muzzleOf(WorldHazardDevice device) =>
            CombatRules.HazardMuzzle(device.Position, device.Scale.Y);

        private static MobSpawnDefinition describeZone(MobSpawnZone zone) => new MobSpawnDefinition(
            zone.EntityId, zone.Position, zone.Size, zone.SpawnCount, zone.MobMaxHealth, zone.ContactDamage,
            zone.DetectionRadius, zone.ProjectileCount, zone.ProjectileDamage, zone.ProjectileSpeed,
            zone.ProjectileRange, zone.AttackCooldown, zone.MobSpeed, zone.Chases);

        /// <summary>
        /// Rebuilds the simulation's view of the room, after the editor changed its layout.
        /// </summary>
        private void reloadLayout()
        {
            if (!rooms.TryGetValue(currentRoomId, out RoomDefinition? room))
                return;

            RoomLoadCountForTesting++;
            // The player keeps their place: a layout change is not a room change.
            simulation.ReplaceLayout(buildLayout(room));
        }

        /// <summary>
        /// Creates the entities of a room that the player's story lets them see.
        /// </summary>
        /// <remarks>
        /// The records of everything hidden are kept, not discarded: <see cref="captureCurrentRoom"/> reads
        /// the live entities, so a hidden object that was merely skipped would be deleted from the document
        /// the first time the player walked out of the room. The editor hides nothing — an author has to be
        /// able to reach what a player cannot.
        /// </remarks>
        private void buildRoomEntities(RoomDefinition room)
        {
            hiddenRecords.Clear();

            foreach (EntityRecord record in room.Entities)
            {
                if (!editorMode && !visibleToPlayer(record))
                {
                    hiddenRecords.Add(record);
                    continue;
                }

                EditableWorldEntity? entity = registry.Create(record, entityContext);

                if (entity == null)
                    continue;

                if (entity is InteractiveNpc npc)
                    npc.DialogueFinished += onDialogueFinished;

                addEntity(entity);
            }
        }

        /// <summary>
        /// Records that the player has heard a character out, if that character is a story point.
        /// </summary>
        /// <remarks>
        /// Only the server's answer counts, so nothing is assumed here: the flag arrives back through
        /// <see cref="DodgeWorldSession.Flags"/> and the room reacts to that. A conversation with nothing
        /// declared on it is just a conversation.
        /// </remarks>
        private void onDialogueFinished(InteractiveNpc npc)
        {
            if (editorMode || npc.StoryFlag is not string flag)
                return;

            session.RaiseStoryFlag(currentRoomId, npc.EntityId, flag, npc.StoryFlagValue, announceStoryReward);
        }

        /// <summary>
        /// Tells the player what a story point paid them, when it paid anything.
        /// </summary>
        /// <remarks>
        /// Silent otherwise, including on a point already passed: hearing "nothing this time" every time
        /// somebody talks to a character again would be noise, and the story moving on is its own signal.
        /// </remarks>
        private void announceStoryReward(StoryOutcome outcome)
        {
            if (!outcome.Paid)
                return;

            var parts = new List<string>();

            if (outcome.Experience > 0)
                parts.Add($"+{outcome.Experience} ОПЫТА");

            if (outcome.Coins > 0)
                parts.Add($"+{outcome.Coins} МОНЕТ");

            announce(string.Join(" · ", parts), long_announcement_hold);
        }

        /// <summary>
        /// Rebuilds the room when the player's story changed what it contains, leaving them where they are:
        /// a door opening is not a reason to be sent back to the entrance.
        /// </summary>
        private void refreshStoryVisibility()
        {
            if (!worldLoaded || !rooms.ContainsKey(currentRoomId))
                return;

            // The live room is captured first, exactly as a room change does it. Rebuilding from the stored
            // room would throw away anything the author has added since the last save — including, when the
            // editor closes, the very object they just gated.
            rooms[currentRoomId] = captureCurrentRoom();
            RoomDefinition room = rooms[currentRoomId];

            var shown = new HashSet<string>(room.Entities
                                                .Where(record => editorMode || visibleToPlayer(record))
                                                .Select(record => record.Id), StringComparer.Ordinal);

            if (shown.SetEquals(entities.Select(entity => entity.EntityId)))
                return;

            setSelection(null);

            foreach (EditableWorldEntity entity in entities.ToArray())
                worldCamera.Remove(entity, true);

            entities.Clear();
            buildRoomEntities(room);

            // The layout changed, but the room did not: the player keeps their place and their health.
            simulation.ReplaceLayout(buildLayout(room));

            if (!editorMode)
                announce("МИР ИЗМЕНИЛСЯ");
        }

        private bool visibleToPlayer(EntityRecord record) =>
            StoryCondition.Read(record.VisibleIfFlag, record.VisibleIfAtLeast, record.VisibleIfBelow)
                          .Matches(session.Flags.Value);

        private readonly List<EntityRecord> hiddenRecords = new List<EntityRecord>();

        private void clearRoom()
        {
            setSelection(null);

            foreach (EditableWorldEntity entity in entities.ToArray())
                worldCamera.Remove(entity, true);

            entities.Clear();

            // The server's mobs belong to the room being left. Waiting for the next snapshot to notice
            // they are gone would leave them standing in the new room for a moment.
            remoteCombat?.Clear();
        }

        /// <summary>The arrival points drawn in this room, in the editor only.</summary>
        private readonly List<ArrivalMarker> arrivalMarkers = new List<ArrivalMarker>();

        /// <summary>
        /// Draws every spot another room's passages put the player down in this one.
        /// </summary>
        /// <remarks>
        /// The points belong to passages elsewhere, so they cannot be children of anything in this room and
        /// have to be looked up across the world. Computed arrivals are drawn as well as hand-placed ones,
        /// in a different colour: how a passage decides where to leave somebody was the one thing about it
        /// that could not be seen, and «I don't understand how they work» is the fault of that.
        /// <para>
        /// Rebuilt rather than kept in step, because it happens when a room is entered, when the editor
        /// opens, and when something is linked — never in the middle of play.
        /// </para>
        /// </remarks>
        private void refreshArrivalMarkers()
        {
            foreach (ArrivalMarker marker in arrivalMarkers)
                worldCamera.Remove(marker, true);

            arrivalMarkers.Clear();

            if (!editorMode)
                return;

            foreach (RoomDefinition room in rooms.Values)
            {
                // The live room's own records are the ones being edited, so they are read from the entities
                // instead — and a passage leading back into its own room has nothing to show anyway.
                if (room.Id == currentRoomId)
                    continue;

                foreach (EntityRecord record in room.Entities)
                {
                    if (record.DestinationRoomId != currentRoomId)
                        continue;

                    if (record.Kind != EntityKinds.PASSAGE && record.Kind != EntityKinds.PORTAL)
                        continue;

                    bool authored = record.ArrivalX is float && record.ArrivalY is float;

                    Vector2? at = authored
                        ? new Vector2(record.ArrivalX!.Value, record.ArrivalY!.Value)
                        : arrivalPosition(room.Id, record.ArrivalEntityId, record.Id, exitDirectionOf(record));

                    if (at is not Vector2 point)
                        continue;

                    string from = record.DisplayName ?? record.Id;

                    var marker = new ArrivalMarker(colours, authored
                        ? $"сюда из «{room.Name}» • {from} • вручную"
                        : $"сюда из «{room.Name}» • {from} • авто", authored)
                    {
                        Position = point,
                        Depth = -9000,
                    };

                    arrivalMarkers.Add(marker);
                    worldCamera.Add(marker);
                }
            }
        }

        private void addEntity(EditableWorldEntity entity)
        {
            entity.EditBegan = pushUndo;
            entity.CurrentSelection = () => selectedEntity;
            entity.Clicked = entityClicked;
            entities.Add(entity);
            worldCamera.Add(entity);
            entity.SetEditing(editorMode);
        }

        /// <summary>
        /// Records the current room so the next change can be undone.
        /// </summary>
        private void pushUndo()
        {
            if (!worldLoaded)
                return;

            undoHistory.Add(captureCurrentRoom());

            if (undoHistory.Count > undo_history_limit)
                undoHistory.RemoveAt(0);
        }

        public void Undo()
        {
            if (undoHistory.Count == 0)
            {
                editorPanel.SetStatus("Отменять нечего.");
                return;
            }

            RoomDefinition previous = undoHistory[^1];
            undoHistory.RemoveAt(undoHistory.Count - 1);

            rooms[previous.Id] = previous;
            currentRoomName = previous.Name;
            enterRoom(previous);
            editorPanel.SetStatus($"Отменено. Шагов в истории: {undoHistory.Count}.");
        }

        private void applyRoomSize(Vector2 size)
        {
            currentRoomSize = new Vector2(
                Math.Clamp(size.X, DodgeWorldDefaults.MINIMUM_MAP_SIZE.X, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.X),
                Math.Clamp(size.Y, DodgeWorldDefaults.MINIMUM_MAP_SIZE.Y, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.Y));

            worldCamera.Size = currentRoomSize;
        }

        /// <summary>
        /// Keeps a room's contents where they were relative to its top left corner across a resize.
        /// </summary>
        /// <remarks>
        /// Room coordinates are measured from the centre, so without this a room grows in both
        /// directions at once: half of the new space appears above the existing layout and half below
        /// it, and everything already placed drifts. Anchoring to the corner makes a room extend
        /// towards the bottom right instead, which is what asking for a larger room looks like.
        /// </remarks>
        private void anchorContentsAfterResize(Vector2 previousSize)
        {
            Vector2 offset = (previousSize - currentRoomSize) / 2;

            if (offset == Vector2.Zero)
                return;

            foreach (EditableWorldEntity entity in entities)
                entity.Position += offset;

            // The spawn point lives on the stored room rather than on an entity, and is read back by
            // the capture that follows this call.
            if (rooms.TryGetValue(currentRoomId, out RoomDefinition? room))
            {
                room.SpawnX += offset.X;
                room.SpawnY += offset.Y;
            }

            cameraPosition += offset;
        }

        /// <summary>
        /// Captures the live entities back into the stored room.
        /// </summary>
        private RoomDefinition captureCurrentRoom()
        {
            // The spawn point is not represented by an entity, so it is carried over from the stored room.
            Vector2 spawn = rooms.TryGetValue(currentRoomId, out RoomDefinition? existing)
                ? existing.Spawn
                : DodgeWorldDefaults.PLAYER_SPAWN;

            return new RoomDefinition
            {
                Id = currentRoomId,
                Name = currentRoomName,
                Width = currentRoomSize.X,
                Height = currentRoomSize.Y,
                SpawnX = spawn.X,
                SpawnY = spawn.Y,
                // Like the spawn point, the room's place on the world map has no entity to read it
                // back from, so it is carried over rather than rebuilt.
                MapX = existing?.MapX,
                MapY = existing?.MapY,
                // Anything the player's story hides has no live entity to read back, so its record is
                // carried over untouched. Without this, walking out of a room would delete from the world
                // everything the player had not yet unlocked.
                Entities = entities.Select(registry.Capture).OfType<EntityRecord>().Concat(hiddenRecords).ToList(),
            };
        }

        private void switchRoom(string roomId)
        {
            if (!rooms.TryGetValue(roomId, out RoomDefinition? room))
            {
                editorPanel.SetStatus($"Комната с ID «{roomId}» не существует.");
                return;
            }

            rooms[currentRoomId] = captureCurrentRoom();

            // Remembered so that a passage can be pointed back the way the author came, which is the link
            // they are almost always after and the one that is most tedious to type out.
            if (roomId != currentRoomId)
                previousRoomId = currentRoomId;

            // History is per-room: undoing into a room you have left would be surprising.
            undoHistory.Clear();

            enterRoom(room);
            passageCooldown = Time.Current + CombatRules.PASSAGE_COOLDOWN;
        }

        #endregion

        #region game loop

        protected override void Update()
        {
            base.Update();

            activeNpc = entities.OfType<InteractiveNpc>().FirstOrDefault(npc => npc.IsDialogueOpen);
            closeDialogueIfWalkedAway();

            // A conversation is deliberately not on this list. The world is shared and runs while somebody
            // reads a line, which is also why a player can walk out of a conversation: standing frozen in
            // front of a mob while a mob keeps moving would be the worst of both.
            bool paused = !worldLoaded || editorMode || chat.InputFocused
                          || warpMenu.IsOpen || worldMap.IsOpen;

            if (paused)
            {
                pendingAttack = null;

                // In the editor the movement keys pan the view instead of walking. Anywhere else a pause
                // drops them, so that keys released while the world was stopped do not come back held.
                if (editorMode && !chat.InputFocused && !worldMap.IsOpen)
                    panCameraByKeys();
                else
                    pressedKeys.Clear();
            }
            else
            {
                // No swinging mid-sentence: the mouse is what advances the bubble, and a click meant for
                // the next line should not also be a swing.
                if (activeNpc != null)
                    pendingAttack = null;

                // The swing is played locally either way, because a sword that waits for a round trip
                // feels broken. When the server owns the room it is also the one that decides what the
                // swing hit; locally there are no mobs for it to hit.
                if (pendingAttack is Vector2 attack)
                    realtime?.Attack(attack);

                simulation.SetInput(localPlayerState.UserId, new PlayerInput(movementInput(), pendingAttack));
                simulation.Step(Time.Elapsed);
                pendingAttack = null;
            }

            reportLocalState();
            updateInteractionPrompts();

            if (!editorMode)
                updatePassages();

            updateCamera();
            updateEntityDepths();
            updateSurfaceSeams();
        }

        /// <summary>
        /// Ends a conversation the player has walked out of.
        /// </summary>
        /// <remarks>
        /// The other half of the world not stopping for a conversation: since the player can still walk,
        /// walking away has to mean something. A little further than the distance that offers the
        /// conversation, so that standing right at the edge of range does not flicker in and out of it.
        /// </remarks>
        private void closeDialogueIfWalkedAway()
        {
            if (activeNpc == null || editorMode)
                return;

            if ((localPlayerState.Position - activeNpc.Position).Length <= CombatRules.INTERACTION_DISTANCE * 1.6f)
                return;

            activeNpc.CloseDialogue();
            activeNpc = null;
        }

        /// <summary>
        /// Walks the view around the room while editing.
        /// </summary>
        /// <remarks>
        /// The arrow keys already nudge the selected object, so there was no way to move the view from the
        /// keyboard at all with something selected. Panning is scaled by the zoom, so the room moves under
        /// the cursor at the same speed however far out the author is looking.
        /// </remarks>
        private void panCameraByKeys()
        {
            Vector2 direction = movementInput();

            if (direction.LengthSquared <= 0)
                return;

            float distance = (float)(Time.Elapsed / 1000) * editor_pan_speed / Math.Max(editorZoom, 0.01f);
            cameraPosition += Vector2.Normalize(direction) * distance;
        }

        /// <summary>How fast the editor's view walks, in world units per second.</summary>
        private const float editor_pan_speed = CombatRules.PLAYER_SPEED * 2.5f;

        private Vector2 movementInput()
        {
            Vector2 direction = Vector2.Zero;

            if (pressedKeys.Contains(Key.A) || pressedKeys.Contains(Key.Left)) direction.X--;
            if (pressedKeys.Contains(Key.D) || pressedKeys.Contains(Key.Right)) direction.X++;
            if (pressedKeys.Contains(Key.W) || pressedKeys.Contains(Key.Up)) direction.Y--;
            if (pressedKeys.Contains(Key.S) || pressedKeys.Contains(Key.Down)) direction.Y++;

            return direction;
        }

        private void updateInteractionPrompts()
        {
            InteractiveNpc? nearest = nearestNpc();

            foreach (InteractiveNpc npc in entities.OfType<InteractiveNpc>())
            {
                npc.SetInteractionAvailable(npc == nearest
                                            && (localPlayerState.Position - npc.Position).Length < CombatRules.INTERACTION_DISTANCE
                                            && !editorMode);
            }

            // A warp offers itself only when there is no NPC to talk to, so one E press cannot mean
            // two things at once.
            WorldWarp? warp = nearest == null || !inInteractionRange(nearest) ? nearestWarp() : null;

            foreach (WorldWarp candidate in entities.OfType<WorldWarp>())
            {
                candidate.SetUnlocked(session.IsWarpUnlocked(currentRoomId, candidate.EntityId));
                candidate.SetPrompt(candidate == warp && inInteractionRange(candidate) && !editorMode && !warpMenu.IsOpen);
            }
        }

        private bool inInteractionRange(EditableWorldEntity entity) =>
            (localPlayerState.Position - entity.Position).Length < CombatRules.INTERACTION_DISTANCE;

        private InteractiveNpc? nearestNpc() => entities.OfType<InteractiveNpc>()
                                                       .OrderBy(npc => (localPlayerState.Position - npc.Position).LengthSquared)
                                                       .FirstOrDefault();

        private WorldWarp? nearestWarp() => entities.OfType<WorldWarp>()
                                                   .OrderBy(warp => (localPlayerState.Position - warp.Position).LengthSquared)
                                                   .FirstOrDefault();

        /// <summary>
        /// Keeps the draw order back to front, and breaks ties by the order the room lists its objects in.
        /// </summary>
        /// <remarks>
        /// The tie-break is what makes layers controllable at all. Panels all share one fixed depth, so
        /// without it two overlapping panels were ordered by whatever the container's sorted insert
        /// happened to do — the author could neither predict which was on top nor change it. Later in the
        /// room's list now means nearer the front, which <see cref="BringSelectionToFront"/> moves.
        /// </remarks>
        private void updateEntityDepths()
        {
            for (int i = 0; i < entities.Count; i++)
            {
                EditableWorldEntity entity = entities[i];
                float depth = (entity.FixedDepth ?? -entity.Position.Y) - i * layer_step;

                if (entity.Depth != depth)
                    worldCamera.ChangeChildDepth(entity, depth);
            }
        }

        /// <summary>
        /// Depth separating two objects that would otherwise share one.
        /// </summary>
        /// <remarks>
        /// A negative power of two, so that adding it stays exact even next to the fixed depths of nine
        /// thousand that panels use — at that size a rounder step would be swallowed by the float and the
        /// tie-break would silently do nothing. Small enough not to reorder objects placed at genuinely
        /// different distances.
        /// </remarks>
        private const float layer_step = 1f / 512;

        /// <summary>
        /// The room's panels, reused between frames so that looking them over costs no allocation.
        /// </summary>
        private readonly List<RoomSurface> surfaces = new List<RoomSurface>();

        /// <summary>What the panels looked like when the seams were last worked out.</summary>
        private int surfaceLayout;

        /// <summary>Where the panels touch, reused between frames like <see cref="surfaces"/>.</summary>
        private readonly List<EdgeSpan> coveredSpans = new List<EdgeSpan>();

        /// <summary>
        /// Hides the outline along the stretches of panel edges another panel covers.
        /// </summary>
        /// <remarks>
        /// Two panels laid flush used to show their seam twice over: both drew a border there, and the one
        /// in front drew its border across the other's fill. Panels are how a floor is built, and a floor
        /// built out of panels should look like a floor.
        /// <para>
        /// Recomputed only when a panel has moved, been resized, or the room changed — the answer is
        /// quadratic in the number of panels, and a room's panels stand still almost all of the time.
        /// </para>
        /// </remarks>
        private void updateSurfaceSeams()
        {
            surfaces.Clear();

            var signature = new HashCode();
            signature.Add(currentRoomId);

            foreach (EditableWorldEntity entity in entities)
            {
                if (entity is not RoomSurface surface)
                    continue;

                surfaces.Add(surface);
                signature.Add(surface.Position);
                signature.Add(surface.Size);
                signature.Add(surface.FacingDegrees);
            }

            int layout = signature.ToHashCode();

            if (layout == surfaceLayout)
                return;

            surfaceLayout = layout;

            foreach (RoomSurface surface in surfaces)
            {
                coveredSpans.Clear();

                foreach (RoomSurface other in surfaces)
                {
                    if (!ReferenceEquals(other, surface))
                        RoomSurface.AddCoveredSpans(surface, other, coveredSpans);
                }

                surface.SetCoveredSpans(coveredSpans);
            }
        }

        private void updateCamera()
        {
            Vector2 target = cameraPosition;
            float zoom = Math.Max(0.01f, worldCamera.Scale.X);
            bool inDialogue = activeNpc?.IsDialogueOpen == true;

            if (!editorMode)
            {
                if (inDialogue)
                    target = activeNpc!.DialogueCameraFocus;
                else
                {
                    Vector2 difference = localPlayerState.Position - cameraPosition;

                    if (difference.X > CombatRules.CAMERA_DEADZONE.X) target.X += difference.X - CombatRules.CAMERA_DEADZONE.X;
                    if (difference.X < -CombatRules.CAMERA_DEADZONE.X) target.X += difference.X + CombatRules.CAMERA_DEADZONE.X;
                    if (difference.Y > CombatRules.CAMERA_DEADZONE.Y) target.Y += difference.Y - CombatRules.CAMERA_DEADZONE.Y;
                    if (difference.Y < -CombatRules.CAMERA_DEADZONE.Y) target.Y += difference.Y + CombatRules.CAMERA_DEADZONE.Y;
                }
            }

            Vector2 viewportSize = worldViewport.ChildSize / zoom;
            Vector2 maximum = new Vector2(
                Math.Max(0, (currentRoomSize.X - viewportSize.X) / 2),
                Math.Max(0, (currentRoomSize.Y - viewportSize.Y) / 2));

            target = clampToRoom(target, maximum);

            if (editorMode)
                cameraPosition = target;
            else
            {
                double timeConstant = inDialogue ? CombatRules.CAMERA_DIALOGUE_SMOOTHING : CombatRules.CAMERA_SMOOTHING;

                cameraPosition += (target - cameraPosition) * (1 - (float)Math.Exp(-Time.Elapsed / timeConstant));
            }

            // The lean is added on top of the followed position rather than folded into the target,
            // because the deadzone would otherwise swallow the way back: a camera left leaning by
            // less than the deadzone is already "close enough" to the player and would never recentre.
            bool walking = !editorMode && !inDialogue && localPlayerState.Moving;
            Vector2 lean = lookAhead.Advance(Time.Elapsed, walking ? localPlayerState.Facing : Vector2.Zero);

            renderedCameraPosition = clampToRoom(cameraPosition + lean, maximum);
            worldCamera.Position = -renderedCameraPosition * zoom;
        }

        private static Vector2 clampToRoom(Vector2 position, Vector2 maximum) => new Vector2(
            Math.Clamp(position.X, -maximum.X, maximum.X),
            Math.Clamp(position.Y, -maximum.Y, maximum.Y));

        private void updatePassages()
        {
            if (Time.Current < passageCooldown)
                return;

            WorldPassage? passage = entities.OfType<WorldPassage>()
                                            .FirstOrDefault(candidate => (candidate.Position - localPlayerState.Position).Length < candidate.TriggerRadius);

            if (passage == null)
                return;

            passageCooldown = Time.Current + CombatRules.PASSAGE_COOLDOWN;

            announcePassage(passage.Destination);

            if (!string.IsNullOrEmpty(passage.DestinationRoomId) && rooms.ContainsKey(passage.DestinationRoomId))
                enterThroughPassage(passage);
            else
                reloadLayout();
        }

        /// <summary>
        /// Walks through a passage, arriving at the way back rather than at the room's entrance.
        /// </summary>
        /// <remarks>
        /// A corridor should let the player turn round and be where they were. The entrance is where a room
        /// is entered from nowhere — a death, a warp, the world being loaded — not where a door leads.
        /// </remarks>
        private void enterThroughPassage(WorldPassage passage)
        {
            string leaving = currentRoomId;
            string? named = passage.ArrivalEntityId;
            string walkedThrough = passage.EntityId;
            Vector2? exit = passage.ExitDirection;
            Vector2? placed = passage.ArrivalPoint;

            switchRoom(passage.DestinationRoomId!);

            if ((placed ?? arrivalPosition(leaving, named, walkedThrough, exit)) is not Vector2 arrival)
                return;

            simulation.PlacePlayer(arrival);
            cameraPosition = arrival;
            lookAhead.Reset();
        }

        /// <summary>
        /// Where a passage leaves the player in the room just entered, or null to leave them at its
        /// entrance.
        /// </summary>
        /// <remarks>
        /// In order: the object the passage names, because the author said so; the doorway here that names
        /// the passage just walked through, which is the other half of the same pair; the way back that
        /// faces the other way, which is the far end of the same corridor; and only then any way back at
        /// all. The last one is the rule this used to have on its own, and with two corridors between the
        /// same two rooms it meant walking through one of them and coming out of the other.
        /// </remarks>
        /// <param name="leavingRoomId">The room being left.</param>
        /// <param name="arrivalEntityId">What the passage says to arrive beside, if anything.</param>
        /// <param name="walkedThroughId">The passage the player walked through, in the room being left.</param>
        /// <param name="exitDirection">Which way that passage faces, if it has a side.</param>
        private Vector2? arrivalPosition(string leavingRoomId, string? arrivalEntityId, string walkedThroughId,
                                         Vector2? exitDirection)
        {
            EditableWorldEntity? target = null;

            if (!string.IsNullOrEmpty(arrivalEntityId))
                target = entities.FirstOrDefault(entity => entity.EntityId == arrivalEntityId);

            WorldPassage[] waysBack = entities.OfType<WorldPassage>()
                                              .Where(candidate => candidate.DestinationRoomId == leavingRoomId)
                                              .ToArray();

            target ??= waysBack.FirstOrDefault(candidate => candidate.ArrivalEntityId == walkedThroughId);

            if (target == null && exitDirection is Vector2 exit && exit.LengthSquared > 0)
            {
                target = waysBack
                         .Where(candidate => candidate.ExitDirection is Vector2 direction && direction.LengthSquared > 0)
                         .OrderBy(candidate => Vector2.Dot(Vector2.Normalize(exit), Vector2.Normalize(candidate.ExitDirection!.Value)))
                         .FirstOrDefault();
            }

            target ??= waysBack.FirstOrDefault();

            if (target == null)
                return null;

            // Stepped clear of the way back, so the player is not standing in it: otherwise turning round
            // would not be a choice, it would be the only thing that could happen.
            var back = target as WorldPassage;
            float clearance = (back?.TriggerRadius ?? 0) + CombatRules.PLAYER_HALF_SIZE + 12;

            // Out of the doorway the way it faces, when it has a facing: through a door pointing east you
            // come out west of it. A ring on the floor has no direction, so step towards the middle of the
            // room instead — which is at least always inside it.
            Vector2 inwards = back?.ExitDirection is Vector2 doorway && doorway.LengthSquared > 0
                ? -Vector2.Normalize(doorway)
                : -target.Position;

            inwards = inwards.LengthSquared > 1 ? Vector2.Normalize(inwards) : new Vector2(0, 1);

            return target.Position + inwards * clearance;
        }

        private void announcePassage(string destination)
        {
            announce(destination);
            passageFlash.ClearTransforms();
            passageFlash.FadeTo(0.72f, 100).Then().FadeOut(350, Easing.OutQuint);
        }

        /// <summary>
        /// Shows a centred message that fades on its own after <paramref name="hold"/> milliseconds.
        /// </summary>
        /// <remarks>
        /// Long enough to be read, since some of these announcements explain why something the player
        /// expected did not happen.
        /// </remarks>
        private void announce(string message, double hold = announcement_hold)
        {
            passageText.Text = message.ToUpperInvariant();
            passageText.ClearTransforms();
            passageText.FadeIn(120).Delay(hold).FadeOut(400, Easing.OutQuint);
        }

        private const double announcement_hold = 1800;

        /// <summary>
        /// For announcements carrying a full sentence rather than a room name.
        /// </summary>
        private const double long_announcement_hold = 3200;

        /// <summary>
        /// Death returns the player to the starting arena rather than to wherever they died, and is the
        /// only thing that makes them whole again.
        /// </summary>
        /// <remarks>
        /// Walking between rooms used to heal, because loading a room reset the player. Health belongs to
        /// the player rather than to the room, so a doorway is not a bandage; a room the server runs says
        /// so too, and its answer arrives through <see cref="onServerHealth"/> either way.
        /// </remarks>
        private void onPlayerDied()
        {
            passageFlash.ClearTransforms();
            passageFlash.FadeTo(0.55f, 80).Then().FadeOut(450, Easing.OutQuint);

            if (currentRoomId == initialRoomId
                || !rooms.TryGetValue(initialRoomId, out RoomDefinition? home))
            {
                // Already home, or the world has no starting arena to return to.
                reloadLayout();
                simulation.PlacePlayer(currentRoomSpawn);
                healLocalPlayer();
                return;
            }

            announcePassage(home.Name);
            switchRoom(initialRoomId);
            healLocalPlayer();
        }

        /// <summary>
        /// The entrance of the room being stood in, for sending somebody back to it.
        /// </summary>
        private Vector2 currentRoomSpawn =>
            rooms.TryGetValue(currentRoomId, out RoomDefinition? room) ? room.Spawn : DodgeWorldDefaults.PLAYER_SPAWN;

        private void healLocalPlayer()
        {
            localPlayerState.HealFull();
            hud.SetHealth(localPlayerState.Health);
            reportLocalState();
        }

        /// <summary>
        /// The server decided the local player's health. Applied rather than reconciled: the server's
        /// answer is the only one that counts, and arguing with it would show two different numbers.
        /// </summary>
        private void onServerHealth(int health)
        {
            // In a room the server runs, the hit is resolved there and never passes through the local
            // simulation, so the drop in this number is the only word that it happened.
            bool hurt = health < localPlayerState.Health;

            localPlayerState.SetHealth(health);
            hud.SetHealth(health);

            if (hurt)
                hurtSound.Play();
        }

        /// <summary>
        /// A mob the client killed itself, which happens only in a room the realtime server is not
        /// running. Those kills pay nothing — the only thing that can grant a reward is the server that
        /// saw the kill — so the player is told once rather than left to wonder.
        /// </summary>
        private void onLocalKill()
        {
            if (editorMode || realtime == null || warnedRoomIsUnrewarded)
                return;

            warnedRoomIsUnrewarded = true;
            announce("КОМНАТА НЕ НА СЕРВЕРЕ: НАГРАДЫ НЕ НАЧИСЛЯЮТСЯ", long_announcement_hold);
        }

        private bool warnedRoomIsUnrewarded;

        /// <summary>
        /// A kill of this player's was rewarded, or refused. The numbers are the server's; nothing here
        /// works out what a kill is worth.
        /// </summary>
        private void onServerReward(DodgeWorldReward reward)
        {
            session.ApplyProgression(reward.Level, reward.TotalExperience, reward.ExperienceForNextLevel,
                reward.TotalCoins);

            hud.FlashProgression(reward.Granted ? colours.Green1 : colours.Orange1);

            // A refusal on the zone's level cap is worth a line on screen: a colour flash alone reads as
            // the setting having no effect. A cooldown refusal is not — it means a kill landed a moment
            // after another in the same zone, which is normal and not the player's business.
            if (reward.Reason == "maximum_farm_level")
                announce("НАГРАДА НЕ НАЧИСЛЕНА: ПРЕВЫШЕН УРОВЕНЬ ЗОНЫ", long_announcement_hold);
        }

        #endregion

        #region input

        /// <summary>
        /// Escape and the footer's back button both arrive here.
        /// </summary>
        /// <remarks>
        /// Escape is the natural key for closing a dialogue or leaving the editor, and the world is
        /// easy to fall out of by accident while reaching for it. So back closes whatever is open
        /// first, and once there is nothing left to close it asks before leaving the world.
        /// </remarks>
        /// <returns><c>true</c> when the press was used here and must not exit the screen.</returns>
        public override bool OnBackButton()
        {
            if (activeNpc?.IsDialogueOpen == true)
            {
                activeNpc.CloseDialogue();
                return true;
            }

            if (warpMenu.IsOpen)
            {
                warpMenu.Close();
                return true;
            }

            if (texturePicker.IsOpen)
            {
                texturePicker.Close();
                return true;
            }

            if (worldMap.IsOpen)
            {
                worldMap.Close();
                return true;
            }

            if (editorMode)
            {
                if (selectedEntity != null)
                {
                    setSelection(null);
                    editorPanel.SetStatus("Выделение снято. Ещё раз — выход из редактора.");
                    return true;
                }

                setEditorMode(false);
                return true;
            }

            if (Time.Current < exitConfirmationDeadline)
                return false;

            exitConfirmationDeadline = Time.Current + exit_confirmation_window;
            announce("НАЖМИ ЕЩЁ РАЗ, ЧТОБЫ ПОКИНУТЬ МИР", long_announcement_hold);
            return true;
        }

        private double exitConfirmationDeadline;

        private const double exit_confirmation_window = 3000;

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key == Key.F2 && !e.Repeat)
            {
                if (session.CanEdit.Value)
                    setEditorMode(!editorMode);
                else
                    editorPanel.SetStatus("У этого аккаунта нет доступа к редактору мира.");

                return true;
            }

            // Before the editor's own keys, so that the map can be opened while editing too.
            if (e.Key == Key.M && !e.Repeat && !e.ControlPressed && !chat.InputFocused && activeNpc?.IsDialogueOpen != true)
            {
                ToggleWorldMap();
                return true;
            }

            if (editorMode && handleEditorKey(e))
                return true;

            if (e.Key == Key.E && !e.Repeat && !editorMode && !chat.InputFocused)
            {
                advanceOrOpenDialogue();
                return true;
            }

            if (e.Key == Key.Enter && !e.Repeat && !editorMode && activeNpc?.IsDialogueOpen != true)
            {
                chat.FocusInput();
                return true;
            }

            // Held keys are collected in the editor too, where they pan the camera instead of walking, and
            // during a conversation, which the player is allowed to walk out of. A modifier means the key
            // belongs to a shortcut rather than to walking — Alt with the arrows is the volume.
            if (isMovementKey(e.Key) && !chat.InputFocused && !e.AltPressed && !e.ControlPressed && !e.SuperPressed)
            {
                pressedKeys.Add(e.Key);
                return true;
            }

            return base.OnKeyDown(e);
        }

        private void advanceOrOpenDialogue()
        {
            if (activeNpc?.IsDialogueOpen == true)
            {
                activeNpc.AdvanceDialogue();
                return;
            }

            if (warpMenu.IsOpen)
            {
                warpMenu.Close();
                return;
            }

            // The map opened from a warp closes the same way the list it replaced did, rather than only
            // on the key that opens the map proper.
            if (worldMap.IsTravelling)
            {
                worldMap.Close();
                return;
            }

            InteractiveNpc? nearest = nearestNpc();

            if (nearest != null && inInteractionRange(nearest))
            {
                // Towards the middle of the room, which is the side the camera can actually pan far
                // enough to show: a bubble against the near wall would be half off the screen.
                nearest.SetDialogueSide(nearest.Position.X <= 0);
                nearest.BeginDialogue();
                activeNpc = nearest;
                return;
            }

            WorldWarp? warp = nearestWarp();

            if (warp != null && inInteractionRange(warp))
                useWarp(warp);
        }

        /// <summary>
        /// Opens a warp the player is standing on, or shows where they can travel from it.
        /// </summary>
        private void useWarp(WorldWarp warp)
        {
            if (!session.IsWarpUnlocked(currentRoomId, warp.EntityId))
            {
                unlockWarp(warp);
                return;
            }

            openWarpMenu(warp);
        }

        private void unlockWarp(WorldWarp warp)
        {
            announce(warp.UnlockCost > 0 ? $"ОТКРЫТИЕ ВАРПА: {warp.UnlockCost} МОНЕТ…" : "ОТКРЫТИЕ ВАРПА…");

            session.UnlockWarp(currentRoomId, warp.EntityId, outcome =>
            {
                switch (outcome)
                {
                    case WarpOutcome.Paid:
                    case WarpOutcome.AlreadyUnlocked:
                        warp.SetUnlocked(true);
                        warp.Pulse();
                        announce($"ВАРП «{warp.DisplayName}» ОТКРЫТ");
                        break;

                    case WarpOutcome.NotEnoughCoins:
                        announce($"НЕ ХВАТАЕТ МОНЕТ: НУЖНО {warp.UnlockCost}", long_announcement_hold);
                        break;

                    default:
                        announce("ВАРП НЕДОСТУПЕН", long_announcement_hold);
                        break;
                }
            });
        }

        /// <summary>
        /// Shows where a warp can send the player, on the map of the world.
        /// </summary>
        /// <remarks>
        /// A list of room names says nothing about where those rooms are. The same map that shows the shape
        /// of the world is a better picker for a network of destinations, and it is the map the author lays
        /// out by hand. The list survives for the one case a map cannot answer: which warp, when a room
        /// holds several.
        /// </remarks>
        private void openWarpMenu(WorldWarp warp)
        {
            // The live room is captured first, so warps just placed or repriced in the editor are offered.
            rooms[currentRoomId] = captureCurrentRoom();

            worldMap.OpenForTravel(WorldGraph.Describe(rooms.Values, initialRoomId), currentRoomId,
                reachableWarps(warp), session.Progression.Value?.Coins);
        }

        /// <summary>
        /// The warps this player has opened, minus the one being stood on.
        /// </summary>
        private WarpPoint[] reachableWarps(WorldWarp standingOn) => WarpCatalogue.Describe(rooms.Values)
            .Where(point => session.IsWarpUnlocked(point.RoomId, point.EntityId))
            .Where(point => point.RoomId != currentRoomId || point.EntityId != standingOn.EntityId)
            .ToArray();

        /// <summary>
        /// Asks which warp of a room, for the rooms that hold more than one the player has opened.
        /// </summary>
        private void chooseWarpInRoom(string roomId)
        {
            WorldWarp? standingOn = entities.OfType<WorldWarp>()
                                            .FirstOrDefault(candidate => inInteractionRange(candidate));

            if (standingOn == null)
                return;

            worldMap.Close();

            WarpPoint[] here = reachableWarps(standingOn).Where(point => point.RoomId == roomId).ToArray();

            warpMenu.Open(here, _ => true, session.Progression.Value?.Coins,
                new WarpPoint(currentRoomId, currentRoomName, standingOn.EntityId, standingOn.DisplayName,
                    standingOn.Position, standingOn.UnlockCost, standingOn.TravelCost));
        }

        private void travelTo(WarpPoint destination)
        {
            warpMenu.Close();
            worldMap.Close();

            if (!rooms.ContainsKey(destination.RoomId))
            {
                announce("КОМНАТА ВАРПА НЕ НАЙДЕНА", long_announcement_hold);
                return;
            }

            session.TravelToWarp(destination.RoomId, destination.EntityId, outcome =>
            {
                switch (outcome)
                {
                    case WarpOutcome.Paid:
                    case WarpOutcome.AlreadyUnlocked:
                        arriveAt(destination);
                        break;

                    case WarpOutcome.NotEnoughCoins:
                        announce($"НЕ ХВАТАЕТ МОНЕТ: НУЖНО {destination.TravelCost}", long_announcement_hold);
                        break;

                    case WarpOutcome.NotUnlocked:
                        announce("ЭТОТ ВАРП ЕЩЁ НЕ ОТКРЫТ", long_announcement_hold);
                        break;

                    default:
                        announce("ПЕРЕХОД НЕ УДАЛСЯ", long_announcement_hold);
                        break;
                }
            });
        }

        /// <summary>
        /// Puts the player at a warp, rather than at the room's entrance.
        /// </summary>
        private void arriveAt(WarpPoint destination)
        {
            announcePassage(destination.RoomName);

            if (destination.RoomId != currentRoomId)
                switchRoom(destination.RoomId);

            // After the room loads, the player stands at its spawn point, so this has to come second.
            simulation.PlacePlayer(destination.Position);
            cameraPosition = destination.Position;

            WorldWarp? arrival = entities.OfType<WorldWarp>().FirstOrDefault(warp => warp.EntityId == destination.EntityId);
            arrival?.Pulse();
        }

        private bool handleEditorKey(KeyDownEvent e)
        {
            switch (e.Key)
            {
                case Key.S when e.ControlPressed:
                    PublishWorld();
                    return true;

                case Key.Z when e.ControlPressed:
                    Undo();
                    return true;

                case Key.C when e.ControlPressed:
                    CopySelected();
                    return true;

                case Key.V when e.ControlPressed:
                    PasteCopied();
                    return true;

                case Key.D when e.ControlPressed:
                    DuplicateSelected();
                    return true;

                case Key.BracketRight when e.ControlPressed:
                    BringSelectionToFront();
                    return true;

                case Key.BracketLeft when e.ControlPressed:
                    SendSelectionToBack();
                    return true;

                case Key.Delete:
                    DeleteSelected();
                    return true;

                case Key.F when selectedEntity != null:
                    cameraPosition = selectedEntity.Position;
                    editorPanel.SetStatus("Камера наведена на выбранный объект.");
                    return true;

                // Not with Alt held: those four are the volume, and the editor has no business taking the
                // game's own shortcuts away from the author.
                case Key.Left when !e.AltPressed:
                case Key.Right when !e.AltPressed:
                case Key.Up when !e.AltPressed:
                case Key.Down when !e.AltPressed:
                    nudge(e.Key, e.ShiftPressed);
                    return true;

                default:
                    return false;
            }
        }

        private void nudge(Key key, bool fine)
        {
            Vector2 direction = key switch
            {
                Key.Left => new Vector2(-1, 0),
                Key.Right => new Vector2(1, 0),
                Key.Up => new Vector2(0, -1),
                _ => new Vector2(0, 1),
            };

            float step = fine ? 1 : CombatRules.GRID_SIZE;

            if (selectedEntity != null)
            {
                selectedEntity.Position += direction * step;
                reloadLayout();
            }
            else
                cameraPosition += direction * step * 4;
        }

        protected override bool OnScroll(ScrollEvent e)
        {
            // Alt with the wheel is the game's volume, wherever the cursor happens to be.
            if (!editorMode || e.AltPressed || !worldViewport.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
                return base.OnScroll(e);

            float direction = Math.Sign(e.ScrollDelta.Y + e.ScrollDelta.X);

            if (direction == 0)
                return true;

            float previousZoom = editorZoom;
            editorZoom = Math.Clamp(editorZoom * (direction > 0 ? 1.12f : 1 / 1.12f), 0.45f, 2.25f);

            // Keeps the point under the cursor fixed while zooming.
            Vector2 cursorFromCentre = worldViewport.ToLocalSpace(e.ScreenSpaceMousePosition) - worldViewport.ChildSize / 2;
            cameraPosition += cursorFromCentre / previousZoom - cursorFromCentre / editorZoom;

            worldCamera.ScaleTo(editorZoom, 160, Easing.OutQuint);
            editorPanel.SetZoomDisplay(editorZoom);
            return true;
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Left && !editorMode && worldLoaded && activeNpc == null && !chat.InputFocused
                && worldViewport.ReceivePositionalInputAt(e.ScreenSpaceMousePosition)
                && !chat.ReceivePositionalInputAt(e.ScreenSpaceMousePosition))
            {
                pendingAttack = e.ScreenSpaceMousePosition - localPlayer.ScreenSpaceDrawQuad.Centre;
                return true;
            }

            return base.OnMouseDown(e);
        }

        /// <summary>
        /// Drags the view around while editing.
        /// </summary>
        /// <remarks>
        /// Three ways in, because each is somebody's habit: the right button, the middle button, and
        /// Space with the left. The right button is free — objects only answer to the left one — and it is
        /// the one that needs no second hand.
        /// </remarks>
        protected override bool OnDragStart(DragStartEvent e)
        {
            bool spaceDrag = e.Button == MouseButton.Left && e.CurrentState.Keyboard.Keys.IsPressed(Key.Space);

            if (!editorMode || (e.Button != MouseButton.Middle && e.Button != MouseButton.Right && !spaceDrag))
                return base.OnDragStart(e);

            editorPanStartMouse = e.ScreenSpaceMousePosition;
            editorPanStartCamera = cameraPosition;
            return true;
        }

        protected override void OnDrag(DragEvent e)
        {
            base.OnDrag(e);
            cameraPosition = editorPanStartCamera - (e.ScreenSpaceMousePosition - editorPanStartMouse) / Math.Max(editorZoom, 0.01f);
        }

        protected override void OnKeyUp(KeyUpEvent e)
        {
            pressedKeys.Remove(e.Key);
            base.OnKeyUp(e);
        }

        private static bool isMovementKey(Key key) =>
            key is Key.W or Key.A or Key.S or Key.D or Key.Up or Key.Down or Key.Left or Key.Right;

        private string dialogueLanguage() => api.Language == Language.ru ? "ru" : "en";

        #endregion

        #region world map

        /// <summary>
        /// Shows the map of rooms, or closes it if it is already showing.
        /// </summary>
        public void ToggleWorldMap()
        {
            if (worldMap.IsOpen)
            {
                worldMap.Close();
                return;
            }

            // The live room is captured first, so a passage just linked in the editor is already a line
            // on the map rather than appearing only after the world is saved.
            rooms[currentRoomId] = captureCurrentRoom();

            worldMap.Open(WorldGraph.Describe(rooms.Values, initialRoomId), currentRoomId, editorMode);
        }

        private void openRoomFromMap(string roomId)
        {
            // Only the author moves between rooms without walking; in play the map is a map.
            if (!editorMode || roomId == currentRoomId)
                return;

            worldMap.Close();
            switchRoom(roomId);
        }

        /// <summary>
        /// Records where a room was dragged to on the map.
        /// </summary>
        /// <remarks>
        /// Not part of the undo history: that is a stack of snapshots of the room being edited, and a
        /// drag here changes a different room's definition. Dragging the room back is what undoes it.
        /// </remarks>
        private void placeRoomOnMap(string roomId, Vector2 position)
        {
            if (!editorMode || !rooms.TryGetValue(roomId, out RoomDefinition? room))
                return;

            room.MapX = position.X;
            room.MapY = position.Y;
            editorPanel.SetStatus($"Комната «{room.Name}» размещена на карте. Сохрани мир, чтобы запомнить.");
        }

        #endregion

        #region editor

        private void setEditorMode(bool enabled)
        {
            editorMode = enabled;
            pressedKeys.Clear();

            if (enabled)
            {
                foreach (InteractiveNpc npc in entities.OfType<InteractiveNpc>())
                    npc.CloseDialogue();

                activeNpc = null;
                chat.ReleaseFocus();
                editorPanel.SetZoomDisplay(editorZoom);
            }
            else
            {
                editorZoom = 1;
                worldCamera.ScaleTo(1, 180, Easing.OutQuint);
            }

            editorPanel.FadeTo(enabled ? 1 : 0, 220, Easing.OutQuint);

            // Keep the world clear of the panel, so an object's right-hand edge and its resize
            // handles stay reachable instead of sitting underneath it.
            worldViewport.Padding = worldViewport.Padding with
            {
                Right = enabled ? DodgeWorldEditorPanel.PANEL_WIDTH : 0,
            };

            foreach (EditableWorldEntity entity in entities)
                entity.SetEditing(enabled);

            refreshArrivalMarkers();
            simulationView.SetDimmed(enabled);
            remoteCombat?.SetDimmed(enabled);

            if (!enabled)
                setSelection(null);

            // Mobs only exist outside the editor, so the layout has to be rebuilt either way.
            reloadLayout();

            // The editor shows everything, including what the player's story hides. Closing it has to put
            // the room back to what this player would actually be looking at.
            if (!enabled)
                refreshStoryVisibility();
        }

        private void selectEntity(EditableWorldEntity entity)
        {
            if (!editorMode)
                return;

            setSelection(entity);
        }

        /// <summary>
        /// Answers a click on an object: the one on top, or the next one down when the same spot is
        /// clicked again.
        /// </summary>
        /// <remarks>
        /// This is how every editor with overlapping objects behaves, and without it an object under
        /// another one could not be reached at all — the one in front always won the click.
        /// </remarks>
        private void entityClicked(EditableWorldEntity clicked, Vector2 screenSpacePosition)
        {
            if (!editorMode)
                return;

            bool sameSpot = (screenSpacePosition - lastClickPosition).Length < same_click_distance;
            lastClickPosition = screenSpacePosition;

            setSelection(sameSpot ? nextUnderCursor(screenSpacePosition) ?? clicked : clicked);
        }

        /// <summary>
        /// The object one step behind the current selection, among everything under the cursor. Wraps back
        /// to the front, so clicking repeatedly walks the whole stack rather than stopping at the bottom.
        /// </summary>
        private EditableWorldEntity? nextUnderCursor(Vector2 screenSpacePosition)
        {
            // Front to back, which is the order the eye reads them in: lower depth is drawn later.
            EditableWorldEntity[] stack = entities
                                          .Where(entity => entity.ReceivePositionalInputAt(screenSpacePosition))
                                          .OrderBy(entity => entity.Depth)
                                          .ToArray();

            if (stack.Length == 0)
                return null;

            int current = selectedEntity == null ? -1 : Array.IndexOf(stack, selectedEntity);

            return stack[(current + 1) % stack.Length];
        }

        /// <summary>Where the last click landed, for recognising a second click on the same object.</summary>
        private Vector2 lastClickPosition = new Vector2(float.MinValue);

        /// <summary>
        /// How far a second click may land from the first and still count as the same spot. Loose enough
        /// for a hand that does not hold still, tight enough that a click elsewhere is a fresh choice.
        /// </summary>
        private const float same_click_distance = 6;

        private void setSelection(EditableWorldEntity? entity)
        {
            if (!ReferenceEquals(selectedEntity, entity))
                selectedEntity?.SetSelected(false);

            selectedEntity = entity;
            entity?.SetSelected(true);

            editorPanel.SetSelection(entity);
            editorPanel.SetMaterialLabels(entity as ITexturedEntity);

            // A passage explains itself on selection: where it leads, and which doorway it will deliver
            // somebody to.
            if (editorMode && entity is WorldPassage passage)
                editorPanel.SetStatus(describePassage(passage));
        }

        public void OnSelectionEdited()
        {
            reloadLayout();
            editorPanel.SetStatus(selectedEntity == null ? "Изменения применены." : $"«{selectedEntity.DisplayName}» обновлён.");
        }

        public void ApplyRoomProperties(string name, string width, string height)
        {
            pushUndo();

            if (!string.IsNullOrWhiteSpace(name))
            {
                currentRoomName = name.Trim();
                hud.SetRoom(currentRoomName);
            }

            Vector2 previousSize = currentRoomSize;

            applyRoomSize(new Vector2(
                EditorValue.Float(width, currentRoomSize.X, DodgeWorldDefaults.MINIMUM_MAP_SIZE.X, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.X),
                EditorValue.Float(height, currentRoomSize.Y, DodgeWorldDefaults.MINIMUM_MAP_SIZE.Y, DodgeWorldDefaults.MAXIMUM_MAP_SIZE.Y)));

            anchorContentsAfterResize(previousSize);

            rooms[currentRoomId] = captureCurrentRoom();
            editorPanel.SetRoom(currentRoomName, currentRoomSize);
            editorPanel.SetStatus($"Комната: {currentRoomName}, {currentRoomSize.X:0} × {currentRoomSize.Y:0}.");
            reloadLayout();
        }

        public void SetSpawnAtPlayer()
        {
            if (!rooms.TryGetValue(currentRoomId, out RoomDefinition? room))
                return;

            room.SpawnX = localPlayerState.Position.X;
            room.SpawnY = localPlayerState.Position.Y;
            editorPanel.SetStatus("Точка входа перенесена под игрока.");
        }

        public void AddSurface() => addNewEntity(EntityKinds.SURFACE, $"{SurfaceKind.CUSTOM_ID_PREFIX}-", "Room panel",
            "Панель комнаты добавлена. Перетащи её и измени размер маркерами.");

        public void AddCollisionZone() => addNewEntity(EntityKinds.COLLISION, "collision-custom-", "Collision zone",
            "Зона коллизии добавлена. Она видна только в редакторе.");

        public void AddNpc() => addNewEntity(EntityKinds.NPC, "npc-custom-", "NPC",
            "NPC добавлен. Открой вкладку «Диалог» и заполни реплики.");

        public void AddMobSpawnZone() => addNewEntity(EntityKinds.MOB_SPAWN, "mob-spawn-", "Mob spawn",
            "Зона появления мобов добавлена в текущую комнату.");

        public void AddEmitter() => addNewEntity(EntityKinds.EMITTER, "emitter-", "Emitter",
            "Эмиттер добавлен. Направление, цикл и сдвиг фазы — в свойствах.");

        public void AddBeam() => addNewEntity(EntityKinds.BEAM, "beam-", "Beam",
            "Луч добавлен. Длину, цикл и сдвиг фазы — в свойствах.");

        public void AddWarp() => addNewEntity(EntityKinds.WARP, "warp-", "Warp",
            "Варп добавлен. Задай цену открытия и цену перехода во вкладке «Объект»; 0 — бесплатно.");

        public void AddPortal() => addNewEntity(EntityKinds.PORTAL, "portal-", "Portal",
            "Портал добавлен и пока никуда не ведёт. Впиши ID комнаты во вкладке «Объект»"
            + " или нажми «Создать комнату из выбранного прохода».");

        public void AddSidePassage() => addNewEntity(EntityKinds.PASSAGE, "passage-", "Passage",
            "Проход добавлен и пока никуда не ведёт. Впиши ID комнаты во вкладке «Объект»"
            + " или нажми «Создать комнату из выбранного прохода».");

        public void AddTerminal() => addNewEntity(EntityKinds.TERMINAL, "quest-board-", "Quest board",
            "Доска заданий добавлена. Вид киоска меняется кнопкой «Сменить вид» — их четыре.",
            record => record.Style = TerminalVariants.QUEST_BOARD);

        public void AddShop() => addNewEntity(EntityKinds.TERMINAL, "shop-", "Shop",
            "Магазин добавлен. Вид киоска меняется кнопкой «Сменить вид» — их четыре.",
            record => record.Style = TerminalVariants.SHOP);

        /// <summary>
        /// Puts a new object of <paramref name="kindName"/> in the middle of the view.
        /// </summary>
        /// <param name="kindName">The entity kind name.</param>
        /// <param name="idPrefix">The identifier prefix for the new entity.</param>
        /// <param name="namePrefix">The display name prefix for the new entity.</param>
        /// <param name="status">The status message to display.</param>
        /// <param name="configure">
        /// Applied to the record before the object is built, for a button that means one particular variant
        /// of a kind rather than the kind's default.
        /// </param>
        private void addNewEntity(string kindName, string idPrefix, string namePrefix, string status,
                                  Action<EntityRecord>? configure = null)
        {
            IWorldEntityKind? kind = registry.Find(kindName);

            if (kind == null)
                return;

            pushUndo();
            customEntityCounter++;

            EntityRecord record = kind.CreateRecord($"{idPrefix}{customEntityCounter}", $"{namePrefix} {customEntityCounter}");
            Vector2 position = CombatRules.SnapToGrid(cameraPosition);
            record.X = position.X;
            record.Y = position.Y;
            configure?.Invoke(record);

            EditableWorldEntity entity = kind.Create(record, entityContext);
            addEntity(entity);
            setSelection(entity);
            reloadLayout();
            editorPanel.SetStatus(status);
        }

        /// <summary>
        /// The object on the clipboard, as a record rather than as a drawable.
        /// </summary>
        /// <remarks>
        /// A record, so a copy survives leaving the room it was taken from: an obstacle course is built by
        /// making one device behave and then repeating it, and rooms are where the repetition happens.
        /// </remarks>
        private EntityRecord? clipboard;

        public void CopySelected()
        {
            if (selectedEntity == null)
            {
                editorPanel.SetStatus("Сначала выбери объект.");
                return;
            }

            clipboard = registry.Capture(selectedEntity);

            if (clipboard == null)
            {
                editorPanel.SetStatus("Этот объект скопировать нельзя.");
                return;
            }

            editorPanel.SetStatus($"«{selectedEntity.DisplayName}» скопирован. Ctrl+V — вставить, в том числе в другой комнате.");
        }

        /// <summary>
        /// Puts the clipboard down at the middle of the view.
        /// </summary>
        public void PasteCopied()
        {
            if (clipboard == null)
            {
                editorPanel.SetStatus("Буфер пуст. Скопируй объект через Ctrl+C.");
                return;
            }

            placeCopy(clipboard, CombatRules.SnapToGrid(cameraPosition), "вставлен");
        }

        /// <summary>
        /// Copies the selection next to itself, which is the quick way to lay out a row of devices.
        /// </summary>
        public void DuplicateSelected()
        {
            if (selectedEntity == null)
            {
                editorPanel.SetStatus("Сначала выбери объект.");
                return;
            }

            EntityRecord? source = registry.Capture(selectedEntity);

            if (source == null)
            {
                editorPanel.SetStatus("Этот объект скопировать нельзя.");
                return;
            }

            // Offset by a grid step, so the copy is visibly its own object and can be dragged off the
            // original rather than hiding underneath it.
            placeCopy(source, CombatRules.SnapToGrid(selectedEntity.Position + new Vector2(CombatRules.GRID_SIZE * 2)),
                "продублирован");
        }

        private void placeCopy(EntityRecord source, Vector2 position, string verb)
        {
            IWorldEntityKind? kind = registry.Find(source.Kind);

            if (kind == null)
            {
                editorPanel.SetStatus("Такого типа объекта здесь нет.");
                return;
            }

            pushUndo();

            EntityRecord copy = DodgeWorldSerializer.Clone(source);
            copy.Id = uniqueEntityId(source.Kind);
            copy.X = position.X;
            copy.Y = position.Y;

            EditableWorldEntity entity = kind.Create(copy, entityContext);
            addEntity(entity);
            setSelection(entity);
            reloadLayout();
            editorPanel.SetStatus($"«{entity.DisplayName}» {verb}.");
        }

        /// <summary>
        /// An id no object in the room is using. Ids identify a warp's price and a reward claim, so two
        /// objects sharing one would share those too.
        /// </summary>
        private string uniqueEntityId(string kind)
        {
            string candidate;

            do
                candidate = $"{kind}-copy-{++customEntityCounter}";
            while (entities.Any(entity => entity.EntityId == candidate) || hiddenRecords.Any(record => record.Id == candidate));

            return candidate;
        }

        /// <summary>
        /// Moves the selection to the front of its layer, and to the front of the room's list with it.
        /// </summary>
        public void BringSelectionToFront() => reorderSelection(toFront: true);

        public void SendSelectionToBack() => reorderSelection(toFront: false);

        private void reorderSelection(bool toFront)
        {
            if (selectedEntity == null)
            {
                editorPanel.SetStatus("Сначала выбери объект.");
                return;
            }

            pushUndo();

            EditableWorldEntity moved = selectedEntity;
            entities.Remove(moved);

            if (toFront)
                entities.Add(moved);
            else
                entities.Insert(0, moved);

            // The depths are recomputed from the list every frame, so moving it here is the whole change.
            editorPanel.SetStatus(toFront
                ? $"«{moved.DisplayName}» — на передний план."
                : $"«{moved.DisplayName}» — на задний план.");
        }

        public void CreateConnectedRoom()
        {
            if (selectedEntity is not WorldPassage passage)
            {
                editorPanel.SetStatus("Сначала выбери портал или проход для новой комнаты.");
                return;
            }

            if (!string.IsNullOrEmpty(passage.DestinationRoomId))
            {
                editorPanel.SetStatus("Этот проход уже связан с комнатой.");
                return;
            }

            string newRoomId;

            do
                newRoomId = $"room-{++roomCounter}";
            while (rooms.ContainsKey(newRoomId));

            string newRoomName = $"Room {roomCounter}";

            passage.DestinationRoomId = newRoomId;
            passage.Destination = newRoomName;

            rooms[newRoomId] = DefaultWorld.CreateConnectedRoom(newRoomId, newRoomName, currentRoomId, currentRoomName);

            // Paired straight away, so the corridor the author just made comes out at its own far end
            // rather than at whichever way back happens to be found first.
            pairWithBackPassage(passage, newRoomId);

            rooms[currentRoomId] = captureCurrentRoom();

            switchRoom(newRoomId);
            PublishWorld();
        }

        /// <summary>
        /// Asks which room the selected passage leads to, on the map.
        /// </summary>
        public void LinkSelectedPassage()
        {
            if (selectedEntity is not WorldPassage passage)
            {
                editorPanel.SetStatus("Сначала выбери портал или проход.");
                return;
            }

            // Captured first, so a passage created a moment ago is already part of the room the map draws.
            rooms[currentRoomId] = captureCurrentRoom();
            worldMap.OpenForLinking(WorldGraph.Describe(rooms.Values, initialRoomId), currentRoomId,
                passage.DisplayName);
        }

        /// <summary>
        /// Points the selected passage at the room the author came from, which is the link they almost
        /// always want and the one the map cannot make obvious.
        /// </summary>
        public void LinkSelectedPassageToPreviousRoom()
        {
            if (previousRoomId == null || !rooms.ContainsKey(previousRoomId))
            {
                editorPanel.SetStatus("Предыдущей комнаты нет — ты ещё никуда не переходил.");
                return;
            }

            linkSelectedPassageTo(previousRoomId);
        }

        private void linkSelectedPassageTo(string roomId)
        {
            if (selectedEntity is not WorldPassage passage)
            {
                editorPanel.SetStatus("Сначала выбери портал или проход.");
                return;
            }

            if (!rooms.TryGetValue(roomId, out RoomDefinition? room))
            {
                editorPanel.SetStatus($"Комната с ID «{roomId}» не существует.");
                return;
            }

            pushUndo();
            worldMap.Close();

            passage.DestinationRoomId = roomId;

            // The caption is what the player reads on the way through, so it follows the room unless the
            // author has already written something of their own.
            if (string.IsNullOrEmpty(passage.Destination) || passage.Destination == WorldPassage.UNLINKED_DESTINATION)
                passage.Destination = room.Name;

            string pairing = pairWithBackPassage(passage, roomId);

            rooms[currentRoomId] = captureCurrentRoom();
            refreshArrivalMarkers();
            editorPanel.SetSelection(passage);
            editorPanel.SetStatus($"«{passage.DisplayName}» ведёт в «{room.Name}». {pairing}");
        }

        /// <summary>
        /// Ties a passage to the one that leads back, so the pair knows about each other.
        /// </summary>
        /// <remarks>
        /// This is what makes a corridor come out where it should. Without a pairing, arriving fell back to
        /// «whichever passage here leads back to that room» — and with two corridors between the same two
        /// rooms, whichever means the first one found, so walking through the second one put the player at
        /// the first. A pair is decided once, when the author links them, rather than guessed at every
        /// crossing.
        /// <para>
        /// The candidate is the doorway in the destination that leads back here and is not already paired
        /// with something else, preferring one that faces the opposite way — that is the door on the other
        /// side of the same corridor.
        /// </para>
        /// </remarks>
        /// <returns>What happened, to be shown to the author.</returns>
        private string pairWithBackPassage(WorldPassage passage, string destinationRoomId)
        {
            if (!rooms.TryGetValue(destinationRoomId, out RoomDefinition? destination))
                return string.Empty;

            EntityRecord[] candidates = destination.Entities
                                                   .Where(record => record.DestinationRoomId == currentRoomId)
                                                   .Where(record => record.Kind == EntityKinds.PASSAGE || record.Kind == EntityKinds.PORTAL)
                                                   .ToArray();

            if (candidates.Length == 0)
            {
                return destinationRoomId == currentRoomId
                    ? "Проход ведёт в свою же комнату."
                    : $"В «{destination.Name}» нет прохода обратно: создай его там и свяжи с этой комнатой — тогда игрок будет выходить у него.";
            }

            // Already paired with this one, or free to be. A doorway spoken for by another passage is left
            // alone: that pair is somebody's corridor too.
            EntityRecord? paired = candidates.FirstOrDefault(record => record.ArrivalEntityId == passage.EntityId);

            EntityRecord? back = paired ?? candidates
                                           .Where(record => string.IsNullOrEmpty(record.ArrivalEntityId))
                                           .OrderByDescending(record => oppositeness(passage, record))
                                           .FirstOrDefault();

            if (back == null)
                return $"Проходы в «{destination.Name}» уже связаны с другими — укажи «ID объекта прибытия» вручную.";

            back.ArrivalEntityId = passage.EntityId;
            passage.ArrivalEntityId = back.Id;

            return $"Игрок выйдет у «{back.DisplayName ?? back.Id}», и оттуда вернётся сюда.";
        }

        /// <summary>
        /// How well a doorway faces the other way from <paramref name="passage"/>, from -1 to 1.
        /// </summary>
        /// <remarks>
        /// The far end of a corridor faces back the way you came: walking east out of one room means
        /// arriving at a west-facing door in the next. Anything without a side scores zero, which leaves it
        /// as a last resort rather than a wrong answer.
        /// </remarks>
        private static float oppositeness(WorldPassage passage, EntityRecord candidate)
        {
            if (passage.ExitDirection is not Vector2 exit || exitDirectionOf(candidate) is not Vector2 direction)
                return 0;

            return -Vector2.Dot(Vector2.Normalize(exit), direction);
        }

        /// <summary>
        /// Which way a stored doorway faces, by the same rule the live one uses, or null for anything
        /// without a side.
        /// </summary>
        private static Vector2? exitDirectionOf(EntityRecord record)
        {
            if (record.Kind != EntityKinds.PASSAGE)
                return null;

            float radians = (record.Facing ?? 0) * MathF.PI / 180;
            float sign = (record.PointsRight ?? true) ? 1 : -1;

            return new Vector2(sign * MathF.Cos(radians), sign * MathF.Sin(radians));
        }

        /// <summary>
        /// Says in words where a passage leads and where the player will come out of it.
        /// </summary>
        /// <remarks>
        /// Shown as soon as a passage is selected, because none of this was visible anywhere: the author
        /// could see that a passage was linked but not which doorway it would deliver somebody to, which is
        /// the half that goes wrong.
        /// </remarks>
        private string describePassage(WorldPassage passage)
        {
            if (string.IsNullOrEmpty(passage.DestinationRoomId) || !rooms.TryGetValue(passage.DestinationRoomId, out RoomDefinition? destination))
                return $"«{passage.DisplayName}» никуда не ведёт: свяжи его с комнатой на карте.";

            string lead = $"«{passage.DisplayName}» ведёт в «{destination.Name}».";

            if (passage.ArrivalPoint is Vector2 point)
                return $"{lead} Точка выхода задана вручную: {(int)point.X}, {(int)point.Y} — отмечена крестом в той комнате.";

            EntityRecord[] waysBack = destination.Entities
                                                 .Where(record => record.DestinationRoomId == currentRoomId)
                                                 .Where(record => record.Kind == EntityKinds.PASSAGE || record.Kind == EntityKinds.PORTAL)
                                                 .ToArray();

            EntityRecord? named = passage.ArrivalEntityId == null
                ? null
                : destination.Entities.FirstOrDefault(record => record.Id == passage.ArrivalEntityId);

            if (named != null)
                return $"{lead} Игрок выйдет у «{named.DisplayName ?? named.Id}» — эти два прохода связаны в пару.";

            EntityRecord? guess = waysBack.OrderByDescending(record => oppositeness(passage, record)).FirstOrDefault();

            if (guess != null)
                return $"{lead} Обратного прохода в паре нет, поэтому игрок выйдет у «{guess.DisplayName ?? guess.Id}» — свяжи его с этой комнатой, чтобы закрепить.";

            return $"{lead} В «{destination.Name}» нет прохода обратно, поэтому игрок окажется на входе той комнаты.";
        }

        /// <summary>
        /// Says that the middle of the view is where the player should come out, and asks which room they
        /// will be coming from.
        /// </summary>
        /// <remarks>
        /// Asked from this end because this is the end that can be pointed at. The spot belongs to the
        /// passage in the other room, so this writes into that room's definition rather than this one's —
        /// which is also why it is outside the <c>Ctrl+Z</c> history, like placing a room on the map.
        /// </remarks>
        public void SetArrivalHere()
        {
            rooms[currentRoomId] = captureCurrentRoom();
            worldMap.OpenForArrival(WorldGraph.Describe(rooms.Values, initialRoomId), currentRoomId,
                currentRoomName);
        }

        private void setArrivalFromRoom(string sourceRoomId)
        {
            worldMap.Close();

            if (!rooms.TryGetValue(sourceRoomId, out RoomDefinition? source))
            {
                editorPanel.SetStatus($"Комната с ID «{sourceRoomId}» не существует.");
                return;
            }

            Vector2 arrival = CombatRules.SnapToGrid(cameraPosition);

            EntityRecord[] doors = source.Entities
                                        .Where(record => record.DestinationRoomId == currentRoomId)
                                        .ToArray();

            if (doors.Length == 0)
            {
                editorPanel.SetStatus($"В «{source.Name}» нет прохода, ведущего сюда. Сначала свяжи проход с этой комнатой.");
                return;
            }

            foreach (EntityRecord door in doors)
            {
                door.ArrivalX = arrival.X;
                door.ArrivalY = arrival.Y;
            }

            // The source room is not the one on screen, so its live entities have to be told too — the
            // author may be standing in the room they just edited.
            foreach (WorldPassage passage in entities.OfType<WorldPassage>())
            {
                if (sourceRoomId == currentRoomId && passage.DestinationRoomId == currentRoomId)
                    passage.ArrivalPoint = arrival;
            }

            refreshArrivalMarkers();

            editorPanel.SetStatus(doors.Length == 1
                ? $"Из «{source.Name}» игрок будет выходить здесь — отмечено крестом. Сохрани мир."
                : $"Из «{source.Name}» сюда ведут {doors.Length} прохода — все будут выводить в отмеченной точке. Сохрани мир.");
        }

        public void OpenLinkedRoom()
        {
            if (selectedEntity is not WorldPassage passage || string.IsNullOrEmpty(passage.DestinationRoomId))
            {
                editorPanel.SetStatus("Сначала выбери связанный портал или проход.");
                return;
            }

            switchRoom(passage.DestinationRoomId);
        }

        public void ReturnToRootRoom() => switchRoom(initialRoomId);

        /// <summary>
        /// Makes the room being stood in the one the world starts in, and the one death returns to.
        /// </summary>
        public void MakeThisRoomInitial()
        {
            if (initialRoomId == currentRoomId)
            {
                editorPanel.SetStatus($"«{currentRoomName}» и так начальная комната.");
                return;
            }

            initialRoomId = currentRoomId;
            editorPanel.SetStatus($"Мир будет начинаться в «{currentRoomName}», и сюда же будет возвращать смерть."
                                  + " Сохрани мир.");
        }

        /// <summary>
        /// Removes the room being stood in, along with the links that led to it.
        /// </summary>
        /// <remarks>
        /// Asks twice: <c>Ctrl+Z</c> keeps snapshots of one room, so it cannot bring a room back. Until the
        /// world is saved nothing is lost for good — the deletion lives in this client only.
        /// </remarks>
        public void DeleteThisRoom()
        {
            if (currentRoomId == initialRoomId)
            {
                editorPanel.SetStatus("Начальную комнату удалить нельзя: сначала сделай начальной другую.");
                return;
            }

            if (rooms.Count <= 1)
            {
                editorPanel.SetStatus("Это последняя комната мира.");
                return;
            }

            if (roomPendingDeletion != currentRoomId)
            {
                roomPendingDeletion = currentRoomId;
                editorPanel.SetStatus($"Удалить «{currentRoomName}» со всем, что в ней? Нажми ещё раз."
                                      + " Отменить через Ctrl+Z будет нельзя.");
                return;
            }

            string removed = currentRoomName;
            string removedId = currentRoomId;

            roomPendingDeletion = null;
            rooms.Remove(removedId);

            int clearedLinks = clearLinksTo(removedId);

            // Left before the room is entered, or capturing the current room would put it back.
            currentRoomId = initialRoomId;
            previousRoomId = null;
            undoHistory.Clear();
            enterRoom(rooms[initialRoomId]);

            editorPanel.SetStatus($"Комната «{removed}» удалена."
                                  + (clearedLinks > 0 ? $" Проходов, которые в неё вели: {clearedLinks} — они больше никуда не ведут." : string.Empty)
                                  + " Сохрани мир, чтобы это стало окончательным.");
        }

        /// <summary>
        /// Unpoints every passage of every other room that led to a room that no longer exists, and returns
        /// how many there were.
        /// </summary>
        private int clearLinksTo(string roomId)
        {
            int cleared = 0;

            foreach (RoomDefinition room in rooms.Values)
            {
                foreach (EntityRecord entity in room.Entities)
                {
                    if (entity.DestinationRoomId != roomId)
                        continue;

                    entity.DestinationRoomId = null;
                    entity.ArrivalEntityId = null;
                    cleared++;
                }
            }

            return cleared;
        }

        private string? roomPendingDeletion;

        private string initialRoomName =>
            rooms.TryGetValue(initialRoomId, out RoomDefinition? room) ? room.Name : DefaultWorld.ROOT_ROOM_NAME;

        public void PublishWorld()
        {
            rooms[currentRoomId] = captureCurrentRoom();

            var document = new DodgeWorldDocument
            {
                InitialRoomId = initialRoomId,
                DefaultWeaponSkinId = defaultWeaponSkinId,
                WeaponSkins = weaponSkins.Values.OrderBy(skin => skin.Id).ToList(),
                Rooms = rooms.Values.OrderBy(room => room.Id).ToList(),
            };

            editorPanel.SetStatus("Сохранение мира…");

            session.Publish(document, outcome => editorPanel.SetStatus(outcome switch
            {
                // The revision is a save counter for the whole world, not a number of rooms: one document
                // holds every room, and each save of it bumps the count by one. It is what lets the server
                // refuse a save written on top of somebody else's.
                PublishOutcome.Published =>
                    $"Мир сохранён целиком: комнат {rooms.Count}, сохранение №{session.Revision.Value}.",
                PublishOutcome.Forbidden => "Публиковать мир может только администратор Dodge World.",
                _ => "Публикация не удалась. Перезагрузи мир: возможно, его изменил другой редактор.",
            }));
        }

        public void ResetRoom()
        {
            pushUndo();

            rooms[currentRoomId] = currentRoomId == initialRoomId
                ? DefaultWorld.CreateRootRoom()
                : DefaultWorld.CreateConnectedRoom(currentRoomId, currentRoomName, initialRoomId, initialRoomName);

            enterRoom(rooms[currentRoomId]);
            editorPanel.SetStatus("Стандартная планировка восстановлена. Сохрани мир.");
        }

        public void CloseEditor() => setEditorMode(false);

        public void AdjustSpawnCount(int delta)
        {
            if (selectedEntity is not MobSpawnZone zone)
            {
                editorPanel.SetStatus("Сначала выбери зону появления мобов.");
                return;
            }

            pushUndo();
            zone.SpawnCount = Math.Clamp(zone.SpawnCount + delta, MobSpawnKind.MIN_SPAWN_COUNT, MobSpawnKind.MAX_SPAWN_COUNT);
            reloadLayout();
            editorPanel.SetStatus($"Мобов в зоне: {zone.SpawnCount}.");
        }

        public void CycleSelectedStyle()
        {
            if (selectedEntity?.CanStyle != true)
            {
                editorPanel.SetStatus("У этого объекта нет цветовых вариантов.");
                return;
            }

            pushUndo();
            selectedEntity.CycleStyle();
        }

        public void ToggleSelectedShape()
        {
            if (selectedEntity?.CanRound != true)
            {
                editorPanel.SetStatus("У этого объекта нет скругления.");
                return;
            }

            pushUndo();
            selectedEntity.ToggleShape();
            editorPanel.SetSelection(selectedEntity);
        }

        public void DeleteSelected()
        {
            if (selectedEntity == null)
            {
                editorPanel.SetStatus("Сначала выбери объект на карте.");
                return;
            }

            // Any object can be removed. The built-in ones used to be protected because code created
            // them; now that a room is a document, undo and "Сбросить текущую комнату" bring them back.
            pushUndo();
            EditableWorldEntity removed = selectedEntity;
            setSelection(null);
            entities.Remove(removed);
            worldCamera.Remove(removed, true);
            reloadLayout();
            editorPanel.SetStatus($"«{removed.DisplayName}» удалён.");
        }

        public void SetSelectedCornerRadius(float value)
        {
            if (selectedEntity is RoomSurface surface)
                surface.SetCornerRadius(value);
        }

        public void SetSelectedTextureOpacity(float value)
        {
            if (selectedEntity is ITexturedEntity target)
                target.SetTextureOpacity(value);
        }

        #endregion

        #region textures

        /// <summary>
        /// Resolves and applies an entity's texture, on behalf of the kind that built it.
        /// </summary>
        private void requestTexture(ITexturedEntity entity) => textureLibrary.LoadEntityTexture(entity, stillInRoom);

        /// <summary>
        /// Whether an entity is still part of the room, checked before a finished load is applied.
        /// </summary>
        private bool stillInRoom(ITexturedEntity entity) => entity is EditableWorldEntity drawable && entities.Contains(drawable);

        /// <summary>
        /// How the mobs of a spawn zone look, answered for the view when a mob appears.
        /// </summary>
        private MobAppearance mobAppearance(string spawnZoneId)
        {
            MobSpawnZone? zone = entities.OfType<MobSpawnZone>().FirstOrDefault(candidate => candidate.EntityId == spawnZoneId);

            return zone == null ? MobAppearance.None : new MobAppearance(zone.MobTexture, zone.TextureOpacity);
        }

        public void ImportTexture()
        {
            importPurpose = TextureImportPurpose.Surface;
            textureImportTarget = null;
            texturePicker.Open(file => importTexture(file));
        }

        /// <summary>
        /// Claims images dropped onto the window, so that adding a texture is a drag rather than a walk
        /// through a file browser.
        /// </summary>
        /// <remarks>
        /// Answered from the extension alone: this is called from the window's own thread, where reaching
        /// into the screen would not be safe, and where the file may not even be readable yet.
        /// </remarks>
        public bool ClaimsDroppedFile(string path)
        {
            try
            {
                return SupportedExtensions.IMAGE_EXTENSIONS.Contains(Path.GetExtension(path).ToLowerInvariant());
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Imports a dropped image, applying it straight to the selected object when there is one.
        /// </summary>
        /// <remarks>
        /// Dropping a picture onto a panel that is selected means "this panel looks like this"; dropping one
        /// with nothing selected only means "keep this". Both are useful, and which was meant is already
        /// said by the selection.
        /// </remarks>
        public void HandleDroppedFile(string path)
        {
            if (!session.CanEdit.Value)
            {
                announce("ТЕКСТУРЫ МОЖЕТ ДОБАВЛЯТЬ ТОЛЬКО АВТОР МИРА", long_announcement_hold);
                return;
            }

            importPurpose = TextureImportPurpose.Surface;
            textureImportTarget = editorMode ? selectedEntity as ITexturedEntity : null;

            if (textureImportTarget != null)
                pushUndo();

            importTexture(new FileInfo(path), announceResult: !editorMode);
        }

        /// <summary>
        /// Uploads an image and puts it in the library.
        /// </summary>
        /// <param name="file">The image on disk.</param>
        /// <param name="announceResult">
        /// Whether to say how it went on screen as well as in the editor panel. Set for a dropped file,
        /// which can arrive while the panel is not even visible.
        /// </param>
        private void importTexture(FileInfo file, bool announceResult = false)
        {
            string extension = Path.GetExtension(file.Name).ToLowerInvariant();

            byte[] content;

            try
            {
                content = File.ReadAllBytes(file.FullName);
            }
            catch (Exception)
            {
                report("Не удалось прочитать файл.");
                return;
            }

            void report(string message)
            {
                editorPanel.SetStatus(message);

                if (announceResult)
                    announce(message.ToUpperInvariant(), long_announcement_hold);
            }

            string contentType = extension is ".jpg" or ".jpeg" ? "image/jpeg" : "image/png";
            var upload = new TextureImport(content, contentType, extension, file.Name, importPurpose);

            editorPanel.SetStatus($"Загрузка текстуры: {file.Name}…");

            ITexturedEntity? target = textureImportTarget;

            session.StoreTexture(upload,
                stored =>
                {
                    textureLibrary.Register(stored, file.Name);
                    textureLibrary.Select(stored);
                    editorPanel.RebuildTextureBrowser();

                    if (importPurpose == TextureImportPurpose.Weapon)
                        setWeaponTexture(stored, file.Name);
                    else if (target != null && stillInRoom(target))
                    {
                        target.SetTexturePath(stored);
                        requestTexture(target);
                        editorPanel.SetMaterialLabels(target);
                        report($"Текстура применена: {file.Name}");
                        return;
                    }

                    report($"Текстура добавлена: {file.Name}");
                },
                () => report("Не удалось загрузить текстуру."));
        }

        public void ApplySelectedTextureToSurface()
        {
            if (selectedEntity is not ITexturedEntity target)
            {
                editorPanel.SetStatus("Текстуру можно применить к поверхности, NPC или зоне мобов.");
                return;
            }

            TextureLibraryEntry? entry = textureLibrary.Selected;

            if (entry == null)
            {
                editorPanel.SetStatus("Сначала выбери текстуру в библиотеке.");
                return;
            }

            if (entry.BuiltIn || string.IsNullOrWhiteSpace(entry.Path))
            {
                editorPanel.SetStatus("Стандартный меч предназначен только для оружия. Выбери другую текстуру.");
                return;
            }

            pushUndo();
            target.SetTexturePath(entry.Path);
            requestTexture(target);
            editorPanel.SetMaterialLabels(target);
            // Mobs are only built when the editor closes, so they pick the image up then.
            editorPanel.SetStatus(target is MobSpawnZone
                ? $"Текстура «{entry.Name}» применена к мобам зоны."
                : $"Текстура «{entry.Name}» применена к «{selectedEntity.DisplayName}».");
        }

        public void ApplySelectedTextureToWeapon()
        {
            TextureLibraryEntry? entry = textureLibrary.Selected;

            if (entry == null)
            {
                editorPanel.SetStatus("Сначала выбери текстуру в библиотеке.");
                return;
            }

            if (entry.BuiltIn || string.IsNullOrWhiteSpace(entry.Path))
            {
                defaultWeaponSkinId = null;
                textureLibrary.SetActiveSkin(null);
                editorPanel.SetStatus("Выбран встроенный стандартный меч.");
                return;
            }

            setWeaponTexture(entry.Path, entry.Name);
        }

        private void setWeaponTexture(string texturePath, string sourceName)
        {
            const string skin_id = "default";

            var skin = new WeaponSkin
            {
                Id = skin_id,
                DisplayName = "Default sword",
                Texture = texturePath,
            };

            weaponSkins[skin_id] = skin;
            defaultWeaponSkinId = skin_id;
            textureLibrary.SetActiveSkin(skin);
            editorPanel.SetStatus($"Текстура меча готова: {sourceName}. Сохрани мир для публикации.");
        }

        public void ClearSurfaceTexture()
        {
            if (selectedEntity is not ITexturedEntity target)
            {
                editorPanel.SetStatus("Сначала выбери объект с текстурой.");
                return;
            }

            pushUndo();
            target.SetTexturePath(null);
            target.SetTexture(null);
            editorPanel.SetMaterialLabels(target);
            editorPanel.SetStatus("Текстура удалена.");
        }

        /// <summary>
        /// Steps how the selected object's image covers it. Panels and NPCs both offer a choice; Mora and a
        /// mob zone are always fitted, and say so rather than doing nothing.
        /// </summary>
        public void CycleTextureFill()
        {
            if (selectedEntity is not ITexturedEntity target || target.TextureFillModeName == null)
            {
                editorPanel.SetStatus("У этого объекта картинка всегда вписывается целиком.");
                return;
            }

            pushUndo();
            target.CycleTextureFill();

            // Tiling needs the image loaded with a repeating wrap mode, so switching into or out of
            // it means resolving the same path through a different store.
            requestTexture(target);
            editorPanel.SetMaterialLabels(target);
        }

        public void ToggleTextureSmoothing()
        {
            if (selectedEntity is not ITexturedEntity target)
                return;

            pushUndo();
            target.ToggleTextureSmoothing();
            requestTexture(target);
            editorPanel.SetMaterialLabels(target);
        }

        public void SelectTextureAsset(string assetId)
        {
            textureLibrary.Select(assetId);
            editorPanel.RebuildTextureBrowser();
        }

        #endregion

        #region test hooks

        internal WorldSimulation SimulationForTesting => simulation;
        internal PlayerState LocalPlayerForTesting => localPlayerState;
        internal bool DialogueOpenForTesting => activeNpc?.IsDialogueOpen == true;
        internal int RoomLoadCountForTesting { get; private set; }
        internal bool WorldLoadedForTesting => worldLoaded;
        internal bool CanEditForTesting => session.CanEdit.Value;
        internal long RevisionForTesting => session.Revision.Value;
        internal string CurrentRoomIdForTesting => currentRoomId;
        internal int RoomCountForTesting => rooms.Count;
        internal int EntityCountForTesting => entities.Count;
        internal Vector2 RoomSizeForTesting => currentRoomSize;
        internal bool EditorModeForTesting => editorMode;
        internal Vector2 CameraPositionForTesting => renderedCameraPosition;
        internal Vector2 CameraLookAheadForTesting => lookAhead.Offset;
        internal WarpMenu WarpMenuForTesting => warpMenu;
        internal WorldMap WorldMapForTesting => worldMap;
        internal IReadOnlyDictionary<string, RoomDefinition> RoomsForTesting => rooms;
        internal TexturePicker TexturePickerForTesting => texturePicker;
        internal DodgeWorldTextureLibrary TextureLibraryForTesting => textureLibrary;
        internal float EditorPanelAlphaForTesting => editorPanel.Alpha;
        internal DodgeWorldEditorPanel EditorPanelForTesting => editorPanel;
        internal IReadOnlyList<EditableWorldEntity> EntitiesForTesting => entities;
        internal void SelectForTesting(EditableWorldEntity entity) => setSelection(entity);
        internal EditableWorldEntity? SelectedForTesting => selectedEntity;
        internal void SwitchRoomForTesting(string roomId) => switchRoom(roomId);
        internal LobbyChatPanel ChatForTesting => chat;
        internal double WorldClockForTesting => simulation.CurrentTime;
        internal int ArrivalMarkerCountForTesting => arrivalMarkers.Count;
        internal int HurtSoundCountForTesting => hurtSound.PlayCountForTesting;
        internal void ReportServerHealthForTesting(int health) => onServerHealth(health);

        #endregion
    }
}
