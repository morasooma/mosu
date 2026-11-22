// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.Menu;
using IntroSequence = osu.Game.Configuration.IntroSequence;

namespace osu.Game.Overlays.Settings.Sections.UserInterface
{
    public partial class MainMenuSettings : SettingsSubsection
    {
        protected override LocalisableString Header => UserInterfaceStrings.MainMenuHeader;

        private IBindable<APIUser> user = null!;

        private readonly Bindable<SettingsNote.Data?> backgroundSourceNote = new Bindable<SettingsNote.Data?>();
        private SettingsItemV2 themeMusicSetting = null!;
        private IDisposable? beatmapSubscription;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IAPIProvider api)
        {
            user = api.LocalUser.GetBoundCopy();

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.ShowMenuTips,
                    Current = config.GetBindable<bool>(OsuSetting.MenuTips)
                })
                {
                    FavouriteId = "OsuSetting.MenuTips",
                },
                themeMusicSetting = new SettingsItemV2(new FormCheckBox
                {
                    Caption = UserInterfaceStrings.OsuMusicTheme,
                    Current = config.GetBindable<bool>(OsuSetting.MenuMusic)
                })
                {
                    FavouriteId = "OsuSetting.MenuMusic",
                    Keywords = new[] { "intro", "welcome" },
                    CanBeShown = { Value = false },
                },
                new SettingsItemV2(new FormEnumDropdown<IntroSequence>
                {
                    Caption = UserInterfaceStrings.IntroSequence,
                    Current = config.GetBindable<IntroSequence>(OsuSetting.IntroSequence),
                })
                {
                    FavouriteId = "OsuSetting.IntroSequence",
                },
                new SettingsItemV2(new FormEnumDropdown<BackgroundSource>
                {
                    Caption = UserInterfaceStrings.BackgroundSource,
                    Current = config.GetBindable<BackgroundSource>(OsuSetting.MenuBackgroundSource),
                })
                {
                    FavouriteId = "OsuSetting.MenuBackgroundSource",
                    Note = { BindTarget = backgroundSourceNote },
                },
                new SettingsItemV2(new FormEnumDropdown<SeasonalBackgroundMode>
                {
                    Caption = UserInterfaceStrings.SeasonalBackgrounds,
                    Current = config.GetBindable<SeasonalBackgroundMode>(OsuSetting.SeasonalBackgroundMode),
                })
                {
                    FavouriteId = "OsuSetting.SeasonalBackgroundMode",
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Offer the old choice only for themes already present in this local library.
            beatmapSubscription = realm.RegisterForNotifications(r => r.All<BeatmapSetInfo>().Where(s => !s.DeletePending),
                (sets, _) => themeMusicSetting.CanBeShown.Value = sets.AsEnumerable().Any(s => IntroScreen.IsKnownIntroBeatmap(s.Hash)));

            user.BindValueChanged(u =>
            {
                if (u.NewValue?.IsSupporter != true)
                    backgroundSourceNote.Value = new SettingsNote.Data(UserInterfaceStrings.NotSupporterNote, SettingsNote.Type.Informational);
                else
                    backgroundSourceNote.Value = null;
            }, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            beatmapSubscription?.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
