// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input;
using osu.Framework.Input.Handlers;
using osu.Framework.Input.Handlers.Joystick;
using osu.Framework.Input.Handlers.Midi;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Input.Handlers.Pen;
using osu.Framework.Input.Handlers.Tablet;
using osu.Framework.Input.Handlers.Touch;
using osu.Framework.IO.Stores;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Chat;
using osu.Game.Online.Leaderboards;
using osu.Game.Online.Metadata;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Spectator;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Overlays.Settings.Sections.Input;
using osu.Game.Resources;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Utils;
using RuntimeInfo = osu.Framework.RuntimeInfo;

namespace osu.Game
{
    /// <summary>
    /// The most basic <see cref="Game"/> that can be used to host osu! components and systems.
    /// Unlike <see cref="OsuGame"/>, this class will not load any kind of UI, allowing it to be used
    /// for provide dependencies to test cases without interfering with them.
    /// </summary>
    [Cached(typeof(OsuGameBase))]
    public partial class OsuGameBase : Framework.Game, ICanAcceptFiles, IBeatSyncProvider
    {
#if DEBUG
        public const string GAME_NAME = "mosu (development)";
#else
        public const string GAME_NAME = "mosu";
#endif

        public const string OSU_PROTOCOL = "osu://";

        /// <summary>
        /// The filename of the main client database.
        /// </summary>
        public const string CLIENT_DATABASE_FILENAME = @"client.realm";

        /// <summary>
        /// Font families bundled with the game, rather than loaded from the user's Fonts directory.
        /// </summary>
        public static readonly IReadOnlySet<string> BuiltInCustomUIFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Default", "Torus", "Inter", "Venera", "Noto", "Torus-Alternate"
        };

        public static readonly List<string> AvailableCustomUIFonts = new List<string>
        {
            "Default", "Torus", "Inter", "Venera", "Noto", "Torus-Alternate"
        };

        private readonly HashSet<string> loadedCustomFontFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public const int SAMPLE_CONCURRENCY = 6;

        public const double SFX_STEREO_STRENGTH = 0.6;

        /// <summary>
        /// Length of debounce (in milliseconds) for commonly occuring sample playbacks that could stack.
        /// </summary>
        public const int SAMPLE_DEBOUNCE_TIME = 20;

        /// <summary>
        /// The maximum volume at which audio tracks should play back at. This can be set lower than 1 to create some head-room for sound effects.
        /// </summary>
        private const double global_track_volume_adjust = 0.8;

        public virtual bool UseDevelopmentServer => DebugUtils.IsDebugBuild;

        /// <summary>
        /// Whether the capability-limited official osu! beatmap client should be created.
        /// Replay/video render processes override this to guarantee fully offline operation.
        /// </summary>
        protected virtual bool EnableOfficialBeatmapIntegration => false;

        public virtual EndpointConfiguration CreateEndpoints()
        {
            EndpointConfiguration config = UseDevelopmentServer ? new DevelopmentEndpointConfiguration() : new ProductionEndpointConfiguration();
            Online.MosuServerEnvironment.IsThirdPartyServer = false;
            Online.MosuServerEnvironment.SupportsSpecialRulesets = true;
            Online.MosuServerEnvironment.ActiveVersion = string.Empty;
            Online.MosuServerEnvironment.ActiveVersionHash = string.Empty;
            Online.MosuServerEnvironment.DisableBeatmapStatusOverwrite = LocalConfig?.Get<bool>(OsuSetting.ForkDisableBeatmapStatusOverwrite) ?? true;

            string serverUrl = Online.MosuServerEnvironment.GetServerUrl(LocalConfig?.Get<bool>(OsuSetting.ForkUseConnectionProxy) ?? false);

            config.APIUrl = serverUrl;
            config.WebsiteUrl = serverUrl;
            config.UpdateUrl = Online.MosuServerEnvironment.UpdateUrl;
            config.SpectatorUrl = $"{serverUrl}/spectator";
            config.MultiplayerUrl = $"{serverUrl}/multiplayer";
            config.MetadataUrl = $"{serverUrl}/metadata";
            config.BeatmapSubmissionServiceUrl = $"{serverUrl}/beatmap-submission";
            config.APIClientID = Online.MosuClientAuthentication.OAuthClientId;
            config.APIClientSecret = Online.MosuClientAuthentication.OAuthClientSecret;

            return config;
        }

        protected override OnlineStore CreateOnlineStore() => new TrustedDomainOnlineStore(LocalConfig);

        public virtual Version AssemblyVersion => Assembly.GetEntryAssembly()?.GetName().Version ?? new Version();

        /// <summary>
        /// MD5 representation of the game executable.
        /// </summary>
        private string versionHash;
        public string VersionHash
        {
            get => !string.IsNullOrEmpty(Online.MosuServerEnvironment.ActiveVersionHash) ? Online.MosuServerEnvironment.ActiveVersionHash : versionHash;
            private set => versionHash = value;
        }

        public bool IsDeployedBuild => AssemblyVersion.Major > 0;

        public virtual string Version
        {
            get
            {
                if (!string.IsNullOrEmpty(Online.MosuServerEnvironment.ActiveVersion))
                    return Online.MosuServerEnvironment.ActiveVersion;

                if (!IsDeployedBuild)
                    return @"local " + (DebugUtils.IsDebugBuild ? @"debug" : @"release");

                string informationalVersion = Assembly.GetEntryAssembly()?
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                // Example: [assembly: AssemblyInformationalVersion("2025.613.0-tachyon+d934e574b2539e8787956c3c9ecce9dadebb10ee")]
                if (!string.IsNullOrEmpty(informationalVersion))
                    return informationalVersion.Split('+').First();

                Version version = AssemblyVersion;
                return $@"{version.Major}.{version.Minor}.{version.Build}-lazer";
            }
        }

        /// <summary>
        /// The <see cref="Edges"/> that the game should be drawn over at a top level.
        /// Defaults to <see cref="Edges.None"/>.
        /// </summary>
        protected virtual Edges SafeAreaOverrideEdges => Edges.None;

        protected OsuConfigManager LocalConfig { get; private set; }

        protected SessionStatics SessionStatics { get; private set; }

        protected OsuColour Colours { get; private set; }

        protected BeatmapManager BeatmapManager { get; private set; }

        protected BeatmapModelDownloader BeatmapDownloader { get; private set; }

        protected ScoreManager ScoreManager { get; private set; }

        protected ScoreModelDownloader ScoreDownloader { get; private set; }

        protected SkinManager SkinManager { get; private set; }

        protected RealmRulesetStore RulesetStore { get; private set; }
        protected RulesetHashCache RulesetHashCache { get; private set; }

        protected RealmKeyBindingStore KeyBindingStore { get; private set; }

        protected GlobalCursorDisplay GlobalCursorDisplay { get; private set; }

        protected MusicController MusicController { get; private set; }

        protected IAPIProvider API { get; set; }
        private IBeatmapApiProvider beatmapApi;
        protected OfficialOsuBeatmapApi? OfficialOsuBeatmapApi { get; private set; }

        protected Storage Storage { get; set; }

        /// <summary>
        /// The language in which the game is currently displayed in.
        /// </summary>
        public Bindable<Language> CurrentLanguage { get; } = new Bindable<Language>();

        protected Bindable<WorkingBeatmap> Beatmap { get; private set; } // cached via load() method

        /// <summary>
        /// The current ruleset selection for the local user.
        /// </summary>
        [Cached]
        [Cached(typeof(IBindable<RulesetInfo>))]
        protected internal readonly Bindable<RulesetInfo> Ruleset = new Bindable<RulesetInfo>();

        /// <summary>
        /// The current mod selection for the local user.
        /// </summary>
        /// <remarks>
        /// If a mod select overlay is present, mod instances set to this value are not guaranteed to remain as the provided instance and will be overwritten by a copy.
        /// In such a case, changes to settings of a mod will *not* propagate after a mod is added to this collection.
        /// As such, all settings should be finalised before adding a mod to this collection.
        /// </remarks>
        [Cached]
        [Cached(typeof(IBindable<IReadOnlyList<Mod>>))]
        protected readonly Bindable<IReadOnlyList<Mod>> SelectedMods = new Bindable<IReadOnlyList<Mod>>(Array.Empty<Mod>());

        /// <summary>
        /// Mods available for the current <see cref="Ruleset"/>.
        /// </summary>
        public readonly Bindable<Dictionary<ModType, IReadOnlyList<Mod>>> AvailableMods = new Bindable<Dictionary<ModType, IReadOnlyList<Mod>>>(new Dictionary<ModType, IReadOnlyList<Mod>>());

        private BeatmapDifficultyCache difficultyCache;
        private IBeatmapUpdater beatmapUpdater;

        private UserLookupCache userCache;
        private BeatmapLookupCache beatmapCache;
        protected LeaderboardManager LeaderboardManager { get; private set; }

        private RulesetConfigCache rulesetConfigCache;

        private SessionAverageHitErrorTracker hitErrorTracker;

        protected SpectatorClient SpectatorClient { get; private set; }

        protected MultiplayerClient MultiplayerClient { get; private set; }

        private MetadataClient metadataClient;

        private RealmAccess realm;

        protected SafeAreaContainer SafeAreaContainer { get; private set; }

        /// <summary>
        /// For now, this is used as a source specifically for beat synced components.
        /// Going forward, it could potentially be used as the single source-of-truth for beatmap timing.
        /// </summary>
        private readonly FramedBeatmapClock beatmapClock = new FramedBeatmapClock(applyOffsets: true, requireDecoupling: false);

        protected override Container<Drawable> Content => content;

        private Container content;

        private DependencyContainer dependencies;

        private readonly BindableNumber<double> globalTrackVolumeAdjust = new BindableNumber<double>(global_track_volume_adjust);

        private Bindable<string> frameworkLocale = null!;

        private IBindable<LocalisationParameters> localisationParameters = null!;
        private ModSettingChangeTracker selectedModSettingChangeTracker;

        /// <summary>
        /// Number of unhandled exceptions to allow before aborting execution.
        /// </summary>
        /// <remarks>
        /// When an unhandled exception is encountered, an internal count will be decremented.
        /// If the count hits zero, the game will crash.
        /// Each second, the count is incremented until reaching the value specified.
        /// </remarks>
        protected virtual int UnhandledExceptionsBeforeCrash => DebugUtils.IsDebugBuild ? 0 : 1;

        public OsuGameBase()
        {
            Name = GAME_NAME;

            allowableExceptions = UnhandledExceptionsBeforeCrash;
        }

        [BackgroundDependencyLoader]
        private void load(ReadableKeyCombinationProvider keyCombinationProvider, FrameworkConfigManager frameworkConfig)
        {
            try
            {
                using (var str = File.OpenRead(typeof(OsuGameBase).Assembly.Location))
                    VersionHash = str.ComputeMD5Hash();
            }
            catch
            {
                // special case for android builds, which can't read DLLs from a packed apk.
                // should eventually be handled in a better way.
                VersionHash = $"{Version}-{RuntimeInfo.OS}".ComputeMD5Hash();
            }

            Resources.AddStore(new DllResourceStore(OsuResources.ResourceAssembly));

            dependencies.Cache(realm = new RealmAccess(Storage, CLIENT_DATABASE_FILENAME, Host.UpdateThread));

            dependencies.CacheAs(new ForkDataStore(Storage));

            dependencies.CacheAs<RulesetStore>(RulesetStore = new RealmRulesetStore(realm, Storage));
            dependencies.CacheAs<IRulesetStore>(RulesetStore);

            dependencies.CacheAs(RulesetHashCache = new RulesetHashCache(RulesetStore));

            Decoder.RegisterDependencies(RulesetStore);

            dependencies.CacheAs(Storage);

            var largeStore = new LargeTextureStore(Host.Renderer, Host.CreateTextureLoaderStore(new NamespacedResourceStore<byte[]>(Resources, @"Textures")));
            largeStore.AddTextureSource(Host.CreateTextureLoaderStore(CreateOnlineStore()));
            dependencies.Cache(largeStore);

            dependencies.CacheAs(LocalConfig);
            dependencies.CacheAs<IGameplaySettings>(LocalConfig);

            LocalConfig.GetBindable<bool>(OsuSetting.ForkDisableBeatmapStatusOverwrite).BindValueChanged(e =>
            {
                Online.MosuServerEnvironment.DisableBeatmapStatusOverwrite = e.NewValue;
            }, true);

            InitialiseFonts();

            addFilesWarning();

            Audio.Samples.PlaybackConcurrency = SAMPLE_CONCURRENCY;

            dependencies.Cache(SkinManager = new SkinManager(Storage, realm, Host, Resources, Audio, Scheduler));
            dependencies.CacheAs<ISkinSource>(SkinManager);

            EndpointConfiguration endpoints = CreateEndpoints();

            MessageFormatter.WebsiteRootUrl = endpoints.WebsiteUrl;

            // Initialise localisation
            frameworkLocale = frameworkConfig.GetBindable<string>(FrameworkSetting.Locale);
            frameworkLocale.BindValueChanged(_ => updateLanguage());

            localisationParameters = Localisation.CurrentParameters.GetBoundCopy();
            localisationParameters.BindValueChanged(_ => updateLanguage(), true);

            CurrentLanguage.BindValueChanged(val => frameworkLocale.Value = val.NewValue.ToCultureCode());

            dependencies.CacheAs(API ??= CreateAPIProvider(endpoints));

            if (EnableOfficialBeatmapIntegration)
                OfficialOsuBeatmapApi = new OfficialOsuBeatmapApi(LocalConfig);

            dependencies.CacheAs<IBeatmapApiProvider>(beatmapApi = new BeatmapApiProvider(API, OfficialOsuBeatmapApi));

            var defaultBeatmap = new DummyWorkingBeatmap(Audio, Textures);

            dependencies.Cache(difficultyCache = new BeatmapDifficultyCache());

            // ordering is important here to ensure foreign keys rules are not broken in ModelStore.Cleanup()
            dependencies.Cache(ScoreManager = new ScoreManager(RulesetStore, () => BeatmapManager, Storage, realm, API, LocalConfig));

            dependencies.Cache(BeatmapManager = new BeatmapManager(Storage, realm, beatmapApi, Audio, Resources, Host, defaultBeatmap, difficultyCache, performOnlineLookups: true));
            dependencies.CacheAs<IWorkingBeatmapCache>(BeatmapManager);

            dependencies.Cache(BeatmapDownloader = new BeatmapModelDownloader(BeatmapManager, beatmapApi, LocalConfig));
            dependencies.Cache(ScoreDownloader = new ScoreModelDownloader(ScoreManager, API));

            // Add after all the above cache operations as it depends on them.
            base.Content.Add(difficultyCache);

            // TODO: OsuGame or OsuGameBase?
            dependencies.CacheAs(beatmapUpdater = CreateBeatmapUpdater());
            dependencies.CacheAs(SpectatorClient = new OnlineSpectatorClient(endpoints));
            dependencies.CacheAs(MultiplayerClient = new OnlineMultiplayerClient(endpoints));
            dependencies.CacheAs(metadataClient = new OnlineMetadataClient(endpoints));

            base.Content.Add(new BeatmapOnlineChangeIngest(beatmapUpdater, realm, metadataClient));

            BeatmapManager.ProcessBeatmap = (beatmapSet, scope) => beatmapUpdater.Process(beatmapSet, scope);

            dependencies.Cache(userCache = new UserLookupCache());
            base.Content.Add(userCache);

            dependencies.Cache(beatmapCache = new BeatmapLookupCache());
            base.Content.Add(beatmapCache);

            dependencies.CacheAs<IRulesetConfigCache>(rulesetConfigCache = new RulesetConfigCache(realm, RulesetStore));

            var powerStatus = CreateBatteryInfo();
            if (powerStatus != null)
                dependencies.CacheAs(powerStatus);

            dependencies.Cache(SessionStatics = new SessionStatics());
            dependencies.Cache(hitErrorTracker = new SessionAverageHitErrorTracker());
            dependencies.Cache(Colours = new OsuColour());

            var configuredTheme = LocalConfig.GetBindable<ThemeMode>(OsuSetting.ForkThemeMode);

            void applyEffectiveTheme()
            {
                bool forceLightTheme = API.LocalUser.Value?.ForceLightTheme ?? false;

                OverlayColourProvider.CurrentTheme.Value = ThemeModeResolver.Resolve(
                    configuredTheme.Value,
                    forceLightTheme,
                    Online.MosuServerEnvironment.IsThirdPartyServer);
            }

            configuredTheme.BindValueChanged(_ => applyEffectiveTheme(), true);
            API.LocalUser.BindValueChanged(_ => applyEffectiveTheme(), true);

            IReadOnlyList<Mod>? pendingNormalisedMods = null;

            void applyPendingNormalisedMods()
            {
                if (pendingNormalisedMods == null || SelectedMods.Disabled)
                    return;

                IReadOnlyList<Mod> mods = pendingNormalisedMods;
                pendingNormalisedMods = null;
                SelectedMods.Value = mods;
            }

            void queueNormalisedMods(IReadOnlyList<Mod> mods)
            {
                pendingNormalisedMods = mods;
                Scheduler.AddOnce(applyPendingNormalisedMods);
            }

            SelectedMods.DisabledChanged += disabled =>
            {
                if (!disabled)
                    Scheduler.AddOnce(applyPendingNormalisedMods);
            };

            SelectedMods.BindValueChanged(mods =>
            {
                if (Online.MosuServerEnvironment.IsThirdPartyServer && mods.NewValue.Any(m => m.Type == ModType.Mosu))
                {
                    queueNormalisedMods(mods.NewValue.Where(m => m.Type != ModType.Mosu).ToList());
                }
            }, true);

            RegisterImportHandler(BeatmapManager);
            RegisterImportHandler(ScoreManager);
            RegisterImportHandler(SkinManager);

            // drop track volume game-wide to leave some head-room for UI effects / samples.
            // this means that for the time being, gameplay sample playback is louder relative to the audio track, compared to stable.
            // we may want to revisit this if users notice or complain about the difference (consider this a bit of a trial).
            Audio.Tracks.AddAdjustment(AdjustableProperty.Volume, globalTrackVolumeAdjust);

            Beatmap = new NonNullableBindable<WorkingBeatmap>(defaultBeatmap);

            dependencies.CacheAs<IBindable<WorkingBeatmap>>(Beatmap);
            dependencies.CacheAs(Beatmap);

            dependencies.Cache(LeaderboardManager = new LeaderboardManager());
            base.Content.Add(LeaderboardManager);

            // add api components to hierarchy.
            if (API is APIAccess apiAccess)
                base.Content.Add(apiAccess);

            if (OfficialOsuBeatmapApi != null)
                base.Content.Add(OfficialOsuBeatmapApi);

            base.Content.Add(SpectatorClient);
            base.Content.Add(MultiplayerClient);
            base.Content.Add(metadataClient);

            base.Content.Add(rulesetConfigCache);

            PreviewTrackManager previewTrackManager;
            dependencies.Cache(previewTrackManager = new PreviewTrackManager(BeatmapManager.BeatmapTrackStore));
            base.Content.Add(previewTrackManager);

            base.Content.Add(MusicController = new MusicController());
            dependencies.CacheAs(MusicController);

            MusicController.TrackChanged += onTrackChanged;
            base.Content.Add(beatmapClock);

            GlobalActionContainer globalBindings;

            OsuMenuSamples menuSamples;
            dependencies.Cache(menuSamples = new OsuMenuSamples());
            base.Content.Add(menuSamples);

            base.Content.Add(SafeAreaContainer = new SafeAreaContainer
            {
                SafeAreaOverrideEdges = SafeAreaOverrideEdges,
                RelativeSizeAxes = Axes.Both,
                Child = CreateScalingContainer().WithChild(globalBindings = new GlobalActionContainer(this)
                {
                    Children = new Drawable[]
                    {
                        (GlobalCursorDisplay = new GlobalCursorDisplay
                        {
                            RelativeSizeAxes = Axes.Both
                        }).WithChild(content = new OsuTooltipContainer(GlobalCursorDisplay.MenuCursor)
                        {
                            RelativeSizeAxes = Axes.Both
                        }),
                    }
                })
            });

            base.Content.Add(new TouchInputInterceptor());
            base.Content.Add(hitErrorTracker);

            KeyBindingStore = new RealmKeyBindingStore(realm, keyCombinationProvider);
            KeyBindingStore.Register(globalBindings, RulesetStore.AvailableRulesets);
            dependencies.Cache(KeyBindingStore);

            dependencies.Cache(globalBindings);

            Ruleset.BindValueChanged(onRulesetChanged);
            Beatmap.BindValueChanged(onBeatmapChanged);

            // make config aware of how to lookup skins for on-screen display purposes.
            // if this becomes a more common thing, tracked settings should be reconsidered to allow local DI.
            LocalConfig.LookupSkinName = id => SkinManager.Query(s => s.ID == id)?.ToString() ?? "Unknown";
            LocalConfig.LookupKeyBindings = l => KeyBindingStore.GetBindingsStringFor(l);
        }

        private void updateLanguage() => CurrentLanguage.Value = LanguageExtensions.GetLanguageFor(frameworkLocale.Value, localisationParameters.Value);

        private void addFilesWarning()
        {
            const string filename = "IMPORTANT READ ME.txt";

            if (!Storage.Exists(filename))
            {
                using (var stream = Storage.CreateFileSafely(filename))
                using (var textWriter = new StreamWriter(stream))
                {
                    textWriter.WriteLine(@"This folder contains all your user files and configuration.");
                    textWriter.WriteLine(@"Please DO NOT make manual changes to this folder.");
                    textWriter.WriteLine();
                    textWriter.WriteLine(@"- If you want to back up your game files, please back up THE ENTIRETY OF THIS DIRECTORY.");
                    textWriter.WriteLine(@"- If you want to delete all of your game files, please delete THE ENTIRETY OF THIS DIRECTORY.");
                    textWriter.WriteLine();
                    textWriter.WriteLine(@"To be very clear, the ""files/"" directory inside this directory stores all the raw pieces of your beatmaps, skins, and replays.");
                    textWriter.WriteLine(@"Importantly, it is NOT the only directory you need a backup of to avoid losing data. If you copy only the ""files/"" directory, YOU WILL LOSE DATA.");
                    textWriter.WriteLine();
                    textWriter.WriteLine(@"For more information on how these files are organised,");
                    textWriter.WriteLine(@"see https://github.com/ppy/osu/wiki/User-file-storage");
                }
            }
        }

        private void onTrackChanged(WorkingBeatmap beatmap, TrackChangeDirection direction) => beatmapClock.ChangeSource(beatmap.Track);

        protected virtual void InitialiseFonts()
        {
            AddFont(Resources, @"Fonts/Torus/Torus-Regular");
            AddFont(Resources, @"Fonts/Torus/Torus-Light");
            AddFont(Resources, @"Fonts/Torus/Torus-SemiBold");
            AddFont(Resources, @"Fonts/Torus/Torus-Bold");

            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-Regular");
            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-Light");
            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-SemiBold");
            AddFont(Resources, @"Fonts/Torus-Alternate/Torus-Alternate-Bold");

            AddFont(Resources, @"Fonts/Inter/Inter-Regular");
            AddFont(Resources, @"Fonts/Inter/Inter-RegularItalic");
            AddFont(Resources, @"Fonts/Inter/Inter-Light");
            AddFont(Resources, @"Fonts/Inter/Inter-LightItalic");
            AddFont(Resources, @"Fonts/Inter/Inter-SemiBold");
            AddFont(Resources, @"Fonts/Inter/Inter-SemiBoldItalic");
            AddFont(Resources, @"Fonts/Inter/Inter-Bold");
            AddFont(Resources, @"Fonts/Inter/Inter-BoldItalic");

            AddFont(Resources, @"Fonts/Noto/Noto-Basic");
            AddFont(Resources, @"Fonts/Noto/Noto-Bopomofo");
            AddFont(Resources, @"Fonts/Noto/Noto-CJK-Basic");
            AddFont(Resources, @"Fonts/Noto/Noto-CJK-Compatibility");
            AddFont(Resources, @"Fonts/Noto/Noto-Hangul");
            AddFont(Resources, @"Fonts/Noto/Noto-Thai");

            AddFont(Resources, @"Fonts/Venera/Venera-Light");
            AddFont(Resources, @"Fonts/Venera/Venera-Bold");
            AddFont(Resources, @"Fonts/Venera/Venera-Black");

            Fonts.AddStore(new OsuIcon.OsuIconStore(Textures));

            RefreshCustomFonts();
        }

        /// <summary>
        /// Updates the list of selectable custom fonts without reloading glyph stores.
        /// </summary>
        public void RefreshCustomFontList()
        {
            try
            {
                var customFontsStorage = Storage.GetStorageForDirectory("Fonts");

                foreach (var file in customFontsStorage.GetFiles(string.Empty, "*.fnt"))
                {
                    if (!AvailableCustomUIFonts.Contains(file))
                        AvailableCustomUIFonts.Add(file);
                }

                foreach (var file in customFontsStorage.GetFiles(string.Empty, "*.ttf"))
                {
                    if (!AvailableCustomUIFonts.Contains(file))
                        AvailableCustomUIFonts.Add(file);
                }

                foreach (var file in customFontsStorage.GetFiles(string.Empty, "*.otf"))
                {
                    if (!AvailableCustomUIFonts.Contains(file))
                        AvailableCustomUIFonts.Add(file);
                }
            }
            catch (Exception ex)
            {
                osu.Framework.Logging.Logger.Error(ex, "Failed to refresh custom UI font list");
            }
        }

        public void RefreshCustomFonts()
        {
            try
            {
                var customFontsStorage = Storage.GetStorageForDirectory("Fonts");
                var customFontsStore = new ResourceStore<byte[]>(new StorageBackedResourceStore(customFontsStorage));

                foreach (var file in customFontsStorage.GetFiles(string.Empty, "*.fnt"))
                {
                    if (!loadedCustomFontFiles.Add(file))
                        continue;

                    string baseName = System.IO.Path.GetFileNameWithoutExtension(file);
                    string expectedPng = $"{file}_0.png";
                    
                    // Auto-fix texture names for user convenience (e.g., Unnamed.png -> Unnamed.fnt_0.png)
                    if (!customFontsStorage.Exists(expectedPng))
                    {
                        if (customFontsStorage.Exists($"{baseName}.png"))
                            System.IO.File.Move(customFontsStorage.GetFullPath($"{baseName}.png"), customFontsStorage.GetFullPath(expectedPng));
                        else if (customFontsStorage.Exists($"{baseName}_0.png"))
                            System.IO.File.Move(customFontsStorage.GetFullPath($"{baseName}_0.png"), customFontsStorage.GetFullPath(expectedPng));
                    }

                    // Inject font directly into framework cache to bypass FormatHint.Binary restriction
                    try
                    {
                        string fntPath = customFontsStorage.GetFullPath(file);
                        var fontFile = SharpFNT.BitmapFont.FromFile(fntPath);

                        string md5;
                        using (var stream = customFontsStorage.GetStream(file))
                        {
                            md5 = stream.ComputeMD5Hash();
                        }

                        var cacheField = typeof(osu.Framework.IO.Stores.GlyphStore).GetField("font_cache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                        if (cacheField != null)
                        {
                            var cache = cacheField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<string, SharpFNT.BitmapFont>;
                            if (cache != null)
                                cache[md5] = fontFile;
                        }
                    }
                    catch (Exception ex)
                    {
                        osu.Framework.Logging.Logger.Error(ex, "Failed to parse/inject font " + file);
                    }

                    string fontName = file;
                    var store = Host.CreateTextureLoaderStore(customFontsStore);
                    Fonts.AddTextureSource(new CustomWeightGlyphStore(customFontsStore, fontName, fontName, store));

                    string[] weights = { "Light", "Regular", "Medium", "SemiBold", "Bold", "Black" };
                    foreach (var weight in weights)
                    {
                        Fonts.AddTextureSource(new CustomWeightGlyphStore(customFontsStore, fontName, fontName + "-" + weight, store));
                        Fonts.AddTextureSource(new CustomWeightGlyphStore(customFontsStore, fontName, fontName + "-" + weight + "Italic", store));
                    }
                    Fonts.AddTextureSource(new CustomWeightGlyphStore(customFontsStore, fontName, fontName + "-Italic", store));



                    if (!AvailableCustomUIFonts.Contains(fontName))
                        AvailableCustomUIFonts.Add(fontName);
                }
                
                foreach (var file in customFontsStorage.GetFiles(string.Empty, "*.ttf"))
                {
                    string fontName = file;
                    if (!AvailableCustomUIFonts.Contains(fontName))
                        AvailableCustomUIFonts.Add(fontName);
                }
                
                foreach (var file in customFontsStorage.GetFiles(string.Empty, "*.otf"))
                {
                    string fontName = file;
                    if (!AvailableCustomUIFonts.Contains(fontName))
                        AvailableCustomUIFonts.Add(fontName);
                }
            }
            catch (Exception ex)
            {
                osu.Framework.Logging.Logger.Error(ex, "Failed to load custom UI fonts from Fonts directory");
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            var localeMappings = Enum.GetValues<Language>().Select(language =>
            {
#if DEBUG
                if (language == Language.debug)
                    return new LocaleMapping("debug", new DebugLocalisationStore());
#endif

                string cultureCode = language.ToCultureCode();

                try
                {
                    return new LocaleMapping(new ResourceManagerLocalisationStore(cultureCode));
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Could not load localisations for language \"{cultureCode}\"");
                    return null;
                }
            }).Where(m => m != null);

            Localisation.AddLocaleMappings(localeMappings);

            SelectedMods.BindValueChanged(_ =>
            {
                selectedModSettingChangeTracker?.Dispose();
                selectedModSettingChangeTracker = new ModSettingChangeTracker(SelectedMods.Value);
                selectedModSettingChangeTracker.SettingChanged += _ => Scheduler.AddOnce(updateBeatmapInfoAwareMods);

                updateBeatmapInfoAwareMods();
            }, true);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent) =>
            dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            // may be non-null for certain tests
            Storage ??= host.Storage;

            LocalConfig ??= UseDevelopmentServer
                ? new DevelopmentOsuConfigManager(Storage)
                : new OsuConfigManager(Storage);

            host.ExceptionThrown += onExceptionThrown;
        }

        #region Exit handling

        /// <summary>
        /// Use to programatically exit the game as if the user was triggering via alt-f4.
        /// By default, will keep persisting until an exit occurs (exit may be blocked multiple times).
        /// May be interrupted (see <see cref="OsuGame"/>'s override).
        /// </summary>
        public virtual void AttemptExit()
        {
            if (!OnExiting())
                Exit();
            else
                Scheduler.AddDelayed(AttemptExit, 2000);
        }

        /// <summary>
        /// An action that restarts the application after it has exited.
        /// </summary>
        [CanBeNull]
        public Action RestartOnExitAction { private get; set; }

        /// <summary>
        /// Signals that the application should not be restarted after it is exited.
        /// </summary>
        public void CancelRestartOnExit()
        {
            RestartOnExitAction = null;
        }

        /// <summary>
        /// If supported by the platform, the game will automatically restart after the next exit.
        /// </summary>
        /// <returns>Whether a restart operation was queued.</returns>
        public virtual bool RestartAppWhenExited() => false;

        #endregion

        /// <summary>
        /// Perform migration of user data to a specified path.
        /// </summary>
        /// <param name="path">The path to migrate to.</param>
        /// <returns>Whether migration succeeded to completion. If <c>false</c>, some files were left behind.</returns>
        /// <exception cref="TimeoutException"></exception>
        public bool MigrateUserData(string path)
        {
            Logger.Log($@"Migrating osu! data from ""{Storage.GetFullPath(string.Empty)}"" to ""{path}""...");

            IDisposable realmBlocker = null;

            try
            {
                ManualResetEventSlim readyToRun = new ManualResetEventSlim();

                bool success = false;

                Scheduler.Add(() =>
                {
                    try
                    {
                        realmBlocker = realm.BlockAllOperations("migration");
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Attempting to block all operations failed: {ex}", LoggingTarget.Database);
                    }

                    readyToRun.Set();
                }, false);

                if (!readyToRun.Wait(30000) || !success)
                    throw new TimeoutException("Attempting to block for migration took too long.");

                bool? cleanupSucceeded = (Storage as OsuStorage)?.Migrate(Host.GetStorage(path));

                Logger.Log(@"Migration complete!");
                return cleanupSucceeded != false;
            }
            finally
            {
                realmBlocker?.Dispose();
            }
        }

        protected virtual IBeatmapUpdater CreateBeatmapUpdater() => new BeatmapUpdater(BeatmapManager, difficultyCache, beatmapApi, Storage);

        protected virtual IAPIProvider CreateAPIProvider(EndpointConfiguration endpoints) => new APIAccess(this, LocalConfig, endpoints, VersionHash, RulesetHashCache);

        protected override UserInputManager CreateUserInputManager() => new OsuUserInputManager();

        protected virtual BatteryInfo CreateBatteryInfo() => null;

        protected virtual Container CreateScalingContainer() => new DrawSizePreservingFillContainer();

        protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new OsuStorage(host, defaultStorage);

        /// <summary>
        /// Creates an input settings subsection for an <see cref="InputHandler"/>.
        /// </summary>
        /// <remarks>Should be overriden per-platform to provide settings for platform-specific handlers.</remarks>
        public virtual SettingsSubsection CreateSettingsSubsectionFor(InputHandler handler)
        {
            // One would think that this could be moved to the `OsuGameDesktop` class, but doing so means that
            // OsuGameTestScenes will not show any input options (as they are based on OsuGame not OsuGameDesktop).
            //
            // This in turn makes it hard for ruleset creators to adjust input settings while testing their ruleset
            // within the test browser interface.
            if (RuntimeInfo.IsDesktop)
            {
                switch (handler)
                {
                    case ITabletHandler th:
                        return new TabletSettings(th);
                }
            }

            switch (handler)
            {
                case MouseHandler mh:
                    return new MouseSettings(mh);

                case JoystickHandler jh:
                    return new JoystickSettings(jh);

                case TouchHandler th:
                    return new TouchSettings(th);

                case PenHandler ph:
                    return new PenSettings(ph);

                case MidiHandler:
                    return new InputSubsection(handler);

                // return null for handlers that shouldn't have settings.
                default:
                    return null;
            }
        }

        private void onBeatmapChanged(ValueChangedEvent<WorkingBeatmap> beatmap)
        {
            if (IsLoaded && !ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException("Global beatmap bindable must be changed from update thread.");

            Logger.Log($"Game-wide working beatmap updated to {beatmap.NewValue}");
            updateBeatmapInfoAwareMods();
        }

        private void updateBeatmapInfoAwareMods()
        {
            IBeatmapInfo beatmapInfo = Beatmap.Value?.BeatmapInfo;

            foreach (var mod in SelectedMods.Value.OfType<IUpdatableByBeatmapInfo>())
                mod.UpdateFromBeatmapInfo(beatmapInfo);
        }

        private void onRulesetChanged(ValueChangedEvent<RulesetInfo> r)
        {
            if (IsLoaded && !ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException("Global ruleset bindable must be changed from update thread.");

            Ruleset instance = null;

            if (r.NewValue?.Available == true)
            {
                try
                {
                    instance = r.NewValue.CreateInstance();
                }
                catch (Exception e)
                {
                    Rulesets.RulesetStore.LogRulesetFailure(r.NewValue, e);
                }
            }

            if (instance == null)
            {
                // reject the change if the ruleset is not available.
                revertRulesetChange();
                return;
            }

            var dict = new Dictionary<ModType, IReadOnlyList<Mod>>();

            try
            {
                foreach (ModType type in Enum.GetValues<ModType>())
                {
                    IEnumerable<Mod> available = instance.GetModsFor(type)
                                                         // Rulesets should never return null mods, but let's be defensive just in case.
                                                         // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                                                         .Where(mod => mod != null);

                    dict[type] = available.ToList();
                }
            }
            catch (Exception e)
            {
                Rulesets.RulesetStore.LogRulesetFailure(r.NewValue, e);
                revertRulesetChange();
                return;
            }

            AvailableMods.Value = dict;

            if (SelectedMods.Disabled)
                return;

            var convertedMods = SelectedMods.Value.Select(mod =>
            {
                var newMod = instance.CreateModFromAcronym(mod.Acronym);
                newMod?.CopyCommonSettingsFrom(mod);
                return newMod;
            }).Where(newMod => newMod != null).ToList();

            if (!ModUtils.CheckValidForGameplay(convertedMods, out var invalid))
                invalid.ForEach(newMod => convertedMods.Remove(newMod));

            if (!convertedMods.Any() && LocalConfig.Get<bool>(OsuSetting.ForkClassicModDefault))
            {
                var classic = instance.CreateModFromAcronym("CL");
                if (classic != null)
                    convertedMods.Add(classic);
            }

            SelectedMods.Value = convertedMods;

            void revertRulesetChange() => Ruleset.Value = r.OldValue?.Available == true ? r.OldValue : RulesetStore.AvailableRulesets.First();
        }

        private int allowableExceptions;

        /// <summary>
        /// Allows a maximum of one unhandled exception, per second of execution.
        /// </summary>
        /// <returns>Whether to ignore the exception and continue running.</returns>
        private bool onExceptionThrown(Exception ex)
        {
            if (Interlocked.Decrement(ref allowableExceptions) < 0)
            {
                Logger.Log("Too many unhandled exceptions, crashing out.");
                RulesetStore?.TryDisableCustomRulesetsCausing(ex);
                return false;
            }

            Logger.Log($"Unhandled exception has been allowed with {allowableExceptions} more allowable exceptions.");
            // restore the stock of allowable exceptions after a short delay.
            Task.Delay(1000).ContinueWith(_ => Interlocked.Increment(ref allowableExceptions));

            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            selectedModSettingChangeTracker?.Dispose();

            RulesetStore?.Dispose();
            LocalConfig?.Dispose();

            beatmapUpdater?.Dispose();

            realm?.Dispose();

            if (Host != null)
                Host.ExceptionThrown -= onExceptionThrown;

            RestartOnExitAction?.Invoke();
        }

        ControlPointInfo IBeatSyncProvider.ControlPoints => Beatmap.Value.BeatmapLoaded ? Beatmap.Value.Beatmap.ControlPointInfo : null;
        IClock IBeatSyncProvider.Clock => beatmapClock;
        ChannelAmplitudes IHasAmplitudes.CurrentAmplitudes => Beatmap.Value.TrackLoaded ? Beatmap.Value.Track.CurrentAmplitudes : ChannelAmplitudes.Empty;
        private class CustomWeightGlyphStore : osu.Framework.IO.Stores.RawCachingGlyphStore, osu.Framework.IO.Stores.IGlyphStore, osu.Framework.IO.Stores.IResourceStore<osu.Framework.Graphics.Textures.TextureUpload>
        {
            private readonly string customFontName;
            private readonly string originalFontName;

            public CustomWeightGlyphStore(osu.Framework.IO.Stores.ResourceStore<byte[]> store, string assetName, string fontName, osu.Framework.IO.Stores.IResourceStore<osu.Framework.Graphics.Textures.TextureUpload> textureLoader) 
                : base(store, assetName, textureLoader)
            {
                customFontName = fontName;
                originalFontName = base.FontName;
            }

            string osu.Framework.IO.Stores.IGlyphStore.FontName => customFontName;

            private string mapName(string name)
            {
                if (name.StartsWith(customFontName + "/"))
                    return originalFontName + "/" + name.Substring(customFontName.Length + 1);
                return name;
            }

            osu.Framework.Graphics.Textures.TextureUpload osu.Framework.IO.Stores.IResourceStore<osu.Framework.Graphics.Textures.TextureUpload>.Get(string name)
            {
                if (string.IsNullOrEmpty(name)) return null;
                char c = name.Last();
                if (base.Font != null && base.Font.Characters.TryGetValue(c, out var character))
                {
                    if (character.Width == 0 || character.Height == 0)
                    {
                        var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(SixLabors.ImageSharp.Configuration.Default, 1, 1);
                        return new osu.Framework.Graphics.Textures.TextureUpload(image);
                    }
                }
                return base.Get(mapName(name));
            }

            System.Threading.Tasks.Task<osu.Framework.Graphics.Textures.TextureUpload> osu.Framework.IO.Stores.IResourceStore<osu.Framework.Graphics.Textures.TextureUpload>.GetAsync(string name, System.Threading.CancellationToken cancellationToken)
            {
                if (string.IsNullOrEmpty(name)) return System.Threading.Tasks.Task.FromResult<osu.Framework.Graphics.Textures.TextureUpload>(null);
                char c = name.Last();
                if (base.Font != null && base.Font.Characters.TryGetValue(c, out var character))
                {
                    if (character.Width == 0 || character.Height == 0)
                    {
                        var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(SixLabors.ImageSharp.Configuration.Default, 1, 1);
                        return System.Threading.Tasks.Task.FromResult(new osu.Framework.Graphics.Textures.TextureUpload(image));
                    }
                }
                return base.GetAsync(mapName(name), cancellationToken);
            }

            System.Collections.Generic.IEnumerable<string> osu.Framework.IO.Stores.IResourceStore<osu.Framework.Graphics.Textures.TextureUpload>.GetAvailableResources() => base.GetAvailableResources().Select(r => r.StartsWith(originalFontName + "/") ? customFontName + "/" + r.Substring(originalFontName.Length + 1) : r);
        }
    }
}
