// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.Legacy;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ServerProfilesSettings : SettingsSubsection
    {
        protected override LocalisableString Header => ForkSettingsStrings.ServerProfilesHeader;

        [Resolved]
        private ServerProfileManager profileManager { get; set; } = null!;

        [Resolved]
        private OsuGame? game { get; set; }

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private StableBanchoSession? stableBanchoSession { get; set; }

        private ProfileDropdown profileDropdown = null!;
        private SettingsButtonV2 deleteButton = null!;
        private SettingsButtonV2 testConnectionButton = null!;

        private readonly Bindable<string> activeProfileId = new Bindable<string>();

        private readonly Bindable<string> profileName = new Bindable<string>();
        private readonly Bindable<string> apiUrl = new Bindable<string>();
        private readonly Bindable<string> websiteUrl = new Bindable<string>();
        private readonly Bindable<string> username = new Bindable<string>();
        private readonly Bindable<string> stablePassword = new Bindable<string>();
        private readonly Bindable<string> banchoUrl = new Bindable<string>();
        private readonly Bindable<bool> useStableProtocol = new Bindable<bool>();
        private readonly Bindable<string> clientId = new Bindable<string>();
        private readonly Bindable<string> clientSecret = new Bindable<string>();
        private readonly Bindable<string> clientVersion = new Bindable<string>();
        private readonly Bindable<string> versionHash = new Bindable<string>();
        private readonly Bindable<bool> supportsSpecialRulesets = new Bindable<bool>();

        private FormTextBox nameTextBox = null!;
        private FormTextBox apiUrlTextBox = null!;
        private FormTextBox websiteUrlTextBox = null!;
        private FormTextBox usernameTextBox = null!;
        private FormTextBox banchoUrlTextBox = null!;
        private FormPasswordTextBox stablePasswordTextBox = null!;
        private FormTextBox clientIdTextBox = null!;
        private FormTextBox clientSecretTextBox = null!;
        private FormTextBox clientVersionTextBox = null!;
        private FormTextBox versionHashTextBox = null!;
        private FormCheckBox specialRulesetsCheckbox = null!;
        private SettingsItemV2 banchoUrlItem = null!;
        private SettingsItemV2 stablePasswordItem = null!;
        private SettingsItemV2 stableStatusItem = null!;
        private SettingsItemV2 clientIdItem = null!;
        private SettingsItemV2 clientSecretItem = null!;
        private SettingsItemV2 clientVersionItem = null!;
        private SettingsItemV2 versionHashItem = null!;
        private SettingsItemV2 specialRulesetsItem = null!;

        private bool isUpdatingFields;

        [BackgroundDependencyLoader]
        private void load()
        {
            profileDropdown = new ProfileDropdown(profileManager)
            {
                Caption = ForkSettingsStrings.ServerProfilesDropdownCaption,
                Current = activeProfileId
            };

            Children = new Drawable[]
            {
                new SettingsItemV2(profileDropdown),
                new DangerousSettingsButtonV2
                {
                    Text = ForkSettingsStrings.ServerProfilesConnectBtn,
                    Action = requestConnection
                },
                testConnectionButton = new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.ServerProfilesTestConnectionBtn,
                    TooltipText = ForkSettingsStrings.ServerProfilesTestConnectionHint,
                    Keywords = new[] { "test", "connection", "bancho", "server", "проверить", "подключение" },
                    Action = () => _ = testConnectionAsync()
                },
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.ServerProfilesAddBtn,
                    Action = addNewProfile
                },
                deleteButton = new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.ServerProfilesDeleteBtn,
                    Action = deleteProfile
                },
                new SettingsItemV2(nameTextBox = new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesNameCaption,
                    Current = profileName
                }),
                new SettingsItemV2(apiUrlTextBox = new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesApiUrlCaption,
                    Current = apiUrl
                }),
                new SettingsItemV2(websiteUrlTextBox = new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesWebsiteUrlCaption,
                    Current = websiteUrl
                }),
                new SettingsItemV2(usernameTextBox = new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesUsernameCaption,
                    Current = username
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesStableProtocolCaption,
                    HintText = ForkSettingsStrings.ServerProfilesStableProtocolHint,
                    Current = useStableProtocol
                }),
                banchoUrlItem = new SettingsItemV2(banchoUrlTextBox = new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesBanchoUrlCaption,
                    Current = banchoUrl,
                    PlaceholderText = "https://c.example.com"
                }),
                stablePasswordItem = new SettingsItemV2(stablePasswordTextBox = new FormPasswordTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesStablePasswordCaption,
                    HintText = ForkSettingsStrings.ServerProfilesStablePasswordHint,
                    Current = stablePassword,
                    PlaceholderText = "Leave empty to keep the stored password"
                }),
                stableStatusItem = new SettingsItemV2(new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesStableStatusCaption,
                    Current = stableBanchoSession?.Status ?? new Bindable<string>("Not active; connect and restart"),
                    ReadOnly = true
                }),
                clientIdItem = new SettingsItemV2(clientIdTextBox = new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesClientIdCaption,
                    Current = clientId
                }),
                clientSecretItem = new SettingsItemV2(clientSecretTextBox = new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesClientSecretCaption,
                    Current = clientSecret
                }),
                clientVersionItem = new SettingsItemV2(clientVersionTextBox = new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesVersionCaption,
                    Current = clientVersion,
                    PlaceholderText = ServerProfileManager.DEFAULT_STABLE_CLIENT_VERSION
                }),
                versionHashItem = new SettingsItemV2(versionHashTextBox = new FormTextBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesVersionHashCaption,
                    Current = versionHash
                }),
                specialRulesetsItem = new SettingsItemV2(specialRulesetsCheckbox = new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ServerProfilesSupportsSpecialRulesetsCaption,
                    HintText = ForkSettingsStrings.ServerProfilesSupportsSpecialRulesetsHint,
                    Current = supportsSpecialRulesets
                })
            };

            profileDropdown.Items = profileManager.Profiles.Select(p => p.Id).ToList();
            activeProfileId.Value = profileManager.ActiveProfile.Id;

            activeProfileId.BindValueChanged(e =>
            {
                var profile = profileManager.Profiles.FirstOrDefault(p => p.Id == e.NewValue);
                if (profile == null) return;

                isUpdatingFields = true;

                nameTextBox.ReadOnly = false;
                apiUrlTextBox.ReadOnly = false;
                websiteUrlTextBox.ReadOnly = false;
                usernameTextBox.ReadOnly = false;
                banchoUrlTextBox.ReadOnly = false;
                clientIdTextBox.ReadOnly = false;
                clientSecretTextBox.ReadOnly = false;
                clientVersionTextBox.ReadOnly = false;
                versionHashTextBox.ReadOnly = false;

                profileName.Value = profile.Name;
                apiUrl.Value = profile.ApiUrl;
                websiteUrl.Value = profile.WebsiteUrl;
                username.Value = profile.Username;
                useStableProtocol.Value = profile.UseStableProtocol;
                banchoUrl.Value = profile.BanchoUrl;
                stablePassword.Value = string.Empty;

                if (profile.IsDefault)
                {
                    clientId.Value = "Protected";
                    clientSecret.Value = "Protected";
                    clientVersion.Value = "Protected";
                    versionHash.Value = "Protected";
                    supportsSpecialRulesets.Value = false;

                    clientIdTextBox.ReadOnly = true;
                    clientSecretTextBox.ReadOnly = true;
                    clientVersionTextBox.ReadOnly = true;
                    versionHashTextBox.ReadOnly = true;

                    specialRulesetsItem.Alpha = 0;
                }
                else
                {
                    clientId.Value = profile.ClientId;
                    clientSecret.Value = profile.ClientSecret;
                    clientVersion.Value = profile.ClientVersion;
                    versionHash.Value = profile.VersionHash;
                    supportsSpecialRulesets.Value = profile.SupportsSpecialRulesets;

                    specialRulesetsItem.Alpha = 1;
                }

                deleteButton.Enabled.Value = (profile.Id != "default");

                isUpdatingFields = false;
                updateProtocolFieldVisibility();
            }, true);

            profileName.BindValueChanged(_ => saveFieldChanges());
            apiUrl.BindValueChanged(_ => saveFieldChanges());
            websiteUrl.BindValueChanged(_ => saveFieldChanges());
            username.BindValueChanged(_ => saveFieldChanges());
            banchoUrl.BindValueChanged(_ => saveFieldChanges());
            useStableProtocol.BindValueChanged(e =>
            {
                if (isUpdatingFields)
                    return;

                if (e.NewValue)
                    applyStableDefaultsToFields();

                updateProtocolFieldVisibility();
                saveFieldChanges();
            });
            clientId.BindValueChanged(_ => saveFieldChanges());
            clientSecret.BindValueChanged(_ => saveFieldChanges());
            clientVersion.BindValueChanged(_ => saveFieldChanges());
            versionHash.BindValueChanged(_ => saveFieldChanges());
            supportsSpecialRulesets.BindValueChanged(_ => saveFieldChanges());

            stablePasswordTextBox.OnCommit += (_, _) => persistStablePassword();
        }

        private void updateProtocolFieldVisibility()
        {
            bool stable = useStableProtocol.Value;

            banchoUrlItem.CanBeShown.Value = stable;
            stablePasswordItem.CanBeShown.Value = stable;
            stableStatusItem.CanBeShown.Value = stable;
            testConnectionButton.CanBeShown.Value = stable;

            clientIdItem.CanBeShown.Value = !stable;
            clientSecretItem.CanBeShown.Value = !stable;
            // The stable protocol sends this value during login and score submission.
            // It must remain visible and editable because accepted versions differ by server.
            clientVersionItem.CanBeShown.Value = true;
            versionHashItem.CanBeShown.Value = !stable;
            specialRulesetsItem.CanBeShown.Value = !stable;
        }

        private void applyStableDefaultsToFields()
        {
            isUpdatingFields = true;

            if (!ServerProfileManager.IsValidStableClientVersion(clientVersion.Value))
                clientVersion.Value = ServerProfileManager.DEFAULT_STABLE_CLIENT_VERSION;
            clientId.Value = "Not used by stable";
            clientSecret.Value = "Not used by stable";
            versionHash.Value = "Not used by stable";
            supportsSpecialRulesets.Value = false;

            if (string.IsNullOrWhiteSpace(banchoUrl.Value) || banchoUrl.Value == "https://")
                banchoUrl.Value = deriveBanchoUrl(apiUrl.Value);

            isUpdatingFields = false;
        }

        private static string deriveBanchoUrl(string apiUrlValue)
        {
            if (!Uri.TryCreate(apiUrlValue, UriKind.Absolute, out Uri? apiUri))
                return "https://c.example.com";

            string host = apiUri.Host;
            if (host.StartsWith("osu.", StringComparison.OrdinalIgnoreCase) || host.StartsWith("api.", StringComparison.OrdinalIgnoreCase))
                host = host[(host.IndexOf('.') + 1)..];

            return $"{apiUri.Scheme}://c.{host}";
        }

        private void saveFieldChanges()
        {
            if (isUpdatingFields) return;

            var profile = profileManager.Profiles.FirstOrDefault(p => p.Id == activeProfileId.Value);
            if (profile == null || profile.IsDefault) return;

            profile.Name = profileName.Value;
            profile.ApiUrl = apiUrl.Value;
            profile.WebsiteUrl = websiteUrl.Value;
            profile.Username = username.Value;
            profile.UseStableProtocol = useStableProtocol.Value;
            profile.BanchoUrl = banchoUrl.Value;
            profile.ClientId = clientId.Value;
            profile.ClientSecret = clientSecret.Value;
            profile.ClientVersion = clientVersion.Value;
            profile.VersionHash = versionHash.Value;
            profile.SupportsSpecialRulesets = supportsSpecialRulesets.Value;

            if (profile.UseStableProtocol)
            {
                ServerProfileManager.EnsureStableDefaults(profile);

                if (clientVersion.Value != profile.ClientVersion)
                {
                    isUpdatingFields = true;
                    clientVersion.Value = profile.ClientVersion;
                    isUpdatingFields = false;
                }
            }

            profileManager.SaveProfiles();

            var selected = activeProfileId.Value;
            isUpdatingFields = true;
            profileDropdown.Items = profileManager.Profiles.Select(p => p.Id).ToList();
            activeProfileId.Value = selected;
            isUpdatingFields = false;
        }

        private void addNewProfile()
        {
            var newProfile = new ServerProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = "New Server Profile",
                ApiUrl = "https://",
                WebsiteUrl = "https://",
                BanchoUrl = "https://",
                ClientId = "",
                ClientSecret = "",
                ClientVersion = "",
                VersionHash = "",
                SupportsSpecialRulesets = false,
                IsDefault = false
            };

            profileManager.AddProfile(newProfile);

            isUpdatingFields = true;
            profileDropdown.Items = profileManager.Profiles.Select(p => p.Id).ToList();
            activeProfileId.Value = newProfile.Id;
            isUpdatingFields = false;

            activeProfileId.TriggerChange();
        }

        private void deleteProfile()
        {
            var selectedId = activeProfileId.Value;
            if (selectedId == "default") return;

            profileManager.DeleteProfile(selectedId);

            isUpdatingFields = true;
            profileDropdown.Items = profileManager.Profiles.Select(p => p.Id).ToList();
            activeProfileId.Value = profileManager.ActiveProfile.Id;
            isUpdatingFields = false;

            activeProfileId.TriggerChange();
        }

        private void connectAndRestart()
        {
            persistStablePassword();
            if (!profileManager.SelectProfile(activeProfileId.Value))
                return;

            Scheduler.Add(() =>
            {
                if (game != null)
                {
                    game.RestartOnExitAction = () =>
                    {
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = Environment.ProcessPath,
                                UseShellExecute = false,
                            });
                    };
                    game.Exit();
                }
            });
        }

        private void requestConnection()
        {
            var profile = profileManager.Profiles.FirstOrDefault(p => p.Id == activeProfileId.Value);
            if (profile == null)
                return;

            if (ServerProfileManager.TryGetForbiddenServer(profile, out string serverName))
            {
                dialogOverlay?.Push(new ForbiddenServerDialog(serverName));
                return;
            }

            dialogOverlay?.Push(new ConfirmDialog(ForkSettingsStrings.ServerProfilesRestartBody, connectAndRestart));
        }

        private async Task testConnectionAsync()
        {
            if (!testConnectionButton.Enabled.Value)
                return;

            persistStablePassword();

            var profile = profileManager.Profiles.FirstOrDefault(p => p.Id == activeProfileId.Value);
            if (profile == null)
                return;

            if (ServerProfileManager.TryGetForbiddenServer(profile, out string serverName))
            {
                dialogOverlay?.Push(new ForbiddenServerDialog(serverName));
                return;
            }

            testConnectionButton.Enabled.Value = false;
            testConnectionButton.Text = ForkSettingsStrings.ServerProfilesTestingConnectionBtn;

            try
            {
                StableBanchoLoginResult login;

                if (profile.Id == profileManager.ActiveProfile.Id && stableBanchoSession != null)
                {
                    login = await stableBanchoSession.LoginNowAsync().ConfigureAwait(false);
                }
                else
                {
                    using var client = new StableScoreSubmissionClient(profile);
                    login = await client.TestConnectionAsync().ConfigureAwait(false);
                }

                showConnectionTestResult(
                    ForkSettingsStrings.ServerProfilesTestSuccessTitle,
                    ForkSettingsStrings.ServerProfilesTestSuccessBody(login.Username, login.UserId.ToString(), profile.BanchoUrl),
                    true);
            }
            catch (Exception ex)
            {
                showConnectionTestResult(
                    ForkSettingsStrings.ServerProfilesTestFailureTitle,
                    ForkSettingsStrings.ServerProfilesTestFailureBody(profile.BanchoUrl, ex.Message),
                    false);
            }
            finally
            {
                Schedule(() =>
                {
                    testConnectionButton.Text = ForkSettingsStrings.ServerProfilesTestConnectionBtn;
                    testConnectionButton.Enabled.Value = true;
                });
            }
        }

        private void showConnectionTestResult(LocalisableString title, LocalisableString body, bool success)
            => Schedule(() => dialogOverlay?.Push(new ConnectionTestResultDialog(title, body, success)));

        private void persistStablePassword()
        {
            string newPassword = stablePassword.Value;
            if (string.IsNullOrEmpty(newPassword))
                return;

            var profile = profileManager.Profiles.FirstOrDefault(p => p.Id == activeProfileId.Value);
            if (profile == null || profile.IsDefault)
                return;

            profile.StablePasswordHash = newPassword.ComputeMD5Hash();
            profileManager.SaveProfiles();
            stablePassword.Value = string.Empty;
        }

        private sealed partial class ForbiddenServerDialog : PopupDialog
        {
            public ForbiddenServerDialog(string serverName)
            {
                HeaderText = ForkSettingsStrings.ServerProfilesForbiddenTitle;
                BodyText = ForkSettingsStrings.ServerProfilesForbiddenBody(serverName);
                Icon = FontAwesome.Solid.Ban;

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = @"OK",
                    },
                };
            }
        }

        private sealed partial class ConnectionTestResultDialog : PopupDialog
        {
            public ConnectionTestResultDialog(LocalisableString title, LocalisableString body, bool success)
            {
                HeaderText = title;
                BodyText = body;
                Icon = success ? FontAwesome.Solid.CheckCircle : FontAwesome.Solid.ExclamationTriangle;

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = @"OK",
                    },
                };
            }
        }

        private partial class ProfileDropdown : FormDropdown<string>
        {
            private readonly ServerProfileManager profileManager;

            public ProfileDropdown(ServerProfileManager profileManager)
            {
                this.profileManager = profileManager;
            }

            protected override LocalisableString GenerateItemText(string item)
            {
                var profile = profileManager.Profiles.FirstOrDefault(p => p.Id == item);
                return profile?.Name ?? item;
            }
        }
    }
}
