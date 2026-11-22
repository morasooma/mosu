// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Volume;
using osu.Game.Screens.Backgrounds;
using osu.Game.Seasonal;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Menu
{
    public abstract partial class IntroScreen : StartupScreen
    {
        /// <summary>
        /// Whether we have loaded the menu previously.
        /// </summary>
        public bool DidLoadMenu { get; private set; }

        // These identities only resolve existing local maps. No archives are distributed or imported.
        internal static bool IsKnownIntroBeatmap(string hash) => hash is
            IntroCircles.BEATMAP_SET_HASH or IntroWelcome.BEATMAP_SET_HASH or IntroTriangles.BEATMAP_SET_HASH or IntroChristmas.CHRISTMAS_BEATMAP_SET_HASH;

        protected abstract string BeatmapHash { get; }

        protected bool UsingThemedIntro { get; private set; }

        // Keep the already-removed voice beginnings muted in local legacy tracks too.
        protected virtual double ThemedIntroMusicStartTime => 0;

        private WorkingBeatmap initialBeatmap;

        protected ITrack Track { get; private set; }

        private const int exit_delay = 3000;

        protected override bool PlayExitSound => false;

        private LeasedBindable<WorkingBeatmap> beatmap;

        private OsuScreen nextScreen;

        [Resolved]
        private MusicController musicController { get; set; }

        [CanBeNull]
        private readonly Func<OsuScreen> createNextScreen;

        private bool usingSilentIntro;

        protected override BackgroundScreen CreateBackground() => new BackgroundScreenDefault
        {
            Colour = Color4.Black
        };

        public override bool? AllowGlobalTrackControl => false;

        protected IntroScreen([CanBeNull] Func<MainMenu> createNextScreen = null)
        {
            this.createNextScreen = createNextScreen;
        }

        [Resolved]
        private BeatmapManager beatmaps { get; set; }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, AudioManager audio, RealmAccess realm)
        {
            // prevent user from changing beatmap while the intro is still running.
            beatmap = Beatmap.BeginLease(false);

            // Preserve the choice between a personal random track and an installed theme.
            if (!config.Get<bool>(OsuSetting.MenuMusic))
            {
                realm.Run(r =>
                {
                    var usableBeatmapSets = r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected)
                                            .AsEnumerable().Where(s => !IsKnownIntroBeatmap(s.Hash)).ToArray();

                    if (usableBeatmapSets.Length > 0)
                    {
                        var found = usableBeatmapSets[RNG.Next(usableBeatmapSets.Length)].Beatmaps.FirstOrDefault();

                        if (found != null)
                            initialBeatmap = beatmaps.GetWorkingBeatmap(found);
                    }
                });
            }

            if (initialBeatmap == null)
            {
                string hash = BeatmapHash;
                var existing = beatmaps.QueryBeatmapSet(s => !s.DeletePending && s.Hash == hash);
                existing?.PerformRead(set =>
                {
                    foreach (var info in set.Beatmaps)
                    {
                        if (info.Path == null || string.IsNullOrEmpty(info.Metadata.AudioFile))
                            continue;

                        var working = beatmaps.GetWorkingBeatmap(info);
                        string audioPath = set.GetPathForFile(info.Metadata.AudioFile);
                        string mapPath = set.GetPathForFile(info.Path);
                        if (audioPath == null || mapPath == null)
                            continue;

                        using var audioFile = working.GetStream(audioPath);
                        using var mapFile = working.GetStream(mapPath);
                        if (audioFile == null || audioFile.Length == 0 || mapFile == null || mapFile.Length == 0)
                            continue;

                        initialBeatmap = working;
                        UsingThemedIntro = true;
                        break;
                    }
                });
            }

            // Missing/deleted local entries never trigger a download, import or repair.
            usingSilentIntro = initialBeatmap == null;
            initialBeatmap ??= new DummyWorkingBeatmap(audio, null);
            AddInternal(new GlobalScrollAdjustsVolume());
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);
            ensureEventuallyArrivingAtMenu();
        }

        [Resolved]
        private INotificationOverlay notifications { get; set; }

        private void ensureEventuallyArrivingAtMenu()
        {
            // This intends to handle the case where an intro may get stuck.
            // Historically, this could happen if the host system's audio device is in a state it can't
            // play audio, causing a clock to never elapse time and the intro to never end.
            //
            // This safety measure gives the user a chance to fix the problem from the settings menu.
            Scheduler.AddDelayed(() =>
            {
                if (DidLoadMenu)
                    return;

                PrepareMenuLoad();
                LoadMenu();

                if (!Debugger.IsAttached)
                {
                    notifications.Post(new SimpleErrorNotification
                    {
                        Text = NotificationsStrings.AudioPlaybackIssue
                    });
                }
            }, 8000);
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            this.FadeIn(300);

            ApplyToBackground(b => b.FadeColour(Color4.Black, 100));

            const double fade_out_time = 500;

            var track = musicController.CurrentTrack;

            // ensure the track doesn't change or loop as we are exiting.
            track.Looping = false;
            Beatmap.Disabled = true;

            // No upstream outro voice; just fade out the music.
            track.VolumeTo(0, fade_out_time, Easing.Out);

            //don't want to fade out completely else we will stop running updates.
            Game.FadeTo(0.01f, fade_out_time).OnComplete(_ => this.Exit());

            base.OnResuming(e);
        }

        private bool backgroundFaded;

        protected void FadeInBackground(float duration = 0)
        {
            ApplyToBackground(b => b.FadeColour(Color4.White, duration));
            backgroundFaded = true;
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);
            initialBeatmap = null;

            if (!backgroundFaded)
                FadeInBackground(200);
        }

        protected void StartTrack()
        {
            var drawableTrack = musicController.CurrentTrack;

            if (!usingSilentIntro && !UsingThemedIntro)
            {
                initialBeatmap?.PrepareTrackForPreview(false, -2600);

                drawableTrack.VolumeTo(0);
                drawableTrack.Restart();
                drawableTrack.VolumeTo(1, 2600, Easing.InCubic);
            }
            else
            {
                drawableTrack.Restart();
            }
        }

        protected override void LogoArriving(OsuLogo logo, bool resuming)
        {
            base.LogoArriving(logo, resuming);

            logo.Colour = Color4.White;
            logo.Triangles = false;
            logo.Ripple = false;

            if (!resuming)
            {
                beatmap.Value = initialBeatmap;
                Track = beatmap.Value.Track;
                // MusicController loads a fresh track on the beatmap change. Set
                // looping on that instance, not the dummy's constructor-time track.
                // Silence must not finish and advance the user's playlist.
                if (usingSilentIntro)
                    Track.Looping = true;

                // ensure the track starts at maximum volume
                musicController.CurrentTrack.FinishTransforms();

                if (UsingThemedIntro && ThemedIntroMusicStartTime > 0)
                {
                    var drawableTrack = musicController.CurrentTrack;
                    double musicStartTime = ThemedIntroMusicStartTime;
                    var voiceVolume = new BindableDouble(0);
                    drawableTrack.AddAdjustment(AdjustableProperty.Volume, voiceVolume);
                    drawableTrack.OnUpdate += _ => voiceVolume.Value = drawableTrack.CurrentTime < musicStartTime ? 0 : 1;
                }

                logo.MoveTo(new Vector2(0.5f));
                logo.ScaleTo(Vector2.One);
                logo.Hide();
            }
            else
            {
                const int quick_appear = 350;
                int initialMovementTime = logo.Alpha > 0.2f ? quick_appear : 0;

                logo.MoveTo(new Vector2(0.5f), initialMovementTime, Easing.OutQuint);

                logo
                    .ScaleTo(1, initialMovementTime, Easing.OutQuint)
                    .FadeIn(quick_appear, Easing.OutQuint)
                    .Then()
                    .RotateTo(20, exit_delay * 1.5f)
                    .FadeOut(exit_delay);
            }
        }

        protected void PrepareMenuLoad()
        {
            if (nextScreen != null)
                return;

            nextScreen = createNextScreen?.Invoke();

            if (nextScreen != null)
                LoadComponentAsync(nextScreen);
        }

        protected void LoadMenu()
        {
            if (DidLoadMenu)
                return;

            beatmap.Return();

            DidLoadMenu = true;
            if (nextScreen != null)
                this.Push(nextScreen);
        }
    }
}
