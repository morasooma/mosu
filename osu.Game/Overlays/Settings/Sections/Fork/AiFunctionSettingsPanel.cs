using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds.AI;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public enum AiFunction
    {
        Nsfw,
        BackgroundDistortion,
        AudioRecommendations,
    }

    /// <summary>One shared settings panel, with a sidebar destination for each AI function.</summary>
    public partial class AiFunctionSettingsPanel : SettingsSubPanel
    {
        private AiFunction requestedFunction;
        private bool selectionPending;

        protected override Drawable CreateHeader() => new SettingsHeader(AiFunctionsStrings.Title, AiFunctionsStrings.SettingsDescription);

        public AiFunctionSettingsPanel()
        {
            AddSection(new NsfwSection());
            AddSection(new DistortionSection());
            AddSection(new AudioSection());
        }

        public void ShowFunction(AiFunction function)
        {
            requestedFunction = function;
            Show();
            if (selectionPending) return;
            selectionPending = true;
            Schedule(selectRequestedFunction);
        }

        private void selectRequestedFunction()
        {
            if (State.Value != Visibility.Visible)
            {
                selectionPending = false;
                return;
            }
            if (!SectionsContainer.Any())
            {
                Schedule(selectRequestedFunction);
                return;
            }
            SearchTextBox.Current.SetDefault();
            var section = SectionsContainer.Single(s => requestedFunction switch
            {
                AiFunction.Nsfw => s is NsfwSection,
                AiFunction.AudioRecommendations => s is AudioSection,
                _ => s is DistortionSection,
            });
            SectionsContainer.ScrollTo(section);
            SectionsContainer.SelectedSection.Value = section;
            selectionPending = false;
        }

        public partial class NsfwSection : SettingsSection
        {
            public override LocalisableString Header => AiFunctionsStrings.Nsfw;
            public override Drawable CreateIcon() => new SpriteIcon { Icon = FontAwesome.Solid.EyeSlash };

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, BackgroundSafetyService? service = null)
            {
                OsuTextFlowContainer status;
                Children = new Drawable[]
                {
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = AiFunctionsStrings.Nsfw,
                        HintText = AiFunctionsStrings.Description,
                        Current = config.GetBindable<bool>(OsuSetting.ForkAiBackgroundBlur),
                    })
                    {
                        FavouriteId = "OsuSetting.ForkAiBackgroundBlur",
                    },
                    new SettingsItemV2(new FormEnumDropdown<BackgroundFilterMode>
                    {
                        Caption = AiFunctionsStrings.Categories,
                        Current = config.GetBindable<BackgroundFilterMode>(OsuSetting.ForkAiBackgroundFilterMode),
                    })
                    {
                        FavouriteId = "OsuSetting.ForkAiBackgroundFilterMode",
                    },
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = AiFunctionsStrings.GeneralModel,
                        Current = config.GetBindable<bool>(OsuSetting.ForkAiGeneralModel),
                    })
                    {
                        FavouriteId = "OsuSetting.ForkAiGeneralModel",
                    },
                    new SettingsItemV2(new FormSliderBar<double>
                    {
                        Caption = AiFunctionsStrings.GeneralThreshold,
                        HintText = AiFunctionsStrings.ThresholdHint,
                        Current = config.GetBindable<double>(OsuSetting.ForkAiGeneralThreshold),
                        KeyboardStep = 0.01f,
                        DisplayAsPercentage = true,
                    })
                    {
                        FavouriteId = "OsuSetting.ForkAiGeneralThreshold",
                    },
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = AiFunctionsStrings.AnimeModel,
                        Current = config.GetBindable<bool>(OsuSetting.ForkAiAnimeModel),
                    })
                    {
                        FavouriteId = "OsuSetting.ForkAiAnimeModel",
                    },
                    new SettingsItemV2(new FormSliderBar<double>
                    {
                        Caption = AiFunctionsStrings.AnimeThreshold,
                        HintText = AiFunctionsStrings.ThresholdHint,
                        Current = config.GetBindable<double>(OsuSetting.ForkAiAnimeThreshold),
                        KeyboardStep = 0.01f,
                        DisplayAsPercentage = true,
                    })
                    {
                        FavouriteId = "OsuSetting.ForkAiAnimeThreshold",
                    },
                    new SettingsItemV2(new FormEnumDropdown<BackgroundSafetyCombination>
                    {
                        Caption = AiFunctionsStrings.Combination,
                        HintText = AiFunctionsStrings.CombinationHint,
                        Current = config.GetBindable<BackgroundSafetyCombination>(OsuSetting.ForkAiCombination),
                    })
                    {
                        FavouriteId = "OsuSetting.ForkAiCombination",
                    },
                    new SettingsItemV2(new FormSliderBar<double>
                    {
                        Caption = AiFunctionsStrings.BlurStrength,
                        Current = config.GetBindable<double>(OsuSetting.ForkAiBlurStrength),
                        KeyboardStep = 1,
                    })
                    {
                        FavouriteId = "OsuSetting.ForkAiBlurStrength",
                    },
                    new SettingsButtonV2
                    {
                        Text = AiFunctionsStrings.Reset,
                        Action = () =>
                        {
                            config.SetValue(OsuSetting.ForkAiGeneralModel, true);
                            config.SetValue(OsuSetting.ForkAiAnimeModel, true);
                            config.SetValue(OsuSetting.ForkAiGeneralThreshold, 0.3);
                            config.SetValue(OsuSetting.ForkAiAnimeThreshold, 0.3);
                            config.SetValue(OsuSetting.ForkAiCombination, BackgroundSafetyCombination.Any);
                            config.SetValue(OsuSetting.ForkAiBackgroundFilterMode, BackgroundFilterMode.Suggestive);
                            config.SetValue(OsuSetting.ForkAiBlurStrength, 16.0);
                        },
                    },
                    status = CreateStatus(AiFunctionsStrings.Disabled),
                };
                service?.Status.BindValueChanged(v => status.Text = v.NewValue, true);
            }
        }

        public partial class DistortionSection : SettingsSection
        {
            public override LocalisableString Header => AiFunctionsStrings.Distortion;
            public override Drawable CreateIcon() => new SpriteIcon { Icon = FontAwesome.Solid.Image };

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, BackgroundSafetyService? service = null, StillLifeService? stillLife = null)
            {
                OsuTextFlowContainer stillLifeStatus;
                Add(new SettingsItemV2(new FormCheckBox
                {
                    Caption = AiFunctionsStrings.StillLife,
                    HintText = AiFunctionsStrings.StillLifeHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkAiStillLife),
                }) {
                    FavouriteId = "OsuSetting.ForkAiStillLife", Keywords = new[] { "still life", "ai", "cpu", "gpu", "vulkan", "ии", "перерисовка" } });
                Add(new SettingsItemV2(new FormEnumDropdown<StillLifeBackend>
                {
                    Caption = AiFunctionsStrings.Backend,
                    Current = config.GetBindable<StillLifeBackend>(OsuSetting.ForkAiStillLifeBackend),
                })
                {
                    FavouriteId = "OsuSetting.ForkAiStillLifeBackend",
                });
                Add(new SettingsItemV2(new FormEnumDropdown<StillLifeResolution>
                {
                    Caption = AiFunctionsStrings.Resolution,
                    Current = config.GetBindable<StillLifeResolution>(OsuSetting.ForkAiStillLifeSize),
                })
                {
                    FavouriteId = "OsuSetting.ForkAiStillLifeSize",
                });
                Add(new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = AiFunctionsStrings.Passes,
                    Current = config.GetBindable<int>(OsuSetting.ForkAiStillLifePasses),
                    KeyboardStep = 1,
                })
                {
                    FavouriteId = "OsuSetting.ForkAiStillLifePasses",
                });
                Add(new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = AiFunctionsStrings.Steps,
                    Current = config.GetBindable<int>(OsuSetting.ForkAiStillLifeSteps),
                    KeyboardStep = 1,
                })
                {
                    FavouriteId = "OsuSetting.ForkAiStillLifeSteps",
                });
                Add(new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = AiFunctionsStrings.Strength,
                    Current = config.GetBindable<double>(OsuSetting.ForkAiStillLifeStrength),
                    KeyboardStep = .01f,
                    DisplayAsPercentage = true,
                })
                {
                    FavouriteId = "OsuSetting.ForkAiStillLifeStrength",
                });
                Add(new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = AiFunctionsStrings.Threads,
                    Current = config.GetBindable<int>(OsuSetting.ForkAiStillLifeThreads),
                    KeyboardStep = 1,
                })
                {
                    FavouriteId = "OsuSetting.ForkAiStillLifeThreads",
                });
                Add(new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = AiFunctionsStrings.Seed,
                    Current = config.GetBindable<int>(OsuSetting.ForkAiStillLifeSeed),
                    KeyboardStep = 1,
                })
                {
                    FavouriteId = "OsuSetting.ForkAiStillLifeSeed",
                });
                Add(new SettingsItemV2(new FormCheckBox
                {
                    Caption = AiFunctionsStrings.CacheEnabled,
                    Current = config.GetBindable<bool>(OsuSetting.ForkAiCacheEnabled),
                })
                {
                    FavouriteId = "OsuSetting.ForkAiCacheEnabled",
                });
                Add(new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = AiFunctionsStrings.CacheLimit,
                    Current = config.GetBindable<int>(OsuSetting.ForkAiCacheMegabytes),
                    KeyboardStep = 64,
                })
                {
                    FavouriteId = "OsuSetting.ForkAiCacheMegabytes",
                });
                Add(new SettingsButtonV2
                {
                    Text = AiFunctionsStrings.ClearCache,
                    Action = () => { service?.ClearArtworkCache(); stillLife?.ClearCache(); },
                });
                Add(stillLifeStatus = CreateStatus(AiFunctionsStrings.StillLifeDisabled));
                stillLifeStatus.Name = "background-distortion-status";
                stillLife?.Status.BindValueChanged(v => stillLifeStatus.Text = v.NewValue, true);
            }
        }

        public partial class AudioSection : SettingsSection
        {
            public override LocalisableString Header => DiscoveryStrings.AudioTitle;
            public override Drawable CreateIcon() => new SpriteIcon { Icon = FontAwesome.Solid.Music };

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config, osu.Game.Overlays.Discovery.DiscoveryAudioService? service = null)
            {
                OsuTextFlowContainer status;
                Children = new Drawable[]
                {
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = DiscoveryStrings.AudioTitle,
                        HintText = DiscoveryStrings.AudioHint,
                        Current = config.GetBindable<bool>(OsuSetting.ForkAiAudioRecommendations),
                    }) { FavouriteId = "OsuSetting.ForkAiAudioRecommendations" },
                    new SettingsItemV2(new FormCheckBox
                    {
                        Caption = DiscoveryStrings.AudioBackgroundSearch,
                        HintText = DiscoveryStrings.AudioBackgroundSearchHint,
                        Current = config.GetBindable<bool>(OsuSetting.ForkAiAudioBackgroundSearch),
                    }) { FavouriteId = "OsuSetting.ForkAiAudioBackgroundSearch" },
                    new SettingsItemV2(new FormEnumDropdown<osu.Game.Overlays.Discovery.DiscoveryAudioDevice>
                    {
                        Caption = AiFunctionsStrings.Backend,
                        Current = config.GetBindable<osu.Game.Overlays.Discovery.DiscoveryAudioDevice>(OsuSetting.ForkAiAudioDevice),
                    }) { FavouriteId = "OsuSetting.ForkAiAudioDevice" },
                    new SettingsItemV2(new FormSliderBar<int>
                    {
                        Caption = DiscoveryStrings.AudioCandidates,
                        Current = config.GetBindable<int>(OsuSetting.ForkAiAudioCandidates),
                        KeyboardStep = 1,
                    }) { FavouriteId = "OsuSetting.ForkAiAudioCandidates" },
                    new SettingsButtonV2 { Text = DiscoveryStrings.AudioClearCache, Action = () => service?.ClearCache() },
                    status = CreateStatus(DiscoveryStrings.AudioDisabled),
                };
                service?.Status.BindValueChanged(v => status.Text = v.NewValue, true);
            }
        }

        private static OsuTextFlowContainer CreateStatus(LocalisableString text) => new OsuTextFlowContainer(s => s.Font = OsuFont.GetFont(size: 14))
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = SettingsPanel.CONTENT_PADDING,
            Text = text,
        };
    }
}
